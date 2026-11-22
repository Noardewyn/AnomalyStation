// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326): the AI's HUD. As the human's and the cyborg's HUDs, the language button (its screen type is not
// translated) is a recorded deferred line. The var block is generated (D111).

// tg: code/_onclick/hud/ai.dm:1
public partial class datum_hud_ai : datum_hud
{
    // tg: code/_onclick/hud/ai.dm:4
    public override async DmTask initialize_screen_objects()
    {
        await base.initialize_screen_objects();
        await add_screen_object(typeof(atom_movable_screen_language_menu), HUD_MOB_LANGUAGE_MENU, HUD_GROUP_STATIC, ui_style, ui_ai_language_menu); // gaps L2
        await add_screen_object(typeof(atom_movable_screen_memories), HUD_MOB_MEMORIES, HUD_GROUP_STATIC, ui_style, ui_ai_memories_menu);
        await add_screen_object(typeof(atom_movable_screen_ai_floor_indicator), HUD_AI_FLOOR_INDICATOR);
        await add_screen_object(typeof(atom_movable_screen_ai_go_up), HUD_AI_GO_UP);
        await add_screen_object(typeof(atom_movable_screen_ai_go_up_down), HUD_AI_GO_DOWN);
        await add_screen_object(typeof(atom_movable_screen_ai_aicore), HUD_AI_AICORE);
        await add_screen_object(typeof(atom_movable_screen_ai_camera_list), HUD_AI_CAMERA_LIST);
        await add_screen_object(typeof(atom_movable_screen_ai_camera_track), HUD_AI_CAMERA_TRACK);
        await add_screen_object(typeof(atom_movable_screen_ai_camera_light), HUD_AI_CAMERA_LIGHT);
        await add_screen_object(typeof(atom_movable_screen_ai_crew_monitor), HUD_AI_CREW_MONITOR);
        await add_screen_object(typeof(atom_movable_screen_ai_crew_manifest), HUD_AI_CREW_MANIFEST);
        await add_screen_object(typeof(atom_movable_screen_ai_alerts), HUD_SILICON_ALERTS);
        await add_screen_object(typeof(atom_movable_screen_ai_announcement), HUD_AI_ANNOUNCEMENT);
        await add_screen_object(typeof(atom_movable_screen_ai_call_shuttle), HUD_AI_CALL_SHUTTLE);
        await add_screen_object(typeof(atom_movable_screen_ai_state_laws), HUD_AI_STATE_LAWS);
        await add_screen_object(typeof(atom_movable_screen_ai_image_take), HUD_AI_TAKE_IMAGE);
        await add_screen_object(typeof(atom_movable_screen_ai_image_view), HUD_AI_IMAGE_VIEW);
        await add_screen_object(typeof(atom_movable_screen_ai_sensors), HUD_AI_SENSORS);
        await add_screen_object(typeof(atom_movable_screen_ai_multicam), HUD_AI_MULTICAM);
        await add_screen_object(typeof(atom_movable_screen_ai_add_multicam), HUD_AI_ADD_MULTICAM);
        await add_screen_object(typeof(atom_movable_screen_ai_modpc), HUD_SILICON_TABLET);
    }
}
