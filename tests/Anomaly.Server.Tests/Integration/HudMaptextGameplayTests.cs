using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Maptext;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Protocol version 18 (HUD maptext, D142) through the real server on MetaStation: a player in an EVA suit with its cell hovers the spacesuit
/// display and the client receives tg's maptext (screen_objects.dm:510, the charge percentage in a right-aligned div) in the display's private
/// appearance, with the box; the unhover (protocol 14, MouseExited) takes it away again. The text parses to the percentage.
/// </summary>
public class HudMaptextGameplayTests
{
    private const int Second = 20;

    private readonly ITestOutputHelper _output;

    public HudMaptextGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private sealed class Source : IAppearanceSource
    {
        private readonly ReplicaWorld _replica;

        public Source(ReplicaWorld replica) => _replica = replica;

        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => _replica.TryGetAppearance(reference, out entry);

        public string ResolveString(TableRef reference) => _replica.ResolveString(reference);

        public string ResolveResource(TableRef reference) => _replica.ResolveResource(reference);
    }

    private static MaptextItem[] TextsOf(ReplicaWorld replica, long id)
        => new MaptextCollector().Collect(new Source(replica), replica.Screen[id].Appearance);

    [MetaServerFact]
    public void The_space_suit_display_shows_its_charge_as_maptext_on_hover_and_loses_it_on_unhover()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 11, TimeoutSteps = 4000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;

        w.Host.EnqueueConsoleLine("give Assistant /obj/item/clothing/suit/space/eva");
        w.Run(5);
        Assert.NotNull(In(w, () => me.get_active_held_item()));
        p.Session.Keybinding(KeybindingCode.QuickEquip);
        w.Run(Second);
        var suit = In(w, () => me.wear_suit) as obj_item_clothing_suit_space;
        Assert.NotNull(suit);

        var display = In(w, () => me.hud_used.screen_objects[Defines.HUD_MOB_SPACESUIT] as atom_movable_screen_spacesuit);
        Assert.NotNull(display);
        Assert.Contains(replica.Screen.Values, e => e.Id == display.dm_id);
        Assert.Empty(TextsOf(replica, display.dm_id)); // no hover: no text (screen_objects.dm:512)

        Assert.True(p.Session.Hover(new ClickTarget(TargetKind.Screen, display.dm_id, 0, 0, 0, 16, 16), w.Now));
        w.Run(2 * Second);
        string sent = In(w, () => display.maptext);
        Assert.False(string.IsNullOrEmpty(sent));
        MaptextItem[] texts = TextsOf(replica, display.dm_id);
        _output.WriteLine("server maptext: " + sent + " | client: " + string.Join(" ; ", texts));
        MaptextItem item = Assert.Single(texts);
        Assert.Equal(sent, item.Text);
        Assert.Equal(32, item.Width); // the display keeps BYOND's default box
        Assert.Equal(32, item.Height);
        MaptextDocument doc = MaptextMarkup.Parse(item.Text);
        string shown = string.Concat(doc.Paragraphs.SelectMany(pg => pg.Runs).Select(r => r.Text));
        Assert.EndsWith("%", shown);
        Assert.True(float.TryParse(shown.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture, out float percent) && percent > 0f && percent <= 100f, shown);
        Assert.Equal(MaptextAlign.Right, doc.Paragraphs[0].Align);
        // the HUD's text stays the session's: no public entry carries it (version 74: the world's atoms' maptext does travel there)
        _output.WriteLine("public maptexts: " + string.Join(" ; ", replica.Appearances.Where(a => !a.Maptext.IsNone).Select(a => replica.ResolveString(a.Maptext)).Distinct().Take(8)));
        Assert.DoesNotContain(replica.Appearances, a => !a.Maptext.IsNone && replica.ResolveString(a.Maptext) == sent);
        Assert.Contains(replica.PrivateAppearances, a => !a.Maptext.IsNone);

        // A click on the display (it turns the thermal regulator on, screen_objects.dm:472) keeps the text while the pointer stays over it.
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, display.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        Assert.Equal(1f, In(w, () => suit.thermal_on));
        Assert.Single(TextsOf(replica, display.dm_id));

        // The pointer leaves to nothing: BYOND's MouseExited (protocol 14) clears the text.
        p.Session.Hover(ClickTarget.None, w.Now);
        w.Run(Second);
        p.Session.Hover(ClickTarget.None, w.Now);
        w.Run(Second);
        Assert.True(string.IsNullOrEmpty(In(w, () => display.maptext)));
        Assert.Empty(TextsOf(replica, display.dm_id));

        // The scripted client's order: the click and the hover of the same press, the click first.
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, display.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        Assert.True(p.Session.Hover(new ClickTarget(TargetKind.Screen, display.dm_id, 0, 0, 0, 16, 16), w.Now));
        w.Run(2 * Second);
        Assert.Single(TextsOf(replica, display.dm_id));
    }
}
