using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Holopad.jsx (silicons SI4, D326): the holopad's calls,
// the AI request and the holodisk player and recorder. The dialing modal's rotated phone icon is an icon.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Holopad
{
    // tg: Holopad.jsx:15
    public static UiNode Render(TguiContext c)
    {
        bool calling = c.Data.Truthy("calling");
        return c.Window(440, 245, null,
            calling
                ? new UiNode(UiKind.Modal) { Key = "dialing" }.With(
                    new UiNode(UiKind.Box) { Key = "dialing", Icon = "phone-alt", FontSize = "36px", Text = " Dialing..." },
                    new UiNode(UiKind.Box) { Key = "hangup", TextAlign = "center", FontSize = "24px" }.With(
                        new UiNode(UiKind.Button) { Key = "hang_up", Icon = "times", Text = "Hang Up", Color = "bad", OnClick = () => c.Act("hang_up") }))
                : null,
            Content(c));
    }

    // tg: Holopad.jsx:47
    private static UiNode Content(TguiContext c)
    {
        UiValue data = c.Data;
        bool onNetwork = data.Truthy("on_network"), onCooldown = data.Truthy("on_cooldown"), allowed = data.Truthy("allowed");
        bool disk = data.Truthy("disk"), diskRecord = data.Truthy("disk_record");
        bool replayMode = data.Truthy("replay_mode"), loopMode = data.Truthy("loop_mode"), recordMode = data.Truthy("record_mode");
        IReadOnlyList<UiValue> holoCalls = data.Get("holo_calls")?.Items ?? Array.Empty<UiValue>();

        var communicator = new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Label = "Communicator" }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "holocall",
                    Icon = "phone-alt",
                    Text = allowed ? "Connect To Holopad" : "Call Holopad",
                    Disabled = !onNetwork,
                    OnClick = () => c.Act("holocall", ("headcall", allowed ? 1 : 0)),
                }));
        foreach (UiValue call in holoCalls)
        {
            bool connected = call.Truthy("connected");
            string callRef = call.Str("ref");
            string caller = call.Str("caller");
            communicator.With(new UiNode(UiKind.LabeledItem) { Key = callRef, Label = connected ? "Current Call" : "Incoming Call" }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "call",
                    Icon = connected ? "phone-slash" : "phone-alt",
                    Text = connected ? "Disconnect call from " + caller : "Answer call from " + caller,
                    Color = connected ? "bad" : "good",
                    Disabled = !onNetwork,
                    OnClick = () => c.Act(connected ? "disconnectcall" : "connectcall", ("holopad", callRef)),
                }));
        }
        if (holoCalls.Any(call => !call.Truthy("connected")))
            communicator.With(new UiNode(UiKind.LabeledItem) { Key = "reject" }.With(
                new UiNode(UiKind.Button) { Key = "rejectall", Icon = "phone-slash", Text = "Reject incoming call(s)", Color = "bad", OnClick = () => c.Act("rejectall") }));

        return new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.Section)
            {
                Key = "holopad",
                Title = "Holopad",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "AIrequest",
                    Icon = "bell",
                    Text = onCooldown ? "AI's Presence Requested" : "Request AI's Presence",
                    Disabled = !onNetwork || onCooldown,
                    OnClick = () => c.Act("AIrequest"),
                },
            }.With(communicator),
            new UiNode(UiKind.Section)
            {
                Key = "holodisk",
                Title = "Holodisk",
                Buttons = new UiNode(UiKind.Button) { Key = "eject", Icon = "eject", Text = "Eject", Disabled = !disk || replayMode || recordMode, OnClick = () => c.Act("disk_eject") },
            }.With(!disk
                ? new UiNode(UiKind.NoticeBox) { Text = "No holodisk" }
                : new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Disk Player" }.With(
                        new UiNode(UiKind.Button) { Key = "replay", Icon = replayMode ? "pause" : "play", Text = replayMode ? "Stop" : "Replay", Selected = replayMode, Disabled = recordMode || !diskRecord, OnClick = () => c.Act("replay_mode") },
                        new UiNode(UiKind.Button) { Key = "loop", Icon = "sync", Text = loopMode ? "Looping" : "Loop", Selected = loopMode, Disabled = recordMode || !diskRecord, OnClick = () => c.Act("loop_mode") },
                        new UiNode(UiKind.Button) { Key = "offset", Icon = "exchange-alt", Text = "Change Offset", Disabled = !replayMode, OnClick = () => c.Act("offset") }),
                    new UiNode(UiKind.LabeledItem) { Label = "Recorder" }.With(
                        new UiNode(UiKind.Button) { Key = "record", Icon = recordMode ? "pause" : "video", Text = recordMode ? "End Recording" : "Record", Selected = recordMode, Disabled = (diskRecord && !recordMode) || replayMode, OnClick = () => c.Act("record_mode") },
                        new UiNode(UiKind.Button) { Key = "clear", Icon = "trash", Text = "Clear Recording", Color = "bad", Disabled = !diskRecord || replayMode || recordMode, OnClick = () => c.Act("record_clear") }))));
    }
}
