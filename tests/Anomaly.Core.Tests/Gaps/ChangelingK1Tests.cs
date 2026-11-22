using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package K1 (D327): the changeling (modules/antagonists/changeling) in a booted toy room: the Dynamic rulesets make a
/// changeling with its innate powers, emporium, first profile, objectives, hive name and the chemical regeneration on Life; the
/// cellular emporium's window sells the abilities for genetic points and readapts; Absorb DNA through a neck grab kills and husks the
/// victim, takes its profile, memories and the respec, and makes the absorbed player a hivemind member; Transform takes a profile's
/// name and look; the stings (extract DNA, mute) work by a middle click in range; the arm blade forms and retracts; reviving stasis fakes
/// death and revives; a changeling absorbed by another falls; the hivemind's say mode reaches the changelings.
/// </summary>
public class ChangelingK1Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, mob_living_carbon_human Alice) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        mob_living_carbon_human alice = r.Join(5, 4, "Alice");
        r.In(() => { Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))); Waiting.NoSleep(alice.equipOutfit(typeof(datum_outfit_job_assistant))); });
        r.Step(Second);
        return (r, feed, bob, alice);
    }

    /// The roundstart ruleset's own call, as Dynamic makes a changeling.
    private static datum_antagonist_changeling MakeChangeling(AudioRoom r, mob_living_carbon_human who)
    {
        r.In(() =>
        {
            var ruleset = Dm.@new<datum_dynamic_ruleset_roundstart_changeling>(SSdynamic.get_config());
            Waiting.NoSleep(ruleset.assign_role(who.mind));
        });
        r.Step(4);
        var ling = r.In(() => IS_CHANGELING(who)) as datum_antagonist_changeling;
        Assert.NotNull(ling);
        return ling;
    }

    private static T Power<T>(AudioRoom r, datum_antagonist_changeling ling) where T : datum_action_changeling => r.In(() =>
        Dm.each<T>(ling.innate_powers).FirstOrDefault() ?? Dm.each(ling.purchased_powers).Select(p => ling.purchased_powers[p]).OfType<T>().FirstOrDefault());

    /// The last "update" message of the window of that interface (the info window updates beside it).
    private static System.Text.Json.JsonElement UpdateOf(List<TguiOutput> outputs, string iface, out string windowId, bool withStatic = false)
    {
        windowId = null;
        System.Text.Json.JsonElement found = default;
        foreach (TguiOutput o in outputs)
        {
            if (o.Kind != TguiOutputKind.Message)
                continue;
            using var doc = System.Text.Json.JsonDocument.Parse(o.Json);
            if (doc.RootElement.GetProperty("type").GetString() != "update")
                continue;
            System.Text.Json.JsonElement payload = doc.RootElement.GetProperty("payload");
            if (InterfaceOf(payload) != iface || (withStatic && !payload.TryGetProperty("static_data", out _)))
                continue;
            found = payload.Clone();
            windowId = o.WindowId;
        }
        Assert.True(found.ValueKind == System.Text.Json.JsonValueKind.Object, "no " + iface + " update: " + string.Join(" | ", outputs.Select(o => o.Kind + " " + o.Json)));
        return found;
    }

    private static void Trigger(AudioRoom r, datum_action action, mob who, int steps = 2)
    {
        r.In(() => Waiting.Detach(action.Trigger(who)));
        r.Step(steps);
    }

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_antagonist_changeling), typeof(datum_antagonist_changeling_space), typeof(datum_antagonist_changeling_headslug),
            typeof(datum_changeling_profile), typeof(datum_cellular_emporium), typeof(datum_action_cellular_emporium),
            typeof(datum_action_changeling), typeof(datum_action_changeling_absorb_dna), typeof(datum_action_changeling_transform),
            typeof(datum_action_changeling_fakedeath), typeof(datum_action_changeling_regenerate), typeof(datum_action_changeling_weapon_arm_blade),
            typeof(datum_action_changeling_sting_extract_dna), typeof(datum_action_changeling_sting_mute), typeof(datum_action_changeling_headcrab),
            typeof(datum_action_changeling_lesserform), typeof(datum_action_changeling_mmi_talk), typeof(datum_saymode_changeling),
            typeof(mob_eye_imaginary_friend), typeof(mob_eye_imaginary_friend_hivemind), typeof(datum_component_ling_decoy_brain),
            typeof(atom_movable_screen_ling_chems), typeof(atom_movable_screen_ling_sting), typeof(obj_item_melee_arm_blade),
            typeof(obj_item_gun_magic_tentacle), typeof(obj_projectile_tentacle), typeof(mob_living_basic_headslug),
            typeof(obj_item_organ_body_egg_changeling_egg), typeof(obj_effect_meteor_meaty_changeling), typeof(datum_status_effect_fleshmend),
            typeof(datum_status_effect_darkness_adapted), typeof(datum_element_digitalcamo), typeof(datum_reagent_medicine_changelingadrenaline),
            typeof(datum_dynamic_ruleset_roundstart_changeling), typeof(datum_dynamic_ruleset_latejoin_changeling),
            typeof(datum_dynamic_ruleset_midround_from_ghosts_space_changeling), typeof(datum_dynamic_ruleset_midround_from_ghosts_space_changeling_mass),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
    }

    [Fact]
    public void TheRulesetMakesAChangelingWithItsPowersProfileObjectivesAndWindow()
    {
        var (r, feed, bob, _) = Room(8701);
        datum_antagonist_changeling ling = MakeChangeling(r, bob);

        // the innate powers (dna_cost CHANGELING_POWER_INNATE), granted; the emporium's action
        var innate = r.In(() => Dm.each<datum_action_changeling>(ling.innate_powers).Select(p => p.GetType()).ToHashSet());
        foreach (Type power in new[] { typeof(datum_action_changeling_absorb_dna), typeof(datum_action_changeling_fakedeath), typeof(datum_action_changeling_regenerate), typeof(datum_action_changeling_transform) })
            Assert.Contains(power, innate);
        Assert.All(r.In(() => Dm.each<datum_action_changeling>(ling.innate_powers).ToList()), p => Assert.Same(bob, r.In(() => p.owner)));
        Assert.Same(bob, r.In(() => ling.emporium_action.owner));

        // the first profile is the changeling's own, protected by nothing; one genome extracted
        Assert.Equal(1, r.In(() => ling.stored_profiles.len));
        Assert.Equal(r.In(() => Dm.str(bob.real_name)), r.In(() => Dm.str(ling.first_profile.name)));
        Assert.Equal(1f, r.In(() => ling.absorbed_count));
        Assert.Same(ling.first_profile, r.In(() => ling.current_profile));

        // the name in the hive and the hive's name; the starting chemicals and points
        Assert.Matches("^M[rsx]\\. ", r.In(() => ling.changelingID));
        Assert.EndsWith("-hive", r.In(() => Dm.str(ling.hive_name)));
        Assert.InRange(r.In(() => ling.chem_charges), 20f, 23f); // a Life tick may have regenerated a little
        Assert.Equal(10f, r.In(() => ling.genetic_points));

        // the objectives: one absorb objective (no other changeling, so not competitive) and an escape
        var objectives = r.In(() => Dm.each<datum_objective>(ling.objectives).ToList());
        Assert.IsType<datum_objective_absorb>(objectives[0]);
        Assert.Contains(objectives, o => o is datum_objective_escape);

        // the traits, the omnitongue, the info window
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_CHANGELING_HIVEMIND)));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_FAKE_SOULLESS)));
        Assert.Equal(1f, r.In(() => bob.get_language_holder().omnitongue));
        Assert.Contains(r.In(() => Take(bob)), o => o.Json != null && o.Json.Contains("AntagInfoChangeling"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ChemicalsRegenerateOnLifeUpToTheStorage()
    {
        var (r, _, bob, _) = Room(8702);
        datum_antagonist_changeling ling = MakeChangeling(r, bob);
        r.In(() => ling.chem_charges = 0);
        r.Step(10 * Second);
        float after = r.In(() => ling.chem_charges);
        Assert.True(after > 0, "no chemicals regenerated: " + after);
        r.In(() => ling.adjust_chemicals(1000));
        Assert.Equal(75f, r.In(() => ling.chem_charges));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEmporiumSellsAnAbilityForGeneticPointsAndReadapts()
    {
        var (r, _, bob, _) = Room(8703);
        datum_antagonist_changeling ling = MakeChangeling(r, bob);
        Take(bob);
        Trigger(r, ling.emporium_action, bob, 4);
        var payload = UpdateOf(Take(bob), "CellularEmporium", out string window, withStatic: true);
        var names = payload.GetProperty("static_data").GetProperty("abilities").EnumerateArray().Select(a => a.GetProperty("name").GetString()).ToList();
        Assert.Contains("Arm Blade", names);
        Assert.DoesNotContain("Absorb DNA", names); // innate powers are not on sale (dna_cost -1)
        Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal).ToList(), names, StringComparer.Ordinal);

        Act(r, bob, window, "evolve", ("path", "/datum/action/changeling/weapon/arm_blade"));
        var blade = Power<datum_action_changeling_weapon_arm_blade>(r, ling);
        Assert.NotNull(blade);
        Assert.Same(bob, r.In(() => blade.owner));
        Assert.Equal(8f, r.In(() => ling.genetic_points));

        // buying it again is refused; readapting without an absorption is refused
        Act(r, bob, window, "evolve", ("path", "/datum/action/changeling/weapon/arm_blade"));
        Assert.Equal(8f, r.In(() => ling.genetic_points));
        Act(r, bob, window, "readapt");
        Assert.Equal(8f, r.In(() => ling.genetic_points));

        // with a respec the powers go and the points come back
        r.In(() => ling.can_respec = 1);
        Act(r, bob, window, "readapt");
        Assert.Equal(10f, r.In(() => ling.genetic_points));
        Assert.Equal(0, r.In(() => ling.purchased_powers.len));
        Assert.Equal(0f, r.In(() => ling.can_respec));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AbsorbingANeckGrabbedVictimKillsItAndTakesItsDnaAndMemories()
    {
        var (r, feed, bob, alice) = Room(8704);
        datum_antagonist_changeling ling = MakeChangeling(r, bob);
        r.In(() =>
        {
            Waiting.NoSleep(bob.start_pulling(alice));
            Waiting.NoSleep(bob.setGrabState(GRAB_KILL));
        });
        r.Step(2);
        var absorb = Power<datum_action_changeling_absorb_dna>(r, ling);
        Trigger(r, absorb, bob, 2);
        Assert.True(r.In(() => Dm.truthy(absorb.is_absorbing)));
        r.Step(50 * Second); // three stages of 15 seconds
        r.AssertNoRuntimes();
        Assert.False(r.In(() => Dm.truthy(absorb.is_absorbing)), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.Equal(DEAD, r.In(() => alice.stat));
        Assert.True(r.In(() => HAS_TRAIT(alice, TRAIT_HUSK)), "the victim is not husked");
        Assert.Equal(2, r.In(() => ling.stored_profiles.len));
        Assert.Equal(1f, r.In(() => ling.true_absorbs));
        Assert.Equal(1f, r.In(() => ling.can_respec));
        Assert.True(feed.Any("We have absorbed"), string.Join(" | ", feed.Chat.Select(c => c.Html))); // the victim, out cold, sees none of it
        // the absorbed player became a hivemind member of the changeling
        Assert.Contains(r.In(() => Dm.each<object>(bob.imaginary_group).ToList()), m => m is mob_eye_imaginary_friend_hivemind);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TransformTakesAProfilesNameAndTheStingsWorkByAMiddleClick()
    {
        var (r, feed, bob, alice) = Room(8705);
        datum_antagonist_changeling ling = MakeChangeling(r, bob);
        r.In(() => ling.chem_charges = 75);

        // the extract DNA sting is a free power (0 points), bought and chosen
        r.In(() => Waiting.NoSleep(ling.purchase_power(typeof(datum_action_changeling_sting_extract_dna))));
        r.Step(2);
        var extract = Power<datum_action_changeling_sting_extract_dna>(r, ling);
        Assert.NotNull(extract);
        Trigger(r, extract, bob);
        Assert.Same(extract, r.In(() => ling.chosen_sting));
        r.In(() => ClientSeam.Click(bob.client, alice, "icon-x=16;icon-y=16;middle=1;button=middle"));
        r.Step(4);
        Assert.Equal(2, r.In(() => ling.stored_profiles.len));
        string aliceName = r.In(() => Dm.str(alice.real_name));
        Assert.NotNull(r.In(() => ling.get_dna(aliceName)));

        // transform into Alice's profile
        var profile = r.In(() => ling.get_dna(aliceName)) as datum_changeling_profile;
        r.In(() => Waiting.NoSleep(ling.transform(bob, profile)));
        r.Step(2);
        Assert.Equal(aliceName, r.In(() => Dm.str(bob.real_name)));
        Assert.Same(profile, r.In(() => ling.current_profile));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheArmBladeFormsAndRetracts()
    {
        var (r, feed, bob, _) = Room(8706);
        datum_antagonist_changeling ling = MakeChangeling(r, bob);
        r.In(() => ling.chem_charges = 75);
        r.In(() => Waiting.NoSleep(ling.purchase_power(typeof(datum_action_changeling_weapon_arm_blade))));
        r.Step(2);
        var blade = Power<datum_action_changeling_weapon_arm_blade>(r, ling);
        Trigger(r, blade, bob);
        var held = r.In(() => Dm.each<obj_item_melee_arm_blade>(bob.held_items).FirstOrDefault());
        Assert.NotNull(held);
        Assert.InRange(r.In(() => ling.chem_charges), 55f, 58f); // 20 spent, and a Life tick may regenerate a little
        Assert.True(r.In(() => HAS_TRAIT(held, TRAIT_NODROP)));
        Trigger(r, blade, bob);
        Assert.Null(r.In(() => Dm.each<obj_item_melee_arm_blade>(bob.held_items).FirstOrDefault()));
        Assert.True(feed.Any("into an arm"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheArmBladeSlashesAComputerAsAnAlienWould()
    {
        // gaps K1b: /obj/attack_alien's user is the carbon, so the arm blade's "muh copypasta" slash reaches it (60 brute, the slash sound)
        var (r, _, bob, _) = Room(8711);
        var computer = r.In(() => Dm.@new<obj_machinery_computer_teleporter>(locate(4, 5, 1)));
        var blade = r.In(() => Dm.@new<obj_item_melee_arm_blade>(bob.loc));
        r.Step(2);
        float before = r.In(() => Dm.num(computer.atom_integrity));
        r.In(() => Waiting.NoSleep(blade.afterattack(computer, bob, Dm.list(), Dm.list())));
        r.Step(2);
        Assert.True(r.In(() => Dm.num(computer.atom_integrity)) < before, "the computer took no damage");
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("slash") == true);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void RevivingStasisFakesDeathThenRevives()
    {
        var (r, feed, bob, _) = Room(8707);
        datum_antagonist_changeling ling = MakeChangeling(r, bob);
        var stasis = Power<datum_action_changeling_fakedeath>(r, ling);
        // a dead changeling enters stasis without the confirmation
        r.In(() => Waiting.NoSleep(bob.death(FALSE)));
        r.Step(2);
        Trigger(r, stasis, bob);
        Assert.True(r.In(() => HAS_TRAIT_FROM(bob, TRAIT_DEATHCOMA, CHANGELING_TRAIT)));
        Assert.InRange(r.In(() => ling.chem_charges), 5f, 8f); // 15 spent, and a Life tick may regenerate a little
        r.Step(42 * Second);
        Assert.True(r.In(() => Dm.truthy(stasis.revive_ready)));
        Assert.True(feed.Any("We are ready to revive."));
        Trigger(r, stasis, bob, 4);
        Assert.NotEqual(DEAD, r.In(() => bob.stat));
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_DEATHCOMA)));
        Assert.False(r.In(() => Dm.truthy(stasis.revive_ready)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHivemindSayModeReachesTheChangelingsOnly()
    {
        var (r, feed, bob, alice) = Room(8708);
        MakeChangeling(r, bob);
        var saymode = r.In(() => SSradio.saymodes[Dm.initial(typeof(datum_saymode_changeling), (datum_saymode s) => s.key)]) as datum_saymode_changeling;
        Assert.NotNull(saymode);
        var lings = r.In(() => Dm.each<object>(saymode.get_lings()).ToList());
        Assert.Contains(bob, lings);
        Assert.DoesNotContain(alice, lings);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRulesetsWeighAndTheChangelingMeteorCarriesASpaceChangeling()
    {
        var (r, _, _, _) = Room(8709);
        foreach (Type type in new[] { typeof(datum_dynamic_ruleset_roundstart_changeling), typeof(datum_dynamic_ruleset_latejoin_changeling),
            typeof(datum_dynamic_ruleset_midround_from_ghosts_space_changeling) })
        {
            float weight = r.In(() => Dm.num(((datum_dynamic_ruleset)Dm.@new(type, SSdynamic.get_config())).get_weight(30, DYNAMIC_TIER_HIGH)));
            Assert.True(weight > 0, type.Name + " weighs " + weight);
        }
        r.AssertNoRuntimes();
    }
}
