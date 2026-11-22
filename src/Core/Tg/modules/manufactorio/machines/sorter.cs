// Translated from tgstation (snapshot 1d99bd7f): code/modules/manufactorio/machines/sorter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the conveyor sort-router, whole. What enters its tile is pushed, a fifth of a second later, to the side of the first
// filter it meets (an inverted filter: the first it does not meet), else to the unmet side; its operate overlay flicks toward that
// side. A multitool or a hand opens tg's ManufacturingSorter window: add a filter (tgui's input list), edit, invert by order, turn
// its side, move it up or down, remove it, turn the unmet side. The var blocks are generated.

// tg: code/modules/manufactorio/machines/sorter.dm:1
public partial class obj_machinery_power_manufacturing_sorter : obj_machinery_power_manufacturing
{
    // tg: code/modules/manufactorio/machines/sorter.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (dir_if_not_met == null)
            dir_if_not_met = dir;
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        for (float i = 1; i <= Dm.length(sort_filters); i++)
        {
            object creating_type = sort_filters[i];
            DmList values = sort_filters[creating_type] as DmList;
            datum_sortrouter_filter new_type = Dm.@new((Type)creating_type, this) as datum_sortrouter_filter;
            new_type.dir_target = Dm.num(values[1]);
            new_type.value = values[2];
            sort_filters[i] = new_type;
        }
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/manufactorio/machines/sorter.dm:22  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_machinery_power_manufacturing_sorter__Initialize__loc_connections;

    // tg: code/modules/manufactorio/machines/sorter.dm:35
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_LIST(sort_filters);
        return dot;
    }

    // tg: code/modules/manufactorio/machines/sorter.dm:39
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        await ui_interact(user);
        return dot;
    }

    // tg: code/modules/manufactorio/machines/sorter.dm:43
    public override async DmTask<object> receive_resource(atom_movable receiving = null, atom from = null, object receive_dir = null)
    {
        if (Dm.length(loc.contents) >= MANUFACTURING_TURF_LAG_LIMIT)
            return MANUFACTURING_FAIL;
        await receiving.Move(loc);
        return MANUFACTURING_SUCCESS;
    }

    // tg: code/modules/manufactorio/machines/sorter.dm:50
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["unmet_dir"] = dir_if_not_met;
        dot["filters"] = Dm.list();
        foreach (datum_sortrouter_filter sorting in Dm.each_cast<datum_sortrouter_filter>(sort_filters))
        {
            ((DmList)dot["filters"]).Add(Dm.list(Dm.list(
                Dm.kv("name", sorting.return_name()),
                Dm.kv("ref", REF(sorting)),
                Dm.kv("inverted", sorting.inverted),
                Dm.kv("dir", sorting.dir_target))));
        }
        return dot;
    }

    // tg: code/modules/manufactorio/machines/sorter.dm:61
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "del_filter":
            {
                datum_sortrouter_filter filter = locate_filter(@params["ref"]);
                if (filter == null)
                    return dot;
                Dm.remove(sort_filters, filter);
                qdel(filter);
                return TRUE;
            }
            case "new_filter":
            {
                if (Dm.length(sort_filters) >= max_filters)
                    return dot;
                if (!Dm.truthy(Dm.length(ui_act__filter_by_name)))
                {
                    ui_act__filter_by_name = Dm.list();
                    foreach (Type to_do in Dm.each_cast<Type>(subtypesof(typeof(datum_sortrouter_filter))))
                        ui_act__filter_by_name[Dm.initial(to_do, (datum_sortrouter_filter p) => p.name)] = to_do;
                    ui_act__filter_by_name = (DmList)sort_list(ui_act__filter_by_name);
                }
                object target_type = await tgui_input_list(usr, "Select a filter", "New Filter", ui_act__filter_by_name);
                if (target_type == null || !Dm.truthy(await usr.can_perform_action(this, ALLOW_SILICON_REACH)))
                    return dot;
                target_type = ui_act__filter_by_name[target_type];
                Dm.append(sort_filters, Dm.@new((Type)target_type, this));
                return TRUE;
            }
            case "rotate":
            {
                datum_sortrouter_filter filter = locate_filter(@params["ref"]);
                if (filter == null)
                    return dot;
                float next_ind = GLOB.cardinals.Find(filter.dir_target) + 1;
                filter.dir_target = Dm.num(GLOB.cardinals[WRAP(next_ind, 1, 5)]);
                return TRUE;
            }
            case "rotate_unmet":
            {
                float next_ind = GLOB.cardinals.Find(dir_if_not_met) + 1;
                dir_if_not_met = GLOB.cardinals[WRAP(next_ind, 1, 5)];
                return TRUE;
            }
            case "edit":
            {
                datum_sortrouter_filter filter = locate_filter(@params["ref"]);
                if (filter == null)
                    return dot;
                await filter.edit(usr);
                return TRUE;
            }
            case "shift":
            {
                datum_sortrouter_filter filter = locate_filter(@params["ref"]);
                if (filter == null)
                    return dot;
                float next_ind = WRAP(sort_filters.Find(filter) + Dm.num(text2num(@params["amount"])), 1, Dm.length(sort_filters) + 1);
                Dm.remove(sort_filters, filter);
                sort_filters.Insert(next_ind, filter);
                return TRUE;
            }
        }
        return dot;
    }

    // tg's locate(params["ref"]) in sort_filters
    private datum_sortrouter_filter locate_filter(object @ref)
    {
        datum_sortrouter_filter filter = Dm.locate_ref(@ref) as datum_sortrouter_filter;
        return filter != null && Dm.@in(filter, sort_filters) ? filter : null;
    }

    // tg: code/modules/manufactorio/machines/sorter.dm:72  var/static/list/filter_by_name (a proc-level static, rules 10.6)
    private static ref DmList ui_act__filter_by_name => ref TgState.Current.obj_machinery_power_manufacturing_sorter__ui_act__filter_by_name;

    // tg: code/modules/manufactorio/machines/sorter.dm:112
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ManufacturingSorter");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/manufactorio/machines/sorter.dm:118
    public virtual async DmTask<object> send_nomobs(atom_movable moving = null, object dir = null)
    {
        mutable_appearance operate = (mutable_appearance)mutable_appearance(icon, "router_operate");
        operate.dir = Dm.num(dir);
        flick_overlay_view(operate, 1 * SECONDS);
        return ismob(moving) ? await moving.Move(get_step(this, Dm.num(dir)), dir) : await send_resource(moving, dir);
    }

    // tg: code/modules/manufactorio/machines/sorter.dm:124
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(anchored) || Dm.truthy(delay_timerid) || !Dm.truthy(Dm.length(Dm.sub(loc?.contents, 1))))
            return null;
        Waiting.NoSleep(launch_everything());
        return null;
    }

    /// Is target something we should even attempt to start launching?
    // tg: code/modules/manufactorio/machines/sorter.dm:130
    public virtual object can_be_launched(atom_movable target = null)
    {
        object dot = TRUE;
        if (target == null || target == this || Dm.truthy(target.anchored)) //target is not movable, us or anchored
            return FALSE;
        mob_living probably_living = target as mob_living;
        if (isdead(target) || (probably_living != null && Dm.truthy(probably_living.incorporeal_move))) //target is incorporeal
            return FALSE;
        return dot;
    }

    // tg: code/modules/manufactorio/machines/sorter.dm:138
    public virtual void on_entered(datum source = null, atom_movable mover = null)
    {
        if (!Dm.truthy(anchored) || !Dm.truthy(can_be_launched(mover)) || Dm.truthy(delay_timerid))
            return;
        delay_timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(launch_everything)), 0.2f * SECONDS, file: "code/modules/manufactorio/machines/sorter.dm", line: 142);
    }

    // tg: code/modules/manufactorio/machines/sorter.dm:144
    public virtual async DmTask launch_everything()
    {
        delay_timerid = null;
        if (!Dm.truthy(anchored))
            return;
        turf where_we_at = get_turf(this);
        foreach (atom_movable mover in Dm.each_cast<atom_movable>(where_we_at.contents.Copy()))
        {
            if (!Dm.truthy(can_be_launched(mover)))
                continue;
            foreach (datum_sortrouter_filter sorting in Dm.each_cast<datum_sortrouter_filter>(sort_filters))
            {
                if (Dm.eq(sorting.meets_conditions(mover), sorting.inverted))
                    continue;
                await send_nomobs(mover, sorting.dir_target);
                return;
            }
            await send_nomobs(mover, dir_if_not_met);
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/manufactorio/machines/sorter.dm:22  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_machinery_power_manufacturing_sorter__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_machinery_power_manufacturing_sorter.on_entered)));

    // tg: code/modules/manufactorio/machines/sorter.dm:72  var/static/list/filter_by_name
    internal DmList obj_machinery_power_manufacturing_sorter__ui_act__filter_by_name;
}
