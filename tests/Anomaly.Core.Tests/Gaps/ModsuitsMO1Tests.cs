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
/// Gaps package MO1 (D333): the MODsuit core, in a booted toy room with Bob as a player: a civilian suit worn on the back deploys its
/// helmet, chestplate, gauntlets and boots onto him and retracts them; activated, it seals each part one step at a time and runs on its
/// core's cell, which drains while it is on and is swapped by hand with the cover open; its general modules (storage, flashlight,
/// ion jetpack) work; the paint kit recolours it from the MODpaint window; a MOD shell is built into a control unit; two MODlink
/// scryers call each other, and the Scryers station trait hangs one on a crewman; the MODsuit window shows the suit and works its acts; a pAI card without a pAI is refused; the D132
/// seam (narrowed by gaps MO2, which ported the department modules) keeps the space ninja's inert modules out of a pre-equipped suit
/// and out of install, and the antagonists' inert soul shard core out of the wizard's suit.
/// </summary>
public class ModsuitsMO1Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Make<T>(AudioRoom r, atom loc) where T : atom
    {
        T thing = r.In(() => Dm.@new<T>(loc));
        r.Step(2);
        return thing;
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = Make<T>(r, who.loc);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    // a civilian MODsuit (its storage, flashlight and welding modules, the last gaps MO2's) worn on Bob's back
    private static obj_item_mod_control_pre_equipped_civilian WornSuit(AudioRoom r, mob_living_carbon_human bob)
    {
        var mod = Make<obj_item_mod_control_pre_equipped_civilian>(r, bob.loc);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(mod, ITEM_SLOT_BACK)))));
        r.Step(2);
        r.In(() => Assert.Same(bob, mod.wearer));
        return mod;
    }

    private static void Deploy(AudioRoom r, mob_living_carbon_human bob, obj_item_mod_control mod)
    {
        r.Command(() => Waiting.Detach(mod.quick_deploy(bob)));
        r.Step(4);
    }

    // activation: a step of activation_step_time per part, then the control unit
    private static void Activate(AudioRoom r, mob_living_carbon_human bob, obj_item_mod_control mod)
    {
        r.Command(() => Waiting.Detach(mod.toggle_activate(bob)));
        r.Step(8 * Second);
    }

    private static string Examine(AudioRoom r, atom a, mob who) => r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(a.examine(who))).Select(o => Dm.str(o))));

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    [Fact]
    public void TheCivilianSuitDeploysItsPartsOntoItsWearerAndRetractsThem()
    {
        var (r, feed, bob) = Room(9401);
        var mod = WornSuit(r, bob);
        r.In(() =>
        {
            Assert.IsType<datum_mod_theme_civilian>(mod.theme);
            Assert.Equal("civilian-control", mod.icon_state);
            Assert.IsType<obj_item_mod_core_standard>(mod.core);
            Assert.IsType<obj_item_stock_parts_power_store_cell_super>(((obj_item_mod_core_standard)mod.core).cell);
            Assert.Equal(5, Dm.each(mod.get_parts(TRUE)).Count()); // the control unit and its four parts
        });
        Deploy(r, bob, mod);
        r.In(() =>
        {
            Assert.IsType<obj_item_clothing_head_mod>(bob.head);
            Assert.IsType<obj_item_clothing_suit_mod>(bob.wear_suit);
            Assert.IsType<obj_item_clothing_gloves_mod>(bob.gloves);
            Assert.IsType<obj_item_clothing_shoes_mod>(bob.shoes);
            Assert.Equal("civilian-helmet", bob.head.icon_state);
            Assert.Contains("civilian", Dm.str(bob.head.name));
            Assert.Contains("MOD helmet", Dm.str(bob.head.name));
            Assert.True(HAS_TRAIT(bob.head, TRAIT_NODROP)); // a deployed part is stuck to its slot
        });
        Assert.True(feed.Any("deploys its parts with a mechanical hiss"));
        Assert.True(Heard(r, "mechmove03"));

        Deploy(r, bob, mod);
        r.In(() =>
        {
            Assert.Null(bob.head);
            Assert.Null(bob.wear_suit);
            Assert.Null(bob.gloves);
            Assert.Null(bob.shoes);
            foreach (obj_item part in Dm.each_cast<obj_item>(mod.get_parts()))
                Assert.Same(mod, part.loc);
        });
        Assert.True(feed.Any("retracts its parts with a mechanical hiss"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSuitSealsItsPartsAStepAtATimeAndUnseals()
    {
        var (r, feed, bob) = Room(9402);
        var mod = WornSuit(r, bob);
        Deploy(r, bob, mod);
        r.Command(() => Waiting.Detach(mod.toggle_activate(bob)));
        r.Step(Second / 2);
        r.In(() =>
        {
            Assert.True(Dm.truthy(mod.activating));
            Assert.False(Dm.truthy(mod.active));
        });
        r.Step(8 * Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(mod.active));
            Assert.False(Dm.truthy(mod.activating));
            foreach (datum_mod_part part in Dm.each_cast<datum_mod_part>(mod.get_part_datums(TRUE)))
                Assert.True(Dm.truthy(part.@sealed), Dm.str(part.part_item) + " is not sealed");
            Assert.Equal("civilian-helmet-sealed", bob.head.icon_state);
            Assert.Equal("civilian-control-sealed", mod.icon_state);
            // the civilian helmet's sealed cover (the civilian theme is not spaceproof)
            Assert.True(Dm.truthy(Dm.bitand(((obj_item_clothing)bob.head).flags_cover, HEADCOVERSEYES)));
        });
        Assert.True(feed.Any("Systems started up"), "the activation message");

        Activate(r, bob, mod);
        r.In(() =>
        {
            Assert.False(Dm.truthy(mod.active));
            foreach (datum_mod_part part in Dm.each_cast<datum_mod_part>(mod.get_part_datums(TRUE)))
                Assert.False(Dm.truthy(part.@sealed));
            Assert.Equal("civilian-helmet", bob.head.icon_state);
            Assert.False(Dm.truthy(Dm.bitand(((obj_item_clothing)bob.head).flags_cover, HEADCOVERSEYES)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheActiveSuitDrainsItsCellWhichIsSwappedByHandWithTheCoverOpen()
    {
        var (r, feed, bob) = Room(9403);
        var mod = WornSuit(r, bob);
        Deploy(r, bob, mod);
        Activate(r, bob, mod);
        var core = r.In(() => (obj_item_mod_core_standard)mod.core);
        float before = r.In(() => Dm.num(core.cell.charge));
        r.Step(10 * Second);
        float after = r.In(() => Dm.num(core.cell.charge));
        Assert.True(after < before, "the cell drains while the suit is on: " + before + " -> " + after);
        r.In(() =>
        {
            Assert.Equal("good", mod.get_chargebar_color());
            Assert.Contains("%", Dm.str(mod.get_chargebar_string()));
        });

        Activate(r, bob, mod); // off
        var screwdriver = Hold<obj_item_screwdriver>(r, bob);
        r.Command(() => Waiting.Detach(mod.screwdriver_act(bob, screwdriver)));
        r.Step(2 * Second); // tg's one-second tool use
        r.In(() => Assert.True(Dm.truthy(mod.open)));
        Assert.True(feed.Any("balloon: cover opened"));
        Assert.Contains("You could remove the cell with an empty hand.", Examine(r, mod, bob));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(screwdriver)));

        // an empty hand on the open suit takes the cell out
        var oldCell = r.In(() => core.cell);
        r.In(() => mod.SEND_SIGNAL(COMSIG_ATOM_ATTACK_HAND, Dm.list(mod, bob)));
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.Null(core.cell);
            Assert.Same(oldCell, bob.get_active_held_item());
            Assert.Equal("Power Cell Missing", Dm.str(mod.get_chargebar_string()));
        });
        Assert.True(feed.Any("balloon: cell removed"));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(oldCell)));

        var newCell = Hold<obj_item_stock_parts_power_store_cell_high>(r, bob);
        r.In(() => mod.SEND_SIGNAL(COMSIG_ATOM_ITEM_INTERACTION, Dm.list(mod, bob, newCell, null)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Same(newCell, core.cell);
            Assert.Same(core, newCell.loc);
        });
        Assert.True(feed.Any("balloon: cell installed"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGeneralModulesStoreLightAndThrust()
    {
        var (r, feed, bob) = Room(9404);
        var mod = WornSuit(r, bob);
        var storage = r.In(() => Dm.locate<obj_item_mod_module_storage>(mod.modules));
        var flashlight = r.In(() => Dm.locate<obj_item_mod_module_flashlight>(mod.modules));
        r.In(() =>
        {
            Assert.NotNull(storage);
            Assert.NotNull(flashlight);
            Assert.NotNull(mod.atom_storage);
            Assert.Same(storage, mod.atom_storage.real_location);
        });
        var crayon = Make<obj_item_toy_crayon_red>(r, bob.loc);
        r.In(() => Assert.True(Dm.truthy(mod.atom_storage.attempt_insert(crayon, bob))));
        r.Step(2);
        r.In(() => Assert.Same(storage, crayon.loc));

        // an ion jetpack installed with the cover open
        var jetpack = Make<obj_item_mod_module_jetpack>(r, bob.loc);
        r.Command(() => Waiting.Detach(mod.install(jetpack, bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.@in(jetpack, mod.modules));
            Assert.NotNull(jetpack.GetComponent(typeof(datum_component_jetpack)));
        });

        Deploy(r, bob, mod);
        Activate(r, bob, mod);
        r.Command(() => Waiting.Detach(flashlight.on_select(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.truthy(flashlight.active));
            Assert.True(Dm.truthy(flashlight.light_on));
            Assert.Equal(flashlight.base_power * Dm.num(flashlight.light_range), flashlight.active_power_cost);
        });
        r.Command(() => Waiting.Detach(jetpack.on_select(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.truthy(jetpack.active));
            Assert.True(Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_full_speed))));
        });
        r.Command(() => Waiting.Detach(jetpack.on_select(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(jetpack.active));
            Assert.False(Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_full_speed))));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePaintKitRecolorsAnInactiveSuitFromItsWindow()
    {
        var (r, feed, bob) = Room(9405);
        var mod = Make<obj_item_mod_control_pre_equipped_civilian>(r, bob.loc);
        var kit = Hold<obj_item_mod_paint>(r, bob);
        r.In(() => Take(bob));
        r.Command(() => Waiting.Detach(mod.tool_act(bob, kit, Dm.list(Dm.kv(RIGHT_CLICK, "1")))));
        var data = Data(r, bob, out string window);
        Assert.Equal(20, data.GetProperty("currentColor").GetArrayLength());
        r.In(() =>
        {
            Assert.Same(mod, kit.editing_mod);
            Assert.NotNull(kit.proxy_view);
        });
        // a red-tinted matrix within the bounds
        var matrixValues = new object[] { 1.2f, 0f, 0f, 0f, 0f, 0.8f, 0f, 0f, 0f, 0f, 0.8f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f };
        r.Command(() => ClientSeam.TguiMessage(bob.client, window, "act/transition_color", Dm.list(Dm.kv("color", Dm.list(matrixValues)))));
        r.Step(4);
        r.Command(() => ClientSeam.TguiMessage(bob.client, window, "act/confirm", Dm.list()));
        r.Step(4);
        r.In(() =>
        {
            Assert.True(mod.color is DmList, "the suit took the colour matrix");
            Assert.Null(kit.editing_mod); // the window closed
        });

        // a matrix with too much red is refused
        var mod2 = Make<obj_item_mod_control_pre_equipped_civilian>(r, bob.loc);
        r.In(() => Take(bob));
        r.Command(() => Waiting.Detach(mod2.tool_act(bob, kit, Dm.list(Dm.kv(RIGHT_CLICK, "1")))));
        Data(r, bob, out string window2);
        var tooRed = new object[] { 1.25f, 0f, 0f, 0f, 1.25f, 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f };
        r.Command(() => ClientSeam.TguiMessage(bob.client, window2, "act/transition_color", Dm.list(Dm.kv("color", Dm.list(tooRed)))));
        r.Step(4);
        r.Command(() => ClientSeam.TguiMessage(bob.client, window2, "act/confirm", Dm.list()));
        r.Step(4);
        Assert.True(feed.Any("total red too high!"));
        r.In(() => Assert.False(mod2.color is DmList));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AModShellIsBuiltIntoAControlUnit()
    {
        var (r, feed, bob) = Room(9406);
        var shell = Make<obj_item_mod_construction_shell>(r, bob.loc);
        var screwdriver = Make<obj_item_screwdriver>(r, bob.loc);
        var wrench = Make<obj_item_wrench>(r, bob.loc);
        void Insert(obj_item thing)
        {
            r.In(() => Waiting.NoSleep(bob.put_in_hands(thing)));
            r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(shell.item_interaction(bob, thing)))));
            r.Step(2);
        }
        Insert(Make<obj_item_mod_core_standard>(r, bob.loc));
        r.In(() => Assert.Equal("mod-construction_core", shell.icon_state));
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(shell.screwdriver_act(bob, screwdriver)))));
        Insert(Make<obj_item_mod_construction_helmet>(r, bob.loc));
        Insert(Make<obj_item_mod_construction_chestplate>(r, bob.loc));
        Insert(Make<obj_item_mod_construction_gauntlets>(r, bob.loc));
        Insert(Make<obj_item_mod_construction_boots>(r, bob.loc));
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(shell.wrench_act(bob, wrench)))));
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(shell.screwdriver_act(bob, screwdriver)))));
        r.In(() => Assert.Equal("mod-construction_screwed_assembly", shell.icon_state));
        Assert.Contains("external plating", Examine(r, shell, bob));
        var plating = Make<obj_item_mod_construction_plating>(r, bob.loc);
        r.In(() => Assert.Contains("standard", Dm.str(plating.name)));
        Insert(plating);
        r.In(() =>
        {
            Assert.True(QDELETED(shell));
            var mod = bob.get_active_held_item() as obj_item_mod_control;
            Assert.NotNull(mod);
            Assert.IsType<datum_mod_theme_standard>(mod.theme);
            Assert.IsType<obj_item_mod_core_standard>(mod.core);
            Assert.Equal("standard-control", mod.icon_state);
        });
        Assert.True(feed.Any("balloon: unit finished"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TwoModlinkScryersCallEachOtherAndHangUp()
    {
        var (r, feed, bob) = Room(9407);
        mob_living_carbon_human al = r.Join(6, 4, "Al");
        r.Step(Second);
        var bobScryer = Make<obj_item_clothing_neck_link_scryer_loaded>(r, bob.loc);
        var alScryer = Make<obj_item_clothing_neck_link_scryer_loaded>(r, al.loc);
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(bobScryer, ITEM_SLOT_NECK))));
            Assert.True(Dm.truthy(Waiting.NoSleep(al.equip_to_slot_if_possible(alScryer, ITEM_SLOT_NECK))));
            Assert.Equal(MODLINK_FREQ_NANOTRASEN, Dm.str(bobScryer.mod_link.frequency));
            Assert.NotEqual(bobScryer.mod_link.id, alScryer.mod_link.id);
            Assert.Same(bobScryer.mod_link, GLOB.mod_link_ids[bobScryer.mod_link.id]);
        });
        Assert.Contains("The MODlink ID is " + r.In(() => bobScryer.mod_link.id), Examine(r, bobScryer, bob));

        // Bob calls: tg's list of the links on his frequency
        r.In(() => Take(bob));
        r.Command(() => Waiting.Detach(bobScryer.ui_action_click(bob)));
        r.Step(4);
        string choice = r.In(() => Dm.text("[] ([])", alScryer, alScryer.mod_link.id));
        string listWindow = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(bob.tgui_open_uis).Last().window.id));
        r.Command(() => ClientSeam.TguiMessage(bob.client, listWindow, "act/submit", Dm.list(Dm.kv("entry", choice))));
        r.Step(4);
        var alert = r.In(() => al.alerts[Dm.text("[]_modlink", REF(bobScryer.mod_link))] as atom_movable_screen_alert_modlink_call);
        Assert.NotNull(alert);
        Assert.True(r.Sounds.Of(al, "sound/items/weapons/ring.ogg").Count > 0 || Heard(r, "ring.ogg"));

        // Al accepts
        Click(r, al, alert);
        r.Step(Second);
        r.In(() =>
        {
            Assert.NotNull(bobScryer.mod_link.link_call);
            Assert.Same(bobScryer.mod_link.link_call, alScryer.mod_link.link_call);
            Assert.True(HAS_TRAIT(bob, TRAIT_IN_CALL));
            Assert.True(HAS_TRAIT(al, TRAIT_IN_CALL));
            Assert.NotNull(bobScryer.mod_link.visual);
            Assert.NotNull(alScryer.mod_link.visual);
            Assert.Same(al, bobScryer.mod_link.visual.loc); // Bob's picture stands by Al
            Assert.Same(bob, alScryer.mod_link.visual.loc); // and Al's by Bob
        });
        Assert.True(feed.Any("balloon: call accepted"));

        // Bob hangs up
        r.Command(() => Waiting.Detach(bobScryer.ui_action_click(bob)));
        r.Step(4);
        r.In(() =>
        {
            Assert.Null(bobScryer.mod_link.link_call);
            Assert.Null(alScryer.mod_link.link_call);
            Assert.False(HAS_TRAIT(bob, TRAIT_IN_CALL));
            Assert.False(HAS_TRAIT(al, TRAIT_IN_CALL));
            Assert.Null(bobScryer.mod_link.visual);
        });
        r.AssertNoRuntimes();
    }
    // the Scryers station trait (neutral_traits.dm:306-335): a crewman gets a MODlink scryer on the neck, labelled with his name, and
    // his tie is moved out of the way; the trait waited out of the random pool (DV432) until the scryer turned live with gaps MO1
    [Fact]
    public void TheScryersTraitHangsALabelledScryerOnACrewmansNeck()
    {
        var (r, _, bob) = Room(9411);
        r.In(() => Assert.False(datum_controller_subsystem_processing_station.WaitsForItsSystem(typeof(datum_station_trait_scryers))));
        var tie = Make<obj_item_clothing_neck_tie_black>(r, bob.loc);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(tie, ITEM_SLOT_NECK)))));
        r.In(() => Dm.@new<datum_station_trait_scryers>().on_job_after_spawn(null, null, bob, null));
        r.Step(2);
        r.In(() =>
        {
            var scryer = bob.wear_neck as obj_item_clothing_neck_link_scryer_loaded;
            Assert.NotNull(scryer);
            Assert.Equal(Dm.str(bob.name), Dm.str(scryer.label));
            Assert.EndsWith(" - " + Dm.str(bob.name), Dm.str(scryer.name));
            Assert.False(QDELETED(tie)); // moved off the neck: a pocket, the backpack or the floor (tg's backup slots)
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheModsuitWindowShowsTheSuitAndDeploysAPart()
    {
        var (r, feed, bob) = Room(9408);
        var mod = WornSuit(r, bob);
        r.In(() => Take(bob));
        r.Command(() => Waiting.Detach(mod.ui_interact(bob)));
        r.Step(4);
        var outputs = r.In(() => Take(bob));
        var payload = LastFullUpdate(outputs, out string window);
        Assert.Equal("MODsuit", InterfaceOf(payload));
        var data = payload.GetProperty("data");
        var status = data.GetProperty("suit_status");
        Assert.Contains("MOD standard core", status.GetProperty("core_name").GetString());
        Assert.Equal(0, status.GetProperty("active").GetInt32());
        Assert.Equal("Unknown", data.GetProperty("user_status").GetProperty("user_name").GetString()); // Bob wears no ID
        var modules = data.GetProperty("module_info").EnumerateArray().Select(m => m.GetProperty("module_name").GetString()).ToList();
        Assert.Contains(modules, n => n.Contains("flashlight"));
        Assert.Contains(modules, n => n.Contains("storage"));
        Assert.Contains(modules, n => n.Contains("welding")); // gaps MO2: the department modules are live
        Assert.Equal(4, data.GetProperty("parts").GetArrayLength()); // the deployable parts (get_parts without the control unit)
        Assert.Equal("ntos", payload.GetProperty("static_data").GetProperty("ui_theme").GetString());

        string helmetRef = r.In(() => REF(mod.get_part_from_slot(ITEM_SLOT_HEAD)) as string);
        Act(r, bob, window, "deploy", ("ref", helmetRef));
        r.In(() => Assert.IsType<obj_item_clothing_head_mod>(bob.head));
        Act(r, bob, window, "lock");
        r.In(() => Assert.True(Dm.truthy(mod.locked)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmptyPaiCardIsRefusedAndOnlyTheAssistantMovesTheSuit()
    {
        var (r, feed, bob) = Room(9409);
        var mod = WornSuit(r, bob);
        var card = Make<obj_item_pai_card>(r, bob.loc);
        r.In(() => Assert.Equal(FALSE, Waiting.NoSleep(mod.insert_pai(bob, card))));
        Assert.True(feed.Any("balloon: pAI unresponsive!"));
        r.In(() =>
        {
            Assert.Null(mod.ai_assistant);
            Assert.Equal(FALSE, Waiting.NoSleep(mod.relaymove(bob, NORTH)));
            Assert.Equal(FALSE, Waiting.NoSleep(mod.remove_pai(bob)));
        });
        Assert.True(feed.Any("balloon: no pAI!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSeamKeepsTheNinjasInertModulesOutOfASuit()
    {
        // D132 seam, narrowed by gaps MO2: the department modules are live and installed; the space ninja's (M10+) stay inert
        var (r, feed, bob) = Room(9410);
        Assert.False(InertSeam.IsInertType(typeof(obj_item_mod_module_welding)), "the welding module is live since gaps MO2");
        Assert.True(InertSeam.IsInertType(typeof(obj_item_mod_module_hacker)), "the ninja's hacker module is M10+'s");
        var mod = Make<obj_item_mod_control_pre_equipped_civilian>(r, bob.loc);
        r.In(() =>
        {
            Assert.Contains(Dm.each(mod.modules), m => m is obj_item_mod_module_welding);
            foreach (object module in Dm.each(mod.modules))
                Assert.False(InertSeam.IsInertType(module.GetType()), module.GetType().Name + " is inert");
        });
        var hacker = Make<obj_item_mod_module_hacker>(r, bob.loc);
        r.Command(() => Waiting.Detach(mod.install(hacker, bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(Dm.@in(hacker, mod.modules));
            Assert.Same(bob.loc, hacker.loc);
        });
        Assert.True(feed.Any("balloon: not ported yet"));

        // the engineering suit gets all its modules (gaps MO2); the ninja's suit gets its live ones only (storage, anti slip,
        // EMP shield, quick cuff), not its cloak, star dispenser, status readout and reinforced DNA lock
        var engineering = Make<obj_item_mod_control_pre_equipped_engineering>(r, bob.loc);
        r.In(() =>
        {
            Assert.Contains(Dm.each(engineering.modules), m => m is obj_item_mod_module_storage);
            Assert.Contains(Dm.each(engineering.modules), m => m is obj_item_mod_module_flashlight);
            Assert.Contains(Dm.each(engineering.modules), m => m is obj_item_mod_module_welding);
            Assert.Contains(Dm.each(engineering.modules), m => m is obj_item_mod_module_rad_protection);
            Assert.Contains(Dm.each(engineering.modules), m => m is obj_item_mod_module_tether);
            Assert.Contains(Dm.each(engineering.modules), m => m is obj_item_mod_module_magboot);
            foreach (object module in Dm.each(engineering.modules))
                Assert.False(InertSeam.IsInertType(module.GetType()), module.GetType().Name + " is inert");
        });
        var ninja = Make<obj_item_mod_control_pre_equipped_ninja>(r, bob.loc);
        r.In(() =>
        {
            Assert.Contains(Dm.each(ninja.modules), m => m is obj_item_mod_module_noslip);
            Assert.DoesNotContain(Dm.each(ninja.modules), m => m is obj_item_mod_module_stealth_ninja);
            foreach (object module in Dm.each(ninja.modules))
                Assert.False(InertSeam.IsInertType(module.GetType()), module.GetType().Name + " is inert");
        });

        // the wizard's enchanted suit: its soul shard core is the antagonists' (D333, inert), so the suit is made coreless
        var enchanted = Make<obj_item_mod_control_pre_equipped_enchanted>(r, bob.loc);
        r.In(() =>
        {
            Assert.True(InertSeam.IsInertType(typeof(obj_item_mod_core_soul_wizard)));
            Assert.Null(enchanted.core);
            Assert.Equal(4, Dm.each(enchanted.get_parts()).Count());
        });
        r.AssertNoRuntimes();
    }
}
