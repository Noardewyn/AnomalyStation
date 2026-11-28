using Anomaly.Client.Shared;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Integration;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 6, slice E, through a real server: the human HUD no longer draws tg's crafting, language and navigate buttons, and the
/// G, L and M keys send the click of the button's screen entry, which the server still sends (<see cref="LiveHud.ButtonEntry"/>; the scene sends a
/// left click on it, as for tg's action buttons). The crafting click opens tg's crafting window, the language click tg's language menu; tg's
/// navigate button is not ported, so the server sends none and M does nothing. Nothing on the wire changed: the entries are tg's own.
/// </summary>
public class HudButtonKeysTests
{
    private const int Second = 20;

    private static bool WindowOpen(RealHeadlessWorld.Player p, string @interface) => p.Session.Replica.UiWindows.Values.Any(v => v.Interface == @interface);

    [Fact]
    public void The_menu_keys_click_tgs_buttons_the_hud_no_longer_draws()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5895, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        w.Run(2 * Second);
        int runtimes = w.World.RuntimeErrorCount;
        ReplicaWorld replica = alice.Session.Replica;
        // the human HUD: its inventory toggle on the screen, so the buttons are hidden (LiveHud.Placed)
        Assert.Contains(replica.Screen.Values, e => LiveHud.RoleOf(replica, e) == LiveHudRole.InventoryToggle);
        foreach ((PlayAction key, string @interface) in new[] { (PlayAction.CraftingMenu, "PersonalCrafting"), (PlayAction.LanguageMenu, "LanguageMenu") })
        {
            long entry = LiveHud.ButtonEntry(replica, key);
            Assert.True(entry != 0, $"{key}: the server sends tg's button");
            Assert.Equal(LiveHudRole.Hidden, LiveHud.Placed(LiveHud.RoleOf(replica, replica.Screen[entry]), humanHud: true, vitals: true));
            Assert.False(WindowOpen(alice, @interface));
            Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Screen, entry, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
            Assert.True(WindowOpen(alice, @interface), $"{key}: tg's {@interface} window");
        }
        // tg's navigate button (human.dm:9) is not ported yet, so the server sends none and M has nothing to click: it does nothing
        Assert.Equal(0, LiveHud.ButtonEntry(replica, PlayAction.Navigate));
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }
}
