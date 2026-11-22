using System.Globalization;
using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

/// <summary>
/// Repeatable render check for model states, using the game's real replica, atlas and chunk renderer.
/// Run ModelShowcase.tscn with --screenshot-after=1,/abs/closed.png --screenshot-after=3,/abs/open.png.
/// Every two seconds source appearances switch, including nested overlays and repeated display glyphs.
/// </summary>
public partial class ModelShowcase : Node3D
{
    private readonly List<(long Id, TableRef First, TableRef Second)> _states = new();
    private ReplicaWorld _replica;
    private MapRenderer _map;
    private double _seconds;
    private int _phase;
    private long _tick = 1;
    private int _checks;
    private int _expectedModels;

    public override void _Ready()
    {
        var world = new ShowcaseWorld();
        world.BuildRoom(17, 11);
        const string wall = "icons/obj/wallmounts.dmi", machineWall = "icons/obj/machines/wallmounts.dmi";
        TableRef Look(string icon, string state, params string[] overlays)
        {
            TableRef reference = world.Item(icon, state);
            // Simple icon appearances are interned. Build a separate root to leave the base unchanged.
            var entry = new AppearanceEntry();
            world.Finish();
            AppearanceEntry simple = world.Snapshot.Appearances[(int)reference.Index - 1];
            entry.Icon = simple.Icon;
            entry.IconState = simple.IconState;
            foreach (string overlay in overlays) entry.Overlays.Add(world.Item(icon, overlay));
            return world.Add(entry);
        }
        void Fixture(string type, string name, int x, int y, TableRef first, TableRef second, bool model = true, byte dir = ByondDir.North)
        {
            _states.Add((world.AddEntity(type, name, x, y, first, dir), first, second));
            if (model) _expectedModels++;
        }
        // ANOMALY_SHOWCASE_MODELS=id,id,...: those catalog entries in rows facing the camera instead, each switching between its first state and its open (or last) one.
        string set = System.Environment.GetEnvironmentVariable("ANOMALY_SHOWCASE_MODELS");
        if (!string.IsNullOrEmpty(set))
        {
            var catalog = Anomaly.Client.Shared.Models.ModelCatalog.Parse(System.IO.File.ReadAllText(Anomaly.Client.Shared.Models.ModelCatalog.Locate()));
            int n = 0, floor = 0;
            foreach (string id in set.Split(','))
            {
                var e = catalog.Entries.First(m => m.Id == id);
                string icon = e.Icons[0], first = e.States.FirstOrDefault() ?? "";
                string[] needs = e.Overlays.Concat(e.AllOverlays).Distinct().Take(1).ToArray();
                TableRef a = Look(icon, first, needs);
                TableRef b = e.OpenStates.Length > 0 ? Look(icon, e.OpenStates[0], needs)
                    : e.OpenOverlays.Length > 0 ? Look(icon, first, needs.Concat(e.OpenOverlays.Take(1)).ToArray())
                    : Look(icon, e.States.LastOrDefault() ?? first, needs);
                if (id == "computer")
                {
                    a = Look(icon, first, "generic", "generic_key");
                    b = Look(icon, first, "computer_broken", "generic_key_off");
                }
                else if (id == "computer_ratvar")
                {
                    a = Look(icon, first, "ratvar1", "ratvar_key1");
                    b = Look(icon, "ratvarcomputer2", "ratvar2", "ratvar_key2");
                }
                else if (id == "modular_console")
                {
                    a = Look(icon, "console", "menu");
                    b = Look(icon, "console-off", "bsod", "broken");
                }
                else if (id == "medical_laptop")
                {
                    a = Look(icon, first, "medlaptop", "laptop_key");
                    b = Look(icon, first, "laptop_broken", "laptop_key_off");
                }
                if (e.Rule == Anomaly.Client.Shared.Models.ModelRule.Wall)
                    Fixture("/obj/machinery/firealarm", id, 2 + n++ * 2 % 16, 10, a, b);
                else
                {
                    Fixture("/obj/machinery/showcase", id, 2 + floor % 8 * 2, 7 - floor / 8 * 2, a, b, dir: ByondDir.South);
                    floor++;
                }
            }
        }
        else
        {
            Fixture("/obj/structure/fireaxecabinet", "cabinet", 2, 10,
                Look(wall, "fireaxe", "axe", "glass", "locked"), Look(wall, "fireaxe", "glass4_raised"));
            const string defib = "icons/obj/machines/defib_mount.dmi";
            TableRef nested = Look(defib, "defib", "charge25", "clamps", "emagged");
            TableRef mount = Look(defib, "defibrillator_mount");
            world.Finish();
            var withDefib = world.Snapshot.Appearances[(int)mount.Index - 1].WithOverlays(_ => true);
            withDefib.Overlays.Add(nested);
            Fixture("/obj/machinery/defibrillator_mount", "defib", 4, 10, world.Add(withDefib), Look(defib, "defibrillator_mount"));
            TableRef Slots(string icon, string state, params (string State, short Offset)[] slots)
            {
                TableRef reference = Look(icon, state);
                world.Finish();
                var entry = world.Snapshot.Appearances[(int)reference.Index - 1].WithOverlays(_ => false);
                foreach (var slot in slots)
                {
                    TableRef child = world.Item(icon, slot.State);
                    world.Finish();
                    var shifted = world.Snapshot.Appearances[(int)child.Index - 1].WithOverlays(_ => true);
                    shifted.PixelW = slot.Offset;
                    entry.Overlays.Add(world.Add(shifted));
                }
                return world.Add(entry);
            }
            const string clock = "icons/obj/digital_clock.dmi", healer = "icons/obj/machines/wall_healer.dmi";
            Fixture("/obj/machinery/digital_clock", "clock", 6, 10,
                Slots(clock, "digital_clock_base", ("+1", -14), ("+1", -10), ("+1", -4), ("+1", 0), ("+separator", 0)),
                Slots(clock, "digital_clock_base", ("+8", -14), ("+8", -10), ("+9", -4), ("+9", 0), ("+separator", 0)));
            Fixture("/obj/machinery/wall_healer", "healer", 8, 10,
                Slots(healer, "wall_healer", ("bar0", 0), ("bar2", 4), ("bar4", 8), ("bar6", 12), ("bar_emissive", 0), ("bar_shadow", 0)),
                Slots(healer, "wall_healer", ("bar7", 0), ("bar0", 4), ("bar5", 8), ("bar1", 12), ("open", 0), ("bar_shadow", 0)));
            const string turret = "icons/obj/machines/turret_control.dmi";
            Fixture("/obj/machinery/turretid", "turret", 10, 10,
                Look(turret, "control", "kill", "button_left", "button_right", "emissive_screen"), Look(turret, "control", "button_left"));
            const string screen = "icons/obj/machines/status_display.dmi";
            Fixture("/obj/machinery/computer/security/telescreen/entertainment", "tv", 12, 10,
                Look(screen, "entertainment_frame", "entertainment"), Look(screen, "entertainment_frame"));
            Fixture("/obj/machinery/door_buttons/access_button", "access", 14, 10,
                Look(machineWall, "access_button_cycle"), Look(machineWall, "access_button_off"));
            Fixture("/obj/structure/sign/picture_frame", "photo", 16, 10,
                Look("icons/obj/signs.dmi", "frame-overlay"), Look("icons/obj/signs.dmi", "frame-overlay", "frame-monkey"));
            const string unary = "icons/obj/machines/atmospherics/unary_devices.dmi";
            Fixture("/obj/machinery/atmospherics/components/unary/vent_pump", "vent", 5, 8,
                Look(unary, "vent_out", "overclocked"), Look(unary, "vent_welded"), model: false);
            Fixture("/obj/machinery/atmospherics/components/unary/vent_scrubber", "scrubber", 8, 8,
                Look(unary, "scrub_wide"), Look(unary, "scrub_off"), model: false);
            const string cameraIcon = "icons/obj/machines/camera.dmi";
            Fixture("/obj/machinery/camera", "camera", 3, 10,
                Look(cameraIcon, "camera", "camera_on"), Look(cameraIcon, "camera_off", "camera_emp"), model: false);
            const string pump = "icons/obj/machines/atmospherics/binary_devices.dmi";
            Fixture("/obj/machinery/atmospherics/components/binary/pump", "pump", 11, 8,
                Look(pump, "pump_on-1"), Look(pump, "pump_off-1"));
            // The south wall reproduces tg's mapped plumbing offsets; floor equipment uses its source plane/layer.
            TableRef Located(TableRef reference, short pixelY = 0, short plane = 0, float layer = 0)
            {
                world.Finish();
                var entry = world.Snapshot.Appearances[(int)reference.Index - 1].WithOverlays(_ => true);
                entry.PixelY = pixelY; entry.Plane = plane; entry.Layer = layer;
                return world.Add(entry);
            }
            const string plumbing = "icons/obj/watercloset.dmi";
            TableRef basin = Located(Look(plumbing, "sink"), -14);
            TableRef kitchen = Located(Look(plumbing, "sink_alt"), -16);
            TableRef shower = Located(Look(plumbing, "shower"), -16);
            Fixture("/obj/structure/sink/directional/north", "basin", 6, 2, basin, basin);
            Fixture("/obj/structure/sink/kitchen/directional/north", "kitchen sink", 8, 2, kitchen, kitchen);
            Fixture("/obj/machinery/shower/directional/north", "shower", 10, 2, shower, shower);
            TableRef urinal = Located(Look(plumbing, "urinal"), -32);
            Fixture("/obj/structure/urinal/directional/south", "urinal", 12, 2, urinal, urinal, dir: ByondDir.South);
            const string lighting = "icons/obj/lighting.dmi", pad = "icons/obj/machines/floor.dmi";
            Fixture("/obj/machinery/light/floor", "floor lamp", 6, 3,
                Look(lighting, "floor", "floor"), Look(lighting, "floor", "floor_emergency"));
            Fixture("/obj/machinery/holopad", "holopad", 10, 3,
                Located(Look(pad, "holopad0"), plane: -6, layer: 10015), Located(Look(pad, "holopad1"), plane: -6, layer: 10015));
            const string tanks = "icons/obj/medical/chemical_tanks.dmi", smes = "icons/obj/machines/engine/other.dmi";
            TableRef Mixture(uint color)
            {
                TableRef reference = Look(tanks, "tank_chem_overlay");
                world.Finish();
                var overlay = world.Snapshot.Appearances[(int)reference.Index - 1].WithOverlays(_ => true);
                overlay.Rgba = color;
                reference = world.Add(overlay);
                TableRef body = Look(tanks, "tank_stationary");
                world.Finish();
                var entry = world.Snapshot.Appearances[(int)body.Index - 1].WithOverlays(_ => true);
                entry.Overlays.Add(reference);
                return world.Add(entry);
            }
            string[] tankStates = { "water", "fuel", "water_high", "fuel_high", "foam", "water_stationary", "fuel_stationary", "tank_stationary" };
            for (int i = 0; i < tankStates.Length; i++)
            {
                TableRef look = Look(tanks, tankStates[i]);
                Fixture("/obj/structure/reagent_dispensers", tankStates[i], 2 + i % 4 * 2, 5 + i / 4 * 2,
                    look, tankStates[i] == "tank_stationary" ? Mixture(0xE02040FF) : look, dir: ByondDir.South);
            }
            Fixture("/obj/structure/reagent_dispensers/plumbed/storage", "independent mixture", 10, 7,
                Mixture(0x2040E0FF), Mixture(0x20D040FF), dir: ByondDir.South);
            string gaugeView = System.Environment.GetEnvironmentVariable("ANOMALY_SHOWCASE_SMES_GAUGE");
            if (gaugeView is "1" or "high")
            {
                // Isolate all six source charge levels while keeping the control lenses inactive.
                TableRef Gauge(int level) => Look(smes, "smes", level == 0 ? Array.Empty<string>() : new[] { "smes-og" + level });
                int first = gaugeView == "high" ? 3 : 0;
                for (int level = 0; level < 3; level++)
                    Fixture("/obj/machinery/power/smes", "gauge " + level, 12 + level * 2, 5,
                        Gauge(level + first), Gauge(level + 3 - first), dir: ByondDir.South);
            }
            else
            {
                Fixture("/obj/machinery/power/smes", "charge and transfer", 12, 5,
                    Look(smes, "smes", "smes-og1", "smes-op0", "smes-oc0"), Look(smes, "smes", "smes-og5", "smes-op1", "smes-oc1"), dir: ByondDir.South);
                Fixture("/obj/machinery/power/smes", "service panel", 14, 5,
                    Look(smes, "smes", "smes-og3", "smes-op1", "smes-oc1"), Look(smes, "smes-o"), dir: ByondDir.South);
                Fixture("/obj/machinery/power/smes", "disabled display", 16, 5,
                    Look(smes, "smes"), Look(smes, "smes"), dir: ByondDir.South);
            }
        }
        world.Finish();
        _replica = new ReplicaWorld();
        _replica.Apply(world.Map); _replica.Apply(world.Snapshot);
        _map = new MapRenderer(); AddChild(_map);
        _map.Build(_replica.Map, _replica, new AppearanceTextures());
        var camera = new Camera3D { Current = true, Fov = 85, Position = new Vector3(TileSpace.WorldX(8), 2.2f, TileSpace.WorldZ(3)) };
        camera.RotationDegrees = new Vector3(-4, 0, 0); AddChild(camera);
        string view = System.Environment.GetEnvironmentVariable("ANOMALY_SHOWCASE_CAM");
        if (!string.IsNullOrEmpty(view))
        {
            float[] at = view.Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            camera.Position = new Vector3(TileSpace.WorldX(at[0]), at[2], TileSpace.WorldZ(at[1]));
            camera.RotationDegrees = new Vector3(at[4], at[3], 0);
        }
        bool dark = System.Environment.GetEnvironmentVariable("ANOMALY_MODEL_SHOWCASE_DARK") == "1";
        AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(.03f, .03f, .04f), GlowEnabled = dark } });
        if (dark)
        {
            var lights = new Anomaly.Client.Light.LightLayer(); AddChild(lights);
            lights.Attach(new ShowcaseFeed(_replica), camera);
        }
        if (App.Instance != null)
        {
            App.Instance.SummaryProvider = () => $"model showcase: checks={_checks} models={_map.ModelCount}/{_expectedModels} lights={_map.ModelLightCount} states={_states.Count} phases={_phase}";
            App.Instance.GameStarted();
        }
    }

    public override void _Process(double delta)
    {
        _seconds += delta;
        int phase = (int)(_seconds / 2);
        if (phase != _phase)
        {
            _phase = phase;
            var change = new Delta { Tick = ++_tick };
            foreach (var state in _states)
                change.Changed.Add(new EntityChange { Id = state.Id, Mask = NetField.Appearance, Appearance = phase % 2 == 0 ? state.First : state.Second });
            _replica.Apply(change);
        }
        _map.Update(delta);
        _checks++;
        if (_map.ModelCount != _expectedModels)
        {
            GD.PushError($"model showcase has unexpected model count: {_map.ModelCount}/{_expectedModels} in phase {_phase}");
            GetTree().Quit(1);
        }
    }
}
