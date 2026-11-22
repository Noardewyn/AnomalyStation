# Strips the generated [DmPath(...)] attribute line of the given DM paths (a hand-written part now carries it), before the types tool reruns.
import sys, re, glob
paths = sys.argv[1:]
for f in glob.glob('src/Core/Tg/Generated/Types/*.g.cs'):
    s = open(f).read(); o = s
    for p in paths:
        s = re.sub(r'\n\[DmPath\("' + re.escape(p) + r'"\)(, DmGeneratedOnly)?(, DmInert)?\]\n', '\n', s)
    if s != o:
        open(f, 'w').write(s); print('stripped in', f)
