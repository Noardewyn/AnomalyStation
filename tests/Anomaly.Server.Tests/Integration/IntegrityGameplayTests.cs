using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
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
/// M5 slice 1 through the real server on MetaStation, with a headless Godot client (<see cref="PlaySession"/>): a player in
/// Meta's Fitness Room (x 137..141, y 183..185) hits its reinforced window to space (136,184) with a mechanical toolbox in
/// combat mode. Each hit is tg's /obj/attacked_by (CALCULATE_FORCE times the toolbox's demolition_mod 1.25, through the
/// reinforced window's 80 melee armour and above its damage_deflection 11), heard as glasshit.ogg; below 75 % of its integrity
/// the client sees tg's crack overlay (damage75, then damage50 and damage25: window.dm:425-437) on the window's appearance, and
/// WindowCracks finds it as the piece renderer does. Enough hits destroy it (atom_destruction, deconstruct: the shatter, two
/// shards, rods and the glass debris; the grille under it stays), and the room vents into space through the grille: the client
/// sees the air stream west into the hole.
/// </summary>
public class IntegrityGameplayTests
{
    private const int Second = 20; // ticks per second
    private const int HoleX = 136, HoleY = 184; // the reinforced window to space
    private const int RoomX = 139, RoomY = 184; // the middle of the room
    private const string GlassHit = "sound/effects/glass/glasshit.ogg";
    private const string Shatter = "sound/effects/glass/glassbr";

    private readonly ITestOutputHelper _output;

    public IntegrityGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static T Find<T>(RealHeadlessWorld w, int x, int y) where T : class
        => In(w, () => Dm.each_cast<atom>(Builtins.locate(x, y, 1).contents).OfType<T>().FirstOrDefault());

    // What lies on the hole's tile and the space west of it (the space wind blows the light debris out).
    private static int Count<T>(RealHeadlessWorld w, int x, int y)
        => In(w, () => Enumerable.Range(x - 12, 13).SelectMany(cx => Enumerable.Range(y - 3, 7).SelectMany(cy => Dm.each_cast<atom>(Builtins.locate(cx, cy, 1).contents))).OfType<T>().Count());

    private static float Kpa(RealHeadlessWorld w, int x, int y)
        => In(w, () => Builtins.locate(x, y, 1) is turf_open t && t.air != null ? Dm.num(t.air.return_pressure()) : -1f);

    // The compositor's view of the replica (Render/ReplicaTables.cs is the client's, outside the engine-free part).
    private sealed class Tables : IAppearanceSource
    {
        private readonly ReplicaWorld _replica;

        public Tables(ReplicaWorld replica) => _replica = replica;

        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => _replica.TryGetAppearance(reference, out entry);

        public string ResolveString(TableRef reference) => _replica.ResolveString(reference);

        public string ResolveResource(TableRef reference) => _replica.ResolveResource(reference);
    }

    [MetaServerFact]
    public void Hitting_the_Fitness_window_with_a_toolbox_cracks_it_breaks_it_and_vents_the_room()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var tables = new Tables(replica);
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);

        var window = Find<obj_structure_window_reinforced_fulltile>(w, HoleX, HoleY);
        Assert.NotNull(window);
        Assert.NotNull(Find<obj_structure_grille>(w, HoleX, HoleY));
        using (w.World.Enter())
        {
            Waiting.NoSleep(me.forceMove(Builtins.locate(HoleX + 1, HoleY, 1)));
            DevCommands.Give(me, "/obj/item/storage/toolbox/mechanical");
        }
        var box = In(w, () => me.get_active_held_item() as obj_item_storage_toolbox_mechanical);
        Assert.NotNull(box);
        p.Session.ToggleCombatMode();
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(me.combat_mode)));
        Assert.Equal(101.3f, Kpa(w, RoomX, RoomY), 0.5f);

        float max = In(w, () => window.max_integrity);
        float perHit = In(w, () => Dm.round(box.force * Dm.num(box.get_demolition_modifier(window)) * (100 - Dm.num(window.get_armor_rating(Defines.MELEE))) * 0.01f, Defines.DAMAGE_PRECISION));
        _output.WriteLine($"the window: {max} integrity, {perHit} a hit");

        // The first hit: tg's damage and the glass hit sound, no crack yet.
        heard.Clear();
        p.Session.Click(new ClickTarget(TargetKind.Entity, window.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
        w.Run(Second);
        Assert.Equal(max - perHit, In(w, () => Dm.num(window.get_integrity())), 3);
        Assert.Contains(heard, e => e.Kind == SoundKind.Play && e.File == GlassHit);
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == window.dm_id);
        Assert.False(WindowCracks.TryFind(tables, seen.State.Appearance, out _, out _), "a window above 75 % shows no cracks");

        // Hit until it breaks, the cracks deepening on the client.
        var cracks = new List<string>();
        int hits = 1;
        int glassHits = heard.Count(e => e.File == GlassHit);
        while (!In(w, () => QDELETED(window)) && hits < 200)
        {
            p.Session.Click(new ClickTarget(TargetKind.Entity, window.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
            w.Run(Second);
            hits++;
            ReplicaEntity now = replica.Entities.SingleOrDefault(e => e.Id == window.dm_id);
            if (now != null && WindowCracks.TryFind(tables, now.State.Appearance, out _, out string state) && (cracks.Count == 0 || cracks[^1] != state))
                cracks.Add(state);
        }
        glassHits = heard.Count(e => e.File == GlassHit);
        _output.WriteLine($"{hits} hits broke the window; the client saw the cracks {string.Join(", ", cracks)}; {glassHits} glass hits heard");
        Assert.True(In(w, () => QDELETED(window)), "the window broke");
        Assert.Equal(new[] { "damage75", "damage50", "damage25" }, cracks);
        Assert.True(glassHits >= hits - 1, "each hit is heard");
        Assert.Contains(heard, e => e.Kind == SoundKind.Play && e.File != null && e.File.StartsWith(Shatter, StringComparison.Ordinal));
        Assert.DoesNotContain(replica.Entities, e => e.Id == window.dm_id);

        // The debris (window.dm:357): two shards, the glass debris and a reinforced window's rods (by the hole or blown west of
        // it); the grille stays.
        Assert.Equal(2, Count<obj_item_shard>(w, HoleX, HoleY));
        Assert.Equal(1, Count<obj_effect_decal_cleanable_glass>(w, HoleX, HoleY));
        Assert.Equal(1, Count<obj_item_stack_rods>(w, HoleX, HoleY));
        Assert.NotNull(Find<obj_structure_grille>(w, HoleX, HoleY));

        // The room vents into space through the grille; the client sees the air streaming west to the hole.
        int westFlows = 0;
        for (int t = 0; t < 40 * Second; t++)
        {
            w.Run(1);
            westFlows = Math.Max(westFlows, replica.AirflowCells.Count(c => c.X >= HoleX && c.X <= 141 && c.Y >= 183 && c.Y <= 185 && Airflow.DirOf(c.Cell) == AirflowDir.West));
        }
        float after = Kpa(w, RoomX, RoomY);
        _output.WriteLine($"40 s after the break: {after:0.0} kPa in the room, most westward airflow cells seen {westFlows}");
        Assert.True(after < 50f, "the room vents: " + after);
        Assert.True(westFlows >= 3, "the client sees the air streaming west to the hole: " + westFlows);
    }

    [MetaServerFact]
    public void Sparks_spread_flick_and_sound_on_the_client_and_decay()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 11, TimeoutSteps = 2000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        w.Run(Second);

        // do_sparks on the player's tile (effects_sparks.dm:8), as a broken lit tube makes them (light.dm:702)
        var seen = new HashSet<long>();
        var flicked = new HashSet<long>();
        using (w.World.Enter())
            do_sparks(3, 1, (atom)me.loc);
        w.Run(3 * Second, _ =>
        {
            foreach (ReplicaEntity e in replica.Entities)
            {
                if (!In(w, () => w.World.Objects.TryGet(e.Id, out object o) && o is obj_effect_particle_effect_sparks))
                    continue;
                seen.Add(e.Id);
                if (!e.FlickAppearance.IsNone && replica.TryGetAppearance(e.FlickAppearance, out AppearanceEntry look) && replica.ResolveString(look.IconState) == "sparks")
                    flicked.Add(e.Id);
            }
        });
        _output.WriteLine($"the client saw {seen.Count} sparks, {flicked.Count} flicking; sounds: {string.Join(", ", heard.Select(e => e.File).Distinct())}");
        Assert.Equal(3, seen.Count);
        Assert.Equal(3, flicked.Count);
        Assert.Contains(heard, e => e.Kind == SoundKind.Play && e.File != null && e.File.StartsWith("sound/effects/sparks/sparks", StringComparison.Ordinal));
        w.Run(3 * Second);
        Assert.DoesNotContain(replica.Entities, e => seen.Contains(e.Id)); // decayed (decay_in, QDEL_IN_STOPPABLE)
    }
}
