using Anomaly.Client.Shared.Icons;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M7 radiation slice (D163) through the real server on MetaStation: the dev console's <c>irradiate X Y 1 8</c> (tg's
/// <c>radiation_pulse(turf, 8, RAD_MEDIUM_INSULATION)</c>) at the feet of three players next to a station wall. Alice, bare, is
/// irradiated within a few pulses (tg's chance at the epicenter: 93%): her client sees tg's green outline on her
/// (<c>add_filter("rad_glow", ...)</c>, irradiated.dm:156) and the Irradiated alert on her HUD; the geiger counter in her hand
/// clicks (its loop's <c>sound/items/geiger/*.ogg</c> reach her client). Bob, in the radiation suit and hood, and Carol, behind
/// the wall (its 0.7 is not above the threshold), are not. No runtime.
/// </summary>
public class RadiationGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const int AroundX = 46, AroundY = 122; // the arrivals corridor
    private const uint GlowRgba = 0x39FF1430; // "#39ff1430"

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static IEnumerable<AppearanceEntry> Tree(ReplicaWorld replica, TableRef root, int depth = 0)
    {
        if (depth > 12 || !replica.TryGetAppearance(root, out AppearanceEntry e))
            yield break;
        yield return e;
        foreach (TableRef child in e.Underlays.Concat(e.Overlays))
            foreach (AppearanceEntry below in Tree(replica, child, depth + 1))
                yield return below;
    }

    private static bool Clear(turf t) => t is turf_open_floor && !Dm.truthy(t.density) && !Dm.each<atom_movable>(t).Any(a => Dm.truthy(a.density));

    private static bool Glows(ReplicaWorld replica, mob m)
        => replica.TryGet(m.dm_id, out ReplicaEntity e) && replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry root)
            && root.Filters?.Any(f => f.Kind == FilterKind.Outline && f.Rgba == GlowRgba) == true;

    private static void Wear(RealHeadlessWorld w, mob_living_carbon_human who, obj_item item, float slot)
    {
        In(w, () =>
        {
            obj_item worn = who.get_item_by_slot(slot) as obj_item;
            if (worn != null)
                Waiting.NoSleep(who.dropItemToGround(worn));
            Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(item, slot))), $"{item} worn");
        });
    }

    [MetaServerFact]
    public void An_irradiate_pulse_glows_and_alerts_a_bare_player_its_geiger_clicks_and_a_suited_or_walled_player_is_spared()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 79, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        var a = (mob_living_carbon_human)w.MobOf(alice);
        var b = (mob_living_carbon_human)w.MobOf(bob);
        var c = (mob_living_carbon_human)w.MobOf(carol);
        ReplicaWorld replica = alice.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);

        // the spot: a plain station wall nearest the arrivals corridor with clear floor on its east (the pulse) and west (Carol)
        turf wall = In(w, () => Dm.each<turf_closed_wall>(Dm.world)
            .Where(t => t.GetType() == typeof(turf_closed_wall) && Dm.eq(t.z, 1))
            .Where(t => Clear(locate(Dm.num(t.x) + 1, Dm.num(t.y), 1)) && Clear(locate(Dm.num(t.x) - 1, Dm.num(t.y), 1)))
            .OrderBy(t => Math.Abs(Dm.num(t.x) - AroundX) + Math.Abs(Dm.num(t.y) - AroundY))
            .First());
        int wx = (int)In(w, () => Dm.num(wall.x)), wy = (int)In(w, () => Dm.num(wall.y));
        int px = wx + 1, py = wy;
        output.WriteLine($"the wall at {wx},{wy}: the pulse at {px},{py}, Carol at {wx - 1},{wy}");
        In(w, () =>
        {
            Waiting.NoSleep(a.forceMove(locate(px, py, 1)));
            Waiting.NoSleep(b.forceMove(locate(px, py, 1)));
            Waiting.NoSleep(c.forceMove(locate(wx - 1, wy, 1)));
        });
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // Bob: tg's radiation suit and hood (utility.dm:189-239, the radiation_protected_clothing element)
        Wear(w, b, In(w, () => Dm.@new<obj_item_clothing_suit_utility_radiation>(b.loc)), ITEM_SLOT_OCLOTHING);
        Wear(w, b, In(w, () => Dm.@new<obj_item_clothing_head_utility_radiation>(b.loc)), ITEM_SLOT_HEAD);
        // Alice: a geiger counter in her hand, switched on (geiger_counter.dm:59)
        var geiger = In(w, () => Dm.@new<obj_item_geiger_counter>(a.loc));
        In(w, () =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(a.put_in_active_hand(geiger))));
            Waiting.NoSleep(geiger.attack_self(a));
        });
        w.Run(Second);
        Assert.False(Glows(replica, a), "no glow before the pulse");
        heard.Clear();

        // the pulses: the dev console's irradiate, tg's radiation_pulse; SSradiation fires every 0.5 s
        bool clicked = false;
        for (int i = 0; i < 6 && !In(w, () => GlobalProcs.HAS_TRAIT(a, TRAIT_IRRADIATED)); i++)
        {
            w.Host.EnqueueConsoleLine($"irradiate {px} {py} 1 8");
            w.Run(Second);
            clicked |= heard.Any(e => e.File != null && e.File.StartsWith("sound/items/geiger/", StringComparison.Ordinal));
        }
        output.WriteLine($"heard: {string.Join(", ", heard.Where(e => e.File != null).Select(e => e.File).Distinct())}");

        // Alice: irradiated, 10 toxin damage, the glow and the alert seen by her client
        Assert.True(In(w, () => GlobalProcs.HAS_TRAIT(a, TRAIT_IRRADIATED)), "Alice is irradiated");
        Assert.True(In(w, () => Dm.num(a.get_tox_loss())) >= 10f);
        w.Run(3);
        Assert.True(Glows(replica, a), "Alice's client sees the green outline on her");
        Assert.True(Glows(bob.Session.Replica, a), "Bob's client sees it on her too");
        var alert = In(w, () => a.alerts[ALERT_IRRADIATED] as atom_movable_screen_alert_irradiated);
        Assert.NotNull(alert);
        Assert.True(replica.Screen.TryGetValue(alert.dm_id, out ScreenEntry shown), "the Irradiated alert is on Alice's HUD");
        Assert.Contains(Tree(replica, shown.Appearance), e => replica.ResolveString(e.IconState) == "irradiated");
        // the geiger counter clicked: its loop's sounds reached the client (geiger_sound.dm:66-87)
        Assert.True(clicked, "the held geiger counter's clicks reach Alice's client");
        Assert.StartsWith("geiger_on_", In(w, () => geiger.icon_state));

        // Bob in the radiation suit, Carol behind the wall: spared
        Assert.False(In(w, () => GlobalProcs.HAS_TRAIT(b, TRAIT_IRRADIATED)), "the radiation suit and hood protect Bob");
        Assert.False(In(w, () => GlobalProcs.HAS_TRAIT(c, TRAIT_IRRADIATED)), "the wall stops the pulse before Carol");
        Assert.Equal(0f, In(w, () => Dm.num(b.get_tox_loss())));
        Assert.Equal(0f, In(w, () => Dm.num(c.get_tox_loss())));
        Assert.False(Glows(replica, b));
        Assert.False(Glows(replica, c));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
