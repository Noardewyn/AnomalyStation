// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/datum_pipeline.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the pipenets): the whole file but the gas visuals (GetGasVisual, UpdateGasVisuals, CalculateGasmixColor and
// /obj/effect/abstract/gas_visual: the pipes' gas colour, with the gas visuals later in M4b), which are deferred leaves, and
// the mapping log of pipes stacked into one pipenet (AREACOORD, the admin coordinates, M10). The var block is generated.

// tg: code/modules/atmospherics/machinery/datum_pipeline.dm:1
public partial class datum_pipeline : datum
{
    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:23
    public override async DmTask New(params object[] __extra)
    {
        other_airs = Dm.list();
        members = Dm.list();
        other_atmos_machines = Dm.list();
        require_custom_reconcilation = Dm.list();
        gas_visuals = Dm.list();
        Dm.append(ref SSair.networks, this);
    }

    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:31
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref SSair.networks, this);
        if (Dm.truthy(building))
            SSair.remove_from_expansion(this);
        if (Dm.truthy(air?.volume))
            temporarily_store_air();
        foreach (obj_machinery_atmospherics_pipe considered_pipe in Dm.each<obj_machinery_atmospherics_pipe>(members))
        {
            considered_pipe.replace_pipenet(considered_pipe.parent, null);
            if (QDELETED(considered_pipe))
                continue;
            SSair.add_to_rebuild_queue(considered_pipe);
        }
        foreach (obj_machinery_atmospherics_components considered_component in Dm.each<obj_machinery_atmospherics_components>(other_atmos_machines))
            considered_component.nullify_pipenet(this);
        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:46
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process__body(dot), dot); // the slot's set waitfor = FALSE (datum process, processing.dm:48)
    }

    private async DmTask<object> process__body(DotBox<object> dot)
    {
        if (!Dm.truthy(update) || Dm.truthy(building))
            return dot.Value;
        await reconcile_air();
        //Only react if the mix has changed, and don't keep updating if it hasn't
        update = Dm.num(await air.react(this));
        CalculateGasmixColor(air);
        return dot.Value;
    }

    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:54
    public virtual void set_air(datum_gas_mixture new_air = null)
    {
        if (new_air == air)
            return;
        air = new_air;
        CalculateGasmixColor(air);
    }

    ///Preps a pipeline for rebuilding, insterts it into the rebuild queue
    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:61
    public virtual void build_pipeline(obj_machinery_atmospherics @base = null)
    {
        building = TRUE;
        float volume = 0;
        if (istype(@base, typeof(obj_machinery_atmospherics_pipe)))
        {
            obj_machinery_atmospherics_pipe considered_pipe = @base as obj_machinery_atmospherics_pipe;
            volume = Dm.num(considered_pipe.volume);
            Dm.append(ref members, considered_pipe);
            if (considered_pipe.air_temporary != null)
            {
                set_air(considered_pipe.air_temporary);
                considered_pipe.air_temporary = null;
            }
        }
        else
            add_machinery_member(@base as obj_machinery_atmospherics_components);

        if (air == null)
            set_air(Dm.@new<datum_gas_mixture>());

        air.volume = volume;
        SSair.add_to_expansion(this, @base);
    }

    ///Has the same effect as build_pipeline(), but this doesn't queue its work, so overrun abounds. It's useful for the pregame
    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:81
    public virtual void build_pipeline_blocking(obj_machinery_atmospherics @base = null)
    {
        float volume = 0;
        if (istype(@base, typeof(obj_machinery_atmospherics_pipe)))
        {
            obj_machinery_atmospherics_pipe considered_pipe = @base as obj_machinery_atmospherics_pipe;
            volume = Dm.num(considered_pipe.volume);
            Dm.append(ref members, considered_pipe);
            if (considered_pipe.air_temporary != null)
            {
                set_air(considered_pipe.air_temporary);
                considered_pipe.air_temporary = null;
            }
        }
        else
            add_machinery_member(@base as obj_machinery_atmospherics_components);

        if (air == null)
            set_air(Dm.@new<datum_gas_mixture>());
        DmList possible_expansions = Dm.list(@base);
        while (Dm.truthy(possible_expansions.len))
        {
            foreach (obj_machinery_atmospherics borderline in Dm.each<obj_machinery_atmospherics>(possible_expansions))
            {
                DmList result = borderline.pipeline_expansion(this) as DmList;
                if (!Dm.truthy(result?.len))
                {
                    Dm.remove(ref possible_expansions, borderline);
                    continue;
                }
                foreach (obj_machinery_atmospherics considered_device in Dm.each<obj_machinery_atmospherics>(result))
                {
                    if (!istype(considered_device, typeof(obj_machinery_atmospherics_pipe)))
                    {
                        considered_device.set_pipenet(this, borderline);
                        add_machinery_member(considered_device as obj_machinery_atmospherics_components);
                        continue;
                    }
                    obj_machinery_atmospherics_pipe item = considered_device as obj_machinery_atmospherics_pipe;
                    if (Dm.truthy(members.Find(item)))
                        continue;
                    if (item.parent != null)
                    {
                        // var/static/pipenetwarnings = 10 and log_mapping("build_pipeline(): [item.type] added to a pipenet while
                        // still having one. ... around [AREACOORD(item)].") (:107-113): the mapping log of the pipes stacked
                        // in one turf, which reads nothing else (AREACOORD, the admin coordinates, M10)
                        Dm.Deferred("code/modules/atmospherics/machinery/datum_pipeline.dm:107", "build_pipeline_blocking: the mapping log of pipes stacked into one pipenet (AREACOORD, M10+)");
                    }

                    Dm.append(ref members, item);
                    Dm.append(ref possible_expansions, item);

                    volume += Dm.num(item.volume);
                    item.replace_pipenet(item.parent, this);

                    if (item.air_temporary != null)
                    {
                        air.merge(item.air_temporary);
                        item.air_temporary = null;
                    }
                }

                Dm.remove(ref possible_expansions, borderline);
            }
        }

        air.volume = volume;

        /**
         *  For a machine to properly "connect" to a pipeline and share gases,
         *  the pipeline needs to acknowledge a gas mixture as its member.
         *  This is currently handled by the other_airs list in the pipeline datum.
         *
         *	Other_airs itself is populated by gas mixtures through the parents list that each machineries have.
         *	This parents list is populated when a machinery calls update_parents and is then added into the queue by the controller.
         */
    }

    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:141
    public virtual void add_machinery_member(obj_machinery_atmospherics_components considered_component = null)
    {
        Dm.combine(ref other_atmos_machines, considered_component);
        if (Dm.truthy(considered_component.custom_reconcilation))
            Dm.combine(ref require_custom_reconcilation, considered_component);
        DmList returned_airs = considered_component.return_pipenet_airs(this) as DmList;
        if (!Dm.truthy(Dm.length(returned_airs)) || Dm.@in(null, returned_airs))
            stack_trace(Dm.text("addMachineryMember: Nonexistent (empty list) or null machinery gasmix added to pipeline datum from [] which is of type []. Nearby: ([], [], [])", considered_component, considered_component.type, considered_component.x, considered_component.y, considered_component.z), "code/modules/atmospherics/machinery/datum_pipeline.dm", 147);
        Dm.combine(ref other_airs, returned_airs);
    }

    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:151
    public virtual void add_member(obj_machinery_atmospherics reference_device = null, obj_machinery_atmospherics device_to_add = null)
    {
        if (!istype(reference_device, typeof(obj_machinery_atmospherics_pipe)))
        {
            reference_device.set_pipenet(this, device_to_add);
            add_machinery_member(reference_device as obj_machinery_atmospherics_components);
        }
        else
        {
            obj_machinery_atmospherics_pipe reference_pipe = reference_device as obj_machinery_atmospherics_pipe;
            if (reference_pipe.parent != null)
                merge(reference_pipe.parent);
            reference_pipe.replace_pipenet(reference_pipe.parent, this);
            DmList adjacent = reference_pipe.pipeline_expansion() as DmList;
            foreach (obj_machinery_atmospherics_pipe adjacent_pipe in Dm.each<obj_machinery_atmospherics_pipe>(adjacent))
            {
                if (adjacent_pipe.parent == this)
                    continue;
                datum_pipeline parent_pipeline = adjacent_pipe.parent;
                merge(parent_pipeline);
            }
            if (!Dm.truthy(members.Find(reference_pipe)))
            {
                Dm.append(ref members, reference_pipe);
                air.volume += Dm.num(reference_pipe.volume);
            }
        }
    }

    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:170
    public virtual void merge(datum_pipeline parent_pipeline = null)
    {
        if (parent_pipeline == this)
            return;
        air.volume += parent_pipeline.air.volume;
        members.Add(parent_pipeline.members);
        foreach (obj_machinery_atmospherics_pipe reference_pipe in Dm.each<obj_machinery_atmospherics_pipe>(parent_pipeline.members))
            reference_pipe.replace_pipenet(reference_pipe.parent, this);
        air.merge(parent_pipeline.air);
        foreach (obj_machinery_atmospherics_components reference_component in Dm.each<obj_machinery_atmospherics_components>(parent_pipeline.other_atmos_machines))
        {
            reference_component.replace_pipenet(parent_pipeline, this);
            if (Dm.truthy(reference_component.custom_reconcilation))
                Dm.combine(ref require_custom_reconcilation, reference_component);
        }
        Dm.combine(ref other_atmos_machines, parent_pipeline.other_atmos_machines);
        Dm.combine(ref other_airs, parent_pipeline.other_airs);
        parent_pipeline.members.Cut();
        parent_pipeline.other_atmos_machines.Cut();
        parent_pipeline.require_custom_reconcilation.Cut();
        update = TRUE;
        qdel(parent_pipeline);
    }

    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:203
    public virtual void temporarily_store_air()
    {
        //Update individual gas_mixtures by volume ratio

        foreach (obj_machinery_atmospherics_pipe member in Dm.each<obj_machinery_atmospherics_pipe>(members))
        {
            member.air_temporary = Dm.@new<datum_gas_mixture>();
            member.air_temporary.volume = Dm.num(member.volume);
            member.air_temporary.copy_from_ratio(air, Dm.num(member.volume) / Dm.num(air.volume));

            member.air_temporary.temperature = air.temperature;
        }
    }

    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:213
    public virtual async DmTask<object> temperature_interact(turf target = null, object share_volume = null, object thermal_conductivity = null)
    {
        float total_heat_capacity = Dm.num(air.heat_capacity());
        float partial_heat_capacity = total_heat_capacity * (Dm.num(share_volume) / Dm.num(air.volume));

        float turf_temperature = Dm.num(target.GetTemperature());
        float turf_heat_capacity = Dm.num(target.GetHeatCapacity());

        if (turf_heat_capacity <= 0 || partial_heat_capacity <= 0)
            return TRUE;

        float delta_temperature = turf_temperature - air.temperature;

        float heat = Dm.num(thermal_conductivity) * CALCULATE_CONDUCTION_ENERGY(delta_temperature, partial_heat_capacity, turf_heat_capacity);
        air.temperature += heat / total_heat_capacity;
        target.TakeTemperature(-1 * heat / turf_heat_capacity);

        if (Dm.truthy(target.blocks_air))
            await target.temperature_expose(air, target.temperature);
        update = TRUE;
        return null;
    }

    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:233
    public virtual object return_air()
    {
        object dot = Dm.add(other_airs, air);
        if (Dm.truthy(list_clear_nulls(dot as DmList)))
            stack_trace(Dm.text("[] has one or more null gas mixtures, which may cause bugs. Null mixtures will not be considered in reconcile_air().", this), "code/modules/atmospherics/machinery/datum_pipeline.dm", 236);
        return dot;
    }

    /// Called when the pipenet needs to update and mix together all the air mixes
    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:239
    public virtual async DmTask reconcile_air()
    {
        DmList gas_mixture_list = Dm.list();
        DmList pipeline_list = Dm.list();
        Dm.append(ref pipeline_list, this);

        for (float i = 1; i <= pipeline_list.len; i++) //can't do a for-each here because we may add to the list within the loop
        {
            datum_pipeline pipeline = pipeline_list[i] as datum_pipeline;
            if (pipeline == null)
                continue;
            Dm.append(ref gas_mixture_list, pipeline.other_airs);
            Dm.append(ref gas_mixture_list, pipeline.air);
            foreach (obj_machinery_atmospherics_components atmos_machine in Dm.each_cast<obj_machinery_atmospherics_components>(pipeline.require_custom_reconcilation))
            {
                Dm.combine(ref pipeline_list, await atmos_machine.return_pipenets_for_reconcilation(this));
                Dm.append(ref gas_mixture_list, atmos_machine.return_airs_for_reconcilation(this));
            }
        }

        float total_thermal_energy = 0;
        float total_heat_capacity = 0;

        float volume_sum = 0;

        process_id = WRAP_UID(process_id + 1);
        datum_gas_mixture total_gas_mixture = Dm.@new<datum_gas_mixture>();
        DmList total_cached_moles = total_gas_mixture.moles;
        DmList cached_specific_heat = datum_gas_mixture.gas_meta[META_GAS_SPECIFIC_HEAT] as DmList; // GAS_META (atmos_helpers.dm)

        foreach (datum_gas_mixture gas_mixture in Dm.each_cast<datum_gas_mixture>(gas_mixture_list))
        {
            // Ensure we never walk the same mix twice
            if (gas_mixture.pipeline_cycle == process_id)
            {
                Dm.remove(ref gas_mixture_list, gas_mixture);
                continue;
            }
            gas_mixture.pipeline_cycle = process_id;
            volume_sum += Dm.num(gas_mixture.volume);

            // This is sort of a combined merge + heat_capacity calculation

            DmList giver_cached_moles = gas_mixture.moles;
            float heat_capacity = Dm.num(values_dot(giver_cached_moles, cached_specific_heat));
            //gas transfer
            foreach ((object gas_id, object amount) in Dm.pairs(giver_cached_moles))
                Dm.append_at(total_cached_moles, gas_id, amount);

            total_heat_capacity += heat_capacity;
            total_thermal_energy += gas_mixture.temperature * heat_capacity;
        }

        if (volume_sum == 0)
            return;

        total_gas_mixture.volume = volume_sum;
        total_gas_mixture.temperature = Dm.truthy(total_heat_capacity) ? (total_thermal_energy / total_heat_capacity) : 0;
        total_gas_mixture.garbage_collect();

        //Update individual gas_mixtures by volume ratio
        foreach (datum_gas_mixture gas_mixture in Dm.each_cast<datum_gas_mixture>(gas_mixture_list))
            gas_mixture.copy_from_ratio(total_gas_mixture, Dm.num(gas_mixture.volume) / volume_sum);
    }

    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:252  var/static/process_id = 0 (rules 10.6: a TgState field)
    private static ref float process_id => ref TgState.Current.datum_pipeline__reconcile_air__process_id;

    //--------------------
    // GAS VISUALS STUFF
    //
    // Gas visuals use direct color + alpha on the gas_visual object rather than
    // a color filter + KEEP_APART.
    // Color filters are expensive.
    // KEEP_APART forces a separate render.

    /**
     * Used to create and/or get the gas visual overlay created using the given icon file.
     * The color is automatically kept up to date and expected to be used as a vis_contents object.
     */
    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:307
    public virtual object GetGasVisual(icon icon_file = null)
    {
        Dm.Deferred("code/modules/atmospherics/machinery/datum_pipeline.dm:307", "GetGasVisual: the pipes' gas colour overlay (the gas visuals, later in M4b)");
        return null;
    }

    /// Called when the gasmix color has changed and the gas visuals need to be updated.
    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:319
    public virtual void UpdateGasVisuals()
    {
        Dm.Deferred("code/modules/atmospherics/machinery/datum_pipeline.dm:319", "UpdateGasVisuals: the pipes' gas colour overlay (the gas visuals, later in M4b)");
    }

    /// After updating, this proc handles looking at the new gas mixture and blends the colors together according to percentage of the gas mix.
    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:325
    [SignalHandler]
    public virtual void CalculateGasmixColor(datum_gas_mixture source = null)
    {
        // The weighted HSV blend of the gases' primary colours into gasmix_color (BlendHSV, hsv_gradient), read only by the
        // gas visuals.
        Dm.Deferred("code/modules/atmospherics/machinery/datum_pipeline.dm:325", "CalculateGasmixColor: the pipes' gas colour (the gas visuals, later in M4b)");
    }
}

// tg: code/modules/atmospherics/machinery/datum_pipeline.dm:190 (procs of the machines defined in datum_pipeline.dm)
public partial class obj_machinery_atmospherics
{
    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:190
    public virtual void add_member(obj_machinery_atmospherics considered_device = null)
    {
        return;
    }
}

public partial class obj_machinery_atmospherics_pipe
{
    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:193
    public override void add_member(obj_machinery_atmospherics considered_device = null)
    {
        parent.add_member(considered_device, this);
    }
}

public partial class obj_machinery_atmospherics_components
{
    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:196
    public override void add_member(obj_machinery_atmospherics considered_device = null)
    {
        datum_pipeline device_pipeline = return_pipenet(considered_device) as datum_pipeline;
        if (device_pipeline == null)
            throw Dm.CRASH(Dm.text("null.add_member() called by [] on []", type, this), "code/modules/atmospherics/machinery/datum_pipeline.dm", 199); // COORD(src) (the admin coordinates, M10) is the atom itself here
        device_pipeline.add_member(considered_device, this);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/atmospherics/machinery/datum_pipeline.dm:252  /datum/pipeline/proc/reconcile_air's var/static/process_id = 0
    internal float datum_pipeline__reconcile_air__process_id;
}
