#!/usr/bin/env python3
"""Recounts the numbers in docs/ai/PORT-CHECKLIST.md from the translation tracker and the client's windows.

The checklist's ticks are kept by hand, in the package that ports a row. Its numbers are not: this script recounts them from
docs/ai/specs/generated/types-index.tsv (the tracker, rewritten by every regen) and godot/scripts/Shared/Tgui/TguiWindows.cs. Run it
after a package's regen and once more at a merge train's end, then commit the file with the package:

    python3 tools/checklist/refresh-port-checklist.py          # rewrite the file, print what moved
    python3 tools/checklist/refresh-port-checklist.py --check  # exit 1 when the file is stale

What it rewrites:
- the status line and the "Sources" bullets of the header (the tracker's totals, the windows the client draws);
- section 11's row of the tgui windows the client does not draw;
- section 1's row of the deferred leaves (the sections of not-translated.md, the rows of live-partial.txt);
- section 0, the overview by tg area, in full (the areas are AREAS below; add one there, not in the file);
- every "`path` n/N" in the rest of the file: a type family (a path starting with "/": its live types of all its types) or a
  directory of tg's code/ (its procs declared, translated or stubbed, of all its procs). N is tg's and never changes, so the
  script picks the meaning whose total is N; a mention none fits is left alone and printed;
- section 13's counts of the windows ticked.
"""

import collections
import datetime
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
DOC = os.path.join(ROOT, 'docs', 'ai', 'PORT-CHECKLIST.md')
INDEX = os.path.join(ROOT, 'docs', 'ai', 'specs', 'generated', 'types-index.tsv')
WINDOWS = os.path.join(ROOT, 'godot', 'scripts', 'Shared', 'Tgui', 'TguiWindows.cs')


def find_tg():
    # tg's clone sits next to the repository; a worktree under .claude/worktrees/ is deeper, so look up the tree
    d = ROOT
    while True:
        cand = os.path.join(os.path.dirname(d), 'tgstation')
        if os.path.isdir(cand):
            return cand
        if os.path.dirname(d) == d:
            return None
        d = os.path.dirname(d)


TG = find_tg()
TG_INTERFACES = os.path.join(TG, 'tgui', 'packages', 'tgui', 'interfaces') if TG else ''

# The areas of section 0: a label and the directories of tg's code/ it counts. "dir" counts the directory with its
# subdirectories, "dir/*" the files directly in it (an area whose subdirectories are areas of their own).
AREAS = [
    ('wounds', ['code/datums/wounds']),
    ('dna', ['code/datums/dna']),
    ('hallucination', ['code/modules/hallucination']),
    ('forensics', ['code/modules/forensics']),
    ('transit tubes', ['code/game/objects/structures/transit_tubes']),
    ('telecomms', ['code/game/machinery/telecomms']),
    ('doors', ['code/game/machinery/doors']),
    ('`mob/living` base', ['code/modules/mob/living/*']),
    ('power', ['code/modules/power']),
    ('atmospherics', ['code/modules/atmospherics']),
    ('movespeed and actionspeed', ['code/modules/movespeed', 'code/modules/actionspeed']),
    ('lobby menu', ['code/modules/lobby_menu']),
    ('tgui', ['code/modules/tgui']),
    ('devices', ['code/game/objects/items/devices']),
    ('implants', ['code/game/objects/items/implants']),
    ('rcd', ['code/game/objects/items/rcd']),
    ('stacks', ['code/game/objects/items/stacks']),
    ('projectiles', ['code/modules/projectiles']),
    ('research', ['code/modules/research']),
    ('mining', ['code/modules/mining']),
    ('`_onclick`', ['code/_onclick']),
    ('carbon', ['code/modules/mob/living/carbon']),
    ('subsystems', ['code/controllers/subsystem']),
    ('mapping', ['code/modules/mapping']),
    ('shuttles', ['code/modules/shuttle']),
    ('status effects', ['code/datums/status_effects']),
    ('storage', ['code/datums/storage']),
    ('turfs', ['code/game/turfs']),
    ('structures', ['code/game/objects/structures/*']),
    ('surgery dir (bodyparts, organs)', ['code/modules/surgery']),
    ('modular computers', ['code/modules/modular_computers']),
    ('client', ['code/modules/client']),
    ('tools', ['code/game/objects/items/tools']),
    ('materials', ['code/datums/materials']),
    ('language', ['code/modules/language']),
    ('jobs', ['code/modules/jobs']),
    ('wires', ['code/datums/wires']),
    ('keybindings', ['code/datums/keybinding']),
    ('recycling (disposal pipes)', ['code/modules/recycling']),
    ('reagents', ['code/modules/reagents']),
    ('clothing', ['code/modules/clothing']),
    ('components', ['code/datums/components']),
    ('elements', ['code/datums/elements']),
    ('machinery', ['code/game/machinery/*']),
    ('computers', ['code/game/machinery/computer']),
    ('items', ['code/game/objects/items/*']),
    ('weaponry', ['code/game/objects/items/weaponry']),
    ('grenades', ['code/game/objects/items/grenades']),
    ('admin', ['code/modules/admin']),
    ('admin verbs', ['code/modules/admin/verbs']),
    ('actions', ['code/datums/actions']),
    ('library', ['code/modules/library']),
    ('plumbing', ['code/modules/plumbing']),
    ('awaymissions', ['code/modules/awaymissions']),
    ('antagonists', ['code/modules/antagonists']),
    ('basic mobs', ['code/modules/mob/living/basic']),
    ('AI controllers', ['code/datums/ai']),
    ('wiremod', ['code/modules/wiremod']),
    ('MODsuits', ['code/modules/mod']),
    ('vehicles and mechs', ['code/modules/vehicles']),
    ('fishing', ['code/modules/fishing']),
    ('events', ['code/modules/events']),
    ('hydroponics', ['code/modules/hydroponics']),
    ('silicons', ['code/modules/mob/living/silicon']),
    ('simple animals', ['code/modules/mob/living/simple_animal']),
    ('spells', ['code/modules/spells']),
    ('paperwork', ['code/modules/paperwork']),
    ('cargo', ['code/modules/cargo']),
    ('transport', ['code/modules/transport']),
    ('quirks', ['code/datums/quirks']),
    ('religion', ['code/modules/religion']),
    ('bitrunning', ['code/modules/bitrunning']),
    ('mapfluff', ['code/modules/mapfluff']),
    ('mutations', ['code/datums/mutations']),
    ('brain traumas', ['code/datums/brain_damage']),
    ('explorer drones', ['code/modules/explorer_drone']),
    ('robot items', ['code/game/objects/items/robot']),
    ('dna infuser', ['code/game/machinery/dna_infuser']),
    ('martial arts', ['code/datums/martial']),
    ('station traits', ['code/datums/station_traits']),
    ('experisci', ['code/modules/experisci']),
    ('minimap', ['code/modules/minimap']),
    ('mafia', ['code/modules/mafia']),
    ('pai', ['code/modules/pai']),
    ('holiday', ['code/modules/holiday']),
    ('photography', ['code/modules/photography']),
    ('porta turrets', ['code/game/machinery/porta_turret']),
    ('buildmode', ['code/modules/buildmode']),
    ('CTF', ['code/modules/capture_the_flag']),
    ('art', ['code/modules/art']),
    ('instruments', ['code/modules/instruments']),
    ('station goals', ['code/modules/station_goals']),
    ('diseases', ['code/datums/diseases']),
    ('newscaster', ['code/game/machinery/newscaster']),
    ('AI modules', ['code/game/objects/items/AI_modules']),
    ('TCG', ['code/game/objects/items/tcg']),
    ('weather', ['code/datums/weather']),
    ('holodeck', ['code/modules/holodeck']),
    ('cards', ['code/modules/cards']),
    ('uplink', ['code/modules/uplink']),
    ('water structures', ['code/game/objects/structures/water_structures']),
    ('economy', ['code/modules/economy']),
    ('achievements', ['code/datums/achievements']),
    ('food and drinks', ['code/modules/food_and_drinks']),
    ('vending', ['code/modules/vending']),
    ('effects', ['code/game/objects/effects']),
    ('skills', ['code/datums/skills']),
    ('memory', ['code/datums/memory']),
    ('mob spawners', ['code/modules/mob_spawn']),
    ('species (`carbon/human/species_types`)', ['code/modules/mob/living/carbon/human/species_types']),
    ('detective work', ['code/modules/detectivework']),
    ('security levels', ['code/modules/security_levels']),
    ('meteors', ['code/modules/meteors']),
    ('lost crew', ['code/modules/lost_crew']),
    ('tutorials', ['code/modules/tutorials']),
    ('deathmatch', ['code/modules/deathmatch']),
    ('basketball', ['code/modules/basketball']),
    ('manufactorio', ['code/modules/manufactorio']),
    ('`__HELPERS`', ['code/__HELPERS']),
]


def load_index():
    types, procs = [], []
    with open(INDEX, encoding='utf-8') as f:
        for line in f:
            if line.startswith('#'):
                continue
            c = line.rstrip('\n').split('\t')
            if c[0] == 'type':
                types.append((c[1], c[4], c[3].rsplit(':', 1)[0]))
            elif c[0] == 'proc':
                procs.append((c[3].rsplit(':', 1)[0], c[5]))
    return types, procs


class Counts:
    def __init__(self, types, procs):
        self.types, self.procs = types, procs
        self.rec_total, self.rec_decl = collections.Counter(), collections.Counter()
        self.dir_total, self.dir_decl = collections.Counter(), collections.Counter()
        for f, s in procs:
            declared = s in ('yes', 'stub')
            parts = f.split('/')
            for i in range(2, len(parts)):
                d = '/'.join(parts[:i])
                self.rec_total[d] += 1
                self.rec_decl[d] += declared
            d = f.rsplit('/', 1)[0]
            self.dir_total[d] += 1
            self.dir_decl[d] += declared

    def area(self, specs):
        decl = total = 0
        for spec in specs:
            if spec.endswith('/*'):
                d = spec[:-2]
                decl += self.dir_decl[d]
                total += self.dir_total[d]
            else:
                decl += self.rec_decl[spec]
                total += self.rec_total[spec]
        return decl, total

    def family(self, root):
        t = [s for p, s, _ in self.types if p == root or p.startswith(root + '/')]
        return sum(s in ('live', 'base') for s in t), len(t)

    # The meanings a "`x` n/N" mention can have, tried in turn; the one whose total is N wins.
    def mention(self, x, total):
        if x.startswith('/'):
            got = self.family(x)
            return got if got[1] == total else None
        seg = '/' + x + '/'
        m = [s for f, s in self.procs if seg in '/' + f or f.endswith('/' + x + '.dm')]
        if len(m) == total:
            return sum(s in ('yes', 'stub') for s in m), len(m)
        roots = sorted(set(p for p, _, _ in self.types if p.endswith('/' + x)))
        for r in roots:
            got = self.family(r)
            if got[1] == total:
                return got
        t = [s for p, s, f in self.types if seg in '/' + f or f.endswith('/' + x + '.dm')]
        if len(t) == total:
            return sum(s in ('live', 'base') for s in t), len(t)
        return None


def version():
    with open(os.path.join(ROOT, 'Version.props'), encoding='utf-8') as f:
        m = re.search(r'<Version>([^<]+)</Version>', f.read())
    return m.group(1) if m else '?'


def drawn_tg_windows():
    with open(WINDOWS, encoding='utf-8') as f:
        keys = set(re.findall(r'^\s*\["([A-Za-z0-9_]+)"\]\s*=', f.read(), re.M))
    if not TG_INTERFACES or not os.path.isdir(TG_INTERFACES):
        return None, None
    names = set()
    # an interface is a .tsx/.jsx file or a directory named for it; common/ is shared parts and Radio.test.tsx a test
    for n in os.listdir(TG_INTERFACES):
        if not n[:1].isupper():
            continue
        if os.path.isdir(os.path.join(TG_INTERFACES, n)):
            names.add(n)
        elif n.count('.') == 1 and n.endswith(('.tsx', '.jsx')):
            names.add(n.split('.')[0])
    return len(keys & names), len(names)


def fmt(n):
    return f'{n:,}'


def pct(a, b):
    return 0 if b == 0 else int(100 * a / b)


def overview(counts):
    buckets = collections.OrderedDict((k, []) for k in ('Done (100%)', 'Mostly ported (70-99%)', 'Partly (2-69%)', 'Not started (0-1%)'))
    for label, specs in AREAS:
        d, t = counts.area(specs)
        if t == 0:
            raise SystemExit(f'area "{label}": no tg procs under {specs}')
        share = d / t
        key = ('Done (100%)' if d == t else 'Mostly ported (70-99%)' if share >= 0.7
               else 'Partly (2-69%)' if share >= 0.02 else 'Not started (0-1%)')
        buckets[key].append((share, label, d, t))
    rows = []
    for key, items in buckets.items():
        if not items:
            continue
        items.sort(key=lambda x: (-x[0], x[1].lower()))
        rows.append(f'| {key} | ' + ', '.join(f'{label} {fmt(d)}/{fmt(t)}' for _, label, d, t in items) + ' |')
    return rows


def refresh(text, counts, notes, today):
    yes = sum(s == 'yes' for _, s in counts.procs)
    stub = sum(s == 'stub' for _, s in counts.procs)
    total_procs = len(counts.procs)
    live = sum(s in ('live', 'base') for _, s, _ in counts.types)
    inert = sum(s == 'inert' for _, s, _ in counts.types)

    # the header
    text, n = re.subn(r'Status: [^\n]*?\. A list', f'Status: recounted {today} ({version()}). A list', text, count=1)
    if n != 1:
        notes.append('header: no "Status:" line')
    text, n = re.subn(
        r'- the translation tracker `docs/ai/specs/generated/types-index\.tsv` \((?:regenerated at `[^`]*`|recounted [0-9-]+)\): .*?stubbed;',
        f'- the translation tracker `docs/ai/specs/generated/types-index.tsv` (recounted {today}): {fmt(len(counts.types))} tg '
        f'types, of them {fmt(live)}\n  live and {fmt(inert)} inert; {fmt(total_procs)} tg procs, of them {fmt(yes)} translated '
        f'({pct(yes, total_procs)}%) and {fmt(stub)} stubbed;',
        text, count=1, flags=re.S)
    if n != 1:
        notes.append('header: no tracker bullet')
    drawn, tg_windows = drawn_tg_windows()
    if drawn is not None:
        text, n = re.subn(r'(- the client\'s tgui windows \(`godot/scripts/Shared/Tgui/TguiWindows\.cs`\): )\d+ of tg\'s \d+ '
                          r'interfaces[^;\n]*;',
                          lambda m: f'{m.group(1)}{drawn} of tg\'s {tg_windows} interfaces;', text, count=1)
        if n != 1:
            notes.append('header: no windows bullet')
        # section 11's row of the windows not drawn: tg's interfaces less the ones the client draws
        text, n = re.subn(r'(- \[[ x]\] The tgui windows the client does not draw: )\d+ of tg\'s \d+;',
                          lambda m: f'{m.group(1)}{tg_windows - drawn} of tg\'s {tg_windows};', text, count=1)
        if n != 1:
            notes.append('section 11: no row of the windows not drawn')
    else:
        notes.append('header: tg\'s clone not found, the windows bullet is left alone')

    # section 1's row of the deferred leaves: the register's sections and the live-partial rows
    with open(os.path.join(ROOT, 'docs', 'ai', 'specs', 'not-translated.md'), encoding='utf-8') as f:
        sections = sum(1 for line in f if line.startswith('## '))
    with open(os.path.join(ROOT, 'docs', 'ai', 'specs', 'live-partial.txt'), encoding='utf-8') as f:
        partial = sum(1 for line in f if line.strip() and not line.startswith('#'))
    text, n = re.subn(r'`not-translated\.md` \(\d+ sections;', f'`not-translated.md` ({sections} sections;', text, count=1)
    text, m = re.subn(r'`live-partial\.txt` \([\d,]+ lines\)', f'`live-partial.txt` ({fmt(partial)} lines)', text, count=1)
    if n != 1 or m != 1:
        notes.append('section 1: no row of the deferred leaves')

    # section 0, from its heading to the rule after it
    start = text.index('## 0. Overview by tg area')
    end = text.index('\n---\n', start)
    section = '\n'.join([
        '## 0. Overview by tg area',
        '',
        f'Share of tg\'s procs declared in each area of `code/` (translated or stubbed), recounted {today} by',
        '`tools/checklist/refresh-port-checklist.py`. All of `code/`: '
        f'{fmt(yes + stub)}/{fmt(total_procs)} ({pct(yes + stub, total_procs)}%). An area at 100% can still have leaves in',
        '`not-translated.md`; the rows below say what of it runs.',
        '',
        '| State | Areas (procs declared / tg procs) |',
        '|---|---|',
        *overview(counts),
    ])
    text = text[:start] + section + '\n' + text[end:]
    sec0_end = start + len(section)

    # "`x` n/N" everywhere but section 0
    def sub(m):
        if start <= m.start() < sec0_end:
            return m.group(0)
        x, total = m.group(1), int(m.group(3).replace(',', ''))
        got = counts.mention(x, total)
        if got is None:
            notes.append(f'left alone: `{x}` {m.group(2)}/{m.group(3)} (no meaning totals {total})')
            return m.group(0)
        return f'`{x}` {fmt(got[0])}/{fmt(got[1])}'
    text = re.sub(r'`([^`\n]+)` (\d+(?:,\d{3})*)/(\d+(?:,\d{3})*)', sub, text)

    # section 13: the windows ticked
    s13 = text.find('## 13. Index of the tgui windows not drawn yet')
    if s13 >= 0:
        head, body = text[:s13], text[s13:]
        lines = body.split('\n')
        ticked_all = 0
        i = 0
        while i < len(lines):
            m = re.match(r'^(### .*?): \d+(?: \(\d+ drawn\))?$', lines[i])
            if m:
                j, done, total = i + 1, 0, 0
                while j < len(lines) and not lines[j].startswith('#'):
                    if lines[j].startswith('- [x]'):
                        done += 1
                        total += 1
                    elif lines[j].startswith('- [ ]'):
                        total += 1
                    j += 1
                lines[i] = f'{m.group(1)}: {total} ({done} drawn)'
                ticked_all += done
            i += 1
        body = '\n'.join(lines)
        body, n = re.subn(r'(\d+) of tg\'s (\d+) windows \(the client drew (\d+) when this index was made[^)]*\)[^.]*\.',
                          lambda m: f'{m.group(1)} of tg\'s {m.group(2)} windows (the client drew {m.group(3)} when this index '
                                    f'was made); {ticked_all} of the {m.group(1)} are ticked now.', body, count=1)
        if n != 1:
            notes.append('section 13: no intro sentence')
        text = head + body
    return text


def main():
    check = '--check' in sys.argv[1:]
    with open(DOC, encoding='utf-8') as f:
        old = f.read()
    counts = Counts(*load_index())
    # The date moves only when a number does, so a file that is current stays byte for byte the same.
    m = re.search(r'Status: recounted (\d{4}-\d\d-\d\d)', old)
    notes = []
    new = refresh(old, counts, notes, m.group(1) if m else '')
    if new != old or not m:
        notes = []
        new = refresh(old, counts, notes, datetime.date.today().isoformat())
    for n in notes:
        print(n)
    if new == old:
        print('PORT-CHECKLIST.md is current')
        return 0
    if check:
        print('PORT-CHECKLIST.md is stale: run tools/checklist/refresh-port-checklist.py')
        return 1
    with open(DOC, 'w', encoding='utf-8') as f:
        f.write(new)
    print('PORT-CHECKLIST.md recounted')
    return 0


if __name__ == '__main__':
    sys.exit(main())
