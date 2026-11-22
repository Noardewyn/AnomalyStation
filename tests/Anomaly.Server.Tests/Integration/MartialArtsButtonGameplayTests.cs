using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Syndicate X3 (D316) through the real server on MetaStation: the Sleeping Carp scroll comes from the dev console into the hand and
/// is read with Z; the mind learns the art and its "move list" button (tg's swap_arts, actions.dmi "martial" over backgrounds.dmi
/// "bg_martial_arts") reaches the client's screen, the client's compositor draws it, and a click on it prints the move list.
/// </summary>
public class MartialArtsButtonGameplayTests
{
    private const int Second = 20;

    private readonly ITestOutputHelper _output;

    public MartialArtsButtonGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [MetaServerFact]
    public void The_client_draws_the_martial_arts_button_after_reading_the_carp_scroll()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 31, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var chat = new List<ServerText>();
        p.Client.ServerTextReceived += chat.Add;

        w.Host.EnqueueConsoleLine("teleport Assistant 80 130"); // a lit hall (the arrivals spot can be too dark to read)
        w.Run(5);
        w.Host.EnqueueConsoleLine("give Assistant /obj/item/book/granter/martial/carp");
        w.Run(5);
        Assert.IsType<obj_item_book_granter_martial_carp>(In(w, () => me.get_active_held_item()));
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(40 * Second); // the pages and the reading time

        var button = In(w, () => Dm.locate<datum_action_swap_arts>(me.actions)?.viewers[me.hud_used] as atom_movable_screen_movable_action_button);
        Assert.True(button != null, "no move list button: " + string.Join(" | ", chat.Select(c => c.Html)));
        ScreenEntry entry = Assert.Single(replica.Screen.Values, e => e.Id == button.dm_id);
        List<(string Icon, string State)> seen = CrewOutfitTests.Icons(replica, entry.Appearance).Select(i => (i.Icon, i.State)).ToList();
        _output.WriteLine($"the button: {entry.ScreenLoc}, {string.Join(", ", seen)}");
        Assert.Contains(("icons/hud/actions.dmi", "martial"), seen);
        Assert.Contains(("icons/mob/actions/backgrounds.dmi", "bg_martial_arts"), seen);
        if (Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot() is string iconRoot)
        {
            var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(iconRoot)));
            Assert.True(compositor.TryCompose(new HairGameplayTests.Tables(replica), entry.Appearance, 2, 0, out ComposedRaster picture, rootOffset: true));
            Assert.Contains(Enumerable.Range(0, picture.Image.Pixels.Length / 4), i => picture.Image.Pixels[i * 4 + 3] != 0);
        }

        // A click on it prints the carp's move list (swap_arts' Trigger -> get_style_help).
        chat.Clear();
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, button.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Contains(chat, c => c.Html.Contains("recall the teachings of the Sleeping Carp", StringComparison.Ordinal));
    }
}
