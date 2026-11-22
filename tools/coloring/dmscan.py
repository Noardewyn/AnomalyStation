"""DM front end of the colouring scanner (Python 3 standard library only).

It is not a DM compiler. It does exactly what the colouring needs, in four steps:

1. ``blank``: comments and string contents are blanked. Embedded string expressions
   (``"[foo()]"``) are kept, because they are code. A newline that sits inside a comment or a
   string is turned into ``\\r`` (a "hidden newline"), so that physical line numbers can still be
   recovered by counting ``\\r`` characters.
2. ``Preprocessor``: walks ``tgstation.dme`` in include order, evaluates ``#if/#ifdef/#ifndef``
   with the macro table as it evolves (``#define``/``#undef``), and feeds every active line to a
   per-file ``FileParser``.
3. ``FileParser``: joins physical lines into logical lines (open parens or brackets, trailing
   backslash), expands macros (function-like and object-like, ``#`` and ``##``), then splits the
   logical line into the DM block structure: type blocks (indentation or braces), ``var``
   declarations, proc definitions and proc bodies.
4. ``BodyAnalyzer``: for each proc body it records the events the colouring needs (calls with
   their receiver chain, ``..()``, ``new``, dynamic ``call()()``, ``set waitfor``,
   ``SIGNAL_HANDLER``, ``SHOULD_NOT_SLEEP``, local variable types, the handler named by a
   ``RegisterSignal`` call), skipping detached code (``spawn``/``ASYNC`` bodies;
   ``INVOKE_ASYNC`` expands into ``ASYNC`` blocks) and code that never runs in the port, where
   ``TICK_CHECK`` is always false (``if(TICK_CHECK)`` bodies, the rest of a block after
   ``if(!TICK_CHECK) continue``).

Event tuples in ``Def.events``:
``('call', line, name, receiver)`` with receiver ``None`` (unqualified), ``'?'`` (not a plain
identifier chain), ``('global',)`` (``call(/proc/x)``), ``('/t',)`` (``call(obj, /t::x())``: exactly
the definition of ``x`` visible at ``/t``) or a tuple of identifiers;
``('new', line, type path, assigned chain, expression chain, assigned)``: a literal path
(``new /t()``) or an implicit new (``var/t/x = new()``) gives the type path; an implicit new
into ``x.y = new()`` gives the assigned chain; ``new expr()`` gives the chain of ``expr`` (or
``None``) and ``assigned`` = (declared type of ``var/t/x =`` or ``None``, chain of ``x.y =`` or
``None``), or ``None`` when the new is not assigned;
``('super', line)``; ``('dyn', line, text)`` for ``call()()`` with a computed target;
``('handler', line, name, type path or ('global',) or None, receiver)`` for ``RegisterSignal``
(the type path is the one of a ``TYPE_PROC_REF``; ``None`` means the listener's own proc).

The analysis itself (roots, slots, propagation) lives in ``coloring.py``.
"""

import os
import re

# --------------------------------------------------------------------------------------------
# 1. Blanker
# --------------------------------------------------------------------------------------------

_JUMP = re.compile(r'//|/\*|@\{"|@"|\{"|"|\'')
_BLOCK_COMMENT = re.compile(r'/\*|\*/|\n')
_STR_NORMAL = re.compile(r'[\\"\[\n]')
_STR_MULTI = re.compile(r'\\|"\}|\[|\n')
_EMBEDDED = re.compile(r'[\[\]\n\']|@\{"|@"|\{"|"')


def _skip_block_comment(src, s, out):
    """DM block comments nest (BYOND reference, 'comment')."""
    depth = 0
    pos = s
    while True:
        m = _BLOCK_COMMENT.search(src, pos)
        if not m:
            return len(src)
        t = m.group()
        if t == '/*':
            depth += 1
        elif t == '*/':
            depth -= 1
            if depth == 0:
                out.append(' ')
                return m.end()
        else:
            out.append('\r')
        pos = m.end()


def _skip_resource(src, s, out):
    n = len(src)
    e = s + 1
    while e < n and src[e] != "'" and src[e] != '\n':
        e += 2 if src[e] == '\\' else 1
    out.append("''")
    return e + 1 if e < n and src[e] == "'" else e


def _blank_embedded(src, pos, out):
    """Copy an embedded expression ``[...]`` of a string; nested strings are blanked again."""
    depth = 1
    while True:
        m = _EMBEDDED.search(src, pos)
        if not m:
            out.append(src[pos:])
            return len(src)
        out.append(src[pos:m.start()])
        t = m.group()
        if t == '[':
            depth += 1
            out.append('[')
            pos = m.end()
        elif t == ']':
            depth -= 1
            out.append(']')
            pos = m.end()
            if depth == 0:
                return pos
        elif t == '\n':
            out.append('\r')
            pos = m.end()
        elif t == "'":
            pos = _skip_resource(src, m.start(), out)
        else:
            pos = _blank_string(src, m.start(), out)


def _blank_string(src, s, out):
    n = len(src)
    if src.startswith('@{"', s):  # raw multi-line string: no escapes, no embedded expressions
        e = src.find('"}', s + 3)
        e = n if e < 0 else e
        out.append('""' + '\r' * src.count('\n', s, e))
        return min(n, e + 2)
    if src.startswith('@"', s):  # raw string
        e = s + 2
        while e < n and src[e] != '"' and src[e] != '\n':
            e += 1
        out.append('""')
        return e + 1 if e < n and src[e] == '"' else e
    multi = src.startswith('{"', s)
    pos = s + (2 if multi else 1)
    pat = _STR_MULTI if multi else _STR_NORMAL
    out.append('"')
    while True:
        m = pat.search(src, pos)
        if not m:
            out.append('"')
            return n
        t = m.group()
        if t == '\\':
            i = m.start()
            if i + 1 < n and src[i + 1] == '\n':
                # Line continuation inside a string: DM skips the following whitespace, blank
                # lines included (tg fishing_equipment.dm:573-580 relies on it).
                pos = i + 1
                while pos < n and src[pos] in ' \t\n':
                    if src[pos] == '\n':
                        out.append('\r')
                    pos += 1
                continue
            pos = i + 2
        elif t == '[':
            out.append('[')
            pos = _blank_embedded(src, m.end(), out)
        elif t == '\n':
            if multi:
                out.append('\r')
                pos = m.end()
            else:  # unterminated single-line string: end it at the newline
                out.append('"')
                return m.start()
        else:  # closing quote
            out.append('"')
            return m.end()


def blank(src):
    """Blank comments and string contents. Newlines inside them become '\\r'."""
    src = src.replace('\r\n', '\n').replace('\r', '\n')
    out = []
    pos = 0
    n = len(src)
    search = _JUMP.search
    while True:
        m = search(src, pos)
        if not m:
            out.append(src[pos:])
            break
        s = m.start()
        out.append(src[pos:s])
        t = m.group()
        if t == '//':
            e = src.find('\n', s)
            pos = n if e < 0 else e
        elif t == '/*':
            pos = _skip_block_comment(src, s, out)
        elif t == "'":
            pos = _skip_resource(src, s, out)
        else:
            pos = _blank_string(src, s, out)
    return ''.join(out)


# --------------------------------------------------------------------------------------------
# 2. Macros and the preprocessor
# --------------------------------------------------------------------------------------------

_IDENT = re.compile(r'(?<![\w$])[A-Za-z_]\w*')
_WS = ' \t\r'


class Macro:
    __slots__ = ('name', 'params', 'variadic', 'body', 'file', 'line')

    def __init__(self, name, params, variadic, body, file, line):
        self.name = name
        self.params = params      # None for object-like macros
        self.variadic = variadic  # name of the variadic parameter, or None
        self.body = body
        self.file = file
        self.line = line


def _match_close(text, i):
    """Index of the bracket closing text[i] ('(' '[' or '{'), or -1."""
    depth = 0
    n = len(text)
    while i < n:
        c = text[i]
        if c in '([{':
            depth += 1
        elif c in ')]}':
            depth -= 1
            if depth == 0:
                return i
        i += 1
    return -1


def _split_args(text):
    """Split an argument list (without the outer parens) at top-level commas."""
    args = []
    depth = 0
    start = 0
    for i, c in enumerate(text):
        if c in '([{':
            depth += 1
        elif c in ')]}':
            depth -= 1
        elif c == ',' and depth == 0:
            args.append(text[start:i])
            start = i + 1
    args.append(text[start:])
    return args


_PASTE = re.compile(r'\s*##\s*')


class MacroExpander:
    """Expands DM macros the way the C-like DM preprocessor does (enough for tg's macros)."""

    MAX_DEPTH = 40

    def __init__(self, macros, opaque):
        self.macros = macros
        self.opaque = opaque  # names that are never expanded (lint markers, CHECK_TICK policy)

    def expand(self, text, active=frozenset(), depth=0):
        if depth > self.MAX_DEPTH:
            return text
        return self._expand_slow(text, active, depth)

    def _expand_slow(self, text, active, depth):
        macros = self.macros
        out = []
        last = 0
        pos = 0
        n = len(text)
        search = _IDENT.search
        while True:
            m = search(text, pos)
            if not m:
                break
            name = m.group()
            mac = macros.get(name)
            if mac is None or name in active or name in self.opaque:
                pos = m.end()
                continue
            start = m.start()
            if mac.params is None:
                out.append(text[last:start])
                out.append(self.expand(mac.body, active | {name}, depth + 1))
                last = pos = m.end()
                continue
            j = m.end()
            while j < n and text[j] in _WS:
                j += 1
            if j >= n or text[j] != '(':
                pos = m.end()
                continue
            close = _match_close(text, j)
            if close < 0:
                pos = m.end()
                continue
            raw_args = _split_args(text[j + 1:close])
            if len(raw_args) == 1 and not raw_args[0].strip() and not mac.params:
                raw_args = []
            body = self._substitute(mac, raw_args, active, depth)
            out.append(text[last:start])
            out.append(self.expand(body, active | {name}, depth + 1))
            hidden = text.count('\r', start, close + 1)
            if hidden:
                out.append('\r' * hidden)
            last = pos = close + 1
        out.append(text[last:])
        return ''.join(out)

    def _substitute(self, mac, raw_args, active, depth):
        params = mac.params
        values = {}
        nfixed = len(params) - (1 if mac.variadic else 0)
        for i in range(nfixed):
            values[params[i]] = raw_args[i].strip().replace('\r', ' ') if i < len(raw_args) else ''
        if mac.variadic:
            values[mac.variadic] = ','.join(raw_args[nfixed:]).strip().replace('\r', ' ')
        body = mac.body
        if not values:
            return body
        # Find parameter occurrences; decide stringize (#p), paste (p ## x) or plain (expanded).
        out = []
        last = 0
        expanded_cache = {}
        for m in _IDENT.finditer(body):
            name = m.group()
            if name not in values:
                continue
            s, e = m.start(), m.end()
            before = body[:s].rstrip(' \t')
            after = body[e:].lstrip(' \t')
            if before.endswith('#') and not before.endswith('##'):
                hash_pos = body.rfind('#', 0, s)
                out.append(body[last:hash_pos])
                out.append('""')
                last = e
                continue
            pasted = before.endswith('##') or after.startswith('##')
            if pasted:
                val = values[name]
            else:
                val = expanded_cache.get(name)
                if val is None:
                    val = self.expand(values[name], active, depth + 1)
                    expanded_cache[name] = val
            out.append(body[last:s])
            out.append(val)
            last = e
        out.append(body[last:])
        result = ''.join(out)
        if '##' in result:
            result = _PASTE.sub('', result)
        return result


_DIRECTIVE = re.compile(r'\s*#\s*(\w+)\s*(.*)$', re.S)
_DEFINE = re.compile(r'([A-Za-z_]\w*)(\(([^)]*)\))?\s*(.*)$', re.S)
_DEFINED = re.compile(r'\bdefined\s*\(\s*([A-Za-z_]\w*)\s*\)|\bdefined\s+([A-Za-z_]\w*)')


class Preprocessor:
    """Walks the .dme in include order and feeds active code lines to per-file parsers."""

    def __init__(self, tg_root, predefined, opaque, parser_factory):
        self.tg_root = tg_root
        self.macros = {}
        for name, body in predefined.items():
            self.macros[name] = Macro(name, None, None, body, '<predefined>', 0)
        self.expander = MacroExpander(self.macros, opaque)
        self.parser_factory = parser_factory
        self.files = []          # repo-relative paths in include order
        self.total_lines = 0
        self.errors = []         # active #error directives
        self.missing = []        # includes that could not be found
        self._seen = set()

    # -- #if evaluation ---------------------------------------------------------------------
    def eval_condition(self, expr):
        expr = expr.replace('\r', ' ')

        def repl(m):
            name = m.group(1) or m.group(2)
            return ' 1 ' if name in self.macros else ' 0 '
        expr = _DEFINED.sub(repl, expr)
        expr = self.expander.expand(expr)
        expr = re.sub(r'(?<![\w$])[A-Za-z_]\w*', ' 0 ', expr)  # unknown identifiers are 0
        expr = expr.replace('&&', ' and ').replace('||', ' or ')
        expr = re.sub(r'!(?!=)', ' not ', expr)
        try:
            return bool(eval(expr, {'__builtins__': {}}, {}))
        except Exception:
            self.errors.append('cannot evaluate #if ' + expr.strip())
            return False

    # -- files ------------------------------------------------------------------------------
    def run(self, dme_rel):
        self.process_file(os.path.join(self.tg_root, dme_rel))

    def _resolve_include(self, name, cur_dir):
        name = name.replace('\\', '/')
        for base in (cur_dir, self.tg_root):
            p = os.path.normpath(os.path.join(base, name))
            if os.path.exists(p):
                return p
        return None

    def process_file(self, path):
        rel = os.path.relpath(path, self.tg_root).replace(os.sep, '/')
        if rel in self._seen:
            return
        self._seen.add(rel)
        with open(path, encoding='utf-8', errors='replace') as f:
            raw = f.read()
        text = blank(raw)
        self._raw_lines = raw.replace('\r\n', '\n').replace('\r', '\n').split('\n')
        self.files.append(rel)
        parser = self.parser_factory(rel, self.expander)
        lines = text.split('\n')
        cur_dir = os.path.dirname(path)
        # Condition stack entries: [parent_active, taken, active]
        stack = []
        active = True
        phys = 1
        k = 0
        nlines = len(lines)
        while k < nlines:
            line = lines[k]
            start_phys = phys
            phys += 1 + line.count('\r')
            stripped = line.lstrip(' \t\r')
            if stripped.startswith('#'):
                full = line
                while full.rstrip(' \t\r').endswith('\\') and k + 1 < nlines:
                    full = full.rstrip(' \t\r')[:-1] + ' '
                    k += 1
                    nxt = lines[k]
                    phys += 1 + nxt.count('\r')
                    full += nxt
                raw_lines = self._raw_lines
                raw_line = raw_lines[start_phys - 1] if start_phys - 1 < len(raw_lines) else ''
                active = self._directive(full, start_phys, rel, cur_dir, stack, active, raw_line)
                self._raw_lines = raw_lines
                parser.directive_break()
                k += 1
                continue
            if active:
                parser.feed(line, start_phys)
            k += 1
        parser.finish()
        self.total_lines += phys - 1

    def _directive(self, full, line_no, rel, cur_dir, stack, active, raw_line=''):
        m = _DIRECTIVE.match(full)
        if not m:
            return active
        kw, rest = m.group(1), m.group(2)
        if kw in ('if', 'ifdef', 'ifndef'):
            if not active:
                stack.append([False, True, False])
                return False
            if kw == 'ifdef':
                val = rest.split()[0] in self.macros if rest.split() else False
            elif kw == 'ifndef':
                val = rest.split()[0] not in self.macros if rest.split() else True
            else:
                val = self.eval_condition(rest)
            stack.append([True, val, val])
            return val
        if kw == 'elif':
            if not stack:
                return active
            top = stack[-1]
            if not top[0] or top[1]:
                top[2] = False
                return False
            val = self.eval_condition(rest)
            top[1] = val
            top[2] = val
            return val
        if kw == 'else':
            if not stack:
                return active
            top = stack[-1]
            top[2] = top[0] and not top[1]
            top[1] = True
            return top[2]
        if kw == 'endif':
            if stack:
                stack.pop()
            return stack[-1][2] if stack else True
        if not active:
            return active
        if kw == 'define':
            dm = _DEFINE.match(rest)
            if dm:
                name = dm.group(1)
                body = dm.group(4).replace('\r', ' ').strip()
                if dm.group(2) is not None and rest[len(name):len(name) + 1] == '(':
                    params = [p.strip() for p in dm.group(3).split(',')] if dm.group(3).strip() else []
                    variadic = None
                    if params and params[-1].endswith('...'):
                        variadic = params[-1][:-3].strip() or '__VA_ARGS__'
                        params[-1] = variadic
                    self.macros[name] = Macro(name, params, variadic, body, rel, line_no)
                else:
                    if dm.group(2) is not None:  # "NAME (x)" with a space: object-like
                        body = (dm.group(2) + ' ' + dm.group(4)).replace('\r', ' ').strip()
                    self.macros[name] = Macro(name, None, None, body, rel, line_no)
        elif kw == 'undef':
            parts = rest.split()
            if parts:
                self.macros.pop(parts[0], None)
        elif kw == 'include':
            im = re.search(r'#\s*include\s*"(.*?)"', raw_line)
            if im:
                name = im.group(1)
                low = name.lower()
                if low.endswith('.dm') or low.endswith('.dme'):
                    p = self._resolve_include(name, cur_dir)
                    if p is None:
                        self.missing.append(name)
                    else:
                        self.process_file(p)
        elif kw == 'error':
            self.errors.append('%s:%d #error %s' % (rel, line_no, rest.strip()[:120]))
        return active


# --------------------------------------------------------------------------------------------
# 3. Block parser
# --------------------------------------------------------------------------------------------

def indent_of(line):
    k = 0
    for ch in line:
        if ch == '\t':
            k += 4
        elif ch == ' ':
            k += 1
        elif ch == '\r':
            continue
        else:
            break
    return k


def join_path(ctx, rel):
    rel = rel.strip()
    if rel.startswith('/'):
        return rel.rstrip('/')
    if not rel:
        return ctx
    return (ctx + '/' + rel).rstrip('/')


VAR_MODIFIERS = frozenset(('global', 'static', 'tmp', 'const', 'final', 'SpacemanDMM_private',
                           'SpacemanDMM_protected', 'SpacemanDMM_final'))


def parse_var_path(segs):
    """segs: path segments after 'var'. Returns (name, type path or None, is_global)."""
    is_global = False
    rest = []
    for s in segs:
        if s in VAR_MODIFIERS:
            if s in ('global', 'static'):
                is_global = True
            continue
        rest.append(s)
    if not rest:
        return None, None, is_global
    name = rest[-1].split('[')[0]
    tsegs = rest[:-1]
    if not tsegs:
        typ = None
    elif tsegs[0] == 'list':
        typ = '/list'
    else:
        typ = '/' + '/'.join(tsegs)
    return name, typ, is_global


def split_items(text):
    """Split a type-level logical line into items (header, brace_body, brace_open).

    ';' separates items; a '{...}' group closes the item it belongs to (DM allows
    ``/type/proc() { ... } /next/type`` once a macro has been expanded onto one line)."""
    items = []
    depth = 0
    start = 0
    i = 0
    n = len(text)
    while i < n:
        c = text[i]
        if c == '(' or c == '[':
            depth += 1
        elif c == ')' or c == ']':
            if depth:
                depth -= 1
        elif depth == 0 and c == ';':
            items.append((text[start:i], None, False))
            start = i + 1
        elif depth == 0 and c == '{':
            j = _match_close(text, i)
            if j < 0:
                items.append((text[start:i], text[i + 1:], True))
                return items
            items.append((text[start:i], text[i + 1:j], False))
            start = j + 1
            i = j
        i += 1
    items.append((text[start:], None, False))
    return [it for it in items if it[0].strip(' \t\r') or it[1] is not None]


_PROC_HDR = re.compile(r'(/?(?:[A-Za-z_]\w*/)*)(operator[^\s(]*|[A-Za-z_]\w*)\s*\(')
_PATH_ONLY = re.compile(r'/?[A-Za-z_]\w*(?:/[A-Za-z_]\w*)*/?$')
_PATH_ASSIGN = re.compile(r'(/?[A-Za-z_][\w/]*?)\s*(?:\+|-|\*|\||&)?=(?!=)')
_AS_RETURN = re.compile(r'as\s+[\w/|]+\s*')
_VAR_DECL = re.compile(r'(/?(?:[A-Za-z_]\w*/)*var/[A-Za-z_][\w/]*)')
_STRING_ONLY = re.compile(r'(""\s*)+$')


class Def:
    """One proc definition (or a synthetic builtin declaration)."""
    __slots__ = ('idx', 'file', 'line', 'type', 'name', 'is_decl', 'is_verb', 'builtin', 'args',
                 'locals', 'path_locals', 'own_waitfor', 'sigh', 'sns', 'events', 'seed_builtin',
                 'detached', 'dead', 'slot', 'parent', 'wf', 'sm', 'witness', 'layer', 'phase',
                 'reads_args', 'returns_value', 'param_uses', 'test_lines', 'typesof_procs')

    def __init__(self, file, line, typ, name, is_decl, is_verb, builtin=False, args=''):
        self.idx = -1
        self.file = file
        self.line = line
        self.type = typ            # '' for global procs
        self.name = name
        self.is_decl = is_decl     # has the proc/ or verb/ keyword (or is a builtin declaration)
        self.is_verb = is_verb
        self.builtin = builtin
        self.args = args
        # local variable name -> [(line, declared type path or None)], one entry per declaration
        # in textual order (a name can be declared again, with another type, in a later block)
        self.locals = {}
        self.path_locals = {}      # local variable name -> base type of the type paths it holds
        self.own_waitfor = None    # None, True or False (set waitfor = ...)
        self.sigh = False          # SIGNAL_HANDLER
        self.sns = False           # SHOULD_NOT_SLEEP(TRUE)
        self.events = []           # (kind, line, data...)
        self.seed_builtin = None   # builtin seed name for synthetic builtin defs
        self.detached = 0          # number of detached (spawn/ASYNC) regions
        self.dead = 0              # number of TICK_CHECK-guarded regions (never run in the port)
        self.slot = None
        self.parent = None
        self.wf = True
        self.sm = False
        self.witness = None
        self.layer = None
        self.phase = None
        # Signature facts for the slot registry (slots.py), collected from the whole body,
        # detached and TICK_CHECK-dead code included unless noted:
        self.reads_args = False    # reads the DM 'args' var (translation-rules.md 7.6)
        self.returns_value = False  # 'return <expr>' or an assignment to '.' of its own (7.4;
        #                             not in detached code)
        self.param_uses = {}       # parameter name -> set of uses: 'test:<proc>' (type test),
        #                            'new', 'ispath', 'typesof', 'initial', 'scope' ('::')
        self.test_lines = None     # [(indent, text, line)] from the first type test of a
        #                            parameter on (for the 3.4 analysis), else None
        self.typesof_procs = []    # (type path, 'proc' or 'verb') of typesof(/t/proc) calls

    def display(self):
        if self.type == '':
            return '/proc/' + self.name
        return self.type + '/' + self.name


class TypeMentions:
    """The type paths of the parse, in order of first mention (a set for ``in`` and iteration).

    Adding a path adds its missing prefixes first (``/a``, ``/a/b``, then ``/a/b/c``), which is
    the order in which OpenDream's code tree creates its nodes (``DMCodeTree.GetDMObjectNode``),
    so the insertion order is the order of first declaration in compile order that the type
    order of ``docs/ai/specs/object-model.md`` section 9 is built from (``typeorder.py``)."""
    __slots__ = ('_paths',)

    def __init__(self):
        self._paths = {}

    def add(self, path):
        paths = self._paths
        if path in paths:
            return
        k = path.find('/', 1)
        while k > 0:
            prefix = path[:k]
            if prefix not in paths:
                paths[prefix] = None
            k = path.find('/', k + 1)
        paths[path] = None

    def __contains__(self, path):
        return path in self._paths

    def __iter__(self):
        return iter(self._paths)

    def __len__(self):
        return len(self._paths)


class ParseResult:
    def __init__(self):
        self.defs = []
        self.type_vars = {}        # type path -> {var name: declared type or None}
        self.parent_type = {}      # type path -> explicit parent_type (the last assignment)
        # every 'parent_type = /p' assignment in parse order: (type path, parent, file, line)
        self.parent_type_decls = []
        self.types = TypeMentions()
        self.global_vars = {}      # global var name -> declared type or None
        self.unparsed = []         # (file, line, text) top-level items that were not understood
        self.unclosed_braces = 0


# spawn(...) bodies are detached; if(TICK_CHECK) bodies are dead in the port (the virtual tick
# usage is 0, translation-rules.md 11.3 and DEVIATIONS.md DV3). Both are skipped the same way.
_SPAWN = re.compile(r'(?<![\w.])(spawn|if)\s*\(')
_DEAD_GUARDS = frozenset(('TICK_CHECK', 'TICK_CHECK_HIGH_PRIORITY'))
# 'if(!TICK_CHECK) continue' (an unrolled CHECK_TICK, SSlighting fire): the jump is always taken
# in the port, so the rest of the enclosing block is dead.
_NEG_DEAD_GUARDS = frozenset(('!TICK_CHECK', '!TICK_CHECK_HIGH_PRIORITY'))
_JUMP_STMT = re.compile(r'(?:continue|break|return)\b')
_CALL = re.compile(r'(\.\.|[A-Za-z_]\w*)\s*\(')
_NEW = re.compile(r'(?<![\w.])new\b')
_LOCAL_VAR = re.compile(r'(?<![\w.])var/([A-Za-z_][\w/]*)')
_WAITFOR = re.compile(r'(?<![\w.])set\s+waitfor\s*=\s*(\w+)')
_SIGH = re.compile(r'(?<![\w.])SIGNAL_HANDLER\b')
_SNS = re.compile(r'(?<![\w.])SHOULD_NOT_SLEEP\s*\(\s*(\w+)\s*\)')
_STATIC_NAME = re.compile(r'(?:\.proc/|\.verb/|^\s*/proc/|[\s(]/proc/|::)([A-Za-z_]\w*)')
# '/t::name()' is a reference to the proc of type /t (BYOND reference, '::' operator); call()
# with it runs exactly that definition on the given object, not the object's override.
_TYPE_PROC_REF = re.compile(r'(/[A-Za-z_][\w/]*)\s*::\s*([A-Za-z_]\w*)\s*\(\s*\)')
_NEW_PATH = re.compile(r'\s*(/[A-Za-z_][\w/]*)')
_NEW_IMPLICIT_TARGET = re.compile(r'(?:var/([A-Za-z_][\w/]*)|([A-Za-z_][\w.]*))\s*=\s*$')
# 'var/x in subtypesof(/t)', 'var/list/x = typesof(/t)': x holds type paths under /t.
# (subtypesof(t) is a tg macro that expands to '( typesof(t) - t )'.)
_PATHS_DECL = re.compile(r'var/(?:\w+/)*?([A-Za-z_]\w*)\s*(?:in|=)\s*\(?\s*(?:valid_subtypesof|typesof)'
                         r'\s*\(\s*(/[A-Za-z_][\w/]*)')
# 'var/x in y': x takes the paths held by the path list y.
_FOR_IN_VAR = re.compile(r'var/([A-Za-z_]\w*)\s+in\s+([A-Za-z_]\w*)\s*\)')
# 'new expr(...)' with a computed type: the parenthesis is the constructor call, not a call of expr.
_NEW_CALL_PAREN = re.compile(r'(?<![\w.])new\s+[A-Za-z_][\w.?]*\s*\(')
_NEW_EXPR = re.compile(r'\s*([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*(?:\(|$|[;,)\]])')

# Words followed by '(' that are statements, DM keywords or lint markers, never proc calls.
NOT_CALLS = frozenset('''if for while switch return new list alist spawn do else in to as del throw
    catch try nameof arglist SHOULD_NOT_SLEEP SIGNAL_HANDLER'''.split())

# Procs that register a signal handler; the handler is the proc reference among the arguments.
SIGNAL_REGISTRARS = frozenset(('RegisterSignal', 'RegisterSignals'))
# A proc reference after macro expansion: PROC_REF(x) -> nameof(.proc/x),
# TYPE_PROC_REF(/t, x) -> nameof(/t.proc/x), GLOBAL_PROC_REF(x) -> /proc/x.
_PROC_REF = re.compile(r'(/[A-Za-z_][\w/]*)?\.(?:proc|verb)/([A-Za-z_]\w*)|(?<![\w.])/proc/([A-Za-z_]\w*)')


def is_ternary_colon(text, k):
    """True when the ':' at text[k] closes a 'cond ? a : b' expression rather than being DM's
    runtime member access 'a:b': an unmatched ternary '?' (not '?.', '?:' or '?[') precedes it in
    the same bracket level, before any ',' or ';'."""
    depth = 0
    pending = 0  # ternary ':' between k and the '?' being looked for
    i = k - 1
    n = len(text)
    while i >= 0:
        c = text[i]
        if c in ')]}':
            depth += 1
        elif c in '([{':
            if depth == 0:
                return False
            depth -= 1
        elif depth == 0:
            if c == '?' and (i + 1 >= n or text[i + 1] not in '.:['):
                if pending == 0:
                    return True
                pending -= 1
            elif c == ':' and (i == 0 or text[i - 1] not in ':?') and text[i + 1:i + 2] != ':':
                pending += 1
            elif c == ',' or c == ';':
                return False
        i -= 1
    return False


def receiver_chain(text, op_start):
    """Parse the receiver of a qualified call backwards from the operator at op_start.

    Returns a tuple of identifier segments (('src',), ('user', 'client'), ...) or None when the
    receiver is not a plain identifier chain (a call result, an index, a literal)."""
    segs = []
    i = op_start - 1
    while True:
        while i >= 0 and text[i] in ' \t\r':
            i -= 1
        if i < 0:
            return None
        c = text[i]
        if not (c.isalnum() or c == '_'):
            return None
        j = i
        while j >= 0 and (text[j].isalnum() or text[j] == '_'):
            j -= 1
        seg = text[j + 1:i + 1]
        if seg[0].isdigit():
            return None
        segs.append(seg)
        k = j
        while k >= 0 and text[k] in ' \t\r':
            k -= 1
        if k >= 0 and text[k] == '.' and (k == 0 or text[k - 1] != '.'):
            i = k - 1
            if i >= 0 and text[i] == '?':
                i -= 1
            continue
        if k >= 1 and text[k] == ':' and text[k - 1] != ':' and not is_ternary_colon(text, k):
            i = k - 1
            continue
        # A bare chain must not itself be a path segment or a member of something we skipped.
        if k >= 0 and text[k] in '/':
            return None
        break
    segs.reverse()
    return tuple(segs)


class Param:
    """One declared parameter of a proc header (after macro expansion)."""
    __slots__ = ('name', 'type', 'default', 'variadic', 'text')

    def __init__(self, name, typ, default, variadic, text):
        self.name = name          # None for '...'
        self.type = typ           # declared type path ('/list' for list/x and x[]) or None
        self.default = default    # default expression text (expanded, strings blanked) or None
        self.variadic = variadic  # True for '...'
        self.text = text


_AS_CLAUSE = re.compile(r'\s+as\s+[\w|/]+\s*$')
_IN_CLAUSE = re.compile(r'\s+in\s+.*$')


def parse_params(args):
    """The parameters of a proc header's argument text, in order."""
    out = []
    for a in _split_args(args.replace('\r', ' ')):
        a = a.strip()
        if not a:
            continue
        if a == '...':
            out.append(Param(None, None, None, True, a))
            continue
        left, eq, right = a.partition('=')
        default = None
        if eq:
            default = _AS_CLAUSE.sub('', right.strip()).strip()
        left = _IN_CLAUSE.sub('', left.strip())
        left = left.split(' as ')[0].strip()
        segs = [x for x in left.split('/') if x]
        if segs and segs[0] == 'var':
            segs = segs[1:]
        if not segs:
            continue
        name, typ, _ = parse_var_path(segs)
        if not name:
            continue
        if '[' in segs[-1]:
            typ = '/list'
        out.append(Param(name.strip(), typ, default, False, a))
    return out


# Signature facts (slots.py): returns, assignments to '.', reads of 'args', typesof(/t/proc).
_RETURN = re.compile(r'(?<![\w.$/])return\b')
_DOT_ASSIGN = re.compile(r'(?<![\w.\])\]"\'?$:])\.\s*(?:\[[^\]\n]*\]\s*)?'
                         r'((?:[-+*/%|&^]|<<|>>|\|\||&&)?=(?!=)|\+\+|--)')
_ARGS = re.compile(r'(?<![\w.:$/])args\b(?!\s*=(?!=))')
_TYPESOF_PROCS = re.compile(r'\btypesof\s*\(\s*(/[A-Za-z_][\w/]*?)/(proc|verb)\s*\)')
TYPE_TEST_PROCS = ('istype', 'islist', 'isnum', 'istext', 'ispath', 'astype', 'ismob', 'isturf',
                   'isarea', 'ismovable', 'isloc', 'isicon', 'isfile')


def statement_rest(text, start):
    """The rest of the statement that starts at text[start]: up to a top-level ';' or an
    unmatched closing bracket."""
    depth = 0
    i = start
    n = len(text)
    while i < n:
        c = text[i]
        if c in '([{':
            depth += 1
        elif c in ')]}':
            if depth == 0:
                break
            depth -= 1
        elif c == ';' and depth == 0:
            break
        i += 1
    return text[start:i].replace('\r', ' ').strip()


def _is_super_call(expr):
    """True when expr is exactly a '..(...)' call."""
    if not expr.startswith('..'):
        return False
    rest = expr[2:].lstrip()
    if not rest.startswith('('):
        return False
    j = _match_close(rest, 0)
    return j == len(rest) - 1


def return_kind(expr):
    """'bare', 'super' (return ..()), 'null', 'dot' (return .) or 'value'."""
    if not expr:
        return 'bare'
    if _is_super_call(expr):
        return 'super'
    if expr == 'null':
        return 'null'
    if expr == '.':
        return 'dot'
    return 'value'


class BodyAnalyzer:
    """Collects the colouring events of one proc body, line by line."""

    def __init__(self, d, base_indent, counter):
        self.d = d
        self.base = base_indent
        self.det_indent = None     # lines indented deeper than this are detached
        self.det_brace = 0         # > 0 while inside a detached multi-line brace block
        self.spawn_pending = None  # indent of a line that ended with a bare spawn(...)
        self.dead_from = None      # lines indented this much or more are dead (after
        #                            'if(!TICK_CHECK) continue', TICK_CHECK is false in the port)
        self.neg_pending = None    # indent of a line that ended with a bare 'if(!TICK_CHECK)'
        self.lines = 0
        self.counter = counter
        names = [x.name for x in parse_params(d.args) if not x.variadic] if d.args else []
        self.args_shadowed = 'args' in names
        if names:
            alt = '|'.join(re.escape(x) for x in sorted(set(names)))
            self.p_test = re.compile(r'\b(%s)\s*\(\s*(%s)\s*[,)]' % ('|'.join(TYPE_TEST_PROCS), alt))
            # A use of the parameter itself as a type: 'new P', 'ispath(P' and 'typesof(P' with P
            # the whole argument ('ispath(P.result_type, /turf)' tests a var of P, not P);
            # 'initial(P.v)' and 'P::v' read a var of P, which may be a type or an instance.
            self.p_use = re.compile(
                r'\bnew\s+(?P<new>%(a)s)\b(?!\s*[.:])'
                r'|\b(?P<tproc>ispath|typesof)\s*\(\s*(?P<tp>%(a)s)\s*(?=[,)])'
                r'|\binitial\s*\(\s*(?P<init>%(a)s)\b'
                r'|(?<![\w.:])(?P<scope>%(a)s)\s*::' % {'a': alt})
        else:
            self.p_test = self.p_use = None

    def feed(self, text, indent, line):
        self.lines += 1
        d = self.d
        if self.dead_from is not None:
            if indent >= self.dead_from:
                self._collect_locals(text, line)
                self._shape(text, text, indent, line)
                return
            self.dead_from = None
        if self.neg_pending is not None:
            np = self.neg_pending
            self.neg_pending = None
            if indent > np and _JUMP_STMT.match(text.lstrip(' \t\r')):
                self.dead_from = np  # this jump line is live, the lines after it are dead
                d.dead += 1
        if self.det_brace > 0:
            self.det_brace += text.count('{') - text.count('}')
            if self.det_brace < 0:
                self.det_brace = 0
            self._collect_locals(text, line)
            self._shape(text, None, indent, line)
            return
        if self.spawn_pending is not None:
            sp = self.spawn_pending
            self.spawn_pending = None
            if indent <= sp and text.lstrip(' \t\r').startswith('{'):
                self.det_brace = text.count('{') - text.count('}')
                self.det_indent = None
                self._collect_locals(text, line)
                self._shape(text, None, indent, line)
                return
        if self.det_indent is not None:
            if indent > self.det_indent:
                self._collect_locals(text, line)
                self._shape(text, None, indent, line)
                return
            self.det_indent = None
        masked = self._mask_spawns(text, indent)
        self._collect_locals(text, line)
        self._shape(text, masked, indent, line)
        self._extract(masked, line)

    def _shape(self, text, live, indent, line):
        """Signature facts of the body (slots.py). live: the line with detached code blanked,
        or None when the whole line is detached (a return there leaves the spawned block)."""
        d = self.d
        if live is not None:
            if 'return' in live and not d.returns_value:
                for m in _RETURN.finditer(live):
                    if return_kind(statement_rest(live, m.end())) == 'value':
                        d.returns_value = True
                        break
            if '.' in live and not d.returns_value:
                for m in _DOT_ASSIGN.finditer(live):
                    if m.group(1) == '=' and _is_super_call(statement_rest(live, m.end())):
                        continue  # '. = ..()'
                    d.returns_value = True
                    break
        if 'args' in text and not d.reads_args and not self.args_shadowed and _ARGS.search(text):
            if 'args' not in d.locals:
                d.reads_args = True
        if 'typesof' in text and ('/proc' in text or '/verb' in text):
            for m in _TYPESOF_PROCS.finditer(text):
                d.typesof_procs.append((m.group(1), m.group(2)))
        if self.p_test is not None:
            tested = False
            if 'is' in text or 'astype' in text:
                for m in self.p_test.finditer(text):
                    d.param_uses.setdefault(m.group(2), set()).add('test:' + m.group(1))
                    tested = True
            if 'new' in text or 'ispath' in text or 'typesof' in text or 'initial' in text \
                    or '::' in text:
                for m in self.p_use.finditer(text):
                    if m.group('new'):
                        d.param_uses.setdefault(m.group('new'), set()).add('new')
                    elif m.group('tp'):
                        d.param_uses.setdefault(m.group('tp'), set()).add(m.group('tproc'))
                    elif m.group('init'):
                        d.param_uses.setdefault(m.group('init'), set()).add('initial')
                    elif m.group('scope'):
                        d.param_uses.setdefault(m.group('scope'), set()).add('scope')
            if tested and d.test_lines is None:
                d.test_lines = []
            if d.test_lines is not None:
                d.test_lines.append((indent, text, line))

    def _mask_spawns(self, text, indent):
        if 'spawn' not in text and 'TICK_CHECK' not in text:
            return text
        pos = 0
        out = text
        while True:
            m = _SPAWN.search(out, pos)
            if not m:
                return out
            s = m.start()
            close = _match_close(out, m.end() - 1)
            if m.group(1) == 'if':
                guard = out[m.end():close].replace(' ', '').replace('\t', '').replace('\r', '') if close >= 0 else ''
                if guard in _NEG_DEAD_GUARDS:
                    rest = out[close + 1:].lstrip(' \t\r')
                    if not rest:
                        self.neg_pending = indent
                    elif _JUMP_STMT.match(rest):
                        self.dead_from = indent
                        self.d.dead += 1
                    pos = m.end()
                    continue
                if guard not in _DEAD_GUARDS:
                    pos = m.end()
                    continue
                self.d.dead += 1
            else:
                self.d.detached += 1
            if close < 0:
                return out[:s] + _blank_keep_cr(out[s:])
            j = close + 1
            n = len(out)
            while j < n and out[j] in ' \t\r':
                j += 1
            if j >= n:  # bare spawn: the following deeper block is detached
                self.det_indent = indent
                self.spawn_pending = indent
                return out[:s] + _blank_keep_cr(out[s:])
            if out[j] == '{':
                e = _match_close(out, j)
                if e < 0:
                    self.det_brace = 1 + out[j + 1:].count('{') - out[j + 1:].count('}')
                    return out[:s] + _blank_keep_cr(out[s:])
                out = out[:s] + _blank_keep_cr(out[s:e + 1]) + out[e + 1:]
                pos = e + 1
                continue
            # single statement: up to ';' or an unmatched '}' at depth 0, or the end of the line
            depth = 0
            e = j
            while e < n:
                c = out[e]
                if c in '([{':
                    depth += 1
                elif c in ')]}':
                    if depth == 0:
                        break
                    depth -= 1
                elif c == ';' and depth == 0:
                    break
                e += 1
            out = out[:s] + _blank_keep_cr(out[s:e]) + out[e:]
            pos = e

    def _collect_locals(self, text, line):
        if 'var/' not in text:
            return
        if 'typesof' in text or ' in ' in text:
            pl = self.d.path_locals
            for m in _PATHS_DECL.finditer(text):
                pl.setdefault(m.group(1), m.group(2).rstrip('/'))
            for m in _FOR_IN_VAR.finditer(text):
                base = pl.get(m.group(2))
                if base and m.group(2) != m.group(1):
                    pl.setdefault(m.group(1), base)
        for m in _LOCAL_VAR.finditer(text):
            segs = m.group(1).split('/')
            name, typ, _ = parse_var_path(segs)
            if name:
                self.d.locals.setdefault(name, []).append((line + text.count('\r', 0, m.start()), typ))

    def _extract(self, text, line):
        d = self.d
        ev = d.events
        if 'set' in text:
            wm = _WAITFOR.search(text)
            if wm:
                v = wm.group(1)
                d.own_waitfor = not (v in ('0', 'FALSE'))
        if 'SIGNAL_HANDLER' in text and _SIGH.search(text):
            d.sigh = True
        if 'SHOULD_NOT_SLEEP' in text:
            sm = _SNS.search(text)
            if sm and sm.group(1) in ('1', 'TRUE'):
                d.sns = True
        if '(' not in text and 'new' not in text:
            return
        new_parens = {nm.end() - 1 for nm in _NEW_CALL_PAREN.finditer(text)} if 'new' in text else ()
        for m in _CALL.finditer(text):
            name = m.group(1)
            s = m.start()
            if new_parens and m.end() - 1 in new_parens:
                continue  # new expr(...): expr is a type path value, not a proc
            if name == '..':
                if s > 0 and text[s - 1] == '.':
                    continue
                ev.append(('super', line + text.count('\r', 0, s)))
                continue
            if name in NOT_CALLS:
                continue
            if s > 0:
                p = text[s - 1]
                if p == '/' or p == '$':
                    continue
                if p == ':' and s > 1 and text[s - 2] == ':':
                    continue
                if name[0].isdigit():
                    continue
            k = s - 1
            while k >= 0 and text[k] in ' \t\r':
                k -= 1
            ln = line + text.count('\r', 0, s)
            recv = None
            qualified = False
            if k >= 0 and text[k] == '.' and not (k > 0 and text[k - 1] == '.'):
                op = k - 1 if k > 0 and text[k - 1] == '?' else k
                recv = receiver_chain(text, op) or '?'
                qualified = True
            elif k >= 0 and text[k] == ':' and not (k > 0 and text[k - 1] == ':') \
                    and not is_ternary_colon(text, k):
                recv = receiver_chain(text, k) or '?'
                qualified = True
            if name in SIGNAL_REGISTRARS:
                close = _match_close(text, m.end() - 1)
                if close >= 0:
                    rm = _PROC_REF.search(text, m.end(), close)
                    if rm:
                        if rm.group(3):
                            ev.append(('handler', ln, rm.group(3), ('global',), recv))
                        else:
                            ev.append(('handler', ln, rm.group(2), rm.group(1), recv))
            if qualified:
                ev.append(('call', ln, name, recv))
                continue
            if name == 'call':
                close = _match_close(text, m.end() - 1)
                if close < 0:
                    continue
                j = close + 1
                while j < len(text) and text[j] in ' \t\r':
                    j += 1
                if j >= len(text) or text[j] != '(':
                    continue  # not an invocation
                args = _split_args(text[m.end():close])
                ref = args[-1]
                tm = _TYPE_PROC_REF.search(ref)
                if tm:
                    ev.append(('call', ln, tm.group(2), (tm.group(1).rstrip('/'),)))
                    continue
                sm = _STATIC_NAME.search(ref)
                if sm:
                    target = sm.group(1)
                    if len(args) >= 2:
                        recv_text = args[0].strip()
                        if re.fullmatch(r'[A-Za-z_][\w.?]*', recv_text):
                            recv = tuple(x for x in recv_text.replace('?.', '.').split('.') if x)
                        else:
                            recv = '?'
                        ev.append(('call', ln, target, recv))
                    else:
                        ev.append(('call', ln, target, ('global',)))
                else:
                    ev.append(('dyn', ln, text[m.start():close + 1].replace('\r', ' ').strip()[:120]))
                continue
            ev.append(('call', ln, name, None))
        if 'new' in text:
            for m in _NEW.finditer(text):
                s = m.start()
                ln = line + text.count('\r', 0, s)
                after = text[m.end():]
                pm = _NEW_PATH.match(after)
                if pm:  # new /type/path(...)
                    ev.append(('new', ln, pm.group(1).rstrip('/'), None, None, None))
                    continue
                assigned = None
                tm = _NEW_IMPLICIT_TARGET.search(text, 0, s)
                if tm:
                    if tm.group(1):
                        _, atyp, _ = parse_var_path(tm.group(1).split('/'))
                        assigned = (atyp, None)
                    else:
                        assigned = (None, tuple(tm.group(2).split('.')))
                rest = after.lstrip(' \t\r')
                if rest[:1] == '(' or rest == '' or rest[:1] in ';)],':
                    # Implicit new: the declared type of the assigned variable.
                    if assigned and assigned[0]:
                        ev.append(('new', ln, assigned[0], None, None, None))
                    elif assigned:
                        ev.append(('new', ln, None, assigned[1], None, None))
                    else:
                        ev.append(('new', ln, None, None, None, None))
                    continue
                em = _NEW_EXPR.match(after)
                expr = tuple(em.group(1).split('.')) if em else None
                ev.append(('new', ln, None, None, expr, assigned))


def _blank_keep_cr(s):
    return ''.join('\r' if c == '\r' else ' ' for c in s)


class FileParser:
    """Turns the active lines of one file into type, var and proc records."""

    def __init__(self, rel, expander, result):
        self.rel = rel
        self.expander = expander
        self.r = result
        self.pending = None
        self.pending_line = 0
        self.depth = 0
        # Frames: [indent, kind, path_or_def, extra]
        #   kind 'type': path; 'proc': (Def, BodyAnalyzer); 'var': path; 'opaque': None;
        #   'brace': path (a brace-delimited type block spanning lines), extra = brace depth
        self.frames = []

    # -- logical lines ------------------------------------------------------------------------
    def feed(self, line, phys):
        if self.pending is None:
            if not line.strip(' \t\r'):
                return
            self.pending = line
            self.pending_line = phys
            self.depth = 0
        else:
            self.pending += '\r' + line
        self.depth += line.count('(') + line.count('[') - line.count(')') - line.count(']')
        if self.depth < 0:
            self.depth = 0
        if self.depth > 0:
            return
        stripped = self.pending.rstrip(' \t\r')
        if stripped.endswith('\\'):
            self.pending = stripped[:-1]
            return
        text, ln = self.pending, self.pending_line
        self.pending = None
        self.handle(text, ln)

    def directive_break(self):
        pass

    def finish(self):
        if self.pending is not None:
            text, ln = self.pending, self.pending_line
            self.pending = None
            self.handle(text, ln)
        while self.frames:
            self._pop()

    # -- structure ----------------------------------------------------------------------------
    def _pop(self):
        f = self.frames.pop()
        if f[1] == 'brace':
            self.r.unclosed_braces += 1

    def _ctx(self):
        for f in reversed(self.frames):
            if f[1] in ('type', 'brace'):
                return f[2]
            if f[1] == 'var':
                return f[2]
        return ''

    def handle(self, raw, ln):
        indent = indent_of(raw)
        text = self.expander.expand(raw)
        frames = self.frames
        # Inside a proc body?
        if frames and frames[-1][1] == 'proc':
            d, body = frames[-1][2]
            if indent > frames[-1][0] or body.det_brace > 0 or frames[-1][3] > 0 or \
                    (body.lines == 0 and text.lstrip(' \t\r').startswith('{')):
                if frames[-1][3] > 0 or (body.lines == 0 and text.lstrip(' \t\r').startswith('{')
                                         and indent <= frames[-1][0]):
                    frames[-1][3] += text.count('{') - text.count('}')
                    if frames[-1][3] <= 0:
                        frames[-1][3] = 0
                        body.feed(text, indent, ln)
                        self._pop()
                        return
                body.feed(text, indent, ln)
                return
            self._pop()
        # Inside a multi-line brace type block?
        if frames and frames[-1][1] == 'brace':
            f = frames[-1]
            f[3] += text.count('{') - text.count('}')
            if f[3] <= 0:
                frames.pop()
                return
            self._type_line(text, ln, indent, f[2], top=False)
            return
        while frames and frames[-1][1] != 'brace' and frames[-1][0] >= indent:
            self._pop()
        if frames and frames[-1][1] == 'var':
            self._var_block_line(text, frames[-1][2])
            return
        if frames and frames[-1][1] == 'opaque':
            return
        self._type_line(text, ln, indent, self._ctx(), top=True)

    def _var_block_line(self, text, owner):
        for item in text.split(';'):
            item = item.strip(' \t\r')
            if not item:
                continue
            path = re.match(r'(/?[A-Za-z_][\w/]*)', item)
            if path:
                self._record_var(owner, path.group(1).strip('/').split('/'))

    def _record_var(self, owner, segs):
        name, typ, is_global = parse_var_path(segs)
        if not name:
            return
        r = self.r
        if owner == '':
            r.global_vars.setdefault(name, typ)
        else:
            r.types.add(owner)
            r.type_vars.setdefault(owner, {}).setdefault(name, typ)

    def _type_line(self, text, ln, indent, ctx, top):
        items = split_items(text)
        last = len(items) - 1
        for i, (header, body, brace_open) in enumerate(items):
            self._item(header, body, brace_open, i == last and top, ln, indent, ctx, text)

    def _item(self, header, body, brace_open, can_open, ln, indent, ctx, whole):
        h = header.strip(' \t\r')
        r = self.r
        if not h:
            if body is not None and not brace_open:
                for it in split_items(body):
                    self._item(it[0], it[1], it[2], False, ln, indent, ctx, whole)
            return
        line = ln + whole.count('\r', 0, max(0, whole.find(h[:20])))
        vm = _VAR_DECL.match(h)
        if vm:
            full = join_path(ctx, vm.group(1))
            segs = full.strip('/').split('/')
            k = segs.index('var')
            owner = '/' + '/'.join(segs[:k]) if k else ''
            if owner:
                r.types.add(owner)
            self._record_var(owner, segs[k + 1:])
            return
        if _STRING_ONLY.match(h):
            return
        pm = _PROC_HDR.match(h)
        if pm and (pm.group(2) not in ('var',)) and pm.group(2) not in NOT_CALLS and \
                not _PATH_ASSIGN.match(h[:pm.end()]):
            close = _match_close(h, pm.end() - 1)
            if close >= 0:
                self._proc(pm, h, close, body, brace_open, can_open, line, indent, ctx)
                return
        am = _PATH_ASSIGN.match(h)
        if am:
            full = join_path(ctx, am.group(1))
            segs = full.strip('/').split('/')
            if 'var' in segs:
                k = segs.index('var')
                owner = '/' + '/'.join(segs[:k]) if k else ''
                self._record_var(owner, segs[k + 1:])
            elif len(segs) >= 1:
                owner = '/' + '/'.join(segs[:-1]) if len(segs) > 1 else ''
                if owner:
                    r.types.add(owner)
                if segs[-1] == 'parent_type':
                    pt = re.match(r'.*?=\s*(/[A-Za-z_][\w/]*)', h)
                    if pt and owner:
                        r.parent_type[owner] = pt.group(1).rstrip('/')
                        r.parent_type_decls.append((owner, pt.group(1).rstrip('/'), self.rel, line))
            return
        if _PATH_ONLY.match(h):
            full = join_path(ctx, h)
            segs = full.strip('/').split('/')
            if 'var' in segs:
                k = segs.index('var')
                owner = '/' + '/'.join(segs[:k]) if k else ''
                if k + 1 < len(segs):
                    self._record_var(owner, segs[k + 1:])
                elif can_open and body is None:
                    self.frames.append([indent, 'var', owner, 0])
                    return
                if body is not None:
                    for item in body.split(';'):
                        pm2 = re.match(r'\s*(/?[A-Za-z_][\w/]*)', item)
                        if pm2:
                            self._record_var(owner, pm2.group(1).strip('/').split('/'))
                return
            if segs[-1] not in ('proc', 'verb'):
                r.types.add(full)
            if body is not None:
                if brace_open:
                    self.frames.append([indent, 'brace', full, 1 + body.count('{') - body.count('}')])
                    if body.strip(' \t\r'):
                        self._type_line(body, line, indent, full, top=False)
                else:
                    for it in split_items(body):
                        self._item(it[0], it[1], it[2], False, line, indent, full, body)
                return
            if can_open:
                self.frames.append([indent, 'type', full, 0])
            return
        r.unparsed.append((self.rel, line, h[:100]))
        if can_open:
            self.frames.append([indent, 'opaque', None, 0])

    def _proc(self, pm, h, close, body, brace_open, can_open, line, indent, ctx):
        r = self.r
        prefix = join_path(ctx, pm.group(1)) if pm.group(1) else ctx
        segs = [s for s in prefix.strip('/').split('/') if s] if prefix else []
        name = pm.group(2)
        is_decl = False
        is_verb = False
        tsegs = segs
        for k, s in enumerate(segs):
            if s == 'proc' or s == 'verb':
                is_decl = True
                is_verb = s == 'verb'
                tsegs = segs[:k]
                break
        typ = '/' + '/'.join(tsegs) if tsegs else ''
        if typ:
            r.types.add(typ)
        args = h[pm.end():close]
        d = Def(self.rel, line, typ, name, is_decl, is_verb, args=args.replace('\r', ' '))
        r.defs.append(d)
        analyzer = BodyAnalyzer(d, indent, None)
        rest = h[close + 1:].strip(' \t\r')
        if rest.startswith('as '):
            rest = _AS_RETURN.sub('', rest, count=1).strip(' \t\r')
        if body is not None:
            if brace_open:
                self.frames.append([indent, 'proc', (d, analyzer), 1 + body.count('{') - body.count('}')])
                if body.strip(' \t\r'):
                    analyzer.feed(body, indent + 1, line)
            else:
                analyzer.feed(body, indent + 1, line)
            return
        if rest:
            analyzer.feed(rest, indent + 1, line)
            return
        if can_open:
            self.frames.append([indent, 'proc', (d, analyzer), 0])


def parse_tree(tg_root, dme='tgstation.dme', predefined=None, opaque=()):
    """Parse every file of the .dme. Returns (ParseResult, Preprocessor)."""
    result = ParseResult()
    pre = Preprocessor(tg_root, predefined or {}, frozenset(opaque),
                       lambda rel, expander: FileParser(rel, expander, result))
    pre.run(dme)
    for i, d in enumerate(result.defs):
        d.idx = i
    return result, pre
