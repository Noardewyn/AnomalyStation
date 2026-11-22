// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/modular_shield.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering package E10 (D287): the whole file. The modular shield control console: a multitool carries a generator from its
// buffer to the console, whose ModularShieldConsole window lists the linked generators with their strength and toggles and
// renames them; a monkey mashes its buttons. The var block is generated (D111).

// tg: code/game/machinery/computer/modular_shield.dm:1
public partial class obj_machinery_computer_modular_shield : obj_machinery_computer
{
    //lets monkeys randomly mash buttons to toggle the generators
    // tg: code/game/machinery/computer/modular_shield.dm:10
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        balloon_alert(user, I18n.T("mashing buttons"));
        if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)))
            return null;
        foreach (obj_machinery_modular_shield_generator generator in Dm.each_cast<obj_machinery_modular_shield_generator>(generators))
        {
            if (prob(50))
                await generator.toggle_shields();
        }
        return null;
    }

    // tg: code/game/machinery/computer/modular_shield.dm:18
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = NONE;
        obj_item_multitool multitool = (obj_item_multitool)tool;
        if (!(multitool.buffer is obj_machinery_modular_shield_generator))
            return dot;

        Dm.combine(ref generators, multitool.buffer);
        RegisterSignal(multitool.buffer, COMSIG_QDELETING, nameof(generator_deleted));
        multitool.set_buffer(null);
        to_chat(user, span_notice(I18n.T("You upload the data from the [] buffer.", multitool)));
        return ITEM_INTERACT_SUCCESS;
    }

    ///checks if all connected generators exist
    // tg: code/game/machinery/computer/modular_shield.dm:30
    [SignalHandler]
    public virtual void generator_deleted(obj_machinery_modular_shield_generator generator = null)
    {
        Dm.remove(ref generators, generator);
    }

    // tg: code/game/machinery/computer/modular_shield.dm:34
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ModularShieldConsole");
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/modular_shield.dm:41
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList generator_list = Dm.list();
        for (float i = 1, i__end = LAZYLEN(generators); i <= i__end; i++)
        {
            obj_machinery_modular_shield_generator generator = generators[i] as obj_machinery_modular_shield_generator;
            DmList this_generator = Dm.list();
            this_generator["name"] = generator.display_name;
            this_generator["id"] = i;
            this_generator["max_strength"] = generator.max_strength;
            this_generator["current_strength"] = generator.stored_strength;
            this_generator["generator_name"] = generator.display_name;
            this_generator["active"] = generator.active;
            this_generator["recovering"] = Dm.truthy(generator.recovering) ? generator.recovering : generator.initiating;
            this_generator["current_regeneration"] = generator.current_regeneration;
            if (Dm.truthy(Dm.bitand(generator.machine_stat, NOPOWER)))
                this_generator["inactive"] = TRUE;
            Dm.append(ref generator_list, Dm.list(this_generator));
        }
        data["generators"] = generator_list;
        return data;
    }

    // tg: code/game/machinery/computer/modular_shield.dm:61
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        obj_machinery_modular_shield_generator selected_generator = generators[@params["id"]] as obj_machinery_modular_shield_generator;
        if (QDELETED(selected_generator))
            return dot;
        object sw__1 = action;
        if (Dm.eq(sw__1, "toggle_shields"))
        {
            await selected_generator.toggle_shields();
            return TRUE;
        }
        else if (Dm.eq(sw__1, "rename"))
        {
            selected_generator.display_name = @params["name"] as string;
            return TRUE;
        }
        return dot;
    }
}
