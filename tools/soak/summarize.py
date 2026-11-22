#!/usr/bin/env python3
"""Writes the results file of a soak run (tools/soak/run.sh, D100).

Reads what the run left in its output folder:
  - the server's metrics CSV (--metrics soak.csv, one row per tick: tick, poll_us, step_us, replication_us, send_us,
    total_us, lateness_us, overrun, sessions, bytes_out, bytes_in, alloc_bytes, gc0, gc1, gc2, heap_bytes in M1, and
    mobs_fired in M2: 1 in the ticks SSmobs fired in, the ticks that run every living mob's Life): every
    numeric column is summarized (n, min, mean, p50, p95, p99, p99.9, max, sum; columns in microseconds are shown in
    milliseconds), and the columns that look like the numbers the soak is about (tick time, overruns, GC, bytes) are
    repeated as a headline; the bytes per player per second are derived from bytes and sessions;
  - the server log (summary lines, the runtime count, how it ended);
  - the bots' JSON report (Anomaly.Tools.Bots --report): joins, moves, desyncs, disconnects, RTT, bytes per bot;
  - the Godot client logs (errors).
It does not know the CSV's column names (P3 owns them): everything is found by name patterns in HEADLINE below, so a
renamed column only moves a line from the headline to the full table.

Output: results.txt (for people) and results.json (for scripts) next to the inputs. Exit status 3 when there are
flags and --strict is given, 0 otherwise.
"""

import argparse
import csv
import glob
import json
import math
import os
import re
import sys

# Name patterns for the headline (case-insensitive, searched in the column name; a column name ending in _us has been
# renamed to _ms and divided by 1000 by then). The first item is the label.
TICK_TOTAL = r"^total$|(total|tick).*(ms|time)|(ms|time).*(total|tick)"
HEADLINE = [
    ("tick total (ms)", TICK_TOTAL),
    ("world step (ms)", r"step"),
    ("replication (ms)", r"repl"),
    ("network poll and send (ms)", r"poll|send|net"),
    ("pacer lateness (ms)", r"late"),
    ("overruns (ticks that overran)", r"overrun"),
    ("GC (collections per tick, allocation per tick, heap)", r"gc|alloc|heap|pause"),
    ("bytes per tick (all sessions) and sessions", r"bytes|session"),
    ("runtimes", r"runtime"),
]
# Columns that count events per row: their sum is the number that matters, not their percentiles.
COUNT_COLUMNS = r"overrun|^gc\d|runtime"

# A flag is raised when a number is outside what a healthy soak shows. Tick budget: 50 ms at 20 TPS.
TICK_BUDGET_MS = 50.0


def percentile(sorted_values, p):
    """Nearest rank, p in [0, 100]."""
    if not sorted_values:
        return float("nan")
    rank = max(1, math.ceil(p / 100.0 * len(sorted_values)))
    return sorted_values[min(rank, len(sorted_values)) - 1]


def read_csv(path):
    """Returns (rows, columns) with columns = {name: [float, ...]} for the numeric columns, in header order."""
    columns = {}
    rows = 0
    header = None
    with open(path, newline="") as handle:
        for record in csv.reader(handle):
            if not record or record[0].startswith("#"):
                continue
            if header is None:
                header = [h.strip() for h in record]
                columns = {name: [] for name in header}
                continue
            rows += 1
            for name, cell in zip(header, record):
                try:
                    columns[name].append(float(cell))
                except ValueError:
                    pass  # a text cell: the column is numeric only if most of its cells are
    numeric = {}
    for name, values in columns.items():
        if not values or len(values) * 2 < rows:
            continue
        if name.endswith("_us"):
            name = name[:-3] + "_ms"
            values = [v / 1000.0 for v in values]
        numeric[name] = values
    return rows, numeric


def summarize_column(values):
    ordered = sorted(values)
    return {
        "n": len(values),
        "min": ordered[0],
        "mean": sum(values) / len(values),
        "p50": percentile(ordered, 50),
        "p95": percentile(ordered, 95),
        "p99": percentile(ordered, 99),
        "p99.9": percentile(ordered, 99.9),
        "max": ordered[-1],
        "sum": sum(values),
    }


def fmt(value):
    if isinstance(value, float):
        if math.isnan(value):
            return "-"
        if abs(value) >= 1000 or value == int(value):
            return "%.0f" % value
        return "%.3f" % value
    return str(value)


def table(lines, stats_by_name):
    header = ["column", "n", "min", "mean", "p50", "p95", "p99", "p99.9", "max", "sum"]
    rows = [header]
    for name, s in stats_by_name.items():
        rows.append([name] + [fmt(s[key]) for key in header[1:]])
    widths = [max(len(row[i]) for row in rows) for i in range(len(header))]
    for index, row in enumerate(rows):
        lines.append("  " + "  ".join(cell.ljust(widths[0]) if i == 0 else cell.rjust(widths[i]) for i, cell in enumerate(row)))
        if index == 0:
            lines.append("  " + "  ".join("-" * w for w in widths))


def tail(path, count):
    try:
        with open(path, errors="replace") as handle:
            return handle.read().splitlines()[-count:]
    except OSError:
        return []


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", required=True, help="the run's output folder")
    parser.add_argument("--csv", help="server metrics CSV (default OUT/soak.csv)")
    parser.add_argument("--server-log", help="default OUT/server.log")
    parser.add_argument("--bots", help="bots report JSON (default OUT/bots.json)")
    parser.add_argument("--meta", action="append", default=[], help="key=value describing the run (repeatable)")
    parser.add_argument("--strict", action="store_true", help="exit 3 when there are flags")
    args = parser.parse_args()

    out = args.out
    csv_path = args.csv or os.path.join(out, "soak.csv")
    log_path = args.server_log or os.path.join(out, "server.log")
    bots_path = args.bots or os.path.join(out, "bots.json")
    meta = {}
    for item in args.meta:
        key, _, value = item.partition("=")
        meta[key] = value

    flags = []
    lines = []
    result = {"meta": meta}

    lines.append("Anomaly Station soak results")
    lines.append("=" * 27)
    for key in ("started_utc", "finished_utc", "bots", "behaviour", "predict", "minutes", "godot_clients", "seed", "port", "aborted", "server_exit", "server_args"):
        if key in meta:
            lines.append("  %-14s %s" % (key, meta[key]))
    lines.append("")

    expected_bots = int(meta["bots"]) if meta.get("bots", "").isdigit() else None
    minutes = float(meta["minutes"]) if meta.get("minutes") else None
    if meta.get("aborted") == "1":
        flags.append("the run was aborted before its time (Ctrl+C or a failure): the numbers cover a partial run")
    if meta.get("server_exit") not in (None, "", "0"):
        flags.append("the server exited with status %s" % meta["server_exit"])

    # ---- server metrics ----
    lines.append("Server tick metrics (%s)" % os.path.basename(csv_path))
    lines.append("-" * 24)
    if os.path.exists(csv_path):
        rows, numeric = read_csv(csv_path)
        result["csv_rows"] = rows
        lines.append("  rows: %d" % rows)
        if minutes:
            expected_ticks = minutes * 60 * 20
            lines.append("  a run of %.1f min is %.0f ticks: %s" % (minutes, expected_ticks,
                         "one row per tick" if rows >= expected_ticks * 0.9 else "fewer rows than ticks (rows are windows or samples, so the percentiles below are over rows)"))
        stats = {name: summarize_column(values) for name, values in numeric.items()}
        result["csv_columns"] = stats
        lines.append("")
        lines.append("  Headline (columns whose names match; the full table follows):")
        used = set()
        headline = {}
        for label, pattern in HEADLINE:
            matched = {n: s for n, s in stats.items() if re.search(pattern, n, re.I) and n not in used}
            if not matched:
                lines.append("    %s: no column matches" % label)
                continue
            lines.append("    %s:" % label)
            table(lines, matched)
            used.update(matched)
            headline[label] = list(matched)
        result["csv_headline"] = headline
        lines.append("")
        lines.append("  All numeric columns:")
        table(lines, stats)

        # M2: the world step of the ticks SSmobs fired in (every 40 ticks it runs the Life of every living mob in one tick, DV3).
        fired, step_ms = numeric.get("mobs_fired"), numeric.get("step_ms")
        if fired and step_ms and len(fired) == len(step_ms):
            mobs_ticks = [v for f, v in zip(fired, step_ms) if f > 0]
            if mobs_ticks:
                ordered = sorted(mobs_ticks)
                other = sorted(v for f, v in zip(fired, step_ms) if f <= 0) or [0.0]
                mobs = {"ticks": len(mobs_ticks), "p50": percentile(ordered, 50), "p95": percentile(ordered, 95), "p99": percentile(ordered, 99), "max": ordered[-1],
                        "others_p99": percentile(other, 99), "others_max": other[-1]}
                result["ssmobs_ticks"] = mobs
                lines.append("")
                lines.append("  SSmobs ticks (world step in the %d ticks it fired in, ms): p50 %.2f, p95 %.2f, p99 %.2f, max %.2f; all other ticks: p99 %.2f, max %.2f" % (
                    mobs["ticks"], mobs["p50"], mobs["p95"], mobs["p99"], mobs["max"], mobs["others_p99"], mobs["others_max"]))
                if mobs["max"] > TICK_BUDGET_MS:
                    flags.append("an SSmobs tick took %.1f ms of the %g ms slot (the Life of every living mob runs in one tick, DV3)" % (mobs["max"], TICK_BUDGET_MS))

        # Bytes per player per second, from the per-tick totals and the session count of each tick (20 ticks a second).
        out_values, in_values, session_values = numeric.get("bytes_out"), numeric.get("bytes_in"), numeric.get("sessions")
        if out_values and in_values and session_values:
            pairs = [(o, i, n) for o, i, n in zip(out_values, in_values, session_values) if n > 0]
            if pairs:
                session_ticks = sum(n for _, _, n in pairs)
                derived = {
                    "server_bytes_out_per_player_per_s": sum(o for o, _, _ in pairs) / session_ticks * 20.0,
                    "server_bytes_in_per_player_per_s": sum(i for _, i, _ in pairs) / session_ticks * 20.0,
                    "mean_sessions": session_ticks / len(pairs),
                    "max_sessions": max(session_values),
                }
                result["derived"] = derived
                lines.append("")
                lines.append("  Per player (server counters, ticks with at least one session):")
                lines.append("    down to each player: %.0f bytes/s; up from each player: %.0f bytes/s; sessions: mean %.1f, max %d" % (
                    derived["server_bytes_out_per_player_per_s"], derived["server_bytes_in_per_player_per_s"], derived["mean_sessions"], derived["max_sessions"]))

        for name, s in stats.items():
            if re.search(r"overrun", name, re.I) and s["sum"] > 0:
                flags.append("server column %s sums to %s (ticks that took longer than their slot)" % (name, fmt(s["sum"])))
            if re.search(r"runtime", name, re.I) and s["sum"] > 0:
                flags.append("server column %s sums to %s (runtime errors in the world)" % (name, fmt(s["sum"])))
            if re.search(r"^gc2$", name, re.I) and s["sum"] > 0:
                flags.append("%s generation 2 collection(s) during the run (the server aims at none)" % fmt(s["sum"]))
            if re.search(TICK_TOTAL, name, re.I) and s["p99"] > TICK_BUDGET_MS:
                flags.append("server column %s has p99 %s over the %g ms tick" % (name, fmt(s["p99"]), TICK_BUDGET_MS))
    else:
        lines.append("  no metrics CSV was written (%s)" % csv_path)
        flags.append("no server metrics CSV")
    lines.append("")

    # ---- server log ----
    lines.append("Server log (%s)" % os.path.basename(log_path))
    lines.append("-" * 15)
    log_tail = tail(log_path, 40)
    if log_tail:
        text = "\n".join(tail(log_path, 100000))
        match = re.findall(r"runtimes=(\d+)", text)
        if match:
            result["runtimes"] = int(match[-1])
            lines.append("  runtimes (last status line): %s" % match[-1])
            if int(match[-1]) > 0:
                flags.append("the server reports %s runtime errors" % match[-1])
        for pattern, label in ((r"unhandled|exception", "exceptions in the log"), (r"^runtime at tick", "runtime lines")):
            count = len(re.findall(pattern, text, re.I | re.M))
            if count:
                lines.append("  %s: %d" % (label, count))
                if label == "exceptions in the log":
                    flags.append("the server log mentions %d exception(s)" % count)
        lines.append("  last lines:")
        for line in log_tail:
            lines.append("    " + line)
    else:
        lines.append("  empty or missing")
        flags.append("no server log")
    lines.append("")

    # ---- bots ----
    lines.append("Bots (%s)" % os.path.basename(bots_path))
    lines.append("-" * 4)
    if os.path.exists(bots_path):
        with open(bots_path) as handle:
            report = json.load(handle)
        agg = report["aggregate"]
        result["bots"] = agg
        lines.append("  behaviour %s, %d bots, run %.1f s, loop %d iterations (%d late, max %.1f ms)" % (
            report.get("behaviour"), agg["bots"], report.get("run_seconds", 0), report.get("iterations", 0),
            report.get("late_iterations", 0), report.get("max_iteration_ms", 0)))
        lines.append("  joined ever %d, at the end %d; joins %d; connected %.0f s in all (%.1f s per bot)" % (
            agg["bots_ever_joined"], agg["bots_joined_at_end"], agg["joins"], agg["connected_seconds_total"], agg["connected_seconds_mean"]))
        lines.append("  input messages sent %d (%d frames); actions sent %d (%s); shoves %d; rests %d" % (
            agg["inputs_sent"], agg.get("frames_sent", 0), agg.get("actions_sent", 0),
            ", ".join("%s %d" % (k, v) for k, v in sorted(agg.get("actions", {}).items())) or "none", agg.get("shoves", 0), agg.get("rests", 0)))
        lines.append("  own moves %d; other moves %d; deltas applied %d" % (agg["own_moves"], agg["other_moves"], agg["deltas_applied"]))
        lines.append("  lobby presses %d; round restarts ridden %d" % (agg.get("lobby_acts", 0), agg.get("round_restarts", 0)))
        if agg.get("behaviours"):
            lines.append("  behaviours: " + ", ".join("%s %d" % (k, v) for k, v in sorted(agg["behaviours"].items())))
        prediction = agg.get("prediction", {})
        if prediction.get("predicting_bots", 0) > 0:
            lines.append("  prediction (%d bots): %d reconciliations, %d corrections, %d snaps, %d bit mismatches, max error %.3f tiles, %d ring overflows" % (
                prediction["predicting_bots"], prediction["reconciliations"], prediction["corrections"], prediction["snaps"],
                prediction["mismatches"], prediction["max_error_tiles"], prediction["overflows"]))
            if prediction["snaps"] > 0:
                flags.append("%d prediction snap(s) (an error over a tile)" % prediction["snaps"])
            if prediction["overflows"] > 0:
                flags.append("%d prediction ring overflow(s): the server stopped acknowledging frames" % prediction["overflows"])
        lines.append("  bytes per bot per second (protocol payload): down %.0f, up %.0f" % (
            agg["bytes_in_per_bot_per_second"], agg["bytes_out_per_bot_per_second"]))
        lines.append("  rtt ms: mean %.1f, p50 %d, p95 %d, p99 %d, max %d (%d samples)" % (
            agg["rtt_ms"]["mean"], agg["rtt_ms"]["p50"], agg["rtt_ms"]["p95"], agg["rtt_ms"]["p99"], agg["rtt_ms"]["max"], agg["rtt_ms"]["samples"]))
        lines.append("  desyncs (replica gaps): %d; decode errors: %d; bot exceptions: %d" % (agg["desyncs"], agg["decode_errors"], agg.get("exceptions", 0)))
        lines.append("  disconnects: %d" % agg["disconnects_total"])
        for reason, count in sorted(agg["disconnects"].items()):
            lines.append("    %s: %d" % (reason, count))

        if agg["desyncs"] > 0:
            flags.append("%d bot desync(s): a replica missed a delta" % agg["desyncs"])
        if agg.get("exceptions", 0) > 0:
            flags.append("%d bot exception(s): %s" % (agg["exceptions"], agg.get("first_exception", "").splitlines()[0] if agg.get("first_exception") else ""))
        # a round restart closes every connection and the rejoins meet NotReady while the next world boots (M10 slice J)
        unexpected = sum(c for r, c in agg["disconnects"].items()
                         if not r.startswith("LocalClose") and not r.endswith(":RoundRestart") and not r.endswith(":NotReady"))
        if unexpected > 0:
            flags.append("%d unexpected bot disconnect(s) (see the reasons above)" % unexpected)
        if expected_bots is not None and agg["bots_joined_at_end"] < expected_bots and meta.get("aborted") != "1" and meta.get("behaviour", "wander") in ("wander", "idle", "churn", "mixed", "rest", "shove"):
            flags.append("only %d of %d bots were joined at the end" % (agg["bots_joined_at_end"], expected_bots))
        if report.get("late_iterations", 0) > 0.01 * max(1, report.get("iterations", 1)):
            flags.append("the bot loop was late in %d of %d iterations: the bots' own load may be off" % (report["late_iterations"], report["iterations"]))
        if agg["own_moves"] == 0 and meta.get("behaviour", "wander") in ("wander", "churn", "mixed", "shove"):
            flags.append("no bot ever moved")
    else:
        lines.append("  no bots report was written (%s)" % bots_path)
        flags.append("no bots report")
    lines.append("")

    # ---- godot clients ----
    godot_logs = sorted(glob.glob(os.path.join(out, "godot-*.log")))
    if godot_logs:
        lines.append("Godot clients")
        lines.append("-" * 13)
        for path in godot_logs:
            text = "\n".join(tail(path, 100000))
            errors = len(re.findall(r"^(ERROR|SCRIPT ERROR)", text, re.M))
            lines.append("  %s: %d lines, %d error line(s)" % (os.path.basename(path), len(text.splitlines()), errors))
            for line in tail(path, 3):
                lines.append("    " + line)
            if errors:
                flags.append("%s has %d Godot error line(s)" % (os.path.basename(path), errors))
        lines.append("")

    lines.append("Flags")
    lines.append("-" * 5)
    if flags:
        for flag in flags:
            lines.append("  - " + flag)
    else:
        lines.append("  none")
    result["flags"] = flags

    text = "\n".join(lines) + "\n"
    with open(os.path.join(out, "results.txt"), "w") as handle:
        handle.write(text)
    with open(os.path.join(out, "results.json"), "w") as handle:
        json.dump(result, handle, indent=1, default=str)
    sys.stdout.write(text)
    return 3 if (flags and args.strict) else 0


if __name__ == "__main__":
    sys.exit(main())
