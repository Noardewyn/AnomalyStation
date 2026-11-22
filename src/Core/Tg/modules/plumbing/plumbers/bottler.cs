// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/bottler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var block is generated (D111).

// tg: code/modules/plumbing/plumbers/bottler.dm:1
public partial class obj_machinery_plumbing_bottler : obj_machinery_plumbing
{
    // tg: code/modules/plumbing/plumbers/bottler.dm:22
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_demand), layer, Dm.named("distinct_reagent_cap", 3)));
        Waiting.NoSleep(setDir(dir));
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/bottler.dm:27
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("A small screen indicates that it will fill for []u.", wanted_amount)));
        if (!Dm.truthy(valid_output_configuration))
            Dm.append(ref dot, span_warning(I18n.T("A flashing notification on the screen reads: \"Output location error!\"")));
        return dot;
    }

    ///changes the tile array
    // tg: code/modules/plumbing/plumbers/bottler.dm:34
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        turf target_turf = get_turf(this) as turf;
        switch (Dm.num(dir))
        {
            case NORTH:
                goodspot = get_step(target_turf, NORTH) as turf;
                inputspot = get_step(target_turf, SOUTH) as turf;
                badspot = get_step(target_turf, EAST) as turf;
                break;
            case SOUTH:
                goodspot = get_step(target_turf, SOUTH) as turf;
                inputspot = get_step(target_turf, NORTH) as turf;
                badspot = get_step(target_turf, WEST) as turf;
                break;
            case WEST:
                goodspot = get_step(target_turf, WEST) as turf;
                inputspot = get_step(target_turf, EAST) as turf;
                badspot = get_step(target_turf, NORTH) as turf;
                break;
            case EAST:
                goodspot = get_step(target_turf, EAST) as turf;
                inputspot = get_step(target_turf, WEST) as turf;
                badspot = get_step(target_turf, SOUTH) as turf;
                break;
        }

        //If by some miracle
        if ((!Dm.truthy(valid_output_configuration)) && (goodspot != null && inputspot != null && badspot != null))
        {
            valid_output_configuration = TRUE;
            begin_processing();
        }
    }

    ///changing input ammount with a window
    // tg: code/modules/plumbing/plumbers/bottler.dm:61
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (!Dm.truthy(valid_output_configuration))
        {
            to_chat(user, span_warning(I18n.T("A flashing notification on the screen reads: \"Output location error!\"")));
            return dot;
        }
        object new_amount = await tgui_input_number(user, "Set Amount to Fill", "Desired Amount", max_value: ((datum_reagents)reagents).maximum_volume, round_value: TRUE);
        if (!Dm.truthy(new_amount) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return dot;
        wanted_amount = Dm.num(new_amount);
        to_chat(user, span_notice(I18n.T(" The [] will now fill for []u.", this, wanted_amount)));
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/bottler.dm:72
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(is_operational))
            return null;
        // Sanity check the result locations and stop processing if they don't exist
        if (goodspot == null || badspot == null || inputspot == null)
        {
            valid_output_configuration = FALSE;
            return PROCESS_KILL;
        }

        datum_reagents holder = (datum_reagents)reagents;
        ///see if machine has enough to fill, is anchored down and has any inputspot objects to pick from
        if (holder.total_volume >= wanted_amount && Dm.truthy(anchored) && Dm.truthy(length(inputspot.contents)))
        {
            Waiting.NoSleep(use_energy(active_power_usage * Dm.num(seconds_per_tick)));
            obj AM = pick(inputspot.contents) as obj;///pick a reagent_container that could be used
            //allowed containers
            if (Dm.truthy(is_type_in_list(AM, process__allowed_containers)))
            {
                obj_item B = (obj_item)AM;
                datum_reagents B_reagents = (datum_reagents)B.reagents;
                ///see if it would overflow else inject
                if ((B_reagents.total_volume + wanted_amount) <= B_reagents.maximum_volume)
                {
                    Waiting.NoSleep(holder.trans_to(B, wanted_amount));
                    Waiting.NoSleep(B.forceMove(goodspot));
                    return null;
                }
                ///glass was full so we move it away
                Waiting.NoSleep(AM.forceMove(badspot));
            }
            else if (istype(AM, typeof(obj_item_slime_extract))) ///slime extracts need inject
            {
                Waiting.NoSleep(AM.forceMove(goodspot));
                Waiting.NoSleep(holder.trans_to(AM, wanted_amount, methods: INJECT));
            }
            else if (istype(AM, typeof(obj_item_slimecross_industrial))) ///no need to move slimecross industrial things
                Waiting.NoSleep(holder.trans_to(AM, wanted_amount, methods: INJECT));
        }
        return null;
    }

    // tg: code/modules/plumbing/plumbers/bottler.dm:85  process's var/static/list/allowed_containers (a proc-level static, rules 10.6)
    private static ref DmList process__allowed_containers => ref TgState.Current.obj_machinery_plumbing_bottler__process__allowed_containers;
}

public sealed partial class TgState
{
    // tg: code/modules/plumbing/plumbers/bottler.dm:85
    internal DmList obj_machinery_plumbing_bottler__process__allowed_containers = Dm.list(
        typeof(obj_item_reagent_containers_cup),
        typeof(obj_item_ammo_casing_shotgun_dart));
}
