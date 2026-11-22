using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ROADMAP M5's demo check through the real server on MetaStation, end to end, with a headless Godot client
/// (<see cref="PlaySession"/>) that clicks, presses keys and works tg's windows as a player does. The engineer joins with the
/// Captain's spare in the ID slot (every player is an assistant until the jobs, M9/M10; the M4c check's stand-in for the access),
/// magboots on his feet (turned on with their own action button) and a mechanical toolbox from the spawn console (the M4a check's
/// way); his tools come out of the toolbox's storage window and go back into it, his materials into the backpack.
/// In the Auxiliary Base (the plan's section 4) he takes iron, glass and rods from the table, a cable coil and an airlock
/// electronics from the rack and sets the electronics' access in tg's AirlockElectronics window. Inside the construction zone he
/// slices the west wall to space with the welder (tg's 10 s): plating, a girder and two iron sheets; the zone vents below 10 kPa
/// and the space wind is heard. Two iron on the girder build the wall again; the zone's airlock lets the room's air in and five
/// air canisters, opened from their own tgui windows, refill it. From the iron's StackCrafting window he builds wall girders and a
/// wall on them, from the rods' a grille with glass on it (the window comes loose and a screwdriver fastens it), and from the iron's
/// a standard airlock assembly that the wrench, the cable, the electronics and the screwdriver make an airlock that opens to him.
/// Then he takes apart what he built: the window (2 glass), the grille (2 rods), the airlock (its assembly and electronics; the
/// cable; 4 iron) and the wall (2 iron and a girder that comes apart into 2 more). Last the M4 breach is redone at the Fitness Room:
/// the toolbox breaks the glass of the bridge's fire axe cabinet, the axe wielded breaks the reinforced window to space in one hit
/// (shards and rods; the grille stays), the room drains and the firelock closes, two sheets of glass on the grille and the
/// screwdriver seal it, and an air canister's valve, opened in its window, refills it. Each step is checked in the world and in what
/// the client sees and hears. The second test runs the build-and-destroy steps twice from a fresh boot with the same seed and
/// compares the state hashes.
/// </summary>
public class M5ScenarioTests
{
    private const int Second = 20; // ticks per second
    private const ulong Seed = 55;

    // The Auxiliary Base Construction room and zone (x 42-51, y 147-160; the zone is the plating x 43-51, y 151-159).
    private const int TableX = 51, TableY = 149; // iron x2 fifty, glass fifty
    private const int RodsX = 51, RodsY = 148; // rods fifty, plasteel 10
    private const int RackX = 50, RackY = 149; // airlock electronics x4, cable coils x2
    private const int ZoneDoorX = 47, ZoneDoorY = 150; // "Construction Zone", access aux_base
    private const int WallX = 42, WallY = 155; // the zone's west wall, space beyond
    private const int ZoneX = 47, ZoneY = 155; // the middle of the zone
    private const int RoomX = 47, RoomY = 148; // the room
    private const int GirderX = 45, GirderY = 157, GrilleX = 46, GrilleY = 157, DoorX = 48, DoorY = 157; // the new things

    // The Fitness Room (x 137-141, y 183-185) and the bridge's fire axe cabinet.
    private const int HoleX = 136, HoleY = 184; // the reinforced window to space
    private const int FitX = 139, FitY = 184; // the middle of the room
    private const int FirelockX = 138, FirelockY = 182;
    private const int CabinetX = 115, CabinetY = 134;

    private const string Welder = "sound/items/tools/welder.ogg";
    private const string Welder2 = "sound/items/tools/welder2.ogg";
    private const string SpaceWind = "sound/effects/space_wind.ogg";
    private const string Ratchet = "sound/items/tools/ratchet";
    private const string Screwdriver = "sound/items/tools/screwdriver";
    private const string Deconstruct = "sound/items/deconstruct.ogg";
    private const string GlassHit = "sound/effects/glass/glasshit.ogg";
    private const string Shatter = "sound/effects/glass/glassbr";

    private readonly ITestOutputHelper _output;

    public M5ScenarioTests(ITestOutputHelper output) => _output = output;

    private static RealHeadlessWorld Boot()
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = Seed, TimeoutSteps = 60000 }, MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        MetaServer.ClearAuxBase(w.World); // the scenario builds in the empty construction zone
        return w;
    }

    [MetaServerFact]
    public void An_engineer_vents_and_rebuilds_a_wall_builds_and_takes_apart_a_wall_a_window_and_an_airlock_and_seals_the_Fitness_breach_with_an_axe_and_glass()
    {
        using RealHeadlessWorld w = Boot();
        var e = new Engineer(w, w.Join("Engineer"), _output);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        e.Kit();
        e.TakeMaterials();
        obj_structure_girder girder = e.CutTheWall();
        e.WatchTheZoneVent();
        e.RebuildTheWall(girder);
        e.RefillTheZone();
        Built built = e.Build();
        e.TakeApart(built);
        e.RedoTheFitnessBreach();
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the player did ran into a runtime
    }

    [MetaServerFact]
    public void The_build_and_destroy_steps_run_twice_from_a_fresh_boot_end_with_the_same_state_hash()
    {
        (ulong Mid, ulong End, long Tick) Once()
        {
            using RealHeadlessWorld w = Boot();
            var e = new Engineer(w, w.Join("Engineer"), _output);
            e.Kit();
            e.TakeMaterials();
            obj_structure_girder girder = e.CutTheWall();
            e.RebuildTheWall(girder);
            Built built = e.Build();
            ulong mid = w.World.ComputeStateHash();
            e.TakeApart(built);
            w.Run(5 * Second);
            return (mid, w.World.ComputeStateHash(), w.Ticks);
        }

        var first = Once();
        var second = Once();
        _output.WriteLine($"first run: built {first.Mid:x16}, end {first.End:x16} at tick {first.Tick}; second: {second.Mid:x16}, {second.End:x16} at tick {second.Tick}");
        Assert.Equal(first.Tick, second.Tick);
        Assert.Equal(first.Mid, second.Mid);
        Assert.Equal(first.End, second.End);
    }

    private sealed record Built(turf_closed_wall Wall, obj_structure_grille Grille, obj_structure_window Window, obj_machinery_door_airlock Door, obj_item_electronics_airlock Electronics);

    /// <summary>The player, his client and the steps of the check.</summary>
    private sealed class Engineer
    {
        private readonly RealHeadlessWorld _w;
        private readonly RealHeadlessWorld.Player _p;
        private readonly mob_living_carbon_human _me;
        private readonly ReplicaWorld _replica;
        private readonly TguiWindows _windows;
        private readonly ITestOutputHelper _out;
        private readonly List<(long Tick, SoundEvent E)> _heard = new();
        private readonly int _z;
        private obj_item_storage_toolbox_mechanical _toolbox;
        private obj_item _pack;
        private obj_item_electronics_airlock _electronics;

        public Engineer(RealHeadlessWorld w, RealHeadlessWorld.Player p, ITestOutputHelper output)
        {
            _w = w;
            _p = p;
            _out = output;
            _me = (mob_living_carbon_human)w.MobOf(p);
            _replica = p.Session.Replica;
            _replica.SoundsReceived += events => _heard.AddRange(events.Select(s => (w.Ticks, s)));
            _windows = new TguiWindows
            {
                SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
                SendClose = window => p.Session.UiClose(window),
            };
            _z = In(() => (int)_me.z);
        }

        // ---------------------------------------------------------------- helpers

        private T In<T>(Func<T> f)
        {
            using (_w.World.Enter())
                return f();
        }

        private void Do(Action a)
        {
            using (_w.World.Enter())
                a();
        }

        private void Run(float seconds) => _w.Run((int)(seconds * Second));

        // The earlier checks' walk: the player is put on the tile (the walking itself is M2's, checked there).
        private void Put(int x, int y)
        {
            Do(() => Waiting.NoSleep(_me.forceMove(Builtins.locate(x, y, _z))));
            Run(1);
        }

        private List<T> On<T>(int x, int y) => In(() => Dm.each_cast<atom>(((turf)Builtins.locate(x, y, _z)).contents).OfType<T>().ToList());

        private turf TurfAt(int x, int y) => In(() => (turf)Builtins.locate(x, y, _z));

        private float Kpa(int x, int y) => In(() => Builtins.locate(x, y, _z) is turf_open t && t.air != null ? Dm.num(t.air.return_pressure()) : -1f);

        private obj_item Active => In(() => _me.get_active_held_item() as obj_item);

        private bool Truthy(Func<object> f) => In(() => Dm.truthy(f()));

        private void Click(atom target, float seconds = 1, ClickButton button = ClickButton.Left)
        {
            Assert.True(_p.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), button, ClickModifiers.None));
            Run(seconds);
        }

        private void ClickTurf(int x, int y, float seconds = 1)
        {
            Assert.True(_p.Session.Click(new ClickTarget(TargetKind.Turf, 0, x, y, _z), ClickButton.Left, ClickModifiers.None));
            Run(seconds);
        }

        private void ClickScreen(long id)
        {
            Assert.True(_p.Session.Click(new ClickTarget(TargetKind.Screen, id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            Run(1);
        }

        private void Key(KeybindingCode code, float seconds = 0.5f)
        {
            _p.Session.Keybinding(code);
            Run(seconds);
        }

        private bool Until(Func<bool> done, float maxSeconds)
        {
            for (int t = 0; t < maxSeconds * Second; t++)
            {
                if (done())
                    return true;
                _w.Run(1);
            }
            return done();
        }

        private List<SoundEvent> Plays(string file, long from)
            => _heard.Where(h => h.Tick >= from && h.E.Kind == SoundKind.Play && h.E.File != null && h.E.File.StartsWith(file, StringComparison.Ordinal)).Select(h => h.E).ToList();

        private bool InStorageWindow(long id) => _replica.Screen.Values.Any(s => s.Id == id && s.Group == HudGroup.Storage);

        private bool Seen(atom a) => _replica.Entities.Any(s => s.Id == a.dm_id);

        private RenderKind KindSeen(atom a) => PropKinds.Classify(_replica.Entities.Single(s => s.Id == a.dm_id).TypePath);

        private List<string> States(atom a)
            => CrewOutfitTests.Icons(_replica, _replica.Entities.Single(s => s.Id == a.dm_id).State.Appearance).Select(i => i.State).ToList();

        private string RootState(atom a)
        {
            ReplicaEntity seen = _replica.Entities.Single(s => s.Id == a.dm_id);
            return _replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? _replica.ResolveString(look.IconState) : null;
        }

        private float Amount<T>(int x, int y) where T : obj_item_stack => On<T>(x, y).Sum(s => In(() => Dm.num(s.amount)));

        private UiNode Shown(int window)
        {
            _windows.Sync(_replica.UiWindows, 1280, 720, 1);
            return _windows.Get(window)?.Content;
        }

        private void CloseWindow(int window)
        {
            Assert.True(_p.Session.UiClose(window));
            Run(1);
            _windows.Sync(_replica.UiWindows, 1280, 720, 1); // a pooled window index reused later starts fresh (S7's note)
        }

        private void PickUp(obj_item item)
        {
            Assert.Null(Active);
            Click(item);
            Assert.Same(item, Active);
        }

        // An item out of a storage the player carries: a click on the container with the empty hand opens its storage window, a
        // click on the item there takes it (M4a's toolbox).
        private T Take<T>(obj_item container) where T : obj_item
        {
            Assert.Null(Active);
            T item = In(() => Dm.each_cast<atom>(container.contents).OfType<T>().FirstOrDefault());
            Assert.True(item != null, $"{typeof(T).Name} in {In(() => container.name)}");
            for (int i = 0; i < 2 && !InStorageWindow(item.dm_id); i++)
                ClickScreen(container.dm_id);
            Assert.True(InStorageWindow(item.dm_id), "the storage window shows " + typeof(T).Name);
            ClickScreen(item.dm_id);
            Assert.Same(item, Active);
            return item;
        }

        // The held item put into a carried storage: a click on the container with it (tg's storage attackby).
        private void Stow(obj_item container)
        {
            obj_item held = Active;
            Assert.NotNull(held);
            ClickScreen(container.dm_id);
            Assert.Same(container, In(() => held.loc));
        }

        // A recipe from the held stack's StackCrafting window (Z), built where the crosshair aims (DV65: nothing dense on the
        // builder's own tile): the player stands south of the tile (x, y) and looks at it.
        private void Craft(string recipe, int x, int y, Func<bool> built, float maxSeconds)
        {
            Put(x, y - 1);
            _p.Session.Hover(new ClickTarget(TargetKind.Turf, 0, x, y, _z), _w.Now);
            Run(0.5f);
            Key(KeybindingCode.ActivateInhand, 1);
            UiWindowState stack = _replica.UiWindows.Values.Single(v => v.Interface == "StackCrafting");
            Shown(stack.Window).Walk().First(n => n.Key == recipe).OnClick();
            Assert.True(Until(built, maxSeconds), recipe + " is built");
            CloseWindow(stack.Window);
        }

        // ---------------------------------------------------------------- the steps

        /// <summary>The kit (set up, as the job's outfit would be): the Captain's spare, magboots, a backpack; then, by the player,
        /// the magboots' button and a toolbox from the spawn console picked up into the hand he keeps it in.</summary>
        public void Kit()
        {
            obj_item_clothing_shoes_magboots boots = null;
            Do(() =>
            {
                if (_me.wear_id is obj_item card)
                    qdel(card);
                var spare = Dm.@new<obj_item_card_id_advanced_gold_captains_spare>(_me.loc);
                Assert.True(Dm.truthy(Waiting.NoSleep(_me.equip_to_slot_if_possible(spare, Defines.ITEM_SLOT_ID))), "the Captain's spare goes on");
                if (_me.shoes is obj_item shoes)
                    qdel(shoes);
                boots = Dm.@new<obj_item_clothing_shoes_magboots>(_me.loc);
                Assert.True(Dm.truthy(Waiting.NoSleep(_me.equip_to_slot_if_possible(boots, Defines.ITEM_SLOT_FEET))), "the magboots go on");
                if (_me.back == null)
                    Assert.True(Dm.truthy(Waiting.NoSleep(_me.equip_to_slot_if_possible(Dm.@new<obj_item_storage_backpack>(_me.loc), Defines.ITEM_SLOT_BACK))));
                _pack = _me.back as obj_item;
                // The player stays in the venting zone for minutes: tg's godmode keeps him from the vacuum's damage (the test's,
                // not tg's; S4's wall test does the same).
                ADD_TRAIT(_me, Defines.TRAIT_GODMODE, "test");
            });
            Run(1);
            long button = In(() => ((atom)Dm.each_cast<datum_action>(boots.actions).First(a => a is datum_action_item_action_toggle).viewers[_me.hud_used]).dm_id);
            ClickScreen(button); // the mag-pulse (magboots.dm:45): the space wind does not move him
            Assert.True(In(() => HAS_TRAIT(_me, Defines.TRAIT_NEGATES_GRAVITY)), "the magboots are on");

            (int x, int y) = In(() => ((int)_me.x, (int)_me.y));
            _w.Host.EnqueueConsoleLine($"spawn /obj/item/storage/toolbox/mechanical {x} {y} {_z}");
            Run(1);
            _toolbox = On<obj_item_storage_toolbox_mechanical>(x, y).Single();
            PickUp(_toolbox);
            Key(KeybindingCode.SwapHands); // the toolbox stays in the other hand
            Assert.Null(Active);
        }

        /// <summary>Step 1: iron, glass and rods from the aux base table, a cable coil and an airlock electronics from its rack, the
        /// electronics' access set in its window; all into the backpack.</summary>
        public void TakeMaterials()
        {
            Put(TableX - 1, TableY - 1);
            foreach (obj_item stack in new obj_item[]
            {
                On<obj_item_stack_sheet_iron>(TableX, TableY).First(), On<obj_item_stack_sheet_glass>(TableX, TableY).First(),
                On<obj_item_stack_rods>(RodsX, RodsY).First(), On<obj_item_stack_cable_coil>(RackX, RackY).First(),
            })
            {
                PickUp(stack);
                Stow(_pack);
            }
            _electronics = On<obj_item_electronics_airlock>(RackX, RackY).First();
            PickUp(_electronics);
            Key(KeybindingCode.ActivateInhand, 1);
            UiWindowState window = Assert.Single(_replica.UiWindows.Tgui());
            Assert.Equal("AirlockElectronics", window.Interface);
            UiNode content = Shown(window.Window);
            string region = window.Merged.Arr("regions").First(r => r.Arr("accesses").Any(a => a.Str("desc") == "Auxiliary Base")).Str("name");
            content.Walk().First(n => n.Kind == UiKind.Tab && n.Key == region).OnClick();
            Shown(window.Window).Walk().First(n => n.Kind == UiKind.Button && n.Key == "Auxiliary Base").OnClick();
            Run(1);
            Assert.Equal(new object[] { Defines.ACCESS_AUX_BASE }, In(() => Dm.each(_electronics.accesses).ToArray()));
            CloseWindow(window.Window);
            Stow(_pack);
            _out.WriteLine("the backpack: " + In(() => string.Join(", ", Dm.each_cast<obj_item>(_pack.contents).Select(i => (string)i.name))));
        }

        /// <summary>Step 2: into the zone through its airlock, the welder from the toolbox lit, the west wall sliced in 10 s.</summary>
        public obj_structure_girder CutTheWall()
        {
            var zoneDoor = On<obj_machinery_door_airlock>(ZoneDoorX, ZoneDoorY).Single();
            Put(ZoneDoorX, ZoneDoorY - 1);
            Click(zoneDoor, 2);
            Assert.False(Truthy(() => zoneDoor.density), "the zone's airlock opens to the aux base access");
            Put(ZoneDoorX, ZoneDoorY + 1);
            Put(WallX + 1, WallY);
            Assert.True(Until(() => Truthy(() => zoneDoor.density), 20), "the zone's airlock closes behind him");
            Assert.IsType<turf_closed_wall>(TurfAt(WallX, WallY));
            Assert.IsAssignableFrom<turf_open_space>(TurfAt(WallX - 1, WallY));
            Assert.Equal("/turf/closed/wall", _replica.TurfTypeAt(WallX, WallY, _z));

            var welder = Take<obj_item_weldingtool>(_toolbox);
            Key(KeybindingCode.ActivateInhand, 1);
            Assert.True(Truthy(() => welder.welding), "the welder is lit");
            long cut = _w.Ticks;
            ClickTurf(WallX, WallY, 0.5f);
            Assert.True(Until(() => TurfAt(WallX, WallY) is turf_open_floor_plating, 12), "the wall is sliced");
            float took = (_w.Ticks - cut) / (float)Second;
            Assert.True(took >= 9.5f, "tg's 10 s (walls.dm:233): " + took);
            var girder = On<obj_structure_girder>(WallX, WallY).SingleOrDefault();
            Assert.NotNull(girder);
            float iron = In(() => Enumerable.Range(WallX - 6, 8).SelectMany(cx => Enumerable.Range(WallY - 3, 7).SelectMany(cy => Dm.each_cast<atom>(((turf)Builtins.locate(cx, cy, _z)).contents)))
                .OfType<obj_item_stack_sheet_iron>().Sum(s => Dm.num(s.amount)));
            Assert.Equal(2f, iron); // dismantle_wall (walls.dm:98): the girder and the sheet_amount
            Run(1);
            Assert.NotEmpty(Plays(Welder, cut)); // the slicing's use sound and the wall's break sound (welder.ogg)
            Assert.Equal("/turf/open/floor/plating", _replica.TurfTypeAt(WallX, WallY, _z));
            Assert.Equal(RenderKind.Girder, KindSeen(girder));
            Assert.StartsWith("girder", RootState(girder), StringComparison.Ordinal);
            _out.WriteLine($"the wall sliced in {took:0.0} s; the client sees the girder as {RootState(girder)}");
            Key(KeybindingCode.ActivateInhand); // the welder off
            Stow(_toolbox);
            return girder;
        }

        /// <summary>Step 2, the air: the zone vents to space through the girder's tile; the room keeps its air.</summary>
        public void WatchTheZoneVent()
        {
            long from = _w.Ticks;
            Assert.True(Until(() => Kpa(ZoneX, ZoneY) < 10f, 240), "the zone vents below 10 kPa: " + Kpa(ZoneX, ZoneY));
            _out.WriteLine($"the zone below 10 kPa {(_w.Ticks - from) / (float)Second:0} s after the cut; space wind heard {Plays(SpaceWind, from).Count} times; the room at {Kpa(RoomX, RoomY):0.0} kPa");
            Assert.NotEmpty(Plays(SpaceWind, from - 15 * Second));
            Assert.Equal(101.3f, Kpa(RoomX, RoomY), 5f); // the closed airlock and the windows keep the room's air
        }

        /// <summary>Step 3: two iron on the girder (the girder wall recipe, 4 s) build the wall again.</summary>
        public void RebuildTheWall(obj_structure_girder girder)
        {
            Put(WallX + 1, WallY);
            var iron = Take<obj_item_stack_sheet_iron>(_pack);
            float before = In(() => Dm.num(iron.amount));
            long from = _w.Ticks;
            Click(girder, 0.5f);
            Assert.True(Until(() => TurfAt(WallX, WallY) is turf_closed_wall, 6), "the wall is built again");
            _out.WriteLine($"the wall rebuilt in {(_w.Ticks - from) / (float)Second:0.0} s");
            Assert.Equal(before - 2, In(() => Dm.num(iron.amount)));
            Assert.True(In(() => QDELETED(girder)));
            Run(1);
            Assert.Equal("/turf/closed/wall", _replica.TurfTypeAt(WallX, WallY, _z));
            Assert.False(Seen(girder));
            Stow(_pack);
        }

        /// <summary>Step 3, the air: the zone's airlock opened lets the room's air in; five air canisters (the spawn console, as the
        /// M4b check) opened from their tgui windows refill the zone.</summary>
        public void RefillTheZone()
        {
            var zoneDoor = On<obj_machinery_door_airlock>(ZoneDoorX, ZoneDoorY).Single();
            Put(ZoneDoorX, ZoneDoorY + 1);
            Click(zoneDoor, 2);
            Assert.False(Truthy(() => zoneDoor.density));
            Run(8);
            float shared = Kpa(ZoneX, ZoneY);
            _out.WriteLine($"the room's air in the zone: {shared:0.0} kPa, the room {Kpa(RoomX, RoomY):0.0} kPa");
            Assert.InRange(shared, 10f, 60f);

            for (int i = 0; i < 5; i++)
                _w.Host.EnqueueConsoleLine($"spawn /obj/machinery/portable_atmospherics/canister/air {44 + i} 152 {_z}");
            Run(0.5f);
            for (int i = 0; i < 5; i++)
            {
                var canister = On<obj_machinery_portable_atmospherics_canister_air>(44 + i, 152).Single();
                Put(44 + i, 153);
                Click(canister);
                UiWindowState ui = _replica.UiWindows.Values.Single(v => v.Interface == "Canister");
                Assert.True(_p.Session.UiAct(ui.Window, "valve"));
                Run(1);
                Assert.True(Truthy(() => canister.valve_open), "the window's valve button opens the valve");
                CloseWindow(ui.Window);
            }
            long opened = _w.Ticks;
            Assert.True(Until(() => Kpa(ZoneX, ZoneY) >= 90f, 150), "the canisters refill the zone: " + Kpa(ZoneX, ZoneY));
            _out.WriteLine($"the zone at {Kpa(ZoneX, ZoneY):0.0} kPa {(_w.Ticks - opened) / (float)Second:0} s after the valves opened");
        }

        /// <summary>Step 4: a wall on new girders, a window on a new grille, an airlock from its assembly.</summary>
        public Built Build()
        {
            // Wall girders from the iron's window, two more sheets on them: a wall.
            var iron = Take<obj_item_stack_sheet_iron>(_pack);
            long from = _w.Ticks;
            Craft("wall girders (anchored)", GirderX, GirderY, () => On<obj_structure_girder>(GirderX, GirderY).Count == 1, 6);
            var girder = On<obj_structure_girder>(GirderX, GirderY).Single();
            _out.WriteLine($"wall girders built in {(_w.Ticks - from) / (float)Second:0.0} s");
            Run(0.5f);
            Assert.Equal(RenderKind.Girder, KindSeen(girder));
            Put(GirderX, GirderY - 1);
            Click(girder, 0.5f);
            Assert.True(Until(() => TurfAt(GirderX, GirderY) is turf_closed_wall, 6), "the girder becomes a wall");
            var wall = (turf_closed_wall)TurfAt(GirderX, GirderY);
            Run(1);
            Assert.Equal("/turf/closed/wall", _replica.TurfTypeAt(GirderX, GirderY, _z));
            Assert.False(Seen(girder));
            Stow(_pack);

            // A grille from the rods' window; two glass on it make a loose window; the screwdriver fastens it.
            Take<obj_item_stack_rods>(_pack);
            Craft("grille", GrilleX, GrilleY, () => On<obj_structure_grille>(GrilleX, GrilleY).Count == 1, 3);
            var grille = On<obj_structure_grille>(GrilleX, GrilleY).Single();
            Stow(_pack);
            Put(GrilleX, GrilleY - 1);
            Run(0.5f);
            Assert.Equal(RenderKind.Grille, KindSeen(grille));
            var glass = Take<obj_item_stack_sheet_glass>(_pack);
            float glassBefore = In(() => Dm.num(glass.amount));
            Click(grille, 0.5f);
            Assert.True(Until(() => On<obj_structure_window>(GrilleX, GrilleY).Count == 1, 4), "glass on the grille makes a window");
            var window = On<obj_structure_window>(GrilleX, GrilleY).Single();
            Assert.Equal(glassBefore - 2, In(() => Dm.num(glass.amount)));
            Assert.False(Truthy(() => window.anchored), "the window comes loose (grille.dm:248-305)");
            Run(0.5f);
            Assert.Equal(RenderKind.FullWindow, KindSeen(window));
            Stow(_pack);
            Take<obj_item_screwdriver>(_toolbox);
            from = _w.Ticks;
            Click(window, 0.5f);
            Assert.True(Until(() => Truthy(() => window.anchored), 6), "the screwdriver fastens the window (window.dm:228)");
            Assert.NotEmpty(Plays(Screwdriver, from));
            Assert.False(In(() => Dm.truthy(window.can_atmos_pass__proc(TurfAt(GrilleX, GrilleY - 1)))), "the fastened window seals");
            Stow(_toolbox);

            // The airlock: the assembly from the iron's window, wrench, cable, electronics, screwdriver.
            Take<obj_item_stack_sheet_iron>(_pack);
            Craft("standard airlock assembly", DoorX, DoorY, () => On<obj_structure_door_assembly>(DoorX, DoorY).Count == 1, 7);
            var assembly = On<obj_structure_door_assembly>(DoorX, DoorY).Single();
            Stow(_pack);
            Put(DoorX + 1, DoorY);
            Run(0.5f);
            Assert.Equal(RenderKind.DoorAssembly, KindSeen(assembly));
            Assert.Contains("panel_c1", States(assembly));

            Take<obj_item_wrench>(_toolbox);
            from = _w.Ticks;
            Click(assembly, 0.5f);
            Assert.True(Until(() => Truthy(() => assembly.anchored), 6), "the wrench anchors the assembly");
            Assert.NotEmpty(Plays(Ratchet, from));
            Stow(_toolbox);
            Take<obj_item_stack_cable_coil>(_pack);
            Click(assembly, 0.5f);
            Assert.True(Until(() => In(() => Dm.num(assembly.state)) == Defines.AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS, 6), "the cable wires it");
            Run(0.5f);
            Assert.Contains("panel_c2", States(assembly));
            Stow(_pack);
            Take<obj_item_electronics_airlock>(_pack);
            Click(assembly, 0.5f);
            Assert.True(Until(() => In(() => Dm.num(assembly.state)) == Defines.AIRLOCK_ASSEMBLY_NEEDS_SCREWDRIVER, 6), "the electronics go in");
            Run(0.5f);
            Assert.Contains("panel_c3", States(assembly));
            Take<obj_item_screwdriver>(_toolbox);
            from = _w.Ticks;
            Click(assembly, 0.5f);
            Assert.True(Until(() => On<obj_machinery_door_airlock>(DoorX, DoorY).Count == 1, 6), "the screwdriver finishes the airlock (door_assembly.dm:344)");
            var door = On<obj_machinery_door_airlock>(DoorX, DoorY).Single();
            Assert.NotEmpty(Plays(Screwdriver, from));
            Stow(_toolbox);
            Run(0.5f);
            Assert.False(Seen(assembly));
            Assert.Equal(RenderKind.Airlock, KindSeen(door));
            Assert.Equal(new object[] { Defines.ACCESS_AUX_BASE }, In(() => Dm.each((DmList)door.req_access).ToArray()));
            Click(door, 2);
            Assert.False(Truthy(() => door.density), "the new airlock opens to the aux base access");
            Assert.True(Until(() => Truthy(() => door.density), 15), "it closes by itself");
            _out.WriteLine($"built: a wall at {GirderX},{GirderY}, a window ({RootState(window)}) on a grille at {GrilleX},{GrilleY}, an airlock at {DoorX},{DoorY}");
            return new Built(wall, grille, window, door, _electronics);
        }

        /// <summary>Step 5: what was built taken apart with tg's tools; the materials are tg's amounts.</summary>
        public void TakeApart(Built b)
        {
            // The window: unscrewed from the floor, the wrench takes it apart (window.dm:254): glass_amount (2) sheets at the user's feet.
            Put(GrilleX, GrilleY - 1);
            Take<obj_item_screwdriver>(_toolbox);
            Click(b.Window, 0.5f);
            Assert.True(Until(() => !Truthy(() => b.Window.anchored), 6), "the screwdriver unfastens the window");
            Stow(_toolbox);
            Take<obj_item_wrench>(_toolbox);
            long from = _w.Ticks;
            Click(b.Window, 0.5f);
            Assert.True(Until(() => In(() => QDELETED(b.Window)), 6), "the wrench takes the window apart");
            Run(0.5f);
            Assert.NotEmpty(Plays(Deconstruct, from));
            Assert.Equal(2f, Amount<obj_item_stack_sheet_glass>(GrilleX, GrilleY - 1)); // new glass_type(user.loc, glass_amount)
            Stow(_toolbox);
            // The grille: the wirecutters' right click cuts it (grille.dm:213 wirecutter_act_secondary, deconstruct): 2 rods.
            Take<obj_item_wirecutters>(_toolbox);
            Click(b.Grille, 1, ClickButton.Right);
            Assert.True(Until(() => In(() => QDELETED(b.Grille)), 4), "the wirecutters cut the grille");
            Assert.Equal(2f, Amount<obj_item_stack_rods>(GrilleX, GrilleY));
            Stow(_toolbox);
            Run(0.5f);
            Assert.False(Seen(b.Window));
            Assert.False(Seen(b.Grille));

            // The airlock: the panel open, the power off, welded shut, pried apart into its assembly and electronics; the assembly's
            // cable cut, unwrenched, welded apart into 4 iron.
            Put(DoorX + 1, DoorY);
            Take<obj_item_screwdriver>(_toolbox);
            Click(b.Door, 1);
            Assert.True(Truthy(() => b.Door.panel_open), "the panel is open");
            Stow(_toolbox);
            // tg unpowers an airlock in its hacking window or at the APC (M7); the area's environment channel goes off as the APC's
            // breaker does (S6's gameplay test does the same), and comes back after.
            area zone = In(() => (area)get_area(b.Door));
            Do(() =>
            {
                zone.power_environ = 0;
                zone.power_change();
            });
            Run(1);
            Assert.False(Truthy(() => b.Door.hasPower()));
            var welder = Take<obj_item_weldingtool>(_toolbox);
            Key(KeybindingCode.ActivateInhand, 1);
            Assert.True(Truthy(() => welder.welding));
            Click(b.Door, 0.5f, ClickButton.Right);
            Assert.True(Until(() => Truthy(() => b.Door.welded), 8), "the airlock is welded shut");
            Key(KeybindingCode.ActivateInhand);
            Stow(_toolbox);
            Take<obj_item_crowbar>(_toolbox);
            Click(b.Door, 0.5f);
            Assert.True(Until(() => On<obj_structure_door_assembly>(DoorX, DoorY).Count == 1, 8), "the crowbar takes the airlock apart (airlock.dm:1267)");
            Stow(_toolbox);
            Do(() =>
            {
                zone.power_environ = Dm.TRUE;
                zone.power_change();
            });
            var back = On<obj_structure_door_assembly>(DoorX, DoorY).Single();
            Assert.Empty(On<obj_machinery_door_airlock>(DoorX, DoorY));
            In(() =>
            {
                Assert.True(Dm.truthy(back.anchored));
                Assert.Equal(Defines.AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS, Dm.num(back.state));
                Assert.IsAssignableFrom<turf>(b.Electronics.loc);
                Assert.Equal(new object[] { Defines.ACCESS_AUX_BASE }, Dm.each(b.Electronics.accesses).ToArray());
                return 0;
            });
            Run(0.5f);
            Assert.False(Seen(b.Door));
            Assert.Equal(RenderKind.DoorAssembly, KindSeen(back));
            Take<obj_item_wirecutters>(_toolbox);
            Click(back, 0.5f);
            Assert.True(Until(() => In(() => Dm.num(back.state)) == Defines.AIRLOCK_ASSEMBLY_NEEDS_WIRES, 6), "the wirecutters take the cable out");
            Stow(_toolbox);
            Assert.Equal(1f, Amount<obj_item_stack_cable_coil>(DoorX + 1, DoorY)); // new cable_coil(get_turf(user), 1) (door_assembly.dm:296)
            Take<obj_item_wrench>(_toolbox);
            Click(back, 0.5f);
            Assert.True(Until(() => !Truthy(() => back.anchored), 6), "the wrench unanchors it");
            Stow(_toolbox);
            welder = Take<obj_item_weldingtool>(_toolbox);
            Key(KeybindingCode.ActivateInhand, 1);
            from = _w.Ticks;
            Click(back, 0.5f);
            Assert.True(Until(() => In(() => QDELETED(back)), 6), "the welder takes the assembly apart");
            Assert.Equal(4f, Amount<obj_item_stack_sheet_iron>(DoorX, DoorY));
            Assert.NotEmpty(Plays(Welder, from).Concat(Plays(Welder2, from))); // tg picks one of the two (weldingtool.dm:15)

            // The wall built in step 4: sliced (2 iron and a girder), the girder wrenched loose and unscrewed: 2 more iron.
            Put(GirderX, GirderY - 1);
            ClickTurf(GirderX, GirderY, 0.5f);
            Assert.True(Until(() => TurfAt(GirderX, GirderY) is turf_open_floor_plating, 12), "the wall is sliced");
            Key(KeybindingCode.ActivateInhand);
            Stow(_toolbox);
            var girder = On<obj_structure_girder>(GirderX, GirderY).Single();
            Assert.Equal(2f, Amount<obj_item_stack_sheet_iron>(GirderX, GirderY));
            Take<obj_item_wrench>(_toolbox);
            Click(girder, 0.5f);
            Assert.True(Until(() => On<obj_structure_girder_displaced>(GirderX, GirderY).Count == 1 || In(() => Dm.num(girder.state)) == Defines.GIRDER_DISPLACED, 6), "the wrench displaces the girder");
            Stow(_toolbox);
            var loose = On<obj_structure_girder>(GirderX, GirderY).Single();
            Take<obj_item_screwdriver>(_toolbox);
            Click(loose, 0.5f);
            Assert.True(Until(() => On<obj_structure_girder>(GirderX, GirderY).Count == 0, 6), "the screwdriver takes the girder apart");
            Assert.Equal(4f, Amount<obj_item_stack_sheet_iron>(GirderX, GirderY));
            Stow(_toolbox);
            Run(1);
            Assert.Equal("/turf/open/floor/plating", _replica.TurfTypeAt(GirderX, GirderY, _z));
            _out.WriteLine("taken apart: the window 2 glass, the grille 2 rods, the airlock its assembly + electronics, 1 cable, 4 iron; the wall 2 iron + a girder of 2 iron");
        }

        /// <summary>Step 6: the M4 breach with the bridge's fire axe and glass.</summary>
        public void RedoTheFitnessBreach()
        {
            // The bridge's fire axe cabinet (locked): its glass broken with the toolbox, the axe taken with the empty hand.
            var cabinet = On<obj_structure_fireaxecabinet>(CabinetX, CabinetY).Single();
            var axe = In(() => cabinet.held_item_value as obj_item_fireaxe);
            Assert.NotNull(axe);
            Put(CabinetX - 1, CabinetY);
            Key(KeybindingCode.SwapHands);
            Assert.Same(_toolbox, Active);
            _p.Session.ToggleCombatMode();
            Run(1);
            long from = _w.Ticks;
            int hits = 0;
            while (!Truthy(() => cabinet.broken) && hits < 60)
            {
                Click(cabinet);
                hits++;
            }
            _p.Session.ToggleCombatMode();
            Run(0.5f);
            _out.WriteLine($"{hits} toolbox hits broke the cabinet's glass; the client sees {string.Join(", ", States(cabinet))}");
            Assert.True(Truthy(() => cabinet.broken));
            Assert.True(Plays(GlassHit, from).Count >= hits - 1, "each hit is heard");
            Key(KeybindingCode.SwapHands);
            Click(cabinet);
            Assert.Same(axe, Active);
            Assert.DoesNotContain("axe", States(cabinet));

            // At the Fitness window: the toolbox put down, the axe wielded (Z), one hit.
            var window = On<obj_structure_window_reinforced_fulltile>(HoleX, HoleY).Single();
            var grille = On<obj_structure_grille>(HoleX, HoleY).Single();
            var firelock = On<obj_machinery_door_firedoor>(FirelockX, FirelockY).Single();
            Put(HoleX + 1, HoleY);
            Do(() => REMOVE_TRAIT(_me, Defines.TRAIT_GODMODE, "test")); // the Fitness room only drains to half an atmosphere here
            Assert.Equal(101.3f, Kpa(FitX, FitY), 0.5f);
            Assert.False(Truthy(() => firelock.density));
            Key(KeybindingCode.SwapHands);
            Key(KeybindingCode.DropItem);
            Key(KeybindingCode.SwapHands);
            Assert.Same(axe, Active);
            Key(KeybindingCode.ActivateInhand);
            Assert.True(In(() => HAS_TRAIT(axe, Defines.TRAIT_WIELDED)), "the axe is wielded");
            Assert.Equal(24f, In(() => axe.force));
            ScreenEntry axeSlot = _replica.Screen.Values.Single(s => s.VisChildren != null && s.VisChildren.Contains(axe.dm_id));
            Assert.True(_replica.TryGetAppearance(axeSlot.VisChildren.Items.Single(c => c.Id == axe.dm_id).Appearance, out AppearanceEntry axeLook));
            Assert.Equal("fireaxe1", _replica.ResolveString(axeLook.IconState));
            _p.Session.ToggleCombatMode();
            Run(0.5f);
            from = _w.Ticks;
            Click(window);
            Assert.True(In(() => QDELETED(window)), "one wielded hit destroys the window (fireaxe.dm:68)");
            Assert.False(Seen(window));
            Assert.NotEmpty(Plays(Shatter, from));
            Assert.Equal(2, On<obj_item_shard>(HoleX, HoleY).Count + On<obj_item_shard>(HoleX - 1, HoleY).Count + On<obj_item_shard>(HoleX + 1, HoleY).Count);
            Assert.False(In(() => QDELETED(grille)), "the grille stays");
            _p.Session.ToggleCombatMode();

            // The room drains through the grille; the client sees the air stream west; the firelock closes.
            int westFlows = 0;
            long closedAt = -1;
            for (int t = 0; t < 30 * Second; t++)
            {
                _w.Run(1);
                westFlows = Math.Max(westFlows, _replica.AirflowCells.Count(c => c.X >= HoleX && c.X <= 141 && c.Y >= 183 && c.Y <= 185 && Airflow.DirOf(c.Cell) == AirflowDir.West));
                if (closedAt < 0 && t % 10 == 0 && Truthy(() => firelock.density))
                    closedAt = _w.Ticks;
            }
            float drained = Kpa(FitX, FitY);
            _out.WriteLine($"the room at {drained:0.0} kPa; the firelock closed {(closedAt < 0 ? "never" : $"{(closedAt - from) / (float)Second:0} s after the hit")}; most westward airflow cells {westFlows}; wind heard {Plays(SpaceWind, from).Count} times");
            Assert.True(drained < 60f, "the room drains: " + drained);
            Assert.True(westFlows >= 3, "the client sees the air streaming west: " + westFlows);
            Assert.NotEmpty(Plays(SpaceWind, from));
            Assert.True(closedAt > 0, "the firelock closes");

            // Two glass on the grille, the screwdriver: sealed.
            Key(KeybindingCode.DropItem); // dropping unwields (twohanded.dm:181)
            Assert.False(In(() => HAS_TRAIT(axe, Defines.TRAIT_WIELDED)));
            Key(KeybindingCode.SwapHands);
            PickUp(_toolbox);
            Key(KeybindingCode.SwapHands);
            Take<obj_item_stack_sheet_glass>(_pack);
            Click(grille, 0.5f);
            Assert.True(Until(() => On<obj_structure_window>(HoleX, HoleY).Count == 1, 5), "glass on the grille makes a window");
            var built = On<obj_structure_window>(HoleX, HoleY).Single();
            Assert.False(Truthy(() => built.anchored));
            Stow(_pack);
            Take<obj_item_screwdriver>(_toolbox);
            from = _w.Ticks;
            for (int tries = 0; tries < 3 && !Truthy(() => built.anchored); tries++)
            {
                Put(HoleX + 1, HoleY);
                Click(built, 0.5f);
                Until(() => Truthy(() => built.anchored), 5);
            }
            Assert.True(Truthy(() => built.anchored), "the screwdriver fastens the window");
            Assert.NotEmpty(Plays(Screwdriver, from));
            Stow(_toolbox);
            Run(0.5f);
            Assert.Equal(RenderKind.FullWindow, KindSeen(built));

            // An air canister (the spawn console, as the M4b check) opened from its window refills the room.
            _w.Host.EnqueueConsoleLine($"spawn /obj/machinery/portable_atmospherics/canister/air 141 185 {_z}");
            Run(1);
            var canister = On<obj_machinery_portable_atmospherics_canister_air>(141, 185).Single();
            Put(140, 185);
            Click(canister);
            UiWindowState ui = _replica.UiWindows.Values.Single(v => v.Interface == "Canister");
            Assert.True(_p.Session.UiAct(ui.Window, "valve"));
            Run(1);
            Assert.True(Truthy(() => canister.valve_open));
            CloseWindow(ui.Window);
            Assert.True(Until(() => Kpa(FitX, FitY) >= 90f, 120), "the canister refills the sealed room: " + Kpa(FitX, FitY));
            _out.WriteLine($"the Fitness room refilled to {Kpa(FitX, FitY):0.0} kPa");
        }
    }
}
