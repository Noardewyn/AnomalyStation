using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps SP3b (D349): the abductor, the vampire and the nightmare through the real server. The all-species option offers them in Character
/// Setup; another player sees the abductor's grey limbs on his appearance; the vampire loses blood through the real tick; the nightmare's
/// heart puts the light eater in his hand.
/// </summary>
public class SpeciesSP3bGameplayTests(ITestOutputHelper output)
{
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
    public void Character_setup_offers_them_and_another_player_sees_an_abductors_limbs_a_vampire_bleeds_and_a_nightmare_has_the_blade()
    {
        var config = new ServerConfig { Seed = 9961, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f, AllSpeciesRoundstart = true };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9961, TimeoutSteps = 600 }, config, strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        RealHeadlessWorld.Player dave = w.Join("Dave");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        var daveMob = (mob_living_carbon_human)w.MobOf(dave);
        w.Run(10);
        float vampireBefore;
        using (w.World.Enter())
        {
            List<string> ids = Dm.each(GlobalProcs.get_selectable_species()).Select(Dm.str).ToList();
            output.WriteLine("selectable: " + string.Join(", ", ids));
            Assert.Contains("abductor", ids);
            Assert.Contains("vampire", ids);
            Assert.Contains("dullahan", ids);
            bobMob.set_species(typeof(datum_species_abductor));
            carolMob.set_species(typeof(datum_species_human_vampire));
            daveMob.set_species(typeof(datum_species_shadow_nightmare));
            vampireBefore = Dm.num(carolMob.get_blood_volume());
        }
        w.Run(60);
        ReplicaWorld replica = alice.Session.Replica;
        List<(string Resource, string State)> abductor = Sprites(replica, replica.Entities.Single(e => e.Id == bobMob.dm_id).State.Appearance);
        output.WriteLine("abductor: " + string.Join(", ", abductor.Select(s => s.Resource + ":" + s.State).Distinct()));
        Assert.Contains(abductor, s => s.Resource.Contains("abductor", StringComparison.Ordinal) || s.State.Contains("abductor", StringComparison.Ordinal));
        using (w.World.Enter())
        {
            Assert.True(Dm.num(carolMob.get_blood_volume()) < vampireBefore);
            var heart = Assert.IsType<obj_item_organ_heart_nightmare>(daveMob.get_organ_slot(Defines.ORGAN_SLOT_HEART));
            Assert.Contains(heart.blade_item, Dm.each_cast<obj_item>(daveMob.held_items).ToList());
        }
    }
}
