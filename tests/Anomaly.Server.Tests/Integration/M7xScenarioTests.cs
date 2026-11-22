using Anomaly.Client.Shared;
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
/// ROADMAP M7x's demo check (D175) through the real server on MetaStation, with real clients (<see cref="PlaySession"/>): a security
/// officer (Alice, the outfit's items put on by hand, tg's outfit cannot be equipped in strict worlds before the implants of M8), a
/// detective (Dan, his holster by hand) and a man to shoot at (Bob), on a clear three-tile-wide lane of the station with a wall at its end (the first the map offers, found at run time: the Auxiliary Base
/// Construction room's; the Firing Range is tried first). The gear is taken from where MetaStation puts it (the armory's racks at 108,175 and 108,172,
/// the warden's locker at 105,165) by clicks, the players put beside it by a teleport (walking through the brig's doors would only add ticks).
/// The officer draws the disabler from her suit storage and disables Bob by clicks (stamina crit in tg's number of hits). The armory's
/// laser gun, taken from its rack on the armory floor by a click, burns him through the security vest by the armour formula; the
/// warden's locker opens to the Captain's spare ID with its laser and compact shotgun inside. The detective's revolver, drawn from
/// his holster by clicks and aimed by the camera's ray (D177), wounds Bob's arm and nothing else, hurts his head when aimed at it
/// and, aimed beside him, misses and hits the wall behind. The armory's riot shotgun is pumped between shots and reloaded shell by
/// shell, a Makarov's magazine is swapped by a click, a temperature gun without its firing pin refuses to fire with tg's message.
/// The flash blinds Bob (his client gets the flash screen), the stun baton from the security belt knocks him down, the cuffs from the
/// pocket restrain him. The clients see the held gun's picture, each shot's Shot field (its height), the firing effect and the impact,
/// and hear the guns. A second test runs the shooting part twice from a fresh boot with the same seed and compares the state hashes.
/// </summary>
public class M7xScenarioTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const ulong Seed = 91;

    private sealed class Actor
    {
        public RealHeadlessWorld.Player P;
        public mob_living_carbon_human Mob;
        public readonly List<SoundEvent> Heard = new();
        public readonly List<ServerText> Texts = new();
        public readonly Dictionary<long, (string Type, ShotState? Shot, int X, int Y)> Shots = new();
        public ReplicaWorld Replica => P.Session.Replica;
    }

    private sealed record Lane(turf Shooter, turf Near, turf Target, int Dir, int Side, turf_closed_wall Wall, int Length);

    private sealed class Scenario
    {
        public RealHeadlessWorld W;
        public Actor Alice, Dan, Bob;
        public Lane Lane;
        public int BootRuntimes;
        public readonly List<string> Runtimes = new();
        public ITestOutputHelper Out;

        public T In<T>(Func<T> f)
        {
            using (W.World.Enter())
                return f();
        }

        public void Do(Action a)
        {
            using (W.World.Enter())
                a();
        }

        public void Run(float seconds) => Ticks((int)(seconds * Second));

        // The ticks, with every client's replica watched for the entities that carry a shot's line (the Shot field).
        public void Ticks(int n) => W.Run(n, _ =>
        {
            foreach (Actor a in new[] { Alice, Dan, Bob })
                foreach (ReplicaEntity e in a.Replica.Entities)
                    if (e.Shot.HasValue || e.IsVisual || ShotLook.KindOf(e.TypePath) != ShotKind.None)
                        a.Shots[e.Id] = (e.TypePath ?? "", e.Shot, e.State.X, e.State.Y);
        });

        public Actor Join(string name)
        {
            var a = new Actor { P = W.Join(name) };
            a.Mob = (mob_living_carbon_human)W.MobOf(a.P);
            a.Replica.SoundsReceived += events => a.Heard.AddRange(events);
            a.P.Client.ServerTextReceived += t => a.Texts.Add(t);
            return a;
        }

        public static double YawOf(int dx, int dy) => Math.Atan2(-dx, dy);

        // Teleports the actor (a set-up step: walking would only add ticks) and lets the client's prediction settle.
        public void Place(Actor a, turf t, atom faces = null)
        {
            Do(() =>
            {
                Waiting.NoSleep(a.Mob.forceMove(t));
                if (faces != null)
                    a.Mob.face_atom(faces);
            });
            if (faces != null)
            {
                (int dx, int dy) = In(() => ((int)Dm.num(faces.x) - (int)Dm.num(t.x), (int)Dm.num(faces.y) - (int)Dm.num(t.y)));
                a.P.Yaw = YawOf(Math.Sign(dx), Math.Sign(dy));
            }
        }

        // Alice or Dan on the lane's shooting tile, Bob on his, facing each other.
        public void Stage(Actor shooter, bool near = false)
        {
            Place(Bob, near ? Lane.Near : Lane.Target, shooter.Mob);
            Place(shooter, Lane.Shooter, Bob.Mob);
            int parking = 0;
            foreach (Actor other in new[] { Alice, Dan }.Where(o => o != shooter)) // the others wait beside the shooting tile, off the line
                Place(other, In(() => get_step(Lane.Shooter, parking++ == 0 ? Lane.Side : REVERSE_DIR(Lane.Side)) as turf));
            Run(0.5f);
            // a step still in the locomotion's hands may land after the placing: the shot is aimed from the lane
            if (In(() => !ReferenceEquals(shooter.Mob.loc, Lane.Shooter) && !ReferenceEquals(get_turf(shooter.Mob), Lane.Shooter)))
            {
                Place(shooter, Lane.Shooter, Bob.Mob);
                Run(0.5f);
            }
        }

        public (double X, double Y) Position(Actor a) => In(() => LocomotionSeam.TryGetState(W.World, a.Mob.dm_id, out Anomaly.Core.Motion.MotionState s, out _)
            ? (s.X / 256.0, s.Y / 256.0) : (Dm.num(a.Mob.x), Dm.num(a.Mob.y)));

        // Bob's point `lateral` tiles to the side of his centre at `up` metres, as the shooter's camera ray holds it.
        public ShotAim AimAtBob(Actor shooter, double up, double lateral = 0)
        {
            (double bx, double by) = Position(Bob);
            (int sx, int sy) = SideVector(Lane.Side);
            (float ox, float oy, int z) = shooter.P.Session.Prediction.RenderPosition();
            double eastE = (ox - 0.5) * 2, northE = (oy - 0.5) * 2, upE = WorldScale.DefaultEyeHeight;
            double dx = (bx + sx * lateral - 0.5) * 2 - eastE, dy = (by + sy * lateral - 0.5) * 2 - northE, dz = up - upE, length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            return PlaySession.AimOf(new PickRay(eastE, northE, upE, z, dx / length, dy / length, dz / length, 10));
        }

        public static (int X, int Y) DirVector(int dir) => dir switch { 4 => (1, 0), 8 => (-1, 0), 1 => (0, 1), _ => (0, -1) };
        public static (int X, int Y) SideVector(int side) => DirVector(side);

        public ClickTarget BobTarget(AimZone zone = AimZone.Chest) => new(TargetKind.Entity, Bob.Mob.dm_id, 0, 0, 0, 16, 18, zone);

        // One click on Bob with the camera's aim, past the click cooldown.
        public void Fire(Actor shooter, ShotAim aim, float seconds = 2, AimZone zone = AimZone.Chest)
        {
            Do(() => shooter.Mob.next_click = -1);
            Assert.True(shooter.P.Session.Click(BobTarget(zone), ClickButton.Left, ClickModifiers.None, aim));
            Run(seconds);
        }

        /// <summary>
        /// One click on Bob (aimed at `up` metres) right after a Life run of the mobs: the next is a whole SSmobs period (2 s) away, so the second the shot runs holds
        /// none, and the hit is read as it left Bob. A Life run heals a new wound's limb (determination, other_reagents.dm:3159), and the Master
        /// staggers SSmobs' first fire by a random 0 to 2 s (master.dm:621), so where it falls among the ticks of a shot depends on the other subsystems.
        /// </summary>
        public void FireBetweenLifeRuns(Actor shooter, double up)
        {
            float Lives() => In(() => SSmobs.times_fired);
            float fired = Lives();
            for (int ticks = 0; Lives() == fired; ticks++)
            {
                Assert.True(ticks <= 2 * Second + 2, "SSmobs did not fire within its stagger and one period");
                Ticks(1);
            }
            fired = Lives();
            Fire(shooter, AimAtBob(shooter, up), 1);
            Assert.Equal(fired, Lives());
        }

        public void ClickScreen(Actor a, atom item)
        {
            Assert.True(a.P.Session.Click(new ClickTarget(TargetKind.Screen, item.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            Run(1);
        }

        public void ClickEntity(Actor a, atom item)
        {
            Assert.True(a.P.Session.Click(new ClickTarget(TargetKind.Entity, item.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            Run(1);
        }

        public void Key(Actor a, KeybindingCode code, float seconds = 0.5f)
        {
            a.P.Session.Keybinding(code);
            Run(seconds);
        }

        public void EmptyHands(Actor a)
        {
            Do(() => Waiting.NoSleep(a.Mob.drop_all_held_items()));
            Run(0.2f);
        }

        public void HealBob(float seconds = 1)
        {
            Do(() => Waiting.NoSleep(Bob.Mob.fully_heal(HEAL_ALL)));
            Run(seconds);
        }

        public float Burn(string zone) => In(() => Dm.num(((obj_item_bodypart)Bob.Mob.get_bodypart(zone)).burn_dam));
        public float Brute(string zone) => In(() => Dm.num(((obj_item_bodypart)Bob.Mob.get_bodypart(zone)).brute_dam));

        public static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

        public static bool OnScreen(Actor a, atom item) => a.Replica.Screen.Values.Any(e => e.VisChildren != null && e.VisChildren.Contains(item.dm_id)) || a.Replica.Screen.ContainsKey(item.dm_id);

        // The item's picture: as the vis_contents child of the HUD slot that shows it (hands, worn slots, storage), else as a world entity.
        public static AppearanceEntry Look(Actor a, atom item)
        {
            foreach (ScreenEntry holder in a.Replica.Screen.Values)
                if (holder.VisChildren != null)
                    foreach (ScreenChild child in holder.VisChildren.Items)
                        if (child.Id == item.dm_id && a.Replica.TryGetAppearance(child.Appearance, out AppearanceEntry shown))
                            return shown;
            ReplicaEntity seen = a.Replica.Entities.FirstOrDefault(e => e.Id == item.dm_id);
            return seen != null && a.Replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? look : null;
        }

        public static string StateOf(Actor a, atom item) => Look(a, item) is { } look ? a.Replica.ResolveString(look.IconState) : null;

        public static List<string> OverlaysOf(Actor a, atom item) => Look(a, item) is { } look
            ? look.Overlays.Select(o => a.Replica.TryGetAppearance(o, out AppearanceEntry e) ? a.Replica.ResolveString(e.IconState) : null).Where(s => s != null).Distinct().ToList()
            : new();

        public bool Heard(Actor a, string file) => a.Heard.Any(h => h.Kind == SoundKind.Play && h.File == file);

        // The standing tile beside a thing on a table or floor.
        public turf StandBeside(atom thing) => In(() =>
        {
            turf at = get_turf(thing);
            return new[] { NORTH, SOUTH, EAST, WEST, NORTHEAST, NORTHWEST, SOUTHEAST, SOUTHWEST }.Select(d => get_step(at, d) as turf).First(t => t != null && Clear(t));
        });
    }

    // A clear lane of three tiles' width: the shooter's tile, Bob's tile four tiles on, and a wall five to nine tiles from the shooter.
    private static Lane FindLane(Scenario s)
    {
        using (s.W.World.Enter())
        {
            bool Free(turf t) => t != null && Scenario.Clear(t);
            IEnumerable<turf> Candidates(bool firingRange)
            {
                for (int x = 1; x <= 255; x++)
                    for (int y = 1; y <= 255; y++)
                    {
                        turf t = locate(x, y, 1);
                        if (t != null && Scenario.Clear(t) && get_area(t) is area room && Dm.@in(room.type, GLOB.the_station_areas) && (!firingRange || Dm.str(room.name).Contains("Firing Range", StringComparison.Ordinal)))
                            yield return t;
                    }
            }
            foreach (bool firingRange in new[] { true, false })
                foreach (turf start in Candidates(firingRange))
                    foreach (int dir in new[] { EAST, WEST, NORTH, SOUTH })
                    {
                        int side = dir == 4 || dir == 8 ? 1 : 4;
                        bool Wide(turf t) => Free(t) && Free(get_step(t, side) as turf) && Free(get_step(t, REVERSE_DIR(side)) as turf);
                        if (!Wide(start))
                            continue;
                        turf at = start;
                        int n = 0;
                        bool good = true;
                        while (good)
                        {
                            at = get_step(at, dir) as turf;
                            n++;
                            if (at == null || n > 10)
                                good = false;
                            else if (at is turf_closed_wall)
                                break;
                            else if (!Wide(at))
                                good = false;
                        }
                        if (good && n >= 6)
                            return new Lane(start, Walk(start, dir, 2), Walk(start, dir, 4), dir, side, (turf_closed_wall)at, n);
                    }
        }
        return null;

        static turf Walk(turf from, int dir, int steps)
        {
            turf t = from;
            for (int i = 0; i < steps; i++)
                t = get_step(t, dir) as turf;
            return t;
        }
    }

    private static Scenario Boot(ITestOutputHelper output, ulong seed = Seed)
    {
        var s = new Scenario { Out = output };
        s.W = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 60000 }, MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        s.Alice = s.Join("Alice");
        s.Dan = s.Join("Dan");
        s.Bob = s.Join("Bob");
        // the lane's fixed shots: Meta's bots and creatures on patrol would take the bullets aimed past Bob at the wall
        s.In(() =>
        {
            foreach (mob_living_basic creature in Dm.each<mob_living_basic>(Dm.world).ToList())
                qdel(creature);
            return 0;
        });
        s.BootRuntimes = s.W.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        Action<Exception> previous = s.W.World.RuntimeErrorSink;
        s.W.World.RuntimeErrorSink = e => { s.Runtimes.Add(e.ToString()); previous?.Invoke(e); };
        s.Lane = FindLane(s);
        Assert.NotNull(s.Lane);
        output.WriteLine($"the lane: shooter {s.In(() => $"{s.Lane.Shooter.x},{s.Lane.Shooter.y}")} to Bob {s.In(() => $"{s.Lane.Target.x},{s.Lane.Target.y}")}, the wall {s.In(() => $"{s.Lane.Wall.x},{s.Lane.Wall.y}")} ({s.In(() => Dm.str((get_area(s.Lane.Shooter) as area)?.name))})");
        return s;
    }

    // Parts 1 to 5: the disabler, the laser, the revolver, the shotguns and the pistol's magazine, the missing pin.
    private void ShootingPart(Scenario s)
    {
        Actor alice = s.Alice, dan = s.Dan, bob = s.Bob;
        ITestOutputHelper output = s.Out;

        // --- 1. The officer's kit, the outfit's items put on by hand; the disabler drawn from the suit storage and Bob disabled by clicks
        obj_item_gun_energy_disabler disabler = null;
        obj_item_assembly_flash_handheld flash = null;
        obj_item_restraints_handcuffs cuffs = null;
        obj_item_storage_belt_security_full belt = null;
        s.Do(() =>
        {
            foreach (object item in new object[] { alice.Mob.w_uniform, alice.Mob.belt, alice.Mob.l_store, alice.Mob.r_store }.Where(i => i != null).ToList())
                qdel(item);
            void Wear(obj_item item, int slot) => Waiting.NoSleep(alice.Mob.equip_to_slot_or_del(item, slot));
            turf feet = get_turf(alice.Mob);
            Wear(Dm.@new<obj_item_clothing_under_rank_security_officer>(feet), ITEM_SLOT_ICLOTHING);
            Wear(Dm.@new<obj_item_clothing_suit_armor_vest_alt_sec>(feet), ITEM_SLOT_OCLOTHING);
            disabler = Dm.@new<obj_item_gun_energy_disabler>(feet);
            Wear(disabler, ITEM_SLOT_SUITSTORE);
            belt = Dm.@new<obj_item_storage_belt_security_full>(feet);
            Wear(belt, ITEM_SLOT_BELT);
            cuffs = Dm.@new<obj_item_restraints_handcuffs>(feet);
            Wear(cuffs, ITEM_SLOT_LPOCKET);
            flash = Dm.@new<obj_item_assembly_flash_handheld>(feet);
            Wear(flash, ITEM_SLOT_RPOCKET);
            Wear(Dm.@new<obj_item_clothing_head_helmet_sec>(feet), ITEM_SLOT_HEAD);
            Wear(Dm.@new<obj_item_clothing_shoes_jackboots_sec>(feet), ITEM_SLOT_FEET);
            Wear(Dm.@new<obj_item_clothing_gloves_color_black_security>(feet), ITEM_SLOT_GLOVES);
        });
        s.Run(1);
        Assert.Same(disabler, s.In(() => alice.Mob.s_store));
        Assert.Same(belt, s.In(() => alice.Mob.belt));
        Assert.Same(cuffs, s.In(() => alice.Mob.l_store));
        Assert.Same(flash, s.In(() => alice.Mob.r_store));

        s.Stage(alice);
        s.Run(1);
        Assert.Null(s.In(() => alice.Mob.get_active_held_item()));
        long toggle = s.In(() => ((atom)alice.Mob.hud_used.screen_objects[HUD_HUMAN_TOGGLE_INVENTORY]).dm_id);
        if (!Scenario.OnScreen(alice, disabler))
        {
            Assert.True(alice.P.Session.Click(new ClickTarget(TargetKind.Screen, toggle, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            s.Run(1);
        }
        Assert.True(Scenario.OnScreen(alice, disabler), "the disabler hangs in its slot on her client");
        s.ClickScreen(alice, disabler);
        Assert.Same(disabler, s.In(() => alice.Mob.get_active_held_item()));
        Assert.Null(s.In(() => alice.Mob.s_store));
        // the held gun's picture on her client: the gun itself with its overlays (charge bars and the mode), drawn in the active hand's slot
        Assert.True(Scenario.OnScreen(alice, disabler), "the held disabler is drawn in her hand slot");
        output.WriteLine($"the held disabler's picture: {Scenario.StateOf(alice, disabler)} [{string.Join(" ", Scenario.OverlaysOf(alice, disabler))}]");
        Assert.Equal("disabler", Scenario.StateOf(alice, disabler));
        Assert.Contains("disabler_charge", Scenario.OverlaysOf(alice, disabler));

        float maxHealth = s.In(() => bob.Mob.maxHealth), crit = s.In(() => bob.Mob.crit_threshold);
        int expected = (int)Math.Ceiling((maxHealth - crit) / 30f);
        float stamina = s.In(() => Dm.num(bob.Mob.get_stamina_loss()));
        int hits = 0;
        bool shotSeen = false;
        for (; hits < 8 && !s.In(() => HAS_TRAIT_FROM(bob.Mob, TRAIT_INCAPACITATED, STAMINA)); hits++)
        {
            bob.Heard.Clear();
            s.Fire(alice, s.AimAtBob(alice, 0.8), 3);
            float now = s.In(() => Dm.num(bob.Mob.get_stamina_loss()));
            Assert.Equal(stamina + 30, now, 0.01);
            stamina = now;
            Assert.True(s.Heard(bob, "sound/items/weapons/taser2.ogg"), "Bob's client hears the disabler");
            shotSeen |= alice.Shots.Values.Any(x => x.Type.StartsWith("/obj/projectile/beam/disabler", StringComparison.Ordinal) && x.Shot.HasValue);
        }
        output.WriteLine($"the disabler: {hits} hits to stamina crit (tg's {expected})");
        Assert.Equal(expected, hits);
        Assert.True(s.In(() => HAS_TRAIT_FROM(bob.Mob, TRAIT_INCAPACITATED, STAMINA)), "stamina crit");
        s.Run(1);
        Assert.True(PostureBits.IsLying(bob.Replica.Entities.Single(e => e.Id == bob.Mob.dm_id).State.Posture), "Bob's client sees him lying");
        Assert.True(shotSeen, "the client sees the disabler's bolt with its Shot field");
        s.EmptyHands(alice);
        s.HealBob();

        // --- 2. The laser from the armory's rack (a click on the gun lying there), through the vest by the armour formula
        List<obj_item_gun_energy_laser> racked = s.In(() => Dm.each<obj_item_gun_energy_laser>(Dm.world).Where(g => g.loc is turf t && Dm.eq(t.x, 108) && Dm.eq(t.y, 175)).ToList());
        Assert.Equal(3, racked.Count);
        Assert.Contains("Armory", s.In(() => Dm.str((get_area(racked[0]) as area)?.name)));
        s.Place(alice, s.StandBeside(racked[0]));
        s.Run(1);
        s.ClickEntity(alice, racked[0]);
        obj_item_gun_energy_laser laser = Assert.IsAssignableFrom<obj_item_gun_energy_laser>(s.In(() => alice.Mob.get_active_held_item()));
        Assert.Contains(laser, racked);
        s.Stage(alice);
        output.WriteLine($"the held laser's picture: {Scenario.StateOf(alice, laser)} [{string.Join(" ", Scenario.OverlaysOf(alice, laser))}]");
        Assert.Equal("laser", Scenario.StateOf(alice, laser));
        Assert.Contains("laser_charge4", Scenario.OverlaysOf(alice, laser));
        alice.Shots.Clear();
        bob.Heard.Clear();
        s.FireBetweenLifeRuns(alice, 0.8);
        float bare = s.In(() => Dm.num(bob.Mob.get_fire_loss()));
        output.WriteLine($"the bare laser burn: {bare}, the chest's {s.Burn(BODY_ZONE_CHEST)}");
        Assert.True(bare > 0 && Math.Abs(bare - s.Burn(BODY_ZONE_CHEST)) < 0.01, "the laser burns the chest, aimed at it");
        Assert.True(s.Heard(bob, "sound/items/weapons/laser.ogg"), "the laser is heard");
        AssertShotKinds(alice, ShotKind.Bolt);
        ShotState laserLine = alice.Shots.Values.First(x => ShotLook.KindOf(x.Type) == ShotKind.Bolt).Shot.Value;
        Assert.Equal(WorldScale.DefaultEyeHeight * 16, laserLine.HeightPixels, 0.5); // from her eye (1.3 m = 20.8 px), down to his chest
        Assert.True(laserLine.SlopeValue < 0, "the beam slopes down to his chest");
        double heading = s.Lane.Dir switch { 1 => 0, 4 => 90, 2 => 180, _ => 270 }; // clockwise from north
        Assert.True(Math.Abs((laserLine.AngleDegrees - heading + 540) % 360 - 180) < 10, "and runs along the lane");
        s.HealBob();
        s.Do(() => Waiting.NoSleep(bob.Mob.equip_to_slot_or_del(Dm.@new<obj_item_clothing_suit_armor_vest_alt_sec>(get_turf(bob.Mob)), ITEM_SLOT_OCLOTHING)));
        s.Run(1);
        s.FireBetweenLifeRuns(alice, 0.8);
        float armour = s.In(() => Dm.num(bob.Mob.run_armor_check(BODY_ZONE_CHEST, LASER, "", "", 0, "", TRUE)));
        float vested = s.In(() => Dm.num(bob.Mob.get_fire_loss()));
        output.WriteLine($"through the vest ({armour} laser armour): {vested} of {bare}");
        Assert.True(armour > 0);
        Assert.Equal(bare * (1 - Math.Min(ARMOR_MAX_BLOCK, armour) / 100), vested, 0.05);
        Assert.Equal(vested, s.Burn(BODY_ZONE_CHEST), 0.01);
        s.Do(() => qdel(bob.Mob.wear_suit));
        s.HealBob();
        s.EmptyHands(alice);

        // the warden's locker (Brig Control): the Captain's spare opens it and a laser and the compact shotgun are inside
        obj_structure_closet_secure_closet_warden locker = s.In(() => Dm.each<obj_structure_closet_secure_closet_warden>(Dm.world).Single());
        Assert.Equal((105f, 165f), s.In(() => (Dm.num(locker.x), Dm.num(locker.y))));
        Assert.True(s.In(() => Dm.truthy(locker.locked)));
        Assert.Contains(s.In(() => Dm.each(locker.contents).Select(c => c.GetType()).ToList()), t => t == typeof(obj_item_gun_energy_laser));
        Assert.Contains(s.In(() => Dm.each(locker.contents).Select(c => c.GetType()).ToList()), t => t == typeof(obj_item_gun_ballistic_shotgun_automatic_combat_compact));
        s.Place(alice, s.StandBeside(locker));
        s.Do(() => Waiting.NoSleep(alice.Mob.put_in_active_hand(Dm.@new<obj_item_card_id_advanced_gold_captains_spare>(get_turf(alice.Mob)))));
        s.Run(1);
        s.ClickEntity(alice, locker);
        Assert.False(s.In(() => Dm.truthy(locker.locked)), "the spare unlocks the warden's locker");
        s.EmptyHands(alice);
        s.ClickEntity(alice, locker);
        s.Run(1);
        Assert.True(s.In(() => Dm.truthy(locker.opened)), "and a click opens it");

        // --- 3. The detective's revolver: from the holster by clicks, aimed at the arm, the head, and beside the body
        obj_item_storage_belt_holster_detective_full holster = null;
        s.Do(() =>
        {
            if (dan.Mob.belt != null)
                qdel(dan.Mob.belt);
            holster = Dm.@new<obj_item_storage_belt_holster_detective_full>(get_turf(dan.Mob));
            Waiting.NoSleep(dan.Mob.equip_to_slot_or_del(holster, ITEM_SLOT_BELT));
        });
        s.Run(1);
        obj_item_gun_ballistic_revolver_c38_detective revolver = s.In(() => Dm.each(holster.contents).OfType<obj_item_gun_ballistic_revolver_c38_detective>().Single());
        s.Stage(dan);
        s.ClickScreen(dan, holster);
        s.ClickScreen(dan, revolver);
        Assert.Same(revolver, s.In(() => dan.Mob.get_active_held_item()));
        Assert.Equal(6f, s.In(() => revolver.get_ammo(FALSE, FALSE)));
        output.WriteLine($"the held revolver's picture: {Scenario.StateOf(dan, revolver)} [{string.Join(" ", Scenario.OverlaysOf(dan, revolver))}]");
        Assert.Equal("c38", Scenario.StateOf(dan, revolver));

        // the head: a bullet aimed at the head's row hurts the head and nothing else
        dan.Shots.Clear();
        bob.Heard.Clear();
        s.Fire(dan, s.AimAtBob(dan, 26.5 * 0.05));
        Assert.True(s.Brute(BODY_ZONE_HEAD) > 0, "the head takes the bullet");
        foreach (string zone in new[] { BODY_ZONE_CHEST, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG })
            Assert.Equal(0f, s.Brute(zone));
        Assert.True(s.Heard(bob, "sound/items/weapons/gun/revolver/shot.ogg"), "the revolver is heard");
        AssertShotKinds(dan, ShotKind.Bullet);
        s.HealBob();

        // beside the body: the bullet flies past him to the wall behind and puffs its impact there
                float hurt = s.In(() => Dm.num(bob.Mob.get_brute_loss()) + Dm.num(bob.Mob.get_fire_loss()) + Dm.num(bob.Mob.get_stamina_loss()));
        dan.Shots.Clear();
        s.Fire(dan, s.AimAtBob(dan, 1.2, 0.5));
        Assert.Equal(hurt, s.In(() => Dm.num(bob.Mob.get_brute_loss()) + Dm.num(bob.Mob.get_fire_loss()) + Dm.num(bob.Mob.get_stamina_loss())));
        output.WriteLine("beside him, the client saw: " + string.Join(", ", dan.Shots.Values.Select(x => $"{x.Type} at {x.X},{x.Y}" + (x.Shot is { } shot ? $" h {shot.HeightPixels:0.0}" : ""))));
        AssertShotKinds(dan, ShotKind.Bullet);
        // tg dents a wall on 75% of bullet hits only (projectile.dm:373); every hit puffs its impact effect there, which the client sees
        // on the wall's tile (or a dense machine's before it) at the height the line had by then (between the floor and the ceiling)
        Assert.Contains(dan.Shots.Values, x => ShotLook.KindOf(x.Type) == ShotKind.Impact && x.Shot is { } shot && shot.HeightPixels is > 0 and < 32
            && s.In(() => locate(x.X, x.Y, 1) is turf_closed_wall || Dm.each_cast<atom>(((turf)locate(x.X, x.Y, 1)).contents).Any(a => a is obj && Dm.truthy(a.density)))); // the wall or a machine before it
        Assert.Equal(4f, s.In(() => revolver.get_ammo(FALSE, FALSE)));

        // the arm: probe where his arm is with the test beam of slice A (the dev hook takes her clicks), then shoot that arm
        dan.P.Session.DevCommand("shoot me /obj/projectile/beam/emitter/hitscan");
        s.Run(0.3f);
        var armAt = new List<(double Offset, string Zone)>();
        for (int step = -20; step <= 20; step++)
        {
            s.HealBob(0.2f);
            s.Fire(dan, s.AimAtBob(dan, 0.9, step * 0.02), 0.3f);
            armAt.Add((step * 0.02, new[] { BODY_ZONE_L_ARM, BODY_ZONE_R_ARM }.FirstOrDefault(z => s.Burn(z) > 0)));
        }
        // the longest run of probes that found one arm: its middle is the arm's centre, a margin for the bullets' own spread
        (double From, double To, string Zone) best = (0, 0, null);
        for (int i = 0; i < armAt.Count; i++)
        {
            if (armAt[i].Zone == null)
                continue;
            int j = i;
            while (j + 1 < armAt.Count && armAt[j + 1].Zone == armAt[i].Zone)
                j++;
            if (best.Zone == null || armAt[j].Offset - armAt[i].Offset > best.To - best.From)
                best = (armAt[i].Offset, armAt[j].Offset, armAt[i].Zone);
            i = j;
        }
        string armZone = best.Zone;
        Assert.NotNull(armZone);
        double armOffset = (best.From + best.To) / 2;
        output.WriteLine($"the {armZone} is at offsets {best.From:0.00} to {best.To:0.00}");
        s.HealBob();
        dan.P.Session.DevCommand("shoot off");
        s.Run(0.3f);
        obj_item_bodypart arm = s.In(() => (obj_item_bodypart)bob.Mob.get_bodypart(armZone));
        int shots = 0;
        for (; shots < 4 && s.In(() => Dm.each<datum_wound>(arm.wounds).Count()) == 0; shots++)
        {
            s.HealBob(); // a limb stops at its max damage (50) and a bullet there wounds no more: one bullet at a whole arm at a time
            s.Fire(dan, s.AimAtBob(dan, 0.9, armOffset), 1);
            // a new wound puts tg's determination in the blood (M8 slice B), which heals the wounded limb a little each Life tick
            // (other_reagents.dm:3159) while the shot's ticks run: exact without a wound, at most a point less with one
            // (a bullet that embeds, tg's random embed, adds its impact damage on top: M9 slice D's radios shifted the seed's rolls onto one)
            // (an embed with or without a wound: M10 slice F's round start shifted the seed's rolls onto one without)
            if (s.In(() => Dm.num(Dm.length(arm.embedded_objects))) > 0)
                Assert.True(s.Brute(armZone) > 24f);
            else if (s.In(() => Dm.each<datum_wound>(arm.wounds).Count()) == 0)
                Assert.Equal(25f, s.Brute(armZone), 0.01);
            else
                Assert.InRange(s.Brute(armZone), 24f, 25f);
            foreach (string zone in new[] { BODY_ZONE_HEAD, BODY_ZONE_CHEST, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG, armZone == BODY_ZONE_L_ARM ? BODY_ZONE_R_ARM : BODY_ZONE_L_ARM })
                Assert.Equal(0f, s.Brute(zone));
        }
        output.WriteLine($"the {armZone}: wounded by shot {shots} ({s.In(() => string.Join(", ", Dm.each<datum_wound>(arm.wounds).Select(w => DmTypes.PathOf(w.GetType()))))})");
        // tg rolls each bullet's wound (about 28% for a .38 at a bare arm: rand(1, 90) - 35 against the pierce threshold), so four shots
        // may open none; BallisticGameplayTests asserts the wound of the same bullet
        Assert.InRange(shots, 1, 4);
        s.EmptyHands(dan);
        s.HealBob();

        // --- 4. The armory's riot shotgun: pumped between shots, loaded shell by shell
        List<obj_item_gun_ballistic_shotgun_riot> riots = s.In(() => Dm.each<obj_item_gun_ballistic_shotgun_riot>(Dm.world).Where(g => g.loc is turf t && Dm.eq(t.x, 108) && Dm.eq(t.y, 172)).ToList());
        Assert.Equal(3, riots.Count);
        s.Place(alice, s.StandBeside(riots[0]));
        s.Run(1);
        s.ClickEntity(alice, riots[0]);
        obj_item_gun_ballistic_shotgun_riot shotgun = riots[0];
        Assert.Same(shotgun, s.In(() => alice.Mob.get_active_held_item()));
        s.Stage(alice, near: true); // two tiles: the pellets' cones are wide
        Assert.Equal(5f, s.In(() => shotgun.magazine.ammo_count()));
        float Hurt() => s.In(() => Dm.num(bob.Mob.get_brute_loss()) + Dm.num(bob.Mob.get_stamina_loss()));
        float before = Hurt();
        bob.Heard.Clear();
        alice.Shots.Clear();
        // tick by tick: a Life run among the shot's ticks may mend it again (SSmobs' first fire is staggered, master.dm:621)
        s.Fire(alice, s.AimAtBob(alice, 0.8), 0.05f);
        float afterFirst = Hurt();
        for (int tick = 0; tick < 2 * Second; tick++)
        {
            s.Ticks(1);
            afterFirst = Math.Max(afterFirst, Hurt());
        }
        output.WriteLine($"the first volley hurt {afterFirst - before}");
        Assert.True(s.Heard(bob, "sound/items/weapons/gun/shotgun/shot.ogg"));
        Assert.True(afterFirst > before, "some pellets hit");
        Assert.Null(s.In(() => shotgun.chambered?.loaded_projectile)); // the spent shell stays until the pump
        alice.Heard.Clear();
        s.Fire(alice, s.AimAtBob(alice, 0.8));
        Assert.Equal(afterFirst, Hurt(), 0.01);
        Assert.True(s.Heard(alice, "sound/items/weapons/gun/general/dry_fire.ogg"), "an unpumped shotgun clicks");
        s.Key(alice, KeybindingCode.ActivateInhand, 1);
        Assert.True(s.Heard(alice, "sound/items/weapons/gun/shotgun/rack.ogg"), "the pump is heard");
        Assert.NotNull(s.In(() => shotgun.chambered?.loaded_projectile));
        Assert.Equal(4f, s.In(() => shotgun.magazine.ammo_count()));
        s.Fire(alice, s.AimAtBob(alice, 0.8));
        Assert.True(Hurt() > afterFirst, "the pumped shotgun shoots again");
        for (int i = 1; i <= 2; i++)
        {
            s.Do(() => Waiting.NoSleep(alice.Mob.put_in_inactive_hand(Dm.@new<obj_item_ammo_casing_shotgun_buckshot>(get_turf(alice.Mob)))));
            s.Run(0.3f);
            s.Key(alice, KeybindingCode.SwapHands, 0.5f);
            alice.Heard.Clear();
            s.ClickScreen(alice, shotgun);
            s.Run(1);
            Assert.Equal(4f + i, s.In(() => shotgun.magazine.ammo_count()));
            Assert.True(s.Heard(alice, "sound/items/weapons/gun/shotgun/insert_shell.ogg"), "each shell is heard going in");
            s.Key(alice, KeybindingCode.SwapHands, 0.5f);
            Assert.Same(shotgun, s.In(() => alice.Mob.get_active_held_item()));
        }
        Assert.Equal(6f, s.In(() => shotgun.magazine.ammo_count()));
        s.EmptyHands(alice);
        s.HealBob();

        // the pistol's magazine swapped by a click: the new one in, the old one out
        obj_item_gun_ballistic_automatic_pistol pistol = null;
        obj_item_ammo_box_magazine_m9mm fresh = null;
        s.Do(() =>
        {
            pistol = Dm.@new<obj_item_gun_ballistic_automatic_pistol>(get_turf(alice.Mob));
            Waiting.NoSleep(alice.Mob.put_in_active_hand(pistol));
        });
        s.Run(0.5f);
        obj_item_ammo_box_magazine_m9mm worn = s.In(() => (obj_item_ammo_box_magazine_m9mm)pistol.magazine);
        s.Fire(alice, s.AimAtBob(alice, 0.8), 1);
        float wornLeft = s.In(() => worn.ammo_count());
        s.Do(() =>
        {
            fresh = Dm.@new<obj_item_ammo_box_magazine_m9mm>(get_turf(alice.Mob));
            Waiting.NoSleep(alice.Mob.put_in_inactive_hand(fresh));
        });
        s.Run(0.3f);
        s.Key(alice, KeybindingCode.SwapHands, 0.5f);
        alice.Heard.Clear();
        s.ClickScreen(alice, pistol);
        s.Run(1);
        output.WriteLine($"the pistol's magazine: {wornLeft} rounds out, {s.In(() => fresh.ammo_count())} in");
        Assert.Same(fresh, s.In(() => pistol.magazine));
        Assert.NotSame(worn, s.In(() => pistol.magazine));
        Assert.Equal(wornLeft, s.In(() => worn.ammo_count()));
        Assert.True(wornLeft < s.In(() => worn.max_ammo));
        Assert.NotEqual(worn, s.In(() => pistol.magazine));
        s.EmptyHands(alice);

        // --- 5. A gun without its firing pin refuses with tg's message
        obj_item_gun_energy_temperature pinless = s.In(() => Dm.@new<obj_item_gun_energy_temperature>(get_turf(alice.Mob)));
        s.Do(() => Waiting.NoSleep(alice.Mob.put_in_active_hand(pinless)));
        s.Run(0.5f);
        Assert.Null(s.In(() => pinless.pin));
        float cell = s.In(() => pinless.cell.charge);
        alice.Texts.Clear();
        s.Fire(alice, s.AimAtBob(alice, 0.8));
        Assert.Equal(cell, s.In(() => pinless.cell.charge));
        Assert.Contains(alice.Texts, t => t.Type == "balloon" && t.Html == "trigger locked, firing pin needed!");
        Assert.Contains(alice.Texts, t => t.Html.Contains("trigger is locked. This weapon doesn't have a firing pin installed!", StringComparison.Ordinal));
        Assert.Equal(0f, s.In(() => Dm.num(bob.Mob.get_fire_loss())));
        s.EmptyHands(alice);
    }

    // The client's view of one shooter's shot: the moving shot with its line (the Shot field: where it started, its height and heading) and the
    // firing effect on the gun.
    private void AssertShotKinds(Actor shooter, ShotKind kind)
    {
        output.WriteLine("the client saw: " + string.Join(", ", shooter.Shots.Values.Select(x => x.Shot is { } shot
            ? $"{x.Type} (h {shot.HeightPixels:0.0} px, slope {shot.SlopeValue:0.000}, heading {shot.AngleDegrees:0})" : x.Type)));
        Assert.Contains(shooter.Shots.Values, x => ShotLook.KindOf(x.Type) == kind && x.Shot.HasValue);
        Assert.Contains(shooter.Shots.Values, x => ShotLook.KindOf(x.Type) == ShotKind.Firing);
    }

    // Part 6: the flash, the baton and the cuffs, taken from the officer's slots by clicks.
    private void RestraintPart(Scenario s)
    {
        Actor alice = s.Alice, bob = s.Bob;
        ITestOutputHelper output = s.Out;
        s.HealBob();
        s.Stage(alice);
        s.Place(bob, s.In(() => get_step(alice.Mob, s.Lane.Dir) as turf), alice.Mob); // face to face for the flash
        s.Run(1);

        // the flash from her right pocket: Bob's client gets the flash screen
        s.EmptyHands(alice);
        obj_item_assembly_flash_handheld flash = s.In(() => (obj_item_assembly_flash_handheld)alice.Mob.r_store);
        bob.Heard.Clear();
        s.ClickScreen(alice, flash);
        Assert.Same(flash, s.In(() => alice.Mob.get_active_held_item()));
        Assert.Contains("flash", Scenario.OverlaysOf(alice, flash).Concat(new[] { Scenario.StateOf(alice, flash) }));
        s.Do(() => alice.Mob.next_click = -1);
        Assert.True(alice.P.Session.Click(s.BobTarget(), ClickButton.Left, ClickModifiers.None));
        s.Ticks(6);
        atom_movable_screen_fullscreen flashScreen = s.In(() => bob.Mob.screens?["flash"] as atom_movable_screen_fullscreen);
        Assert.NotNull(flashScreen);
        Assert.Contains(bob.Replica.Screen.Values, e => e.Id == flashScreen.dm_id);
        Assert.True(s.In(() => Dm.truthy(bob.Mob.IsKnockdown())), "the head-on flash knocks him down");
        Assert.True(s.Heard(bob, "sound/items/weapons/flash.ogg") || s.Heard(alice, "sound/items/weapons/flash.ogg"), "the flash is heard");
        output.WriteLine("flash ok");
        s.Run(10);
        s.EmptyHands(alice);
        s.HealBob();
        s.Do(() => alice.Mob.next_click = -1);

        // the baton from the security belt: the belt opened by a click, the baton taken, switched on, and the hit stuns
        obj_item_storage_belt_security_full belt = s.In(() => (obj_item_storage_belt_security_full)alice.Mob.belt);
        obj_item_melee_baton_security_loaded baton = s.In(() => Dm.each(belt.contents).OfType<obj_item_melee_baton_security_loaded>().Single());
        s.ClickScreen(alice, belt);
        s.ClickScreen(alice, baton);
        Assert.Same(baton, s.In(() => alice.Mob.get_active_held_item()));
        s.Key(alice, KeybindingCode.ActivateInhand, 0.3f);
        Assert.Equal(1f, s.In(() => baton.active));
        Assert.Equal("stunbaton_active", Scenario.StateOf(alice, baton));
        float charge = s.In(() => baton.cell.charge);
        s.Do(() => alice.Mob.next_click = -1);
        Assert.True(alice.P.Session.Click(s.BobTarget(), ClickButton.Left, ClickModifiers.None));
        s.Run(1);
        Assert.True(s.In(() => Dm.num(bob.Mob.get_stamina_loss())) >= 50, "the baton's stamina damage");
        Assert.True(s.In(() => baton.cell.charge) < charge, "a hit costs charge");
        s.Run(1.5f);
        Assert.True(s.In(() => Dm.truthy(bob.Mob.IsKnockdown())), "your muscles seize");
        Assert.True(PostureBits.IsLying(bob.Replica.Entities.Single(e => e.Id == bob.Mob.dm_id).State.Posture), "Bob's client sees him lying");
        Assert.True(s.Heard(bob, "sound/items/weapons/egloves.ogg"), "the baton's hit is heard");
        output.WriteLine("baton ok; Bob heard " + string.Join(", ", bob.Heard.Where(h => h.File != null).Select(h => h.File).Distinct()));

        // the cuffs from her left pocket on the downed man: tg's four seconds
        s.EmptyHands(alice);
        obj_item_restraints_handcuffs cuffs = s.In(() => (obj_item_restraints_handcuffs)alice.Mob.l_store);
        s.ClickScreen(alice, cuffs);
        Assert.Same(cuffs, s.In(() => alice.Mob.get_active_held_item()));
        bob.Heard.Clear();
        s.Do(() => alice.Mob.next_click = -1);
        Assert.True(alice.P.Session.Click(s.BobTarget(), ClickButton.Left, ClickModifiers.None));
        s.Run(2);
        Assert.Null(s.In(() => bob.Mob.handcuffed));
        s.Run(3);
        Assert.Same(cuffs, s.In(() => bob.Mob.handcuffed));
        Assert.True(s.In(() => HAS_TRAIT(bob.Mob, TRAIT_RESTRAINED)));
        Assert.NotNull(s.In(() => bob.Mob.overlays_standing[HANDCUFF_LAYER]));
        Assert.True(s.In(() => Dm.truthy(bob.Mob.alerts?[ALERT_HANDCUFFED])));
        Assert.True(s.Heard(bob, "sound/items/weapons/handcuffs.ogg"), "the cuffs are heard");
        output.WriteLine("cuffs ok; Bob heard " + string.Join(", ", bob.Heard.Where(h => h.File != null).Select(h => h.File).Distinct()));
    }

    private static void AssertClean(Scenario s)
    {
        foreach (string r in s.Runtimes)
            s.Out.WriteLine("RUNTIME " + r[..Math.Min(r.Length, 1500)]);
        Assert.True(s.Runtimes.Count == 0, string.Join("\n", s.Runtimes.Take(3)));
        Assert.Equal(s.BootRuntimes, s.W.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void An_officer_disables_burns_and_cuffs_a_man_and_a_detective_shoots_him_through_a_real_client_on_MetaStation()
    {
        Scenario s = Boot(output);
        using (s.W)
        {
            ShootingPart(s);
            RestraintPart(s);
            AssertClean(s);
        }
    }

    [MetaServerFact]
    public void The_shooting_part_run_twice_from_a_fresh_boot_ends_with_the_same_state_hash()
    {
        (ulong Hash, long Tick) Once()
        {
            Scenario s = Boot(output);
            using (s.W)
            {
                ShootingPart(s);
                return (s.W.World.ComputeStateHash(), s.W.Ticks);
            }
        }

        var first = Once();
        var second = Once();
        output.WriteLine($"first run: {first.Hash:x16} at tick {first.Tick}; second: {second.Hash:x16} at tick {second.Tick}");
        Assert.Equal(first.Tick, second.Tick);
        Assert.Equal(first.Hash, second.Hash);
    }
}
