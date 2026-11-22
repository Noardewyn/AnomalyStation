using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package OG1 (D339, D341): the organs and implants left and the recovered crew. The eye augments give their sight (thermals' mobs,
/// night vision's cutoff, the shield's flash protection, the flashlight eyes' beam); damaged eyes blind and healed eyes see again; a
/// heart stopped by an EMP restarts; the tongues change the speech; the appendix inflames by tg's call; the spell implant gives its
/// spell; a long-term body bag holds a recovered crewmember with injuries whose brain explodes if taken out, and revived he is offered
/// to the ghosts, the chosen one gets a bank account, a lockbox only he opens and the death lore; and every new live type of the
/// package is made, put in, run and taken out with no runtimes.
/// </summary>
public class OrgansOG1Tests
{
    private static readonly Type[] OrganTypes =
    {
        typeof(obj_item_organ_eyes_robotic_flashlight), typeof(obj_item_organ_eyes_robotic_shield), typeof(obj_item_organ_eyes_robotic_glow),
        typeof(obj_item_organ_eyes_robotic_tacvisor), typeof(obj_item_organ_eyes_robotic_tacvisor_deathsquad), typeof(obj_item_organ_eyes_night_vision_maintenance_adapted),
        typeof(obj_item_organ_eyes_zombie), typeof(obj_item_organ_heart_cursed), typeof(obj_item_organ_heart_cybernetic_surplus), typeof(obj_item_organ_heart_freedom),
        typeof(obj_item_organ_heart_evolved), typeof(obj_item_organ_heart_evolved_sacred), typeof(obj_item_organ_heart_cybernetic_anomalock),
        typeof(obj_item_organ_heart_cybernetic_anomalock_prebuilt), typeof(obj_item_organ_heart_ethereal), typeof(obj_item_organ_liver_bloody),
        typeof(obj_item_organ_liver_cybernetic_surplus), typeof(obj_item_organ_liver_distillery), typeof(obj_item_organ_liver_snail), typeof(obj_item_organ_liver_bone),
        typeof(obj_item_organ_liver_bone_plasmaman), typeof(obj_item_organ_liver_golem), typeof(obj_item_organ_lungs_cybernetic_surplus), typeof(obj_item_organ_lungs_ethereal),
        typeof(obj_item_organ_lungs_slime), typeof(obj_item_organ_stomach_cybernetic_surplus), typeof(obj_item_organ_stomach_cybernetic_tier2),
        typeof(obj_item_organ_stomach_cybernetic_tier3), typeof(obj_item_organ_stomach_golem), typeof(obj_item_organ_tongue_abductor), typeof(obj_item_organ_tongue_bone),
        typeof(obj_item_organ_tongue_jelly), typeof(obj_item_organ_tongue_snail), typeof(obj_item_organ_tongue_zombie), typeof(obj_item_organ_vocal_cords),
        typeof(obj_item_organ_vocal_cords_adamantine), typeof(obj_item_organ_appendix_golem),
    };

    private static readonly Type[] OtherTypes =
    {
        typeof(obj_item_implant_spell), typeof(obj_item_storage_lockbox_mind), typeof(obj_item_flesh_shears),
    };

    private static string Diag(AudioRoom r, Feed feed) => "chat: " + string.Join(" | ", feed.Chat.TakeLast(10).Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int width = 13)
    {
        var r = new AudioRoom(width, 9, seed: seed);
        // tg's strings/ files (a decayed brain's mood, the zombie tongue's words), as the server mounts them
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Put<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item_organ
    {
        T organ = r.In(() => Dm.@new<T>(who.loc));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(organ.Insert(who, TRUE)))));
        r.Step(2);
        return organ;
    }

    [Fact]
    public void The_eye_augments_give_thermal_sight_night_vision_flash_protection_and_a_beam()
    {
        var (r, _, bob) = Room(10401);
        float before = r.In(() => Dm.num(bob.get_eye_protection()));

        Put<obj_item_organ_eyes_robotic_thermals>(r, bob);
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(bob.sight, SEE_MOBS))), "thermals see the mobs");

        var shield = Put<obj_item_organ_eyes_robotic_shield>(r, bob);
        Assert.False(r.In(() => Dm.truthy(Dm.bitand(bob.sight, SEE_MOBS))), "the thermals were replaced");
        Assert.True(r.In(() => Dm.num(bob.get_eye_protection())) >= before + FLASH_PROTECTION_WELDER, "the shield's flash protection");
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(shield.organ_flags, ORGAN_ROBOTIC))));

        var night = Put<obj_item_organ_eyes_night_vision_maintenance_adapted>(r, bob);
        Assert.True(r.In(() => Dm.truthy(bob.lighting_color_cutoffs)), "night vision gives its colour cutoffs");

        var flashlight = Put<obj_item_organ_eyes_robotic_flashlight>(r, bob);
        r.In(() =>
        {
            Assert.True(Dm.truthy(bob.is_blind()), "tg's flashlight eyes blind their host");
            Assert.NotNull(flashlight.eye);
            Assert.Same(bob, flashlight.eye.loc);
            Assert.True(Dm.truthy(flashlight.eye.light_on));
        });
        r.In(() => Waiting.NoSleep(flashlight.Remove(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(bob.is_blind_from(FLASHLIGHT_EYES)), "the flashlight eyes' blindness goes with them");
            Assert.True(flashlight.eye == null || QDELETED(flashlight.eye) || flashlight.eye.loc != bob);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Failing_eyes_blind_their_host_and_healed_eyes_see_again()
    {
        var (r, _, bob) = Room(10402);
        var eyes = r.In(() => bob.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes);
        Assert.NotNull(eyes);
        r.In(() => Waiting.NoSleep(eyes.apply_organ_damage(eyes.maxHealth)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.truthy(Dm.bitand(eyes.organ_flags, ORGAN_FAILING)));
            Assert.True(Dm.truthy(bob.is_blind()), "failed eyes blind");
        });
        r.In(() => Waiting.NoSleep(eyes.set_organ_damage(0)));
        r.Step(2);
        Assert.False(r.In(() => Dm.truthy(bob.is_blind())), "healed eyes see");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_emp_stops_a_cybernetic_heart_which_restarts_after_tgs_ten_seconds()
    {
        var (r, feed, bob) = Room(10403);
        var heart = Put<obj_item_organ_heart_cybernetic>(r, bob);
        r.In(() =>
        {
            heart.emp_vulnerability = 100; // tg's 80% chance of the lasting effect, made certain
            Waiting.NoSleep(heart.emp_act(EMP_HEAVY));
        });
        r.Step(2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(heart.is_beating()), "stopped");
            Assert.True(Dm.truthy(bob.undergoing_cardiac_arrest()));
            Assert.True(Dm.truthy(Dm.bitand(heart.organ_flags, ORGAN_EMP)));
        });
        Assert.True(feed.Any("as if your heart has stopped!"), Diag(r, feed));
        r.Step(11 * Second);
        Assert.True(r.In(() => Dm.truthy(heart.is_beating())), "restarted");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_snail_tongue_draws_out_the_letters_and_the_bone_tongue_chatters()
    {
        var (r, feed, bob) = Room(10404);
        Put<obj_item_organ_tongue_snail>(r, bob);
        r.In(() => Waiting.NoSleep(bob.say("hi")));
        r.Step(2);
        Assert.True(feed.Chat.Any(c => c.Html != null && c.Html.Contains("Hhhiii", StringComparison.Ordinal)), Diag(r, feed));

        var bone = Put<obj_item_organ_tongue_bone>(r, bob);
        r.In(() => Assert.Contains(typeof(datum_language_calcic), Dm.each(bone.get_possible_languages()).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_appendix_inflames_into_tgs_appendicitis()
    {
        var (r, feed, bob) = Room(10405);
        var appendix = r.In(() => bob.get_organ_slot(ORGAN_SLOT_APPENDIX) as obj_item_organ_appendix);
        Assert.NotNull(appendix);
        // the inflammation is the organ's own in this snapshot (no disease datum): its stage rises and the organ shows it
        r.In(() =>
        {
            appendix.inflamation_stage = 1;
            appendix.update_appearance();
        });
        r.Step(2);
        r.In(() =>
        {
            Assert.Contains("inflamed", Dm.str(appendix.name));
            Assert.Equal("appendixinflamed", Dm.str(appendix.icon_state));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_spell_implant_grants_its_spell_and_takes_it_back()
    {
        var (r, _, bob) = Room(10406);
        var implant = r.In(() =>
        {
            // tg's base spell implant has no spell; a mapped or admin-made one is given its spell_type before Initialize: here its spell
            var i = Dm.@new<obj_item_implant_spell>(bob.loc);
            i.spell_type = typeof(datum_action_cooldown_spell_conjure_invisible_chair);
            i.spell_to_give = Dm.@new(i.spell_type, i) as datum_action_cooldown_spell;
            return i;
        });
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(implant.implant(bob, bob, TRUE)))));
        r.Step(2);
        var spell = r.In(() => implant.spell_to_give);
        Assert.NotNull(spell);
        Assert.Same(bob, r.In(() => spell.owner));
        r.In(() => Waiting.NoSleep(implant.removed(bob, TRUE)));
        r.Step(2);
        Assert.Null(r.In(() => spell.owner));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_long_term_body_bag_holds_an_injured_recovered_crewmember_with_his_effects()
    {
        var (r, _, _) = Room(10407);
        var bag = r.In(() => Dm.@new<obj_structure_closet_body_bag_lost_crew_with_body>(locate(7, 4, 1)));
        r.Step(2);
        // tg's closets fill on their first opening; until then the body that is not made yet keeps the bag from folding
        r.In(() => Assert.False(Dm.truthy(bag.attempt_fold(null))));
        r.In(() => Waiting.NoSleep(bag.open()));
        r.Step(2);
        turf spot = r.In(() => locate(7, 4, 1) as turf);
        string contents = r.In(() => string.Join(", ", Dm.each(spot.contents).Select(o => Dm.str(((datum)o).type)))) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);
        r.In(() =>
        {
            Assert.True(Dm.truthy(bag.body_spawned), contents);
            var body = Dm.each<mob_living_carbon_human>(spot.contents).Single();
            Assert.Equal(DEAD, body.stat);
            Assert.NotNull(body.mind);
            Assert.True(Dm.num(body.get_brute_loss()) + Dm.num(body.get_fire_loss()) + Dm.num(body.get_tox_loss()) > 0
                || Dm.each<obj_item_organ>(body.organs).Count() < 10 || body.bodyparts.len < 6, "the body carries tg's injuries: " + contents);
            Assert.True(Dm.num(((datum_reagents)body.reagents).get_reagent_amount(typeof(datum_reagent_toxin_formaldehyde))) > 0, "formaldehyde");
            Assert.True(Dm.num(body.timeofdeath) < Dm.num(world.time), "a death date in the past");
            Assert.NotNull(body.station_timestamp_timeofdeath);
            var paper = Dm.each<obj_item_paper>(spot.contents).Single();
            Assert.Contains(Dm.str(paper.name), new[] { "DO NOT REMOVE BRAIN", "BODYSWAPPING PERMITTED" });
            var brain = body.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
            Assert.NotNull(brain.GetComponent(typeof(datum_component_ghostrole_on_revive)));
            Assert.True(HAS_TRAIT(body, TRAIT_GHOSTROLE_ON_REVIVE));
            // the station class's assignment: a lockbox only his mind opens, with the job's headset and visiting ID
            var box = Dm.each<obj_item_storage_lockbox_mind>(spot.contents).SingleOrDefault();
            Assert.True(box != null, contents);
            Assert.Same(body.mind, box.mind);
            Assert.NotEmpty(Dm.each<obj_item_card_id_advanced>(box.contents));
            Assert.Equal(Dm.str(body.real_name), Dm.str(Dm.each<obj_item_card_id_advanced>(box.contents).Single().registered_name));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_recovered_crewmembers_brain_explodes_when_taken_out()
    {
        var (r, feed, _) = Room(10408);
        var body = r.In(() =>
        {
            var b = Waiting.NoSleep(GLOB.lost_crew_manager.create_lost_crew(recovered_items: Dm.list(), protected_items: Dm.list())) as mob_living_carbon_human;
            Waiting.NoSleep(b.forceMove(locate(8, 4, 1)));
            return b;
        });
        r.Step(2);
        if (r.In(() => HAS_TRAIT(body, TRAIT_HUSK)))
            return; // a husked body may be bodyswapped (tg's BODYSWAPPING PERMITTED)
        var brain = r.In(() => body.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain);
        r.In(() => Waiting.NoSleep(brain.Remove(body)));
        r.Step(4);
        Assert.True(r.In(() => QDELETED(brain)), "the brain exploded");
        Assert.True(feed.Any("The brain explodes"), Diag(r, feed));
    }

    [Fact]
    public void A_revived_recovered_crewmember_is_offered_to_the_ghosts_and_the_chosen_one_rejoins_the_crew()
    {
        var (r, feed, bob) = Room(10409, width: 24);
        mob_living_carbon_human ginaBody = r.Join(20, 2, "Gina");
        client gina = r.In(() => ginaBody.client);
        r.In(() => ginaBody.ghostize(TRUE));
        r.Step(2);
        var ghost = Assert.IsType<mob_dead_observer>(r.In(() => gina.mob));

        var bag = r.In(() => Dm.@new<obj_structure_closet_body_bag_lost_crew_with_body>(locate(6, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bag.open()));
        r.Step(2);
        var body = r.In(() => Dm.each<mob_living_carbon_human>(locate(6, 4, 1)).Single());
        var box = r.In(() => Dm.each<obj_item_storage_lockbox_mind>(locate(6, 4, 1)).Single());
        r.In(() => Waiting.NoSleep(body.revive(HEAL_ALL)));
        r.Step(2);
        // the poll: the ghost's alert, signed up by a click
        var alert = r.In(() => Dm.each(ghost.alerts).Select(k => ghost.alerts[k]).OfType<atom_movable_screen_alert_poll_alert>().SingleOrDefault());
        Assert.True(alert != null, "no poll alert: " + Diag(r, feed));
        Click(r, ghost, alert);
        r.Step(16 * Second);
        Assert.Same(body, r.In(() => gina.mob));
        r.In(() =>
        {
            Assert.True(body.stat != DEAD);
            Assert.NotNull(body.mind.has_antag_datum(typeof(datum_antagonist_recovered_crew)));
            Assert.True(Dm.truthy(body.account_id), "a bank account");
            Assert.Null(body.GetComponent(typeof(datum_component_ghostrole_on_revive)));
            // his lockbox opens for him and nobody else
            Assert.True(Dm.truthy(box.can_unlock(body, null, TRUE)));
            Assert.False(Dm.truthy(box.can_unlock(bob, null, TRUE)));
        });
        r.Step(20 * Second);
        Assert.True(feed.Chat.Any(c => c.Html.Contains("My account number was")), Diag(r, feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Every_organ_is_made_put_in_run_and_taken_out_and_every_type_is_destroyed_with_no_runtimes()
    {
        var (r, _, bob) = Room(10410);
        foreach (Type type in OrganTypes)
        {
            var organ = r.In(() => (obj_item_organ)Dm.@new(type, locate(3, 3, 1)));
            r.Step(1);
            r.In(() => Waiting.NoSleep(organ.Insert(bob, TRUE)));
            r.Step(Second * 3);
            r.In(() =>
            {
                if (organ.owner == bob)
                    Waiting.NoSleep(organ.Remove(bob, TRUE));
                qdel(organ);
            });
            r.Step(2);
            r.In(() =>
            {
                if (bob.stat == DEAD)
                    Waiting.NoSleep(bob.revive(HEAL_ALL));
                Waiting.NoSleep(bob.regenerate_organs());
            });
            r.Step(2);
            Assert.True(r.Room.Runtimes.Count == 0, type.Name + ": " + string.Join("\n", r.Room.RuntimeText));
        }
        foreach (Type type in OtherTypes.Append(typeof(obj_structure_closet_body_bag_lost_crew_with_body)))
        {
            atom made = r.In(() => (atom)Dm.@new(type, locate(9, 3, 1)));
            r.Step(2);
            r.In(() => qdel(made));
            r.Step(2);
        }
        r.Step(Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_lost_crew_types_and_the_organs_are_live()
    {
        foreach (Type type in OrganTypes.Concat(OtherTypes).Concat(new[]
        {
            typeof(datum_lost_crew_manager), typeof(datum_corpse_damage_class_station), typeof(datum_corpse_damage_class_station_spaced),
            typeof(datum_corpse_damage_class_station_morgue), typeof(obj_structure_closet_body_bag_lost_crew_with_body), typeof(datum_component_ghostrole_on_revive),
            typeof(datum_element_orbit_twitcher), typeof(datum_element_dangerous_organ_removal), typeof(datum_element_lube_walking), typeof(obj_structure_ethereal_crystal),
        }))
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }
}
