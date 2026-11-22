// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/verbs/secrets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the panel with its power and light buttons (power, unpower, quickpower, blackout, whiteout). The verb's body is secrets,
// which the admin console's secrets runs (D275). The panel's other buttons wait for what they drive: DV351, the window offers only
// these, and ui_act has no other case. Deferred: the blackbox counts.

public static partial class GlobalProcs
{
    // ADMIN_VERB(secrets, R_NONE, "Secrets", ...): the verb's body, which the admin console's secrets runs (D275)
    // tg: code/modules/admin/verbs/secrets.dm:3
    public static async DmTask secrets(client user = null)
    {
        datum_secrets_menu tgui = Dm.@new<datum_secrets_menu>(user);
        await tgui.ui_interact(user.mob);
        // BLACKBOX_LOG_ADMIN_VERB("Secrets Panel"): the blackbox (not translated)
    }
}

// tg: code/modules/admin/verbs/secrets.dm:8
public partial class datum_secrets_menu : datum
{
    // tg: code/modules/admin/verbs/secrets.dm:13
    public override async DmTask New(params object[] __extra)
    {
        object user = Dm.arg(__extra, 0); //user can either be a client or a mob due to byondcode(tm)
        holder = user as client ?? (user as mob)?.client; //if its a client, assign it to holder; if its a mob, assign the mob's client to holder

        is_debugger = Dm.num(check_rights(R_DEBUG));
        is_funmin = Dm.num(check_rights(R_FUN));
        await DmTask.CompletedTask;
    }

    // tg: code/modules/admin/verbs/secrets.dm:24
    public override object ui_state(mob user = null)
    {
        return ADMIN_STATE(R_NONE);
    }

    // tg: code/modules/admin/verbs/secrets.dm:27
    public override void ui_close(mob user = null)
    {
        qdel(this);
    }

    // tg: code/modules/admin/verbs/secrets.dm:30
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Secrets");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/admin/verbs/secrets.dm:36
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["is_debugger"] = is_debugger;
        data["is_funmin"] = is_funmin;
        return data;
    }

    // tg: code/modules/admin/verbs/secrets.dm:44
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(check_rights(R_ADMIN))) // tg's (action != "admin_log" || action != "show_admins") is always true
            return dot;
        switch (Dm.str(action))
        {
            case "power":
                if (!Dm.truthy(is_funmin))
                    return dot;
                log_admin(Dm.text("[] made all areas powered", key_name(holder)));
                message_admins(span_adminnotice(Dm.text("[] made all areas powered", key_name_admin(holder))));
                await power_restore();
                break;
            case "unpower":
                if (!Dm.truthy(is_funmin))
                    return dot;
                log_admin(Dm.text("[] made all areas unpowered", key_name(holder)));
                message_admins(span_adminnotice(Dm.text("[] made all areas unpowered", key_name_admin(holder))));
                await power_failure();
                break;
            case "quickpower":
                if (!Dm.truthy(is_funmin))
                    return dot;
                log_admin(Dm.text("[] made all SMESs powered", key_name(holder)));
                message_admins(span_adminnotice(Dm.text("[] made all SMESs powered", key_name_admin(holder))));
                await power_restore_quick();
                break;
            case "blackout":
                if (!Dm.truthy(is_funmin))
                    return dot;
                message_admins(Dm.text("[] broke all lights", key_name_admin(holder)));
                foreach (obj_machinery_light L in Dm.each<obj_machinery_light>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_light))))
                {
                    await L.break_light_tube();
                    CHECK_TICK();
                }
                break;
            case "whiteout":
                if (!Dm.truthy(is_funmin))
                    return dot;
                message_admins(Dm.text("[] fixed all lights", key_name_admin(holder)));
                foreach (obj_machinery_light L in Dm.each<obj_machinery_light>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_light))))
                {
                    await L.fix();
                    CHECK_TICK();
                }
                break;
        }
        return dot;
    }
}
