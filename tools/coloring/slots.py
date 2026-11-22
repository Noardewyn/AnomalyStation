"""The slot registry: the C# signature of every slot (translation-rules.md 7.1, Appendix A).

For each slot with tg definitions (root type path + proc name, as in colored-slots.tsv), the
signature is computed from every definition of the slot by translation-rules.md v1:

* 3.3, parameters: the root declaration fixes the parameter names and types (P1 typed: the class,
  DmList for list/x and x[]; P2 untyped without a default or with '= null': object; P3 untyped
  with a numeric constant default: float?; P4 untyped with a text literal default, or a proc
  reference default ('/proc/x', GLOBAL_PROC_REF, PROC_REF; text by 7.7): string; P5 a
  non-constant default: the P1/P2 type; P6 '...': the params array; P7 'as' hints ignored). Every
  parameter gets '= null'. The constant test runs on the macro-expanded default: numbers,
  operators and BYOND's builtin numeric constants (stddef: TRUE, FALSE, NORTH, ...).
* P1's 'Type under V3's test': a typed parameter is Type when a definition of the slot gives it a
  typepath default (a proc path such as '/proc/x' is not one: it is text, 7.7) or uses it as a type
  in 'new P' or 'typesof(P'. 'ispath(P)' is a type test
  (3.4 below), not a use as a type: a parameter that tg tests with ispath holds a path or an
  instance. When the 3.4 analysis reaches no verdict for such a parameter, it is listed as
  undecided (the value is a typepath on one side of the test) and the declared type is kept. 'initial(P.v)'
  and 'P::v' read an instance as well as a type (translation-rules.md 10.3), so the scanner
  cannot decide them: they are listed as undecided and the declared type is kept.
* 3.4, typed parameters that tg fills with other kinds: object when a definition type-tests the
  parameter and reads it again on a path where the test showed it is not of its declared type
  (typeparams_object below); the cases the regex front end cannot follow are listed as undecided.
* 3.5 '-' cells: a default the root's parameter cannot hold retypes the parameter object: for
  an untyped parameter typed by its root default, an override default of another known kind (a
  text default where the root has a numeric one); for a typed parameter, a text or numeric
  default in any definition (a typepath default makes it Type instead, P1 above).
* 7.2, variadic: the root declares '...', a definition reads 'args' (7.6), or an override
  declares more parameters than the root: a trailing 'params object[] __extra'. An override whose
  parameter names differ from the root's (fewer, other or more) needs [DmParams] (counted).
* 7.4, return type: void when no definition returns a value of its own ('return <expr>' other
  than 'return ..()', 'return .' and 'return null', or an assignment to '.' other than
  '. = ..()') and the BYOND builtin root, if any, returns nothing; /datum/New is always void.
  Narrowing to float/string/class is a per-slot decision of Appendix A, never made here.
* waiting-model.md v1 4.2, waiting: a slot whose callers must wait (colored-slots.tsv
  'suspends = yes': some definition may sleep with an effective waitfor = TRUE) returns DmTask
  (void) or DmTask<object> (object) on the root and every override. A coloured slot whose
  sleeping definitions are all waitfor = FALSE ('colored = yes', 'suspends = no') stays
  synchronous; those definitions get the callee-side wrapper of waiting-model.md 5.4.
* 2.2 and 2.4, names: C# keywords get '@' ('@await' always); a proc whose name is also a var
  declared on the same type or along one ancestor line is renamed '<name>__proc'. 2.3, class
  names: the path with '/' -> '_', the shorter of two colliding paths gets '__2'.

BYOND builtin roots (/datum/New, /atom/Enter, ...) take their parameters from the BYOND
reference's 'Format' line when tg does not define the proc on the root type itself.
"""

import collections
import re

import dmscan
import typeorder

# BYOND builtin procs on builtin types: parameters (BYOND reference info.html, 'Format' of
# /<type>/proc/<name>; OpenDream DMStandard where the reference shows none) and whether the
# builtin returns a value ('Returns' of the same entry).
BUILTIN_PARAMS = {
    ('/datum', 'New'): ('', False), ('/datum', 'Del'): ('', False),
    ('/datum', 'Topic'): ('href, href_list[]', False), ('/datum', 'Read'): ('savefile/F', False),
    ('/datum', 'Write'): ('savefile/F', False),
    ('/atom', 'Click'): ('location, control, params', False),
    ('/atom', 'DblClick'): ('location, control, params', False),
    ('/atom', 'MouseDown'): ('location, control, params', False),
    ('/atom', 'MouseDrag'): ('over_object, src_location, over_location, src_control, over_control, params', False),
    ('/atom', 'MouseDrop'): ('over_object, src_location, over_location, src_control, over_control, params', False),
    ('/atom', 'MouseEntered'): ('location, control, params', False),
    ('/atom', 'MouseExited'): ('location, control, params', False),
    ('/atom', 'MouseMove'): ('location, control, params', False),
    ('/atom', 'MouseUp'): ('location, control, params', False),
    ('/atom', 'MouseWheel'): ('delta_x, delta_y, location, control, params', False),
    ('/atom', 'Entered'): ('atom/movable/Obj, atom/OldLoc', False),
    ('/atom', 'Exited'): ('atom/movable/Obj, atom/newloc', False),
    ('/atom', 'Uncrossed'): ('atom/movable/O', False), ('/atom', 'Crossed'): ('atom/movable/O', False),
    ('/atom', 'Cross'): ('atom/movable/O', True), ('/atom', 'Uncross'): ('atom/movable/O', True),
    ('/atom', 'Enter'): ('atom/movable/O, atom/oldloc', True),
    ('/atom', 'Exit'): ('atom/movable/O, atom/newloc', True),
    ('/atom', 'Stat'): ('', False),
    ('/atom/movable', 'Bump'): ('atom/Obstacle', False),
    ('/atom/movable', 'Move'): ('NewLoc, Dir=0, step_x=0, step_y=0', True),
    ('/mob', 'Login'): ('', False), ('/mob', 'Logout'): ('', False),
    ('/client', 'New'): ('TopicData', True), ('/client', 'Del'): ('', False),
    ('/client', 'Topic'): ('href, href_list[], hsrc', False), ('/client', 'Stat'): ('', False),
    ('/client', 'Command'): ('command as command_text', False),
    ('/client', 'Import'): ('Query', True), ('/client', 'Export'): ('file', False),
    ('/client', 'AllowUpload'): ('filename, filelength', True),
    ('/client', 'SoundQuery'): ('', True), ('/client', 'MeasureText'): ('text, style, width=0', True),
    ('/client', 'Move'): ('loc, dir', True), ('/client', 'Center'): ('', False),
    ('/client', 'IsByondMember'): ('', True), ('/client', 'CheckPassport'): ('passport_identifier', True),
    ('/client', 'SendPage'): ('msg, recipient, options', True), ('/client', 'GetAPI'): ('Api, Name', True),
    ('/client', 'SetAPI'): ('Api, Key, Value', False), ('/client', 'RenderIcon'): ('object', True),
    ('/client', 'Click'): ('object, location, control, params', False),
    ('/client', 'DblClick'): ('object, location, control, params', False),
    ('/client', 'MouseDown'): ('object, location, control, params', False),
    ('/client', 'MouseDrag'): ('src_object, over_object, src_location, over_location, src_control, over_control, params', False),
    ('/client', 'MouseDrop'): ('src_object, over_object, src_location, over_location, src_control, over_control, params', False),
    ('/client', 'MouseEntered'): ('object, location, control, params', False),
    ('/client', 'MouseExited'): ('object, location, control, params', False),
    ('/client', 'MouseMove'): ('object, location, control, params', False),
    ('/client', 'MouseUp'): ('object, location, control, params', False),
    ('/client', 'MouseWheel'): ('object, delta_x, delta_y, location, control, params', False),
    ('/world', 'New'): ('', False), ('/world', 'Del'): ('', False),
    ('/world', 'Profile'): ('command, format', True), ('/world', 'GetConfig'): ('config_set, param', True),
    ('/world', 'SetConfig'): ('config_set, param, value', False), ('/world', 'OpenPort'): ('port', True),
    ('/world', 'IsSubscribed'): ('player, type', True),
    ('/world', 'IsBanned'): ('key, address, computer_id, type', True),
    ('/world', 'Error'): ('exception', False), ('/world', 'Reboot'): ('reason', False),
    ('/world', 'Repop'): ('', False), ('/world', 'Export'): ('Addr, File, Persist, Clients, Method', True),
    ('/world', 'Import'): ('', True),
    # world/Topic: "the return value is sent back to the caller" (BYOND reference, world Topic).
    ('/world', 'Topic'): ('T, Addr, Master, Keys', True),
    ('/world', 'Tick'): ('', False), ('/world', 'SetScores'): ('key, fields', True),
    ('/world', 'GetScores'): ('key, fields', True), ('/world', 'GetMedal'): ('medal, player', True),
    ('/world', 'SetMedal'): ('medal, player', True), ('/world', 'ClearMedal'): ('medal, player', True),
    ('/world', 'AddCredits'): ('player, credits, note', True),
    ('/world', 'GetCredits'): ('player', True), ('/world', 'PayCredits'): ('player, credits, note', True),
}
for _name in ('North', 'South', 'East', 'West', 'Northeast', 'Northwest', 'Southeast', 'Southwest'):
    BUILTIN_PARAMS[('/client', _name)] = ('', True)

# BYOND builtin constants (stddef.dm; OpenDream DMStandard/Defines.dm and _Globals.dm) that tg
# uses without defining: numeric ones count as numeric constants in a default, text ones as text.
BYOND_NUMERIC = frozenset('''TRUE FALSE NORTH SOUTH EAST WEST UP DOWN NORTHEAST SOUTHEAST SOUTHWEST
    NORTHWEST FLOAT_LAYER AREA_LAYER TURF_LAYER OBJ_LAYER MOB_LAYER FLY_LAYER EFFECTS_LAYER
    TOPDOWN_LAYER BACKGROUND_LAYER FLOAT_PLANE ANIMATION_END_NOW ANIMATION_LINEAR_TRANSFORM
    ANIMATION_PARALLEL ANIMATION_SLICE ANIMATION_END_LOOP ANIMATION_RELATIVE ANIMATION_CONTINUE
    LINEAR_EASING SINE_EASING CIRCULAR_EASING CUBIC_EASING BOUNCE_EASING ELASTIC_EASING
    BACK_EASING QUAD_EASING JUMP_EASING EASE_IN EASE_OUT NO_STEPS FORWARD_STEPS SLIDE_STEPS
    SYNC_STEPS ICON_ADD ICON_SUBTRACT ICON_MULTIPLY ICON_OVERLAY ICON_AND ICON_OR ICON_UNDERLAY
    SEE_INFRA SEE_SELF SEE_MOBS SEEMOBS SEE_OBJS SEEOBJS SEE_TURFS SEETURFS SEE_PIXELS SEE_THRU
    SEE_BLACKNESS BLIND MOB_PERSPECTIVE EYE_PERSPECTIVE EDGE_PERSPECTIVE BLEND_DEFAULT
    BLEND_OVERLAY BLEND_ADD BLEND_SUBTRACT BLEND_MULTIPLY BLEND_INSET_OVERLAY SOUND_MUTE
    SOUND_PAUSED SOUND_STREAM SOUND_UPDATE COLORSPACE_RGB COLORSPACE_HSV COLORSPACE_HSL
    COLORSPACE_HCY FILTER_COLOR_RGB FILTER_COLOR_HSV FILTER_COLOR_HSL FILTER_COLOR_HCY LONG_GLIDE
    RESET_COLOR RESET_ALPHA RESET_TRANSFORM NO_CLIENT_COLOR KEEP_TOGETHER KEEP_APART PLANE_MASTER
    TILE_BOUND PIXEL_SCALE PASS_MOUSE TILE_MOVER MATRIX_COPY MATRIX_MULTIPLY MATRIX_ADD
    MATRIX_SUBTRACT MATRIX_INVERT MATRIX_INTERPOLATE MATRIX_ROTATE MATRIX_SCALE MATRIX_TRANSLATE
    MATRIX_MODIFY PROFILE_STOP PROFILE_CLEAR PROFILE_AVERAGE PROFILE_START PROFILE_REFRESH
    PROFILE_RESTART MASK_INVERSE MASK_SWAP FILTER_OVERLAY FILTER_UNDERLAY OUTLINE_SHARP
    OUTLINE_SQUARE WAVE_BOUNDED WAVE_SIDEWAYS MOUSE_INACTIVE_POINTER MOUSE_ACTIVE_POINTER
    MOUSE_DRAG_POINTER MOUSE_DROP_POINTER MOUSE_ARROW_POINTER MOUSE_CROSSHAIRS_POINTER
    MOUSE_HAND_POINTER MOUSE_LEFT_BUTTON MOUSE_RIGHT_BUTTON MOUSE_MIDDLE_BUTTON MOUSE_CTRL_KEY
    MOUSE_SHIFT_KEY MOUSE_ALT_KEY CONTROL_FREAK_ALL CONTROL_FREAK_SKIN CONTROL_FREAK_MACROS
    VIS_INHERIT_ICON VIS_INHERIT_ICON_STATE VIS_INHERIT_DIR VIS_INHERIT_LAYER VIS_INHERIT_PLANE
    VIS_INHERIT_ID VIS_UNDERLAY VIS_HIDE TOPDOWN_MAP ISOMETRIC_MAP SIDE_MAP TILED_ICON_MAP
    LEGACY_MOVEMENT_MODE TILE_MOVEMENT_MODE PIXEL_MOVEMENT_MODE UNIFORM_RAND NORMAL_RAND
    LINEAR_RAND SQUARE_RAND JSON_PRETTY_PRINT'''.split())
BYOND_TEXT = frozenset(('MALE', 'FEMALE', 'NEUTER', 'PLURAL', 'UNIX', 'MS_WINDOWS'))

# BYOND builtin vars per builtin type (OpenDream DMStandard Types/*.dm var declarations), for the
# var/proc name clash rule (translation-rules.md 2.4).
BUILTIN_TYPE_VARS = {
    '/datum': 'type parent_type vars tag',
    '/atom': '''name text desc suffix verbs contents overlays underlays vis_locs vis_contents loc
        dir x y z pixel_x pixel_y pixel_z pixel_w icon_w icon_z icon icon_state layer plane alpha
        color invisibility mouse_opacity infra_luminosity luminosity opacity transform blend_mode
        gender density maptext filters appearance appearance_flags maptext_width maptext_height
        maptext_x maptext_y step_x step_y render_source mouse_drag_pointer mouse_drop_pointer
        mouse_over_pointer mouse_drop_zone render_target vis_flags pixloc''',
    '/atom/movable': '''screen_loc animate_movement locs glide_size step_size bound_x bound_y
        bound_width bound_height bounds particles''',
    '/mob': 'client key ckey group see_invisible see_infrared sight see_in_dark',
    '/client': '''verbs screen images vars statobj statpanel default_verb_category tag type mob eye
        lazy_eye perspective edge_limit view pixel_x pixel_y pixel_z pixel_w show_popup_menus
        show_verb_panel byond_version byond_build address inactivity key ckey connection
        computer_id tick_lag authenticate timezone script color control_freak mouse_pointer_icon
        preload_rsc fps dir gender glide_size virtual_eye bounds bound_x bound_y bound_width
        bound_height''',
    '/world': '''contents vars log area turf mob name time timezone timeofday realtime tick_lag cpu
        fps tick_usage loop_checks maxx maxy maxz icon_size view movement_mode byond_version
        byond_build version address port internet_address url visibility status process params
        sleep_offline system_type map_cpu hub hub_password reachable game_state host map_format
        cache_lifespan executor''',
}

# C# keywords (C# language reference); a DM name that is one gets '@' (translation-rules.md 2.2).
CS_KEYWORDS = frozenset('''abstract as base bool break byte case catch char checked class const
    continue decimal default delegate do double else enum event explicit extern false finally fixed
    float for foreach goto if implicit in int interface internal is lock long namespace new null
    object operator out override params private protected public readonly ref return sbyte sealed
    short sizeof stackalloc static string struct switch this throw true try typeof uint ulong
    unchecked unsafe ushort using virtual void volatile while await'''.split())

TYPE_TESTS = {  # BYOND's type-test procs with a fixed tested type
    'ismob': '/mob', 'isturf': '/turf', 'isarea': '/area', 'ismovable': '/atom/movable',
    'isloc': '/atom', 'islist': '/list',
}
KIND_TESTS = ('isnum', 'istext', 'ispath', 'isicon', 'isfile')  # tests for a non-object kind

_NUM_TOKEN = re.compile(r'\s+|0[xX][0-9a-fA-F]+|(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?|[-+*/%()<>&|^~!]|[A-Za-z_]\w*')
_PATH_LITERAL = re.compile(r'/[A-Za-z_][\w/]*$')
# A proc reference is text, never a typepath (translation-rules.md 7.7; 3.2 V3 and 3.3 P1 exclude it):
# a proc or verb path ('/proc/x', '/verb/x', '/t/proc/x'; GLOBAL_PROC_REF(x) expands to '/proc/x') and
# the nameof() that PROC_REF, VERB_REF, TYPE_PROC_REF and TYPE_VERB_REF expand to.
_PROC_PATH = re.compile(r'(?:/[A-Za-z_]\w*)*/(?:proc|verb)/[A-Za-z_]\w*$')
_NAMEOF = re.compile(r'nameof\s*\([^()]*\)$')
_STRING_LITERAL = re.compile(r'"(?:[^"\\]|\\.)*"$')
_JUMP = re.compile(r'(?:return|continue|break|CRASH|throw|del)\b')
_IF_HEAD = re.compile(r'(else\s+)?if\s*\(')


def cs_ident(name):
    return '@' + name if name in CS_KEYWORDS else name


def is_proc_ref(expr):
    """True when the (macro-expanded) default is a proc reference: text by translation-rules.md 7.7."""
    if expr is None:
        return False
    e = _strip_parens(expr)
    return bool(_PROC_PATH.match(e) or _NAMEOF.match(e))


def default_kind(expr):
    """'none' (no default or null), 'num', 'text' (a proc reference too), 'path', 'resource' or 'expr'."""
    if expr is None:
        return 'none'
    e = expr.replace('\r', ' ').strip()
    while e.startswith('(') and e.endswith(')') and dmscan._match_close(e, 0) == len(e) - 1:
        e = e[1:-1].strip()
    if not e or e == 'null':
        return 'none'
    if _STRING_LITERAL.match(e) or e in BYOND_TEXT or is_proc_ref(e):
        return 'text'
    if _PATH_LITERAL.match(e):
        return 'path'
    if e.startswith("'") and e.endswith("'"):
        return 'resource'
    pos = 0
    has_num = False
    while pos < len(e):
        m = _NUM_TOKEN.match(e, pos)
        if not m:
            return 'expr'
        tok = m.group()
        if tok[0].isalpha() or tok[0] == '_':
            if tok not in BYOND_NUMERIC:
                return 'expr'
            has_num = True
        elif tok[0].isdigit() or tok[0] == '.':
            has_num = True
        pos = m.end()
    return 'num' if has_num else 'expr'


def _strip_parens(text):
    t = text.replace('\r', ' ').strip()
    while t.startswith('(') and dmscan._match_close(t, 0) == len(t) - 1:
        t = t[1:-1].strip()
    return t


def _split_condition(cond):
    """(terms, op): the top-level '||' or '&&' terms of a condition; op None for one term;
    (None, None) when both operators appear at the top level."""
    terms = []
    ops = set()
    depth = 0
    start = 0
    i = 0
    n = len(cond)
    while i < n:
        c = cond[i]
        if c in '([{':
            depth += 1
        elif c in ')]}':
            depth -= 1
        elif depth == 0 and cond.startswith('||', i) or depth == 0 and cond.startswith('&&', i):
            ops.add(cond[i:i + 2])
            terms.append(cond[start:i])
            i += 2
            start = i
            continue
        i += 1
    terms.append(cond[start:])
    if len(ops) > 1:
        return None, None
    return [t.strip() for t in terms], (ops.pop() if ops else None)


def _split_statement(text):
    """(statement, rest): the first statement of text ('{ ... }' or up to a top-level ';') and
    what follows it on the line."""
    t = text.lstrip(' \t\r')
    if not t:
        return '', ''
    if t.startswith('{'):
        j = dmscan._match_close(t, 0)
        if j < 0:
            return t, ''
        return t[1:j].strip(), t[j + 1:].lstrip(' \t\r;').strip()
    depth = 0
    for i, c in enumerate(t):
        if c in '([{':
            depth += 1
        elif c in ')]}':
            depth -= 1
        elif c == ';' and depth == 0:
            return t[:i].strip(), t[i + 1:].strip(' \t\r;')
    return t.strip(), ''


class ClassNames:
    """C# class names of DM type paths (translation-rules.md 2.3, collisions B.1)."""

    def __init__(self, paths):
        groups = collections.defaultdict(list)
        for p in paths:
            groups[p[1:].replace('/', '_')].append(p)
        self.names = {}
        self.collisions = []
        for name, ps in groups.items():
            if len(ps) == 1:
                self.names[ps[0]] = name
                continue
            ps = sorted(ps, key=lambda p: (p.count('/'), p))
            self.collisions.append(tuple(ps))
            self.names[ps[0]] = name + '__2'   # fewer segments: '_' inside a segment
            for p in ps[1:]:
                self.names[p] = name

    def of(self, path):
        if path in ('/list', '/alist'):
            return 'DmList'
        return self.names.get(path) or path[1:].replace('/', '_')


class SlotSig:
    __slots__ = ('slot', 'root_def', 'params', 'ptypes', 'notes', 'undecided', 'variadic',
                 'why_variadic', 'extra_overrides', 'dmparams_overrides', 'returns', 'cs_name',
                 'hides', 'builtin_root')


class Registry:
    """Computes the SlotSig of every slot with tg definitions."""

    def __init__(self, a, tree):
        self.a = a
        self.tree = tree
        self.classes = ClassNames(tree.order)
        self._vars_by_name = collections.defaultdict(set)
        for t, tv in a.p.type_vars.items():
            for name in tv:
                self._vars_by_name[name].add(t)
        for t, names in BUILTIN_TYPE_VARS.items():
            for name in names.split():
                self._vars_by_name[name].add(t)
        self.sigs = {}
        for key, s in a.slots.items():
            if s.tg_defs():
                self.sigs[key] = self._compute(s)

    # -- helpers ----------------------------------------------------------------------------
    def related(self, t1, t2):
        return self.tree.is_subtype(t1, t2) or self.tree.is_subtype(t2, t1)

    def renamed(self, root, name):
        """translation-rules.md 2.4: a var of the same name on the same type or one ancestor line."""
        if not root:
            return False
        return any(self.related(v, root) for v in self._vars_by_name.get(name, ()))

    def hides(self, root, name):
        """translation-rules.md 7.1: a builtin root slot whose C# class inherits a different slot
        of the same name (/client/New under tg's /client parent_type = /datum)."""
        t = self.tree.parent.get(root)
        while t:
            key = self.a.visible_slot(t, name)
            if key is not None and key != (root, name):
                return True
            t = self.tree.parent.get(t)
        return False

    @staticmethod
    def def_params(d):
        if d.builtin:
            spec = BUILTIN_PARAMS.get((d.type, d.name))
            return dmscan.parse_params(spec[0]) if spec else []
        return dmscan.parse_params(d.args)

    # -- one slot ---------------------------------------------------------------------------
    def _compute(self, s):
        a = self.a
        root_t, name = s.key
        sig = SlotSig()
        sig.slot = s
        tg = s.tg_defs()
        at_root = [d for d in tg if d.type == root_t]
        builtin = [d for d in s.defs if d.builtin and d.type == root_t]
        sig.builtin_root = bool(builtin) and not at_root
        root = at_root[0] if at_root else (builtin[0] if builtin else min(tg, key=lambda d: d.idx))
        sig.root_def = root
        rparams = self.def_params(root)
        fixed = [p for p in rparams if not p.variadic]
        sig.params = fixed
        sig.notes = []
        sig.undecided = []
        # variadic (7.2, 7.6)
        why = []
        if any(p.variadic for p in rparams):
            why.append('root ...')
        readers = [d for d in tg if d.reads_args]
        if readers:
            why.append('args')
        extra = 0
        dmparams = 0
        per_def = []
        for d in tg:
            ps = [p for p in self.def_params(d) if not p.variadic]
            per_def.append((d, ps))
            if d is root:
                continue
            if len(ps) > len(fixed):
                extra += 1
            if [p.name for p in ps] != [p.name for p in fixed]:  # fewer, other or more names (7.2)
                dmparams += 1
        if extra:
            why.append('extra params')
        sig.variadic = bool(why)
        sig.why_variadic = why
        sig.extra_overrides = extra
        sig.dmparams_overrides = dmparams
        # parameter types (3.3, 3.4, 3.5)
        sig.ptypes = []
        for i, p in enumerate(fixed):
            sig.ptypes.append(self._param_type(s, root, i, p, per_def, sig))
        # return type (7.4)
        if s.key == ('/datum', 'New'):
            sig.returns = 'void'
        else:
            value = any(d.returns_value for d in tg)
            if not value and builtin:
                spec = BUILTIN_PARAMS.get((root_t, name))
                value = bool(spec and spec[1])
            sig.returns = 'object' if value else 'void'
        sig.cs_name = cs_ident(name + '__proc' if self.renamed(root_t, name) else name)
        sig.hides = bool(root_t) and bool(builtin) and self.hides(root_t, name)
        return sig

    def _param_type(self, s, root, i, p, per_def, sig):
        kinds = set()
        for d, ps in per_def:
            if i < len(ps) and d is not root:
                k = default_kind(ps[i].default)
                if k != 'none':
                    kinds.add(k)
        rk = default_kind(p.default)
        if p.type is not None:
            ctype = self.classes.of(p.type)
            uses = set()
            for d, ps in per_def:
                if i < len(ps):
                    uses |= d.param_uses.get(ps[i].name, set())
            if 'text' in kinds or 'num' in kinds:
                sig.notes.append('%s: object (an override has a %s default)' % (
                    p.name, '/'.join(sorted(kinds & {'text', 'num'}))))
                return 'object'
            verdict, why = self.typed_param_verdict(s, i, p, per_def)
            if verdict == 'object':
                sig.notes.append('%s: object (3.4: %s)' % (p.name, why))
                return 'object'
            if verdict == 'undecided':
                sig.undecided.append('%s: 3.4 %s' % (p.name, why))
            path_default = rk == 'path' or 'path' in kinds
            if p.type != '/list' and (path_default or uses & {'new', 'typesof'}):
                how = sorted((uses & {'new', 'typesof'}) | ({'typepath default'} if path_default else set()))
                sig.notes.append('%s: Type (V3: %s)' % (p.name, ', '.join(how)))
                return 'Type'
            if p.type != '/list' and 'ispath' in uses and not uses & {'initial', 'scope'} and \
                    not any(u.startswith(p.name + ':') for u in sig.undecided):
                # ispath(P) is a type test (3.4, rules 3.3 P1); with no verdict it leaves the
                # declared type when no read follows on the typepath side, though the value
                # is a typepath there ('/area/shuttle/place_on_top_react', shuttles.dm:23).
                sig.undecided.append('%s: Type? (ispath(%s) with no 3.4 verdict: a typepath on one side)' % (
                    p.name, p.name))
            if p.type != '/list' and uses & {'initial', 'scope'}:
                sig.undecided.append('%s: Type? (%s reads an instance or a type)' % (
                    p.name, ' and '.join(x if x == 'initial' else '::' for x in sorted(uses & {'initial', 'scope'}))))
            return ctype
        # untyped
        base = {'num': 'float?', 'text': 'string', 'path': 'Type'}.get(rk, 'object')
        if base != 'object':
            # A default of a known other kind cannot be assigned in the override's prologue
            # (3.5 '-' cell); a non-constant default ('expr') has no kind the scanner knows.
            other = kinds - {rk, 'expr'}
            if other:
                sig.notes.append('%s: object (root default %s, an override default %s)' % (
                    p.name, rk, '/'.join(sorted(other))))
                return 'object'
        if rk == 'path':
            sig.notes.append('%s: Type (typepath default)' % p.name)
        elif rk == 'text' and is_proc_ref(p.default):
            sig.notes.append('%s: string (proc reference default, 7.7)' % p.name)
        return base

    # -- 3.4 ----------------------------------------------------------------------------------
    def typed_param_verdict(self, s, i, p, per_def):
        """('typed' | 'object' | 'undecided', reason) for the typed root parameter i."""
        undecided = None
        for d, ps in per_def:
            if i >= len(ps):
                continue
            n = ps[i].name
            uses = d.param_uses.get(n)
            if not uses or not any(u.startswith('test:') for u in uses):
                continue
            v, why = self._def_verdict(d, n, p.type, ps[i].type or p.type)
            where = '%s:%d' % (d.file, why[0]) if why else d.file
            if v == 'object':
                return 'object', '%s (%s)' % (why[1], where)
            if v == 'undecided' and undecided is None:
                undecided = '%s (%s)' % (why[1], where)
        if undecided:
            return 'undecided', undecided
        return 'typed', ''

    def _relation(self, proc, tested, declared):
        """How a type test relates to the declared (slot) type: 'fail' (the failing branch shows
        the value is not of the declared type), 'success' (the succeeding branch shows it),
        'none' (neither branch does) or 'unknown'."""
        if proc in KIND_TESTS:
            if declared == '/icon' and proc == 'isicon':
                return 'fail'
            return 'success'
        if tested is None:
            return 'unknown'
        if declared == '/list' or tested == '/list':
            return 'fail' if declared == tested else 'success'
        tree = self.tree
        if tested not in tree.ids or declared not in tree.ids:
            return 'unknown'
        if tree.is_subtype(declared, tested):
            return 'fail'
        if tree.is_subtype(tested, declared):
            return 'none'
        return 'success'

    def _def_verdict(self, d, n, declared, own_declared):
        """The verdict of one definition: its type tests of parameter n, in order."""
        lines = d.test_lines or []
        test_re = re.compile(r'\b(%s)\s*\(\s*%s\s*([,)])' % ('|'.join(dmscan.TYPE_TEST_PROCS), re.escape(n)))
        undecided = None
        for j, (indent, text, line) in enumerate(lines):
            for m in test_re.finditer(text):
                proc = m.group(1)
                rel = self._relation(proc, self._tested(m, text, own_declared), declared)
                if rel == 'none':
                    continue
                ln = line + text.count('\r', 0, m.start())
                if rel == 'unknown':
                    undecided = undecided or ('undecided', (ln, 'a type test against a computed type'))
                    continue

                def known(term):
                    return self._known_term(term, n, test_re, declared, own_declared)
                v = self._site(lines, j, m, n, rel, known)
                if v[0] == 'object':
                    return 'object', (ln, v[1])
                if v[0] in ('undecided', 'diagnostic'):
                    # A read that only feeds a CRASH/stack_trace/log message: 3.4 read literally
                    # makes the parameter object, but the declared type changes only the text of
                    # that message, so the declared type is kept and the case is listed.
                    undecided = undecided or ('undecided', (ln, v[1] + (
                        '; 3.4 read literally gives object' if v[0] == 'diagnostic' else '')))
        if undecided:
            return undecided
        return 'typed', None

    @staticmethod
    def _tested(m, text, own_declared):
        """The type that the type test matched by m (a test_re match in text) tests."""
        proc = m.group(1)
        if proc in ('istype', 'astype'):
            if m.group(2) == ')':
                return own_declared  # istype(x): the declared type of x in this definition
            tm = re.match(r'\s*(/[A-Za-z_][\w/]*)\s*\)', text[m.end():])
            return tm.group(1).rstrip('/') if tm else None
        return TYPE_TESTS.get(proc)

    def _known_term(self, term, n, test_re, declared, own_declared):
        """What one term of an if condition is known to be for a non-null n: (for a value of
        n's declared type, for a value of another kind), each True, False or None (unknown).
        A null test of n is known for both (3.4 cares about non-null values of another kind;
        null fits every declared type); 'n' alone is true for an object; another type test of
        n is known for a value of the declared type by its relation to that type (true for the
        declared type or an ancestor, false for a disjoint type or a non-object kind)."""
        t = _strip_parens(term)
        neg = False
        while t.startswith('!'):
            t = _strip_parens(t[1:])
            neg = not neg
        en = re.escape(n)
        val = None
        if re.fullmatch(r'isnull\s*\(\s*%s\s*\)|%s\s*==\s*null|null\s*==\s*%s' % (en, en, en), t):
            val = (False, False)
        elif re.fullmatch(r'%s\s*!=\s*null|null\s*!=\s*%s' % (en, en), t):
            val = (True, True)
        elif t == n:
            val = (True, None)  # an object is true; another kind may be 0 or ""
        else:
            m = test_re.match(t)
            if m and dmscan._match_close(t, t.index('(', m.start())) == len(t) - 1:
                rel = self._relation(m.group(1), self._tested(m, t, own_declared), declared)
                val = {'fail': (True, None), 'success': (False, None)}.get(rel)
        if val is None:
            return None, None
        return tuple(None if v is None else v != neg for v in val)

    def _site(self, lines, j, m, n, rel, known):
        """The verdict of one type test of n at lines[j] (match m). known(term): what another
        term of the condition is known to be (_known_term)."""
        indent, text, _ = lines[j]
        tclose = dmscan._match_close(text, text.index('(', m.start()))
        if tclose < 0:
            return 'undecided', 'unparsed test'
        test_text = text[m.start():tclose + 1]
        stripped = text.lstrip(' \t\r')
        off = len(text) - len(stripped)
        hm = _IF_HEAD.match(stripped)
        if not hm:
            if re.match(r'return\b', stripped) and not self._reads_region(
                    [stripped.replace(test_text, ' ')], n)[0]:
                return 'typed', ''
            return 'undecided', 'type test outside an if condition'
        close = dmscan._match_close(stripped, hm.end() - 1)
        if close < 0:
            return 'undecided', 'unparsed if'
        if not (hm.end() <= m.start() - off < close):
            return 'undecided', 'type test outside the if condition'
        cond = stripped[hm.end():close]
        terms, op = _split_condition(cond)
        if terms is None:
            return 'undecided', 'condition mixing && and ||'
        idx = None
        neg = False
        for k, term in enumerate(terms):
            t = _strip_parens(term)
            if t == test_text:
                idx, neg = k, False
            elif t.startswith('!') and _strip_parens(t[1:].strip()) == test_text:
                idx, neg = k, True
        if idx is None:
            return 'undecided', 'type test nested in the condition'
        term_true = (rel == 'success') != neg   # the term's value where n is not of its type
        values = [known(term) if k != idx else (not term_true, term_true)
                  for k, term in enumerate(terms)]

        def outcomes(kind):
            """The branches ('body', 'else') that a path can take, with short-circuit
            evaluation: kind 0 for a value of the declared type, 1 for one of another kind. An
            unknown term can go either way, so with nothing known about the other terms a
            branch is shared unless the test's own term decides it."""
            out = set()
            for v in values:
                v = v[kind]
                if op == '||':
                    if v is not False:
                        out.add('body')
                    if v is True:
                        return out
                else:  # '&&', or a single term
                    if v is not True:
                        out.add('else')
                    if v is False:
                        return out
            out.add('else' if op == '||' else 'body')
            return out
        other = outcomes(1)    # where n is not of its declared type
        same = outcomes(0)     # where it is
        # the later terms of the condition that the other-kind path evaluates
        later = terms[idx + 1:] if (op == '&&' and term_true) or (op == '||' and not term_true) else []
        reads, diag = self._reads_region(later, n)
        if reads:
            return ('diagnostic' if diag else 'object'), self._why(n, diag)
        inline, rest = _split_statement(stripped[close + 1:])
        body, k = self._block(lines, j, indent, inline)
        else_lines = []
        while k < len(lines):
            ei, et, _ = lines[k]
            es = et.lstrip(' \t\r')
            if ei != indent or not re.match(r'else\b', es):
                break
            inline, rest = _split_statement(es[4:])
            blk, k = self._block(lines, k, indent, inline)
            else_lines.extend(blk)
        region = {'body': body, 'else': else_lines}
        # Only a branch that the declared-type path never takes tells the kinds apart; a branch
        # both paths can take (the else of 'istype(x) && cond') is shared code.
        path = []
        for b in ('body', 'else'):
            if b in other and b not in same:
                path.extend(region[b])
        # The code after the if statement belongs to the other-kind path only when that path
        # reaches it and the declared-type path cannot (every branch it takes ends in a jump).
        if any(not self._ends_in_jump(region[b]) for b in other) and \
                all(self._ends_in_jump(region[b]) for b in same):
            path.extend(([rest] if rest else []) + [t for _, t, _ in lines[k:]])
        reads, diag = self._reads_region(path, n)
        if reads:
            return ('diagnostic' if diag else 'object'), self._why(n, diag)
        return 'typed', ''

    @staticmethod
    def _why(n, diag):
        return 'reads %s where the type test showed another kind%s' % (
            n, ' (only in a CRASH/stack_trace/log message)' if diag else '')

    @staticmethod
    def _block(lines, j, indent, inline):
        """The text of an if/else body: the inline statement after the header, then the
        deeper-indented lines; and the index of the first line after it."""
        out = [inline] if inline else []
        k = j + 1
        while k < len(lines) and lines[k][0] > indent:
            out.append(lines[k][1])
            k += 1
        return out, k

    @staticmethod
    def _ends_in_jump(region):
        if not region:
            return False
        last = region[-1].strip(' \t\r')
        return bool(_JUMP.match(last)) or bool(re.search(r';\s*(?:return|continue|break)\b[^;]*$', last))

    @staticmethod
    def _reads_region(region, n):
        """(reads, diagnostic): a read of n other than in a type test or a null test; an
        assignment that overwrites n without reading it ends the region. diagnostic: every read
        is inside a CRASH, stack_trace or log_* call."""
        word = re.compile(r'(?<![\w.:$/])%s\b' % re.escape(n))
        en = re.escape(n)
        allowed = re.compile(
            r'\b(?:%s|isnull)\s*\(\s*%s\s*[,)]|(?<![\w.])%s\s*[!=]=\s*null\b|\bnull\s*[!=]=\s*%s\b'
            r'|!\s*%s\b(?!\s*[.\[:(])|\bif\s*\(\s*!?\s*%s\s*\)'
            % ('|'.join(dmscan.TYPE_TEST_PROCS), en, en, en, en, en))
        assign = re.compile(r'^\s*%s\s*=(?!=)(.*)$' % en, re.S)
        diag_call = re.compile(r'\b(?:CRASH|stack_trace|log_\w+|message_admins|warning)\s*\(')
        found = False
        all_diag = True
        for text in region:
            am = assign.match(text.replace('\r', ' '))
            if am:
                if word.search(am.group(1)):
                    return True, False
                break
            masked = allowed.sub(lambda mm: ' ' * len(mm.group()), text)
            for wm in word.finditer(masked):
                found = True
                inside = False
                for cm in diag_call.finditer(masked, 0, wm.start()):
                    close = dmscan._match_close(masked, cm.end() - 1)
                    if close < 0 or close > wm.start():
                        inside = True
                if not inside:
                    all_diag = False
        return found, found and all_diag

    # -- output -------------------------------------------------------------------------------
    def signature(self, sig, dmtask=False):
        """The C# declaration of the slot's root. dmtask: the slot's callers must wait
        (colored-slots.tsv 'suspends = yes', waiting-model.md 4.2), so void becomes DmTask and
        object DmTask<object>."""
        s = sig.slot
        root_t = s.key[0]
        params = ['%s %s = null' % (t, cs_ident(p.name)) for p, t in zip(sig.params, sig.ptypes)]
        if sig.variadic:
            params.append('params object[] __extra')
        ret = sig.returns
        if dmtask:
            ret = 'DmTask' if ret == 'void' else 'DmTask<object>'
        if not root_t:
            head = 'public static'
        elif sig.hides:
            head = 'public new virtual'
        else:
            head = 'public virtual'
        return '%s %s %s(%s)' % (head, ret, sig.cs_name, ', '.join(params))

    def waitfor_text(self, sig):
        """The root's own 'set waitfor' and the overrides that set it themselves (Appendix A)."""
        s = sig.slot
        root = sig.root_def
        own = 'FALSE' if (not root.builtin and root.own_waitfor is False) else ''
        others = sum(1 for d in s.tg_defs() if d is not root and d.own_waitfor is False)
        if others:
            own = (own + ' ' if own else '') + '(%d override%s: FALSE)' % (others, '' if others == 1 else 's')
        return own


# ------------------------------------------------------------------------------------------------
# slots.tsv
# ------------------------------------------------------------------------------------------------

SLOTS_TSV_HEADER = ('root_path', 'name', 'cs_name', 'root_def', 'dm_params', 'variadic',
                    'returns', 'colored', 'suspends', 'signature', 'extra_param_overrides',
                    'dmparams_overrides', 'waitfor', 'waitfor_false', 'defs', 'notes', 'undecided')


class RawHeaders:
    """The DM parameter text of a definition as written in the tg source (for display)."""

    def __init__(self, tg):
        self.tg = tg
        self._files = {}

    def lines(self, rel):
        r = self._files.get(rel)
        if r is None:
            try:
                with open('%s/%s' % (self.tg, rel), encoding='utf-8', errors='replace') as f:
                    r = f.read().replace('\r\n', '\n').split('\n')
            except OSError:
                r = []
            self._files[rel] = r
        return r

    def params(self, d):
        if d.builtin:
            spec = BUILTIN_PARAMS.get((d.type, d.name))
            return spec[0] if spec else ''
        src = self.lines(d.file)
        k = d.line - 1
        if 0 <= k < len(src):
            m = re.search(r'(?<![\w.])%s\s*\(' % re.escape(d.name), src[k])
            if m:
                text = ''
                depth = 0
                start = m.end() - 1
                for j in range(k, min(k + 30, len(src))):
                    line = src[j][start:] if j == k else src[j]
                    line = re.sub(r'//.*$', '', line)
                    for c_i, c in enumerate(line):
                        if c == '(':
                            depth += 1
                        elif c == ')':
                            depth -= 1
                            if depth == 0:
                                text += line[:c_i + 1]
                                return ' '.join(text.strip()[1:-1].split())
                    text += line + ' '
        return ' '.join(d.args.replace('\r', ' ').split())


def tsv_rows(reg, raw, flags_of):
    """Rows of slots.tsv, sorted like colored-slots.tsv."""
    rows = []
    for key in sorted(reg.sigs, key=lambda k: (k[0] or '/proc', k[1])):
        sig = reg.sigs[key]
        s = sig.slot
        f = flags_of(s)
        rd = sig.root_def
        rows.append((
            key[0] or '/proc', key[1], sig.cs_name,
            'builtin' if rd.builtin else '%s:%d' % (rd.file, rd.line),
            raw.params(rd),
            ', '.join(sig.why_variadic) if sig.variadic else 'no',
            sig.returns, 'yes' if s.colored else 'no', 'yes' if s.suspends else 'no',
            reg.signature(sig, dmtask=s.suspends),
            str(sig.extra_overrides), str(sig.dmparams_overrides),
            reg.waitfor_text(sig), f['waitfor_false'], str(f['defs']),
            '; '.join(sig.notes), '; '.join(sig.undecided)))
    return rows


# ------------------------------------------------------------------------------------------------
# Appendix A cross-check
# ------------------------------------------------------------------------------------------------

def appendix_a(rules_text):
    """The rows of translation-rules.md Appendix A: (root, name, dm_root, signature, extras,
    waitfor, raw row)."""
    i = rules_text.find('## Appendix A.')
    j = rules_text.find('## Appendix B.', i)
    rows = []
    for line in rules_text[i:j].split('\n'):
        if not line.startswith('| `'):
            continue
        cells = [c.strip() for c in line.strip()[1:-1].split('|')]
        if len(cells) < 5:
            continue
        m = re.match(r'`([^`]*)` `([^`]*)`', cells[0])
        if not m:
            continue
        sigs = [x for x in re.findall(r'`([^`]*)`', cells[2]) if 'public ' in x]
        rows.append((m.group(1), m.group(2), cells[1], sigs[0] if sigs else '', cells[3], cells[4], line))
    return rows


def appendix_b2(rules_text):
    """The names of translation-rules.md Appendix B.2 (var/proc clashes)."""
    m = re.search(r'\*\*B\.2[^*]*\*\*\s*(.*?)\. No member', rules_text, re.S)
    return [x.strip() for x in m.group(1).split(',')] if m else []


def cross_check(reg, a, rules_text):
    """[(row label, field, Appendix A, generated)] for every difference, and the row count."""
    rows = appendix_a(rules_text)
    diffs = []
    for root, name, dm_root, sig_text, extras, waitfor, _ in rows:
        label = '`%s` `%s`' % (root, name)
        if root == '/proc':  # a global proc: its slot key has the empty root
            root = ''
        if re.search(r'\boverride\b', sig_text):
            key = a.visible_slot(root, name)
            d = next((x for x in a.defs if x.type == root and x.name == name and not x.builtin), None)
            ps = [p.name for p in dmscan.parse_params(d.args) if not p.variadic] if d else []
            want = re.search(r'DmParams\(([^)]*)\)', sig_text)
            want = [x.strip().strip('"') for x in want.group(1).split(',')] if want else []
            if key is None or key == (root, name):
                diffs.append((label, 'slot', 'override of another slot', 'a slot of its own'))
            elif ps != want:
                diffs.append((label, 'DmParams', ', '.join(want), ', '.join(ps)))
            continue
        sig = reg.sigs.get((root, name))
        if sig is None:
            diffs.append((label, 'slot', sig_text, 'no such slot'))
            continue
        mine = reg.signature(sig, dmtask=False)
        if ' '.join(mine.split()) != ' '.join(sig_text.split()):
            diffs.append((label, 'signature', sig_text, mine))
        if extras.strip().split(' ')[0].isdigit():
            n = int(extras.strip().split(' ')[0])
            if n != sig.extra_overrides:
                diffs.append((label, 'extra-param overrides', extras, str(sig.extra_overrides)))
        mw = reg.waitfor_text(sig)
        theirs_root = waitfor.strip().startswith('FALSE')
        mine_root = mw.startswith('FALSE')
        theirs_over = '(' in waitfor
        mine_over = '(' in mw
        if theirs_root != mine_root or theirs_over != mine_over:
            diffs.append((label, 'waitfor', waitfor, mw or '(none)'))
    return diffs, len(rows)


def md_escape(text):
    return text.replace('|', '\\|')


def summary_md(reg, rows, diffs, nrows, b2_names, renamed):
    sigs = reg.sigs.values()
    lines = []
    w = lines.append
    cnt = collections.Counter()
    for sig in sigs:
        cnt['slots'] += 1
        cnt['global' if not sig.slot.key[0] else 'typed'] += 1
        cnt['variadic'] += sig.variadic
        cnt['object' if sig.returns == 'object' else 'void'] += 1
        cnt['colored'] += sig.slot.colored
        cnt['suspends'] += sig.slot.suspends
        cnt['builtin_root'] += sig.builtin_root
        cnt['renamed'] += sig.cs_name.endswith('__proc')
        cnt['hides'] += sig.hides
        for t in sig.ptypes:
            cnt['p_' + t if t in ('object', 'float?', 'string', 'Type', 'DmList') else 'p_class'] += 1
        for n in sig.notes:
            if '3.4' in n:
                cnt['n_34'] += 1
            elif 'proc reference' in n:
                cnt['n_procref'] += 1
            elif 'Type' in n:
                cnt['n_type'] += 1
            else:
                cnt['n_default'] += 1
        for u in sig.undecided:
            if 'Type? (ispath' in u:
                cnt['u_ispath'] += 1
            elif 'Type?' in u:
                cnt['u_type'] += 1
            elif 'gives object' in u:
                cnt['u_diag'] += 1
            else:
                cnt['u_34'] += 1
        cnt['u_slots'] += bool(sig.undecided)
    w('| Quantity | Value |')
    w('|---|---|')
    w('| Slots with tg definitions (rows of `slots.tsv`): on types / global procs | %d: %d / %d |' % (cnt['slots'], cnt['typed'], cnt['global']))
    w('| Return type `object` / `void` (uncoloured) | %d / %d |' % (cnt['object'], cnt['void']))
    w('| Coloured / with callers that must wait (`suspends`: the signature is `DmTask` or `DmTask<object>`) | %d / %d |' % (cnt['colored'], cnt['suspends']))
    w('| Variadic (`params object[] __extra`: root `...`, a definition reads `args`, or an override adds parameters) | %d |' % cnt['variadic'])
    w('| Roots that are BYOND builtins without a tg definition on the root type (parameters from the BYOND reference) | %d |' % cnt['builtin_root'])
    w('| Parameters: class / `DmList` / `Type` / `float?` / `string` / `object` | %d / %d / %d / %d / %d / %d |' % (
        cnt['p_class'], cnt['p_DmList'], cnt['p_Type'], cnt['p_float?'], cnt['p_string'], cnt['p_object']))
    w('| Typed parameters retyped `object` by 3.4 (type-tested, read where the test showed another kind) | %d |' % cnt['n_34'])
    w('| Parameters made `Type` (typepath default, `new P`, `typesof(P)`) | %d |' % cnt['n_type'])
    w('| Parameters made `string` by a proc reference default (`/proc/x`, `GLOBAL_PROC_REF`, `PROC_REF`; 7.7) | %d |' % cnt['n_procref'])
    w('| Parameters retyped `object` by a default of another kind in an override (3.5) | %d |' % cnt['n_default'])
    w('| Undecided parameters (kept as declared; column `undecided`): 3.4 cases the scanner cannot follow / read only in a CRASH, stack_trace or log message / `initial(P.v)` or `P::v` / `ispath(P)` with no 3.4 verdict | %d / %d / %d / %d (in %d slots) |' % (
        cnt['u_34'], cnt['u_diag'], cnt['u_type'], cnt['u_ispath'], cnt['u_slots']))
    w('| Procs renamed `<name>__proc` (2.4: a var of the same name on one ancestor line) | %d slots, %d names |' % (cnt['renamed'], len(renamed)))
    w('| Builtin roots that hide an inherited slot (`public new virtual`, 7.1) | %d |' % cnt['hides'])
    w('| Class-name collisions (2.3, B.1) | %d: %s |' % (len(reg.classes.collisions), ', '.join(
        '`%s` / `%s`' % (p[0], p[1]) for p in reg.classes.collisions)))
    w('| Appendix A rows checked / differences | %d / %d |' % (nrows, len(diffs)))
    missing = sorted(set(b2_names) - set(renamed))
    extra = sorted(set(renamed) - set(b2_names))
    w('')
    w('Compared with translation-rules.md Appendix B.2 (%d names, a regex scan that includes `UNIT_TESTS` code): not renamed here (their var or proc exists only under `UNIT_TESTS`): %s; renamed here and missing from B.2: %s.' % (
        len(b2_names), ', '.join('`%s`' % x for x in missing) or 'none', ', '.join('`%s`' % x for x in extra) or 'none'))
    return '\n'.join(lines)


def diffs_md(diffs):
    if not diffs:
        return 'No differences.'
    lines = ['| Slot | Field | Appendix A | Generated (`slots.tsv`, uncoloured) |', '|---|---|---|---|']
    for label, field, theirs, mine in diffs:
        lines.append('| %s | %s | %s | %s |' % (label, field, md_escape(theirs), md_escape(mine)))
    return '\n'.join(lines)
