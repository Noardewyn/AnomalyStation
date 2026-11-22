using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/PaperSheet/ (index.tsx, PrimaryView.tsx, Preview.tsx,
// Stamper.tsx, StampView.tsx, Stamp.tsx, helpers.ts, types.ts, constants.ts; gaps S3, D298). Engine-free (compiled into the tests too).
// DEVIATION(DV300): tg renders each input with `marked` (gfm, breaks, smartypants; links and images killed) into HTML that DOMPurify
// cleans and the browser draws in the pen's font; the client turns the same markdown into the chat's HTML subset (MarkdownToHtml below)
// and draws it with the kit's Html text: bold, italic, colours, line breaks, lists and rules show, the pen fonts, heading sizes and table
// borders do not. tg's `<input>` fields inside the text are drawn as the field's brackets in the text, with a box per open field under
// the page while writing; the stamps are bureaucracy.dmi's paper_stamp impressions (DV44: the client draws tg's sprites), not tg's
// large stamp PNGs.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class PaperSheet
{
    // tg: constants.ts:1
    private const int TEXTAREA_INPUT_HEIGHT = 200;
    // tg: types.ts:50  enum InteractionType
    private const int InteractionReading = 0, InteractionWriting = 1, InteractionStamping = 2;
    // the text area's key: its text is also the window's local state "input:paper_text"
    private const string TextAreaKey = "paper_text";

    /// <summary>The stamp's picture: tg's stamp class (the stamp's icon state, stamps.cs) as bureaucracy.dmi's impression on paper.</summary>
    public const string StampIcon = "icons/obj/service/bureaucracy.dmi";

    public static string StampState(string stampClass) => "paper_" + stampClass;

    // tg: index.tsx:9
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue held = data.Get("held_item_details");
        bool writeMode = CanEdit(held);
        if (!writeMode && c.UseState("inputFieldData", Empty).Count > 0)
            c.Local.SetQuiet("inputFieldData", Empty); // tg: index.tsx:14 the open fields' text is dropped out of write mode

        c.WindowTheme = "paper";
        // tg: index.tsx:24  <Window title={paper_name} theme="paper" width={420} height={500 + (writeMode ? TEXTAREA_INPUT_HEIGHT : 0)}>
        //      <Window.Content backgroundColor={paper_color}>
        return c.Window(420, 500 + (writeMode ? TEXTAREA_INPUT_HEIGHT : 0), data.Str("paper_name"), PrimaryView(c));
    }

    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

    // tg: helpers.ts:3
    public static bool CanEdit(UiValue heldItemDetails)
        => heldItemDetails != null && heldItemDetails.Kind == UiValueKind.Object && (int)heldItemDetails.Num("interaction_mode") == InteractionWriting;

    // tg: PrimaryView.tsx:11
    private static UiNode PrimaryView(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue held = data.Get("held_item_details");
        string paper_color = data.Str("paper_color", "#FFFFFF");
        double max_length = data.Num("max_length", 5000);
        string useFont = Fallback(held?.Str("font", null), data.Str("default_pen_font"));
        string useColor = Fallback(held?.Str("color", null), data.Str("default_pen_color"));
        bool useBold = held != null && held.Truthy("use_bold");

        var inputFieldData = c.UseState("inputFieldData", Empty);
        string textAreaText = c.UseState("textAreaText", "");
        int interactMode = held != null && held.Kind == UiValueKind.Object ? (int)held.Num("interaction_mode") : InteractionReading;
        bool savableData = textAreaText.Length > 0 || inputFieldData.Count > 0;
        int dmCharacters = data.Arr("raw_text_input").Sum(i => i.Str("raw_text").Length);
        int usedCharacters = dmCharacters + textAreaText.Length;
        bool tooManyCharacters = usedCharacters > max_length;
        bool canEdit = interactMode == InteractionWriting;

        // tg: Preview.tsx:497 render: the DM-side text, then (writing) the text area's preview
        (string dmText, int fieldCount, List<(int Index, int Length)> openFields) = PreviewFromDM(data, !canEdit);
        string previewText = dmText;
        if (canEdit)
            previewText += FormatAndProcessRawText(data, textAreaText, useFont, useColor, useBold, fieldCount, true, false, null).Text;

        // tg: Stamper.tsx (the ghost stamp of a held stamp) and StampView.tsx (the paper's stamps)
        string stampClass = held?.Str("stamp_class", null);
        var page = new UiNode(UiKind.PaperPage)
        {
            Key = "page",
            TextValue = "<span class='paper-text'>" + previewText + "</span>",
            Background = paper_color,
            Ghost = interactMode == InteractionStamping && !string.IsNullOrEmpty(stampClass) ? StampNode("ghost", stampClass, 0, 0, 0, 0.5) : null,
        };
        if (page.Ghost != null)
            page.OnStamp = (x, y, rotation) => c.Act("add_stamp", ("x", x), ("y", y), ("rotation", rotation));
        int index = 0;
        foreach (UiValue stamp in data.Arr("raw_stamp_input"))
            page.With(StampNode("stamp:" + index++, stamp.Str("class"), stamp.Num("x"), stamp.Num("y"), stamp.Num("rotation"), null));

        var column = new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            // tg: PrimaryView.tsx:88  <Flex.Item grow={3} basis={1}><PreviewView/> (Preview.tsx:524 <Section fill fitted scrollable>)
            new UiNode(UiKind.StackItem) { Grow = 3, Scrollable = true, Background = paper_color }.With(page));
        if (canEdit)
        {
            var fieldRows = new List<UiNode>();
            double maxField = data.Num("max_input_field_length", 42);
            foreach ((int fieldIndex, int length) in openFields)
            {
                string key = fieldIndex.ToString(CultureInfo.InvariantCulture);
                fieldRows.Add(new UiNode(UiKind.Stack) { Key = "field-row:" + key }.With(
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Color = "label", Text = $"Field {fieldIndex + 1}:" }),
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Input)
                    {
                        Key = "field:" + key,
                        Fluid = true,
                        MaxLength = (int)Math.Min(maxField, length),
                        TextValue = inputFieldData.TryGetValue(key, out string value) ? value : "",
                        OnInput = text =>
                        {
                            // tg: Preview.tsx:121 onInputHandler: a typed field is kept, an emptied one dropped
                            var fields = new Dictionary<string, string>(c.UseState("inputFieldData", Empty), StringComparer.Ordinal);
                            if (text.Length > 0)
                                fields[key] = text;
                            else
                                fields.Remove(key);
                            c.SetState<IReadOnlyDictionary<string, string>>("inputFieldData", fields);
                        },
                    })));
            }
            // tg: PrimaryView.tsx:99  <Flex.Item shrink={1} height={`${TEXTAREA_INPUT_HEIGHT}px`}>
            column.With(new UiNode(UiKind.StackItem) { Height = TEXTAREA_INPUT_HEIGHT + "px" }.With(new UiNode(UiKind.Section)
            {
                Title = "Insert Text",
                Fill = true,
                Buttons = new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Color = tooManyCharacters ? "bad" : null, Text = $"{usedCharacters} / {max_length.ToString(CultureInfo.InvariantCulture)}" }),
                    new UiNode(UiKind.StackItem).With(c.ButtonConfirm("save", new UiNode(UiKind.Button)
                    {
                        Text = "Save",
                        Color = "good",
                        Disabled = !savableData || tooManyCharacters,
                        OnClick = () =>
                        {
                            // tg: PrimaryView.tsx:118
                            if (textAreaText.Length > 0)
                            {
                                c.Act("add_text", ("text", textAreaText));
                                c.SetState("textAreaText", "");
                                c.SetState("input:" + TextAreaKey, "");
                            }
                            if (inputFieldData.Count > 0)
                            {
                                c.Act("fill_input_field", ("field_data", UiValue.ObjectOf(inputFieldData.Select(kv => new KeyValuePair<string, UiValue>(kv.Key, TguiContext.ToValue(kv.Value))))));
                                c.SetState("inputFieldData", Empty);
                            }
                        },
                    }))),
            }.With(
                new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                    fieldRows.Count > 0 ? new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Stack) { Vertical = true }.With(fieldRows)) : null,
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.TextArea)
                    {
                        Key = TextAreaKey,
                        TextValue = textAreaText,
                        Wrap = true,
                        Height = "100%",
                        Color = useColor,
                        Bold = useBold,
                        OnInput = text => c.SetState("textAreaText", text),
                    })))));
        }
        return column;
    }

    private static string Fallback(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

    // tg: Stamp.tsx:7  <div className="Paper__Stamp {sprite}" style={left: x, top: y + yOffset, transform: rotate(rotation deg), opacity}>
    private static UiNode StampNode(string key, string stampClass, double x, double y, double rotation, double? opacity)
        => new UiNode(UiKind.Box)
        {
            Key = key,
            DmIcon = StampIcon,
            DmIconState = StampState(stampClass),
            CropToPixels = true,
            Width = "64px",
            Height = "64px",
            PosX = x,
            PosY = y,
            Rotation = rotation,
            Opacity = opacity,
        };

    // tg: Preview.tsx:155 createPreviewFromDM: every DM-side input, its fields numbered on from the previous one; the open fields come out
    // for the write mode's boxes
    public static (string Text, int FieldCount, List<(int Index, int Length)> OpenFields) PreviewFromDM(UiValue data, bool readOnly)
    {
        var output = new StringBuilder();
        int fieldCount = 0;
        var open = new List<(int, int)>();
        foreach (UiValue value in data.Arr("raw_text_input"))
        {
            string rawText = value.Str("raw_text").Trim();
            if (rawText.Length == 0)
                continue;
            string fontColor = Fallback(value.Str("color", null), data.Str("default_pen_color"));
            string fontFace = Fallback(value.Str("font", null), data.Str("default_pen_font"));
            (string text, int next) = FormatAndProcessRawText(data, rawText, fontFace, fontColor, value.Truthy("bold"), fieldCount, readOnly, value.Truthy("advanced_html"), open);
            output.Append(text);
            fieldCount = next;
        }
        return (output.ToString(), fieldCount, open);
    }

    // tg: Preview.tsx:303 formatAndProcessRawText: markdown, sanitising, the fields, the writing implement's span
    public static (string Text, int NextCounter) FormatAndProcessRawText(UiValue data, string rawText, string font, string color, bool bold, int fieldCounter, bool forceReadonlyFields, bool advancedHtml, List<(int Index, int Length)> openFields)
    {
        // First lets make sure it ends in a new line (tg's own: rawText[rawText.length] is always undefined, so two new lines)
        rawText += "\n\n";
        string parsedText = MarkdownToHtml(rawText);
        string sanitizedText = SanitizeText(parsedText, advancedHtml);
        (string fielded, int next) = CreateFields(data, sanitizedText, color, forceReadonlyFields, fieldCounter, openFields);
        // tg: Preview.tsx:262 setFontInText
        string fonted = $"<span style=\"color:{color};font-family:{font};{(bold ? "font-weight: bold;" : "")}\">{fielded}</span>";
        return (fonted, next);
    }

    // tg: Preview.tsx:25 fieldRegex
    private static readonly Regex FieldRegex = new(@"\[((?:_+))\]", RegexOptions.Compiled);

    // tg: Preview.tsx:362 createFields: a filled field shows its text (a signature italic), an open one its blank
    private static (string Text, int Counter) CreateFields(UiValue data, string text, string color, bool readOnly, int counter, List<(int Index, int Length)> openFields)
    {
        IReadOnlyList<UiValue> raw_field_input = data.Arr("raw_field_input");
        string result = FieldRegex.Replace(text, match =>
        {
            int index = counter++;
            int length = match.Groups[1].Value.Length;
            UiValue filled = raw_field_input.FirstOrDefault(f => FieldIndexText(f.Get("field_index")) == index.ToString(CultureInfo.InvariantCulture));
            if (filled != null)
            {
                // tg: Preview.tsx:451 createFilledInputField: bold, the field's colour, a signature italic
                UiValue fieldData = filled.Get("field_data");
                string fieldColor = Fallback(fieldData?.Str("color", null), color);
                string style = $"color:{fieldColor};font-weight: bold;" + (filled.Truthy("is_signature") ? "font-style: italic;" : "");
                return $"[<span style=\"{style}\">{Escape(fieldData?.Str("raw_text") ?? "")}</span>]";
            }
            if (!readOnly)
                openFields?.Add((index, length));
            return "[" + new string('_', length) + "]";
        });
        return (result, counter);
    }

    // tgui compares field_index with `${counter}`: the DM side sends the window's text key ("0"), a number would never match
    private static string FieldIndexText(UiValue value) => value == null ? null : value.Kind == UiValueKind.String ? value.AsStr() : null;

    // tg: sanitize.ts sanitizeText (DOMPurify): scripts, frames, forms, styles and event handlers go; the kit's HTML text draws only its
    // subset anyway
    private static readonly Regex Dangerous = new(@"<\s*(script|style|iframe|object|embed|form|input|textarea|button|select|link|meta)\b[^>]*>(.*?<\s*/\s*\1\s*>)?", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex Handlers = new(@"\son\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string SanitizeText(string html, bool advancedHtml)
    {
        string clean = Dangerous.Replace(html, "");
        clean = Handlers.Replace(clean, "");
        if (!advancedHtml)
            clean = Regex.Replace(clean, @"<\s*img\b[^>]*>", "", RegexOptions.IgnoreCase);
        return clean;
    }

    private static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    // ---- tg's `marked` (gfm, breaks: true, smartypants; links and images as text: helpers.ts walkTokens), the client's subset ----

    private static readonly Regex Heading = new(@"^ {0,3}(#{1,6})(?:\s+(.*?))?\s*#*\s*$", RegexOptions.Compiled);
    private static readonly Regex Rule = new(@"^ {0,3}([-*_])(?:\s*\1){2,}\s*$", RegexOptions.Compiled);
    private static readonly Regex Bullet = new(@"^ {0,3}([-*+])\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex Ordered = new(@"^ {0,3}(\d{1,9})[.)]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex Quote = new(@"^ {0,3}>\s?(.*)$", RegexOptions.Compiled);
    private static readonly Regex Fence = new(@"^ {0,3}(```|~~~)", RegexOptions.Compiled);

    /// <summary>
    /// The markdown of a paper input as the HTML the kit draws: headings (bold lines), rules, block quotes, bullet and numbered lists,
    /// code, paragraphs with a line break per new line (marked's <c>breaks</c>), bold, italic, struck text and code spans; links and
    /// images keep their text; the HTML a writer typed passes through; tg's <c>[___]</c> fields are kept whole for the fields' pass.
    /// </summary>
    public static string MarkdownToHtml(string src)
    {
        string[] lines = src.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var html = new StringBuilder();
        var paragraph = new List<string>();
        int i = 0;

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
                return;
            html.Append("<p>").Append(string.Join("<br>", paragraph.Select(Inline))).Append("</p>");
            paragraph.Clear();
        }

        while (i < lines.Length)
        {
            string line = lines[i];
            if (line.Trim().Length == 0)
            {
                FlushParagraph();
                i++;
                continue;
            }
            Match m;
            if ((m = Fence.Match(line)).Success)
            {
                FlushParagraph();
                string fence = m.Groups[1].Value;
                var code = new List<string>();
                i++;
                while (i < lines.Length && !lines[i].TrimStart().StartsWith(fence, StringComparison.Ordinal))
                    code.Add(Escape(lines[i++]));
                i++;
                html.Append("<pre><code>").Append(string.Join("<br>", code)).Append("</code></pre>");
                continue;
            }
            if ((m = Heading.Match(line)).Success)
            {
                FlushParagraph();
                int level = m.Groups[1].Value.Length;
                html.Append($"<h{level}><b>").Append(Inline(m.Groups[2].Value)).Append($"</b></h{level}>");
                i++;
                continue;
            }
            if (Rule.IsMatch(line) && paragraph.Count == 0)
            {
                html.Append(RuleHtml);
                i++;
                continue;
            }
            if (Quote.IsMatch(line))
            {
                FlushParagraph();
                var quoted = new List<string>();
                while (i < lines.Length && (m = Quote.Match(lines[i])).Success)
                {
                    quoted.Add(m.Groups[1].Value);
                    i++;
                }
                html.Append("<blockquote><i>").Append(MarkdownToHtml(string.Join("\n", quoted))).Append("</i></blockquote>");
                continue;
            }
            if (Bullet.IsMatch(line) || Ordered.IsMatch(line))
            {
                FlushParagraph();
                bool ordered = Ordered.IsMatch(line);
                int number = ordered ? int.Parse(Ordered.Match(line).Groups[1].Value, CultureInfo.InvariantCulture) : 1;
                html.Append(ordered ? "<ol>" : "<ul>");
                while (i < lines.Length && (m = ordered ? Ordered.Match(lines[i]) : Bullet.Match(lines[i])).Success)
                {
                    html.Append("<li>").Append(ordered ? number++ + ". " : "• ").Append(Inline(m.Groups[2].Value)).Append("</li>");
                    i++;
                }
                html.Append(ordered ? "</ol>" : "</ul>");
                continue;
            }
            paragraph.Add(line);
            i++;
        }
        FlushParagraph();
        return Regex.Replace(html.ToString(), @"<\s*hr\s*/?\s*>", RuleHtml, RegexOptions.IgnoreCase);
    }

    // a rule as the chat's text draws it: a line of its own
    private const string RuleHtml = "<br>────────────────────<br>";

    private static readonly Regex Tag = new(@"<[^>]*>", RegexOptions.Compiled);

    // the inline rules on the text between the HTML tags a writer typed
    private static string Inline(string text)
    {
        var sb = new StringBuilder();
        int at = 0;
        foreach (Match tag in Tag.Matches(text))
        {
            sb.Append(InlineText(text.Substring(at, tag.Index - at)));
            sb.Append(tag.Value);
            at = tag.Index + tag.Length;
        }
        sb.Append(InlineText(text.Substring(at)));
        return sb.ToString();
    }

    private static readonly Regex CodeSpan = new(@"`([^`]+)`", RegexOptions.Compiled);
    private static readonly Regex Image = new(@"!\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex Link = new(@"\[([^\]]+)\]\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex Strong = new(@"(\*\*|__)(?=\S)(.+?)(?<=\S)\1", RegexOptions.Compiled);
    private static readonly Regex Em = new(@"(?<![\w*])(\*|_)(?=\S)(.+?)(?<=\S)\1(?![\w*])", RegexOptions.Compiled);
    private static readonly Regex Del = new(@"~~(?=\S)(.+?)(?<=\S)~~", RegexOptions.Compiled);

    private static string InlineText(string text)
    {
        if (text.Length == 0)
            return text;
        // the fields first, out of the emphasis rules' way (tg's inputField tokenizer, helpers.ts:14)
        var kept = new List<string>();
        string Keep(string value)
        {
            kept.Add(value);
            return "" + (kept.Count - 1).ToString(CultureInfo.InvariantCulture) + "";
        }
        text = FieldRegex.Replace(text, m => Keep(m.Value));
        text = CodeSpan.Replace(text, m => Keep("<code>" + Escape(m.Groups[1].Value) + "</code>"));
        text = Image.Replace(text, m => m.Groups[1].Value); // walkTokens: images as text
        text = Link.Replace(text, m => m.Groups[1].Value); // links as text
        text = Strong.Replace(text, m => "<b>" + m.Groups[2].Value + "</b>");
        text = Em.Replace(text, m => "<i>" + m.Groups[2].Value + "</i>");
        text = Del.Replace(text, m => "<s>" + m.Groups[1].Value + "</s>");
        text = SmartyPants(text);
        return Regex.Replace(text, "(\\d+)", m => kept[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)]);
    }

    // marked-smartypants: dashes, the ellipsis and curly quotes
    private static string SmartyPants(string text)
    {
        text = text.Replace("---", "—").Replace("--", "–").Replace("...", "…");
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            bool opening = i == 0 || char.IsWhiteSpace(text[i - 1]) || "([{—–".Contains(text[i - 1]);
            if (ch == '"')
                sb.Append(opening ? '“' : '”');
            else if (ch == '\'')
                sb.Append(opening ? '‘' : '’');
            else
                sb.Append(ch);
        }
        return sb.ToString();
    }
}
