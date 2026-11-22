using System.Reflection;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Tools;

/// <summary>
/// Hand-picked constants the M0 translation needs, read by reflection from the compiled <see cref="Defines"/>
/// class. Every expected value was worked out by reading the DM line cited in the comment (<c>(1&lt;&lt;5)</c> is 32,
/// <c>(1 SECONDS)</c> is 10, a text literal is its text), not by running the converter. An int expectation also
/// checks that the constant is a <c>const int</c>, a float one a <c>const float</c>.
/// </summary>
public class DefinesValueTests
{
    public static IEnumerable<object[]> Values() => Table.Select(e => new[] { e.Name, e.Value });

    private static readonly (string Name, object Value)[] Table =
    {
        // _flags.dm:1-17
        ("ALL", 16777215), ("NONE", 0), ("ALL_CARDINALS", 15),
        ("DF_USE_TAG", 1), ("DF_VAR_EDITED", 2), ("DF_ISPROCESSING", 4), ("DF_STATIC_OBJECT", 8), ("DF_EVLOGGING", 16),
        // _flags.dm:23-62 (flags_1)
        ("ATMOS_IS_PROCESSING_1", 1), ("ON_BORDER_1", 2), ("NO_SCREENTIPS_1", 4), ("PREVENT_CLICK_UNDER_1", 8),
        ("HOLOGRAM_1", 16), ("INITIALIZED_1", 32), ("ADMIN_SPAWNED_1", 64), ("PREVENT_CONTENTS_EXPLOSION_1", 128),
        ("ALLOW_DARK_PAINTS_1", 256), ("UNPAINTABLE_1", 512), ("SUPERMATTER_IGNORES_1", 1024), ("CAN_BE_DIRTY_1", 2048),
        ("HTML_USE_INITAL_ICON_1", 4096), ("IS_PLAYER_COLORABLE_1", 8192), ("HAS_CONTEXTUAL_SCREENTIPS_1", 16384),
        ("HAS_DISASSOCIATED_STORAGE_1", 32768), ("DECAL_INIT_UPDATE_EXPERIENCED_1", 65536),
        ("IGNORE_TURF_PIXEL_OFFSET_1", 131072), ("NO_NEW_GAGS_PREVIEW_1", 262144),
        // _flags.dm:66-78 (update_appearance)
        ("UPDATE_NAME", 1), ("UPDATE_DESC", 2), ("UPDATE_ICON_STATE", 4), ("UPDATE_OVERLAYS", 8), ("UPDATE_GREYSCALE", 16),
        ("UPDATE_SMOOTHING", 32), ("UPDATE_ICON", 12),
        ("RICOCHET_SHINY", 1), ("RICOCHET_HARD", 2),
        // _flags.dm:86-102 (turf flags)
        ("NOJAUNT", 1), ("UNUSED_RESERVATION_TURF", 2), ("RESERVATION_TURF", 4), ("NO_LAVA_GEN", 8), ("NO_RUINS", 16),
        ("NO_RUST", 32), ("IS_SOLID", 64), ("NO_CLEARING", 128), ("TURF_BLOCKS_POPULATE_TERRAIN_FLORAFEATURES", 256),
        // _flags.dm:106-136 (area flags)
        ("VALID_TERRITORY", 1), ("BLOBS_ALLOWED", 2), ("NOTELEPORT", 4), ("HIDDEN_AREA", 8), ("BLOCK_SUICIDE", 32),
        ("XENOBIOLOGY_COMPATIBLE", 64), ("CULT_PERMITTED", 128), ("NO_DEATH_MESSAGE", 256), ("EVENT_PROTECTED", 512),
        ("QUIET_LOGS", 1024), ("BINARY_JAMMING", 2048), ("NO_BOH", 4096), ("UNLIMITED_FISHING", 8192), ("NO_GRAVITY", 16384),
        ("LOCAL_TELEPORT", 32768),
        ("UNIQUE_AREA", 1), ("CAVES_ALLOWED", 2), ("FLORA_ALLOWED", 4), ("MOB_SPAWN_ALLOWED", 8), ("MEGAFAUNA_SPAWN_ALLOWED", 16),
        ("PERSISTENT_ENGRAVINGS", 32), ("VIRTUAL_AREA", 64), ("VIRTUAL_SAFE_AREA", 128),
        // _flags.dm:161-189 (pass_flags)
        ("PASSTABLE", 1), ("PASSGLASS", 2), ("PASSGRILLE", 4), ("PASSBLOB", 8), ("PASSMOB", 16), ("PASSCLOSEDTURF", 32),
        ("LETPASSTHROW", 64), ("PASSMACHINE", 128), ("PASSSTRUCTURE", 256), ("PASSFLAPS", 512), ("PASSDOORS", 1024),
        ("PASSVEHICLE", 2048), ("PASSITEM", 4096), ("LETPASSCLICKS", 8192), ("PASSWINDOW", 16384),
        // _flags.dm:192-203 (movement types)
        ("GROUND", 1), ("FLYING", 2), ("VENTCRAWLING", 4), ("FLOATING", 8), ("PHASING", 16), ("UPSIDE_DOWN", 32),
        ("MOVETYPES_NOT_TOUCHING_GROUND", 42), ("SOURCE_MOVETYPES", "movetypes"),
        // _flags.dm:206-224 (resistance_flags)
        ("LAVA_PROOF", 1), ("FIRE_PROOF", 2), ("FLAMMABLE", 4), ("ON_FIRE", 8), ("UNACIDABLE", 16), ("ACID_PROOF", 32),
        ("INDESTRUCTIBLE", 64), ("FREEZE_PROOF", 128), ("SHUTTLE_CRUSH_PROOF", 256), ("BOMB_PROOF", 512),
        // _flags.dm:227-239 (tesla_zap)
        ("ZAP_MACHINE_EXPLOSIVE", 1), ("ZAP_ALLOW_DUPLICATES", 2), ("ZAP_OBJ_DAMAGE", 4), ("ZAP_MOB_DAMAGE", 8),
        ("ZAP_MOB_STUN", 16), ("ZAP_GENERATES_POWER", 32), ("ZAP_LOW_POWER_GEN", 64), ("ZAP_DEFAULT_FLAGS", 28),
        ("ZAP_FUSION_FLAGS", 28), ("ZAP_SUPERMATTER_FLAGS", 32), ("ZAP_GIZMO_FLAGS", 12),
        // _flags.dm:242-252
        ("EMP_PROTECT_SELF", 1), ("EMP_PROTECT_CONTENTS", 2), ("EMP_PROTECT_WIRES", 4), ("EMP_NO_EXAMINE", 8), ("EMP_PROTECT_ALL", 7),
        // _flags.dm:255-275 (mobility)
        ("MOBILITY_MOVE", 1), ("MOBILITY_STAND", 2), ("MOBILITY_PICKUP", 4), ("MOBILITY_USE", 8), ("MOBILITY_UI", 16),
        ("MOBILITY_STORAGE", 32), ("MOBILITY_PULL", 64), ("MOBILITY_REST", 128), ("MOBILITY_LIEDOWN", 256),
        ("MOBILITY_FLAGS_DEFAULT", 127), ("MOBILITY_FLAGS_CARBON_DEFAULT", 511), ("MOBILITY_FLAGS_REST_CAPABLE_DEFAULT", 511),
        // _flags.dm:278-327
        ("AA_TARGET_SEE_APPEARANCE", 1), ("AA_MATCH_TARGET_OVERLAYS", 2), ("KEEP_TOGETHER_ORIGINAL", "keep_together_original"),
        ("RELIGION_TOOL_INVOKE", 1), ("RELIGION_TOOL_SACRIFICE", 2), ("RELIGION_TOOL_SECTSELECT", 4),
        ("SKILLCHIP_ALLOWS_MULTIPLE", 1), ("SKILLCHIP_RESTRICTED_CATEGORIES", 2), ("MAX_BITFIELD_SIZE", 24), ("MAX_BITFLAG_DIGITS", 8),
        ("IGNORE_USER_LOC_CHANGE", 1), ("IGNORE_TARGET_LOC_CHANGE", 2), ("IGNORE_HELD_ITEM", 4), ("IGNORE_INCAPACITATED", 8),
        ("IGNORE_SLOWDOWNS", 16), ("DO_AFTER_CHECK_NEXT_MOVE", 32), ("FLORA_HERBAL", 1), ("FLORA_WOODEN", 2), ("FLORA_STONE", 4),

        // qdel.dm:6-51
        ("QDEL_HINT_QUEUE", 0), ("QDEL_HINT_LETMELIVE", 1), ("QDEL_HINT_IWILLGC", 2), ("QDEL_HINT_HARDDEL", 3),
        ("QDEL_HINT_HARDDEL_NOW", 4), ("GC_QUEUE_FILTER", 1), ("GC_QUEUE_CHECK", 2), ("GC_QUEUE_HARDDELETE", 3), ("GC_QUEUE_COUNT", 3),
        ("GC_QUEUE_ITEM_QUEUE_TIME", 1), ("GC_QUEUE_ITEM_REF", 2), ("GC_QUEUE_ITEM_GCD_DESTROYED", 3), ("GC_QUEUE_ITEM_INDEX_COUNT", 3),
        ("GC_FILTER_QUEUE", 10), ("GC_CHECK_QUEUE", 3000), ("GC_DEL_QUEUE", 100), ("QDEL_ITEM_ADMINS_WARNED", 1),
        ("QDEL_ITEM_SUSPENDED_FOR_LAG", 2), ("GC_CURRENTLY_BEING_QDELETED", -2),

        // MC.dm:38-90
        ("MC_LOOP_RTN_NEWSTAGES", 1), ("MC_LOOP_RTN_GRACEFUL_EXIT", 2), ("SS_NO_INIT", 1), ("SS_NO_FIRE", 2), ("SS_BACKGROUND", 4),
        ("SS_TICKER", 8), ("SS_KEEP_TIMING", 16), ("SS_POST_FIRE_TIMING", 32), ("SS_OK_TO_FAIL_INIT", 64), ("SS_IDLE", 0),
        ("SS_QUEUED", 1), ("SS_RUNNING", 2), ("SS_PAUSED", 3), ("SS_SLEEPING", 4), ("SS_PAUSING", 5), ("INITSTAGE_FIRST", 1),
        ("INITSTAGE_EARLY", 2), ("INITSTAGE_MAIN", 3), ("INITSTAGE_LAST", 4), ("INITSTAGE_MAX", 4),

        // subsystems.dm:33-188, 284
        ("TIMER_UNIQUE", 1), ("TIMER_OVERRIDE", 2), ("TIMER_CLIENT_TIME", 4), ("TIMER_STOPPABLE", 8), ("TIMER_NO_HASH_WAIT", 16),
        ("TIMER_LOOP", 32), ("TIMER_DELETE_ME", 64), ("TIMER_ID_NULL", -1), ("PROCESS_KILL", 26), ("INITIALIZATION_INSSATOMS", 0),
        ("INITIALIZATION_INNEW_MAPLOAD", 2), ("INITIALIZATION_INNEW_REGULAR", 1), ("INITIALIZE_HINT_NORMAL", 0),
        ("INITIALIZE_HINT_LATELOAD", 1), ("INITIALIZE_HINT_QDEL", 2), ("SS_INIT_FAILURE", -2), ("SS_INIT_NONE", -1),
        ("SS_INIT_SUCCESS", 2), ("SS_INIT_NO_NEED", 3), ("SS_INIT_NO_MESSAGE", 4), ("FIRE_PRIORITY_GARBAGE", 15),
        ("FIRE_PRIORITY_DEFAULT", 50), ("FIRE_PRIORITY_MOBS", 100), ("FIRE_PRIORITY_TIMER", 700), ("FIRE_PRIORITY_INPUT", 1000),
        ("RUNLEVEL_LOBBY", 1), ("RUNLEVEL_SETUP", 2), ("RUNLEVEL_GAME", 4), ("RUNLEVEL_POSTGAME", 8), ("RUNLEVELS_DEFAULT", 14),
        ("SS_INIT_TIMER_KEY", "ss_init"),

        // dcs/declarations.dm:4-56
        ("COMPONENT_INCOMPATIBLE", 1), ("COMPONENT_NOTRANSFER", 2), ("ELEMENT_INCOMPATIBLE", 1), ("ELEMENT_DETACH_ON_HOST_DESTROY", 1),
        ("ELEMENT_BESPOKE", 2), ("ELEMENT_COMPLEX_DETACH", 4), ("ELEMENT_DONT_SORT_LIST_ARGS", 8), ("ELEMENT_NO_LIST_UNIT_TEST", 16),
        ("COMPONENT_DUPE_HIGHLANDER", 0), ("COMPONENT_DUPE_ALLOWED", 1), ("COMPONENT_DUPE_UNIQUE", 2), ("COMPONENT_DUPE_SOURCES", 3),
        ("COMPONENT_DUPE_UNIQUE_PASSARGS", 4), ("COMPONENT_DUPE_SELECTIVE", 5),

        // time.dm:2-117
        ("MIDNIGHT_ROLLOVER", 864000), ("STATION_YEAR_OFFSET", 540), ("JANUARY", 1), ("DECEMBER", 12), ("NEW_YEAR", "New Year"),
        ("MILLISECONDS", 0.01f), ("DECISECONDS", 1), ("SECONDS", 10), ("MINUTES", 600), ("HOURS", 36000), ("DAYS", 864000),
        ("YEARS", 315360000f), ("TIMEZONE_LINT", 14), ("TIMEZONE_CHADT", 13.75f), ("TIMEZONE_LHST", 10.5f), ("TIMEZONE_ACST", 9.5f),

        // movement.dm:2-145 (MAX_GLIDE_SIZE is ICON_SIZE_ALL, _helpers.dm:10, 32)
        ("MIN_GLIDE_SIZE", 1), ("MAX_GLIDE_SIZE", 32), ("MOVEMENT_DEFAULT_PRIORITY", 10), ("MOVEMENT_SPACE_PRIORITY", 100),
        ("MOVEMENT_ABOVE_SPACE_PRIORITY", 101), ("MOVEMENT_LOOP_START_INSTANT", 1), ("MOVEMENT_LOOP_START_FAST", 2),
        ("MOVEMENT_LOOP_IGNORE_PRIORITY", 4), ("MOVEMENT_LOOP_IGNORE_GLIDE", 8), ("MOVEMENT_LOOP_NO_DIR_UPDATE", 16),
        ("MOVEMENT_LOOP_OUTSIDE_CONTROL", 32), ("MOVELOOP_STATUS_PAUSED", 1), ("MOVELOOP_STATUS_RUNNING", 2),
        ("MOVELOOP_STATUS_QUEUED", 4), ("MOVEMENT_BUCKET_TIME", 1), ("MOVEMENT_BUCKET_LIST", 2), ("CURRENTLY_Z_FALLING", 1),
        ("CURRENTLY_Z_MOVING_GENERIC", 2), ("CURRENTLY_Z_FALLING_FROM_MOVE", 3), ("CURRENTLY_Z_ASCENDING", 4),
        ("ZMOVE_CHECK_PULLING", 1), ("ZMOVE_CHECK_PULLEDBY", 2), ("ZMOVE_FALL_CHECKS", 4), ("ZMOVE_CAN_FLY_CHECKS", 8),
        ("ZMOVE_INCAPACITATED_CHECKS", 16), ("ZMOVE_IGNORE_OBSTACLES", 32), ("ZMOVE_FEEDBACK", 64), ("ZMOVE_ALLOW_BUCKLED", 128),
        ("ZMOVE_VENTCRAWLING", 256), ("ZMOVE_INCLUDE_PULLED", 512), ("ZMOVE_ALLOW_ANCHORED", 1024), ("ZMOVE_CHECK_PULLS", 3),
        ("ZMOVE_FLIGHT_FLAGS", 667), ("ZMOVE_STAIRS_FLAGS", 130), ("ZMOVE_FALL_FLAGS", 132), ("FIRST_DIAG_STEP", 1),
        ("SECOND_DIAG_STEP", 2), ("TELEPORT_CHANNEL_BLUESPACE", "bluespace"), ("TELEPORT_CHANNEL_FREE", "free"),
        ("MOVELOOP_FAILURE", 0), ("MOVELOOP_SUCCESS", 1), ("MOVELOOP_NOT_READY", 2), ("NEWTONS", 1), ("DEFAULT_INERTIA_SPEED", 5),
        ("INERTIA_FORCE_CAP", 25), ("INERTIA_SPEED_COEF", 0.375f),

        // maths.dm:8-269
        ("NUM_E", 2.71828183f), ("PI", 3.1416f), ("INFINITY", 1e31f), ("SHORT_REAL_LIMIT", 16777216), ("ACCURACY", 10000),
        ("SI_COEFFICIENT", "coefficient"), ("SI_UNIT", "unit"),

        // do_afters.dm:1-12
        ("DOAFTER_SOURCE_SURGERY", "doafter_surgery"), ("DOAFTER_SOURCE_MECHADRILL", "doafter_mechadrill"),
        ("DOAFTER_SOURCE_SURVIVALPEN", "doafter_survivalpen"), ("DOAFTER_SOURCE_GETTING_UP", "doafter_gettingup"),
        ("DOAFTER_SOURCE_CLIMBING_LADDER", "doafter_climbingladder"), ("DOAFTER_SOURCE_SPIDER", "doafter_spider"),
        ("DOAFTER_SOURCE_HEAL_TOUCH", "doafter_heal_touch"), ("DOAFTER_SOURCE_PLANTING_DEVICE", "doafter_planting_device"),
        ("DOAFTER_SOURCE_CHARGE_CRANKRECHARGE", "doafter_charge_crank_recharge"), ("DOAFTER_SOURCE_REMOVING_HOOK", "doafter_removing_hook"),
        ("DOAFTER_SOURCE_CHARGING_ESWORD", "doafter_charging_esword"), ("DOAFTER_SOURCE_RIP_HEART", "doafter_rip_heart"),

        // dcs/signals/signals_atom/signals_atom_movable.dm:6-46
        ("COMSIG_MOVABLE_ATTEMPTED_MOVE", "movable_attempted_move"), ("COMSIG_MOVABLE_PRE_MOVE", "movable_pre_move"),
        ("COMPONENT_MOVABLE_BLOCK_PRE_MOVE", 1), ("COMSIG_MOVABLE_MOVED", "movable_moved"),
        ("COMSIG_MOVABLE_TURF_INITIALIZING", "movable_turf_initializing"), ("COMSIG_MOVABLE_CROSS", "movable_cross"),
        ("COMPONENT_BLOCK_CROSS", 1), ("COMSIG_MOVABLE_CROSS_OVER", "movable_cross_am"), ("COMSIG_MOVABLE_BUMP", "movable_bump"),
        ("COMPONENT_INTERCEPT_BUMPED", 1), ("COMSIG_MOVABLE_IMPACT_ZONE", "item_impact_zone"),
        ("COMSIG_MOVABLE_PREBUCKLE", "prebuckle"), ("COMSIG_MOB_BUCKLED", "mob_buckle"),
        // dcs/signals/signals_datum.dm:7-40
        ("COMSIG_COMPONENT_ADDED", "component_added"), ("COMSIG_COMPONENT_REMOVING", "component_removing"),
        ("COMSIG_PREQDELETED", "parent_preqdeleted"), ("COMSIG_QDELETING", "parent_qdeleting"),
        ("COMSIG_ADMIN_DELETING", "parent_admin_deleting"), ("COMSIG_TOPIC", "handle_topic"), ("COMSIG_VV_TOPIC", "vv_topic"),
        ("COMPONENT_VV_HANDLED", 1), ("COMSIG_UI_ACT", "ui_act"), ("COMSIG_ELEMENT_ATTACH", "element_attach"),
        ("COMSIG_ELEMENT_DETACH", "element_detach"),

        // traits/declarations.dm:66-128
        ("TRAIT_KNOCKEDOUT", "knockedout"), ("TRAIT_IMMOBILIZED", "immobilized"), ("TRAIT_FLOORED", "floored"),
        ("TRAIT_FORCED_STANDING", "forcedstanding"), ("TRAIT_HANDS_BLOCKED", "handsblocked"), ("TRAIT_UI_BLOCKED", "uiblocked"),
        ("TRAIT_PULL_BLOCKED", "pullblocked"), ("TRAIT_RESTRAINED", "restrained"), ("TRAIT_UNDENSE", "undense"),
        ("TRAIT_GODMODE", "godmode"), ("TRAIT_INCAPACITATED", "incapacitated"), ("TRAIT_MUTE", "mute"), ("TRAIT_DEAF", "deaf"),
        ("TRAIT_FORCE_WHISPER", "softspoken"), ("TRAIT_FENCE_CLIMBER", "can_climb_fences"),

        // other files the base translation reads
        ("ICON_SIZE_ALL", 32), ("ICON_SIZE_X", 32), ("ICON_SIZE_Y", 32), // _helpers.dm:10-14
        ("GLOBAL_PROC", "some_magic_bullshit"), // callbacks.dm:1
        ("HEAL_ADMIN", 1), ("HEAL_RESTRAINTS", 262144), // mobs.dm:989, 1025
        ("LIGHTING_ROUND_VALUE", 0.015625f), // lighting.dm:35 (1 / 64)
        ("ONE_ATMOSPHERE", 101.325f), ("T0C", 273.15f), ("T20C", 293.15f), ("TCMB", 2.7f), ("CELL_VOLUME", 2500), // atmos_core.dm
        ("WATT", 1), ("JOULE", 1), ("REAGENTS_METABOLISM", 0.2f), // power.dm:14-16, mobs.dm:512
    };

    [Fact]
    public void TheTableIsLargeAndHasNoDuplicates()
    {
        Assert.True(Table.Length >= 200, $"only {Table.Length} hand-picked constants");
        Assert.Equal(Table.Length, Table.Select(e => e.Name).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Values))]
    public void Constant(string name, object expected)
    {
        FieldInfo field = typeof(Defines).GetField(name, BindingFlags.Public | BindingFlags.Static);
        Assert.True(field != null, $"Defines.{name} does not exist");
        Assert.True(field.IsLiteral, $"Defines.{name} is not a const");
        Assert.Equal(expected.GetType(), field.FieldType);
        Assert.Equal(expected, field.GetRawConstantValue());
    }

    [Fact]
    public void FloatExpressions_FoldInBinary32()
    {
        // atmos_core.dm:140  MOLES_CELLSTANDARD (ONE_ATMOSPHERE*CELL_VOLUME/(T20C*R_IDEAL_GAS_EQUATION)), each step binary32:
        // ONE_ATMOSPHERE 101.325 (:25), CELL_VOLUME 2500 (:152), T20C 293.15 (:31), R_IDEAL_GAS_EQUATION 8.31 (:23).
        float oneAtmosphere = 101.325f;
        float cellVolume = 2500f;
        float t20c = 293.15f;
        float r = 8.31f;
        float numerator = oneAtmosphere * cellVolume;
        float denominator = t20c * r;
        Assert.Equal(numerator / denominator, (float)TgClone.Constant("MOLES_CELLSTANDARD"));
        // reactions.dm:126  N2O_DECOMPOSITION_SCALE_DIVISOR ((-1/4) * ((MAX - MIN)**2)), MAX 100000 (:124), MIN 0 (:122)
        Assert.Equal(-0.25f * (100000f * 100000f), (float)TgClone.Constant("N2O_DECOMPOSITION_SCALE_DIVISOR"));
    }

    [Fact]
    public void NotEmittedNames_HaveNoConstant()
    {
        // qdel.dm:23 QDEL_HINT_FINDREFERENCE is under #ifdef REFERENCE_TRACKING (off in production).
        Assert.False(TgClone.HasConstant("QDEL_HINT_FINDREFERENCE"));
        // _tick.dm:14 TICK_USAGE world.tick_usage; subsystems.dm:277 SSMOBS_DT (SSmobs.wait/10): run-time values.
        Assert.False(TgClone.HasConstant("TICK_USAGE"));
        Assert.False(TgClone.HasConstant("SSMOBS_DT"));
        // BYOND's own constants are the shim's (translation-rules.md 1.6).
        Assert.False(TgClone.HasConstant("TRUE"));
        Assert.False(TgClone.HasConstant("NORTH"));
    }

    [Fact]
    public void SpanHelpers_AreGlobalProcs()
    {
        // span.dm:103 (span_notice), :155 (span_warning): "<span class='notice'>" + str + "</span>"
        Assert.Equal("<span class='notice'>hi</span>", GlobalProcs.span_notice("hi"));
        Assert.Equal("<span class='warning'></span>", GlobalProcs.span_warning(null));
    }
}
