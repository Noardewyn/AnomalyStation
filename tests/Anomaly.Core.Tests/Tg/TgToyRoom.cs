using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.UnitTests;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg;

/// <summary>
/// The test side of the toy room (wave 4, W4.M): the host helper <see cref="ToyRoom"/> builds the map from tg's real types;
/// this wrapper adds what only a test world needs and a boot does by itself: tg's world start in miniature (the logger,
/// SSgarbage, SStimer, GLOB with its InitGlobal procs run through <c>Dm.@new</c>, SSchat and the configuration, SSdcs,
/// SSmapping, SSicon_smooth, SSobj, SSair, SSspatial_grid, SSvis_overlays, SSsound_tokens, SStimed_actions and SSatoms,
/// initialized once the room stands), so that a real <c>/obj</c> or <c>/mob</c> can be created, moved, qdel'd and asked to
/// do_after in it (what package C's <c>MobRoom</c> lists that a mob needs). Nothing is a fixture: the turfs are
/// <c>/turf/open/indestructible</c> and <c>/turf/closed/indestructible</c> with tg's own <c>Enter</c>, the area is
/// <c>/area/misc/testroom</c> with tg's own <c>Entered</c> and <c>Exited</c>, and the strict world keeps
/// <c>NotTranslatedThrows</c> on, so a reached <c>Dm.NotTranslated</c> is a failure.
/// <para>
/// Two forms: <see cref="Create"/> (a world that does not boot, stepped by hand; the room is ready at once) and
/// <see cref="Boot"/> (the real Master through <see cref="TgTestWorld"/>, the room built as the boot's <c>compiledMap</c>
/// step, tick 30 reached, RUNLEVEL_GAME set, so that SStimed_actions and SStimer fire every tick). A ported tg unit test
/// asks for the first form inside its own world through <see cref="Install"/>.
/// </para>
/// </summary>
internal sealed class TgToyRoom
{
    public const int Size = ToyRoom.DefaultSize;

    public World World { get; }

    /// <summary>The runtimes the world reported (its runtime error sink).</summary>
    public List<Exception> Runtimes { get; }

    /// <summary>The world log, in a booted room (null otherwise).</summary>
    public TgTestWorld.Log Log { get; }

    /// <summary>Whether the real Master runs in this world.</summary>
    public bool Booted { get; }

    public int Width { get; }

    public int Height { get; }

    public int Levels { get; }

    private area_misc_testroom _room;

    private TgToyRoom(World world, List<Exception> runtimes, TgTestWorld.Log log, bool booted, int width, int height, int levels)
    {
        World = world;
        Runtimes = runtimes;
        Log = log;
        Booted = booted;
        Width = width;
        Height = height;
        Levels = levels;
    }

    // ---- the two forms ----

    /// <summary>
    /// A world that does not boot, with the toy room built and SSatoms initialized (its turfs and the room are initialized
    /// when this returns). Nothing runs unless the test steps the world.
    /// </summary>
    public static TgToyRoom Create(int width = Size, int height = Size, int levels = 1, ulong seed = 21)
    {
        var runtimes = new List<Exception>();
        var world = new World(seed) { RuntimeErrorSink = runtimes.Add };
        M4bAudio.TestSoundLengths.Install(world); // the host's sound length table (M4b audio slice 5)
        var room = new TgToyRoom(world, runtimes, null, false, width, height, levels);
        using (world.Enter())
        {
            PrepareWorld();
            room._room = ToyRoom.Build(width, height, levels);
            InitializeAtoms();
        }
        return room;
    }

    /// <summary>
    /// A booted world with the real Master: the room is the boot's <c>compiledMap</c> step (the atoms it makes wait for
    /// SSatoms, which the Master initializes at tick 20), the clock stands at tick 30 and, when <paramref name="game"/>
    /// is set, the run level is RUNLEVEL_GAME (as SSticker would set it), so SStimed_actions fires every tick.
    /// <paramref name="mapExtras"/> runs right after the room is built, inside the boot (atoms made there wait for SSatoms);
    /// <paramref name="configure"/> runs before the boot (the world's file seam: tg's strings, M2 F1).
    /// </summary>
    public static TgToyRoom Boot(int width = Size, int height = Size, int levels = 1, ulong seed = 21, bool game = true, Action<World> mapExtras = null, Action<World> configure = null)
    {
        area_misc_testroom built = null;
        World world = TgTestWorld.Start(out TgTestWorld.Log log, seed, configure: configure, compiledMap: w =>
        {
            built = ToyRoom.Build(width, height, levels);
            mapExtras?.Invoke(w);
        });
        world.Step(30); // Master.Initialize sleeps 10 ds, then initializes every stage at tick 20
        Assert.True(TgTestWorld.Initialized(world), "the Master did not initialize the booted toy room");
        if (game)
            TgTestWorld.RunLevel(world, RUNLEVEL_GAME);
        return new TgToyRoom(world, log.Runtimes, log, true, width, height, levels) { _room = built };
    }

    /// <summary>
    /// For a ported tg unit test: inside the test's own world (<see cref="TgUnitTest.New"/>, after the harness made GLOB), makes
    /// whatever of tg's world start is missing, builds the toy room, initializes SSatoms and sets the test's
    /// <c>run_loc_floor_bottom_left</c> and <c>run_loc_floor_top_right</c> (unit_test.dm:61-62).
    /// </summary>
    public static void Install(TgUnitTest test, int width = Size, int height = Size)
    {
        PrepareWorld();
        ToyRoom.Build(width, height);
        InitializeAtoms();
        test.run_loc_floor_bottom_left = ToyRoom.FloorBottomLeft();
        test.run_loc_floor_top_right = ToyRoom.FloorTopRight();
    }

    // ---- what a test world needs and a boot makes ----

    /// <summary>
    /// Gaps E2b: the dark matt-eor raises the alert when it is made (meteor_dark_matteor.dm:23), so the level datums must exist, as
    /// tg's MC always creates and initializes SSsecurity_level before the atoms (a harness Master may have made it uninitialized).
    /// </summary>
    public static void InitializeSecurityLevel()
    {
        if (SSsecurity_level == null)
            Dm.@new<datum_controller_subsystem_security_level>();
        if (SSsecurity_level.current_security_level == null)
            Waiting.NoSleep(SSsecurity_level.Initialize());
    }

    /// <summary>
    /// In the current world: tg's world start in miniature, each piece only when it is missing (so the harness of the ported
    /// unit tests, which made GLOB, SSgarbage, SSdcs and SSobj already, keeps them). SSatoms is made new and uninitialized.
    /// </summary>
    public static void PrepareWorld()
    {
        MapGrid.Attach(World.Current); // every turf is created at the assignment that makes its cell, as in BYOND
        if (Dm.world.area == null)
            TgWorld.dm_init_vars(Dm.world); // the /world var block of world.dm:11-24: world.area is /area/space, tg's world area
        if (logger == null)
            logger = Dm.@new<datum_log_holder>(); // Genesis creates it before the Master (game/world.dm:97)
        if (SSgarbage == null)
            Dm.@new<datum_controller_subsystem_garbage>(); // qdel reads it (garbage.dm:346)
        if (SStimer == null)
            Dm.@new<datum_controller_subsystem_timer>(); // GLOB's New queues a QDEL_IN (globals.dm:24); the cogbar's end too
        if (SSmaterials == null)
            Dm.@new<datum_controller_subsystem_materials>(); // GLOB's make_datum_reference_lists reads its recipes (global_lists.dm:93)
            Dm.@new<datum_controller_subsystem_accessories>(); // M10x slice D2: tg makes it at PreInit, before GLOB (sprite_accessories.dm:47); bodies read its lists
        if (GLOB__var == null)
            Dm.@new<datum_controller_global_vars>(); // New runs every InitGlobal proc (globals.dm:11, :48)
        // gaps P3: SSatoms' setupGenetics makes every mutation, and the speech ones read tg's strings files in their New
        if (Tools.TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(World.Current).Files, Tools.TgClone.Root, TgStrings.GeneticsFiles);
        TgTestGenesis.Install(); // SSchat and the configuration
        if (SSdcs == null)
            Dm.@new<datum_controller_subsystem_processing_dcs>();
        if (SSmapping == null)
            Dm.@new<datum_controller_subsystem_mapping>();
        if (SSicon_smooth == null)
            Dm.@new<datum_controller_subsystem_icon_smooth>();
        if (SSore_generation == null)
            Dm.@new<datum_controller_subsystem_ore_generation>(); // Lavaland L2c: an ore vent and a boulder register with it (ore_vent.dm:90, boulder.dm:40), tg's MC always creates it
        if (SSstation == null)
        {
            Dm.@new<datum_controller_subsystem_processing_station>(); // the small crystal announces itself (supermatter_variants.dm:67), tg's MC always has it
            SSstation.announcer = Dm.@new<datum_centcom_announcer_default>();
        }
        if (SSobj == null)
            Dm.@new<datum_controller_subsystem_processing_obj>(); // /obj/Destroy stops processing on it (objs.dm:74)
        if (SSair == null)
            Dm.@new<datum_controller_subsystem_air>(); // a turf's Destroy and a space turf's Initialize call into it
        if (SStransport == null)
            Dm.@new<datum_controller_subsystem_processing_transport>(); // D325: a platform's controller and an elevator panel or button look the lifts up in it, tg's MC always creates it
        if (SSspatial_grid == null)
            Dm.@new<datum_controller_subsystem_spatial_grid>(); // /mob/Initialize makes the mob hearing sensitive (mob.dm:100)
        if (SSpoints_of_interest == null)
            Dm.@new<datum_controller_subsystem_points_of_interest>(); // M10x slice C: a living mob and a ghost are points of interest (living.dm:19), tg's MC always creates it
        if (SSlighting == null)
            Dm.@new<datum_controller_subsystem_lighting>(); // M7 lumcount: created, not initialized: the room's turfs have no lighting mark and count as lit (get_lumcount's !lighting_object, lighting_turf.dm:22)
        if (SSradio == null)
            Dm.@new<datum_controller_subsystem_radio>(); // M9 slice D: a radio registers its frequencies (radio.dm:168), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_cameras>(); // M10 slice D: a security camera registers itself (camera.dm:124), tg's MC always creates it
            Waiting.NoSleep(Dm.@new<datum_controller_subsystem_verbs>().Initialize()); // silicons SI4: a human and an AI are given their verbs (human.dm:2, ai.dm:79), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_pai>(); // silicons SI6: a pAI card joins SSpai's card list (card.dm:32), tg's MC always creates it
            Waiting.NoSleep(Dm.@new<datum_controller_subsystem_processing_instruments>().Initialize()); // silicons SI6c: an instrument's song takes its instrument from SSinstruments (_song.dm:182), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_persistent_paintings>(); // silicons SI4d: a painting frame registers itself (paintings.dm:585), tg's MC always creates it
        if (SSskills == null)
            Waiting.NoSleep(Dm.@new<datum_controller_subsystem_skills>().Initialize()); // gaps SL1: a mind's skills read the skill singletons (mind/skills.dm, GetSkillRef), tg's MC always creates it
        if (SSmodular_computers == null)
            Dm.@new<datum_controller_subsystem_modular_computers>(); // M9 slice E: the NTNet log of a relay and of the PDAs (modular_computers.dm:146), tg's MC always creates it
        if (SSvis_overlays == null)
            Dm.@new<datum_controller_subsystem_vis_overlays>(); // the cogbar's Destroy (cogbar.dm:46)
        if (SSsound_tokens == null)
            Dm.@new<datum_controller_subsystem_sound_tokens>(); // /mob/Moved with a client (mob_movement.dm:590)
        if (SSsounds == null)
        {
            // M4b audio slice 6: playsound takes a channel from SSsounds (sound.dm:36), which tg's MC always creates and
            // initializes before the atoms (INIT_ORDER_SOUNDS); a hotspot's puff and an item's drop sound reach it.
            Dm.@new<datum_controller_subsystem_sounds>();
            Waiting.NoSleep(SSsounds.Initialize());
        }
        if (SStimed_actions == null)
            Dm.@new<datum_controller_subsystem_processing_timed_actions>();
        InitializeSecurityLevel();
        if (SSmobs == null)
            Dm.@new<datum_controller_subsystem_mobs>(); // the room's levels grow its per-z lists (world.incrementMaxZ, world.dm:493)
        // M2 P2: a human's status effects (the organless blindness of setup_organless_effects, human.dm:57) start and stop
        // processing on SSfastprocess, SSprocessing or SSpriority_effects, which tg's MC always creates.
        if (SSfastprocess == null)
            Dm.@new<datum_controller_subsystem_processing_fastprocess>();
        MakeAi();
        if (SSprocessing == null)
            Dm.@new<datum_controller_subsystem_processing>();
        if (SSpriority_effects == null)
            Dm.@new<datum_controller_subsystem_processing_priority_effects>();
        // M2 F1: a GAGS item's Initialize names its icons through SSgreyscale (atom_greyscale.dm:26), and an outfit takes its
        // items from SSwardrobe (outfit.dm:162); tg's MC always creates both, and the Master counts an SS_NO_INIT one initialized.
        if (SSgreyscale == null)
        {
            Dm.@new<datum_controller_subsystem_processing_greyscale>();
            SSgreyscale.initialized = Dm.TRUE;
        }
        if (SSwardrobe == null)
            Dm.@new<datum_controller_subsystem_wardrobe>();
        // M5x slice 5: a singularity starts processing on SSsinguloprocess (singularity.dm:61), which tg's MC always creates.
        if (SSsinguloprocess == null)
            Dm.@new<datum_controller_subsystem_processing_singuloprocess>();
        // The M7 delamination slice (G6): a resonance cascade's crystal mass starts processing on SSsupermatter_cascade
        // (cascade_delam_objects.dm:25), which tg's MC always creates.
        if (SSsupermatter_cascade == null)
            Dm.@new<datum_controller_subsystem_processing_supermatter_cascade>();
        // The M7 projectiles slice: a fired projectile starts processing on SSprojectiles (projectile.dm:817) and its Destroy stops
        // it (:285), which tg's MC always creates.
        if (SSprojectiles == null)
            Dm.@new<datum_controller_subsystem_processing_projectiles>();
        // M6 slice 1: a blood decal starts drying on SSblood_drying (blood.dm:43), which tg's MC always creates.
        if (SSblood_drying == null)
            Dm.@new<datum_controller_subsystem_processing_blood_drying>();
        // M7 slice G1: a solar panel, tracker or control computer registers for COMSIG_SUN_MOVED on SSsun (solar.dm:38), which tg's MC always creates.
        if (SSsun == null)
            Dm.@new<datum_controller_subsystem_sun>();
        // An incident display reads the delamination counter on SSpersistence (incident_display.dm:102), which tg's MC always creates.
        if (SSpersistence == null)
            Dm.@new<datum_controller_subsystem_persistence>();
        // Syndicate X2: a ghost-controllable mob (a regal rat, a bot given a personality) polls the ghosts on SSpolling
        // (ghost_direct_control.dm:81), which tg's MC always creates.
        if (SSpolling == null)
            Dm.@new<datum_controller_subsystem_polling>();
        // M7 lighting slice L1: a trash item on a station level (the burnt flare, the melted candle) records its mess on SSblackbox (trash.dm:13), which tg's MC always creates.
        if (SSblackbox == null)
            Dm.@new<datum_controller_subsystem_blackbox>();
        // M6 slice 4: a dismembered limb is thrown (dismemberment.dm:50), and throw_at reads SSthrowing's state
        // (atoms_movable.dm:1442), which tg's MC always creates. The thrownthing's Destroy takes itself out of currentrun
        // (throwing.dm:116), which the ticker's first fire sets (throwing.dm:21); a world that never fires it has it set here.
        if (SSthrowing == null)
        {
            Dm.@new<datum_controller_subsystem_throwing>();
            SSthrowing.currentrun ??= Dm.list();
        }
        // M6 slice 5: gibs streak along tg's move_to loop (blood.dm:610), which runs on SSmovement, which tg's MC always creates.
        if (SSmovement == null)
            Dm.@new<datum_controller_subsystem_movement>();
        SSatoms = null; // the harness's stand-in, if any; SUBSYSTEM_DEF's New would Recover() from it
        Dm.@new<datum_controller_subsystem_atoms>();
    }

    /// <summary>Gaps C1: a basic mob's controller stands on SSai_controllers and SSai_movement, which tg's MC always creates and initializes before the atoms.</summary>
    public static void MakeAi()
    {
        if (SSai_movement == null)
        {
            Dm.@new<datum_controller_subsystem_movement_ai_movement>();
            SSai_movement.SetupAIMovementInstances();
        }
        if (SSai_controllers == null)
        {
            Dm.@new<datum_controller_subsystem_ai_controllers>();
            SSai_controllers.setup_targeting_strats();
            SSai_controllers.setup_target_priority_strats();
            SSai_controllers.setup_target_sources();
            SSai_controllers.on_max_z_changed();
        }
    }

    /// <summary>Runs SSatoms' Initialize as the Master's init_subsystem would (the room's turfs and area initialize here).</summary>
    public static void InitializeAtoms()
    {
        // M4b audio slice 6: playsound finds its listeners through SSspatial_grid's cells (get_hearers_in_range,
        // spatial_info.dm:147), which tg's MC initializes before the atoms (INITSTAGE_EARLY); the grid covers the built room.
        if (!Dm.truthy(SSspatial_grid.initialized))
            Waiting.NoSleep(SSspatial_grid.Initialize());
        Waiting.NoSleep(SSatoms.Initialize());
    }

    // ---- the room ----

    /// <summary>The <c>/area/misc/testroom</c> of the room.</summary>
    public area_misc_testroom Room => _room;

    public void In(Action body)
    {
        using (World.Enter())
            body();
    }

    public T In<T>(Func<T> body)
    {
        using (World.Enter())
            return body();
    }

    /// <summary>The turf at (x, y, z) (inside the world's scope).</summary>
    public static turf T(int x, int y, int z = 1) => locate(x, y, z);

    /// <summary>tg's run_loc_floor_bottom_left (inside the world's scope).</summary>
    public static turf BottomLeft(int z = 1) => ToyRoom.FloorBottomLeft(z);

    /// <summary>tg's run_loc_floor_top_right (inside the world's scope).</summary>
    public static turf TopRight(int z = 1) => ToyRoom.FloorTopRight(z);

    /// <summary>The runtimes reported so far as text, for assertions that show them.</summary>
    public IEnumerable<string> RuntimeText => Runtimes.Select(e => e.ToString());

    /// <summary>Steps the world <paramref name="ticks"/> ticks.</summary>
    public void Step(int ticks = 1) => World.Step(ticks);
}
