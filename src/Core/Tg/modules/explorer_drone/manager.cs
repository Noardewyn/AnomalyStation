// Translated from tgstation (snapshot 1d99bd7f): code/modules/explorer_drone/manager.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the admins' adventure manager (the AdventureBrowser window): the loaded adventures, and playing one through as
// a test. The verb's body runs from the server console's "adventures" (as the other admin windows', D275). The var block is
// generated (D111).

/// Admin adventure manager
// tg: code/modules/explorer_drone/manager.dm:2
public partial class datum_adventure_browser : datum
{
    // tg: code/modules/explorer_drone/manager.dm:6
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AdventureBrowser");
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/explorer_drone/manager.dm:13
    public override object ui_state(mob user = null) => ADMIN_STATE(R_DEBUG);

    /// Handles finishing adventure
    // tg: code/modules/explorer_drone/manager.dm:17
    [SignalHandler]
    public virtual void resolve_adventure(datum source = null, object result = null)
    {
        feedback_message = I18n.T("Adventure ended with result : []", result);
        QDEL_NULL(ref temp_adventure);
    }

    // tg: code/modules/explorer_drone/manager.dm:22
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        feedback_message = "";
        switch (action)
        {
            case "play":
            {
                datum_adventure_db_entry target = Dm.locate_ref(@params["ref"]) is datum_adventure_db_entry found && Dm.@in(found, GLOB.explorer_drone_adventure_db_entries) ? found : null; // locate(params["ref"]) in GLOB.explorer_drone_adventure_db_entries
                if (!Dm.truthy(target))
                    return null;
                if (Dm.truthy(temp_adventure))
                    QDEL_NULL(ref temp_adventure);
                temp_adventure = target.create_adventure() as datum_adventure;
                if (!Dm.truthy(temp_adventure))
                {
                    feedback_message = I18n.T("Instantiating adventure failed. Check runtime logs for details.");
                    return TRUE;
                }
                RegisterSignal(temp_adventure, COMSIG_ADVENTURE_FINISHED, nameof(resolve_adventure));
                temp_adventure.start_adventure();
                feedback_message = I18n.T("Adventure started");
                return TRUE;
            }
            case "adventure_choice":
                temp_adventure?.select_choice(@params["choice"]);
                return TRUE;
            case "end_play":
                if (Dm.truthy(temp_adventure))
                    QDEL_NULL(ref temp_adventure);
                feedback_message = I18n.T("Adventure stopped");
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/explorer_drone/manager.dm:51
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = await base.ui_data(user) as DmList;
        DmList adventure_data = Dm.list();
        foreach (datum_adventure_db_entry db_entry in Dm.each<datum_adventure_db_entry>(GLOB.explorer_drone_adventure_db_entries))
        {
            Dm.append(ref adventure_data, Dm.list(Dm.list(
                Dm.kv("ref", REF(db_entry)),
                Dm.kv("filename", db_entry.filename),
                Dm.kv("name", db_entry.name),
                Dm.kv("version", db_entry.version),
                Dm.kv("uploader", db_entry.uploader),
                Dm.kv("approved", db_entry.approved),
                Dm.kv("json_status", Dm.truthy(db_entry.raw_json) ? "Valid JSON" : "Empty"))));
        }
        dot["adventures"] = adventure_data;
        dot["feedback_message"] = feedback_message;
        dot["play_mode"] = Dm.not(Dm.not(temp_adventure));
        dot["adventure_data"] = temp_adventure == null ? null : await temp_adventure.ui_data(user);
        if (Dm.truthy(temp_adventure?.delayed_action))
        {
            dot["delay_time"] = temp_adventure.delayed_action[1];
            dot["delay_message"] = temp_adventure.delayed_action[2];
        }
        else
        {
            dot["delay_time"] = 0;
            dot["delay_message"] = "";
        }
        return dot;
    }

    // tg: code/modules/explorer_drone/manager.dm:75
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        qdel(this);
    }

    // tg: code/modules/explorer_drone/manager.dm:79
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/simple/adventure)): the asset transport is the client's (DV44); the AdventureBrowser window
        // draws the preset screens from its own copy of tg's icons/ui/adventure/ pictures.
        return Dm.list();
    }

    // tg: code/modules/explorer_drone/manager.dm:82
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_NULL(ref temp_adventure);
        return dot;
    }
}

// ADMIN_VERB(adventure_manager, R_DEBUG, "Adventure Manager", "View and edit adventures.", ADMIN_CATEGORY_DEBUG): the verb's body,
// which the server console's "adventures" runs (D275; the verb's rights are the console's)
// tg: code/modules/explorer_drone/manager.dm:86
public partial class client : datum
{
    // tg: code/modules/explorer_drone/manager.dm:86
    public async DmTask adventure_manager()
    {
        datum_adventure_browser browser = Dm.@new<datum_adventure_browser>();
        await browser.ui_interact(mob);
    }
}
