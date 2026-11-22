// Translated from tgstation (snapshot 1d99bd7f): code/game/area/areas/misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the var overrides of /area/space (tg's /world/area) and the toy world's room /area/misc/testroom
// declared (W4.0 contracts, rules 10.2); package B (wave 4) owns the file. The [DmPath] of /area/space is on
// game/area/areas.cs (rules 2.3).

// Areas that don't fit any of the other files, or only serve one purpose.

// tg: code/game/area/areas/misc.dm:3
public partial class area_space : area
{
    // tg: code/game/area/areas/misc.dm:4-20
    public area_space()
    {
        icon_state = "space";
        requires_power = TRUE;
        always_unpowered = TRUE;
        static_lighting = FALSE;
        base_lighting_alpha = 255;
        base_lighting_color = COLOR_STARLIGHT;
        power_light = FALSE;
        power_equip = FALSE;
        power_environ = FALSE;
        area_flags = NO_GRAVITY;
        outdoors = TRUE;
        ambience_index = AMBIENCE_SPACE;
        flags_1 = CAN_BE_DIRTY_1;
        sound_environment = SOUND_AREA_SPACE;
        ambient_buzz = null; //Space is deafeningly quiet
        allow_shuttle_docking = TRUE;
        skip_minimap_rendering = TRUE;
    }
}

// tg: code/game/area/areas/misc.dm:28  (an implicit type: declared only through its subtypes, first here; rules 2.3)
[DmPath("/area/misc")]
public partial class area_misc : area
{
}

// tg: code/game/area/areas/misc.dm:37
[DmPath("/area/misc/testroom")]
public partial class area_misc_testroom : area_misc
{
    // tg: code/game/area/areas/misc.dm:38-44
    public area_misc_testroom()
    {
        requires_power = FALSE;
        default_gravity = STANDARD_GRAVITY;
        // Mobs should be able to see inside the testroom
        static_lighting = FALSE;
        base_lighting_alpha = 255;
        name = "Test Room";
        icon_state = "test_room";
    }
}
