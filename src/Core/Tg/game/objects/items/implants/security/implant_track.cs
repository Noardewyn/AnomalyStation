// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/security/implant_track.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M8 slice F, D186). The tracking implants are inert: no prisoner management console or locator is ported, so nothing
// reads where they are; they are implanted, shown on the security HUD as tg's flag says and expire as tg has it.

// tg: code/game/objects/items/implants/security/implant_track.dm:1
public partial class obj_item_implant_tracking : obj_item_implant
{
    // tg: code/game/objects/items/implants/security/implant_track.dm:22
    public override object is_shown_on_console(obj_machinery_computer_prisoner_management console = null)
    {
        if (imp_in.stat == DEAD && Dm.num(imp_in.timeofdeath) + lifespan_postmortem < world.time)
            return FALSE;
        if (!Dm.truthy(is_valid_z_level(get_turf(console), get_turf(imp_in))))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items/implants/security/implant_track.dm:29
    public override object get_management_console_data()
    {
        DmList info_shown = (DmList)base.get_management_console_data();
        info_shown["Location"] = (get_area_name(imp_in, format_text: TRUE) is var or__1 && Dm.truthy(or__1) ? or__1 : "Unknown");
        return info_shown;
    }

    // tg: code/game/objects/items/implants/security/implant_track.dm:34
    public override object get_management_console_buttons()
    {
        DmList buttons = (DmList)base.get_management_console_buttons();
        Dm.append(buttons, Dm.list(Dm.list( // UNTYPED_LIST_ADD: the button is one element
            Dm.kv("name", "Warn"),
            Dm.kv("color", "average"),
            Dm.kv("tooltip", "Sends a message directly to the target's brain."),
            Dm.kv("action_key", "warn"))));
        return buttons;
    }

    // tg: code/game/objects/items/implants/security/implant_track.dm:44
    public override async DmTask<object> handle_management_console_action(mob user = null, DmList @params = null, obj_machinery_computer_prisoner_management console = null)
    {
        object dot = await base.handle_management_console_action(user, @params, console);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(@params["implant_action"], "warn"))
        {
            object warning = await tgui_input_text(user, I18n.T("What warning do you want to send to []?", imp_in.name), I18n.T("Messaging"), max_length: MAX_MESSAGE_LEN);
            if (!Dm.truthy(warning) || QDELETED(this) || QDELETED(user) || QDELETED(console) || imp_in == null)
                return TRUE;
            if (!Dm.truthy(console.is_operational) || !Dm.truthy(await user.can_perform_action(console, NEED_DEXTERITY | ALLOW_SILICON_REACH)))
                return TRUE;

            to_chat(imp_in, span_hear(I18n.T("You hear a voice in your head saying: '[]'", warning)));
            log_directed_talk(user, imp_in, warning, LOG_SAY, "implant message");
            return TRUE;
        }
        return null;
    }
}

// tg: code/game/objects/items/implants/security/implant_track.dm:61
public partial class obj_item_implant_tracking_c38 : obj_item_implant_tracking
{
    // tg: code/game/objects/items/implants/security/implant_track.dm:81
    [DmParams("target", "user", "silent", "force")]
    public override async DmTask<object> implant(mob_living target = null, mob user = null, float? silent = null, float? force = null, params object[] __extra)
    {
        object dot = await base.implant(target, user, silent, force, __extra);
        // QDEL_IN_STOPPABLE(src, lifespan) (__DEFINES/qdel.dm), expanded inline
        timerid = _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this), lifespan, TIMER_STOPPABLE, file: "code/game/objects/items/implants/security/implant_track.dm", line: 82);
        return dot;
    }

    // tg: code/game/objects/items/implants/security/implant_track.dm:85
    public override async DmTask<object> removed(mob_living source = null, float? silent = null, float? special = null)
    {
        object dot = await base.removed(source, silent, special);
        deltimer(timerid);
        timerid = null;
        return dot;
    }

    // tg: code/game/objects/items/implants/security/implant_track.dm:90
    public override object Destroy(float? force = null)
    {
        return base.Destroy(force);
    }
}
