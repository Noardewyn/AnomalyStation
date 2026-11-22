using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/InstrumentEditor.tsx (silicons SI6c, D326). The ID label's
// dotted underline and its tooltip are the label's tooltip; the Divider between the two columns is the kit's.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class InstrumentEditor
{
    // tg: InstrumentEditor.tsx:52
    public static UiNode Render(TguiContext c) => c.Window(750, 500, null,
        Settings(c),
        new UiNode(UiKind.Collapsible) { Key = "editor", Open = true, Title = "Music Editor", Icon = "pencil" }.With(Editing(c)),
        new UiNode(UiKind.Collapsible) { Key = "help", Title = "Help Section", Icon = "question" }.With(Help(c)));

    private static UiNode Number(string key, double value, double min, double max, Action<double> change, bool disabled = false) => new UiNode(UiKind.NumberInput)
    {
        Key = key,
        Value = value,
        MinValue = min,
        MaxValue = max,
        Step = 1,
        Disabled = disabled,
        OnChange = change,
    };

    // tg: InstrumentEditor.tsx:70
    private static UiNode Settings(TguiContext c)
    {
        UiValue d = c.Data;
        bool playing = d.Truthy("playing"), canSwitch = d.Truthy("can_switch_instrument"), ready = d.Truthy("instrument_ready"), sustain = d.Truthy("sustain_indefinitely");
        IReadOnlyList<UiValue> instruments = d.Arr("possible_instruments");
        IReadOnlyList<UiValue> lines = d.Arr("lines");
        var section = new UiNode(UiKind.Section) { Key = "settings", Title = "Settings" };
        if (lines.Count > 0)
            section.With(new UiNode(UiKind.Box) { Key = "play", FontSize = "16px" }.With(
                new UiNode(UiKind.Button) { Key = "play_music", Text = playing ? "Stop Music" : "Start Playing", OnClick = () => c.Act("play_music") }));
        section.With(
            new UiNode(UiKind.Stack) { Key = "id" }.With(
                new UiNode(UiKind.StackItem) { Text = "ID:", Tooltip = "All nearby instruments with the same ID will start playing the same song when any one starts playing.", Underline = true },
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Input) { Key = "id_input", MaxLength = 20, TextValue = d.Str("id"), CommitOnly = true, OnInput = value => c.Act("set_instrument_id", ("id", value)) })),
            new UiNode(UiKind.Stack) { Key = "repeats" }.With(
                new UiNode(UiKind.StackItem) { Text = "Repeats Left:" },
                new UiNode(UiKind.StackItem).With(Number("repeat", d.Num("repeat"), 0, d.Num("max_repeats"), v => c.Act("set_repeat_amount", ("amount", v)), playing))),
            !canSwitch ? null : new UiNode(UiKind.Stack) { Key = "instrument", Fill = true }.With(
                new UiNode(UiKind.StackItem) { Text = "Instrument Using" },
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Dropdown)
                {
                    Key = "instrument_pick",
                    Width = "40%",
                    TextValue = d.Str("using_instrument"),
                    Options = instruments.Select(i => i.Str("name")).ToArray(),
                    OnSelected = name => c.Act("change_instrument", ("new_instrument", instruments.FirstOrDefault(i => i.Str("name") == name)?.Str("id"))),
                })),
            new UiNode(UiKind.Stack) { Key = "columns" }.With(
                new UiNode(UiKind.StackItem) { Key = "playback" }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(
                    new UiNode(UiKind.Box) { Text = "Playback Settings:" },
                    new UiNode(UiKind.Stack) { Key = "shift" }.With(
                        new UiNode(UiKind.StackItem).With(Number("note_shift", d.Num("note_shift"), d.Num("note_shift_min"), d.Num("note_shift_max"), v => c.Act("set_note_shift", ("amount", v)))),
                        new UiNode(UiKind.StackItem) { Text = "keys / " + d.Num("octaves").ToString(System.Globalization.CultureInfo.InvariantCulture) + " octaves" }),
                    new UiNode(UiKind.Stack) { Key = "mode" }.With(
                        new UiNode(UiKind.StackItem) { Text = "Mode:" },
                        new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Dropdown)
                        {
                            Key = "sustain_mode",
                            Width = "100%",
                            TextValue = d.Str("sustain_mode"),
                            Options = d.Arr("sustain_modes").Select(m => m.AsStr()).ToArray(),
                            OnSelected = mode => c.Act("set_sustain_mode", ("new_mode", mode)),
                        })),
                    new UiNode(UiKind.Stack) { Key = "sustain" }.With(
                        new UiNode(UiKind.StackItem) { Text = d.Str("sustain_mode_button") + ":" },
                        new UiNode(UiKind.StackItem).With(Number("sustain_value", d.Num("sustain_mode_duration"), d.Num("sustain_mode_min"), d.Num("sustain_mode_max"), v => c.Act("edit_sustain_mode", ("amount", v))))))),
                new UiNode(UiKind.Divider) { Key = "divider", Vertical = true },
                new UiNode(UiKind.StackItem) { Key = "status" }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(
                    new UiNode(UiKind.Stack) { Key = "ready" }.With(
                        new UiNode(UiKind.StackItem) { Text = "Status:" },
                        new UiNode(UiKind.StackItem) { Color = ready ? "#5EFB6E" : "#FF0000", Text = ready ? "Ready" : "Instrument Definition Error!" }),
                    new UiNode(UiKind.Stack) { Key = "volume" }.With(
                        new UiNode(UiKind.StackItem) { Text = "Volume:" },
                        new UiNode(UiKind.StackItem).With(Number("volume_input", d.Num("volume"), d.Num("min_volume"), d.Num("max_volume"), v => c.Act("set_volume", ("amount", v))))),
                    new UiNode(UiKind.Stack) { Key = "dropoff" }.With(
                        new UiNode(UiKind.StackItem) { Text = "Volume Dropoff Threshold:" },
                        new UiNode(UiKind.StackItem).With(Number("dropoff_input", d.Num("volume_dropoff_threshold"), 1, 100, v => c.Act("set_dropoff_volume", ("amount", v))))),
                    new UiNode(UiKind.Button)
                    {
                        Key = "sustain_hold",
                        Text = sustain ? "Sustaining last held note indefinitely" : "Not sustaining last held note indefinitely",
                        OnClick = () => c.Act("toggle_sustain_hold_indefinitely"),
                    }))));
        return section;
    }

    // tg: InstrumentEditor.tsx:277
    private static UiNode Editing(TguiContext c)
    {
        IReadOnlyList<UiValue> lines = c.Data.Arr("lines");
        var list = new UiNode(UiKind.Box) { Key = "lines" };
        for (int index = 0; index < lines.Count; index++)
        {
            UiValue line = lines[index];
            double count = line.Num("line_count");
            list.With(new UiNode(UiKind.Stack) { Key = "line" + index, FontSize = "11px" }.With(
                new UiNode(UiKind.StackItem) { Text = "Line " + index + ":" },
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "edit", Text = "Edit", OnClick = () => c.Act("modify_line", ("line_editing", count)) }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "delete", Text = "X", OnClick = () => c.Act("delete_line", ("line_deleted", count)) }),
                new UiNode(UiKind.StackItem) { Text = line.Str("line_text") }));
        }
        return new UiNode(UiKind.Section) { Key = "editing" }.With(
            new UiNode(UiKind.Stack) { Key = "song" }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "new", Text = "Start a New Song", OnClick = () => c.Act("start_new_song") }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "import", Text = "Import a Song", OnClick = () => c.Act("import_song") })),
            new UiNode(UiKind.Stack) { Key = "tempo" }.With(
                new UiNode(UiKind.StackItem) { Text = "Tempo:" },
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "faster", Text = "-", OnClick = () => c.Act("tempo", ("tempo_change", "increase_speed")) }),
                new UiNode(UiKind.StackItem) { Text = c.Data.Num("bpm").ToString(System.Globalization.CultureInfo.InvariantCulture) + " BPM" },
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "slower", Text = "+", OnClick = () => c.Act("tempo", ("tempo_change", "decrease_speed")) })),
            list,
            new UiNode(UiKind.Box) { Key = "add" }.With(new UiNode(UiKind.Button) { Key = "add_line", Text = "Add Line", OnClick = () => c.Act("add_new_line") }));
    }

    // tg: InstrumentEditor.tsx:322
    private static UiNode Help(TguiContext c) => new UiNode(UiKind.Section) { Key = "help_text" }.With(new UiNode(UiKind.Box)
    {
        Wrap = true,
        Text = "Lines are a series of chords, separated by commas (,), each with notes separated by hyphens (-).\n"
            + "Every note in a chord will play together, with chord timed by the tempo.\n"
            + "Notes are played by the names of the note, and optionally, the accidental, and/or the octave number.\n"
            + "By default, every note is natural and in octave 3. Defining otherwise is remembered for each note.\n"
            + "Example: C,D,E,F,G,A,B will play a C major scale.\n"
            + "After a note has an accidental placed, it will be remembered: C,C4,C,C3 is C3,C4,C4,C3\n"
            + "Chords can be played simply by seperating each note with a hyphon: A-C#,Cn-E,E-G#,Gn-B\n"
            + "A pause may be denoted by an empty chord: C,E,,C,G\n"
            + "To make a chord be a different time, end it with /x, where the chord length will be length\n"
            + "defined by tempo / x: C,G/2,E/4\n"
            + "Combined, an example is: E-E4/4,F#/2,G#/8,B/8,E3-E4/4\n"
            + "Lines may be up to " + c.Data.Num("max_line_chars") + " characters.\n"
            + "A song may only contain up to " + c.Data.Num("max_lines") + " lines.",
    });
}
