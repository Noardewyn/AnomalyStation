// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/vatgrowing/microscope.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, with its Microscope window. The crafting recipe is var-only. The var blocks are generated (D111).

// tg: code/modules/research/xenobiology/vatgrowing/microscope.dm:1
public partial class obj_structure_microscope : obj_structure
{
    // tg: code/modules/research/xenobiology/vatgrowing/microscope.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_item_typechecks), TgState.Current.obj_structure_microscope__Initialize__hovering_item_typechecks));
        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_bare_hands), Dm.named("rmb_text", I18n.T("Remove petri dish"))));
        return dot;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/microscope.dm:20
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (tool is obj_item_petri_dish)
            return await add_dish(user, (obj_item_petri_dish)tool);
        return dot;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/microscope.dm:25
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (current_dish != null)
            return await remove_dish(user as mob_living);
        return dot;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/microscope.dm:30
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Microscope", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/microscope.dm:36
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        data["has_dish"] = current_dish != null ? TRUE : FALSE;
        data["cell_lines"] = Dm.list();

        if (current_dish == null)
            return data;
        if (current_dish.sample == null)
            return data;

        foreach (object organism in Dm.each(current_dish.sample.micro_organisms))
        {
            if (organism is datum_micro_organism_cell_line)
            {
                datum_micro_organism_cell_line cell_line = (datum_micro_organism_cell_line)organism;
                Type resulting_atom = cell_line.resulting_atom;
                float dt = Dm.num(SSmachines.wait) / 10; // SSMACHINES_DT (__DEFINES/subsystems.dm:276)
                DmList organism_data = Dm.list(
                    Dm.kv("type", "cell line"),
                    Dm.kv("name", cell_line.name),
                    Dm.kv("desc", cell_line.desc),
                    Dm.kv("icon", resulting_atom != null ? Dm.initial(resulting_atom, (atom a) => a.icon) : ""),
                    Dm.kv("icon_state", resulting_atom != null ? Dm.initial(resulting_atom, (atom a) => a.icon_state) : ""),
                    Dm.kv("consumption_rate", cell_line.consumption_rate * dt),
                    Dm.kv("growth_rate", cell_line.growth_rate * dt),
                    Dm.kv("suspectibility", cell_line.virus_suspectibility * dt),
                    Dm.kv("requireds", get_reagent_list(cell_line.required_reagents)),
                    Dm.kv("supplementaries", get_reagent_list(cell_line.supplementary_reagents)),
                    Dm.kv("suppressives", get_reagent_list(cell_line.suppressive_reagents))
                );
                data["cell_lines"] = Dm.add(data["cell_lines"], Dm.list(organism_data));
            }

            if (organism is datum_micro_organism_virus)
            {
                datum_micro_organism_virus virus = (datum_micro_organism_virus)organism;
                DmList virus_data = Dm.list(
                    Dm.kv("type", "virus"),
                    Dm.kv("name", virus.name),
                    Dm.kv("desc", virus.desc)
                );
                data["cell_lines"] = Dm.add(data["cell_lines"], Dm.list(virus_data));
            }
        }

        return data;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/microscope.dm:77
    public virtual object get_reagent_list(DmList reagents = null)
    {
        DmList reagent_list = Dm.list();
        foreach (object i in Dm.each(reagents)) //Convert from assoc to normal. Yeah very shit.
        {
            Type reagent = i as Type;
            reagent_list[Dm.text("[]", Dm.initial(reagent, (datum_reagent r) => r.name))] = Dm.num(reagents[i]) * (Dm.num(SSmachines.wait) / 10); // SSMACHINES_DT
        }
        return reagent_list;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/microscope.dm:84
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "eject_petridish":
                if (current_dish != null)
                {
                    await remove_dish(ui.user as mob_living);
                    dot = TRUE;
                }
                break;
        }
        update_appearance();
        return dot;
    }

    ///Insert a new dish, swapping the inserted one
    // tg: code/modules/research/xenobiology/vatgrowing/microscope.dm:96
    public virtual async DmTask<object> add_dish(mob_living user = null, obj_item_petri_dish new_dish = null)
    {
        obj_item_petri_dish old_dish = null;
        if (current_dish != null)
            old_dish = current_dish;
        if (!Dm.truthy(await user.transferItemToLoc(new_dish, this)))
        {
            balloon_alert(user, I18n.T("couldn't add!"));
            return ITEM_INTERACT_FAILURE;
        }
        current_dish = new_dish;
        await update_static_data_for_all_viewers();
        if (old_dish != null)
        {
            if (!Dm.truthy(await user.put_in_hands(old_dish)))
                await old_dish.forceMove(get_turf(this));
            balloon_alert(user, I18n.T("dish swapped"));
        }
        else
            balloon_alert(user, I18n.T("dish added"));
        return ITEM_INTERACT_SUCCESS;
    }

    ///Take the inserted dish, or drop it on the floor
    // tg: code/modules/research/xenobiology/vatgrowing/microscope.dm:114
    public virtual async DmTask<object> remove_dish(mob_living user = null)
    {
        if (current_dish == null)
            return SECONDARY_ATTACK_CONTINUE_CHAIN;
        if (!Dm.truthy(await user.put_in_hands(current_dish)))
            await current_dish.forceMove(get_turf(this));
        current_dish = null;
        await update_static_data_for_all_viewers();
        balloon_alert(user, I18n.T("dish removed"));
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/research/xenobiology/vatgrowing/microscope.dm:12  /obj/structure/microscope/Initialize's var/static/list/hovering_item_typechecks
    internal DmList obj_structure_microscope__Initialize__hovering_item_typechecks = Dm.list(
        Dm.kv(typeof(obj_item_petri_dish), Dm.list(Dm.kv(SCREENTIP_CONTEXT_LMB, "Add petri dish"))));
}
