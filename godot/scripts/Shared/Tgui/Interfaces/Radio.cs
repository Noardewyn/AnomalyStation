using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Radio.tsx and the RADIO_CHANNELS of
// tgui/packages/tgui/constants.ts (M9 slice D). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Radio
{
    // tg: tgui/packages/tgui/constants.ts:90  RADIO_CHANNELS
    public static readonly IReadOnlyList<(string Name, int Freq, string Color)> RadioChannels = new (string, int, string)[]
    {
        ("Syndicate", 1213, "#8f4a4b"),
        ("Red Team", 1215, "#ff4444"),
        ("Blue Team", 1217, "#3434fd"),
        ("Green Team", 1219, "#34fd34"),
        ("Yellow Team", 1221, "#fdfd34"),
        ("CentCom", 1337, "#2681a5"),
        ("Supply", 1347, "#b88646"),
        ("Service", 1349, "#6ca729"),
        ("Science", 1351, "#c68cfa"),
        ("Command", 1353, "#fcdf03"),
        ("Medical", 1355, "#57b8f0"),
        ("Engineering", 1357, "#f37746"),
        ("Security", 1359, "#dd3535"),
        ("AI Private", 1447, "#d65d95"),
        ("Common", 1459, "#1ecc43"),
    };

    // tg: tgui/packages/tgui/interfaces/Radio.tsx:30
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool freqlock = data.Truthy("freqlock");
        double frequency = data.Num("frequency");
        double minFrequency = data.Num("minFrequency");
        double maxFrequency = data.Num("maxFrequency");
        bool listening = data.Truthy("listening");
        bool broadcasting = data.Truthy("broadcasting");
        bool command = data.Truthy("command");
        bool useCommand = data.Truthy("useCommand");
        bool subspace = data.Truthy("subspace");
        bool subspaceSwitchable = data.Truthy("subspaceSwitchable");
        var tuned = RadioChannels.FirstOrDefault(ch => ch.Freq == frequency);
        var channels = (data.Get("channels")?.Fields ?? Array.Empty<KeyValuePair<string, UiValue>>())
            .Select(f => (Name: f.Key, Status: f.Value.Truthy)).ToList();
        // Calculate window height
        double height = 105;
        if (channels.Count > 0)
            height += channels.Count * 25 + 8;
        else if (subspace)
            height += 24;

        var frequencyRow = new UiNode(UiKind.LabeledItem) { Label = "Frequency" }.With(
            freqlock
                ? new UiNode(UiKind.Box) { Key = "freq", Color = "light-gray", Text = TguiFormat.ToFixed(frequency / 10, 1) + " kHz" }
                : new UiNode(UiKind.NumberInput)
                {
                    Key = "freq",
                    Unit = "kHz",
                    Step = 0.2,
                    StepPixelSize = 10,
                    AllowFloats = true,
                    MinValue = minFrequency / 10,
                    MaxValue = maxFrequency / 10,
                    Value = frequency / 10,
                    Format = v => TguiFormat.ToFixed(v, 1),
                    OnChange = v => c.Act("frequency", ("adjust", v - frequency / 10)),
                },
            tuned.Name != null ? new UiNode(UiKind.Box) { Key = "tuned", Color = tuned.Color, Text = "[" + tuned.Name + "]" } : null);

        var audio = new UiNode(UiKind.LabeledItem) { Label = "Audio" }.With(
            new UiNode(UiKind.Button) { Key = "listen", Width = "37px", TextAlign = "center", Icon = listening ? "volume-up" : "volume-mute", Selected = listening, OnClick = () => c.Act("listen") },
            new UiNode(UiKind.Button) { Key = "broadcast", Width = "37px", TextAlign = "center", Icon = broadcasting ? "microphone" : "microphone-slash", Selected = broadcasting, OnClick = () => c.Act("broadcast") },
            command ? new UiNode(UiKind.Button) { Key = "command", Icon = "bullhorn", Selected = useCommand, Text = "High volume " + (useCommand ? "ON" : "OFF"), OnClick = () => c.Act("command") } : null,
            subspaceSwitchable ? new UiNode(UiKind.Button) { Key = "subspace", Icon = "bullhorn", Selected = subspace, Text = "Subspace Tx " + (subspace ? "ON" : "OFF"), OnClick = () => c.Act("subspace") } : null);

        UiNode channelRow = null;
        if (subspace || channels.Count > 0)
        {
            channelRow = new UiNode(UiKind.LabeledItem) { Label = "Channels" }.With(
                channels.Count == 0 ? new UiNode(UiKind.Box) { Key = "nokeys", Color = "bad", Text = "No encryption keys installed." } : null,
                new UiNode(UiKind.Stack) { Vertical = true }.With(channels.Select(ch => new UiNode(UiKind.Box) { Key = "ch:" + ch.Name }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "channel",
                        Icon = ch.Status ? "check-square-o" : "square-o",
                        Selected = ch.Status,
                        Text = ch.Name,
                        OnClick = () => c.Act("channel", ("channel", ch.Name)),
                    },
                    !subspace && !freqlock ? new UiNode(UiKind.Button)
                    {
                        Key = "tune",
                        Icon = "walkie-talkie",
                        Text = "Tune",
                        Disabled = RadioChannels.FirstOrDefault(r => r.Name == ch.Name).Freq == frequency,
                        OnClick = () => c.Act("tune_to_channel", ("channel", ch.Name)),
                    } : null))));
        }

        return c.Window(330, height, null,
            new UiNode(UiKind.Section).With(
                new UiNode(UiKind.LabeledList).With(frequencyRow, audio, channelRow)));
    }
}
