#!/usr/bin/env python3
"""Accepted lines per week, from git history (ROADMAP section 7, point 6).

Counts net lines (added minus deleted) of tracked source files per ISO week,
split into the classes used by docs/ai/estimate/roadmap_estimate.py:

  translated  src/Core/Tg/** except Builtin/ and Generated/ (translated tg code)
  new_logic   src/** otherwise (Kernel, DmShim, Tg/Builtin, Server, ...) and tools/** (except blender)
  client      godot/**, tools/blender/**   (counts double in the weighted figure)
  tests       tests/**
  generated   any path with /Generated/ or a .g.cs suffix (reported, never weighted)

Net lines per week are stable against rewrites and cheap to compute; they are not
"lines present at HEAD attributed to the week that added them". Renames are
counted as a deletion in the old class and an addition in the new one
(--no-renames), so moving a file between classes is not lost.

The estimate counts translated volume in DM lines ("1 DM line ~ 1 C# line"),
but a C# translation is longer than its DM source. So the script also measures
the C#/DM ratio at HEAD: every translated file names its source in the header
line "// Translated from tgstation (snapshot 1d99bd7f): code/<path>.dm"; the
ratio is (non-blank, non-comment C# lines of those files) / (non-blank,
non-comment DM lines of the named sources). The "translated_dm" column divides
the weekly translated figure by that ratio; the weighted figure uses it.
Partially translated sources make the ratio an underestimate of C# verbosity
(and translated_dm an overestimate); the number of files and the ratio are
printed so the reader can judge.

Usage: tools/measure_velocity.py [--since YYYY-MM-DD] [--csv] [--tg ../tgstation]
"""
import argparse
import collections
import datetime
import os
import re
import subprocess
import sys

GENERATED_MARKERS = ("/Generated/", ".g.cs", ".Designer.cs")
CODE_EXTS = (".cs", ".py", ".gd", ".gdshader", ".shader", ".sh")
HEADER_RE = re.compile(r"^// Translated from tgstation \(snapshot [0-9a-f]+\): (code/\S+\.dm)")
SKIP_MARKERS = ("// SKELETON", "// PARTIAL", "Partly translated", "// Partial")


def classify(path):
    if any(m in path for m in GENERATED_MARKERS):
        return "generated"
    if path.startswith("tests/"):
        return "tests"
    if path.startswith("godot/") or path.startswith("tools/blender/"):
        return "client"
    if path.startswith("src/Core/Tg/") and not path.startswith("src/Core/Tg/Builtin/"):
        return "translated"
    if path.startswith("src/") or path.startswith("tools/"):
        return "new_logic"
    return None  # docs and project files are not counted


def code_lines(text, comment_prefix):
    """Non-blank lines that are not whole-line comments (block comments are counted; good enough for a ratio)."""
    n = 0
    for line in text.splitlines():
        s = line.strip()
        if s and not s.startswith(comment_prefix):
            n += 1
    return n


def translation_ratio(repo_root, tg_root):
    """(ratio, files, cs_lines, dm_lines) over translated files at HEAD that name a readable DM source."""
    files = subprocess.run(["git", "ls-files", "src/Core/Tg"], cwd=repo_root, capture_output=True, text=True, check=True).stdout.split()
    cs_total = dm_total = count = 0
    dm_seen = set()
    for f in files:
        if classify(f) != "translated" or not f.endswith(".cs"):
            continue
        with open(os.path.join(repo_root, f), encoding="utf-8") as fh:
            text = fh.read()
        m = HEADER_RE.match(text)
        if not m:
            continue
        # Skeletons and partial mirrors name a whole .dm file in their header but translate only a few members of
        # it; counting them would make the ratio meaningless. Only fully translated files measure C# verbosity.
        if any(marker in text for marker in SKIP_MARKERS):
            continue
        dm_path = os.path.join(tg_root, m.group(1))
        if not os.path.exists(dm_path):
            continue
        count += 1
        cs_total += code_lines(text, "//")
        if dm_path not in dm_seen:  # a DM file split over several C# files is counted once
            dm_seen.add(dm_path)
            with open(dm_path, encoding="utf-8", errors="replace") as fh:
                dm_total += code_lines(fh.read(), "//")
    if dm_total == 0:
        return None, 0, 0, 0
    return cs_total / dm_total, count, cs_total, dm_total


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--since", default=None)
    ap.add_argument("--csv", action="store_true")
    ap.add_argument("--tg", default=None, help="tgstation clone (default: ../tgstation next to the repository)")
    args = ap.parse_args()

    repo_root = subprocess.run(["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True, check=True).stdout.strip()
    tg_root = args.tg or os.path.join(os.path.dirname(repo_root), "tgstation")

    cmd = ["git", "log", "--numstat", "--no-renames", "--date=short", "--pretty=format:@@%H|%ad|%an|%s", "--no-merges"]
    if args.since:
        cmd.append(f"--since={args.since}")
    out = subprocess.run(cmd, cwd=repo_root, capture_output=True, text=True, check=True).stdout

    weeks = collections.defaultdict(collections.Counter)
    commits = collections.Counter()
    week = None
    for line in out.splitlines():
        if line.startswith("@@"):
            _, date, _, _ = line[2:].split("|", 3)
            iso = datetime.date.fromisoformat(date).isocalendar()
            week = f"{iso[0]}-W{iso[1]:02d}"
            commits[week] += 1
            continue
        if not line.strip() or week is None:
            continue
        added, deleted, path = line.split("\t", 2)
        if added == "-" or not path.endswith(CODE_EXTS):
            continue
        cls = classify(path)
        if cls is None:
            continue
        weeks[week][cls] += int(added) - int(deleted)

    ratio, nfiles, cs_lines, dm_lines = translation_ratio(repo_root, tg_root)
    r = ratio if ratio else 1.0

    classes = ["translated", "new_logic", "client", "tests", "generated"]
    rows = []
    for w in sorted(weeks):
        c = weeks[w]
        translated_dm = round(c["translated"] / r)
        weighted = translated_dm + c["new_logic"] + 2 * c["client"] + c["tests"]
        rows.append((w, commits[w], *(c[k] for k in classes), translated_dm, weighted))

    header = ["week", "commits", *classes, "translated_dm", "weighted"]
    if args.csv:
        print(",".join(header))
        for row in rows:
            print(",".join(str(x) for x in row))
        return 0

    print(f"{'week':9} {'commits':>7} " + " ".join(f"{k:>13}" for k in header[2:]))
    for row in rows:
        print(f"{row[0]:9} {row[1]:>7} " + " ".join(f"{x:>13}" for x in row[2:]))
    tot = [sum(row[i] for row in rows) for i in range(1, len(header))]
    print(f"{'total':9} {tot[0]:>7} " + " ".join(f"{x:>13}" for x in tot[1:]))
    print()
    if ratio:
        print(f"C#/DM ratio at HEAD: {ratio:.2f} ({nfiles} translated files, {cs_lines} C# code lines over {dm_lines} DM code lines).")
    else:
        print("C#/DM ratio at HEAD: no translated files with a readable DM source yet; translated_dm uses ratio 1.0.")
    print("weighted = translated_dm + new_logic + 2*client + tests (generated excluded); net lines (added - deleted).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
