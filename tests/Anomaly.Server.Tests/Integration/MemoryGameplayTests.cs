using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps package G4 (datums/memory, the memory panel) on MetaStation through the real server and a client's own stack, in the corridor
/// east of the wall at (42,155): a player walks east onto a lubed tile and slips; the HUD's memories button opens tg's MemoryPanel window,
/// which lists the slip by tg's name with its quality's star.
/// </summary>
public class MemoryGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const int X = 43, Y = 155;
    private const double East = -Math.PI / 2; // Godot's yaw: 0 looks north, +90 degrees west

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static ClickTarget Screen(long id) => new(TargetKind.Screen, id, 0, 0, 0, 16, 16);

    [MetaServerFact]
    public void A_player_slips_on_a_lubed_floor_and_finds_the_memory_in_the_panel_the_HUD_button_opens()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 94, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        MetaServer.ClearAuxBase(w.World); // the test works in the empty construction zone
        RealHeadlessWorld.Player p = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // the lubed tile two steps east; the player walks onto it and falls
        In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(Builtins.locate(X, Y, 1)));
            Waiting.NoSleep(((turf_open)Builtins.locate(X + 2, Y, 1)).MakeSlippery(TURF_WET_LUBE, 100));
            return true;
        });
        w.Run(Second);
        p.Yaw = East;
        p.Keys = MoveKeys.Forward;
        for (int i = 0; i < 4 * Second && In(w, () => me.mind.memories[typeof(datum_memory_was_slipped)]) == null; i++)
            w.Run(1);
        p.Keys = MoveKeys.None;
        w.Run(Second);
        string remembered = In(w, () => Dm.str((me.mind.memories[typeof(datum_memory_was_slipped)] as datum_memory)?.name));
        output.WriteLine($"at ({In(w, () => me.x)},{In(w, () => me.y)}), the job {In(w, () => Dm.str(me.mind.assigned_role.title))}: \"{remembered}\"");
        Assert.NotNull(remembered);
        Assert.StartsWith("The slipping of the ", remembered, StringComparison.Ordinal);

        // the memories button of the HUD (tg's icon state, which the client draws at tg's screen_loc) opens the panel
        var screenButton = In(w, () => (atom)me.hud_used.screen_objects[HUD_MOB_MEMORIES]);
        long button = screenButton.dm_id;
        Assert.Equal("memories", In(w, () => Dm.str(screenButton.icon_state)));
        Assert.Equal(ui_human_memories_menu, replica.Screen.Values.Single(e => e.Id == button).ScreenLoc);
        Assert.True(p.Session.Click(Screen(button), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("MemoryPanel", window.Interface);
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        UiNode content = windows.Get(window.Window).Content;
        Assert.Equal("Memory Panel", windows.Get(window.Window).Context.WindowTitle);
        UiNode row = content.Walk().First(n => n.Key?.StartsWith("name:") == true && n.Text == remembered);
        string index = row.Key["name:".Length..];
        UiNode star = content.Walk().Single(n => n.Key == "quality:" + index);
        Assert.Equal(("star", "linear-gradient(to right, #964B30, #D68B60, #B66B30, #D68B60, #964B30);"), (star.Icon, star.Background)); // tg's "meh"

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
