using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Jukebox.tsx (Gaps H3, the bar: the jukebox and the
// radiant dance machine). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// The knob's three side buttons (min, max, reset) sit beside it in the box, as Canister's do; tg places them absolutely.

public static class Jukebox
{
    // tg: tgui/packages/tgui/interfaces/Jukebox.tsx:30
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool active = data.Truthy("active");
        bool looping = data.Truthy("looping");
        string trackSelected = data.Str("track_selected", null);
        double volume = data.Num("volume");
        var songs = data.Arr("songs");

        // tg: Jukebox.tsx:34  sortBy(songs, [(song) => song.name])
        var songsSorted = songs.OrderBy(s => s.Str("name"), System.StringComparer.Ordinal).ToArray();
        // tg: Jukebox.tsx:35  songs.find((song) => song.name === track_selected)
        UiValue songSelected = songs.FirstOrDefault(s => s.Str("name") == trackSelected);
        string selectedLength = songSelected?.Str("length");
        string selectedBeat = songSelected?.Str("beat");
        bool beatIsOne = songSelected?.Field("beat") is { Kind: UiValueKind.Number } b && b.Number == 1;

        // tg: Jukebox.tsx:40  <Window width={370} height={313}>
        return c.Window(370, 313, null,
            new UiNode(UiKind.Section)
            {
                Title = "Song Player",
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "toggle",
                        Icon = active ? "pause" : "play",
                        Text = active ? "Stop" : "Play",
                        Selected = active,
                        OnClick = () => c.Act("toggle"),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "loop",
                        Icon = "arrow-rotate-left",
                        Text = "Repeat",
                        Disabled = active,
                        Checkbox = looping,
                        OnClick = () => c.Act("loop", ("looping", !looping)),
                    }),
            }.With(
                new UiNode(UiKind.LabeledList).With(
                    // tg: Jukebox.tsx:64
                    new UiNode(UiKind.LabeledItem) { Label = "Track Selected" }.With(
                        new UiNode(UiKind.Dropdown)
                        {
                            Key = "track",
                            Width = "240px",
                            Options = songsSorted.Select(s => s.Str("name")).ToArray(),
                            Disabled = active,
                            TextValue = string.IsNullOrEmpty(songSelected?.Str("name")) ? "Select a Track" : songSelected.Str("name"),
                            OnSelected = value => c.Act("select_track", ("track", value)),
                        }),
                    // tg: Jukebox.tsx:77
                    new UiNode(UiKind.LabeledItem) { Label = "Track Length" }.With(
                        new UiNode(UiKind.Box) { Text = string.IsNullOrEmpty(selectedLength) ? "No Track Selected" : selectedLength }),
                    // tg: Jukebox.tsx:80
                    new UiNode(UiKind.LabeledItem) { Label = "Track Beat" }.With(
                        new UiNode(UiKind.Box)
                        {
                            Text = (string.IsNullOrEmpty(selectedBeat) || selectedBeat == "0" ? "No Track Selected" : selectedBeat)
                                + (beatIsOne ? " beat" : " beats"),
                        }))),
            // tg: Jukebox.tsx:86
            new UiNode(UiKind.Section) { Title = "Machine Settings" }.With(
                new UiNode(UiKind.LabeledControls) { Justify = "center" }.With(
                    new UiNode(UiKind.LabeledControl) { Label = "Volume" }.With(
                        new UiNode(UiKind.Box).With(
                            new UiNode(UiKind.Knob)
                            {
                                Key = "volume",
                                Size = 3.2,
                                Color = volume >= 25 ? "red" : "green",
                                Value = volume,
                                Unit = "%",
                                MinValue = 0,
                                MaxValue = 50,
                                Step = 1,
                                StepPixelSize = 1,
                                OnChange = value => c.Act("set_volume", ("volume", value)),
                            },
                            new UiNode(UiKind.Button)
                            {
                                Key = "min",
                                Color = "transparent",
                                Icon = "fast-backward",
                                OnClick = () => c.Act("set_volume", ("volume", "min")),
                            },
                            new UiNode(UiKind.Button)
                            {
                                Key = "max",
                                Color = "transparent",
                                Icon = "fast-forward",
                                OnClick = () => c.Act("set_volume", ("volume", "max")),
                            },
                            new UiNode(UiKind.Button)
                            {
                                Key = "reset",
                                Color = "transparent",
                                Icon = "undo",
                                OnClick = () => c.Act("set_volume", ("volume", "reset")),
                            })))));
    }
}
