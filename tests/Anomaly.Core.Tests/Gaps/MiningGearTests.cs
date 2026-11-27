using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The miners' gear left: the bluespace shelter capsules unfolding their shelters (and refusing crowded ground and the map's edge),
/// the wormhole jaunter (in hand, and on the belt over a chasm), the Kheiral cuffs' GPS off the station, the grapple gun's zip-line,
/// the vent pointer, the mine cart on its rails, the explorer suit and the goliath cloak, the labour camp's vendor and the light
/// emitters; and a sweep over every type the package made live. The rooms have two levels, the station's and a mining level.
/// </summary>
public class MiningGearTests
{
    private readonly ITestOutputHelper _output;

    public MiningGearTests(ITestOutputHelper output) => _output = output;

    private const int Size = 21;

    private static string Diag(Feed feed, AudioRoom r) => " chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    /// The converted shelters (maps/templates/shelter_*.as3dmap) at tg's paths, as the host mounts them (MapSeam).
    private static void MountShelters(World w)
    {
        IDmFileSystem files = DmWorldState.Of(w).Files;
        foreach (string converted in Directory.GetFiles(Path.Combine(MapFixtures.RepoRoot(), "maps", "templates"), "shelter_*" + MapText.Extension))
            files.WriteText("_maps/templates/" + Path.GetFileNameWithoutExtension(converted) + ".dmm", File.ReadAllText(converted));
    }

    /// A room whose level 1 is the station's and level 2 a mining level (the toy room's SSmapping keeps no z_list of its own).
    private static AudioRoom Room(ulong seed, Action<World> mapExtras = null)
    {
        var r = new AudioRoom(Size, Size, seed: seed, levels: 2, mapExtras: mapExtras, configure: MountShelters);
        r.In(() =>
        {
            SSmapping.z_list = Dm.list();
            SSmapping.z_level_to_plane_offset ??= Dm.list();
            SSmapping.z_level_to_lowest_plane_offset ??= Dm.list();
            SSmapping.gravity_by_z_level ??= Dm.list();
            SSmapping.z_level_to_stack ??= Dm.list();
            SSmapping.manage_z_level(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))), FALSE, contain_turfs: FALSE);
            SSmapping.manage_z_level(Dm.@new<datum_space_level>(2, "mining", Dm.list(Dm.kv(ZTRAIT_MINING, TRUE))), FALSE, contain_turfs: FALSE);
            SSmapping.preloadShelterTemplates();
        });
        return r;
    }

    private static void Jumpsuit(mob_living_carbon_human wearer)
    {
        if (wearer.w_uniform == null)
            Waiting.NoSleep(wearer.equip_to_slot_or_del(Dm.@new<obj_item_clothing_under_color_grey>(wearer.loc), ITEM_SLOT_ICLOTHING));
    }

    private static T Held<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        var thing = Dm.@new<T>(who.loc);
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(thing))));
        return thing;
    });

    [Fact]
    public void A_capsule_unfolds_its_shelter_and_throws_back_who_stood_too_close()
    {
        var r = Room(13001);
        Feed feed = Listen(r);
        var miner = r.Join(4, 10, "Miner");
        var bystander = r.Dummy(11, 10);
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(new[] { "shelter_alpha", "shelter_beta", "shelter_charlie", "shelter_toilet" },
                Dm.each(SSmapping.shelter_templates).Select(k => Dm.str(k)).OrderBy(k => k, StringComparer.Ordinal).ToArray());
            var alpha = (datum_map_template_shelter)SSmapping.shelter_templates["shelter_alpha"];
            Assert.Equal(5f, alpha.width);
            Assert.Equal(5f, alpha.height);
        });

        var capsule = Held<obj_item_survivalcapsule>(r, miner);
        r.In(() => Assert.Contains("This capsule has the Shelter Alpha stored.", Dm.each(Waiting.NoSleep(capsule.examine(miner))).Select(s => Dm.str(s))));
        r.In(() => Waiting.NoSleep(capsule.attack_self(miner)));
        Assert.True(feed.Any("begins to shake. Stand back!"), Diag(feed, r));
        Assert.Equal(1f, r.In(() => capsule.used));
        // thrown away from the miner, it lands beside the bystander
        r.In(() => Waiting.NoSleep(capsule.forceMove(locate(10, 10, 1))));
        bool thrown = false;
        for (int i = 0; i < 5 * Second + 4; i++)
        {
            r.Step();
            thrown |= r.In(() => bystander.throwing != null);
        }

        r.In(() =>
        {
            Assert.True(QDELETED(capsule), "the capsule is spent" + Diag(feed, r));
            turf centre = locate(10, 10, 1);
            Assert.IsType<area_misc_survivalpod>(get_area(centre));
            Assert.IsType<turf_closed_wall_mineral_plastitanium_survival_pod>(locate(8, 8, 1));
            var fridge = Assert.Single(Dm.each<obj_machinery_smartfridge_survival_pod_preloaded>(Dm.world));
            Assert.Equal(5, Dm.each<obj_item_food_donkpocket_warm>(fridge).Count());
            Assert.True(Dm.each<obj_item_storage_dice>(fridge).Any() || Dm.each<obj_item_instrument_guitar>(fridge).Any());
            Assert.False(InertSeam.IsInert(fridge));
            var computer = Assert.Single(Dm.each<obj_item_gps_computer>(Dm.world));
            Assert.NotNull(computer.GetComponent(typeof(datum_component_gps_item)));
            Assert.NotEmpty(Dm.each<obj_machinery_door_airlock_survival_pod>(Dm.world));
            Assert.NotEmpty(Dm.each<obj_effect_particle_effect_fluid_smoke>(Dm.world));
            // the bystander was knocked down and thrown away from the middle (DV416's arc is slower than tg's first step, so the shelter's
            // walls, loaded the same tick, catch him: DV612)
            Assert.True(thrown, "the bystander is thrown");
            Assert.True(Dm.truthy(bystander.IsParalyzed()) || Dm.truthy(bystander.IsKnockdown()), "the bystander is knocked down");
            Assert.Same(locate(4, 10, 1), miner.loc);
        });
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("phasein.ogg", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_capsule_refuses_a_crowded_spot_and_the_maps_edge()
    {
        var r = Room(13002);
        Feed feed = Listen(r);
        var miner = r.Join(4, 10, "Miner");
        r.Step(Second);
        r.In(() => Dm.@new<obj_machinery_vending_snack>(locate(11, 11, 1))); // dense and anchored, inside the footprint
        var capsule = Held<obj_item_survivalcapsule>(r, miner);
        r.In(() => Waiting.NoSleep(capsule.attack_self(miner)));
        r.In(() => Waiting.NoSleep(capsule.forceMove(locate(10, 10, 1))));
        r.Step(5 * Second + 4);
        Assert.True(feed.Any("doesn't have room to deploy! You need to clear a 5x5 area!"), Diag(feed, r));
        r.In(() =>
        {
            Assert.False(QDELETED(capsule));
            Assert.Equal(0f, capsule.used);
            Assert.IsNotType<area_misc_survivalpod>(get_area(locate(10, 10, 1)));
            var alpha = (datum_map_template_shelter)SSmapping.shelter_templates["shelter_alpha"];
            // rock, which the mining shelters may cover, but no wall
            Waiting.NoSleep(locate(5, 5, 2).ChangeTurf(typeof(turf_closed_mineral)));
            Assert.Equal(SHELTER_DEPLOY_ALLOWED, Dm.str(alpha.check_deploy(locate(5, 5, 2), capsule)));
            Waiting.NoSleep(locate(6, 6, 2).ChangeTurf(typeof(turf_closed_wall)));
            Assert.Equal(SHELTER_DEPLOY_BAD_TURFS, Dm.str(alpha.check_deploy(locate(5, 5, 2), capsule)));
        });
    }

    [Fact]
    public void The_jaunter_on_the_belt_saves_its_wearer_from_a_chasm_through_a_tunnel_to_a_station_beacon()
    {
        var r = Room(13003);
        Feed feed = Listen(r);
        var miner = r.Join(10, 10, "Miner");
        obj_item_wormhole_jaunter jaunter = null;
        obj_item_beacon beacon = null;
        r.In(() =>
        {
            beacon = Dm.@new<obj_item_beacon>(locate(10, 10, 1));
            Waiting.NoSleep(miner.forceMove(locate(5, 10, 2)));
            Jumpsuit(miner);
            jaunter = Dm.@new<obj_item_wormhole_jaunter>(miner.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(miner.equip_to_slot_or_del(jaunter, ITEM_SLOT_BELT))));
            Assert.Same(jaunter, miner.belt);
            Waiting.NoSleep(locate(6, 10, 2).ChangeTurf(typeof(turf_open_chasm_lavaland)));
        });
        r.Step(Second);
        r.In(() => Waiting.NoSleep(miner.Move(locate(6, 10, 2), EAST)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(QDELETED(jaunter), "the jaunter is spent" + Diag(feed, r));
            Assert.Equal(1f, miner.z);
            Assert.True(get_dist(miner, beacon) <= 6, $"the wearer lands near the beacon: {miner.x},{miner.y}");
            Assert.NotEqual(DEAD, miner.stat);
            Assert.True(Dm.truthy(miner.IsParalyzed()));
        });
        Assert.True(feed.Any("activates, saving you from"), Diag(feed, r));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("resonator_blast.ogg", StringComparison.Ordinal));
    }

    [Fact]
    public void The_jaunter_in_hand_finds_no_beacon_then_opens_a_tunnel_beside_its_user()
    {
        var r = Room(13004);
        Feed feed = Listen(r);
        var miner = r.Join(10, 10, "Miner");
        r.In(() => Waiting.NoSleep(miner.forceMove(locate(10, 10, 2))));
        r.Step(Second);
        var jaunter = Held<obj_item_wormhole_jaunter>(r, miner);
        r.In(() => Waiting.NoSleep(jaunter.attack_self(miner)));
        Assert.True(feed.Any("found no beacons in the world to anchor a wormhole to."), Diag(feed, r));
        Assert.False(r.In(() => QDELETED(jaunter)));

        r.In(() => Dm.@new<obj_item_beacon>(locate(10, 10, 1)));
        r.In(() => Waiting.NoSleep(jaunter.attack_self(miner)));
        r.In(() =>
        {
            Assert.True(QDELETED(jaunter));
            var tunnel = Assert.Single(Dm.each<obj_effect_portal_jaunt_tunnel>(Dm.world));
            Assert.Equal(2f, tunnel.z);
            Assert.Equal(1f, get_dist(tunnel, miner));
            Assert.Equal(1f, tunnel.hard_target.z);
        });
        Assert.True(feed.Any("activates the wormhole jaunter!"), Diag(feed, r));
    }

    [Fact]
    public void The_kheiral_cuffs_signal_off_the_station_and_go_quiet_back_on_it()
    {
        var r = Room(13005);
        Feed feed = Listen(r);
        var miner = r.Join(10, 10, "Miner");
        obj_item_clothing_accessory_kheiral_cuffs cuffs = null;
        r.In(() =>
        {
            Jumpsuit(miner);
            var card = Dm.@new<obj_item_card_id_advanced>(miner.loc);
            card.registered_name = "Mina Miner";
            Waiting.NoSleep(miner.equip_to_slot_or_del(card, ITEM_SLOT_ID));
            cuffs = Dm.@new<obj_item_clothing_accessory_kheiral_cuffs>(miner.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(miner.equip_to_slot_or_del(cuffs, ITEM_SLOT_GLOVES))));
            Assert.Equal(1f, cuffs.on_wrist);
            Assert.Equal(0f, cuffs.gps_enabled);
            Assert.Null(cuffs.GetComponent(typeof(datum_component_gps_kheiral_cuffs)));
        });
        r.In(() => Waiting.NoSleep(miner.forceMove(locate(10, 10, 2))));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(1f, cuffs.far_from_home);
            Assert.Equal(1f, cuffs.gps_enabled);
            var gps = Assert.IsType<datum_component_gps_kheiral_cuffs>(cuffs.GetComponent(typeof(datum_component_gps_kheiral_cuffs)));
            Assert.Equal("*Mina Miner's Kheiral Link", Dm.str(gps.gpstag));
            Assert.Contains(gps, Dm.each<datum_component_gps>(GLOB.GPS_list));
            Assert.True(HAS_TRAIT(miner, TRAIT_MULTIZ_SUIT_SENSORS));
            Assert.Contains("The cuff's GPS signal is on.", Dm.each(Waiting.NoSleep(cuffs.examine(miner))).Select(s => Dm.str(s)).Select(s => s.Contains("GPS signal is on", StringComparison.Ordinal) ? "The cuff's GPS signal is on." : s));
        });
        Assert.True(feed.Any("balloon: gps activated"), Diag(feed, r));
        r.In(() => Waiting.NoSleep(miner.forceMove(locate(10, 10, 1))));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(0f, cuffs.far_from_home);
            Assert.Equal(0f, cuffs.gps_enabled);
            Assert.Null(cuffs.GetComponent(typeof(datum_component_gps_kheiral_cuffs)));
            Assert.False(HAS_TRAIT(miner, TRAIT_MULTIZ_SUIT_SENSORS));
        });
        Assert.True(feed.Any("balloon: gps de-activated"), Diag(feed, r));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("handcuffs.ogg", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_grapple_gun_needs_low_pressure_or_an_emag_and_zips_its_user_to_the_rock()
    {
        var r = Room(13006);
        Feed feed = Listen(r);
        var miner = r.Join(4, 10, "Miner");
        r.In(() => Waiting.NoSleep(locate(12, 10, 1).ChangeTurf(typeof(turf_closed_wall))));
        r.Step(Second);
        var gun = Held<obj_item_grapple_gun>(r, miner);
        turf rock = r.In(() => locate(12, 10, 1));
        r.In(() =>
        {
            Assert.Equal(1f, gun.hooked);
            Assert.Contains(Dm.each(gun.overlays), o => o is DmAppearance m && Dm.str(m.icon_state) == "grapple_gun_hooked");
            // the station's air is too thick for its mechanism
            Assert.Equal(ITEM_INTERACT_BLOCKING, Waiting.NoSleep(gun.ranged_interact_with_atom(rock, miner)));
        });
        Assert.True(feed.Any("balloon: gun mechanism won't work here!"), Diag(feed, r));
        r.In(() => Waiting.NoSleep(gun.emag_act(miner, null)));
        Assert.True(feed.Any("balloon: pressure settings overloaded"), Diag(feed, r));
        r.In(() =>
        {
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(gun.ranged_interact_with_atom(rock, miner)));
            Assert.Equal(0f, gun.hooked);
            Assert.NotNull(gun.zipline);
        });
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("zipline_fire.ogg", StringComparison.Ordinal));
        bool zipping = false;
        for (int i = 0; i < 5 * Second && !zipping; i++)
        {
            r.Step();
            zipping = r.In(() => miner.x > 4);
        }
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.Equal(11f, miner.x);
            Assert.Equal(10f, miner.y);
            Assert.Equal(1f, gun.hooked);
            Assert.Null(gun.zipline);
            Assert.False(HAS_TRAIT(miner, TRAIT_IMMOBILIZED));
            Assert.NotEmpty(Dm.each<obj_effect_temp_visual_mook_dust>(Dm.world));
        });
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("zipline_hit.ogg", StringComparison.Ordinal));
    }

    [Fact]
    public void The_vent_pointer_points_at_the_nearest_unscanned_vent_on_its_level()
    {
        var r = Room(13007);
        obj_item_pinpointer_vent pointer = null;
        obj_structure_ore_vent near = null, far = null;
        r.In(() =>
        {
            near = Dm.@new<obj_structure_ore_vent>(locate(6, 6, 1));
            far = Dm.@new<obj_structure_ore_vent>(locate(16, 16, 1));
            Dm.@new<obj_structure_ore_vent>(locate(4, 4, 2)); // another level
            pointer = Dm.@new<obj_item_pinpointer_vent>(locate(3, 3, 1));
            pointer.scan_for_target();
            Assert.Same(near, pointer.target);
            near.discovered = TRUE;
            pointer.scan_for_target();
            Assert.Same(far, pointer.target);
        });
    }

    [Fact]
    public void A_shoved_mine_cart_rolls_along_its_rails_and_spills_its_load_at_the_end_of_the_line()
    {
        obj_structure_closet_crate_miningcar cart = null;
        obj_item_storage_toolbox load = null;
        var r = Room(13008, w =>
        {
            for (int x = 5; x <= 10; x++)
            {
                var rail = Dm.@new<obj_structure_minecart_rail>(locate(x, 10, 1));
                rail.dir = EAST;
            }
            cart = Dm.@new<obj_structure_closet_crate_miningcar>(locate(5, 10, 1));
        });
        Feed feed = Listen(r);
        var miner = r.Join(4, 10, "Miner");
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(1f, cart.on_rails);
            Assert.Equal(0.5f, cart.drag_slowdown);
            load = Dm.@new<obj_item_storage_toolbox>(cart);
            Waiting.NoSleep(miner.setDir(EAST));
            Waiting.Detach(cart.shove_off(miner));
        });
        Assert.True(feed.Any("balloon: setting off..."), Diag(feed, r));
        for (int i = 0; i < 10 * Second && !r.In(() => cart.momentum == 0 && cart.x > 5); i++)
            r.Step();
        r.In(() =>
        {
            Assert.Equal(10f, cart.x); // the end of the line, still in the miner's sight
            Assert.Equal(0f, cart.momentum);
            Assert.Equal(1f, cart.opened);
            Assert.NotSame(cart, load.loc);
        });
        Assert.True(feed.Any("comes to a halt!"), Diag(feed, r));
        Assert.True(feed.Any("spills its contents") || feed.Any("breaks open, spilling its contents"), Diag(feed, r));
    }

    [Fact]
    public void The_explorer_suit_takes_plates_and_the_goliath_cloak_turns_ceremonial_in_hand()
    {
        var r = Room(13009);
        Feed feed = Listen(r);
        var miner = r.Join(4, 10, "Miner");
        r.Step(Second);
        r.In(() =>
        {
            var suit = Dm.@new<obj_item_clothing_suit_hooded_explorer>(miner.loc);
            Assert.NotNull(suit.GetComponent(typeof(datum_component_armor_plate)));
            Assert.Same(GLOB.mining_suit_allowed, suit.allowed);
            Assert.Equal("#796755", Dm.str(suit.get_general_color()));
            var hood = Dm.@new<obj_item_clothing_head_hooded_explorer>(miner.loc);
            Assert.NotNull(hood.GetComponent(typeof(datum_component_armor_plate)));
            var skull = Dm.@new<obj_item_clothing_head_helmet_skull>(miner.loc);
            var plate = (datum_component_armor_plate)skull.GetComponent(typeof(datum_component_armor_plate));
            Assert.Equal(typeof(obj_item_clothing_accessory_talisman), plate.upgrade_item);
        });
        var cloak = Held<obj_item_clothing_suit_hooded_cloak_goliath>(r, miner);
        r.In(() =>
        {
            Assert.Same(GLOB.mining_suit_allowed, cloak.allowed);
            Assert.Equal(CLICK_ACTION_SUCCESS, Waiting.NoSleep(cloak.click_alt(miner)));
            Assert.Equal((float)ITEM_SLOT_NECK, cloak.slot_flags);
            Assert.Null(cloak.cold_protection); // tg: explorer_gear.dm:154 sets it to null
            Assert.Equal(CLICK_ACTION_SUCCESS, Waiting.NoSleep(cloak.click_alt(miner)));
            Assert.Equal((float)ITEM_SLOT_OCLOTHING, cloak.slot_flags);
        });
        Assert.True(feed.Any("for ceremonial use."), Diag(feed, r));
        Assert.True(feed.Any("for defensive use."), Diag(feed, r));
    }

    [Fact]
    public void The_labour_camp_vendor_sells_for_labour_points_only()
    {
        var r = Room(13010);
        Feed feed = Listen(r);
        var prisoner = r.Join(4, 10, "Prisoner");
        r.Step(Second);
        r.In(() =>
        {
            var vendor = Dm.@new<obj_machinery_vending_sustenance_labor_camp>(locate(5, 11, 1));
            var product = Dm.each<datum_data_vending_product>(vendor.product_records).First();
            var card = Dm.@new<obj_item_card_id_advanced_prisoner>(prisoner.loc);
            card.points = 100;
            Assert.Equal(100f, Dm.num(vendor.fetch_balance_to_use(card)));
            Assert.Equal(TRUE, Waiting.NoSleep(vendor.proceed_payment(card, prisoner, product, 30)));
            Assert.Equal(70f, card.points);
            Assert.Equal(FALSE, Waiting.NoSleep(vendor.proceed_payment(card, prisoner, product, 300)));
            Assert.Equal(70f, card.points);
            var staff = Dm.@new<obj_item_card_id_advanced>(prisoner.loc);
            Assert.Null(vendor.fetch_balance_to_use(staff));
            Assert.Equal(FALSE, Waiting.NoSleep(vendor.proceed_payment(staff, prisoner, product, 30)));
        });
        r.Step(2);
        Assert.True(feed.Any("You do not possess enough points to purchase"), Diag(feed, r));
        Assert.True(feed.Any("take bribes! Pay with labor points!"), Diag(feed, r));
    }

    [Fact]
    public void The_light_emitters_light_their_tile()
    {
        var r = Room(13011);
        r.In(() =>
        {
            var mine = Dm.@new<obj_effect_light_emitter>(locate(5, 5, 1));
            Assert.Equal(8f, mine.light_range);
            var podbay = Dm.@new<obj_effect_light_emitter_podbay>(locate(9, 9, 1));
            Assert.Equal(8f, podbay.light_range);
            Assert.Equal(1f, podbay.light_power);
        });
    }

    /// Every type the package made live is made, initialized and deleted without a runtime.
    [Fact]
    public void Every_newly_live_mining_type_is_made_and_deleted_cleanly()
    {
        var r = Room(13012);
        Type[] types =
        [
            typeof(obj_item_survivalcapsule), typeof(obj_item_survivalcapsule_luxury), typeof(obj_item_survivalcapsule_luxuryelite),
            typeof(obj_item_survivalcapsule_bathroom), typeof(obj_item_gps_computer), typeof(obj_machinery_smartfridge_survival_pod),
            typeof(obj_machinery_smartfridge_survival_pod_preloaded), typeof(obj_item_fakeartefact), typeof(obj_item_wormhole_jaunter),
            typeof(obj_item_clothing_accessory_kheiral_cuffs), typeof(obj_item_grapple_gun), typeof(obj_item_pinpointer_vent),
            typeof(obj_structure_closet_crate_miningcar), typeof(obj_structure_minecart_rail), typeof(obj_structure_minecart_rail_railbreak),
            typeof(obj_structure_mining_shuttle_beacon), typeof(obj_item_assault_pod_mining), typeof(obj_item_assault_pod_mining_unrestricted),
            typeof(obj_item_extraction_pack_networked), typeof(obj_structure_extraction_point_networked), typeof(obj_effect_light_emitter),
            typeof(obj_effect_light_emitter_podbay), typeof(obj_effect_light_emitter_thunderdome), typeof(obj_effect_light_emitter_fake_outdoors),
            typeof(obj_effect_light_emitter_tendril), typeof(obj_item_clothing_suit_hooded_explorer), typeof(obj_item_clothing_head_hooded_explorer),
            typeof(obj_item_clothing_suit_hooded_explorer_syndicate), typeof(obj_item_clothing_head_hooded_explorer_syndicate),
            typeof(obj_item_clothing_suit_hooded_cloak_goliath), typeof(obj_item_clothing_head_hooded_cloakhood_goliath),
            typeof(obj_item_clothing_head_helmet_skull), typeof(obj_machinery_vending_sustenance_labor_camp),
            typeof(obj_structure_sign_poster_official_no_erp), typeof(obj_structure_sign_poster_official_no_erp_directional_north),
        ];
        var made = new List<atom>();
        r.In(() =>
        {
            int x = 2;
            foreach (Type type in types)
            {
                var thing = (atom)Dm.@new(type, locate(x, 4, 1));
                Assert.False(InertSeam.IsInert(thing), type.Name + " is live");
                made.Add(thing);
                x = x >= Size - 2 ? 2 : x + 1;
            }
            Assert.False(InertSeam.IsInert(Dm.@new<obj_effect_portal_jaunt_tunnel>(locate(10, 12, 1), 100, null, FALSE, locate(10, 12, 2))));
            // the forgery takes the look of one of tg's treasures
            var forgery = made.OfType<obj_item_fakeartefact>().Single();
            Assert.NotEqual("expensive forgery", Dm.str(forgery.name));
            Assert.NotEqual("x2", Dm.str(forgery.icon_state));
            foreach (Type template in new[] { typeof(datum_map_template_shelter_alpha), typeof(datum_map_template_shelter_beta), typeof(datum_map_template_shelter_charlie), typeof(datum_map_template_shelter_toilet) })
            {
                var shelter = (datum_map_template_shelter)Dm.@new(template);
                Assert.True(shelter.width > 0 && shelter.height > 0, template.Name + " has its map's size");
                Assert.True(Dm.truthy(is_type_in_typecache(locate(1, 1, 1), shelter.blacklisted_turfs)), "the walls are barred");
            }
        });
        r.Step(2 * Second);
        r.In(() =>
        {
            foreach (atom thing in made)
                qdel(thing);
        });
        r.Step(2 * Second);
        _output.WriteLine(string.Join("\n", r.Room.RuntimeText));
        r.AssertNoRuntimes();
    }
}
