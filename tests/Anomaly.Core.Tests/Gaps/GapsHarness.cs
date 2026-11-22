using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package G1: what the tests of the IV drip, the safe and the loot panel share. A player clicks (the host's click), the window's
/// messages reach the client's tgui output as tg's JSON, and the window's acts go back as the client's messages (as M5's TguiTests).
/// </summary>
internal static class GapsHarness
{
    /// <summary>The text the players are told (chat lines and balloon alerts).</summary>
    internal sealed class Feed : IMessageSink
    {
        public List<(object Target, string Html)> Chat { get; } = new();

        void IMessageSink.Chat(object target, DmList message) => Chat.Add((target, Dm.str(message?["html"])));

        public void Output(object target, string text) { }

        public void Balloon(object viewer, object atom, string text) => Chat.Add((viewer, "balloon: " + text));

        public bool Any(string text) => Chat.Any(c => c.Html != null && c.Html.Contains(text, StringComparison.Ordinal));
    }

    public static Feed Listen(AudioRoom r)
    {
        var feed = new Feed();
        PresentationSinks.Of(r.World).Messages = feed;
        return feed;
    }

    public const int Second = 20;

    public static void Click(AudioRoom r, mob who, atom target, string modifiers = "")
    {
        r.Command(() => ClientSeam.Click(who.client, target, "icon-x=16;icon-y=16;left=1;button=left" + (modifiers.Length > 0 ? ";" + modifiers : "")));
        r.Step(2);
    }

    public static List<TguiOutput> Take(mob who)
    {
        var taken = who.client.dm_tgui_output.ToList();
        who.client.dm_tgui_output.Clear();
        return taken;
    }

    /// <summary>The last "update" message of a window: its payload (the window's id comes out).</summary>
    public static JsonElement LastUpdate(List<TguiOutput> outputs, out string windowId)
    {
        windowId = null;
        JsonElement found = default;
        foreach (TguiOutput o in outputs)
        {
            if (o.Kind != TguiOutputKind.Message)
                continue;
            using JsonDocument doc = JsonDocument.Parse(o.Json);
            if (doc.RootElement.GetProperty("type").GetString() != "update")
                continue;
            found = doc.RootElement.GetProperty("payload").Clone();
            windowId = o.WindowId;
        }
        Assert.True(found.ValueKind == JsonValueKind.Object, "no update message: " + string.Join(" | ", outputs.Select(o => o.Kind + " " + o.Json)));
        return found;
    }

    public static string InterfaceOf(JsonElement payload) => payload.GetProperty("config").GetProperty("interface").GetProperty("name").GetString();

    /// <summary>The window's act, as the client sends it ("act/NAME" with its params).</summary>
    public static void Act(AudioRoom r, mob who, string windowId, string action, params (string Key, object Value)[] args)
    {
        r.Command(() =>
        {
            DmList payload = Dm.list();
            foreach ((string key, object value) in args)
                payload[key] = value;
            ClientSeam.TguiMessage(who.client, windowId, "act/" + action, payload);
        });
        r.Step(4);
    }

    /// <summary>The newest data of the window after an act: tg answers a handled act with a new update.</summary>
    public static JsonElement Data(AudioRoom r, mob who, out string windowId, int steps = 4)
    {
        r.Step(steps);
        return LastUpdate(Take(who), out windowId).GetProperty("data");
    }

    public static T Place<T>(AudioRoom r, int x, int y) where T : atom
    {
        T thing = r.In(() => Dm.@new<T>(locate(x, y, 1)));
        r.Step(Second);
        return thing;
    }
}
