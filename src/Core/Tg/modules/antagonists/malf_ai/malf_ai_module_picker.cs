// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/malf_ai/malf_ai_module_picker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI8, D326). The var block is generated (D111).

/// The datum and interface for the malf unlock menu, which lets them choose actions to unlock.
// tg: code/modules/antagonists/malf_ai/malf_ai_module_picker.dm:2
public partial class datum_module_picker : datum
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_module_picker.dm:7
    public override async DmTask New(params object[] __extra)
    {
        possible_modules = get_malf_modules() as DmList;
        await DmTask.CompletedTask;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_module_picker.dm:19
    public override object ui_state(mob user = null) => GLOB.always_state;

    // tg: code/modules/antagonists/malf_ai/malf_ai_module_picker.dm:22
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "MalfunctionModulePicker", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_module_picker.dm:28
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        data["processingTime"] = processing_time;
        if (user is mob_living_silicon_ai ai_user)
            data["hackedAPCs"] = Dm.length(ai_user.hacked_apcs);
        return data;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_module_picker.dm:35
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        DmList categories = Dm.list();
        DmList modules = Dm.list();
        data["categories"] = categories;
        data["modules"] = modules;

        foreach (datum_ai_module module in Dm.each_cast<datum_ai_module>(possible_modules))
        {
            object icon_state = module.icon_state;
            object icon = module.icon;
            if (!Dm.truthy(module.icon_state) && !Dm.truthy(module.upgrade) && module.power_type != null)
            {
                Type active_ability = module.power_type;
                icon = Dm.initial(active_ability, (datum_action a) => a.button_icon);
                icon_state = Dm.initial(active_ability, (datum_action a) => a.button_icon_state);
            }

            modules.Add(Dm.list(Dm.list(
                Dm.kv("name", module.name),
                Dm.kv("icon", icon),
                Dm.kv("icon_state", icon_state),
                Dm.kv("cost", module.cost),
                Dm.kv("desc", module.description),
                Dm.kv("category", module.category),
                Dm.kv("minimumApcs", module.minimum_apcs))));
            if (!Dm.@in(module.category, categories))
                categories.Add(module.category);
        }

        return data;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_module_picker.dm:61
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (!isAI(usr))
            return null;
        switch (Dm.str(action))
        {
            case "buy":
                object item_name = @params?["name"];
                foreach (datum_ai_module module in Dm.each_cast<datum_ai_module>(possible_modules.Copy()))
                {
                    if (Dm.eq(module.name, item_name))
                    {
                        await purchase_module((mob_living_silicon_ai)usr, module);
                        return TRUE;
                    }
                }
                break;
        }
        return null;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_module_picker.dm:76
    public virtual async DmTask purchase_module(mob_living_silicon_ai AI = null, datum_ai_module AM = null)
    {
        if (AM == null)
            return;
        if (AI == null || AI.stat == DEAD)
            return;
        if (AM.cost > processing_time)
            return;
        if (AM.minimum_apcs > Dm.length(AI.hacked_apcs))
            return;
        // Give the power and take away the money.
        if (Dm.truthy(AM.upgrade)) //upgrade and upgrade() are separate, be careful!
        {
            await AM.upgrade__proc(AI);
            possible_modules.Remove(AM);
            if (Dm.truthy(AM.unlock_text))
                to_chat(AI, AM.unlock_text);
            if (AM.unlock_sound != null)
                AI.playsound_local(AI, AM.unlock_sound, 50, 0);
            await update_static_data(AI);
        }
        else
        {
            if (AM.power_type != null)
            {
                datum_action_innate_ai action = Dm.each<datum_action_innate_ai>(AI.actions).FirstOrDefault(a => AM.power_type.IsInstanceOfType(a));
                if (action == null) //Unlocking for the first time
                {
                    var AC = (datum_action)Dm.@new(AM.power_type);
                    await AC.Grant(AI);
                    AI.current_modules.Add(Dm.@new(AM.GetType()));
                    if (Dm.truthy(AM.one_purchase))
                    {
                        possible_modules.Remove(AM);
                        await update_static_data(AI);
                    }
                    if (Dm.truthy(AM.unlock_text))
                        to_chat(AI, AM.unlock_text);
                    if (AM.unlock_sound != null)
                        AI.playsound_local(AI, AM.unlock_sound, 50, 0);
                }
                else //Adding uses to an existing module
                {
                    action.uses += Dm.initial(action.GetType(), (datum_action_innate_ai a) => a.uses);
                    action.desc = Dm.text("[] It has [] use[] remaining.", Dm.initial(action.GetType(), (datum_action a) => a.desc), action.uses, action.uses == 1 ? "" : "s");
                    await action.build_all_button_icons();
                }
            }
        }
        processing_time -= AM.cost;
        log_malf_upgrades(Dm.text("[] purchased []", key_name(AI), AM.name));
        // SSblackbox.record_feedback("nested tally", "malfunction_modules_bought", ...): the statistics database (not-translated section 19)
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_module_picker.dm:10
    public static object get_malf_modules()
    {
        DmList modules = Dm.list();
        foreach (object path in Dm.each(GLOB.malf_modules))
        {
            var AM = (datum_ai_module)Dm.@new((Type)path);
            if (AM.power_type == typeof(datum_action_innate_ai) && !Dm.truthy(AM.upgrade))
                continue;
            modules.Add(AM);
        }
        return modules;
    }
}
