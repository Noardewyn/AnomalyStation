using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package J1 (D298): the clothing every job starts in, in a booted toy room. Every one of the 58 types that were inert (labcoats,
/// winter coats, jackets, boots and shoes, gloves, hats, ties, masks, sunglasses, badges and medals) is live and is worn in its slot and taken
/// off again with no runtimes; a labcoat's buttons toggle (toggle_suit.dm), a winter coat's hood comes up and goes down with its action
/// (toggle_attached_clothing.dm), sunglasses block a flash and the beer goggles scan reagents, the work boots' laces tie and untie, the
/// jackboots hold a knife, the tie is tied by alt-click, the detective's fedora gives its candy corn, the security helmet's visor flips and
/// the lawyer's badge objects at a table slam.
/// </summary>
public class JobClothingTests
{
    private static readonly Type[] AllTypes =
    {
        typeof(obj_item_clothing_accessory_lawyers_badge),
        typeof(obj_item_clothing_accessory_medal_gold_captain),
        typeof(obj_item_clothing_accessory_pocketprotector_full),
        typeof(obj_item_clothing_glasses_science),
        typeof(obj_item_clothing_glasses_sunglasses),
        typeof(obj_item_clothing_glasses_sunglasses_reagent),
        typeof(obj_item_clothing_gloves_botanic_leather),
        typeof(obj_item_clothing_gloves_captain),
        typeof(obj_item_clothing_gloves_color_black),
        typeof(obj_item_clothing_gloves_color_black_security),
        typeof(obj_item_clothing_gloves_latex_coroner),
        typeof(obj_item_clothing_gloves_latex_nitrile),
        typeof(obj_item_clothing_head_fedora_det_hat),
        typeof(obj_item_clothing_head_helmet_sec),
        typeof(obj_item_clothing_head_soft_paramedic),
        typeof(obj_item_clothing_head_utility_surgerycap_black),
        typeof(obj_item_clothing_mask_surgical),
        typeof(obj_item_clothing_neck_tie_black_tied),
        typeof(obj_item_clothing_neck_tie_detective),
        typeof(obj_item_clothing_neck_tie_red_tied),
        typeof(obj_item_clothing_shoes_jackboots),
        typeof(obj_item_clothing_shoes_jackboots_sec),
        typeof(obj_item_clothing_shoes_laceup),
        typeof(obj_item_clothing_shoes_sneakers_orange),
        typeof(obj_item_clothing_shoes_workboots),
        typeof(obj_item_clothing_shoes_workboots_black),
        typeof(obj_item_clothing_shoes_workboots_mining),
        typeof(obj_item_clothing_suit_apron),
        typeof(obj_item_clothing_suit_atmos_overalls),
        typeof(obj_item_clothing_suit_hooded_wintercoat_captain),
        typeof(obj_item_clothing_suit_hooded_wintercoat_cargo_qm),
        typeof(obj_item_clothing_suit_hooded_wintercoat_engineering),
        typeof(obj_item_clothing_suit_hooded_wintercoat_engineering_atmos),
        typeof(obj_item_clothing_suit_hooded_wintercoat_engineering_ce),
        typeof(obj_item_clothing_suit_hooded_wintercoat_hop),
        typeof(obj_item_clothing_suit_hooded_wintercoat_medical_chemistry),
        typeof(obj_item_clothing_suit_hooded_wintercoat_medical_cmo),
        typeof(obj_item_clothing_suit_hooded_wintercoat_medical_coroner),
        typeof(obj_item_clothing_suit_hooded_wintercoat_medical_paramedic),
        typeof(obj_item_clothing_suit_hooded_wintercoat_science_genetics),
        typeof(obj_item_clothing_suit_hooded_wintercoat_science_rd),
        typeof(obj_item_clothing_suit_hooded_wintercoat_security),
        typeof(obj_item_clothing_suit_jacket_quartermaster),
        typeof(obj_item_clothing_suit_toggle_cargo_tech),
        typeof(obj_item_clothing_suit_toggle_jacket_det_trench),
        typeof(obj_item_clothing_suit_toggle_labcoat),
        typeof(obj_item_clothing_suit_toggle_labcoat_chemist),
        typeof(obj_item_clothing_suit_toggle_labcoat_cmo),
        typeof(obj_item_clothing_suit_toggle_labcoat_coroner),
        typeof(obj_item_clothing_suit_toggle_labcoat_genetics),
        typeof(obj_item_clothing_suit_toggle_labcoat_paramedic),
        typeof(obj_item_clothing_suit_toggle_labcoat_research_director),
        typeof(obj_item_clothing_suit_toggle_labcoat_roboticist),
        typeof(obj_item_clothing_suit_toggle_labcoat_science),
        typeof(obj_item_clothing_suit_toggle_lawyer),
        typeof(obj_item_clothing_suit_toggle_suspenders),
        typeof(obj_item_clothing_under_rank_civilian_lawyer_bluesuit),
        typeof(obj_item_clothing_under_rank_medical_scrubs_coroner)
    };

    public static IEnumerable<object[]> Types() => AllTypes.Select(t => new object[] { t });

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Make<T>(AudioRoom r, atom where) where T : obj_item
    {
        T thing = r.In(() => Dm.@new<T>(where));
        r.Step(2);
        return thing;
    }

    private static T Wear<T>(AudioRoom r, mob_living_carbon_human who, object slot) where T : obj_item
    {
        T thing = Make<T>(r, who.loc as atom);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(thing, slot, disable_warning: TRUE, bypass_equip_delay_self: TRUE)))));
        r.Step(2);
        return thing;
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T thing = Make<T>(r, who.loc as atom);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(thing)))));
        return thing;
    }

    // A click, then a second of waiting: the next click of the same mob is refused inside tg's click delay.
    private static void Alt(AudioRoom r, mob who, atom target)
    {
        Click(r, who, target, "alt=1");
        r.Step(Second);
    }

    [Fact]
    public void EveryJobClothingTypeIsLiveAndIsWornAndTakenOffWithNoRuntimes()
    {
        var (r, _, bob) = Room(9801);
        var failures = new List<string>();
        foreach (Type type in AllTypes)
        {
            mob_living_carbon_human dummy = r.Dummy(5, 4);
            obj_item item = r.In(() => (obj_item)Dm.@new(type, dummy.loc));
            r.Step(2);
            if (r.In(() => InertSeam.IsInert(item)))
                failures.Add(type.Name + " is inert");
            if (item is obj_item_clothing_accessory accessory)
            {
                // an accessory is pinned onto a jumpsuit and worn with it
                obj_item_clothing_under_color_grey suit = r.In(() => Dm.@new<obj_item_clothing_under_color_grey>(dummy.loc));
                r.In(() => Waiting.NoSleep(dummy.equip_to_slot_if_possible(suit, ITEM_SLOT_ICLOTHING, disable_warning: TRUE, bypass_equip_delay_self: TRUE)));
                bool attached = r.In(() => Dm.truthy(Waiting.NoSleep(suit.attach_accessory(accessory, dummy))));
                if (!attached || !r.In(() => Dm.@in(accessory, suit.attached_accessories)))
                    failures.Add(type.Name + " was not pinned on a jumpsuit");
            }
            else
            {
                object slot = r.In(() => (object)(float)Dm.num(item.slot_flags) is float flags && flags > 0 ? (float)((int)flags & -(int)flags) : 0f);
                if ((float)slot == 0)
                    failures.Add(type.Name + " has no slot");
                else
                {
                    bool worn = r.In(() => Dm.truthy(Waiting.NoSleep(dummy.equip_to_slot_if_possible(item, slot, disable_warning: TRUE, bypass_equip_delay_self: TRUE))));
                    if (!worn || !r.In(() => ReferenceEquals(dummy.get_item_by_slot(slot), item)))
                        failures.Add(type.Name + " was not worn in slot " + slot);
                    r.Step(2);
                    r.In(() => Waiting.NoSleep(dummy.temporarilyRemoveItemFromInventory(item, force: TRUE)));
                }
            }
            r.In(() => qdel(item));
            r.In(() => qdel(dummy));
            r.Step(2);
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALabcoatToggleItsButtonsWithAltClickAndTellsSoWhenExamined()
    {
        var (r, feed, bob) = Room(9802);
        var coat = Wear<obj_item_clothing_suit_toggle_labcoat_cmo>(r, bob, ITEM_SLOT_OCLOTHING);
        string closed = r.In(() => coat.icon_state);
        r.In(() =>
        {
            Assert.NotNull(coat.GetComponent(typeof(datum_component_toggle_icon)));
            DmList lines = (DmList)Waiting.NoSleep(coat.examine(bob));
            Assert.Contains("Alt-click on", string.Join("|", Dm.each<object>(lines).Select(l => Dm.str(l))));
            Assert.Contains(typeof(obj_item_melee_baton_telescopic), Dm.each<object>(coat.allowed).ToList()); // the CMO's own allowed list
        });

        Alt(r, bob, coat);
        Assert.Equal(closed + "_t", r.In(() => coat.icon_state));
        Assert.True(feed.Any("toggled buttons"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Alt(r, bob, coat);
        Assert.Equal(closed, r.In(() => coat.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWinterCoatsHoodComesUpAndGoesDownWithItsActionAndNotWhileHeld()
    {
        var (r, feed, bob) = Room(9803);
        var coat = Hold<obj_item_clothing_suit_hooded_wintercoat_captain>(r, bob);
        datum_component_toggle_attached_clothing hooding = r.In(() => (datum_component_toggle_attached_clothing)coat.GetComponent(typeof(datum_component_toggle_attached_clothing)));
        Assert.NotNull(hooding);
        r.In(() =>
        {
            Assert.IsType<obj_item_clothing_head_hooded_winterhood_captain>(coat.hood);
            Assert.True(Dm.@in(coat.hood, coat.contents), "the hood waits in the coat");
            Assert.Contains(typeof(obj_item_gun_energy), Dm.each<object>(coat.allowed).ToList()); // GLOB.security_wintercoat_allowed
            Waiting.NoSleep(coat.ui_action_click(bob, hooding.toggle_action));
        });
        Assert.True(feed.Any("wear it first"), "a held coat does not put its hood up: " + string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.In(() => Assert.Null(bob.head));

        // wear it: the hood goes onto the head, the coat zips and its picture changes (the hood is no longer drawn down)
        r.In(() => Waiting.NoSleep(bob.temporarilyRemoveItemFromInventory(coat, force: TRUE)));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(coat, ITEM_SLOT_OCLOTHING, disable_warning: TRUE, bypass_equip_delay_self: TRUE)))));
        r.Step(2);
        r.In(() => Waiting.NoSleep(coat.ui_action_click(bob, hooding.toggle_action)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Same(coat.hood, bob.head);
            Assert.True(Dm.truthy(coat.zipped));
            Assert.EndsWith("_t", coat.icon_state);
            Assert.Equal(coat.icon_state, coat.worn_icon_state);
        });

        // and down again
        r.In(() => Waiting.NoSleep(coat.ui_action_click(bob, hooding.toggle_action)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Null(bob.head);
            Assert.True(Dm.@in(coat.hood, coat.contents));
            Assert.False(Dm.truthy(coat.zipped));
            Assert.False(coat.icon_state.EndsWith("_t"));
        });

        // the hood goes back into the coat when the coat is taken off with the hood up (POST_UNEQUIP)
        r.In(() => Waiting.NoSleep(coat.ui_action_click(bob, hooding.toggle_action)));
        r.Step(2);
        r.In(() => Assert.Same(coat.hood, bob.head));
        r.In(() => Waiting.NoSleep(bob.temporarilyRemoveItemFromInventory(coat, force: TRUE)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Null(bob.head);
            Assert.True(Dm.@in(coat.hood, coat.contents));
            Assert.False(Dm.truthy(coat.zipped));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void SunglassesBlockAFlashAndTheBeerGogglesAndScienceGogglesScanReagents()
    {
        var (r, _, bob) = Room(9804);
        Assert.True(r.In(() => Dm.num(bob.get_eye_protection()) < FLASH_PROTECTION_FLASH));
        var shades = Wear<obj_item_clothing_glasses_sunglasses>(r, bob, ITEM_SLOT_EYES);
        r.In(() => Assert.True(Dm.num(bob.get_eye_protection()) >= FLASH_PROTECTION_FLASH, "sunglasses.flash_protect (_glasses.dm:402)"));
        r.In(() => Waiting.NoSleep(bob.temporarilyRemoveItemFromInventory(shades, force: TRUE)));

        var beer = Wear<obj_item_clothing_glasses_sunglasses_reagent>(r, bob, ITEM_SLOT_EYES);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(bob, TRAIT_REAGENT_SCANNER));
            Assert.True(HAS_TRAIT(bob, TRAIT_BOOZE_SLIDER));
            Assert.True(Dm.num(bob.get_eye_protection()) >= FLASH_PROTECTION_FLASH);
            Waiting.NoSleep(bob.temporarilyRemoveItemFromInventory(beer, force: TRUE));
            Assert.False(HAS_TRAIT(bob, TRAIT_REAGENT_SCANNER));
        });

        Wear<obj_item_clothing_glasses_science>(r, bob, ITEM_SLOT_EYES);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(bob, TRAIT_REAGENT_SCANNER));
            Assert.True(HAS_TRAIT(bob, TRAIT_RESEARCH_SCANNER));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WorkBootsLacesUntieAndTieAgainAndTheJackbootsHoldAKnife()
    {
        var (r, feed, bob) = Room(9805);
        var boots = Hold<obj_item_clothing_shoes_workboots>(r, bob);
        r.In(() =>
        {
            Assert.Equal(SHOES_TIED, Dm.num(boots.tied));
            Assert.Equal(8 * SECONDS, Dm.num(boots.lace_time));
            Assert.IsType<datum_storage_pockets_shoes>(boots.atom_storage);
        });
        Click(r, bob, boots); // attack_self: unlace
        r.Step(8 * Second + 4);
        Assert.Equal(SHOES_UNTIED, r.In(() => Dm.num(boots.tied)));
        Assert.True(feed.Any("shoelaces"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Click(r, bob, boots);
        r.Step(8 * Second + 4);
        Assert.Equal(SHOES_TIED, r.In(() => Dm.num(boots.tied)));

        // the jackboots have a knife slot (the shoes' pockets), but no more than a pen's worth of stuff
        var jack = Make<obj_item_clothing_shoes_jackboots>(r, bob.loc as atom);
        var knife = Make<obj_item_knife_kitchen>(r, bob.loc as atom);
        var horn = Make<obj_item_bikehorn>(r, bob.loc as atom);
        r.In(() =>
        {
            Assert.IsType<datum_storage_pockets_shoes>(jack.atom_storage);
            Assert.True(Dm.truthy(jack.atom_storage.attempt_insert(knife, bob)), "a knife slips into the boot");
            Assert.Same(jack, knife.loc);
            Assert.False(Dm.truthy(jack.atom_storage.attempt_insert(horn, bob)), "a bike horn does not");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ATieIsTiedAndUntiedWithAltClickAndAClipOnIsNot()
    {
        var (r, feed, bob) = Room(9806);
        var tie = Wear<obj_item_clothing_neck_tie_red>(r, bob, ITEM_SLOT_NECK);
        r.In(() =>
        {
            Assert.False(Dm.truthy(tie.is_tied));
            Assert.Equal("tie_greyscale_untied", tie.icon_state);
            Assert.Equal(1 * SECONDS, Dm.num(tie.strip_delay));
        });
        Alt(r, bob, tie);
        r.Step(4 * Second + 4); // tie_timer 4 s
        r.In(() =>
        {
            Assert.True(Dm.truthy(tie.is_tied));
            Assert.Equal("tie_greyscale_tied", tie.icon_state);
            Assert.Equal(4 * SECONDS, Dm.num(tie.strip_delay));
        });
        Assert.True(feed.Any("You successfully tied"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Alt(r, bob, tie);
        r.Step(4 * Second + 4);
        Assert.False(r.In(() => Dm.truthy(tie.is_tied)));

        var tied = Make<obj_item_clothing_neck_tie_black_tied>(r, bob.loc as atom);
        Assert.Equal("tie_greyscale_tied", r.In(() => tied.icon_state));
        var loose = Make<obj_item_clothing_neck_tie_detective>(r, bob.loc as atom);
        r.In(() => Assert.Equal(NONE, Dm.num(Waiting.NoSleep(loose.click_alt(bob))))); // a clip-on has nothing to tie
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDetectivesFedoraHoldsItsFlaskAndGivesACandyCornEveryTwoMinutes()
    {
        var (r, feed, bob) = Room(9807);
        var hat = Hold<obj_item_clothing_head_fedora_det_hat>(r, bob);
        r.In(() =>
        {
            Assert.IsType<datum_storage_pockets_small_fedora_detective>(hat.atom_storage);
            Assert.Single(Dm.each_cast<obj_item_reagent_containers_cup_glass_flask_det>(hat.contents).ToList());
        });
        Alt(r, bob, hat);
        Assert.True(r.In(() => Dm.each_cast<atom>(bob.contents).OfType<obj_item_food_candy_corn>().Any() || Dm.each_cast<atom>(bob.loc.contents).OfType<obj_item_food_candy_corn>().Any()), "a candy corn");
        Assert.True(feed.Any("You slip a candy corn"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Alt(r, bob, hat);
        Assert.True(feed.Any("A candy corn was just taken"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.Step(2 * 60 * Second + 2);
        Alt(r, bob, hat);
        Assert.Equal(2, feed.Chat.Count(c => c.Html != null && c.Html.Contains("You slip a candy corn")));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSecurityHelmetFlipsItsVisorAndTightensItsStraps()
    {
        var (r, _, bob) = Room(9808);
        var helmet = Hold<obj_item_clothing_head_helmet_sec>(r, bob);
        r.In(() => Assert.True(Dm.truthy(Dm.bitand(helmet.flags_cover, HEADCOVERSEYES))));
        Alt(r, bob, helmet);
        r.In(() =>
        {
            Assert.True(Dm.truthy(helmet.flipped_visor));
            Assert.False(Dm.truthy(Dm.bitand(helmet.flags_cover, HEADCOVERSEYES)));
            Assert.EndsWith("-novisor", helmet.icon_state);
        });
        Assert.NotEmpty(r.Sounds.Of(bob, "sound/items/handling/helmet/visor_down.ogg").Concat(r.Sounds.Heard.Where(h => h.File != null && h.File.Contains("visor"))));
        Alt(r, bob, helmet);
        r.In(() => Assert.True(Dm.truthy(Dm.bitand(helmet.flags_cover, HEADCOVERSEYES))));

        bool hid = r.In(() => Dm.truthy(Dm.bitand(helmet.flags_inv, HIDEHAIR)));
        Click(r, bob, helmet); // attack_self: loosen or tighten the straps (3 s)
        r.Step(3 * Second + 4);
        Assert.NotEqual(hid, r.In(() => Dm.truthy(Dm.bitand(helmet.flags_inv, HIDEHAIR))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASurgicalMaskPullsDownInHandAndTheOrangeShoesChainTheirHandcuffs()
    {
        var (r, feed, bob) = Room(9809);
        var mask = Hold<obj_item_clothing_mask_surgical>(r, bob);
        Assert.False(r.In(() => Dm.truthy(mask.up)));
        Click(r, bob, mask);
        Assert.True(r.In(() => Dm.truthy(mask.up)), string.Join(" | ", feed.Chat.Select(c => c.Html)));

        var shoes = Wear<obj_item_clothing_shoes_sneakers_orange>(r, bob, ITEM_SLOT_FEET);
        var cuffs = Make<obj_item_restraints_handcuffs>(r, bob.loc as atom);
        float walking = r.In(() => Dm.num(shoes.slowdown));
        r.In(() => Waiting.NoSleep(cuffs.forceMove(shoes)));
        r.In(() =>
        {
            Assert.Same(cuffs, shoes.attached_cuffs);
            Assert.Equal(15, Dm.num(shoes.slowdown));
            Assert.Equal("sneakers_chained", shoes.icon_state);
            Assert.False(Dm.truthy(shoes.can_mob_unequip(bob)), "You need help taking these off!");
            Waiting.NoSleep(cuffs.forceMove(bob.loc as atom));
            Assert.Null(shoes.attached_cuffs);
            Assert.Equal(walking, Dm.num(shoes.slowdown));
            Assert.NotEqual("sneakers_chained", shoes.icon_state);
            Assert.True(Dm.truthy(shoes.can_mob_unequip(bob)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALawyersBadgeObjectsAtATableSlamAndAMedalIsAwardedWithItsInscription()
    {
        var (r, feed, bob) = Room(9810);
        obj_item_clothing_under_color_grey suit = Wear<obj_item_clothing_under_color_grey>(r, bob, ITEM_SLOT_ICLOTHING);
        var badge = Make<obj_item_clothing_accessory_lawyers_badge>(r, bob.loc as atom);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(suit.attach_accessory(badge, bob)))));
        var table = Place<obj_structure_table>(r, 6, 6);
        r.In(() => bob._SendSignal(COMSIG_LIVING_SLAM_TABLE, Dm.list(bob, table)));
        r.Step(Second);
        Assert.True(feed.Any("Objection!!"), string.Join(" | ", feed.Chat.Select(c => c.Html)));

        // a medal pinned on someone by another: the inscription and the awarded name (medals.dm:25)
        mob_living_carbon_human alice = r.Join(6, 4, "Alice");
        r.In(() => { bob.real_name = "Bob"; alice.real_name = "Alice"; });
        var medal = Make<obj_item_clothing_accessory_medal_gold_captain>(r, bob.loc as atom);
        r.In(() =>
        {
            medal.commendation_message = "For fine work";
            Assert.True(Dm.truthy(Waiting.NoSleep(suit.attach_accessory(medal, alice))));
            Assert.Equal("Alice", medal.awarder);
            Assert.Equal("Bob", medal.awarded_to);
            Assert.Contains("The inscription reads: For fine work - Awarded to Bob by Alice", medal.desc);
        });
        r.AssertNoRuntimes();
    }
}
