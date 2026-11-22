using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.Wave4.TurfArea;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using TgMob = Anomaly.Core.Tg.mob;

namespace Anomaly.Core.Tests.Tg.Wave4.Mob;

// Fixtures of the independent behaviour tests of package C (wave 4: mob, obj and timed actions), written from the DM code
// of mob.dm, objs.dm, timed_actions.dm, cogbar.dm and progressbar.dm. Fixture paths are /<kind>/test_mob_<name> as in the
// translator's smoke fixtures (translation-rules.md 15); the translator's types (MobFixtureTypes.cs) are reused where they
// fit. Unlike the smoke tests' MobTestWorld, these worlds keep NotTranslatedThrows on (since packages A, B and D landed,
// nothing a client-less mob reaches is a skipped skeleton) and stand mobs on real turfs of a room with gravity, so that
// step runs the real Move chain.

/// <summary>
/// A world as tg's has it right after SSatoms initialized, for tests that step the clock themselves: package B's real
/// turfs (turf_open_indestructible, with tg's own Enter) of a 7 x 7 x 1 room of /area/misc/testroom (STANDARD_GRAVITY),
/// GLOB with every translated InitGlobal proc, the logger, SSchat, the configuration, SSgarbage, SSdcs, SSmapping (its
/// per-z gravity list grown), SSobj, SSair, SSicon_smooth, SStimer, SStimed_actions, the inert SSspatial_grid stub and
/// an initialized SSatoms. No Master runs: <see cref="Poll"/> fires SStimed_actions the way the Master would.
/// </summary>
internal sealed class MobRoom
{
    public const int Size = 7;

    public readonly TurfAreaWorld Map;
    public World World => Map.World;
    public List<Exception> Runtimes => Map.Runtimes;

    public MobRoom(ulong seed = 11, int size = Size)
    {
        Map = new TurfAreaWorld(size, size, 1, typeof(turf_open_indestructible), typeof(area_misc_testroom), seed);
        // The M7 supermatter slice: the crystal starts its hum at creation (supermatter.dm:228), and a looping sound with sound
        // tokens reads its files' lengths (SSsounds.get_sound_length, sounds.dm:251), which tg's rust-g always knows: the host's
        // sound length table (M4b audio slice 5), as TgTestWorld and TgToyRoom install it.
        M4bAudio.TestSoundLengths.Install(World);
        using (World.Enter())
        {
            Dm.@new<datum_controller_subsystem_processing_timed_actions>(); // (SStimer comes with the turf world)
            TgToyRoom.MakeAi();
            Dm.@new<datum_controller_subsystem_spatial_grid>(); // /mob/Initialize makes the mob hearing sensitive (mob.dm:100)
            Dm.@new<datum_controller_subsystem_points_of_interest>(); // M10x slice C: a living mob is a point of interest (living.dm:19), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_processing_station>(); // the small crystal announces itself (supermatter_variants.dm:67), tg's MC always has it
            SSstation.announcer = Dm.@new<datum_centcom_announcer_default>();
            Dm.@new<datum_controller_subsystem_radio>(); // M9 slice D: a headset registers its frequencies (radio.dm:168)
            Dm.@new<datum_controller_subsystem_cameras>(); // M10 slice D: a security camera registers itself (camera.dm:124), tg's MC always creates it
            Waiting.NoSleep(Dm.@new<datum_controller_subsystem_verbs>().Initialize()); // silicons SI4: a human and an AI are given their verbs (human.dm:2, ai.dm:79), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_pai>(); // silicons SI6: a pAI card joins SSpai's card list (card.dm:32), tg's MC always creates it
            Waiting.NoSleep(Dm.@new<datum_controller_subsystem_skills>().Initialize()); // gaps SL1: a mind's skills read the skill singletons (GetSkillRef), tg's MC always creates it
            Waiting.NoSleep(Dm.@new<datum_controller_subsystem_processing_instruments>().Initialize()); // silicons SI6c: an instrument's song takes its instrument from SSinstruments (_song.dm:182), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_persistent_paintings>(); // silicons SI4d: a painting frame registers itself (paintings.dm:585), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_processing_transport>(); // D325: an elevator panel, button or indicator and a lift platform look the lifts up in it, tg's MC always creates it
            Dm.@new<datum_controller_subsystem_lighting>(); // M7 lumcount: created, not initialized (the turfs count as lit, lighting_turf.dm:22)
            Dm.@new<datum_controller_subsystem_vis_overlays>(); // the cogbar's Destroy reads it (cogbar.dm:46)
            Dm.@new<datum_controller_subsystem_sound_tokens>(); // /mob/Moved reads it for a mob with a client (mob_movement.dm:590)
            Dm.@new<datum_controller_subsystem_tgui>(); // M5 slice 3: Logout closes the mob's windows (logout.dm:4, SStgui.on_logout), as tg's MC always creates it
            Dm.@new<datum_controller_subsystem_movement>(); // M6 slice 5: a gib spawner's gibs streak along tg's move_to loop (blood.dm:610), on SSmovement
            Dm.@new<datum_controller_subsystem_processing_plumbing>(); // E15: a plumbing component stops processing on SSplumbing when deleted (_plumbing.dm:114), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_polling>(); // Syndicate X2: a ghost-controllable mob (a regal rat) polls the ghosts on SSpolling (ghost_direct_control.dm:81), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_security_level>(); // Syndicate N1a: a nuke reads the security level in its Initialize (_nuclear_bomb.dm:66), tg's MC always creates it
            // M4b audio slice 6: playsound takes a channel from SSsounds (sound.dm:36), which tg's MC always creates and
            // initializes before the atoms; a hotspot's puff and an item's drop sound reach it.
            Dm.@new<datum_controller_subsystem_sounds>();
            Waiting.NoSleep(SSsounds.Initialize());
            // M2 P2: a human's status effects (the organless blindness of setup_organless_effects, human.dm:57) start and stop
            // processing on SSfastprocess, SSprocessing or SSpriority_effects, which tg's MC always creates.
            if (SSfastprocess == null)
                Dm.@new<datum_controller_subsystem_processing_fastprocess>();
            if (SSprocessing == null)
                Dm.@new<datum_controller_subsystem_processing>();
            if (SSpriority_effects == null)
                Dm.@new<datum_controller_subsystem_processing_priority_effects>();
            TgToyRoom.InitializeSecurityLevel(); // gaps E2b: the dark matt-eor raises the alert at creation
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
            // The M7 projectiles slice: a fired projectile starts processing on SSprojectiles (projectile.dm:817) and its Destroy
            // stops it (:285), which tg's MC always creates.
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
            // M7 lighting slice L1: a trash item on a station level (the burnt flare, the melted candle) records its mess on SSblackbox (trash.dm:13), which tg's MC always creates.
            if (SSblackbox == null)
                Dm.@new<datum_controller_subsystem_blackbox>();
            // M9 slice E: a head's PDA looks for its fax on SSmachines (role_tablet_presets.dm:34), a relay logs to SSmodular_computers
            // (relays.dm:38) and the message server stamps the round time from SSticker (time.dm:8); tg's MC always creates them.
            if (SSmachines == null)
                Dm.@new<datum_controller_subsystem_machines>();
            if (SSticker == null)
                Dm.@new<datum_controller_subsystem_ticker>();
            if (SSmodular_computers == null)
                Dm.@new<datum_controller_subsystem_modular_computers>();
            TurfAreaWorld.InitializeSSatoms();
        }
    }

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

    /// <summary>One fire of SStimed_actions as RunQueue starts it (the subsystem is SS_RUNNING while it fires, master.dm:896).</summary>
    public static void Poll()
    {
        SStimed_actions.state = SS_RUNNING;
        Waiting.NoSleep(SStimed_actions.fire(FALSE));
        SStimed_actions.state = SS_IDLE;
    }

    /// <summary>One fire of SStimer, as the Master fires a ticker subsystem (for QDEL_IN and the cogbar's end).</summary>
    public static void FireTimer()
    {
        SStimer.state = SS_RUNNING;
        Waiting.NoSleep(SStimer.fire(FALSE));
        SStimer.state = SS_IDLE;
    }

    /// <summary>Steps the world <paramref name="ticks"/> ticks; every tick fires SStimer and SStimed_actions after the sleepers woke.</summary>
    public void Tick(int ticks = 1)
    {
        for (int i = 0; i < ticks; i++)
        {
            World.Step();
            using (World.Enter())
            {
                FireTimer();
                Poll();
            }
        }
    }

    /// <summary>The turf at x, y, level 1 (inside the world's scope).</summary>
    public static turf T(int x, int y) => locate(x, y, 1);

    /// <summary>The runtimes reported so far as text, for assertions that show them.</summary>
    public IEnumerable<string> RuntimeText => Runtimes.Select(e => e.ToString());
}

/// <summary>
/// A booted world with the real Master (TgTestWorld.Boot's route): a room of /area/misc/testroom turfs of the world's own
/// area (so every turf has gravity), tick 30 reached (the Master initialized every stage at tick 20) and the runlevel set to
/// RUNLEVEL_GAME, as SSticker would, so that SStimed_actions fires every tick. Do_afters here are polled by the real Master.
/// </summary>
internal sealed class BootedRoom
{
    public const int Size = 7;

    public readonly World World;
    public readonly TgTestWorld.Log Log;

    public BootedRoom(ulong seed = 5, Action<World> mapExtras = null)
    {
        World = TgTestWorld.Start(out Log, seed, compiledMap: w =>
        {
            BuildRoom(w);
            mapExtras?.Invoke(w); // atoms made here wait for SSatoms, as a loaded map's do
        });
        World.Step(30); // Master.Initialize sleeps 10 ds, then initializes every stage at tick 20
        Assert.True(TgTestWorld.Initialized(World));
        TgTestWorld.RunLevel(World, RUNLEVEL_GAME);
    }

    private static void BuildRoom(World world)
    {
        Dm.world.turf = typeof(turf_open_indestructible);
        Dm.world.area = typeof(area_misc_testroom);
        Dm.world.maxx = Size;
        Dm.world.maxy = Size;
        Dm.world.maxz = 1;
        SSmapping.gravity_by_z_level.len = 1;
        ToyRoom.LinkLevels(1);
    }

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

    public IEnumerable<string> RuntimeText => Log.Runtimes.Select(e => e.ToString());
}

/// <summary>
/// The caller of a do_after, as a verb or an AI loop would be: it records the result and the tick it came back on, and
/// whether it came back at all. Run with <see cref="Waiting.Detach(DmTask)"/> or from a world phase.
/// </summary>
internal sealed class DoAfterCall
{
    public bool Done;
    public object Result;
    public long DoneTick = -1;

    /// <summary>Starts <paramref name="call"/> (a do_after expression) and records how it ends. Call inside the world's scope.</summary>
    public static DoAfterCall Start(Func<DmTask<object>> call)
    {
        var record = new DoAfterCall();
        Waiting.Detach(record.Run(call));
        return record;
    }

    private async DmTask Run(Func<DmTask<object>> call)
    {
        Result = await call();
        DoneTick = World.Current.Tick;
        Done = true;
    }

    /// <summary>The result as a DM number, or fails when there is none.</summary>
    public float Number
    {
        get
        {
            Assert.True(Done, "the do_after has not come back");
            Assert.NotNull(Result);
            return Dm.num(Result);
        }
    }
}

/// <summary>A signal listener that logs the user's COMSIG_DO_AFTER_BEGAN and COMSIG_DO_AFTER_ENDED with the tick.</summary>
[DmPath("/datum/test_mob_do_after_listener")]
public class datum_test_mob_do_after_listener : datum
{
    [NotHashed] public readonly List<string> heard = new();

    [SignalHandler]
    public virtual void on_began(datum source = null) => heard.Add(World.Current.Tick + ":BEGAN");

    [SignalHandler]
    public virtual void on_ended(datum source = null) => heard.Add(World.Current.Tick + ":ENDED");
}

/// <summary>
/// A mob that never ran initialize_actionspeed: scope.md 3 C scenario (f). Everything else of /mob/Initialize runs, so its
/// cached_multiplicative_actions_slowdown stays null (the var's default, mob_defines.dm:63).
/// </summary>
[DmPath("/mob/test_mob_no_actionspeed")]
public class mob_test_mob_no_actionspeed : TgMob
{
    public override async DmTask initialize_actionspeed()
    {
        await DmTask.CompletedTask;
    }
}

/// <summary>A mob that holds an item as its active held item (inventory is M2: get_active_held_item is a skeleton).</summary>
[DmPath("/mob/test_mob_holder")]
public class mob_test_mob_holder : TgMob
{
    [NotHashed] public obj_item active_item;

    public override object get_active_held_item() => active_item;
}

/// <summary>An item without tg's /atom/New (only a thing to hold, equip or drop).</summary>
[DmPath("/obj/item/test_mob_thing")]
public class obj_item_test_mob_thing : obj_item
{
}

/// <summary>A dead mob by default (stat is a var of /mob, mob_defines.dm:73), to see which list Initialize puts it on.</summary>
[DmPath("/mob/test_mob_dead")]
public class mob_test_mob_dead : TgMob
{
    public mob_test_mob_dead()
    {
        stat = DEAD;
    }
}

/// <summary>
/// A mob that records, in the order they run, the leaves /mob/Initialize reaches (mob.dm:81-101), and whether the
/// parent's Initialize (/atom/Initialize) had marked it INITIALIZED_1 at that point: tg calls `..()` after set_nutrition and
/// before setup_hud_traits.
/// </summary>
[DmPath("/mob/test_mob_order")]
public class mob_test_mob_order : TgMob
{
    [NotHashed] public readonly List<string> order = new();

    private void Note(string what)
        => order.Add(what + (Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)) ? "+init" : ""));

    public override object update_incapacitated()
    {
        Note("update_incapacitated");
        return base.update_incapacitated();
    }

    public override async DmTask set_focus(datum new_focus = null)
    {
        Note("set_focus");
        await base.set_focus(new_focus);
    }

    public override void prepare_huds()
    {
        Note("prepare_huds");
        base.prepare_huds();
    }

    public override async DmTask set_nutrition(object set_to = null, float? forced = null)
    {
        Note("set_nutrition");
        await base.set_nutrition(set_to, forced);
    }

    public override void setup_hud_traits()
    {
        Note("setup_hud_traits");
        base.setup_hud_traits();
    }

    public override async DmTask update_config_movespeed()
    {
        Note("update_config_movespeed");
        await base.update_config_movespeed();
    }

    public override async DmTask initialize_actionspeed()
    {
        Note("initialize_actionspeed");
        await base.initialize_actionspeed();
    }

    public override void log_mob_tag(object text = null, DmList data = null)
    {
        Note("log_mob_tag");
        base.log_mob_tag(text, data);
    }
}

/// <summary>An obj whose Initialize records the id_tag registration state it sees and counts its processing.</summary>
[DmPath("/obj/test_mob_processing")]
public class obj_test_mob_processing : obj
{
    [NotHashed] public float processed;

    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        processed++;
        return null;
    }
}

/// <summary>A second obj with the same id_tag as <see cref="obj_test_mob_tagged"/> (a map bug in tg: ids are meant to be unique).</summary>
[DmPath("/obj/test_mob_tagged/twin")]
public class obj_test_mob_tagged_twin : obj_test_mob_tagged
{
}

/// <summary>A dense obj (the do_after target that blocks a mob's way in the step tests).</summary>
[DmPath("/obj/test_mob_wall")]
public class obj_test_mob_wall : obj
{
    public obj_test_mob_wall()
    {
        density = TRUE;
    }
}

/// <summary>A mob that counts the Moved calls it gets (the hook of mob/Moved, mob_movement.dm:588).</summary>
[DmPath("/mob/test_mob_mover")]
public class mob_test_mob_mover : TgMob
{
    [NotHashed] public readonly List<string> moves = new();

    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        moves.Add(Dm.str(movement_dir));
        return await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
    }
}

/// <summary>
/// More extra_checks targets than the translator's <see cref="datum_test_mob_check"/>: a check that sleeps before it
/// answers (so InvokeAsync returns its `.` at the first sleep, null) and one that runtimes.
/// </summary>
[DmPath("/datum/test_mob_check/ext")]
public class datum_test_mob_check_ext : datum_test_mob_check
{
    public virtual async DmTask<object> sleepy_check()
    {
        await sleep(1);
        return TRUE;
    }

    public virtual object broken_check()
    {
        throw Dm.CRASH("broken check", "tests/Mob", 1);
    }
}
