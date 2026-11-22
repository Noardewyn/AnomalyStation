"""DM object-tree order of every type path, and the definition order of the procs of each type.

``docs/ai/specs/object-model.md`` section 9 fixes the order in which the port enumerates types
(``typesof``, ``subtypesof``, type caches): depth-first preorder of the type tree, children in
the order of their first declaration in compile order. This is OpenDream's order, reproduced
here step by step from its sources (OpenDreamProject/OpenDream, master):

1. ``DMCompiler/DM/DMCodeTree.cs``: every statement of the program adds its type path to a code
   tree (``AddType`` -> ``GetDMObjectNode``: one node per path segment, a node's children in
   order of first mention). OpenDream's own ``DMStandard`` library is parsed first and kept in a
   separate tree (``FinishDMStandard``); the program's tree is ``_root``.
2. ``DMCodeTree.DefineEverything``: preorder passes over the program's tree, then over the
   DMStandard tree (``Pass(_root); Pass(_dmStandardRoot!)``), repeated while nodes are still
   waiting. ``ObjectNode.TryDefineType`` gives a type its id through
   ``DMObjectTree.GetOrCreateDMObject``, which first creates the missing prefix types ("dummy"
   parents) and, for a one-segment path, ``/datum`` (or the root for ``client``, ``datum``,
   ``list``, ``alist``, ``vector``, ``savefile``, ``world``, ``callee``). A type with an
   explicit ``parent_type`` waits until that parent exists (``_parentTypes``; the first
   assignment wins, ``DMCodeTree.Vars.cs`` ``AddObjectVarOverride``) and is then re-parented.
   Ids count up from the root (id 0, created by the ``DMCompiler`` constructor).
3. ``OpenDreamRuntime/Objects/DreamObjectTree.cs``: a type's children are its inheriting types in
   id order (``InheritingTypes``), and ``GetAllDescendants`` (used by ``typesof``,
   ``DreamProcNativeRoot.NativeProc_typesof``) is the depth-first preorder over them.

The program's statements come from ``dmscan`` (``ParseResult.types``, in order of first mention,
prefixes first, which is the code tree's node order). The DMStandard statements are the type
declarations of OpenDream's ``DMCompiler/DMStandard`` files in their include order
(``_Standard.dm``), listed in ``DMSTANDARD_TYPES``. BYOND's own order has not been compared with
a BYOND run (object-model.md section 9).

Proc order: ``typesof(/t/proc)`` lists the procs declared on ``/t`` itself (with the ``proc/``
keyword, not inherited ones and not overrides): tg's ``/datum/controller/global_vars/Initialize``
(controllers/globals.dm:51-56) compares their count with the number of global vars and warns
when they differ, so on BYOND it returns exactly the ``InitGlobal*`` procs. The order is the
order of definition in compile order, as for types.
"""

import collections

# OpenDream DMCompiler/DM/DMObjectTree.cs GetOrCreateDMObject: one-segment paths whose parent is
# the root; every other one-segment path is created under /datum.
ROOT_PARENTED = frozenset(('client', 'datum', 'list', 'alist', 'vector', 'savefile', 'world',
                           'callee'))

# The type declarations of OpenDream's DMStandard (DMCompiler/DMStandard/_Standard.dm includes
# Types/AList.dm, Callee.dm, Client.dm, Database.dm, Datum.dm, Exception.dm, Filter.dm,
# Generator.dm, Icon.dm, Image.dm, List.dm, Matrix.dm, Mutable_Appearance.dm, Particles.dm,
# PixLoc.dm, Regex.dm, Savefile.dm, Sound.dm, Vector.dm, World.dm, Atoms/_Atom.dm, Area.dm,
# Mob.dm, Movable.dm, Obj.dm, Turf.dm in this order), as the preorder of its code tree.
DMSTANDARD_TYPES = (
    '/alist', '/callee', '/client', '/database', '/database/query', '/datum', '/exception',
    '/dm_filter', '/generator', '/icon', '/image', '/list', '/matrix', '/mutable_appearance',
    '/particles', '/pixloc', '/regex', '/savefile', '/sound', '/vector', '/world', '/atom',
    '/atom/movable', '/area', '/mob', '/obj', '/turf',
)

# parent_type assignments of DMStandard (Types/*.dm: '/atom parent_type = /datum', ...). They
# come before any program statement, so they win over a program's own assignment.
DMSTANDARD_PARENT = (
    ('/database', '/datum'), ('/exception', '/datum'), ('/generator', '/datum'),
    ('/icon', '/datum'), ('/image', '/datum'), ('/matrix', '/datum'),
    ('/mutable_appearance', '/image'), ('/particles', '/datum'), ('/regex', '/datum'),
    ('/sound', '/datum'), ('/atom', '/datum'), ('/area', '/atom'), ('/mob', '/atom/movable'),
    ('/obj', '/atom/movable'), ('/turf', '/atom'),
)

ROOT = ''  # the root type '/'


def _preorder(paths):
    """Code-tree preorder of paths given in order of first mention (prefixes first)."""
    children = collections.defaultdict(list)
    for p in paths:
        k = p.rfind('/')
        children[p[:k]].append(p)
    out = []
    stack = [ROOT]
    while stack:
        p = stack.pop()
        if p != ROOT:
            out.append(p)
        stack.extend(reversed(children.get(p, ())))
    return out


def _parent_of_path(path):
    k = path.rfind('/')
    if k > 0:
        return path[:k]
    return ROOT if path[1:] in ROOT_PARENTED else '/datum'


class TypeTree:
    """The type ids and the final type tree, following OpenDream's compiler and runtime."""

    def __init__(self, program_types, program_parent_decls):
        """program_types: type paths in order of first mention, prefixes first (dmscan's
        ParseResult.types); program_parent_decls: (type, parent, ...) in parse order."""
        explicit = {}
        for t, p in DMSTANDARD_PARENT:
            explicit.setdefault(t, p)
        self.ignored_parent_decls = []
        for decl in program_parent_decls:
            t, p = decl[0], decl[1]
            if t in explicit:
                if explicit[t] != p:
                    self.ignored_parent_decls.append(decl)
                continue
            explicit[t] = p
        self.explicit_parent = dict(explicit)
        self.ids = {ROOT: 0}
        self.created_parent = {}
        self.parent = {}
        pending = dict(explicit)
        trees = (_preorder(program_types), _preorder(DMSTANDARD_TYPES))
        waiting = [list(tree) for tree in trees]
        while True:
            progress = False
            nxt = []
            for nodes in waiting:
                rest = []
                for path in nodes:
                    if self._try_define(path, pending):
                        progress = True
                    else:
                        rest.append(path)
                nxt.append(rest)
            waiting = nxt
            if not progress or not any(waiting):
                break
        self.undefined = sorted({p for nodes in waiting for p in nodes})
        for t in self.ids:
            if t == ROOT:
                continue
            # A defined type with an explicit parent_type is re-parented (TryDefineType sets
            # dmObject.Parent); the loop above defines every type whose parent exists.
            par = self.explicit_parent.get(t)
            self.parent[t] = par if par is not None and par in self.ids else self.created_parent[t]
        children = collections.defaultdict(list)
        for t in sorted(self.parent, key=self.ids.__getitem__):
            children[self.parent[t]].append(t)
        self.children = children
        order = []
        stack = [ROOT]
        seen = set()
        while stack:
            t = stack.pop()
            if t in seen:
                continue
            seen.add(t)
            if t != ROOT:
                order.append(t)
            stack.extend(reversed(children.get(t, ())))
        self.order = order
        self.unreached = sorted(set(self.parent) - seen)

    def _create(self, path):
        if path in self.ids:
            return
        par = _parent_of_path(path)
        self._create(par)
        self.created_parent[path] = par
        self.ids[path] = len(self.ids)

    def _try_define(self, path, pending):
        par = pending.get(path)
        if par is not None:
            if par not in self.ids:
                return False  # parent type isn't ready yet (DMCodeTree.ObjectNode.TryDefineType)
            del pending[path]
        self._create(path)
        return True

    def is_subtype(self, t, base):
        seen = set()
        while t is not None and t not in seen:
            if t == base:
                return True
            seen.add(t)
            t = self.parent.get(t)
        return False


def enumerated_proc_types(defs):
    """The (type path, 'proc' or 'verb') pairs that tg enumerates with typesof(/t/proc) or
    typesof(/t/verb) in a proc body, sorted."""
    return sorted({e for d in defs if not d.builtin for e in d.typesof_procs})


def proc_order(defs, wanted):
    """{(type path, 'proc' or 'verb'): [names]} for the wanted pairs: the procs (verbs) declared on
    the type itself with the proc/ (verb/) keyword, first definition per name, in definition
    order (parse order, which is compile order)."""
    out = collections.OrderedDict((w, []) for w in wanted)
    seen = set()
    for d in defs:
        if d.builtin or not d.is_decl:
            continue
        key = (d.type, 'verb' if d.is_verb else 'proc')
        names = out.get(key)
        if names is None or (key, d.name) in seen:
            continue
        seen.add((key, d.name))
        names.append(d.name)
    return out


# ------------------------------------------------------------------------------------------------
# C# output
# ------------------------------------------------------------------------------------------------

def _raw_literal(text):
    """A C# raw string literal holding text, one source line per text line, closing delimiter
    at column 0 (so nothing is stripped). The decoders ignore '\\r', so a CRLF checkout still
    decodes."""
    assert '"""' not in text and '\r' not in text
    return '"""\n' + text + '\n"""'


def encode_tree(order, parent):
    """The preorder as text, one line per type: its depth below the root (0 for a child of the
    root) as a decimal number, then the path segment it adds to its parent's path ('/datum',
    '/movable'), or '=' and its full path when its parent is not its path prefix (a top-level
    type under /datum, parent_type). In a preorder a type's parent is the nearest earlier line
    one level up, so the decoder rebuilds the paths with a stack. Returns (text, max depth)."""
    depth = {}
    lines = []
    for t in order:
        p = parent[t]
        d = 0 if p == ROOT else depth[p] + 1
        depth[t] = d
        if p == ROOT or not t.startswith(p + '/'):
            seg = '=' + t if p != ROOT else t
        else:
            seg = t[len(p):]
        lines.append('%d%s' % (d, seg))
    return '\n'.join(lines), (max(depth.values()) if depth else 0)


def decode_tree(text):
    """Python mirror of the C# decoder in TypeOrder.g.cs (used by the self-check)."""
    out = []
    stack = []
    for line in text.replace('\r', '').split('\n'):
        k = 0
        while line[k].isdigit():
            k += 1
        d = int(line[:k])
        seg = line[k:]
        del stack[d:]
        if seg.startswith('='):
            path = seg[1:]
        else:
            path = (stack[-1] if stack else '') + seg
        stack.append(path)
        out.append(path)
    return out


HEADER = """// <auto-generated>
// Generated by tools/coloring/color_procs.py from tgstation (snapshot %s), production build
// (translation-rules.md 1.7). Do not edit; regenerate with: python3 tools/coloring/color_procs.py
// </auto-generated>
// Derived from tgstation code. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
"""


def render_type_order(tree, tg_rev):
    encoded, max_depth = encode_tree(tree.order, tree.parent)
    lines = [HEADER % tg_rev]
    w = lines.append
    w('using System.Collections.Immutable;')
    w('')
    w('namespace Anomaly.Core.Tg;')
    w('')
    w('/// <summary>')
    w('/// Every DM type path of the production build in DM object-tree order (docs/ai/specs/object-model.md section 9):')
    w('/// depth-first preorder of the type tree, children in the order of their first declaration in compile order')
    w('/// (tgstation.dme include order, then line order), with type ids, builtin types and parent_type handled as')
    w('/// OpenDream does. Method and limits: tools/coloring/typeorder.py, docs/ai/specs/colored-procs.md ("Type order").')
    w('/// </summary>')
    w('public static class TypeOrder')
    w('{')
    w('    /// <summary>Number of type paths in <see cref="Paths"/>.</summary>')
    w('    public const int Count = %d;' % len(tree.order))
    w('')
    w('    /// <summary>The type paths in DM object-tree order: "/world", "/savefile", "/datum", ...</summary>')
    w('    public static readonly ImmutableArray<string> Paths = Decode(Encoded(), Count);')
    w('')
    w('    private const int MaxDepth = %d;' % (max_depth + 1))
    w('')
    w('    // One line per type in preorder: its depth below the root (0 for a child of the root), then the path segment it')
    w('    // adds to its parent\'s path ("2/global_vars"), or "=" and its full path when its parent is not its path prefix')
    w('    // ("1=/atom": /atom is a child of /datum). The parent of a line is the nearest earlier line one level up.')
    w('    private static ImmutableArray<string> Decode(string encoded, int count)')
    w('    {')
    w('        ImmutableArray<string>.Builder paths = ImmutableArray.CreateBuilder<string>(count);')
    w('        string[] stack = new string[MaxDepth];')
    w('        foreach (string raw in encoded.Split(\'\\n\'))')
    w('        {')
    w('            string line = raw.TrimEnd(\'\\r\');')
    w('            int k = 0;')
    w('            int depth = 0;')
    w('            while (line[k] >= \'0\' && line[k] <= \'9\')')
    w('                depth = depth * 10 + (line[k++] - \'0\');')
    w('            string path = line[k] == \'=\'')
    w('                ? line.Substring(k + 1)')
    w('                : (depth == 0 ? "" : stack[depth - 1]) + line.Substring(k);')
    w('            stack[depth] = path;')
    w('            paths.Add(path);')
    w('        }')
    w('        if (paths.Count != count)')
    w('            throw new InvalidOperationException("TypeOrder: decoded " + paths.Count + " paths, expected " + count);')
    w('        return paths.MoveToImmutable();')
    w('    }')
    w('')
    w('    // A method, not a const: a const string would be stored twice in the assembly.')
    w('    private static string Encoded() => ' + _raw_literal(encoded) + ';')
    w('}')
    return '\n'.join(lines) + '\n'


def render_proc_order(procs, tg_rev):
    """ProcOrder.g.cs: for each type whose procs (verbs) tg enumerates with typesof(/t/proc)
    (typesof(/t/verb)), the names of the procs declared on it, in definition order."""
    lines = [HEADER % tg_rev]
    w = lines.append
    w('using System.Collections.Generic;')
    w('using System.Collections.Immutable;')
    w('')
    w('namespace Anomaly.Core.Tg;')
    w('')
    w('/// <summary>')
    w('/// What typesof(/t/proc) and typesof(/t/verb) return for the types tg enumerates that way: the procs (verbs) declared on')
    w('/// the type itself with the proc/ (verb/) keyword, not inherited ones and not overrides, in definition order in')
    w('/// compile order (docs/ai/specs/object-model.md section 9). tg runs its GLOBAL_* initializers by iterating')
    w('/// typesof(/datum/controller/global_vars/proc) (controllers/globals.dm:51) and checks their count against its global')
    w('/// vars, so on BYOND the list holds exactly the procs declared there. Only the enumerated types are listed (found by')
    w('/// tools/coloring in every proc body of the production build); asking for another type throws. Method:')
    w('/// tools/coloring/typeorder.py.')
    w('/// </summary>')
    w('public static class ProcOrder')
    w('{')
    for kind, field in (('proc', 'Procs'), ('verb', 'Verbs')):
        rows = [(t, names) for (t, k), names in procs.items() if k == kind]
        w('    /// <summary>Type path -> the %s names declared on it, in definition order (DM names, no C# renames).</summary>' % kind)
        w('    public static readonly ImmutableDictionary<string, ImmutableArray<string>> %s =' % field)
        if not rows:
            w('        ImmutableDictionary<string, ImmutableArray<string>>.Empty;')
        else:
            w('        ImmutableDictionary.CreateRange(StringComparer.Ordinal, new KeyValuePair<string, ImmutableArray<string>>[]')
            w('        {')
            for t, names in rows:
                w('            new("%s", ImmutableArray.Create(' % t)
                for i, nm in enumerate(names):
                    w('                "%s"%s' % (nm, ',' if i < len(names) - 1 else ')),'))
                if not names:
                    w('                Array.Empty<string>())),')
            w('        });')
        w('')
    w('    /// <summary>typesof(/t/proc) for a type that tg enumerates; throws for any other type.</summary>')
    w('    public static ImmutableArray<string> ProcsOf(string typePath) =>')
    w('        Procs.TryGetValue(typePath, out ImmutableArray<string> names)')
    w('            ? names')
    w('            : throw new KeyNotFoundException("ProcOrder: tg does not enumerate typesof(" + typePath + "/proc); regenerate with tools/coloring");')
    w('}')
    return '\n'.join(lines) + '\n'


def summary_md(tree, procs):
    """The generated 'Type order' block of docs/ai/specs/colored-procs.md."""
    lines = []
    w = lines.append
    _, max_depth = encode_tree(tree.order, tree.parent)
    reparented = [t for t in tree.order if tree.parent[t] != ROOT and not t.startswith(tree.parent[t] + '/')]
    w('| Quantity | Value |')
    w('|---|---|')
    w('| Type paths (`TypeOrder.Count`), builtin types included | %d |' % len(tree.order))
    w('| Children of the root, in order | %s |' % ', '.join('`%s`' % t for t in tree.children[ROOT]))
    w('| Deepest type (levels below the root) | %d |' % (max_depth + 1))
    w('| Types whose parent is not their path prefix (one-segment types under `/datum`, `parent_type`) | %d |' % len(reparented))
    w('| Explicit `parent_type` in tg (production build) | %s |' % (', '.join(
        '`%s` -> `%s`' % (t, p) for t, p in sorted(tree.explicit_parent.items())
        if (t, p) not in DMSTANDARD_PARENT) or 'none'))
    w('| Types never defined (their explicit parent does not exist) / not reachable from the root | %d / %d |' % (
        len(tree.undefined), len(tree.unreached)))
    w('| `ProcOrder`: types tg enumerates with `typesof(/t/proc)` or `typesof(/t/verb)` | %s |' % (', '.join(
        '`%s/%s` (%d)' % (t, k, len(names)) for (t, k), names in procs.items()) or 'none'))
    w('')
    w('Where a type sits among its siblings when its parent is not its path prefix (position among the parent\'s children, from 1):')
    w('')
    w('| Parent | Children | Non-prefix children and their positions |')
    w('|---|---|---|')
    for p in [ROOT] + tree.order:
        ch = tree.children.get(p)
        if not ch:
            continue
        odd = [(t, k + 1) for k, t in enumerate(ch) if p != ROOT and not t.startswith(p + '/')]
        if odd:
            w('| `%s` | %d | %s |' % (p, len(ch), ', '.join('`%s` %d' % x for x in odd)))
    return '\n'.join(lines)
