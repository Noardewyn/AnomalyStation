using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The radial's Climb in through the real server (DV639, DV640): a joined player hovers a machine, a disposal bin or an open closet next
/// to him, the server's hints carry Climb in, and the client's choice (tg's drag of his own mob onto it, or the closet's pick) puts him
/// inside.
/// </summary>
public class ClimbInGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static ClickTarget Entity(long id) => new(TargetKind.Entity, id, 0, 0, 0, 16, 16);

    // A player and a thing of the type on a free tile beside him (the area needs no power), with the radial's entry for it.
    private (RealHeadlessWorld W, RealHeadlessWorld.Player P, mob_living_carbon_human Me, T Thing, RadialEntry Entry) Beside<T>(ulong seed, Action<T> prepare) where T : atom_movable
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 2400 });
        RealHeadlessWorld.Player p = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(p);
        w.World.RuntimeErrorSink = e => output.WriteLine("RUNTIME: " + e);
        w.Run(10);
        T thing = In(w, () =>
        {
            (get_area(me) as area).requires_power = FALSE;
            turf free = new[] { EAST, WEST, NORTH, SOUTH }.Select(d => get_step(me, d) as turf)
                .First(t => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density)));
            return Dm.@new<T>(free);
        });
        w.Run(Second);
        In(w, () => { prepare(thing); return 0; });
        w.Run(Second);
        var hints = new List<ContextHintsMessage>();
        p.Client.ContextHintsReceived += hints.Add;
        p.Session.Rehover();
        p.Session.Hover(Entity(thing.dm_id), w.Now);
        w.Run(4);
        ContextHintsMessage answer = hints.LastOrDefault(h => h.Target == thing.dm_id);
        Assert.NotNull(answer);
        output.WriteLine(typeof(T).Name + ": " + string.Join(", ", answer.Hints.Select(h => h.Label + " " + h.Action)));
        RadialEntry entry = RadialModel.Build(answer, 0, true).Single(r => r.Label == "Climb in");
        return (w, p, me, thing, entry);
    }

    private static void DropSelf(RealHeadlessWorld w, RealHeadlessWorld.Player p, atom thing, int seconds)
    {
        Assert.True(p.Session.Drop(new DragRelease(new ClickTarget(TargetKind.Entity, p.Client.OwnEntity, 0, 0, 0, 16, 16), Entity(thing.dm_id), ClickButton.Left, ClickModifiers.None, 255)));
        w.Run(seconds * Second);
    }

    [Fact]
    public void A_player_climbs_into_a_dna_scanner_from_the_radial()
    {
        var (w, p, me, scanner, entry) = Beside<obj_machinery_dna_scannernew>(13751, s => Waiting.NoSleep(s.open_machine()));
        using (w)
        {
            Assert.Equal(RadialKind.DropSelf, entry.Kind);
            DropSelf(w, p, scanner, 1);
            Assert.Same(scanner, In(w, () => me.loc));
            Assert.Same(me, In(w, () => scanner.occupant));
        }
    }

    [Fact]
    public void A_player_climbs_into_a_disposal_bin_from_the_radial()
    {
        var (w, p, me, bin, entry) = Beside<obj_machinery_disposal_bin>(13752, _ => { });
        using (w)
        {
            Assert.Equal(RadialKind.DropSelf, entry.Kind);
            DropSelf(w, p, bin, 3); // stuff_mob_in's do_after
            Assert.Same(bin, In(w, () => me.loc));
        }
    }

    [Fact]
    public void A_player_climbs_into_an_open_closet_from_the_radial_and_it_shuts_behind_him()
    {
        var (w, p, me, closet, entry) = Beside<obj_structure_closet>(13753, c => Waiting.NoSleep(c.open()));
        using (w)
        {
            Assert.Equal(RadialKind.Extra, entry.Kind);
            Assert.True(p.Session.ContextPick(Entity(closet.dm_id), entry.Label));
            w.Run(Second);
            Assert.Same(closet, In(w, () => me.loc));
            Assert.False(In(w, () => Dm.truthy(closet.opened)), "the closet shut behind him");
        }
    }

    // Every (resource, icon state) of an appearance and its overlays and underlays, as the client walks them
    private static List<(string Resource, string State)> Sprites(ReplicaWorld replica, TableRef root)
    {
        var found = new List<(string, string)>();
        var seen = new HashSet<TableRef>();
        void Walk(TableRef r)
        {
            if (!seen.Add(r) || !replica.TryGetAppearance(r, out AppearanceEntry e))
                return;
            found.Add((replica.ResolveResource(e.Icon) ?? "", replica.ResolveString(e.IconState) ?? ""));
            foreach (TableRef o in e.Underlays)
                Walk(o);
            foreach (TableRef o in e.Overlays)
                Walk(o);
        }
        Walk(root);
        return found;
    }

    [Fact]
    public void Another_player_sees_a_joined_lizards_own_limbs()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 13754, TimeoutSteps = 600 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        w.Run(10);
        using (w.World.Enter())
        {
            bobMob.dna.features[Defines.FEATURE_MUTANT_COLOR] = "#3355cc";
            bobMob.set_species(typeof(datum_species_lizard));
        }
        w.Run(10);
        ReplicaEntity seen = alice.Session.Replica.Entities.Single(e => e.Id == bobMob.dm_id);
        List<(string Resource, string State)> sprites = Sprites(alice.Session.Replica, seen.State.Appearance);
        const string sheet = "icons/mob/human/species/lizard/bodyparts.dmi";
        foreach (string limb in new[] { "lizard_head", "lizard_chest", "lizard_l_arm", "lizard_r_arm" })
            Assert.Contains(sprites, s => s.Resource == sheet && s.State.StartsWith(limb, StringComparison.Ordinal));
        // the legs from the same sheet, plain or digitigrade by his preferences (cut to the leg's mask, the iconops recipe)
        foreach (string side in new[] { "l_leg", "r_leg" })
            Assert.Contains(sprites, s => s.Resource.Contains(sheet, StringComparison.Ordinal) && (s.State == "lizard_" + side || s.State == "digitigrade_" + side));
        Assert.DoesNotContain(sprites, s => s.State.StartsWith("human_l_arm", StringComparison.Ordinal) || s.State.StartsWith("human_r_leg", StringComparison.Ordinal));
    }
}
