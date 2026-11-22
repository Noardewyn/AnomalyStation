using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>A test power source (fixture, rules 15): a power machine that adds its output to its network every SSmachines
/// cycle in its process step, as tg's generators do (a PACMAN's process, port_gen.dm), so the APCs read it as the next cycle's
/// avail (powernet.dm:92).</summary>
[DmPath("/obj/machinery/power/test_m7apc_source")]
public class obj_machinery_power_test_m7apc_source : obj_machinery_power
{
    public float output_energy;

    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (output_energy > 0)
            add_avail(output_energy);
        return null;
    }
}

/// <summary>
/// M7 slice 3: tg's APC (modules/power/apc/*.dm, power.dm:135-245, controllers/subsystem/machines.dm:114-139) in a booted toy
/// room whose area requires power: the APC made at map load with tg's charged cell (start_charge 90 % of an upgraded megacell)
/// and its terminal on the cable under it, the channels set by the cell's charge at tg's thresholds (late_process: 30 % the
/// equipment, 15 % the lights, 0 everything), the static usage drawn from the cell without a grid and from the grid with one,
/// the cell charged at tg's capped rate per channel step (charge_channel), the area's machines and lights unpowered by the
/// channels and powered again, the breaker, the window's acts, the cover, the cell and the lock, the EMP and the flicker.
/// </summary>
public class ApcTests
{
    private const int Second = 20;
    private const int ApcX = 3, ApcY = 2, LampX = 5, SourceX = 7, DoorX = 9, DoorY = 5, VentX = 10, VentY = 6;
    private const string Left = "icon-x=16;icon-y=16;left=1;button=left";
    private const string Right = "icon-x=16;icon-y=16;right=1;button=right";

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_power_apc Apc, obj_machinery_light Lamp,
        obj_machinery_power_test_m7apc_source Source, obj_machinery_door_airlock Door, obj_machinery_atmospherics_components_unary_vent_pump Vent, area Room);

    private static Rig Build(ulong seed)
    {
        obj_machinery_power_apc apc = null;
        obj_machinery_light lamp = null;
        obj_machinery_power_test_m7apc_source source = null;
        obj_machinery_door_airlock door = null;
        obj_machinery_atmospherics_components_unary_vent_pump vent = null;
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            // the room's area needs power (tg's station areas: requires_power, areas.dm:79), so its APC runs it
            ((area)GLOB.areas_by_type[typeof(area_misc_testroom)]).requires_power = TRUE;
            // the cables under the floor (the room's own turfs: the area's turf lists, which get_lights reads, keep them)
            for (int x = 2; x <= SourceX; x++)
                Dm.@new<obj_structure_cable>(locate(x, ApcY, 1));
            apc = Dm.@new<obj_machinery_power_apc_auto_name_directional_south>(locate(ApcX, ApcY, 1));
            lamp = Dm.@new<obj_machinery_light_directional_south>(locate(LampX, ApcY, 1));
            source = Dm.@new<obj_machinery_power_test_m7apc_source>(locate(SourceX, ApcY, 1));
            door = Dm.@new<obj_machinery_door_airlock>(locate(DoorX, DoorY, 1));
            vent = Dm.@new<obj_machinery_atmospherics_components_unary_vent_pump_on>(locate(VentX, VentY, 1));
        });
        mob_living_carbon_human bob = r.Join(ApcX, ApcY + 1, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.In(() => { lamp.allow_break_on_init = FALSE; if (!Dm.eq(lamp.status, LIGHT_OK)) Waiting.NoSleep(lamp.fix()); });
        r.Step(Second);
        return new Rig(r, bob, apc, lamp, source, door, vent, r.In(() => (area)GLOB.areas_by_type[typeof(area_misc_testroom)]));
    }

    /// <summary>Whole SSmachines cycles (its wait in ticks): each runs every APC step once.</summary>
    private static void MachinesTick(AudioRoom r, int runs = 1) => r.Step(runs * r.In(() => (int)Dm.num(SSmachines.wait)) * 2);

    private static float SecondsPerTick(AudioRoom r) => r.In(() => Dm.num(SSmachines.wait) / 10);

    private static void Charge(Rig g, float percent) => g.R.In(() => g.Apc.cell.charge = g.Apc.cell.maxcharge * percent / 100);

    private static (float Equip, float Light, float Environ) Channels(Rig g) => g.R.In(() => (Dm.num(g.Apc.equipment), g.Apc.lighting, g.Apc.environ));

    private static (bool Equip, bool Light, bool Environ) AreaPower(Rig g) => g.R.In(() =>
        (Dm.truthy(g.Room.power_equip), Dm.truthy(g.Room.power_light), Dm.truthy(g.Room.power_environ)));

    private static bool NoPower(Rig g, obj_machinery m) => g.R.In(() => Dm.truthy(Dm.bitand(m.machine_stat, NOPOWER)));

    private static List<string> Overlays(Rig g) => g.R.In(() => Dm.each(g.Apc.overlays).Select(o => o switch
    {
        string t => t,
        mutable_appearance m => m.icon_state,
        DmAppearance d => d.icon_state,
        _ => null,
    }).Where(s => s != null).ToList());

    private static void Click(AudioRoom r, mob who, atom target, string how = Left, float seconds = 1)
    {
        r.Command(() => ClientSeam.Click(who.client, target, how));
        r.Step(Math.Max(2, (int)(seconds * Second)));
    }

    private static List<TguiOutput> Take(mob who)
    {
        var taken = who.client.dm_tgui_output.ToList();
        who.client.dm_tgui_output.Clear();
        return taken;
    }

    private static JsonElement LastData(List<TguiOutput> outputs, out string windowId)
    {
        windowId = null;
        JsonElement found = default;
        foreach (TguiOutput o in outputs)
        {
            if (o.Kind != TguiOutputKind.Message)
                continue;
            using JsonDocument doc = JsonDocument.Parse(o.Json);
            if (doc.RootElement.GetProperty("type").GetString() != "update")
                continue;
            found = doc.RootElement.GetProperty("payload").Clone();
            windowId = o.WindowId;
        }
        Assert.True(found.ValueKind == JsonValueKind.Object, "no update message");
        return found;
    }

    private static void Act(AudioRoom r, mob who, string windowId, string action, DmList payload = null)
    {
        r.Command(() => ClientSeam.TguiMessage(who.client, windowId, "act/" + action, payload));
        r.Step(2);
    }

    /// <summary>Opens the APC's window with an empty hand and unlocks it with a debug ID (togglelock), returning the window.</summary>
    private static string OpenUnlocked(Rig g)
    {
        Take(g.Bob);
        Click(g.R, g.Bob, g.Apc);
        LastData(Take(g.Bob), out string window);
        var card = WallTests.Hold<obj_item_card_id_advanced_debug>(g.R, g.Bob);
        Click(g.R, g.Bob, g.Apc);
        Assert.Equal(0f, g.R.In(() => g.Apc.locked));
        g.R.In(() => qdel(card));
        g.R.Step(2);
        return window;
    }

    [Fact]
    public void AnApcMadeAtMapLoadHasTgsChargedCellItsTerminalAndPowersItsArea()
    {
        var g = Build(7301);
        g.R.In(() =>
        {
            obj_machinery_power_apc apc = g.Apc;
            // Initialize (apc_main.dm:176): its area, the processing list, the access, the name, the wires
            Assert.Same(g.Room, apc.area);
            Assert.Same(apc, g.Room.apc);
            Assert.True(Dm.@in(apc, SSmachines.processing_apcs));
            Assert.True(Dm.truthy(Dm.bitand(apc.datum_flags, DF_ISPROCESSING)));
            Assert.True(Dm.@in(ACCESS_ENGINE_EQUIP, apc.req_access));
            Assert.Contains("Test Room APC", Dm.str(apc.name)); // auto_name (apc_main.dm:202)
            Assert.IsType<datum_wires_apc>(apc.wires);
            Assert.Equal(7f, Dm.num(apc.wires.wires.len) - 6); // the seven wires and six duds (add_duds(6), wires/apc.dm:15)
            // mapload: the secured board, the cell at start_charge, the terminal on the cable under it
            Assert.Equal(APC_ELECTRONICS_SECURED, apc.has_electronics);
            Assert.IsType<obj_item_stock_parts_power_store_battery_upgraded>(apc.cell);
            Assert.Same(apc, apc.cell.loc);
            Assert.Equal(2500000f, apc.cell.maxcharge);
            obj_machinery_power_terminal terminal = apc.terminal;
            Assert.NotNull(terminal);
            Assert.Same(apc.loc, terminal.loc);
            Assert.Same(apc, terminal.master);
            obj_structure_cable under = Dm.locate<obj_structure_cable>(apc.loc);
            Assert.Same(under.powernet, terminal.powernet);
            Assert.Same(terminal, ((datum_powernet)terminal.powernet).nodes[terminal]);
            Assert.False(Dm.truthy(((datum_powernet)terminal.powernet).nodes[apc]), "an APC is reached through its terminal");
            Assert.Equal(-APC_PIXEL_OFFSET, apc.pixel_y); // setDir (:260): south
            // all channels on automatic, the breaker on, the area powered by update() (the 0.5 s timer, :229)
            Assert.True(Dm.truthy(apc.operating));
            Assert.Equal((3f, 3f, 3f), (Dm.num(apc.equipment), apc.lighting, apc.environ));
            Assert.True(Dm.truthy(g.Room.power_equip) && Dm.truthy(g.Room.power_light) && Dm.truthy(g.Room.power_environ));
            Assert.True(Dm.truthy(g.Lamp.on));
            Assert.False(Dm.truthy(Dm.bitand(g.Door.machine_stat, NOPOWER)));
        });
        float spt = SecondsPerTick(g.R);
        float before = g.R.In(() => g.Apc.cell.charge);
        float statics = g.R.In(() => Dm.num(g.Room.energy_usage[AREA_USAGE_STATIC_LIGHT]) + Dm.num(g.Room.energy_usage[AREA_USAGE_STATIC_EQUIP]) + Dm.num(g.Room.energy_usage[AREA_USAGE_STATIC_ENVIRON]));
        Assert.True(statics > 0, "the lamp and the machines have static usage");
        MachinesTick(g.R);
        g.R.In(() =>
        {
            // early_process (:565): without a grid the static usage of the three channels comes from the cell
            Assert.Equal(before - statics, g.Apc.cell.charge, 1);
            Assert.Equal(APC_NO_POWER, g.Apc.main_status); // late_process: no avail on the network
            Assert.Equal(APC_NOT_CHARGING, Dm.num(g.Apc.charging));
            Assert.Equal(statics, g.Apc.lastused_total, 1);
            // the appearance (apc_appearance.dm): closed and working, the lock, charge and channel lights
            Assert.Equal("apc0", g.Apc.icon_state);
        });
        List<string> overlays = Overlays(g);
        Assert.Contains("apcox-1", overlays);
        Assert.Contains("apco3-0", overlays);
        Assert.Contains("apco0-3", overlays);
        Assert.Contains("apco1-3", overlays);
        Assert.Contains("apco2-3", overlays);
        Assert.Equal(90f, g.R.In(() => Dm.num(g.Apc.cell.percent())), 0);
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheChannelsGoOffAtTgsCellThresholdsAndTheAreasMachinesLoseTheirPower()
    {
        var g = Build(7302);
        // late_process (:635-681): under 30 % the equipment goes off (automatic channels only)
        Charge(g, 29);
        MachinesTick(g.R);
        Assert.Equal((1f, 3f, 3f), Channels(g));
        Assert.Equal((false, true, true), AreaPower(g));
        // under 15 % the lights too, the environment stays
        Charge(g, 14);
        MachinesTick(g.R);
        Assert.Equal((1f, 1f, 3f), Channels(g));
        Assert.Equal((false, false, true), AreaPower(g));
        g.R.In(() =>
        {
            Assert.False(Dm.truthy(g.Lamp.on));
            Assert.True(Dm.truthy(g.Lamp.low_power_mode), "the lamp is on its emergency cell");
            // the low power switches the lights to the night shift (:651-653, set_nightshift)
            Assert.True(Dm.truthy(g.Apc.low_power_nightshift_lights));
            Assert.True(Dm.truthy(g.Apc.nightshift_lights));
            Assert.True(Dm.truthy(g.Lamp.nightshift_enabled));
        });
        Assert.False(NoPower(g, g.Door), "the environment channel still powers the door");
        Assert.False(NoPower(g, g.Vent));
        // a manually set channel stays on until the cell is empty (autoset, apc_power_proc.dm:94)
        g.R.In(() => { g.Apc.lighting = APC_CHANNEL_ON; g.Apc.update(); });
        MachinesTick(g.R);
        Assert.Equal((1f, 2f, 3f), Channels(g));
        Assert.True(AreaPower(g).Light);
        // empty: every channel is forced off, the door and the vent lose their power
        Charge(g, 0);
        MachinesTick(g.R);
        Assert.Equal((1f, 0f, 1f), Channels(g));
        Assert.Equal((false, false, false), AreaPower(g));
        Assert.True(NoPower(g, g.Door), "the door is unpowered");
        Assert.True(NoPower(g, g.Vent), "the vent is unpowered");
        g.R.In(() =>
        {
            Assert.False(Dm.truthy(g.Door.hasPower()));
            Assert.False(Dm.truthy(g.Vent.is_operational));
            Assert.True(Dm.truthy(g.Lamp.low_power_mode));
        });
        // the icon follows at the next late_process (queue_icon_update, apc_appearance.dm:107)
        MachinesTick(g.R);
        Assert.Contains("apco1-0", Overlays(g));
        Assert.Contains("apco2-1", Overlays(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void WithGridPowerTheStaticUsageComesFromTheGridAndTheCellChargesAtTgsRateRestoringTheChannels()
    {
        var g = Build(7303);
        Charge(g, 0);
        MachinesTick(g.R);
        Assert.Equal((1f, 1f, 1f), Channels(g));
        Assert.True(NoPower(g, g.Door));
        // the source feeds the network; its output is avail from the next cycle on (powernet.dm:92)
        g.R.In(() => g.Source.output_energy = 1000000);
        float spt = SecondsPerTick(g.R);
        float rate = g.R.In(() => Math.Min(g.Apc.cell.chargerate, g.Apc.cell.maxcharge * 0.01f)) * spt; // CHARGELEVEL, charge_channel (:709)
        Assert.Equal(50000f, rate);
        MachinesTick(g.R); // the source's first output
        MachinesTick(g.R);
        g.R.In(() =>
        {
            // charge_channel (:692): the environment step charges first, at most the capped rate per cycle
            Assert.Equal(rate, g.Apc.cell.charge, 1);
            Assert.Equal(APC_CHARGING, Dm.num(g.Apc.charging));
            Assert.Equal(APC_HAS_POWER, g.Apc.main_status);
            Assert.Equal(rate, g.Apc.lastused_charge, 1);
        });
        // any charge brings the environment back (2 %), the lights at 15 %, the equipment at 30 %
        Assert.Equal((1f, 1f, 3f), Channels(g));
        Assert.False(NoPower(g, g.Door), "the environment channel powers the door again");
        Assert.False(NoPower(g, g.Vent));
        int toLights = (int)Math.Ceiling(0.15f * 2500000f / rate) - 1;
        MachinesTick(g.R, toLights);
        Assert.Equal((1f, 3f, 3f), Channels(g));
        g.R.In(() =>
        {
            Assert.True(Dm.truthy(g.Lamp.on), "the lamp is on again");
            Assert.False(Dm.truthy(g.Lamp.low_power_mode));
        });
        int toEquip = (int)Math.Ceiling(0.30f * 2500000f / rate) - toLights - 1;
        MachinesTick(g.R, toEquip);
        Assert.Equal((3f, 3f, 3f), Channels(g));
        Assert.Equal((true, true, true), AreaPower(g));
        // with the grid the static usage comes from it, so the cell only gains the charge
        float before = g.R.In(() => g.Apc.cell.charge);
        MachinesTick(g.R);
        Assert.Equal(before + rate, g.R.In(() => g.Apc.cell.charge), 1);
        // full: APC_FULLY_CHARGED, the green screen light
        g.R.In(() => g.Apc.cell.charge = g.Apc.cell.maxcharge - 10);
        MachinesTick(g.R);
        g.R.In(() =>
        {
            Assert.Equal(g.Apc.cell.maxcharge, g.Apc.cell.charge);
            Assert.Equal(APC_FULLY_CHARGED, Dm.num(g.Apc.charging));
        });
        MachinesTick(g.R);
        g.R.In(() => Assert.Equal(LIGHT_COLOR_GREEN, g.Apc.light_color));
        Assert.Contains("apco3-2", Overlays(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheBreakerInTheWindowCutsTheAreaAndTurnsItBackOnWithTgsSounds()
    {
        var g = Build(7304);
        string window = OpenUnlocked(g);
        int offs = g.R.In(() => g.R.Sounds.Heard.Count(h => h.File == "sound/machines/terminal/terminal_off.ogg"));
        Act(g.R, g.Bob, window, "breaker");
        g.R.In(() => Assert.False(Dm.truthy(g.Apc.operating)));
        Assert.Equal((false, false, false), AreaPower(g));
        Assert.True(NoPower(g, g.Door));
        Assert.True(g.R.In(() => g.R.Sounds.Heard.Count(h => h.File == "sound/machines/terminal/terminal_off.ogg")) > offs, "update() plays terminal_off");
        JsonElement data = LastData(Take(g.Bob), out _).GetProperty("data");
        Assert.Equal(0, data.GetProperty("isOperating").GetInt32());
        // the breaker off: only the lock and the charge lights (update_overlays, apc_appearance.dm:53)
        List<string> overlays = Overlays(g);
        Assert.Contains("apcox-0", overlays);
        Assert.DoesNotContain(overlays, o => o.StartsWith("apco0-") || o.StartsWith("apco1-") || o.StartsWith("apco2-"));
        // off, early_process draws nothing
        float before = g.R.In(() => g.Apc.cell.charge);
        MachinesTick(g.R);
        Assert.Equal(before, g.R.In(() => g.Apc.cell.charge));
        Act(g.R, g.Bob, window, "breaker");
        g.R.In(() => Assert.True(Dm.truthy(g.Apc.operating)));
        Assert.Equal((true, true, true), AreaPower(g));
        Assert.False(NoPower(g, g.Door));
        Assert.Contains(g.R.Sounds.Heard, h => h.File == "sound/machines/terminal/terminal_on.ogg");
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheWindowsActsSetTheChannelsTheCoverLockTheChargeModeAndTheLights()
    {
        var g = Build(7305);
        Take(g.Bob);
        Click(g.R, g.Bob, g.Apc);
        JsonElement payload = LastData(Take(g.Bob), out string window);
        Assert.Equal("Apc", payload.GetProperty("config").GetProperty("interface").GetProperty("name").GetString());
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(1, data.GetProperty("locked").GetInt32());
        Assert.Equal(3, data.GetProperty("powerChannels").GetArrayLength());
        Assert.Equal("Equipment", data.GetProperty("powerChannels")[0].GetProperty("title").GetString());
        Assert.Equal(3, data.GetProperty("powerChannels")[1].GetProperty("topicParams").GetProperty("auto").GetProperty("lgt").GetInt32());
        Assert.InRange(data.GetProperty("powerCellStatus").GetDouble(), 89, 90);
        // locked: the acts are refused (ui_act :480) but the night shift's
        Act(g.R, g.Bob, window, "cover");
        Assert.Equal(1f, g.R.In(() => g.Apc.coverlocked));
        // the night breaker waits 10 s from the round's start (last_nightshift_switch > world.time - 10 SECONDS)
        g.R.Step(10 * Second);
        Act(g.R, g.Bob, window, "toggle_nightshift");
        g.R.In(() => { Assert.True(Dm.truthy(g.Apc.nightshift_lights)); Assert.True(Dm.truthy(g.Lamp.nightshift_enabled)); });
        Act(g.R, g.Bob, window, "toggle_nightshift"); // the night breaker is cycling (10 s, apc_power_proc.dm:24)
        g.R.In(() => Assert.True(Dm.truthy(g.Apc.nightshift_lights)));

        window = OpenUnlocked(g);
        Act(g.R, g.Bob, window, "cover");
        Assert.Equal(0f, g.R.In(() => g.Apc.coverlocked));
        Act(g.R, g.Bob, window, "charge");
        g.R.In(() => { Assert.Equal(0f, g.Apc.chargemode); Assert.Equal(APC_NOT_CHARGING, Dm.num(g.Apc.charging)); });
        Act(g.R, g.Bob, window, "charge");
        Assert.Equal(1f, g.R.In(() => g.Apc.chargemode));
        // setsubsystem (apc_power_proc.dm:118): 1 off, 2 on, 3 automatic
        Act(g.R, g.Bob, window, "channel", g.R.In(() => Dm.list(Dm.kv("lgt", 1))));
        Assert.Equal(APC_CHANNEL_OFF, g.R.In(() => g.Apc.lighting));
        Assert.False(AreaPower(g).Light);
        g.R.In(() => Assert.True(Dm.truthy(g.Lamp.low_power_mode)));
        Act(g.R, g.Bob, window, "channel", g.R.In(() => Dm.list(Dm.kv("eqp", 2))));
        Assert.Equal(APC_CHANNEL_ON, g.R.In(() => Dm.num(g.Apc.equipment)));
        Act(g.R, g.Bob, window, "channel", g.R.In(() => Dm.list(Dm.kv("env", 1))));
        Assert.True(NoPower(g, g.Door));
        Act(g.R, g.Bob, window, "channel", g.R.In(() => Dm.list(Dm.kv("env", 3))));
        Act(g.R, g.Bob, window, "channel", g.R.In(() => Dm.list(Dm.kv("lgt", 3))));
        Assert.Equal((2f, 3f, 3f), Channels(g));
        Assert.Equal((true, true, true), AreaPower(g));
        Assert.False(NoPower(g, g.Door));
        g.R.In(() => Assert.False(Dm.truthy(g.Lamp.low_power_mode)));
        // the emergency lights (:538): the lamps' no_low_power
        Act(g.R, g.Bob, window, "emergency_lighting");
        g.R.In(() => { Assert.True(Dm.truthy(g.Apc.emergency_lights)); Assert.True(Dm.truthy(g.Lamp.no_low_power)); });
        data = LastData(Take(g.Bob), out _).GetProperty("data");
        Assert.Equal(0, data.GetProperty("emergencyLights").GetInt32());
        Assert.Equal(1, data.GetProperty("nightshiftLights").GetInt32());
        Assert.Equal(2, data.GetProperty("powerChannels")[0].GetProperty("status").GetInt32());
        Act(g.R, g.Bob, window, "emergency_lighting");
        g.R.In(() => Assert.False(Dm.truthy(g.Lamp.no_low_power)));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheIdLocksTheCoverOpensWithACrowbarTheCellComesOutAndGoesBackAndTheScrewdriverShowsTheWires()
    {
        var g = Build(7306);
        // the cover is locked: the crowbar is refused (crowbar_act :252)
        WallTests.Hold<obj_item_crowbar>(g.R, g.Bob);
        Click(g.R, g.Bob, g.Apc);
        Assert.Equal(APC_COVER_CLOSED, g.R.In(() => g.Apc.opened));
        g.R.In(() => g.Apc.coverlocked = FALSE);
        Click(g.R, g.Bob, g.Apc);
        Assert.Equal(APC_COVER_OPENED, g.R.In(() => g.Apc.opened));
        Assert.Equal("apc2", g.R.In(() => g.Apc.icon_state)); // open with a cell (update_icon_state :28)
        Assert.Empty(Overlays(g));
        // an empty hand takes the cell out (attack_hand :118)
        obj_item_stock_parts_power_store cell = g.R.In(() => g.Apc.cell);
        WallTests.Hold<obj_item_wirecutters>(g.R, g.Bob);
        g.R.In(() => { foreach (object held in Dm.each(g.Bob.held_items.Copy())) if (held is obj_item i) qdel(i); });
        Click(g.R, g.Bob, g.Apc);
        g.R.In(() =>
        {
            Assert.Null(g.Apc.cell);
            Assert.True(Dm.@in(cell, g.Bob.held_items));
            Assert.Equal(APC_NOT_CHARGING, Dm.num(g.Apc.charging));
            Assert.Equal("apc1", g.Apc.icon_state);
        });
        // no cell: late_process forces every channel off
        MachinesTick(g.R);
        Assert.Equal((1f, 1f, 1f), Channels(g));
        Assert.True(NoPower(g, g.Door));
        // the cell in the hand goes back (cell_act :55)
        Click(g.R, g.Bob, g.Apc);
        g.R.In(() => { Assert.Same(cell, g.Apc.cell); Assert.Same(g.Apc, cell.loc); Assert.Equal("apc2", g.Apc.icon_state); });
        MachinesTick(g.R);
        Assert.Equal((3f, 3f, 3f), Channels(g));
        // the crowbar closes it again and relocks the cover
        WallTests.Hold<obj_item_crowbar>(g.R, g.Bob);
        Click(g.R, g.Bob, g.Apc);
        g.R.In(() => { Assert.Equal(APC_COVER_CLOSED, g.Apc.opened); Assert.Equal(1f, g.Apc.coverlocked); Assert.Equal("apc0", g.Apc.icon_state); });
        // the screwdriver exposes the wires (screwdriver_act :312)
        WallTests.Hold<obj_item_screwdriver>(g.R, g.Bob);
        Click(g.R, g.Bob, g.Apc);
        g.R.In(() => { Assert.Equal(1f, g.Apc.panel_open); Assert.Equal("apcewires", g.Apc.icon_state); });
        Click(g.R, g.Bob, g.Apc);
        g.R.In(() => Assert.Equal("apc0", g.Apc.icon_state));
        // the ID's lock (togglelock :476): the debug card has the engineering access, a bare card does not
        WallTests.Hold<obj_item_card_id>(g.R, g.Bob);
        Click(g.R, g.Bob, g.Apc);
        Assert.Equal(1f, g.R.In(() => g.Apc.locked));
        WallTests.Hold<obj_item_card_id_advanced_debug>(g.R, g.Bob);
        Click(g.R, g.Bob, g.Apc);
        Assert.Equal(0f, g.R.In(() => g.Apc.locked));
        Assert.Contains("apcox-0", Overlays(g));
        Click(g.R, g.Bob, g.Apc);
        Assert.Equal(1f, g.R.In(() => g.Apc.locked));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmpTurnsTheChannelsOffForAMinute()
    {
        var g = Build(7307);
        g.R.In(() => Waiting.NoSleep(g.Apc.emp_act(EMP_HEAVY)));
        // emp_act (apc_tool_act.dm:460): every channel off, the area unpowered, the reset in 60 s
        Assert.Equal((0f, 0f, 0f), Channels(g));
        Assert.Equal((false, false, false), AreaPower(g));
        Assert.True(NoPower(g, g.Door));
        g.R.Step(61 * Second);
        // reset(APC_RESET_EMP) (apc_main.dm:734): the equipment and the environment back on automatic, the lights stay off
        Assert.Equal((3f, 0f, 3f), Channels(g));
        Assert.Equal((true, false, true), AreaPower(g));
        Assert.False(NoPower(g, g.Door));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ThePowerNetworksFlickerReachesTheApcsLights()
    {
        var g = Build(7308);
        g.R.In(() =>
        {
            // get_lights (apc_main.dm:553): the fixtures on the area's turfs
            Assert.Contains(g.Lamp, Dm.each_cast<obj_machinery_light>(g.Apc.get_lights()));
            // the flicker (light.dm:542) returns TRUE at once and dims the lamp to the low-power light
            Assert.True(Dm.truthy(g.Lamp.flicker(1)));
            Assert.Equal(8f * 0.25f, Dm.num(g.Lamp.light_range), 3);
        });
        g.R.Step(10 * Second);
        g.R.In(() => { Assert.False(Dm.truthy(g.Lamp.flickering)); Assert.True(Dm.truthy(g.Lamp.on)); Assert.Equal(8f, Dm.num(g.Lamp.light_range), 3); });
        // propagate_light_flicker (powernet.dm:108): the network's APCs flicker their lights
        datum_powernet net = g.R.In(() => (datum_powernet)g.Apc.terminal.powernet);
        g.R.In(() => net.propagate_light_flicker());
        g.R.In(() => Assert.True(Dm.truthy(net.flickering)));
        g.R.AssertNoRuntimes();
    }
}
