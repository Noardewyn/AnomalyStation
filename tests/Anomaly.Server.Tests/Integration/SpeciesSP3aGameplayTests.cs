using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps SP3a (D349): the skeleton, the zombie and the golem through the real server. The all-species option offers them in Character
/// Setup; another player sees a skeleton's and a golem's bone and stone limbs on his appearance, and a zombified player's claws and
/// the golem's buff come from the status effects through the real tick.
/// </summary>
public class SpeciesSP3aGameplayTests(ITestOutputHelper output)
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
    public void Character_setup_offers_them_and_another_player_sees_a_skeletons_and_a_golems_limbs_and_a_zombies_claws()
    {
        var config = new ServerConfig { Seed = 9911, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f, AllSpeciesRoundstart = true };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9911, TimeoutSteps = 600 }, config, strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        RealHeadlessWorld.Player dave = w.Join("Dave");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        var daveMob = (mob_living_carbon_human)w.MobOf(dave);
        w.Run(10);
        using (w.World.Enter())
        {
            List<string> ids = Dm.each(GlobalProcs.get_selectable_species()).Select(Dm.str).ToList();
            output.WriteLine("selectable: " + string.Join(", ", ids));
            Assert.Contains("skeleton", ids);
            Assert.Contains("zombie", ids);
            Assert.Contains("golem", ids);
            bobMob.set_species(typeof(datum_species_skeleton));
            carolMob.set_species(typeof(datum_species_golem));
            Waiting.NoSleep(daveMob.apply_status_effect(typeof(datum_status_effect_zombie)));
        }
        w.Run(30);
        ReplicaWorld replica = alice.Session.Replica;
        List<(string Resource, string State)> skeleton = Sprites(replica, replica.Entities.Single(e => e.Id == bobMob.dm_id).State.Appearance);
        List<(string Resource, string State)> golem = Sprites(replica, replica.Entities.Single(e => e.Id == carolMob.dm_id).State.Appearance);
        output.WriteLine("skeleton: " + string.Join(", ", skeleton.Select(s => s.Resource + ":" + s.State).Distinct()));
        output.WriteLine("golem: " + string.Join(", ", golem.Select(s => s.Resource + ":" + s.State).Distinct()));
        Assert.Contains(skeleton, s => s.Resource.Contains("skeleton", StringComparison.Ordinal) || s.State.Contains("skeleton", StringComparison.Ordinal));
        Assert.Contains(golem, s => s.Resource.Contains("golem", StringComparison.Ordinal));
        using (w.World.Enter())
        {
            Assert.All(Dm.each_cast<obj_item>(daveMob.held_items), h => Assert.IsType<obj_item_mutant_hand_zombie>(h));
            Assert.NotNull(daveMob.get_organ_slot(Defines.ORGAN_SLOT_ZOMBIE));
        }
    }
}
