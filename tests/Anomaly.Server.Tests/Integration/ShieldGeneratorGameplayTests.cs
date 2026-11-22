using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Engineering package E10 on MetaStation through the real server and a client's own stack. An engineer seals a hull breach with
/// one of engineering's anti-breach projectors (shieldgen.dm: the shield appears on the space tile and the room stops sharing its
/// air with it); carries two of the teleporter room's shield wall generators onto a powered cable, wrenches, unlocks and starts them
/// (the wall stands between them and stops him when he walks into it); walks through one of Meta's atmospheric holofields, which
/// shares no air with its neighbours (atmos_field_gen.dm). Then he builds a modular shield generator and its console, links them with
/// a multitool, opens both windows (ModularShieldGen, ModularShieldConsole) and raises the bubble from the console.
/// </summary>
public class ShieldGeneratorGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const double North = 0, West = Math.PI / 2, South = Math.PI, East = -Math.PI / 2; // Godot's yaw: 0 looks north, +90 degrees west

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static bool Clear(turf t) => t is turf_open && !isspaceturf(t) && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    private static turf Beside(atom thing)
    {
        foreach (float dir in new float[] { SOUTH, NORTH, EAST, WEST })
        {
            turf t = get_step(thing, dir);
            if (t != null && Clear(t))
                return t;
        }
        return null;
    }

    private static ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static AppearanceEntry Look(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? look : null;
    }

    private static string IconOf(ReplicaWorld replica, atom a)
    {
        AppearanceEntry look = Look(replica, a);
        return look == null ? null : replica.ResolveString(look.IconState);
    }

    private static List<string> OverlaysOf(ReplicaWorld replica, atom a)
    {
        var shown = new List<string>();
        AppearanceEntry look = Look(replica, a);
        if (look == null)
            return shown;
        foreach (TableRef o in look.Overlays)
        {
            if (replica.TryGetAppearance(o, out AppearanceEntry overlay))
                shown.Add(replica.ResolveString(overlay.IconState));
        }
        return shown;
    }

    private static void Hold<T>(RealHeadlessWorld w, mob_living_carbon_human me) where T : obj_item => In(w, () =>
    {
        foreach (object held in Dm.each(me.held_items.Copy()))
        {
            if (held is obj_item old)
                qdel(old);
        }
        if (typeof(T) != typeof(obj_item))
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(Dm.@new<T>(me.loc), merge_stacks: FALSE))));
    });

    private static void EmptyHands(RealHeadlessWorld w, mob_living_carbon_human me) => Hold<obj_item>(w, me);

    private void Click(RealHeadlessWorld w, RealHeadlessWorld.Player p, atom target, float seconds = 1)
    {
        Assert.True(p.Session.Click(On(target), ClickButton.Left, ClickModifiers.None));
        w.Run((int)(seconds * Second));
    }

    private static void StandAt(RealHeadlessWorld w, mob me, turf t)
    {
        Assert.NotNull(t);
        In(w, () => Waiting.NoSleep(me.forceMove(t)));
        w.Run(Second / 2);
    }

    private static float Pressure(RealHeadlessWorld w, turf t) => In(w, () => Dm.num(((turf_open)t).air.return_pressure()));

    private static (RealHeadlessWorld W, RealHeadlessWorld.Player P, mob_living_carbon_human Me, ReplicaWorld Replica, TguiWindows Windows, int BootRuntimes) Boot(ulong seed)
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        In(w, () => ADD_TRAIT(me, TRAIT_GODMODE, ADMIN_TRAIT)); // the breach vents the room he stands in
        return (w, p, me, p.Session.Replica, windows, bootRuntimes);
    }

    [MetaServerFact]
    public void An_engineer_seals_a_breach_with_a_projector_raises_a_shield_wall_on_a_powered_cable_and_walks_through_a_holofield()
    {
        var (w, p, me, replica, _, bootRuntimes) = Boot(101);
        using (w)
        {
            // ---- the anti-breach projector: Meta keeps two in engineering (the maintenance loot spawners add more) ----
            List<obj_machinery_shieldgen> projectors = In(w, () => Dm.each<obj_machinery_shieldgen>(Dm.world).Where(g => get_area(g) is area_station_engineering).OrderBy(g => g.dm_id).ToList());
            output.WriteLine("the projectors: " + string.Join("; ", In(w, () => projectors.Select(g => $"{g.x},{g.y},{g.z} {((area)get_area(g)).name} {g.GetType().Name}").ToList())));
            Assert.Equal(2, projectors.Count);
            // a breach two or three tiles from one of them: an inner floor tile blown out to space
            (obj_machinery_shieldgen projector, turf breach, turf inside) = In(w, () =>
            {
                foreach (obj_machinery_shieldgen g in projectors)
                {
                    foreach (turf t in Dm.each<turf_open_floor>(RANGE_TURFS(3, g)).OrderBy(t => t.dm_id))
                    {
                        if (get_dist(t, g) < 2 || !Clear(t) || Beside(g) == null)
                            continue;
                        foreach (float dir in new float[] { SOUTH, NORTH, EAST, WEST })
                        {
                            if (get_step(t, dir) is turf n && Clear(n) && get_dist(n, g) >= 1)
                                return (g, (turf)t, n);
                        }
                    }
                }
                return (null, (turf)null, (turf)null);
            });
            Assert.NotNull(breach);
            output.WriteLine($"the projector at {In(w, () => $"{projector.x},{projector.y} in {((area)get_area(projector)).name}")}, the breach at {In(w, () => $"{breach.x},{breach.y}")}");
            int bx = In(w, () => (int)breach.x), by = In(w, () => (int)breach.y);
            // he secures the projector first
            StandAt(w, me, In(w, () => Beside(projector)));
            Assert.Equal("shieldoff", IconOf(replica, projector));
            Hold<obj_item_wrench>(w, me);
            Click(w, p, projector);
            Assert.True(In(w, () => Dm.truthy(projector.anchored)));
            EmptyHands(w, me);
            float before = Pressure(w, inside);

            // the hull is breached: the room vents into space
            In(w, () => Waiting.NoSleep(breach.ChangeTurf(typeof(turf_open_space))));
            turf space = In(w, () => locate(bx, by, 1));
            Assert.True(In(w, () => isspaceturf(space)));
            w.Run(Second / 2);
            Assert.True(In(w, () => Dm.@in(space, inside.atmos_adjacent_turfs)), "the breach shares the room's air");
            float venting = Pressure(w, inside);

            // the projector on: a shield on the breach, and the room no longer shares its air with it
            Click(w, p, projector, 0.25f);
            Assert.True(In(w, () => Dm.truthy(projector.active)));
            obj_structure_emergency_shield seal = In(w, () => Dm.locate<obj_structure_emergency_shield>(space));
            Assert.NotNull(seal);
            Assert.False(In(w, () => Dm.@in(space, inside.atmos_adjacent_turfs)), "the shield holds the air");
            float sealedAt = Pressure(w, inside);
            w.Run(5 * Second);
            float later = Pressure(w, inside);
            output.WriteLine($"beside the breach: {before} kPa before, {venting} venting, {sealedAt} sealed, {later} five seconds later");
            Assert.True(venting < before, "the breach vents");
            Assert.True(later >= sealedAt - 0.5f, "the room stops venting");
            Assert.True(later > 20f, "the room keeps its air");
            Assert.Equal("shieldon", IconOf(replica, projector));
            Assert.Equal("shield-old", IconOf(replica, seal));
            // off again: the shield goes and the breach vents once more
            Click(w, p, projector);
            Assert.True(In(w, () => QDELETED(seal)));
            Assert.True(In(w, () => Dm.@in(space, inside.atmos_adjacent_turfs)));
            w.Run(Second);
            Assert.Equal("shieldoff", IconOf(replica, projector));

            // ---- two of the teleporter room's shield wall generators on a powered cable ----
            List<obj_machinery_power_shieldwallgen> gens = In(w, () => Dm.each<obj_machinery_power_shieldwallgen>(Dm.world)
                .Where(g => get_area(g) is area_station_command_teleporter).OrderBy(g => g.dm_id).ToList());
            output.WriteLine($"the teleporter room's shield wall generators: {gens.Count}");
            Assert.True(gens.Count >= 2);
            Assert.NotEmpty(In(w, () => Dm.each<obj_machinery_power_shieldwallgen_xenobiologyaccess>(Dm.world).Where(g => get_area(g) is area_station_maintenance_department_science_xenobiology).ToList()));
            obj_machinery_power_shieldwallgen a = gens[0], b = gens[1];
            // a straight run of five clear floor tiles whose two ends carry a cable of the same fed network
            (turf from, turf to, float along) = In(w, () =>
            {
                foreach (obj_structure_cable cable in Dm.each<obj_structure_cable>(Dm.world).OrderBy(c => c.dm_id))
                {
                    if (cable.z != 1 || cable.powernet is not datum_powernet net || Dm.num(net.avail) < 100000 || get_area(cable) is not area_station)
                        continue;
                    turf start = get_turf(cable);
                    foreach (float dir in new float[] { EAST, NORTH })
                    {
                        var run = new List<turf> { start };
                        for (int i = 0; i < 4; i++)
                            run.Add(run[^1] == null ? null : get_step(run[^1], dir));
                        if (run.Any(t => t == null || !Clear(t) || get_area(t) != get_area(start)))
                            continue;
                        if (Dm.each<obj_structure_cable>(run[4]).Any(c => c.powernet == net) && new[] { 0, 2, 4 }.All(i => get_step(run[i], turn(dir, 90)) is turf side && Clear(side)))
                            return (start, run[4], dir);
                    }
                }
                return ((turf)null, (turf)null, 0f);
            });
            if (from == null)
                output.WriteLine("fed networks: " + string.Join("; ", In(w, () => Dm.each<obj_structure_cable>(Dm.world).Select(c => c.powernet).OfType<datum_powernet>().Distinct()
                    .Select(n => $"{Dm.num(n.avail)}/{Dm.num(n.load)} {length(n.cables)}").Take(12).ToList())));
            Assert.NotNull(from);
            output.WriteLine($"the cable run from {In(w, () => $"{from.x},{from.y}")} to {In(w, () => $"{to.x},{to.y}")} in {In(w, () => ((area)get_area(from)).name)}");
            In(w, () =>
            {
                Waiting.NoSleep(a.forceMove(from));
                Waiting.NoSleep(b.forceMove(to));
            });
            foreach (obj_machinery_power_shieldwallgen gen in new[] { a, b })
            {
                StandAt(w, me, In(w, () => get_step(gen, turn(along, 90))));
                Hold<obj_item_wrench>(w, me);
                Click(w, p, gen);
                Assert.True(In(w, () => Dm.truthy(gen.anchored) && gen.powernet != null));
                Hold<obj_item_card_id_advanced_debug>(w, me); // the teleporter access
                Click(w, p, gen);
                Assert.Equal(0f, In(w, () => gen.locked));
                EmptyHands(w, me);
                Click(w, p, gen);
                Assert.True(In(w, () => Dm.truthy(gen.active)));
            }
            w.Run(4 * Second);
            List<obj_machinery_shieldwall> walls = In(w, () =>
            {
                var found = new List<obj_machinery_shieldwall>();
                turf t = from;
                for (int i = 0; i < 3; i++)
                {
                    t = get_step(t, along);
                    found.AddRange(Dm.each<obj_machinery_shieldwall>(t));
                }
                return found;
            });
            Assert.Equal(3, walls.Count);
            Assert.Equal((2f, 2f), In(w, () => (Dm.num(a.active), Dm.num(b.active))));
            Assert.Equal(("shield_wall_gen_on", "shield_wall_gen_on"), (IconOf(replica, a), IconOf(replica, b)));
            Assert.All(walls, wall => Assert.Equal("shieldwall", IconOf(replica, wall)));

            // he walks into the wall's middle tile and it stops him
            turf middle = In(w, () => get_turf(walls[1]));
            turf start2 = In(w, () => get_step(middle, turn(along, 90)));
            StandAt(w, me, start2);
            float toward = turn(along, 270) == NORTH ? (float)North : turn(along, 270) == SOUTH ? (float)South : turn(along, 270) == EAST ? (float)East : (float)West;
            p.Yaw = toward;
            p.Keys = MoveKeys.Forward;
            w.Run(2 * Second);
            p.Keys = MoveKeys.None;
            w.Run(Second / 2);
            Assert.Same(start2, In(w, () => get_turf(me)));

            // one generator off: the wall falls
            StandAt(w, me, In(w, () => get_step(a, turn(along, 90))));
            Click(w, p, a);
            Assert.False(In(w, () => Dm.truthy(a.active)));
            Assert.All(walls, wall => Assert.True(In(w, () => QDELETED(wall))));
            w.Run(Second);
            Assert.Equal("shield_wall_gen", IconOf(replica, a));

            // ---- an atmospheric holofield of Meta's: it shares no air and lets a person through ----
            List<obj_machinery_atmos_shield_gen> atmos = In(w, () => Dm.each<obj_machinery_atmos_shield_gen_active>(Dm.world).Where(g => get_area(g) is area_station).Cast<obj_machinery_atmos_shield_gen>().ToList());
            Assert.True(atmos.Count >= 6);
            List<obj_machinery_atmos_shield_gen> masters = In(w, () => atmos.Where(g => length(g.fields) > 0).ToList());
            output.WriteLine("the holofields: " + string.Join("; ", In(w, () => masters.Select(g => $"{g.x},{g.y} {((area)get_area(g)).name} on {g.on}").ToList())));
            Assert.Equal(atmos.Count / 2, masters.Count);
            Assert.All(atmos, g => Assert.Equal(2f, In(w, () => g.on)));
            (obj_machinery_atmos_shield_gen master, turf fieldTile, turf entry) = In(w, () =>
            {
                foreach (obj_machinery_atmos_shield_gen g in masters)
                {
                    turf t = get_turf(g);
                    foreach (float dir in new float[] { SOUTH, NORTH, EAST, WEST })
                    {
                        turf n = get_step(t, dir);
                        if (n != null && Clear(n))
                            return (g, t, n);
                    }
                }
                return (null, (turf)null, (turf)null);
            });
            Assert.NotNull(master);
            obj_effect_atmos_shield field = In(w, () => Dm.locate<obj_effect_atmos_shield>(fieldTile));
            Assert.NotNull(field);
            Assert.Equal(0f, In(w, () => LAZYLEN(fieldTile.atmos_adjacent_turfs)));
            Assert.StartsWith("atmosshield-", IconOf(replica, field));
            Assert.Contains("active", OverlaysOf(replica, master));
            // Meta puts each pair on a mass driver's blast door: the door opens (its button's job) and the field alone holds the air
            obj_machinery_door_poddoor door = In(w, () => Dm.locate<obj_machinery_door_poddoor>(fieldTile));
            Assert.NotNull(door);
            In(w, () => Waiting.Detach(door.open()));
            w.Run(3 * Second);
            Assert.False(In(w, () => Dm.truthy(door.density)), "the blast door is open");
            Assert.Equal(0f, In(w, () => LAZYLEN(fieldTile.atmos_adjacent_turfs)));
            float inner = Pressure(w, entry);
            StandAt(w, me, entry);
            float dirIn = In(w, () => get_dir(entry, fieldTile));
            p.Yaw = dirIn == NORTH ? North : dirIn == SOUTH ? South : dirIn == EAST ? East : West;
            p.Keys = MoveKeys.Forward;
            bool crossed = false;
            for (int i = 0; i < 3 * Second && !crossed; i++)
            {
                w.Run(1);
                crossed = In(w, () => get_turf(me) == fieldTile);
            }
            p.Keys = MoveKeys.None;
            output.WriteLine($"the walk: from {In(w, () => $"{entry.x},{entry.y}")} toward {In(w, () => $"{fieldTile.x},{fieldTile.y}: " + string.Join(", ", Dm.each_cast<atom>(fieldTile.contents).Select(c => c.GetType().Name + (Dm.truthy(c.density) ? "*" : "")).ToList()))}, ended at {In(w, () => $"{me.x},{me.y}")}");
            Assert.True(crossed, "the holofield lets a person through");
            Assert.False(In(w, () => QDELETED(field)));
            w.Run(2 * Second);
            Assert.InRange(Pressure(w, entry), inner - 5f, inner + 5f); // the room behind it keeps its air

            Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
        }
    }

    [MetaServerFact]
    public void An_engineer_builds_a_modular_shield_links_its_console_with_a_multitool_and_raises_the_bubble_from_the_windows()
    {
        var (w, p, me, replica, windows, bootRuntimes) = Boot(102);
        using (w)
        {
            // a room clear for three tiles around
            turf centre = In(w, () =>
            {
                foreach (turf t in Dm.each<turf_open_floor>(Dm.world).OrderBy(t => t.dm_id))
                {
                    if (t.z != 1 || get_area(t) is not area_station)
                        continue;
                    if (Dm.each<turf>(RANGE_TURFS(3, t)).All(n => Clear(n) && get_area(n) == get_area(t)))
                        return (turf)t;
                }
                return null;
            });
            Assert.NotNull(centre);
            output.WriteLine($"the generator at {In(w, () => $"{centre.x},{centre.y} in {((area)get_area(centre)).name}")}");
            obj_machinery_modular_shield_generator gen = In(w, () => Dm.@new<obj_machinery_modular_shield_generator>(centre));
            obj_machinery_computer_modular_shield console = In(w, () => Dm.@new<obj_machinery_computer_modular_shield>(get_step(get_step(centre, EAST), EAST)));
            turf between = In(w, () => get_step(centre, EAST));
            StandAt(w, me, between);
            Assert.Equal("gen_recovering_closed", IconOf(replica, gen));

            // it charges from nothing to 60 at 1.25 a second (process :379)
            for (int i = 0; i < 70 && In(w, () => Dm.truthy(gen.recovering)); i++)
                w.Run(Second);
            Assert.Equal(60f, In(w, () => gen.stored_strength));
            w.Run(Second);
            Assert.Equal("gen_ready_closed", IconOf(replica, gen));

            // the multitool carries it to the console
            Hold<obj_item_multitool>(w, me);
            Click(w, p, gen);
            Click(w, p, console);
            Assert.Single(In(w, () => Dm.each(console.generators).ToList()));

            // the console's window: one generator, full and ready
            EmptyHands(w, me);
            Click(w, p, console);
            UiWindowState consoleWindow = Assert.Single(replica.UiWindows.Tgui(), u => u.Interface == "ModularShieldConsole");
            UiNode content = Shown(windows, replica, consoleWindow.Window);
            Assert.Equal((450.0, 275.0), (windows.Get(consoleWindow.Window).Context.WindowWidth, windows.Get(consoleWindow.Window).Context.WindowHeight));
            Assert.Equal("Shield Generator", ByKey(content, "generator:1:name").TextValue);
            Assert.Equal("60/60 + 5", ByKey(content, "generator:1:strength").Text);
            Assert.Equal("Off", ByKey(content, "generator:1:power").Text);

            // its power button raises the bubble: a ring around the generator, solid after two seconds
            ByKey(content, "generator:1:power").OnClick();
            w.Run(Second);
            Assert.True(In(w, () => Dm.truthy(gen.active)));
            List<obj_structure_emergency_shield_modular> bubble = In(w, () => Dm.each<obj_structure_emergency_shield_modular>(gen.deployed_shields).ToList());
            output.WriteLine($"the bubble: {bubble.Count} tiles");
            Assert.Equal(16, bubble.Count);
            Assert.All(bubble, s => Assert.InRange(In(w, () => get_dist(s, gen)), 2f, 3f));
            w.Run(2 * Second);
            Assert.All(bubble, s => Assert.True(In(w, () => Dm.truthy(s.density))));
            AppearanceEntry look = Look(replica, bubble[0]);
            Assert.Equal(("shield-old", 255), (replica.ResolveString(look.IconState), (int)look.Alpha));
            Assert.Equal(1f, In(w, () => Dm.num(gen.current_regeneration)));
            content = Shown(windows, replica, consoleWindow.Window);
            Assert.Equal(("On", "60/60 + 1"), (ByKey(content, "generator:1:power").Text, ByKey(content, "generator:1:strength").Text));

            // the generator's own window shows it on; its button takes the bubble down
            Click(w, p, gen);
            UiWindowState genWindow = Assert.Single(replica.UiWindows.Tgui(), u => u.Interface == "ModularShieldGen");
            content = Shown(windows, replica, genWindow.Window);
            Assert.Equal((690.0, 225.0), (windows.Get(genWindow.Window).Context.WindowWidth, windows.Get(genWindow.Window).Context.WindowHeight));
            Assert.Equal(("On", true), (ByKey(content, "power").Text, ByKey(content, "power").Selected));
            Assert.Equal("Radius 3/3", ByKey(content, "radius").Text);
            Assert.True(ByKey(content, "set-radius").Disabled);
            ByKey(content, "power").OnClick();
            w.Run(Second);
            Assert.False(In(w, () => Dm.truthy(gen.active)));
            Assert.All(bubble, s => Assert.True(In(w, () => QDELETED(s))));
            content = Shown(windows, replica, genWindow.Window);
            Assert.Equal("Off", ByKey(content, "power").Text);
            Assert.False(ByKey(content, "set-radius").Disabled);

            Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
        }
    }
}
