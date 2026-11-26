using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Species package SP2b (D349) through the real server: the ethereal, podperson, mushroomperson and snailperson are seen by another
/// player as tg's bodypart overlays and worn shell, an ethereal glows and goes dark to an EMP, and Character Setup lists their features.
/// </summary>
public class SpeciesSP2bGameplayTests(ITestOutputHelper output)
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
    public void Another_player_sees_the_new_species_parts_and_an_ethereal_glows_and_goes_dark()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9861, TimeoutSteps = 600 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        RealHeadlessWorld.Player dave = w.Join("Dave");
        RealHeadlessWorld.Player erin = w.Join("Erin");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        var daveMob = (mob_living_carbon_human)w.MobOf(dave);
        var erinMob = (mob_living_carbon_human)w.MobOf(erin);
        w.Run(10);
        using (w.World.Enter())
        {
            // the server's config makes the ethereal selectable
            Assert.Contains("ethereal", Dm.each(GlobalProcs.get_selectable_species()).Select(Dm.str));
            bobMob.dna.features[FEATURE_MUTANT_COLOR] = "#3399ff";
            bobMob.set_species(typeof(datum_species_ethereal));
            carolMob.set_species(typeof(datum_species_pod));
            daveMob.set_species(typeof(datum_species_mush));
            // the job's backpack goes first: tg gives the shell to a back slot that is free
            if (erinMob.get_item_by_slot(ITEM_SLOT_BACK) is obj_item pack)
            {
                Waiting.NoSleep(erinMob.temporarilyRemoveItemFromInventory(pack, 1f));
                GlobalProcs.qdel(pack);
            }
            erinMob.set_species(typeof(datum_species_snail));
        }
        w.Run(20);
        ReplicaWorld replica = alice.Session.Replica;
        List<(string Resource, string State)> Of(mob_living_carbon_human m)
            => Sprites(replica, replica.Entities.Single(e => e.Id == m.dm_id).State.Appearance);
        List<(string Resource, string State)> ethereal = Of(bobMob), pod = Of(carolMob), mush = Of(daveMob), snail = Of(erinMob);
        foreach (var list in new[] { ethereal, pod, mush, snail })
            output.WriteLine(string.Join("\n", list.Select(s => s.Resource + " " + s.State)) + "\n--");
        Assert.Contains(ethereal, s => s.Resource == "icons/mob/human/species/ethereal/bodyparts.dmi" && s.State.StartsWith("ethereal_chest", StringComparison.Ordinal));
        Assert.Contains(pod, s => s.State.StartsWith("pod_chest", StringComparison.Ordinal));
        Assert.Contains(pod, s => s.State.StartsWith("m_pod_hair_", StringComparison.Ordinal));
        Assert.Contains(mush, s => s.State.StartsWith("mush_chest", StringComparison.Ordinal));
        Assert.Contains(mush, s => s.State.Contains("m_caps_round", StringComparison.Ordinal));
        Assert.Contains(snail, s => s.State.Contains("snailshell", StringComparison.Ordinal));

        // the ethereal's glow follows its colour, and an EMP puts it out
        using (w.World.Enter())
        {
            var glow = (datum_status_effect_grouped_bodypart_effect_ethereal_glow)bobMob.has_status_effect(typeof(datum_status_effect_grouped_bodypart_effect_ethereal_glow));
            Assert.NotNull(glow);
            Assert.True(Dm.truthy(glow.ethereal_light.light_on));
            Waiting.NoSleep(bobMob.emp_act(EMP_LIGHT));
            Assert.False(Dm.truthy(glow.ethereal_light.light_on));
        }
        w.Run(10);
        using (w.World.Enter())
        {
            var glow = (datum_status_effect_grouped_bodypart_effect_ethereal_glow)bobMob.has_status_effect(typeof(datum_status_effect_grouped_bodypart_effect_ethereal_glow));
            Assert.True(Dm.truthy(glow.disrupted));
        }
    }

    [Fact]
    public void Character_setup_lists_the_new_species_and_their_features_and_applies_them()
    {
        using var rig = new Bots.BotServerRig(strict: true);
        using (rig.World.Enter())
        {
            // the pod, mushroomperson and snail are not roundstart in tg: make them selectable as the admin's config would
            ((datum_config_entry)GlobalProcs.config.entries_by_type[typeof(datum_config_entry_keyed_list_roundstart_races)]).config_entry_value =
                Dm.list(Dm.kv(SPECIES_HUMAN, 1f), Dm.kv(SPECIES_ETHEREAL, 1f), Dm.kv(SPECIES_PODPERSON, 1f), Dm.kv(SPECIES_MUSHROOM, 1f), Dm.kv(SPECIES_SNAIL, 1f));
            GlobalProcs.GLOB.roundstart_races = Dm.list();
            ((datum_preference_choiced)GlobalProcs.GLOB.preference_entries[typeof(datum_preference_choiced_species)]).cached_values = null;
            DmList selectable = GlobalProcs.get_selectable_species() as DmList;
            foreach (string id in new[] { SPECIES_ETHEREAL, SPECIES_PODPERSON, SPECIES_MUSHROOM, SPECIES_SNAIL })
                Assert.Contains(id, Dm.each(selectable).Select(Dm.str));

            DmList Icons(Type type) => (DmList)((DmList)((datum_preference)GlobalProcs.GLOB.preference_entries[type]).compile_constant_data())["icons"];
            Assert.Equal(19, Dm.length(Icons(typeof(datum_preference_choiced_ethereal_color))));
            Assert.True(Dm.length(Icons(typeof(datum_preference_choiced_species_feature_pod_hair))) >= 10);

            // the species' savefile keys are the ones Character Setup shows
            Assert.Contains("feature_ethcolor", Dm.each(((datum_species)Dm.@new<datum_species_ethereal>()).get_features()).Select(Dm.str));
            Assert.Contains("feature_pod_hair", Dm.each(((datum_species)Dm.@new<datum_species_pod>()).get_features()).Select(Dm.str));
            Assert.Contains("feature_mushperson_cap", Dm.each(((datum_species)Dm.@new<datum_species_mush>()).get_features()).Select(Dm.str));

            // the preferences apply to the body: the ethereal's colour, the pod's hairstyle
            var human = Dm.@new<mob_living_carbon_human>(Builtins.locate(2, 2, 1));
            human.set_species(typeof(datum_species_ethereal));
            var prefs = Dm.@new<datum_preferences>(Dm.@new<datum_client_interface>());
            prefs.write_preference((datum_preference)GlobalProcs.GLOB.preference_entries[typeof(datum_preference_choiced_species)], SPECIES_ETHEREAL);
            var colour = (datum_preference_choiced_ethereal_color)GlobalProcs.GLOB.preference_entries[typeof(datum_preference_choiced_ethereal_color)];
            colour.apply_to_human(human, "Pink", prefs);
            Assert.Equal("#ff99cc", Dm.str(human.dna.features[FEATURE_MUTANT_COLOR]));
        }
    }
}
