using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/MODpaint.jsx (gaps MO1): the MOD paint kit's
// recolouring (mod/mod_paint.dm ui_data): the colour matrix's nine channel sliders (what red, green and blue each turn to, 0 to
// 125 %), the six presets, the help and confirm buttons, and the preview of the suit (the proxy map view: a screen object with no
// turfs, so drawn as a picture, MapPicture). Each slider move or preset sends the whole matrix (transition_color); confirm applies
// it. Flex and Flex.Item are the kit's Stack and StackItem. Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class MODpaint
{
    // tg: MODpaint.jsx:15
    public static double[] ColorToMatrix(string preset) => preset switch
    {
        "red" => new[] { 1, 0, 0, 0, 0.25, 0.5, 0, 0, 0.25, 0, 0.5, 0, 0, 0, 0, 1, 0, 0, 0, 0.0 },
        "yellow" => new[] { 0.5, 0.5, 0, 0, 0.5, 0.5, 0, 0, 0.25, 0.25, 0.5, 0, 0, 0, 0, 1, 0, 0, 0, 0.0 },
        "green" => new[] { 0.5, 0.25, 0, 0, 0, 1, 0, 0, 0, 0.25, 0.5, 0, 0, 0, 0, 1, 0, 0, 0, 0.0 },
        "teal" => new[] { 0.25, 0.25, 0.25, 0, 0, 0.5, 0.5, 0, 0, 0.5, 0.5, 0, 0, 0, 0, 1, 0, 0, 0, 0.0 },
        "blue" => new[] { 0.25, 0, 0.25, 0, 0, 0.5, 0.25, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0.0 },
        "purple" => new[] { 0.5, 0, 0.5, 0, 0.25, 0.5, 0.25, 0, 0.5, 0, 0.5, 0, 0, 0, 0, 1, 0, 0, 0, 0.0 },
        _ => null,
    };

    // tg: MODpaint.jsx:50
    private static string DisplayText(string prefix) => prefix switch
    {
        "r" => "Red",
        "g" => "Green",
        "b" => "Blue",
        _ => null,
    };

    private static readonly string[] Presets = { "red", "yellow", "green", "teal", "blue", "purple" };
    private static readonly string[] Prefixes = { "r", "g", "b" };

    private static UiValue MatrixValue(IEnumerable<double> matrix) => UiValue.ListOf(matrix.Select(v => UiValue.Of(v)));

    // tg: MODpaint.jsx:61
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string mapRef = data.Str("mapRef");
        double[] currentColor = data.Arr("currentColor").Select(v => v.AsNum()).ToArray();
        double Entry(int i) => i < currentColor.Length ? currentColor[i] : 0;

        var rows = new UiNode(UiKind.StackItem) { Key = "channels", Width = "30%" };
        for (int row = 0; row < 3; row++)
        {
            var section = new UiNode(UiKind.Section) { Key = "row:" + row, Title = DisplayText(Prefixes[row]) + " turns to:" };
            for (int col = 0; col < 3; col++)
            {
                int index = row * 4 + col;
                section.With(new UiNode(UiKind.Stack) { Key = "line:" + index }.With(
                    new UiNode(UiKind.StackItem) { Width = "30%" }.With(
                        new UiNode(UiKind.Box) { Color = "label", Text = DisplayText(Prefixes[col]) + ":" }),
                    new UiNode(UiKind.StackItem) { Width = "70%" }.With(new UiNode(UiKind.Slider)
                    {
                        Key = "slider:" + index,
                        TextAlign = "right",
                        Value = Entry(index) * 100,
                        MinValue = 0,
                        MaxValue = 125,
                        Step = 1,
                        StepPixelSize = 0.75,
                        Format = value => TguiFormat.JsNumber(value) + "%",
                        OnChange = value =>
                        {
                            double[] retColor = (double[])currentColor.Clone();
                            if (index < retColor.Length)
                                retColor[index] = value / 100;
                            c.Act("transition_color", ("color", MatrixValue(retColor)));
                        },
                    })));
            }
            rows.With(section);
        }

        var presets = new UiNode(UiKind.Box) { Key = "presets_row", TextAlign = "center" };
        foreach (string preset in Presets)
        {
            string name = preset;
            presets.With(new UiNode(UiKind.Button)
            {
                Key = "preset:" + name,
                Height = "50px",
                Width = "50px",
                Color = name,
                Tooltip = char.ToUpperInvariant(name[0]) + name[1..],
                OnClick = () => c.Act("transition_color", ("color", MatrixValue(ColorToMatrix(name)))),
            });
        }

        // tg: MODpaint.jsx:65  <Window width={600} height={365}>
        return c.Window(600, 365, null, new UiNode(UiKind.Stack) { Key = "content", Fill = true }.With(
            rows,
            new UiNode(UiKind.StackItem) { Key = "controls", Width = "25%" }.With(
                new UiNode(UiKind.Section) { Key = "presets", Height = "70%", Title = "Presets" }.With(presets),
                new UiNode(UiKind.Section) { Key = "actions", TextAlign = "center", FontSize = "28px" }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "help",
                        Height = "50px",
                        Width = "50px",
                        Icon = "question",
                        Color = "average",
                        Tooltip = "This is a color matrix. Think of it as editing the image in 3 layers, red, green, and blue, rather than editing the final image like with RGB.",
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "confirm",
                        Height = "50px",
                        Width = "50px",
                        Icon = "check",
                        Color = "good",
                        Tooltip = "Confirm changes!",
                        OnClick = () => c.Act("confirm"),
                    })),
            new UiNode(UiKind.StackItem) { Key = "preview_item", Width = "45%" }.With(
                new UiNode(UiKind.Section) { Key = "preview", Fill = true, Title = "Preview" }.With(new UiNode(UiKind.ByondUi)
                {
                    Key = "map:" + mapRef,
                    MapRef = mapRef,
                    MapPicture = true,
                    Width = "100%",
                    Height = "230px",
                }))));
    }
}
