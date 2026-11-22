// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/medipen_refiller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5, the chemistry machines' row): the medipen refiller. Its screentips (add_context) are a live-partial row
// (D264); the plunger is inert, so plunger_act is reached by no player yet. The var block is generated but allowed_pens (a
// var/static, TgState).

// tg: code/game/machinery/medipen_refiller.dm:1
public partial class obj_machinery_medipen_refiller : obj_machinery
{
    ///List of medipen subtypes it can refill and the chems needed for it to work.
    // tg: code/game/machinery/medipen_refiller.dm:11  var/static/list/allowed_pens (rules 10.6)
    public static ref DmList allowed_pens => ref TgState.Current.obj_machinery_medipen_refiller__allowed_pens;

    // tg: code/game/machinery/medipen_refiller.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_demand)));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        register_context();
        return dot;
    }

    // tg: code/game/machinery/medipen_refiller.dm:56
    public override async DmTask<object> RefreshParts()
    {
        await base.RefreshParts();
        float new_volume = 100;
        foreach (datum_stock_part_matter_bin matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            new_volume += 100 * Dm.num(matter_bin.tier);
        if (reagents == null)
            create_reagents(new_volume, TRANSPARENT);
        ((datum_reagents)reagents).maximum_volume = new_volume;
        return TRUE;
    }

    // tg: code/game/machinery/medipen_refiller.dm:66
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(user.combat_mode))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (Dm.truthy(DOING_INTERACTION(user, this)))
        {
            balloon_alert(user, I18n.T("already interacting!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (is_reagent_container(tool) && Dm.truthy(tool.is_open_container()))
        {
            var reagent_container = (obj_item_reagent_containers)tool;
            if (!Dm.truthy(length(((datum_reagents)reagent_container.reagents).reagent_list)))
            {
                balloon_alert(user, I18n.T("nothing to transfer!"));
                return ITEM_INTERACT_BLOCKING;
            }

            object units = await ((datum_reagents)reagent_container.reagents).trans_to(this, reagent_container.amount_per_transfer_from_this, transferred_by: user);
            if (Dm.truthy(units))
            {
                balloon_alert(user, I18n.T("[] units transferred", units));
                return ITEM_INTERACT_SUCCESS;
            }
            balloon_alert(user, I18n.T("reagent storage full!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (tool is obj_item_reagent_containers_hypospray_medipen medipen)
        {
            if (!Dm.truthy(LAZYFIND(allowed_pens, medipen.type)))
            {
                balloon_alert(user, I18n.T("medipen incompatible!"));
                return ITEM_INTERACT_BLOCKING;
            }

            if (Dm.truthy(length((medipen.reagents as datum_reagents)?.reagent_list)))
            {
                balloon_alert(user, I18n.T("medipen full!"));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(((datum_reagents)reagents).has_reagent(allowed_pens[medipen.type], 10)))
            {
                balloon_alert(user, I18n.T("not enough reagents!"));
                return ITEM_INTERACT_BLOCKING;
            }

            add_overlay("active");
            if (Dm.truthy(await do_after(user, 2 * SECONDS, this)))
            {
                medipen.used_up = FALSE;
                await medipen.add_initial_reagents();
                await ((datum_reagents)reagents).remove_reagent(allowed_pens[medipen.type], 10);
                balloon_alert(user, I18n.T("refilled"));
                await use_energy(active_power_usage);
            }
            cut_overlays();
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/game/machinery/medipen_refiller.dm:113
    public override async DmTask<object> plunger_act(obj_item_plunger attacking_plunger = null, mob_living user = null, object reinforced = null)
    {
        user.balloon_alert_to_viewers(I18n.T("furiously plunging..."), I18n.T("plunging medipen refiller..."));
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this)))
            return TRUE;
        user.balloon_alert_to_viewers(I18n.T("finished plunging"));
        await ((datum_reagents)reagents).expose(get_turf(this), TOUCH);
        await ((datum_reagents)reagents).clear_reagents();
        return TRUE;
    }

    // tg: code/game/machinery/medipen_refiller.dm:122
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
        => await default_unfasten_wrench(user, tool);

    // tg: code/game/machinery/medipen_refiller.dm:125
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
        => await default_deconstruction_crowbar(user, tool);

    // tg: code/game/machinery/medipen_refiller.dm:128
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/medipen_refiller.dm:131
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]_open", base_icon_state) : Dm.str(base_icon_state);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/medipen_refiller.dm:11  the medipen refiller's var/static/list/allowed_pens
    internal DmList obj_machinery_medipen_refiller__allowed_pens = Dm.list(
        Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen), typeof(datum_reagent_medicine_epinephrine)),
        Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_atropine), typeof(datum_reagent_medicine_atropine)),
        Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_salbutamol), typeof(datum_reagent_medicine_salbutamol)),
        Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_oxandrolone), typeof(datum_reagent_medicine_oxandrolone)),
        Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_salacid), typeof(datum_reagent_medicine_sal_acid)),
        Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_penacid), typeof(datum_reagent_medicine_pen_acid)),
        Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_mutadone), typeof(datum_reagent_medicine_mutadone)),
        Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_methamphetamine), typeof(datum_reagent_drug_methamphetamine)),
        Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_survival), typeof(datum_reagent_medicine_c2_libital)),
        Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_survival_luxury), typeof(datum_reagent_medicine_c2_penthrite)),
        Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_survival_luxury_purple), typeof(datum_reagent_medicine_c2_penthrite)),
        Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_invisibility), typeof(datum_reagent_drug_saturnx)));
}
