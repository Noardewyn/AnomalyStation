// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/pai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI6, D326): the pAI's HUD. As the human's and the cyborg's HUDs, the language and navigate buttons (their
// screen types are not translated) are a recorded deferred line. The var block is generated (D111).

// tg: code/_onclick/hud/pai.dm:2
public partial class datum_hud_pai : datum_hud
{
    // tg: code/_onclick/hud/pai.dm:5
    public override async DmTask initialize_screen_objects()
    {
        await base.initialize_screen_objects();
        await add_screen_object(typeof(atom_movable_screen_healthdoll_living), HUD_MOB_HEALTHDOLL, HUD_GROUP_INFO);
        await add_screen_object(typeof(atom_movable_screen_language_menu), HUD_MOB_LANGUAGE_MENU, HUD_GROUP_STATIC, ui_style, ui_pai_language_menu); // gaps L2
        // :9 the navigate button: its screen type is not translated, as the human's HUD (human.dm:9)
        Dm.Deferred("code/_onclick/hud/pai.dm:9", "initialize_screen_objects: the navigate button (as the human's HUD, M10+)");
        await add_screen_object(typeof(atom_movable_screen_memories), HUD_MOB_MEMORIES, HUD_GROUP_STATIC, ui_style, ui_pai_memories_menu);

        await add_screen_object(typeof(atom_movable_screen_pai_software), HUD_PAI_SOFTWARE);
        await add_screen_object(typeof(atom_movable_screen_pai_shell), HUD_PAI_SHELL);
        await add_screen_object(typeof(atom_movable_screen_pai_chassis), HUD_PAI_CHASSIS);
        await add_screen_object(typeof(atom_movable_screen_pai_rest), HUD_MOB_REST);
        await add_screen_object(typeof(atom_movable_screen_pai_light), HUD_CYBORG_LAMP);
        await add_screen_object(typeof(atom_movable_screen_pai_newscaster), HUD_PAI_NEWSCASTER);
        await add_screen_object(typeof(atom_movable_screen_pai_host_monitor), HUD_PAI_HOST_MONITOR);
        await add_screen_object(typeof(atom_movable_screen_pai_crew_manifest), HUD_AI_CREW_MANIFEST);
        await add_screen_object(typeof(atom_movable_screen_pai_state_laws), HUD_AI_STATE_LAWS);
        await add_screen_object(typeof(atom_movable_screen_pai_internal_gps), HUD_PAI_GPS);
        await add_screen_object(typeof(atom_movable_screen_pai_image_take), HUD_AI_TAKE_IMAGE);
        await add_screen_object(typeof(atom_movable_screen_pai_image_view), HUD_AI_IMAGE_VIEW);
        await add_screen_object(typeof(atom_movable_screen_pai_radio), HUD_CYBORG_RADIO);
        await add_screen_object(typeof(atom_movable_screen_pai_modpc), HUD_SILICON_TABLET);

        update_software_buttons();
    }

    // tg: code/_onclick/hud/pai.dm:29
    public virtual void update_software_buttons()
    {
        var owner = (mob_living_silicon_pai)mymob;
        foreach (object button_key in Dm.each(screen_objects))
        {
            if (screen_objects[button_key] is atom_movable_screen_pai button && Dm.truthy(button.required_software))
                button.color = Dm.truthy(owner.installed_software.Find(button.required_software)) ? null : COLOR_GRAY;
        }
    }
}
