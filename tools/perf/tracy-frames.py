"""Sums up a Tracy capture of the game client (tools/perf/fly.sh): each leg's frame times and its longest frames with their zones.

    python tools/perf/tracy-frames.py ZONES.csv [MESSAGES.csv]

ZONES.csv is `tracy-csvexport -u -p capture.tracy` (every zone and plot point), MESSAGES.csv is `tracy-csvexport -m capture.tracy`.
The client sends the frame time and the engine's counters as plots once a frame (godot/scripts/Dev/FrameProbe.cs) and a message
at the world's reveal and at each step of its --keys script; the legs are the spans between a "script: ... W+" and the next "W-".
"""
import bisect
import csv
import sys
from collections import defaultdict

PLOTS = ('render cpu ms', 'render gpu ms', 'jit ms', 'draw calls', 'draw calls: shadows', 'shadows switched on', 'shadow casters changed',
         'pipelines draw', 'pipelines specialization', 'gc pause ms')


IN_FRAME = ('shadows switched on', 'shadow casters changed')


def load(path):
    frames, plots, zones = [], defaultdict(list), []
    with open(path, newline='', encoding='utf-8', errors='replace') as f:
        for row in csv.DictReader(f):
            ns = int(row['ns_since_start'])
            if row['exec_time_ns'] == '':
                if row['name'] == 'frame ms':
                    frames.append((ns, float(row['value'])))
                else:
                    plots[row['name']].append((ns, float(row['value'])))
            else:
                zones.append((ns, int(row['exec_time_ns']), row['name'], row['value']))
    frames.sort()
    zones.sort()
    return frames, plots, zones


def legs(messages_path, frames):
    if not messages_path:
        return [('all', frames[0][0], frames[-1][0])]
    marks = []
    with open(messages_path, encoding='utf-8', errors='replace') as f:
        next(f, None)  # MessageName,total_ns; a message's own commas are not quoted, so the time is the last field
        for line in f:
            text, _, ns = line.rstrip().rpartition(',')
            if ns.isdigit():
                marks.append((int(ns), text))
    marks.sort()
    out, start, n = [], None, 0
    for ns, text in marks:
        if text.startswith('script:') and text.endswith('W+'):
            start = ns
        elif text.startswith('script:') and text.endswith('W-') and start is not None:
            n += 1
            out.append(('leg %d (%s)' % (n, 'first sight' if n == 1 else 'again'), start, ns))
            start = None
    return out or [('all', frames[0][0], frames[-1][0])]


def pct(values, q):
    s = sorted(values)
    return s[min(len(s) - 1, int(q * len(s)))]


def main():
    frames, plots, zones = load(sys.argv[1])
    zone_starts = [z[0] for z in zones]
    plot_at = {}
    for name, points in plots.items():
        points.sort()
        plot_at[name] = ([p[0] for p in points], [p[1] for p in points])

    def plot(name, ns, ms=0.0):
        # FrameProbe's plots of a frame are sent right after its "frame ms"; the others (IN_FRAME) are summed over the frame
        if name not in plot_at:
            return 0.0
        keys, values = plot_at[name]
        if name in IN_FRAME:
            lo, hi = bisect.bisect_right(keys, ns - int(ms * 1e6)), bisect.bisect_right(keys, ns)
            return sum(values[lo:hi])
        i = bisect.bisect_left(keys, ns)
        return values[i] if i < len(keys) and keys[i] - ns < 2_000_000 else 0.0

    def frame_zones(end_ns, ms):
        start = end_ns - int(ms * 1e6)
        i = bisect.bisect_left(zone_starts, start)
        by_name, by_item = defaultdict(float), defaultdict(float)
        while i < len(zones) and zones[i][0] < end_ns:
            _, dur, name, text = zones[i]
            by_name[name] += dur / 1e6
            if text:
                by_item[name + ' ' + text] += dur / 1e6
            i += 1
        return by_name, by_item

    for title, start, end in legs(sys.argv[2] if len(sys.argv) > 2 else None, frames):
        leg = [(ns, ms) for ns, ms in frames if start <= ns <= end]
        if len(leg) < 10:
            continue
        ms = [m for _, m in leg]
        seconds = sum(ms) / 1000
        print('%s: %d frames in %.1f s, %.1f fps; p50 %.1f ms, p95 %.1f, p99 %.1f, max %.1f; over 33 ms %d, over 50 %d, over 100 %d'
              % (title, len(ms), seconds, len(ms) / seconds, pct(ms, .5), pct(ms, .95), pct(ms, .99), max(ms),
                 sum(m > 33 for m in ms), sum(m > 50 for m in ms), sum(m > 100 for m in ms)))
        slow = [(ns, m) for ns, m in leg if m > 33]
        totals, items, sums = defaultdict(float), defaultdict(float), defaultdict(float)
        for ns, m in slow:
            by_name, by_item = frame_zones(ns, m)
            for k, v in by_name.items():
                totals[k] += v
            for k, v in by_item.items():
                items[k] += v
            for p in PLOTS:
                sums[p] += plot(p, ns, m)
        if slow:
            print('  in the %d frames over 33 ms (%.0f ms in all): %s' % (len(slow), sum(m for _, m in slow),
                  ', '.join('%s %.0f' % (p, sums[p]) for p in PLOTS)))
            print('  zones: ' + ', '.join('%s %.0f' % kv for kv in sorted(totals.items(), key=lambda kv: -kv[1])[:16]))
            print('  items: ' + ', '.join('%s %.0f' % kv for kv in sorted(items.items(), key=lambda kv: -kv[1])[:12]))
        print('  longest frames:')
        for ns, m in sorted(leg, key=lambda f: -f[1])[:12]:
            by_name, by_item = frame_zones(ns, m)
            top = sorted(by_name.items(), key=lambda kv: -kv[1])[:7]
            top_items = sorted(by_item.items(), key=lambda kv: -kv[1])[:3]
            print('    %.0f s: %.1f ms; render cpu %.1f gpu %.1f, jit %.1f, draws %d (shadows %d, lamp shadow changes %d), pipelines draw %d spec %d; %s; %s'
                  % ((ns - frames[0][0]) / 1e9, m, plot('render cpu ms', ns), plot('render gpu ms', ns), plot('jit ms', ns),
                     plot('draw calls', ns), plot('draw calls: shadows', ns), plot('shadows switched on', ns, m) + plot('shadow casters changed', ns, m),
                     plot('pipelines draw', ns), plot('pipelines specialization', ns),
                     ', '.join('%s %.1f' % kv for kv in top), ', '.join('%s %.1f' % kv for kv in top_items)))


if __name__ == '__main__':
    main()
