using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The languages on MetaStation (gaps L2): a crewman's HUD shows tg's language button (the talk wheel), a click on it opens his
/// language menu with Galactic Common, which he speaks, understands and has as his default.
/// </summary>
public class LanguageGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f) { using (w.World.Enter()) return f(); }

    [MetaServerFact]
    public void The_language_button_on_the_hud_opens_the_language_menu()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 66, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        w.Run(Second);

        long button = In(w, () => Dm.each<atom_movable_screen_language_menu>(me.client.screen).Single().dm_id);
        ScreenEntry entry = alice.Session.Replica.Screen.Values.Single(e => e.Id == button);
        Assert.Contains("talk_wheel", CrewOutfitTests.Icons(alice.Session.Replica, entry.Appearance).Select(i => i.State));

        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Screen, button, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        var windows = new TguiWindows { SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters), SendClose = window => alice.Session.UiClose(window) };
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        TguiWindow window = windows.Open.First(x => x.Interface == "LanguageMenu");
        UiNode common = window.Content.Walk().First(n => n.Key == "language:Galactic Common");
        Assert.True(common.Walk().First(n => n.Key == "default").Checkbox);
        Assert.Equal("good", common.Walk().First(n => n.Key == "speakIcon").Color);
    }
}
