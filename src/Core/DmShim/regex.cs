using System.Text;
using System.Text.RegularExpressions;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

/// <summary>
/// The BYOND <c>/regex</c> datum (BYOND ref <c>/regex</c>; OpenDream <c>DreamObjectRegex</c> and
/// <c>DreamProcNativeRegex</c>, which this follows): a compiled pattern created by <c>regex(pattern, flags)</c> or
/// <c>new /regex(pattern, flags)</c>, with <c>Find</c>, <c>Replace</c> and their <c>_char</c> variants. The flags are
/// <c>"g"</c> (global: <c>Find</c> resumes at <c>next</c>, <c>Replace</c> replaces every match), <c>"i"</c> (ignore
/// case) and <c>"m"</c> (multiline). BYOND's escapes that .NET lacks are rewritten: <c>\l</c> (a letter), <c>\L</c> (not
/// a letter) and <c>\_</c>. The replacement text uses .NET's syntax, which BYOND's shares: <c>$1</c>-<c>$9</c>,
/// <c>$&amp;</c>, <c>$`</c> and <c>$'</c>. A proc as the replacement (a global proc reference, which is text in the port) is
/// called with the match and its groups, and its result replaces the match. Positions
/// count UTF-16 code units in <c>Find</c> and <c>Replace</c> and characters in the <c>_char</c> variants (DV4,
/// <see cref="DmTextUnits"/>). A builtin value type: no world id, never registered. Owner: values.
/// </summary>
[DmPath("/regex")]
public class regex
{
    /// <summary>The flags text the regex was made with.</summary>
    public object flags;

    /// <summary>The groups of the last match (1-based: <c>group[1]</c> is <c>$1</c>), or null before one.</summary>
    public DmList group;

    /// <summary>The 1-based position of the last match.</summary>
    public object index;

    /// <summary>The text of the last match.</summary>
    public object match;

    /// <summary>The pattern text.</summary>
    public object name;

    /// <summary>With the <c>"g"</c> flag, the position after the last match, where the next <c>Find</c> starts; null when the last search found nothing.</summary>
    public object next;

    /// <summary>The last haystack searched, or the result of the last <c>Replace</c>.</summary>
    public object text;

    private Regex _regex;
    private bool _global;

    /// <summary>DM <c>new /regex(pattern, flags)</c>; a /regex as the pattern copies it.</summary>
    public void New(object pattern = null, object flags = null)
    {
        if (pattern is regex copy_from)
        {
            name = copy_from.name;
            this.flags = copy_from.flags;
            _regex = copy_from._regex;
            _global = copy_from._global;
            return;
        }
        if (pattern is not string pattern_text)
            throw new DmRuntimeException("regex: invalid pattern " + Dm.str(pattern));
        name = pattern_text;
        this.flags = flags;
        var options = RegexOptions.None;
        if (flags is string flags_text)
        {
            if (flags_text.Contains('i')) options |= RegexOptions.IgnoreCase;
            if (flags_text.Contains('m')) options |= RegexOptions.Multiline;
            if (flags_text.Contains('g')) _global = true;
        }
        try
        {
            _regex = new Regex(Translate(pattern_text), options | RegexOptions.CultureInvariant);
        }
        catch (ArgumentException e)
        {
            throw new DmRuntimeException("regex: invalid pattern \"" + pattern_text + "\": " + e.Message);
        }
    }

    /// <summary>
    /// DM <c>R.Find(haystack, Start = 1, End = 0)</c>: the 1-based position of the first match from Start up to End (0
    /// is the end of the text), or 0. Sets <c>text</c>, and on a match <c>match</c>, <c>index</c> and <c>group</c>; with
    /// the <c>"g"</c> flag a call without Start on the same haystack resumes at <c>next</c>.
    /// </summary>
    public object Find(object haystack = null, object Start = null, object End = null) => FindIn(haystack, Start, End, characters: false);

    /// <summary>DM <c>R.Find_char</c>: <see cref="Find"/> counting characters.</summary>
    public object Find_char(object haystack = null, object Start = null, object End = null) => FindIn(haystack, Start, End, characters: true);

    /// <summary>
    /// DM <c>R.Replace(haystack, replacement, Start = 1, End = 0)</c>: the haystack with the first match from Start (every
    /// match with the <c>"g"</c> flag) replaced; the text after End is kept as is. Sets <c>text</c> to the result and,
    /// without the <c>"g"</c> flag, <c>next</c> to the position after the replacement.
    /// </summary>
    public object Replace(object haystack = null, object replacement = null, object Start = null, object End = null) => ReplaceIn(haystack, replacement, Start, End, characters: false);

    /// <summary>DM <c>R.Replace_char</c>: <see cref="Replace"/> counting characters.</summary>
    public object Replace_char(object haystack = null, object replacement = null, object Start = null, object End = null) => ReplaceIn(haystack, replacement, Start, End, characters: true);

    private object FindIn(object haystack, object start_arg, object end_arg, bool characters)
    {
        string haystack_text = haystack as string ?? "";
        var units = new DmTextUnits(haystack_text, characters);
        int next_position;
        if (start_arg == null)
            next_position = _global && text is string last && last == haystack_text && next != null ? (int)Dm.num(next) : 1;
        else
            next_position = (int)Dm.num(start_arg);
        int end = (int)Dm.num(end_arg);

        text = haystack;

        if (units.Length <= next_position - 1)
        {
            if (_global) next = null;
            return 0f;
        }

        // End is the position after the last character searched, as findtext's (0: the end of the text)
        int begin_unit = Math.Clamp(next_position - 1, 0, units.Length);
        int end_unit = end == 0 ? units.Length : Math.Clamp(end - 1, begin_unit, units.Length);
        int begin = units.Offset(begin_unit);
        Match found = _regex.Match(haystack_text, begin, units.Offset(end_unit) - begin);
        if (found.Success)
        {
            int position = units.UnitAt(found.Index) + 1;
            index = (float)position;
            match = found.Value;
            DmList groups = Dm.list();
            for (int i = 1; i < found.Groups.Count; i++)
                Dm.append(ref groups, found.Groups[i].Success ? found.Groups[i].Value : null);
            group = groups;
            // An empty match moves next one past it (gaps TY1): otherwise a global Find loop over a pattern that can match
            // nothing finds the same empty match forever. The BYOND ref is silent and OpenDream does not move; tg relies on it
            // (code/modules/chatter/chatter.dm:12 loops `while(R.Find(message) != 0)` with "([\l\d]*)([^\l\d\s])?" over speech).
            if (_global) next = (float)(units.UnitAt(found.Index + found.Length) + 1 + (found.Length == 0 ? 1 : 0));
            return (float)position;
        }

        if (_global) next = null;
        return 0f;
    }

    private object ReplaceIn(object haystack, object replacement, object start_arg, object end_arg, bool characters)
    {
        if (haystack is not string haystack_text)
            return null;
        if (replacement != null && replacement is not string)
            throw new DmRuntimeException("regex.Replace: the replacement must be text or a proc");
        string replacement_text = (string)replacement ?? "";
        var units = new DmTextUnits(haystack_text, characters);
        int start = start_arg == null ? 1 : (int)Dm.num(start_arg);
        int end = (int)Dm.num(end_arg);

        int begin_unit = Math.Clamp(start - 1, 0, units.Length);
        int end_unit = end == 0 ? units.Length : Math.Clamp(end - 1, begin_unit, units.Length);
        string head = units.Sub(0, end_unit);
        string tail = units.Sub(end_unit, units.Length);
        int begin = units.Offset(begin_unit);

        // A proc as the replacement (a proc reference is text in the port, rules 7.7: GLOBAL_PROC_REF(x) is "/proc/x"): it is
        // called with the match and its groups, and its result, as text, replaces the match (OpenDream DoProcReplace).
        // A proc of the regex's own type ("/regex/format_component/proc/x", wiremod's format component, gaps CI1) is called with the
        // regex as src, as BYOND does.
        object proc_src = null;
        bool own_proc = replacement_text != null && replacement_text.StartsWith("/regex/", StringComparison.Ordinal)
            && replacement_text.Contains("/proc/", StringComparison.Ordinal)
            && DmBinder.Resolve(this, replacement_text.Substring(replacement_text.LastIndexOf('/') + 1)) != DmBinder.Target.Missing;
        if (own_proc)
        {
            proc_src = this;
            replacement_text = replacement_text.Substring(replacement_text.LastIndexOf('/') + 1);
        }
        if (own_proc || (DmBinder.IsGlobalProcPath(replacement_text) && DmBinder.Resolve(null, replacement_text) != DmBinder.Target.Missing))
        {
            string current = head;
            int position = begin;
            while (position <= current.Length)
            {
                Match found = _regex.Match(current, position);
                if (!found.Success)
                    break;
                var call_args = new object[found.Groups.Count];
                for (int i = 0; i < found.Groups.Count; i++)
                    call_args[i] = found.Groups[i].Success ? found.Groups[i].Value : null;
                string piece = Dm.str(Waiting.NoSleep(Dm.Call(proc_src, replacement_text, call_args), "regex.Replace"));
                current = current.Substring(0, found.Index) + piece + current.Substring(found.Index + found.Length);
                position = found.Index + Math.Max(piece.Length, 1);
                if (!_global)
                {
                    next = (float)(units.UnitAt(Math.Min(found.Index, haystack_text.Length)) + new DmTextUnits(piece, characters).Length + 1);
                    break;
                }
            }
            string proc_replaced = current + tail;
            text = proc_replaced;
            return proc_replaced;
        }

        if (!_global)
        {
            Match found = _regex.Match(head, begin);
            if (!found.Success)
                return haystack_text;
            string piece = found.Result(replacement_text);
            next = (float)(units.UnitAt(found.Index) + new DmTextUnits(piece, characters).Length + 1);
        }

        string replaced = _regex.Replace(head, replacement_text, _global ? -1 : 1, begin) + tail;
        text = replaced;
        return replaced;
    }

    // BYOND's escapes that .NET does not know (OpenDream DreamObjectRegex.Initialize): \l a letter, \L not a letter, \_ an
    // underscore.
    private static string Translate(string pattern)
    {
        var built = new StringBuilder(pattern.Length);
        bool inside_brackets = false;
        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];
            if (c == '\\' && i + 1 < pattern.Length)
            {
                char escaped = pattern[++i];
                if (escaped == 'l')
                    built.Append(inside_brackets ? "A-Za-z" : "[A-Za-z]");
                else if (escaped == 'L')
                    built.Append(inside_brackets ? "\\W0-9_" : "[\\W0-9_]");
                else if (escaped == '_')
                    built.Append('_');
                else
                    built.Append('\\').Append(escaped);
                continue;
            }
            if (c == '[')
                inside_brackets = true;
            else if (c == ']' && inside_brackets)
                inside_brackets = false;
            built.Append(c);
        }
        return built.ToString();
    }
}

public static partial class Dm
{
    /// <summary>DM <c>regex(pattern, flags)</c> (BYOND ref <c>/proc/regex</c>): a new <see cref="DmShim.regex"/>; a /regex as the pattern copies it.</summary>
    public static regex regex(object pattern = null, object flags = null)
    {
        var made = new regex();
        made.New(pattern, flags);
        return made;
    }

    /// <summary>DM <c>REGEX_QUOTE(text)</c>: the text with every regex metacharacter escaped, to match it literally.</summary>
    public static string REGEX_QUOTE(object text) => Regex.Escape(text as string ?? "").Replace("]", "\\]").Replace("}", "\\}");

    /// <summary>DM <c>REGEX_QUOTE_REPLACEMENT(text)</c>: the text with every <c>$</c> doubled, to use it literally as a replacement.</summary>
    public static string REGEX_QUOTE_REPLACEMENT(object text) => (text as string ?? "").Replace("$", "$$");
}
