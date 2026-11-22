using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.Mob;

// Fixtures of package C's smoke tests (wave 4: mob, obj and timed actions). Fixture paths are /<kind>/test_mob_<name>
// (translation-rules.md 15), which no tg path starts with.

/// <summary>
/// A world as tg's has it when atoms are created after the Master initialized SSatoms: GLOB with every translated
/// InitGlobal proc, the logger, SSchat and the configuration (TgTestGenesis), SSgarbage, SSdcs, SSobj, SStimed_actions,
/// SSmapping, SSicon_smooth, SSspatial_grid (an inert stub until M2) and an initialized SSatoms (so /atom/New initializes at once), on a 3x3x1 map of the default
/// world turf and area. Written before packages A and D were merged, when /mob/Initialize and /mob/Destroy still reached
/// their skeletons (mob lists, movespeed, actionspeed, hearing sensitivity): the world runs with
/// <c>DmWorldState.NotTranslatedThrows</c> off unless a test asks otherwise (those packages are merged now; the flag stays
/// as it was written), and the tests assert only on what package C owns.
/// </summary>
internal sealed class MobTestWorld
{
    public readonly World World;
    public readonly List<Exception> Runtimes = new();

    public MobTestWorld(ulong seed = 7, bool notTranslatedThrows = false)
    {
        World = new World(seed) { RuntimeErrorSink = Runtimes.Add };
        MapGrid.Attach(World);
        Tools.GeneticsStrings.Mount(World); // gaps P3: setupGenetics' speech mutations read them
        using (World.Enter())
        {
            DmWorldState.Of(World).NotTranslatedThrows = notTranslatedThrows;
            GLOB__var = new datum_controller_global_vars();
            Dm.@new<datum_controller_subsystem_materials>(); // M5 slice 2: GLOB's make_datum_reference_lists reads SSmaterials' recipes (global_lists.dm:93)
            Dm.@new<datum_controller_subsystem_accessories>(); // M10x slice D2: tg makes it at PreInit, before GLOB (sprite_accessories.dm:47); bodies read its lists
            Dm.@new<datum_controller_subsystem_garbage>(); // M5 slice 8: before GLOB, as the Master makes it (master.cs:186-198): init_crafting_recipes qdels the recipes out of scope
            Waiting.NoSleep(GLOB.Initialize());
            Dm.@new<datum_controller_subsystem_processing_dcs>();
            Dm.@new<datum_controller_subsystem_processing_obj>();
            Dm.@new<datum_controller_subsystem_processing_timed_actions>();
            Dm.@new<datum_controller_subsystem_mapping>();
            Dm.@new<datum_controller_subsystem_icon_smooth>();
            Dm.@new<datum_controller_subsystem_atoms>();
            Dm.@new<datum_controller_subsystem_spatial_grid>();
            Dm.@new<datum_controller_subsystem_points_of_interest>(); // M10x slice C: a living mob is a point of interest (living.dm:19), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_radio>(); // M9 slice D: a headset registers its frequencies (radio.dm:168)
            Dm.@new<datum_controller_subsystem_cameras>(); // M10 slice D: a security camera registers itself (camera.dm:124), tg's MC always creates it
            Waiting.NoSleep(Dm.@new<datum_controller_subsystem_verbs>().Initialize()); // silicons SI4: a human and an AI are given their verbs (human.dm:2, ai.dm:79), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_pai>(); // silicons SI6: a pAI card joins SSpai's card list (card.dm:32), tg's MC always creates it
            Waiting.NoSleep(Dm.@new<datum_controller_subsystem_skills>().Initialize()); // gaps SL1: a mind's skills read the skill singletons (GetSkillRef), tg's MC always creates it
            Waiting.NoSleep(Dm.@new<datum_controller_subsystem_processing_instruments>().Initialize()); // silicons SI6c: an instrument's song takes its instrument from SSinstruments (_song.dm:182), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_persistent_paintings>(); // silicons SI4d: a painting frame registers itself (paintings.dm:585), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_processing_transport>(); // D325: an elevator panel, button or indicator and a lift platform look the lifts up in it, tg's MC always creates it
            TgTestGenesis.Install();
            Dm.world.maxx = 3;
            Dm.world.maxy = 3;
            Dm.world.maxz = 1;
            Waiting.NoSleep(SSatoms.Initialize());
        }
    }

    /// <summary>The runtimes reported so far, other than the not-translated sites of other packages.</summary>
    public List<Exception> OwnRuntimes => Runtimes.Where(e => e is not DmNotTranslatedException).ToList();
}
