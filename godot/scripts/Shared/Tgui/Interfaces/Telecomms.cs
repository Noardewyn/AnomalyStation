using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Telecomms.jsx (M9 slice D: the telecomms
// machines' multitool window). Engine-free (compiled into the tests too). The Input's placeholder is its tooltip (as in AtmosHandbook).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Telecomms
{
    private sealed record Channel(double Freq, string Name, string Color);

    // tg: tgui/packages/tgui/interfaces/Telecomms.jsx:17
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        var channels = (data.Get("channels")?.Items ?? Array.Empty<UiValue>())
            .Select(ch => new Channel(ch.Num("freq"), ch.Str("name"), ch.Str("color"))).ToList();
        string type = data.Str("type");
        double minfreq = data.Num("minfreq");
        double maxfreq = data.Num("maxfreq");
        double frequency = data.Num("frequency");
        bool multitool = data.Truthy("multitool");
        UiValue multibuffValue = data.Get("multibuff");
        bool multibuff = multibuffValue?.Truthy ?? false;
        bool toggled = data.Truthy("toggled");
        string id = data.Str("id");
        string network = data.Str("network");
        bool prefab = data.Truthy("prefab");
        double changefrequency = data.Num("changefrequency");
        bool broadcasting = data.Truthy("broadcasting");
        bool receiving = data.Truthy("receiving");
        IReadOnlyList<UiValue> frequencyinfos = data.Get("frequencyinfos")?.Items ?? Array.Empty<UiValue>();
        IReadOnlyList<UiValue> linked = data.Get("linked")?.Items ?? Array.Empty<UiValue>();
        IReadOnlyList<UiValue> frequencies = data.Get("frequencies")?.Items ?? Array.Empty<UiValue>();

        UiNode ChannelTag(string key, double freq)
        {
            Channel ch = channels.FirstOrDefault(x => x.Freq == freq);
            return ch == null ? null : new UiNode(UiKind.Box) { Key = key, Color = ch.Color, Text = "[" + ch.Name + "]" };
        }

        var content = new List<UiNode>();
        if (!multitool)
            content.Add(new UiNode(UiKind.NoticeBox) { Key = "nomultitool", Text = "Use a multitool to make changes." });
        content.Add(new UiNode(UiKind.Section) { Title = "Settings" }.With(
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem)
                {
                    Label = "Power",
                    Buttons = new UiNode(UiKind.Button) { Key = "toggle", Icon = toggled ? "power-off" : "times", Text = toggled ? "On" : "Off", Color = toggled ? "good" : "bad", Disabled = !multitool, OnClick = () => c.Act("toggle") },
                },
                new UiNode(UiKind.LabeledItem)
                {
                    Label = "Identification String",
                    Buttons = new UiNode(UiKind.Input) { Key = "id", Width = 13.0, TextValue = id, OnCommit = v => c.Act("id", ("value", v)) },
                },
                new UiNode(UiKind.LabeledItem)
                {
                    Label = "Network",
                    Buttons = new UiNode(UiKind.Input) { Key = "network", Width = 10.0, TextValue = network, Tooltip = "tcommsat", OnCommit = v => c.Act("network", ("value", v)) },
                },
                new UiNode(UiKind.LabeledItem)
                {
                    Label = "Prefabrication",
                    Buttons = new UiNode(UiKind.Button) { Key = "prefab", Icon = prefab ? "check" : "times", Color = prefab ? "good" : "bad", Text = prefab ? "True" : "False", Disabled = true },
                })));

        if (toggled && multitool)
        {
            if (type == "bus")
            {
                content.Add(new UiNode(UiKind.Section) { Title = "Bus" }.With(
                    new UiNode(UiKind.Table).With(new UiNode(UiKind.TableRow).With(
                        new UiNode(UiKind.TableCell) { Text = "Change Frequency:" },
                        new UiNode(UiKind.TableCell).With(ChannelTag("changetag", changefrequency)),
                        new UiNode(UiKind.NumberInput) { Key = "change_freq", Unit = "kHz", Step = 0.2, StepPixelSize = 10, AllowFloats = true, MinValue = minfreq / 10, MaxValue = maxfreq / 10, Value = changefrequency / 10, OnChange = v => c.Act("change_freq", ("value", v)) },
                        new UiNode(UiKind.Button) { Key = "clear_freq", Icon = "times", Color = "bad", Disabled = changefrequency == 0, OnClick = () => c.Act("change_freq", ("value", 10001)) }))));
            }
            if (type == "relay")
            {
                content.Add(new UiNode(UiKind.Section) { Title = "Relay" }.With(
                    new UiNode(UiKind.Button) { Key = "receive", Text = "Receiving", Icon = receiving ? "volume-up" : "volume-mute", Color = receiving ? "" : "bad", OnClick = () => c.Act("receive") },
                    new UiNode(UiKind.Button) { Key = "broadcast", Text = "Broadcasting", Icon = broadcasting ? "microphone" : "microphone-slash", Color = broadcasting ? "" : "bad", OnClick = () => c.Act("broadcast") }));
            }
            content.Add(new UiNode(UiKind.Section) { Title = "Linked Network Entities" }.With(
                new UiNode(UiKind.Table).With(linked.Select(entry => new UiNode(UiKind.TableRow) { Key = "link:" + entry.Str("id"), Candystripe = true }.With(
                    new UiNode(UiKind.TableCell) { Bold = true, Text = TguiFormat.JsNumber(entry.Num("index")) + ". " + entry.Str("id") + " (" + entry.Str("name") + ")" },
                    multitool ? new UiNode(UiKind.Button) { Key = "unlink", Icon = "times", Color = "bad", Disabled = !multitool, OnClick = () => c.Act("unlink", ("value", entry.Num("index"))) } : null)))));

            var rows = frequencies.Select(entry => new UiNode(UiKind.TableRow) { Key = "freq:" + TguiFormat.JsNumber(entry.Number), Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Bold = true, Text = TguiFormat.JsNumber(entry.Number / 10) + " kHz" },
                new UiNode(UiKind.TableCell).With(ChannelTag("tag", entry.Number)),
                new UiNode(UiKind.TableCell),
                multitool ? new UiNode(UiKind.Button) { Key = "delete", Icon = "times", Color = "bad", Disabled = !multitool, OnClick = () => c.Act("delete", ("value", entry.Number)) } : null)).ToList();
            if (multitool)
            {
                rows.Add(new UiNode(UiKind.TableRow) { Key = "addfreq", Candystripe = true }.With(
                    new UiNode(UiKind.TableCell) { Text = "Add Frequency" },
                    new UiNode(UiKind.TableCell).With(ChannelTag("addtag", frequency)),
                    new UiNode(UiKind.TableCell).With(new UiNode(UiKind.NumberInput) { Key = "tempfreq", Unit = "kHz", Step = 0.2, StepPixelSize = 10, AllowFloats = true, MinValue = minfreq / 10, MaxValue = maxfreq / 10, Value = frequency / 10, OnChange = v => c.Act("tempfreq", ("value", v)) }),
                    new UiNode(UiKind.Button) { Key = "freq", Icon = "plus", Disabled = !multitool, OnClick = () => c.Act("freq") }));
            }
            content.Add(new UiNode(UiKind.Section) { Title = "Filtered Frequencies" }.With(new UiNode(UiKind.Table).With(rows)));

            if (type == "server")
            {
                content.Add(new UiNode(UiKind.Section)
                {
                    Title = "Frequencies Settings",
                    Buttons = new UiNode(UiKind.Button) { Key = "add_freq_info", Icon = "plus", Color = "good", OnClick = () => c.Act("add_freq_info") },
                }.With(new UiNode(UiKind.Table).With(frequencyinfos.Select(freq => new UiNode(UiKind.TableRow) { Key = "info:" + freq.Str("frequency"), Candystripe = true }.With(
                    new UiNode(UiKind.Stack) { Justify = "space-between", Align = "center" }.With(
                        new UiNode(UiKind.StackItem) { Bold = true, Text = TguiFormat.JsNumber(freq.Num("frequency") / 10) + " kHz" },
                        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "name", Color = freq.Str("color"), Text = "[" + freq.Str("name") + "]" }),
                        new UiNode(UiKind.StackItem).With(
                            multitool ? new UiNode(UiKind.Button) { Key = "modify", Icon = "wrench", Color = "good", Disabled = !multitool, OnClick = () => c.Act("modify_freq_info", ("freq", freq.Str("frequency"))) } : null,
                            multitool ? new UiNode(UiKind.Button) { Key = "deleteinfo", Icon = "times", Color = "bad", Disabled = !multitool, OnClick = () => c.Act("delete_freq_info", ("freq", freq.Str("frequency"))) } : null)))))));
            }

            if (multitool)
            {
                content.Add(new UiNode(UiKind.Section) { Title = "Multitool" }.With(
                    multibuff ? new UiNode(UiKind.Box) { Key = "buffer", Bold = true, Text = "Current Buffer: " + (multibuffValue.Kind == UiValueKind.String ? multibuffValue.Text : multibuffValue.ToJson()) } : null,
                    new UiNode(UiKind.LabeledControls).With(
                        new UiNode(UiKind.Button) { Key = "addmachine", Icon = "plus", Text = "Add Machine", Disabled = !multitool, OnClick = () => c.Act("buffer") },
                        new UiNode(UiKind.Button) { Key = "link", Icon = "link", Text = "Link", Disabled = !multibuff, OnClick = () => c.Act("link") },
                        new UiNode(UiKind.Button) { Key = "flush", Icon = "times", Color = "bad", Text = "Flush", Disabled = !multibuff, OnClick = () => c.Act("flush") })));
            }
        }

        // <Window.Content scrollable>: one growing, scrolled item
        return c.Window(400, 600, id, new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Grow = 1, Scrollable = true }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(content))));
    }
}
