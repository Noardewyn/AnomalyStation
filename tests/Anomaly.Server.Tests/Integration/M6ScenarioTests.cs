using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ROADMAP M6's demo check through the real server on MetaStation, end to end, with three headless Godot clients
/// (<see cref="PlaySession"/>): an attacker, a victim and an observer, each a player. On the bridge the attacker breaks the fire
/// axe cabinet's glass with a toolbox (the M5 check's way), takes the axe and wields it; the victim picks a wrench up. In combat
/// mode the attacker aims at the arm holding the wrench and swings until tg's wounds mangle it and it comes off (the melee path:
/// attack, receive_damage, check_wounding, try_dismember). The observer's client hears the swings and tg's dismember sound, sees the
/// arm gone from the victim's picture, the victim's bleed overlay, the severed arm and the wrench on the floor and blood around
/// (tg's attack messages reach the attacker and the victim; the bystanders' copy waits for hearing, M9); the victim's own client's health doll shows the arm missing, the lost hand cannot pick the wrench
/// up and the other hand can. Then the heavy damage: in the Aux base construction zone tg's admin bomb (the dev explode, a heavy
/// impact at the victim) is set off until tg's ex_act takes a leg (human_defense.dm:339, its rolls), the observer seeing the leg on
/// the floor; healed of the damage but not the limb, the victim lies and crawls, slower than the walk measured before.
/// </summary>
public class M6ScenarioTests
{
    private const int Second = 20;
    private const ulong Seed = 66;

    // The bridge's fire axe cabinet and the tiles beside it.
    private const int CabinetX = 115, CabinetY = 134;
    private const int AttackerX = 114, AttackerY = 134, VictimX = 113, VictimY = 134, ObserverX = 113, ObserverY = 136;
    private const int LaneX = 110, LaneY = 135; // three free tiles east, the walk and the crawl

    // The Aux base construction zone (plating x 43-51, y 151-159): the bomb's spot and the watchers' corner, out of its range.
    private const int BombX = 47, BombY = 155, WatchX = 51, WatchY = 159;

    private const string Dismember = "sound/effects/dismember.ogg";

    private readonly ITestOutputHelper _output;

    public M6ScenarioTests(ITestOutputHelper output) => _output = output;

    private sealed class Player
    {
        public RealHeadlessWorld.Player P;
        public mob_living_carbon_human Mob;
        public ReplicaWorld Replica => P.Session.Replica;
        public readonly List<(long Tick, SoundEvent E)> Heard = new();
        public readonly List<string> Texts = new();
    }

    private RealHeadlessWorld _w;

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

    private Player Join(string name)
    {
        var p = new Player { P = _w.Join(name) };
        p.Mob = (mob_living_carbon_human)_w.MobOf(p.P);
        p.Replica.SoundsReceived += events => p.Heard.AddRange(events.Select(s => (_w.Ticks, s)));
        p.P.Client.ServerTextReceived += t => p.Texts.Add(t.Html);
        return p;
    }

    private static turf At(int x, int y) => Builtins.locate(x, y, 1);

    private void Put(Player p, int x, int y) => Do(() => Waiting.NoSleep(p.Mob.forceMove(At(x, y))));

    private static obj_item_bodypart Part(mob_living_carbon_human h, string zone) => (obj_item_bodypart)h.get_bodypart(zone);

    private obj_item Active(Player p) => In(() => p.Mob.get_active_held_item() as obj_item);

    private void Click(Player p, atom target, AimZone zone = AimZone.None, float seconds = 1)
    {
        Assert.True(p.P.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16, zone), ClickButton.Left, ClickModifiers.None));
        Run(seconds);
    }

    private void Key(Player p, KeybindingCode code, float seconds = 0.5f)
    {
        p.P.Session.Keybinding(code);
        Run(seconds);
    }

    private List<T> On<T>(int x, int y) => In(() => Dm.each_cast<atom>(At(x, y).contents).OfType<T>().ToList());

    private static List<string> States(Player watcher, atom a)
        => watcher.Replica.TryGet(a.dm_id, out ReplicaEntity e) ? CrewOutfitTests.Icons(watcher.Replica, e.State.Appearance).Select(l => l.State).Where(s => s != null).ToList() : new();

    private static List<(string Icon, string State, float Layer)> Looks(Player watcher, atom a)
        => watcher.Replica.TryGet(a.dm_id, out ReplicaEntity e) ? CrewOutfitTests.Icons(watcher.Replica, e.State.Appearance) : new();

    private static List<SoundEvent> Plays(Player p, string file, long from)
        => p.Heard.Where(h => h.Tick >= from && h.E.Kind == SoundKind.Play && h.E.File != null && h.E.File.StartsWith(file, StringComparison.Ordinal)).Select(h => h.E).ToList();

    // The health doll's zones as the victim's client reads them (D107's HudReader): tg's healthdoll limbs, "[zone][level]".
    private static List<string> DollLimbs(Player p)
    {
        foreach (ScreenEntry entry in p.Replica.Screen.Values)
        {
            var zones = new List<FoundZone>();
            HudReader.Collect(p.Replica, entry.Appearance, zones);
            if (zones.Count > 0)
                return zones.Select(z => $"{z.Zone.Zone}:{z.Zone.Kind}:{z.Zone.Level}").ToList();
        }
        return new List<string>();
    }

    // The seconds the player's forward key takes to carry it two tiles east (the cap: never).
    private float SecondsForTwoTiles(Player p, float cap)
    {
        int start = In(() => (int)p.Mob.x);
        p.P.Yaw = -Math.PI / 2; // Godot's yaw: 0 looks north, +90 degrees west
        p.P.Keys = MoveKeys.Forward;
        int t = 0;
        for (; t < cap * Second && In(() => (int)p.Mob.x) < start + 2; t++)
            _w.Run(1);
        p.P.Keys = MoveKeys.None;
        Run(1);
        return In(() => (int)p.Mob.x) >= start + 2 ? t / (float)Second : float.PositiveInfinity;
    }

    [MetaServerFact]
    public void A_fire_axe_takes_an_arm_off_a_bomb_takes_a_leg_the_observer_sees_it_and_the_victims_hands_and_legs_react()
    {
        using (_w = new RealHeadlessWorld(new LoopbackOptions { Seed = Seed, TimeoutSteps = 60000 }, MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false))
        {
            Player attacker = Join("Attacker"), victim = Join("Victim"), observer = Join("Observer");
            int bootRuntimes = _w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
            var runtimes = new List<string>();
            var sink = _w.World.RuntimeErrorSink;
            _w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); sink?.Invoke(e); };
            foreach ((int x, int y) in new[] { (AttackerX, AttackerY), (VictimX, VictimY), (ObserverX, ObserverY), (LaneX, LaneY), (LaneX + 1, LaneY), (LaneX + 2, LaneY) })
                Assert.True(In(() => At(x, y) is turf_open && !Dm.truthy(At(x, y).density)), $"({x},{y}) is open floor");
            Put(attacker, AttackerX, AttackerY);
            Put(victim, LaneX, LaneY);
            Put(observer, ObserverX, ObserverY);
            Run(1);

            // The victim's walk, for the crawl later.
            float walk = SecondsForTwoTiles(victim, 5);
            _output.WriteLine($"the victim walks two tiles in {walk:0.00} s");
            Assert.True(walk < 2, "the victim walks");
            Put(victim, VictimX, VictimY);

            // The fire axe: the cabinet's glass broken with a toolbox from the spawn console, the axe taken and wielded.
            obj_structure_fireaxecabinet cabinet = On<obj_structure_fireaxecabinet>(CabinetX, CabinetY).Single();
            obj_item_fireaxe axe = In(() => cabinet.held_item_value as obj_item_fireaxe);
            Assert.NotNull(axe);
            _w.Host.EnqueueConsoleLine($"spawn /obj/item/storage/toolbox/mechanical {AttackerX} {AttackerY} 1");
            Run(1);
            var toolbox = On<obj_item_storage_toolbox_mechanical>(AttackerX, AttackerY).Single();
            Click(attacker, toolbox);
            Assert.Same(toolbox, Active(attacker));
            attacker.P.Session.ToggleCombatMode();
            Run(1);
            int hits = 0;
            while (!In(() => Dm.truthy(cabinet.broken)) && hits++ < 60)
                Click(attacker, cabinet);
            attacker.P.Session.ToggleCombatMode();
            Run(0.5f);
            Assert.True(In(() => Dm.truthy(cabinet.broken)), "the toolbox breaks the cabinet's glass");
            Key(attacker, KeybindingCode.DropItem);
            Click(attacker, cabinet);
            Assert.Same(axe, Active(attacker));
            Key(attacker, KeybindingCode.ActivateInhand);
            Assert.True(In(() => HAS_TRAIT(axe, TRAIT_WIELDED)), "the axe is wielded");
            _output.WriteLine($"{hits} toolbox hits broke the cabinet; the axe's force {In(() => axe.force)}");

            // The victim picks a wrench up.
            obj_item_wrench wrench = In(() => Dm.@new<obj_item_wrench>(At(VictimX, VictimY)));
            Run(1);
            Click(victim, wrench);
            Assert.Same(wrench, Active(victim));
            bool left = In(() => !IS_RIGHT_INDEX(Dm.num(victim.Mob.get_held_index_of_item(wrench))));
            string zone = left ? BODY_ZONE_L_ARM : BODY_ZONE_R_ARM;
            obj_item_bodypart arm = In(() => Part(victim.Mob, zone));
            List<string> dollBefore = DollLimbs(victim);
            List<string> seenBefore = States(observer, victim.Mob);
            _output.WriteLine($"the wrench in the {zone} hand; the doll: {string.Join(", ", dollBefore)}; the observer sees: {string.Join(", ", seenBefore)}");
            Assert.Contains(zone + ":Level:0", dollBefore);
            Assert.Contains(seenBefore, s => s.Contains(zone, StringComparison.Ordinal));

            // The swings, aimed at that arm, until it comes off.
            attacker.P.Session.ToggleCombatMode();
            Run(0.5f);
            long from = _w.Ticks;
            int swings = 0;
            while (In(() => arm.owner) != null && swings++ < 40)
                Click(attacker, victim.Mob, left ? AimZone.LeftArm : AimZone.RightArm);
            attacker.P.Session.ToggleCombatMode();
            Run(2);
            _output.WriteLine($"{swings} swings; the victim's health {In(() => victim.Mob.health)}, stat {In(() => victim.Mob.stat)}; wounds: "
                + In(() => string.Join(", ", Dm.each_cast<datum_wound>(victim.Mob.all_wounds).Select(x => DmTypes.PathOf(x.GetType())))));
            Assert.Null(In(() => arm.owner));
            Assert.True(In(() => victim.Mob.stat) != DEAD, "the victim lives");

            // The observer: the swings and the dismember heard, tg's messages, the arm gone, the bleeding, the arm and the wrench on
            // the floor, blood around.
            Assert.True(Plays(observer, "sound/items/weapons/bladeslice", from).Count + Plays(observer, "sound/items/weapons/genhit", from).Count > 0, "the swings are heard");
            Assert.NotEmpty(Plays(observer, Dismember, from));
            foreach (Player who in new[] { attacker, victim, observer })
                _output.WriteLine($"{In(() => who.Mob.name)}'s texts ({who.Texts.Count}): " + string.Join(" | ", who.Texts.Skip(Math.Max(0, who.Texts.Count - 4))));
            // tg's attack messages reach the attacker and the victim; the bystanders' copy (visible_message to viewers) waits for
            // hearing in M9 (mob.dm:280, a deferred leaf).
            Assert.Contains(attacker.Texts, t => t.Contains("with the fire axe", StringComparison.Ordinal));
            Assert.Contains(victim.Texts, t => t.Contains("severing it completely!", StringComparison.Ordinal));
            List<(string Icon, string State, float Layer)> seenAfter = Looks(observer, victim.Mob);
            _output.WriteLine("the observer sees the victim: " + string.Join(", ", seenAfter.Select(l => $"{l.Icon}:{l.State}")));
            Assert.DoesNotContain(seenAfter, l => l.State != null && l.State.Contains(zone, StringComparison.Ordinal) && !l.State.Contains("stump", StringComparison.Ordinal)
                && l.Icon != null && l.Icon != "icons/mob/effects/bleed_overlays.dmi");
            // tg's stump is an invisible bodypart that cannot bleed (stumps.dm:1): the loss shows as the gap and the blood sprayed
            // (dismemberment.dm:37); the overlay is the other cuts' bleeding, when the swings left any (the wound rolls are random)
            bool otherCuts = In(() => Dm.each_cast<datum_wound>(victim.Mob.all_wounds).Any());
            Assert.Equal(otherCuts, seenAfter.Any(l => l.Icon == "icons/mob/effects/bleed_overlays.dmi"));
            Assert.True(observer.Replica.TryGet(arm.dm_id, out ReplicaEntity severed), "the observer sees the severed arm");
            Assert.Contains(States(observer, arm), s => s.Contains(zone, StringComparison.Ordinal));
            Assert.True(In(() => wrench.loc is turf), "the lost hand dropped the wrench");
            Assert.True(observer.Replica.TryGet(wrench.dm_id, out _), "the observer sees the wrench on the floor");
            Run(5);
            var bloodIds = In(() => Enumerable.Range(VictimX - 2, 5).SelectMany(x => Enumerable.Range(VictimY - 2, 5).SelectMany(y => On<obj_effect_decal_cleanable_blood>(x, y))).Select(b => b.dm_id).ToList());
            int bloodSeen = bloodIds.Count(id => observer.Replica.TryGet(id, out _));
            _output.WriteLine($"the severed arm at ({severed.State.X},{severed.State.Y}); {bloodIds.Count} blood decals around, {bloodSeen} seen by the observer; blood volume {In(() => victim.Mob.blood_volume)}");
            Assert.True(bloodSeen > 0, "the observer sees blood on the floor");
            Assert.True(In(() => victim.Mob.blood_volume) < BLOOD_VOLUME_NORMAL, "the victim bleeds");

            // The victim's own client: the doll without the arm; the lost hand cannot pick the wrench up, the other one can.
            List<string> dollAfter = DollLimbs(victim);
            _output.WriteLine("the victim's doll: " + string.Join(", ", dollAfter));
            Assert.Contains(zone + ":Missing:0", dollAfter);
            Put(victim, In(() => (int)wrench.x), In(() => (int)wrench.y));
            Run(1);
            if (In(() => victim.Mob.stat) == STABLE)
            {
                bool lostActive = In(() => Part(victim.Mob, zone) == null && !Dm.truthy(victim.Mob.has_hand_for_held_index(victim.Mob.active_hand_index)));
                _output.WriteLine($"the victim's active hand is the lost one: {lostActive}");
                if (lostActive)
                {
                    Click(victim, wrench);
                    Assert.True(In(() => wrench.loc is turf), "the lost hand cannot pick the wrench up");
                    Key(victim, KeybindingCode.SwapHands);
                }
                Click(victim, wrench);
                Assert.Same(wrench, Active(victim));
                Assert.Equal(left ? 2f : 1f, In(() => Dm.num(victim.Mob.get_held_index_of_item(wrench))));
            }

            // The heavy damage: tg's admin bomb at the victim in the construction zone, the others watching from its corner.
            Put(attacker, WatchX, WatchY);
            Put(observer, WatchX - 1, WatchY);
            Do(() =>
            {
                ADD_TRAIT(attacker.Mob, TRAIT_GODMODE, "test"); // the bomb may open the zone to space; the watchers stay out of it
                ADD_TRAIT(observer.Mob, TRAIT_GODMODE, "test");
            });
            obj_item_bodypart legLost = null;
            int bombs = 0;
            while (legLost == null && bombs++ < 20) // each heavy impact rolls 40% x 33% per limb (human_defense.dm:339)
            {
                if (bombs > 1)
                {
                    observer.P.Session.DevCommand($"fullheal #{victim.Mob.dm_id}");
                    Run(1);
                }
                Put(victim, BombX, BombY);
                Run(1);
                var legs = In(() => new[] { Part(victim.Mob, BODY_ZONE_L_LEG), Part(victim.Mob, BODY_ZONE_R_LEG) });
                from = _w.Ticks;
                observer.P.Session.DevCommand($"explode {BombX} {BombY} 1 0 1 2");
                Run(3);
                legLost = legs.FirstOrDefault(l => In(() => l.owner) == null);
                _output.WriteLine($"bomb {bombs}: limbs {In(() => string.Join(",", Dm.each_cast<obj_item_bodypart>(victim.Mob.bodyparts).Select(b => b.body_zone)))}; health {In(() => victim.Mob.health)}, stat {In(() => victim.Mob.stat)}; the legs' owners {string.Join(",", legs.Select(l => In(() => l.owner?.dm_id)))}, the victim {victim.Mob.dm_id}; wounds "
                    + In(() => string.Join(", ", Dm.each_cast<datum_wound>(victim.Mob.all_wounds).Select(x => DmTypes.PathOf(x.GetType())))));
            }
            Assert.True(legLost != null, "a bomb takes a leg");
            Run(1);
            Assert.DoesNotContain(legLost, In(() => Dm.each_cast<obj_item_bodypart>(victim.Mob.bodyparts).ToList()));
            Assert.True(In(() => victim.Mob.stat) != DEAD, "the victim lives");
            Assert.True(observer.Replica.TryGet(legLost.dm_id, out ReplicaEntity leg), "the observer sees the leg");
            Assert.NotEmpty(Plays(observer, Dismember, from));
            _output.WriteLine($"the leg at ({leg.State.X},{leg.State.Y}): {string.Join(", ", States(observer, legLost))}");
            Assert.DoesNotContain(States(observer, victim.Mob), s => s.Contains((string)In(() => legLost.body_zone), StringComparison.Ordinal) && !s.Contains("stump", StringComparison.Ordinal));

            // Healed of the damage (not of the limb), back on the bridge's floor: the victim lies and crawls, slower than it walked.
            Put(victim, LaneX, LaneY);
            victim.P.Session.DevCommand("heal me");
            int waited = 0;
            while (In(() => victim.Mob.stat) != STABLE && waited++ < 30)
                Run(1);
            Run(1);
            Assert.Equal(STABLE, In(() => victim.Mob.stat));
            Assert.Equal(LYING_DOWN, In(() => victim.Mob.body_position));
            float crawl = SecondsForTwoTiles(victim, 30);
            _output.WriteLine($"one leg left: the victim crawls two tiles in {crawl:0.00} s (walked them in {walk:0.00} s)");
            Assert.True(!float.IsInfinity(crawl), "the victim crawls");
            Assert.True(crawl > walk * 2, "the crawl is slower than the walk");
            foreach (string r in runtimes)
                _output.WriteLine("RUNTIME " + r[..Math.Min(r.Length, 1500)]);
            Assert.Equal(bootRuntimes, _w.World.RuntimeErrorCount);
        }
    }
}
