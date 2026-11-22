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
/// Gaps package SR1 (D333): the storage leftovers in a booted toy room with Bob as a player: every container the package made live
/// spawns live with its contents and no runtime; the can ring counts its cans and holds drinks only; the paper sack takes a pen's
/// design from its radial and becomes a hat under a knife; the gladius scabbard draws its blade by alt-click and counters an
/// attack it was readied for; the organ box's coolant freezes what it holds; the gum packet's wrappers show their gum and give it
/// up; the garment bag keeps out filled backpacks and blocks the backpacks inside it; the bio bag holds what tg lists; a bag of
/// holding put into another asks first and tears reality on a yes.
/// </summary>
public class StorageSR1Tests
{
    private static readonly Type[] AllTypes =
    {
        typeof(obj_item_storage_belt_champion), typeof(obj_item_storage_belt_sheath), typeof(obj_item_storage_belt_sheath_sabre),
        typeof(obj_item_storage_belt_sheath_sabre_gunpowered), typeof(obj_item_storage_belt_sheath_grass_sabre),
        typeof(obj_item_storage_belt_sheath_grass_sabre_gunpowered), typeof(obj_item_storage_belt_sheath_gladius),
        typeof(obj_item_storage_belt_sheath_katana), typeof(obj_item_storage_belt_sheath_katana_empty),
        typeof(obj_item_storage_belt_sheath_katana_toy), typeof(obj_item_storage_belt_sheath_katana_toy_empty),
        typeof(obj_item_storage_belt_sheath_ninja), typeof(obj_item_storage_belt_sheath_hanzo_katana),
        typeof(obj_item_storage_belt_sheath_hanzo_katana_empty), typeof(obj_item_storage_belt_mining_vendor),
        typeof(obj_item_storage_belt_soulstone_full), typeof(obj_item_storage_belt_soulstone_full_chappy),
        typeof(obj_item_storage_belt_military_snack), typeof(obj_item_storage_belt_military_snack_full),
        typeof(obj_item_storage_belt_military_abductor_full), typeof(obj_item_storage_belt_military_assault_full),
        typeof(obj_item_storage_belt_wands_full), typeof(obj_item_storage_belt_wands_full_discount),
        typeof(obj_item_storage_belt_wand_bandolier), typeof(obj_item_storage_belt_janitor_full),
        typeof(obj_item_storage_belt_fannypack_black_rogue), typeof(obj_item_storage_belt_fannypack_yellow_bee_terrorist),
        typeof(obj_item_storage_box_stickers_googly), typeof(obj_item_storage_box_shipping), typeof(obj_item_storage_box_shipping_debug),
        typeof(obj_item_storage_box_tape_wizard), typeof(obj_item_storage_box_fakesyndiesuit), typeof(obj_item_storage_box_holy),
        typeof(obj_item_storage_box_holy_clock), typeof(obj_item_storage_box_holy_student), typeof(obj_item_storage_box_holy_sentinel),
        typeof(obj_item_storage_box_holy_witchhunter), typeof(obj_item_storage_box_holy_adept), typeof(obj_item_storage_box_holy_follower),
        typeof(obj_item_storage_box_holy_divine_archer), typeof(obj_item_storage_box_holy_heretic), typeof(obj_item_storage_box_floor_camo),
        typeof(obj_item_storage_box_collar_bomb), typeof(obj_item_storage_box_itemset_crusader_blue),
        typeof(obj_item_storage_box_itemset_crusader_red), typeof(obj_item_storage_box_wizard_kit),
        typeof(obj_item_storage_box_wizard_kit_red), typeof(obj_item_storage_box_wizard_kit_yellow),
        typeof(obj_item_storage_box_wizard_kit_magusred), typeof(obj_item_storage_box_wizard_kit_magusblue),
        typeof(obj_item_storage_box_wizard_kit_black), typeof(obj_item_storage_box_debugtools), typeof(obj_item_storage_box_emergencytank),
        typeof(obj_item_storage_box_engitank), typeof(obj_item_storage_box_flat_fentanylpatches), typeof(obj_item_storage_box_papersack),
        typeof(obj_item_storage_box_papersack_meat), typeof(obj_item_storage_box_papersack_wheat), typeof(obj_item_storage_box_ingredients),
        typeof(obj_item_storage_box_ingredients_wildcard), typeof(obj_item_storage_box_ingredients_american),
        typeof(obj_item_storage_box_gum), typeof(obj_item_storage_box_gum_wake_up), typeof(obj_item_storage_box_gum_nicotine),
        typeof(obj_item_storage_box_gum_happiness), typeof(obj_item_storage_box_gum_bubblegum), typeof(obj_item_storage_box_mothic_rations),
        typeof(obj_item_storage_box_tiziran_goods), typeof(obj_item_storage_box_tiziran_cans), typeof(obj_item_storage_box_tiziran_meats),
        typeof(obj_item_storage_box_mothic_goods), typeof(obj_item_storage_box_mothic_cans_sauces),
        typeof(obj_item_storage_box_condimentbottles), typeof(obj_item_storage_box_ramen_beef), typeof(obj_item_storage_box_survival_radio),
        typeof(obj_item_storage_box_survival_mining_bonus), typeof(obj_item_storage_box_survival_engineer_radio),
        typeof(obj_item_storage_box_survival_centcom), typeof(obj_item_storage_box_survival_security_radio),
        typeof(obj_item_storage_box_clown), typeof(obj_item_storage_box_miner_modkits), typeof(obj_item_storage_box_syringes_variety),
        typeof(obj_item_storage_box_medipens), typeof(obj_item_storage_box_medipens_utility), typeof(obj_item_storage_box_beakers_big),
        typeof(obj_item_storage_box_beakers_bluespace), typeof(obj_item_storage_box_beakers_variety),
        typeof(obj_item_storage_box_injectors), typeof(obj_item_storage_box_plumbing), typeof(obj_item_storage_box_evilmeds),
        typeof(obj_item_storage_box_triage_cards), typeof(obj_item_storage_box_alchemist_basic_chems),
        typeof(obj_item_storage_box_alchemist_random_chems), typeof(obj_item_storage_box_alchemist_chemistry_kit),
        typeof(obj_item_storage_box_mechabeacons), typeof(obj_item_storage_box_methdealer), typeof(obj_item_storage_box_opiumdealer),
        typeof(obj_item_storage_box_kronkdealer), typeof(obj_item_storage_box_swab), typeof(obj_item_storage_box_gorillacubes),
        typeof(obj_item_storage_box_stockparts_basic), typeof(obj_item_storage_box_stockparts_deluxe),
        typeof(obj_item_storage_box_rndboards), typeof(obj_item_storage_box_stabilized), typeof(obj_item_storage_box_stingbangs),
        typeof(obj_item_storage_box_wall_flash), typeof(obj_item_storage_box_emps), typeof(obj_item_storage_box_seccarts),
        typeof(obj_item_storage_box_firingpins_paywall), typeof(obj_item_storage_box_firingpins_syndicate),
        typeof(obj_item_storage_box_lasertagpins), typeof(obj_item_storage_box_alienhandcuffs), typeof(obj_item_storage_box_large_dart),
        typeof(obj_item_storage_box_holy_grenades), typeof(obj_item_storage_box_fireworks),
        typeof(obj_item_storage_box_fireworks_dangerous), typeof(obj_item_storage_box_firecrackers), typeof(obj_item_storage_box_sparklers),
        typeof(obj_item_storage_box_cups), typeof(obj_item_storage_box_pdas), typeof(obj_item_storage_box_ids),
        typeof(obj_item_storage_box_silver_ids), typeof(obj_item_storage_box_snappops), typeof(obj_item_storage_box_lights_bulbs),
        typeof(obj_item_storage_box_lights_tubes), typeof(obj_item_storage_box_fountainpens), typeof(obj_item_storage_box_dishdrive),
        typeof(obj_item_storage_box_actionfigure), typeof(obj_item_storage_box_tail_pin), typeof(obj_item_storage_box_party_poppers),
        typeof(obj_item_storage_box_pinpointer_pairs), typeof(obj_item_storage_box_heretic_box),
        typeof(obj_item_storage_box_purity_seal_box), typeof(obj_item_storage_box_stamps), typeof(obj_item_storage_bag_trash_filled),
        typeof(obj_item_storage_bag_sheetsnatcher_debug), typeof(obj_item_storage_bag_money_vault),
        typeof(obj_item_storage_bag_money_dutchmen), typeof(obj_item_storage_bag_harpoon_quiver), typeof(obj_item_storage_bag_quiver_full),
        typeof(obj_item_storage_bag_quiver_holy), typeof(obj_item_storage_bag_quiver_endless), typeof(obj_item_storage_bag_garment_captain),
        typeof(obj_item_storage_bag_garment_hop), typeof(obj_item_storage_bag_garment_paramedic), typeof(obj_item_storage_backpack_holding),
        typeof(obj_item_storage_backpack_meat), typeof(obj_item_storage_backpack_duffelbag_sec_surgery),
        typeof(obj_item_storage_backpack_duffelbag_clown_cream_pie), typeof(obj_item_storage_backpack_duffelbag_syndie_hitman),
        typeof(obj_item_storage_backpack_duffelbag_syndie_med_bioterrorbundle), typeof(obj_item_storage_backpack_duffelbag_syndie_surgery),
        typeof(obj_item_storage_backpack_duffelbag_mining_conscript), typeof(obj_item_storage_basket),
        typeof(obj_item_storage_lockbox_clusterbang), typeof(obj_item_storage_lockbox_medal_service),
        typeof(obj_item_storage_lockbox_bitrunning_encrypted), typeof(obj_item_storage_organbox),
        typeof(obj_item_storage_organbox_preloaded), typeof(obj_item_storage_test_tube_rack), typeof(obj_item_storage_test_tube_rack_full),
        typeof(obj_item_storage_cans), typeof(obj_item_storage_cans_sixsoda), typeof(obj_item_storage_cans_sixbeer),
        typeof(obj_item_storage_cans_sixgamerdrink), typeof(obj_item_storage_cans_sixenergydrink), typeof(obj_item_storage_toolbox_fishing),
        typeof(obj_item_storage_toolbox_fishing_small), typeof(obj_item_storage_toolbox_drone), typeof(obj_item_storage_toolbox_ammobox),
        typeof(obj_item_storage_toolbox_ammobox_strilka310), typeof(obj_item_storage_toolbox_ammobox_strilka310_surplus),
        typeof(obj_item_storage_toolbox_ammobox_wt550m9), typeof(obj_item_storage_toolbox_ammobox_wt550m9ap),
        typeof(obj_item_storage_bubblegum_wrapper), typeof(obj_item_bag_of_holding_inert)
    };

    private static readonly Type[] StorageKinds =
    {
        typeof(datum_storage_bag_sheet_snatcher), typeof(datum_storage_bag_sheet_snatcher_borg), typeof(datum_storage_bag_sheet_snatcher_debug),
        typeof(datum_storage_bag_books), typeof(datum_storage_bag_chemistry), typeof(datum_storage_bag_bio), typeof(datum_storage_bag_xeno),
        typeof(datum_storage_bag_construction), typeof(datum_storage_bag_harpoon_quiver), typeof(datum_storage_bag_garment), typeof(datum_storage_bag_quiver),
        typeof(datum_storage_bag_quiver_less), typeof(datum_storage_bag_quiver_endless), typeof(datum_storage_bag_money), typeof(datum_storage_bag_fishing),
        typeof(datum_storage_box_donk_bullets), typeof(datum_storage_box_bandages), typeof(datum_storage_box_gorilla_cube_box), typeof(datum_storage_box_snappops),
        typeof(datum_storage_box_lights), typeof(datum_storage_box_gum), typeof(datum_storage_box_fishing_lures), typeof(datum_storage_box_ramen_beef),
        typeof(datum_storage_lockbox_dueling), typeof(datum_storage_lockbox_bitrunning_decrypted), typeof(datum_storage_toolbox_fishing),
        typeof(datum_storage_toolbox_fishing_small), typeof(datum_storage_test_tube_rack), typeof(datum_storage_organ_box), typeof(datum_storage_sixcan),
        typeof(datum_storage_bag_of_holding), typeof(datum_action_innate_blade_counter), typeof(datum_action_innate_blade_counter_gunpowered),
    };

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    private static T Place<T>(AudioRoom r, int x, int y) where T : atom
    {
        T thing = r.In(() => Dm.@new<T>(locate(x, y, 1)));
        r.Step(2);
        return thing;
    }

    // a left click after tg's click cooldown of the one before
    private static void Use(AudioRoom r, mob who, atom target, string modifiers = "")
    {
        r.Step(Second);
        Click(r, who, target, modifiers);
    }

    private static void PickRadial(AudioRoom r, mob who, string name)
    {
        r.Step(2);
        (long id, int index) = r.In(() =>
        {
            datum_radial_menu menu = Dm.each_cast<datum_radial_menu>(who.client.dm_radial_menus).SingleOrDefault();
            Assert.True(menu != null, "no radial menu");
            int i = menu.dm_elements.FindIndex(e => e.Name != null && e.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            Assert.True(i >= 0, "no slice " + name + " in " + string.Join(", ", menu.dm_elements.Select(e => e.Name)));
            return (menu.dm_id, i + 1);
        });
        r.Command(() => ClientSeam.RadialPick(who.client, id, index));
        r.Step(6); // the radial's wait (a stoplag loop) resumes, then the pick runs
    }

    // an act in the newest window of that interface the player has open (tg's AlertModal)
    private static void ActIn(AudioRoom r, mob who, string iface, string action, params (string Key, object Value)[] args)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/" + action, Dm.list(args.Select(a => (object)Dm.kv(a.Key, a.Value)).ToArray())));
        r.Step(4);
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    private static bool CanInsert(AudioRoom r, obj_item container, obj_item thing, mob user) =>
        r.In(() => Dm.truthy(container.atom_storage.can_insert(thing, user, messages: FALSE)));

    [Fact]
    public void EveryStorageLeftoverSpawnsLiveWithNoRuntime()
    {
        var (r, _, bob) = Room(9101);
        var failures = new List<string>();
        foreach (Type kind in StorageKinds)
        {
            if (Attribute.IsDefined(kind, typeof(DmInertAttribute), inherit: false))
                failures.Add(kind.Name + " is inert");
        }
        foreach (Type type in AllTypes)
        {
            int before = r.Room.Runtimes.Count;
            obj_item item = r.In(() => (obj_item)Dm.@new(type, locate(6, 4, 1)));
            r.Step(2);
            if (r.In(() => InertSeam.IsInert(item)))
                failures.Add(type.Name + " is inert");
            r.In(() => Waiting.NoSleep(item.examine(bob)));
            r.In(() => qdel(item));
            r.Step(2);
            foreach (Exception e in r.Room.Runtimes.Skip(before))
                failures.Add(type.Name + ": " + e.Message);
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void TheCanRingCountsItsCansAndHoldsOnlyDrinks()
    {
        var (r, _, bob) = Room(9102);
        var ring = Place<obj_item_storage_cans_sixsoda>(r, 4, 4);
        r.In(() =>
        {
            Assert.Equal(6, Dm.each<obj_item_reagent_containers_cup_soda_cans_cola>(ring.contents).Count());
            Assert.Equal("canholder6", ring.icon_state);
        });
        obj_item can = r.In(() => Dm.each<obj_item>(ring.contents).First());
        r.In(() => Waiting.NoSleep(ring.atom_storage.remove_single(bob, can, bob.loc)));
        r.Step(2);
        r.In(() => Assert.Equal("canholder5", ring.icon_state));

        // the ring holds cans and bottles of beer, ale and water, nothing else
        var crowbar = Place<obj_item_crowbar>(r, 4, 4);
        Assert.False(CanInsert(r, ring, crowbar, bob));
        Assert.True(CanInsert(r, ring, can, bob));
        r.In(() => Assert.True(Dm.truthy(ring.atom_storage.attempt_insert(can, bob))));
        r.Step(2);
        r.In(() => Assert.Equal("canholder6", ring.icon_state));

        // the gamer and energy packs pick six of their drinks
        var gamer = Place<obj_item_storage_cans_sixgamerdrink>(r, 5, 4);
        r.In(() => Assert.Equal(6, Dm.each<obj_item_reagent_containers_cup_soda_cans>(gamer.contents).Count()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePaperSackTakesAPenDesignAndBecomesAHatUnderAKnife()
    {
        var (r, feed, bob) = Room(9103);
        var sack = Place<obj_item_storage_box_papersack>(r, 5, 4);
        r.In(() =>
        {
            Assert.Equal("paperbag_None", sack.icon_state);
            Assert.Equal(5, Dm.length(sack.papersack_designs));
        });

        // a pen: the radial of tg's five designs; the heart is etched on
        var pen = Hold<obj_item_pen>(r, bob);
        r.In(() => Waiting.Detach(sack.tool_act(bob, pen, null)));
        PickRadial(r, bob, "Heart");
        Assert.True(feed.Any("balloon: modified"));
        r.In(() =>
        {
            Assert.Equal("Heart", sack.design_choice);
            Assert.Equal("paperbag_Heart", sack.icon_state);
            Assert.Equal("A paper sack with a heart etched onto the side.", sack.desc);
        });

        // something inside closes it
        var filler = Place<obj_item_pen>(r, 5, 4);
        r.In(() => Assert.True(Dm.truthy(sack.atom_storage.attempt_insert(filler, bob, @override: TRUE))));
        r.Step(2);
        r.In(() => Assert.Equal("paperbag_Heart_closed", sack.icon_state));
        r.In(() => Waiting.NoSleep(sack.atom_storage.remove_single(bob, filler, sack.loc)));
        r.Step(2);

        // the smiley design and a knife: a sack hat with the smile
        r.In(() =>
        {
            sack.design_choice = "SmileyFace";
            qdel(pen);
        });
        r.Step(2);
        var knife = Hold<obj_item_knife_kitchen>(r, bob);
        object result = r.In(() => Waiting.NoSleep(sack.tool_act(bob, knife, null)));
        r.Step(2);
        Assert.Equal((float)ITEM_INTERACT_SUCCESS, Dm.num(result));
        r.In(() =>
        {
            Assert.True(QDELETED(sack));
            Assert.Single(Dm.each<obj_item_clothing_head_costume_papersack_smiley>(locate(5, 4, 1).contents));
        });
        Assert.True(feed.Any("You cut eyeholes into"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheScabbardDrawsItsBladeByAltClickAndCountersAReadiedAttack()
    {
        var (r, feed, bob) = Room(9104);
        var sheath = Place<obj_item_storage_belt_sheath_gladius>(r, 4, 4);
        obj_item_claymore_gladius gladius = r.In(() =>
        {
            Assert.Equal("gladius_sheath-full", sheath.icon_state);
            return Assert.Single(Dm.each<obj_item_claymore_gladius>(sheath.contents));
        });
        string examined = r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(sheath.examine(bob))).Select(Dm.str)));
        Assert.Contains("Alt-click it to quickly draw the blade.", examined, StringComparison.Ordinal);

        // the alt-click draws the blade into the hand
        Use(r, bob, sheath, "alt=1");
        r.In(() =>
        {
            Assert.Same(gladius, bob.get_active_held_item());
            Assert.Equal("gladius_sheath", sheath.icon_state);
        });
        Assert.True(feed.Any("You take"));

        // put back: full again, and the counter waits out its resheath cooldown
        r.In(() => Assert.True(Dm.truthy(sheath.atom_storage.attempt_insert(gladius, bob))));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal("gladius_sheath-full", sheath.icon_state);
            Assert.True(sheath.resheath_cooldown > world.time);
        });

        // worn at the belt, the counterattack action is Bob's
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(bob.loc), ITEM_SLOT_ICLOTHING))));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(sheath, ITEM_SLOT_BELT))));
        });
        r.Step(2);
        var counter = r.In(() => Assert.Single(Dm.each<datum_action_innate_blade_counter>(sheath.actions)));
        r.In(() =>
        {
            Assert.Same(bob, counter.owner);
            Assert.False(Dm.truthy(Waiting.NoSleep(counter.IsAvailable(TRUE))));
        });
        Assert.True(feed.Any("You only just resheathed your blade!"));
        r.Step(11 * Second);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(counter.IsAvailable(TRUE)))));

        // a mindless body is too unpredictable
        var dummy = r.Dummy(5, 4);
        r.In(() => Waiting.NoSleep(counter.do_ability(bob, dummy)));
        Assert.True(feed.Any("They are too unpredictable to counterattack!"));

        // Ann, readied for, punches: Bob draws and strikes her hand instead of taking the blow
        var ann = r.Join(5, 5, "Ann");
        r.Step(Second);
        // the joined body keeps its random name (the key is Ann's)
        string annName = r.In(() => Dm.str(ann.name));
        r.In(() => Waiting.NoSleep(counter.do_ability(bob, ann)));
        Assert.True(feed.Any(Dm.text("You prepare to counterattack []!", annName)), string.Join("\n", feed.Chat.Select(c => c.Html)));
        r.In(() =>
        {
            Assert.True(sheath.full_ability_cooldown > world.time);
            ann.set_combat_mode(TRUE);
        });
        float bobBefore = r.In(() => Dm.num(bob.get_brute_loss()));
        Click(r, ann, bob);
        r.Step(2);
        r.In(() =>
        {
            Assert.Same(gladius, bob.get_active_held_item());
            Assert.Equal(0f, sheath.full_ability_cooldown);
            Assert.True(Dm.num(ann.get_brute_loss()) > 0, "the counter did not strike Ann");
            Assert.Equal(bobBefore, Dm.num(bob.get_brute_loss()));
        });
        Assert.True(Heard(r, "unsheath.ogg"));
        Assert.True(feed.Any("counter-attack"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheOrganBoxCoolantFreezesWhatItHolds()
    {
        var (r, _, bob) = Room(9105);
        var box = Place<obj_item_storage_organbox_preloaded>(r, 4, 4);
        r.In(() =>
        {
            Assert.Equal(100f, Dm.num(((datum_reagents)box.reagents).get_reagent_amount(typeof(datum_reagent_cryostylane))));
            Assert.Equal("organbox", box.icon_state);
        });
        var heart = Place<obj_item_organ_heart>(r, 4, 4);
        r.In(() => Assert.True(Dm.truthy(box.atom_storage.attempt_insert(heart, bob))));
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(heart, TRAIT_FROZEN), "the heart did not freeze");
            Assert.True(Dm.truthy(Dm.bitand(heart.organ_flags, ORGAN_FROZEN)));
            Assert.True(Dm.truthy(box.cooling));
            Assert.Equal("organbox-working", box.icon_state);
            Assert.True(Dm.num(((datum_reagents)box.reagents).get_reagent_amount(typeof(datum_reagent_cryostylane))) < 100f, "no coolant spent");
        });

        // taken out it thaws; with the coolant gone the box stops cooling
        r.In(() => Waiting.NoSleep(box.atom_storage.remove_single(bob, heart, box.loc)));
        r.Step(2);
        r.In(() => Assert.False(HAS_TRAIT(heart, TRAIT_FROZEN)));
        r.In(() => Waiting.NoSleep(((datum_reagents)box.reagents).clear_reagents()));
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(box.cooling));
            Assert.Equal("organbox", box.icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGumPacketsWrappersShowTheirGumAndGiveItUp()
    {
        var (r, _, bob) = Room(9106);
        var packet = Place<obj_item_storage_box_gum_nicotine>(r, 4, 4);
        var wrappers = r.In(() => Dm.each<obj_item_storage_bubblegum_wrapper>(packet.contents).ToList());
        Assert.Equal(4, wrappers.Count);
        var wrapper = wrappers[0];
        r.In(() =>
        {
            obj_item_food_bubblegum gum = Assert.Single(Dm.each<obj_item_food_bubblegum>(wrapper.contents));
            Assert.IsType<obj_item_food_bubblegum_nicotine>(gum);
            Assert.Equal(1f, wrapper.atom_storage.max_slots);
        });
        Assert.Contains(r.In(() => Dm.each<obj_item_food_bubblegum>(wrapper.contents).Single().icon_state), Overlays(r, wrapper));

        // the wrapper in hand, its alt-click (tg's click_alt: the storage's own alt-click opens it first when it can): the gum comes out
        // into the other hand
        r.In(() => Waiting.NoSleep(packet.atom_storage.remove_single(bob, wrapper, bob.loc)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.put_in_hands(wrapper)));
        r.Step(2);
        r.In(() => Assert.Equal((float)CLICK_ACTION_SUCCESS, Dm.num(Waiting.NoSleep(wrapper.click_alt(bob)))));
        r.Step(2);
        r.In(() =>
        {
            Assert.Empty(Dm.each<obj_item_food_bubblegum>(wrapper.contents));
            Assert.Contains(Dm.each<obj_item>(bob.held_items), i => i is obj_item_food_bubblegum_nicotine);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGarmentBagKeepsOutFilledBackpacksAndBlocksTheOnesInside()
    {
        var (r, feed, bob) = Room(9107);
        var bag = Place<obj_item_storage_bag_garment_captain>(r, 4, 4);
        var inner = r.In(() =>
        {
            Assert.Equal(20, Dm.length(bag.contents));
            return Dm.each<obj_item_storage_backpack_captain>(bag.contents).Single();
        });

        // a backpack in the bag takes nothing more
        var pen = Place<obj_item_pen>(r, 4, 4);
        r.In(() => Assert.False(Dm.truthy(inner.atom_storage.attempt_insert(pen, bob))));
        Assert.True(feed.Any("balloon: garment bag is in the way!"));

        // a filled backpack is refused; clothes go in
        var full = Place<obj_item_storage_backpack>(r, 4, 4);
        r.In(() => Assert.True(Dm.truthy(full.atom_storage.attempt_insert(pen, bob))));
        r.In(() => Waiting.NoSleep(bag.atom_storage.remove_single(bob, Dm.each<obj_item_clothing_shoes_laceup>(bag.contents).Single(), bag.loc)));
        r.Step(2);
        r.In(() => Assert.False(Dm.truthy(bag.atom_storage.can_insert(full, bob, messages: TRUE))));
        Assert.True(feed.Any("balloon: can't store filled backpacks!"));
        var crowbar = Place<obj_item_crowbar>(r, 4, 4);
        Assert.False(CanInsert(r, bag, crowbar, bob));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBagsHoldWhatTgListsForThem()
    {
        var (r, _, bob) = Room(9108);
        var bio = Place<obj_item_storage_bag_bio>(r, 4, 4);
        var money = Place<obj_item_storage_bag_money_vault>(r, 4, 4);
        var lights = Place<obj_item_storage_box_lights_tubes>(r, 4, 4);
        var rack = Place<obj_item_storage_test_tube_rack_full>(r, 4, 4);
        var syringe = Place<obj_item_reagent_containers_syringe>(r, 4, 4);
        var crowbar = Place<obj_item_crowbar>(r, 4, 4);
        Assert.True(CanInsert(r, bio, syringe, bob));
        Assert.False(CanInsert(r, bio, crowbar, bob));
        Assert.False(CanInsert(r, money, crowbar, bob));
        r.In(() =>
        {
            Assert.Equal(7, Dm.length(money.contents));
            Assert.Equal(21, Dm.each<obj_item_light_tube>(lights.contents).Count());
            Assert.Equal(8, Dm.each<obj_item_reagent_containers_cup_tube>(rack.contents).Count());
            Assert.Equal("rack8", rack.icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABagOfHoldingPutIntoAnotherAsksFirstAndTearsRealityOnAYes()
    {
        var (r, _, bob) = Room(9109);
        var outer = Place<obj_item_storage_backpack_holding>(r, 4, 4);
        var inner = Place<obj_item_storage_backpack_holding>(r, 4, 4);
        r.In(() => Assert.True(HAS_TRAIT(outer, TRAIT_BLUESPACE_INTERFERENCE)));

        // the insertion asks; "Abort" leaves both bags as they are
        r.In(() => Assert.False(Dm.truthy(outer.atom_storage.attempt_insert(inner, bob))));
        ActIn(r, bob, "AlertModal", "choose", ("choice", "Abort"));
        r.Step(Second);
        r.In(() =>
        {
            Assert.False(QDELETED(outer));
            Assert.False(QDELETED(inner));
            Assert.Same(locate(4, 4, 1), inner.loc);
        });

        // "Proceed": Bob is gibbed, both bags go and a tear in reality opens where the bag was
        r.In(() => Assert.False(Dm.truthy(outer.atom_storage.attempt_insert(inner, bob))));
        ActIn(r, bob, "AlertModal", "choose", ("choice", "Proceed"));
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(QDELETED(outer));
            Assert.True(QDELETED(inner));
            Assert.True(QDELETED(bob) || bob.stat == DEAD);
            Assert.NotEmpty(Dm.each<obj_reality_tear>(locate(4, 4, 1).contents));
        });
    }
}
