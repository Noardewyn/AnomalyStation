"""Colouring analysis over the parse of the tg tree: roots, slots, call resolution, propagation.

Terms (docs/ai/specs/colored-procs.md has the full method):

* root declaration: the topmost definition of a proc name on a type path with no ancestor that
  declares it. Builtin procs of the builtin types are declarations too; the builtin procs of the
  builtin root types (BUILTIN_ROOT_TYPES, e.g. /client) stop the walk, so /client/New is a slot of
  its own even though tg sets /client parent_type = /datum;
* slot: (root type path, proc name), one C# virtual hierarchy. Global procs use the root '/proc';
* a def "may sleep" when its own code (outside detached spawn/ASYNC bodies) reaches a sleep
  primitive, makes a call that may suspend it, or calls ..() into a parent that may suspend it.
  Sync-mandated defs (SIGNAL_HANDLER, SHOULD_NOT_SLEEP(TRUE), own or inherited from the parent
  chain) never sleep by tg's lint; their calls that may suspend are conflict sites
  (Waiting.NoSleep);
* a def "suspends its caller" when it may sleep and its effective waitfor is TRUE (waitfor is
  inherited along the override chain, OpenDream DMCodeTree.Procs.cs);
* a slot is "coloured" when some def in it may sleep (its C# signature is DmTask), and
  "suspends" when some def in it suspends its caller;
* a call site resolves to target sets: 'virtual' (static receiver type T: the def visible at T
  plus every override on a subtype of T), 'exact' (new T, call(obj, /T::x()): the def visible
  at T only), 'all' (every def of the slot: untyped receivers, the upper bound) or 'super'
  (..(): the parent def).
  A caller is coloured through a call when some def of a target set suspends its caller.
"""

import collections

from dmscan import Def, _split_args, parse_var_path

# ------------------------------------------------------------------------------------------
# Builtins (sources: BYOND reference info.html; OpenDream DMCompiler/DMStandard/Types/*.dm)
# ------------------------------------------------------------------------------------------

# Builtin procs declared on builtin types, as OpenDream's DMStandard declares them.
BUILTIN_TYPE_PROCS = {
    '/datum': ('New', 'Del', 'Topic', 'Read', 'Write'),
    '/atom': ('Click', 'DblClick', 'MouseDown', 'MouseDrag', 'MouseDrop', 'MouseEntered',
              'MouseExited', 'MouseMove', 'MouseUp', 'MouseWheel', 'Entered', 'Exited',
              'Uncrossed', 'Crossed', 'Cross', 'Uncross', 'Enter', 'Exit', 'Stat'),
    '/atom/movable': ('Bump', 'Move'),
    '/mob': ('Login', 'Logout'),
    '/client': ('New', 'Del', 'Topic', 'Stat', 'Command', 'Import', 'Export', 'AllowUpload',
                'SoundQuery', 'MeasureText', 'Move', 'North', 'South', 'East', 'West',
                'Northeast', 'Northwest', 'Southeast', 'Southwest', 'Center', 'Click',
                'DblClick', 'MouseDown', 'MouseDrag', 'MouseDrop', 'MouseEntered',
                'MouseExited', 'MouseMove', 'MouseUp', 'MouseWheel', 'IsByondMember',
                'CheckPassport', 'SendPage', 'GetAPI', 'SetAPI', 'RenderIcon'),
    '/world': ('New', 'Del', 'Profile', 'GetConfig', 'SetConfig', 'OpenPort', 'IsSubscribed',
               'IsBanned', 'Error', 'Reboot', 'Repop', 'Export', 'Import', 'Topic', 'Tick',
               'SetScores', 'GetScores', 'GetMedal', 'SetMedal', 'ClearMedal', 'AddCredits',
               'GetCredits', 'PayCredits'),
    '/list': ('New', 'Add', 'Copy', 'Cut', 'Find', 'Insert', 'Join', 'Remove', 'RemoveAll',
              'Swap', 'Splice'),
    '/alist': ('New', 'Add', 'Copy', 'Cut', 'Find', 'Insert', 'Join', 'Remove', 'RemoveAll',
               'Swap', 'Splice'),
    '/savefile': ('New', 'Flush', 'ExportText', 'ImportText', 'Lock', 'Unlock'),
    '/icon': ('Blend', 'Crop', 'DrawBox', 'Flip', 'GetPixel', 'Height', 'IconStates', 'Insert',
              'MapColors', 'Scale', 'SetIntensity', 'Shift', 'SwapColor', 'Turn', 'Width'),
    '/matrix': ('Interpolate', 'Add', 'Invert', 'Multiply', 'Scale', 'Subtract', 'Translate',
                'Turn'),
    '/regex': ('Find', 'Find_char', 'Replace', 'Replace_char'),
    '/database': ('Close', 'Error', 'ErrorMsg', 'Open'),
    '/database/query': ('Add', 'Clear', 'Columns', 'Execute', 'GetColumn', 'GetRowData',
                        'NextRow', 'RowsAffected', 'Reset'),
    '/generator': ('Rand',),
    '/vector': ('New', 'Cross', 'Dot', 'Interpolate', 'Normalize', 'Turn'),
    '/pixloc': ('New',),
}

# Builtin types whose parent is the root '/' (OpenDream DMObjectTree). Their builtin procs are
# roots even when tg re-parents the type (tg client_defines.dm: /client parent_type = /datum),
# so /client/New is a separate slot from /datum/New.
BUILTIN_ROOT_TYPES = ('/datum', '/client', '/list', '/alist', '/vector', '/savefile', '/world',
                      '/callee')

# Builtin parent types (OpenDream parent_type declarations).
BUILTIN_PARENT = {
    '/atom': '/datum', '/area': '/atom', '/turf': '/atom', '/obj': '/atom/movable',
    '/mob': '/atom/movable', '/image': '/datum', '/mutable_appearance': '/image',
    '/icon': '/datum', '/matrix': '/datum', '/regex': '/datum', '/sound': '/datum',
    '/exception': '/datum', '/database': '/datum', '/generator': '/datum',
    '/particles': '/datum',
}

# Builtin vars with an object type (used to type receiver chains like src.loc.foo()).
BUILTIN_VARS = {
    '/datum': {'vars': '/list'},
    '/atom': {'loc': '/atom', 'contents': '/list', 'overlays': '/list', 'underlays': '/list',
              'verbs': '/list', 'vis_contents': '/list', 'vis_locs': '/list', 'filters': '/list'},
    '/atom/movable': {'locs': '/list'},
    '/mob': {'client': '/client', 'group': '/list'},
    '/client': {'mob': '/mob', 'eye': '/atom', 'statobj': '/atom', 'screen': '/list',
                'images': '/list', 'verbs': '/list', 'vars': '/list', 'bounds': '/list'},
    '/world': {'contents': '/list', 'vars': '/list'},
    '/image': {'loc': '/atom', 'overlays': '/list', 'underlays': '/list',
               'vis_contents': '/list', 'filters': '/list'},
}

# Builtin global procs that sleep the caller (BYOND reference: sleep; input "The current proc
# sleeps until they respond"; alert "This sleeps the current proc"; shell "The calling proc will
# sleep"; winexists/winget contact the client and sleep the caller).
BUILTIN_SLEEP_GLOBALS = ('sleep', 'input', 'alert', 'shell', 'winexists', 'winget')

# Builtin type procs that sleep the caller: world.Export ("cause the caller to sleep"), hub
# calls (they contact the hub), client calls that need a client round trip like winget
# (MeasureText: tg balloon_alert.dm "MeasureText blocks"; SoundQuery; RenderIcon).
BUILTIN_SLEEP_TYPE_PROCS = {
    '/world': ('Export', 'Import', 'IsSubscribed', 'GetScores', 'SetScores', 'GetMedal',
               'SetMedal', 'ClearMedal', 'GetCredits', 'AddCredits', 'PayCredits'),
    '/client': ('MeasureText', 'SoundQuery', 'RenderIcon'),
}

# Overridable procs that the native implementation of a builtin proc calls (reached when a tg
# override calls ..() into the builtin). The receiver type is the static type of the call.
# BYOND reference, "Default action" of each proc: client/Click() calls object.Click(), and so
# on for the mouse procs; client/Topic() calls hsrc.Topic(); client/Move() calls mob.Move();
# client/North() and the other direction procs call src.Move(); client/Stat() calls
# statobj.Stat(); client/New() calls mob.Login(); client/Del() calls mob.Logout().
_CLIENT_MOUSE = ('Click', 'DblClick', 'MouseDown', 'MouseDrag', 'MouseDrop', 'MouseEntered',
                 'MouseExited', 'MouseMove', 'MouseUp', 'MouseWheel')
BUILTIN_CALLS = {
    ('/atom/movable', 'Move'): (('/atom', 'Exit'), ('/atom', 'Enter'), ('/atom', 'Uncross'),
                                ('/atom', 'Cross'), ('/atom', 'Exited'), ('/atom', 'Entered'),
                                ('/atom', 'Uncrossed'), ('/atom', 'Crossed'),
                                ('/atom/movable', 'Bump')),
    ('/client', 'New'): (('/mob', 'Login'),),
    ('/client', 'Del'): (('/mob', 'Logout'),),
    ('/client', 'Topic'): (('/datum', 'Topic'),),
    ('/client', 'Move'): (('/mob', 'Move'),),
    ('/client', 'Stat'): (('/atom', 'Stat'),),
}
for _name in _CLIENT_MOUSE:
    BUILTIN_CALLS[('/client', _name)] = (('/atom', _name),)
for _name in ('North', 'South', 'East', 'West', 'Northeast', 'Northwest', 'Southeast', 'Southwest'):
    BUILTIN_CALLS[('/client', _name)] = (('/client', 'Move'),)

# Builtin global procs that call /atom/movable/Move synchronously.
STEP_PROCS = ('step', 'step_to', 'step_away', 'step_towards', 'step_rand')

# Every other builtin global proc name (BYOND reference /proc anchors plus OpenDream _char
# variants). A call to one of these does not wait and is not a slot.
BUILTIN_GLOBALS = frozenset('''ASSERT CRASH EXCEPTION REGEX_QUOTE abs addtext alert alist animate
    arccos arcsin arctan arglist ascii2text astype block bound_pixloc bounds bounds_dist browse
    browse_rsc call call_ext ceil ckey ckeyEx clamp cmptext cmptextEx copytext copytext_char cos
    fcopy fcopy_rsc fdel fexists file file2text filter findlasttext findlasttextEx
    findlasttext_char findlasttextEx_char findtext findtextEx findtext_char findtextEx_char flick
    flist floor fract ftime ftp generator get_dir get_dist get_step get_step_away get_step_rand
    get_step_to get_step_towards get_steps_to gradient hascall hearers html_decode html_encode
    icon icon_states image initial input isarea isfile isicon isinf islist isloc ismob
    ismovable isnan isnull isnum isobj ispath ispointer issaved istext isturf istype jointext
    json_decode json_encode length length_char lentext lerp link list list2params load_ext
    load_resource locate log lowertext matrix max md5 min missile nameof newlist noise_hash
    nonspantext nonspantext_char num2text obounds ohearers orange output oview oviewers
    params2list pick pixloc prob rand rand_seed range ref refcount regex replacetext
    replacetextEx replacetext_char replacetextEx_char rgb rgb2num roll round run sha1 shell
    shutdown sign sin sleep sorttext sorttextEx sound spantext spantext_char splicetext
    splicetext_char splittext splittext_char sqrt startup stat statpanel step step_away
    step_rand step_to step_towards tan text text2ascii text2ascii_char text2file text2num
    text2path time2text trimtext trunc turn typesof uppertext url_decode url_encode
    values_cut_over values_cut_under values_dot values_product values_sum vector view viewers
    walk walk_away walk_rand walk_to walk_towards winclone winexists winget winset winshow'''.split())

# call()() sites whose computed target is known from tg's conventions. Every other call()() with
# a computed target may reach any proc, so it counts as a sleep (the upper bound).
_COMPARATOR_USERS = (
    '/proc/sorted_insert', '/proc/assert_sorted', '/datum/heap/swim', '/datum/heap/sink',
    '/datum/heap/get_greater_child', '/datum/sort_instance/binarySort',
    '/datum/sort_instance/countRunAndMakeAscending', '/datum/sort_instance/gallopLeft',
    '/datum/sort_instance/gallopRight', '/datum/sort_instance/mergeLo',
    '/datum/sort_instance/mergeHi', '/datum/sort_instance/mergeAt2')
DYNAMIC_TARGETS = {
    # globals.dm: call(src, I)() over typesof(/datum/controller/global_vars/proc), the
    # InitGlobal<name> procs that the GLOBAL_* macros declare on the global_vars controller.
    '/datum/controller/global_vars/Initialize': ('type-procs', '/datum/controller/global_vars'),
    # signals.dm: call(listener, handler)(): the handlers registered with RegisterSignal. They
    # are SIGNAL_HANDLER procs (sync-mandated); registered handlers in coloured slots are listed
    # in nosleep-sites.tsv (context RegisterSignal).
    '/datum/_SendSignal': ('signal-handlers', None),
}
for _name in _COMPARATOR_USERS:
    # call(cmp)(a, b) with no object: a global comparator proc, by convention cmp_*.
    DYNAMIC_TARGETS[_name] = ('global-prefix', 'cmp_')

GLOBAL_ROOT = '/proc'

# Call-site kinds whose candidate slots form a union (no static receiver type).
UNION_KINDS = frozenset(('untyped', 'typed-miss', 'typed-down', 'unresolved', 'new-untyped'))


def type_depth(t):
    return 0 if not t else t.count('/')


class Slot:
    __slots__ = ('key', 'defs', 'last_at', 'colored', 'suspends', 'call_sites', 'union_sites',
                 'builtin')

    def __init__(self, key):
        self.key = key
        self.defs = []
        self.last_at = {}          # type -> the def that dispatch reaches at that type
        self.colored = False
        self.suspends = False
        self.call_sites = 0
        self.union_sites = 0
        self.builtin = False

    def root(self):
        return self.key[0] or GLOBAL_ROOT

    def display(self):
        return self.root() + '/' + self.key[1]

    def tg_defs(self):
        return [d for d in self.defs if not d.builtin]


class Site:
    """One call site: a named call, a new, a ..() call, a call()() or a builtin call."""
    __slots__ = ('caller', 'line', 'kind', 'targets', 'text')

    def __init__(self, caller, line, kind, targets, text=''):
        self.caller = caller
        self.line = line
        self.kind = kind
        self.targets = targets     # [(slot key, mode, type or def)]
        self.text = text

    def union(self):
        """An upper-bound site: the receiver (or the created type) is not known statically.
        An untyped receiver with a single candidate slot is a call of that slot's root."""
        if self.kind == 'new-untyped':
            return True
        return self.kind in UNION_KINDS and len(self.targets) > 1


class Options:
    def __init__(self, bound='upper', dynamic_calls='sleep', dispatch='narrow'):
        self.bound = bound                  # 'upper': union sites colour; 'lower': they are ignored
        self.dynamic_calls = dynamic_calls  # 'sleep': call()() with an unknown target may sleep
        # 'narrow': a call reaches the overrides its static receiver type can dispatch to;
        # 'slot': a call reaches every definition of the callee slot (comparison only)
        self.dispatch = dispatch


class Analysis:
    """The colouring of one parse. Its per-definition results (slot, parent, wf, sm, layer,
    witness, phase) are stored on the parse's Def objects, so the last Analysis constructed over
    a parse owns them; keep a summary (counts) of an earlier one, not the object."""

    def __init__(self, parse, options):
        self.p = parse
        self.opt = options
        self.defs = list(parse.defs)
        self._parent_cache = {}
        self._anc_cache = {}
        self._visible_cache = {}
        self._vis_def_cache = {}
        self._var_cache = {}
        self._param_cache = {}
        self.kind_counts = collections.Counter()
        self._build_types()
        self._add_builtin_defs()
        self._assign_slots()
        self._link_parents()
        self._resolve_events()

    # -- types --------------------------------------------------------------------------------
    def _build_types(self):
        types = set(self.p.types)
        types.update(BUILTIN_TYPE_PROCS)
        types.update(BUILTIN_PARENT)
        for d in self.defs:
            if d.type:
                types.add(d.type)
        for t in list(types):  # every prefix of a known path is a type too
            segs = t.strip('/').split('/')
            for k in range(1, len(segs)):
                types.add('/' + '/'.join(segs[:k]))
        self.types = types

    def parent(self, t):
        c = self._parent_cache.get(t, 0)
        if c != 0:
            return c
        if t in self.p.parent_type:
            r = self.p.parent_type[t]
        elif t in BUILTIN_PARENT:
            r = BUILTIN_PARENT[t]
        elif t in BUILTIN_ROOT_TYPES or not t:
            r = None
        else:
            segs = t.strip('/').split('/')
            r = '/datum' if len(segs) == 1 else '/' + '/'.join(segs[:-1])
        self._parent_cache[t] = r
        return r

    def ancestors(self, t):
        """(t, its parent, ...), cycle-safe."""
        r = self._anc_cache.get(t)
        if r is not None:
            return r
        seen = set()
        out = []
        x = t
        while x is not None and x not in seen:
            seen.add(x)
            out.append(x)
            x = self.parent(x)
        r = tuple(out)
        self._anc_cache[t] = r
        return r

    def is_subtype(self, t, base):
        return base in self.ancestors(t)

    def var_type(self, t, name):
        key = (t, name)
        if key in self._var_cache:
            return self._var_cache[key]
        r = None
        for x in self.ancestors(t):
            tv = self.p.type_vars.get(x)
            if tv is not None and name in tv:
                r = tv[name]
                break
            bv = BUILTIN_VARS.get(x)
            if bv is not None and name in bv:
                r = bv[name]
                break
        self._var_cache[key] = r
        return r

    def _var_declared(self, t, name):
        for x in self.ancestors(t):
            tv = self.p.type_vars.get(x)
            if tv is not None and name in tv:
                return True
            if name in BUILTIN_VARS.get(x, ()):
                return True
        return False

    # -- builtin declarations ------------------------------------------------------------------
    def _add_builtin_defs(self):
        builtin = []
        for t in sorted(BUILTIN_TYPE_PROCS):
            for name in BUILTIN_TYPE_PROCS[t]:
                d = Def('<builtin>', 0, t, name, True, False, builtin=True)
                if name in BUILTIN_SLEEP_TYPE_PROCS.get(t, ()):
                    d.seed_builtin = name
                builtin.append(d)
        self.defs = builtin + self.defs
        for i, d in enumerate(self.defs):
            d.idx = i

    # -- roots and slots -----------------------------------------------------------------------
    def _assign_slots(self):
        decls = collections.defaultdict(set)      # type -> names declared there
        root_stop = collections.defaultdict(set)  # type -> builtin names that are roots there
        for d in self.defs:
            if d.type and d.is_decl:
                decls[d.type].add(d.name)
            if d.builtin and d.type in BUILTIN_ROOT_TYPES:
                root_stop[d.type].add(d.name)
        self.decls = decls
        self.root_stop = root_stop
        # Overrides without any visible declaration (an undeclared override is a DM compile
        # error, so these are parse gaps or builtins the table misses): the topmost type that
        # defines the name in the chain becomes an implicit root.
        defined_at = collections.defaultdict(set)
        for d in self.defs:
            if d.type:
                defined_at[d.name].add(d.type)
        self.orphans = []
        for d in sorted((d for d in self.defs if d.type), key=lambda d: (type_depth(d.type), d.idx)):
            if self._visible_raw(d.type, d.name) is None:
                top = d.type
                for x in self.ancestors(d.type):
                    if x in defined_at[d.name]:
                        top = x
                decls[top].add(d.name)
                self._visible_cache.clear()
                self.orphans.append(d)
        self.slots = {}
        self.slots_by_name = collections.defaultdict(list)
        for d in self.defs:
            key = self.visible_slot(d.type, d.name) if d.type else ('', d.name)
            s = self.slots.get(key)
            if s is None:
                s = Slot(key)
                self.slots[key] = s
                self.slots_by_name[key[1]].append(s)
            s.defs.append(d)
            s.last_at[d.type] = d   # defs are in parse order: the last one at a type wins
            d.slot = s
        for s in self.slots.values():
            s.builtin = all(d.builtin for d in s.defs)
        for name in self.slots_by_name:
            self.slots_by_name[name].sort(key=lambda s: s.key)

    def _visible_raw(self, t, name):
        top = None
        for x in self.ancestors(t):
            if name in self.root_stop.get(x, ()):
                return (x, name)
            if name in self.decls.get(x, ()):
                top = x
        return (top, name) if top is not None else None

    def visible_slot(self, t, name):
        key = (t, name)
        r = self._visible_cache.get(key, 0)
        if r == 0:
            r = self._visible_raw(t, name)
            self._visible_cache[key] = r
        return r

    def visible_def(self, key, t):
        """The def that a call on a receiver of exact type t reaches in slot key."""
        ck = (key, t)
        r = self._vis_def_cache.get(ck, 0)
        if r != 0:
            return r
        s = self.slots[key]
        r = None
        if not key[0]:
            r = s.last_at.get('')
        else:
            for x in self.ancestors(t):
                r = s.last_at.get(x)
                if r is not None:
                    break
        self._vis_def_cache[ck] = r
        return r

    def dispatchable(self, d):
        return d.slot.last_at.get(d.type) is d

    # -- override chain, waitfor and sync-mandated inheritance -----------------------------------
    def _link_parents(self):
        for s in self.slots.values():
            by_type = collections.defaultdict(list)
            for d in s.defs:  # parse order (builtin declarations first)
                by_type[d.type].append(d)
            for t, ds in by_type.items():
                for k, d in enumerate(ds):
                    if k > 0:
                        d.parent = ds[k - 1]
                        continue
                    par = None
                    if t:
                        for x in self.ancestors(t)[1:]:
                            if x in by_type:
                                par = by_type[x][-1]
                                break
                    d.parent = par
        for d in sorted(self.defs, key=self._chain_len):  # parents first
            par = d.parent
            if d.own_waitfor is not None:
                d.wf = d.own_waitfor
            else:
                d.wf = par.wf if par is not None else True
            d.sm = d.sigh or d.sns or (par.sm if par is not None else False)

    def _chain_len(self, d):
        n = 0
        seen = set()
        while d.parent is not None and d.idx not in seen:
            seen.add(d.idx)
            d = d.parent
            n += 1
        return n

    # -- call resolution -------------------------------------------------------------------------
    def _params(self, d):
        r = self._param_cache.get(d.idx)
        if r is not None:
            return r
        r = {}
        for a in _split_args(d.args):
            a = a.strip()
            if not a or a == '...':
                continue
            a = a.split('=')[0]
            a = a.split(' as ')[0].split(' in ')[0].strip()
            segs = [x for x in a.split('/') if x]
            if segs and segs[0] == 'var':
                segs = segs[1:]
            if not segs:
                continue
            name, typ, _ = parse_var_path(segs)
            if name:
                r[name] = typ
        self._param_cache[d.idx] = r
        return r

    @staticmethod
    def local_decl(d, name, line):
        """The declaration of local `name` in scope at `line`: the most recent one at or before
        it, as (True, declared type or None for an untyped local); (False, None) when no local of
        that name is declared before the line (the name is then a parameter or a var)."""
        found = (False, None)
        for ln, typ in d.locals.get(name, ()):
            if ln > line:
                break
            found = (True, typ)
        return found

    def chain_type(self, d, segs, line):
        """Static type of a receiver chain (('src',), ('user', 'client'), ...) at `line`, or None."""
        first = segs[0]
        is_local, ltype = self.local_decl(d, first, line)
        if first == 'src':
            t = d.type or None
        elif first == 'usr':
            t = '/mob'
        elif first == 'world':
            t = '/world'
        elif first == 'args':
            t = '/list'
        elif is_local:
            t = ltype  # an untyped local shadows a typed var of the same name
        else:
            params = self._params(d)
            if first in params:
                t = params[first]
            else:
                t = self.var_type(d.type, first) if d.type else None
                if t is None and (not d.type or not self._var_declared(d.type, first)):
                    t = self.p.global_vars.get(first)
        for seg in segs[1:]:
            if t is None:
                return None
            t = self.var_type(t, seg)
        return t

    def _all(self, name):
        return [(s.key, 'all', None) for s in self.slots_by_name.get(name, ())]

    def resolve_call(self, d, name, recv, line):
        """(kind, targets). kind: self, global, typed, typed-down, typed-miss, untyped,
        unresolved, seed, step, builtin."""
        if recv is None:
            if d.type:
                key = self.visible_slot(d.type, name)
                if key is not None:
                    return 'self', [(key, 'virtual', d.type)]
            if ('', name) in self.slots:
                return 'global', [(('', name), 'exact', '')]
            if name in BUILTIN_SLEEP_GLOBALS:
                return 'seed', []
            if name in STEP_PROCS:
                return 'step', [(self.visible_slot('/atom/movable', 'Move'), 'virtual', '/atom/movable')]
            if name in BUILTIN_GLOBALS:
                return 'builtin', []
            cands = self._all(name)
            return ('unresolved', cands) if cands else ('builtin', [])
        if recv == '?':
            return 'untyped', self._all(name)
        if recv == ('global',):
            if ('', name) in self.slots:
                return 'global', [(('', name), 'exact', '')]
            return 'untyped', self._all(name)
        if recv[0].startswith('/'):  # call(obj, /t::name()): the definition visible at /t
            key = self.visible_slot(recv[0], name)
            if key is not None:
                return 'typed', [(key, 'exact', recv[0])]
            return 'untyped', self._all(name)
        if recv[0] == 'global' and len(recv) == 2:
            t = self.p.global_vars.get(recv[1])
        else:
            t = self.chain_type(d, recv, line)
        if t is None:
            return 'untyped', self._all(name)
        key = self.visible_slot(t, name)
        if key is not None:
            return 'typed', [(key, 'virtual', t)]
        cands = [(s.key, 'all', None) for s in self.slots_by_name.get(name, ())
                 if s.key[0] and self.is_subtype(s.key[0], t)]
        if cands:
            return 'typed-down', cands
        return 'typed-miss', self._all(name)

    def resolve_new(self, d, line, typ, target, expr=None, assigned=None):
        """new /t() and implicit new: exactly the New visible at t. new expr(): the created
        type is a subtype of a static type when tg gives one (expr is X.type or a typed path
        var, or the result goes into a typed var); otherwise any New (the upper bound)."""
        if typ is None and target is not None:
            typ = self.chain_type(d, target, line)
        if typ:
            key = self.visible_slot(typ, 'New')
            if key is not None:
                return 'new', [(key, 'exact', typ)]
        base = None
        if expr and len(expr) == 1 and expr[0] in d.path_locals:
            base = d.path_locals[expr[0]]
        elif expr:
            if expr[-1] == 'type':
                base = self.chain_type(d, expr[:-1], line) if len(expr) > 1 else (d.type or None)
            else:
                base = self.chain_type(d, expr, line)
            if base == '/list':
                base = None  # a list of paths: unknown element type
        if base is None and assigned:
            base = assigned[0] or (self.chain_type(d, assigned[1], line) if assigned[1] else None)
        if base:
            key = self.visible_slot(base, 'New')
            if key is not None:
                return 'new-typed', [(key, 'virtual', base)]
        return 'new-untyped', [(('/datum', 'New'), 'all', None)]

    def resolve_dynamic(self, d, text):
        """Targets of a call()() whose proc is computed; None means unknown (may sleep)."""
        if '__PROC__' in text:
            return 'dyn-self', [(d.slot.key, 'virtual', d.type) if d.type else (d.slot.key, 'exact', '')]
        spec = DYNAMIC_TARGETS.get(d.display())
        if spec is None:
            return 'dynamic', None
        how, arg = spec
        if how == 'type-procs':
            keys = sorted({x.slot.key for x in self.defs
                           if x.type == arg and x.is_decl and not x.builtin})
            return 'dyn-known', [(k, 'exact', arg) for k in keys]
        if how == 'global-prefix':
            keys = sorted(k for k in self.slots if not k[0] and k[1].startswith(arg))
            return 'dyn-known', [(k, 'exact', '') for k in keys]
        return 'dyn-signal', []  # signal-handlers: checked through the RegisterSignal sites

    def resolve_handler(self, d, line, name, typ, recv):
        if typ == ('global',):
            return [(('', name), 'exact', '')] if ('', name) in self.slots else []
        if typ is None:
            if recv is None:
                typ = d.type or None
            elif recv != '?':
                typ = self.chain_type(d, recv, line)
        if typ:
            key = self.visible_slot(typ, name)
            return [(key, 'virtual', typ)] if key is not None else []
        return self._all(name)

    def _resolve_events(self):
        """Builds the call sites, seeds, ..() edges and RegisterSignal handler sites."""
        self.sites = []
        self.seed_sites = []       # (def, line, primitive): direct sleep primitives
        self.dyn_sites = []        # (def, line, text): call()() with an unknown target
        self.seeds = {}            # def idx -> (line, primitive), the first one
        self.dyn_seeds = {}        # def idx -> (line, text), the first one
        self.handler_sites = []
        self.super_children = collections.defaultdict(list)  # parent def idx -> [Site]
        for d in self.defs:
            if d.builtin:
                for typ, name in BUILTIN_CALLS.get((d.type, d.name), ()):
                    key = self.visible_slot(typ, name)
                    if key is not None:
                        self.sites.append(Site(d, 0, 'builtin-call', [(key, 'virtual', typ)]))
                continue
            for ev in d.events:
                k = ev[0]
                if k == 'call':
                    kind, targets = self.resolve_call(d, ev[2], ev[3], ev[1])
                    self.kind_counts[kind] += 1
                    if kind == 'seed':
                        self.seed_sites.append((d, ev[1], ev[2]))
                        if d.idx not in self.seeds or ev[1] < self.seeds[d.idx][0]:
                            self.seeds[d.idx] = (ev[1], ev[2])
                        continue
                    if targets:
                        self.sites.append(Site(d, ev[1], kind, targets, ev[2]))
                elif k == 'new':
                    kind, targets = self.resolve_new(d, ev[1], ev[2], ev[3], ev[4], ev[5])
                    self.kind_counts[kind] += 1
                    self.sites.append(Site(d, ev[1], kind, targets, 'new'))
                elif k == 'super':
                    self.kind_counts['super'] += 1
                    if d.parent is not None:
                        site = Site(d, ev[1], 'super', [(d.slot.key, 'super', d.parent)], '..()')
                        self.sites.append(site)
                        self.super_children[d.parent.idx].append(site)
                elif k == 'dyn':
                    kind, targets = self.resolve_dynamic(d, ev[2])
                    self.kind_counts[kind] += 1
                    if targets is None:
                        if self.opt.dynamic_calls == 'sleep':
                            self.dyn_sites.append((d, ev[1], ev[2]))
                            if d.idx not in self.dyn_seeds or ev[1] < self.dyn_seeds[d.idx][0]:
                                self.dyn_seeds[d.idx] = (ev[1], ev[2])
                    elif targets:
                        self.sites.append(Site(d, ev[1], kind, targets, ev[2]))
                elif k == 'handler':
                    targets = self.resolve_handler(d, ev[1], ev[2], ev[3], ev[4] if len(ev) > 4 else None)
                    if targets:
                        self.handler_sites.append(Site(d, ev[1], 'handler', targets, ev[2]))
        if self.opt.dispatch == 'slot':
            for site in self.sites:
                if site.kind != 'super':
                    site.targets = [(key, 'all', None) for key, _, _ in site.targets]
            for site in self.handler_sites:
                site.targets = [(key, 'all', None) for key, _, _ in site.targets]
        # Count call sites per slot.
        for site in self.sites:
            if site.kind == 'super' or site.kind == 'builtin-call':
                continue
            if site.union():
                for key, _, _ in site.targets:
                    self.slots[key].union_sites += 1
            else:
                for key, _, _ in site.targets:
                    self.slots[key].call_sites += 1
        # Index the sites by what can reach them: typed sites (phase 1, the lower bound) and
        # union sites (phase 2, untyped or ambiguous receivers: the upper bound).
        self.index = (_Index(), _Index())
        for site in self.sites:
            if site.kind == 'super':
                continue  # super_children
            ix = self.index[1 if site.union() else 0]
            for key, mode, t in site.targets:
                if mode == 'all':
                    ix.all[key].append(site)
                else:
                    if mode == 'virtual':
                        ix.virtual[(key, t)].append(site)
                    vis = self.visible_def(key, t)
                    if vis is not None:
                        ix.vis[vis.idx].append(site)

    # -- target sets -------------------------------------------------------------------------------
    def target_defs(self, key, mode, t):
        """The defs a target can dispatch to (for reporting; propagation uses the indexes)."""
        s = self.slots[key]
        if mode == 'super':
            return [t]
        if mode == 'all':
            return [d for d in s.defs if self.dispatchable(d)]
        vis = self.visible_def(key, t)
        out = [vis] if vis is not None else []
        if mode == 'virtual':
            out.extend(d for d in s.defs if d is not vis and self.dispatchable(d)
                       and d.type != t and self.is_subtype(d.type, t))
        return out

    def target_suspends(self, key, mode, t):
        """A def of the target set suspends its caller (valid after run())."""
        ck = (key, mode, t if mode != 'super' else t.idx)
        r = self._suspend_cache.get(ck)
        if r is None:
            r = any(d.layer is not None and d.wf for d in self.target_defs(key, mode, t))
            self._suspend_cache[ck] = r
        return r

    # -- propagation -------------------------------------------------------------------------------
    def run(self):
        """Layered (breadth-first) fixpoint, so every witness chain is a shortest one.

        Phase 1 propagates from the sleep primitives through statically resolved call sites
        only (the lower bound); phase 2 adds the union sites (untyped or ambiguous receivers,
        new of an unknown type) and call()() with an unknown target, and continues to the upper
        bound. Defs coloured in phase 1 keep their phase-1 witnesses."""
        defs = self.defs
        self._suspend_cache = {}
        for d in defs:
            d.layer = None
            d.witness = None
            d.phase = None
        for s in self.slots.values():
            s.colored = s.suspends = False
        frontier = {}
        for d in defs:
            if d.sm:
                continue
            if d.builtin:
                if d.seed_builtin:
                    frontier[d.idx] = ('builtin', 0, d.seed_builtin)
                continue
            if d.idx in self.seeds:
                line, prim = self.seeds[d.idx]
                frontier[d.idx] = ('seed', line, prim)
        self.layers = 0
        self._propagate(frontier, (self.index[0],), 1)
        if self.opt.bound == 'upper':
            # Phase 2: every def that already suspends its caller reaches the union sites, and
            # call()() with an unknown target counts as a sleep.
            nxt = {}
            for d in defs:
                if d.layer is not None and d.wf:
                    self._reach(d, (self.index[1],), nxt, super_edges=False)
            for idx in sorted(self.dyn_seeds):
                d = defs[idx]
                if d.layer is None and not d.sm:
                    line, text = self.dyn_seeds[idx]
                    self._offer(nxt, d, ('dyn', line, text))
            self._propagate(nxt, self.index, 2)
        self.counts = (sum(1 for s in self.slots.values() if s.colored and not s.builtin),
                       sum(1 for d in defs if not d.builtin and d.layer is not None))
        return self

    def _offer(self, nxt, c, w):
        if c.layer is not None or c.sm:
            return
        old = nxt.get(c.idx)
        if old is None or _wkey(w) < _wkey(old):
            nxt[c.idx] = w

    def _reach(self, d, indexes, nxt, super_edges=True):
        """Offers every caller that a def suspending its caller reaches."""
        if super_edges:
            for site in self.super_children.get(d.idx, ()):
                self._offer(nxt, site.caller, ('super', site.line, d))
        disp = self.dispatchable(d)
        key = d.slot.key
        for ix in indexes:
            reach = list(ix.vis.get(d.idx, ()))
            if disp:
                reach.extend(ix.all.get(key, ()))
                if d.type:
                    for t in self.ancestors(d.type):
                        reach.extend(ix.virtual.get((key, t), ()))
            for site in reach:
                self._offer(nxt, site.caller, ('call', site.line, d, site.kind))

    def _propagate(self, frontier, indexes, phase):
        defs = self.defs
        while frontier:
            active = sorted(frontier.items(), key=lambda kv: (defs[kv[0]].file, defs[kv[0]].line, kv[0]))
            for idx, w in active:
                d = defs[idx]
                d.layer = self.layers
                d.witness = w
                d.phase = phase
                d.slot.colored = True
                if d.wf:
                    d.slot.suspends = True
            nxt = {}
            for idx, _ in active:
                d = defs[idx]
                if d.wf:  # waitfor = FALSE: the caller gets '.' at the first sleep
                    self._reach(d, indexes, nxt)
            frontier = nxt
            self.layers += 1

    # -- reporting helpers ---------------------------------------------------------------------------
    def chain(self, d, limit=80):
        """Witness chain: 'def (file:line)' is the def and the line where it makes the next call."""
        parts = []
        while d is not None and limit:
            limit -= 1
            w = d.witness
            if w is None:
                parts.append(d.display() + ' (?)')
                break
            if w[0] == 'builtin':
                parts.append(d.display() + ' (builtin, sleeps)')
                break
            parts.append('%s (%s:%d)' % (d.display(), d.file, w[1]) if not d.builtin
                         else '%s (builtin)' % d.display())
            if w[0] == 'seed':
                parts.append(w[2])
                break
            if w[0] == 'dyn':
                parts.append('call()() ' + ' '.join(w[2].split()))
                break
            if w[0] == 'super':
                parts.append('..()')
            d = w[2]
        return ' -> '.join(parts)

    def slot_witness_def(self, s):
        """The def a slot's witness starts from: one that suspends its caller if any (else
        one that may sleep), preferring tg defs over builtin ones, then the lower bound, then
        the shortest chain."""
        cands = [d for d in s.defs if d.layer is not None and (d.wf or not s.suspends)]
        if not cands:
            return None
        return min(cands, key=lambda d: (d.builtin, d.phase, d.layer, d.file, d.line, d.idx))

    def slot_witness(self, s):
        d = self.slot_witness_def(s)
        return self.chain(d) if d is not None else ''

    def sync_context(self, d):
        """Why a def's C# method is synchronous, or None when it can await.

        waiting-model.md v1 4.2: a slot whose callers must wait (suspends) is async in every
        definition; in any other slot a definition is a plain method, except one that may sleep
        (waitfor = FALSE, own or inherited), whose body is the async __body of the 5.4 wrapper."""
        if d.sm:
            return 'SIGNAL_HANDLER' if _own_or_inherited(d, 'sigh') else 'SHOULD_NOT_SLEEP'
        if not d.slot.suspends and d.layer is None:
            return 'sync'
        return None

    def nosleep_sites(self):
        """Call sites in synchronous C# methods whose callee slot suspends (Waiting.NoSleep): a
        callee slot that does not suspend has a synchronous signature (waiting-model.md 4.2).

        Yields (site, context, targets that suspend, may_suspend). Direct sleep primitives and
        unknown call()() targets in sync-mandated defs are yielded as sites without targets."""
        for d, line, prim in self.seed_sites:
            if d.sm:
                yield Site(d, line, 'seed', [], prim), self.sync_context(d), [], True
        for d, line, text in self.dyn_sites:
            if d.sm:
                yield Site(d, line, 'dynamic', [], text), self.sync_context(d), [], True
        for site in self.sites:
            if site.kind == 'builtin-call':
                continue
            ctx = self.sync_context(site.caller)
            if ctx is None:
                continue
            col = [(key, mode, t) for key, mode, t in site.targets if self.slots[key].suspends]
            if not col:
                continue
            may = any(self.target_suspends(key, mode, t) for key, mode, t in col)
            yield site, ctx, col, may
        for site in self.handler_sites:
            col = [(key, mode, t) for key, mode, t in site.targets if self.slots[key].suspends]
            if not col:
                continue
            may = any(self.target_suspends(key, mode, t) for key, mode, t in col)
            yield site, 'RegisterSignal', col, may


class _Index:
    """Call sites by what can reach them (see Analysis._reach)."""
    __slots__ = ('all', 'virtual', 'vis')

    def __init__(self):
        self.all = collections.defaultdict(list)       # slot key -> sites ('all' targets)
        self.virtual = collections.defaultdict(list)   # (slot key, T) -> sites ('virtual')
        self.vis = collections.defaultdict(list)       # def idx -> sites whose visible def it is


def _own_or_inherited(d, attr):
    seen = set()
    while d is not None and d.idx not in seen:
        if getattr(d, attr):
            return True
        seen.add(d.idx)
        d = d.parent
    return False


def _wkey(w):
    # Deterministic preference between witnesses offered in the same layer: the earliest line,
    # then calls before ..() before call()(), then the callee's position.
    if w[0] == 'call':
        return (w[1], 0, w[2].file, w[2].line, w[2].idx)
    if w[0] == 'super':
        return (w[1], 1, w[2].file, w[2].line, w[2].idx)
    return (w[1], 2, '', 0, 0)
