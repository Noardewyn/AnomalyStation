// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/combustible_flooder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file (plasma's and hot ice's material, basemats.dm:232, :582). The var
// block is generated (D111). The admin and attack logs of flood are deferred leaves (logging, M10).

/// Component that floods gas when ignited by fire.
// tg: code/datums/components/combustible_flooder.dm:2
public partial class datum_component_combustible_flooder : datum_component
{
    // tg: code/datums/components/combustible_flooder.dm:8
    [DmParams("initialize_gas_id", "initialize_gas_amount", "initialize_temp_amount")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object initialize_gas_id = Dm.arg(__extra, 0);
        object initialize_gas_amount = Dm.arg(__extra, 1);
        object initialize_temp_amount = Dm.arg(__extra, 2);

        this.gas_id = initialize_gas_id;
        this.gas_amount = initialize_gas_amount;
        this.temp_amount = initialize_temp_amount;

        // Any item made of plasma is going to have this component, making it extremely difficult to blacklist fire hazards during create_and_destroy.
        // So let's just completely neuter them during unit tests so we don't burn down the testing area and cause spurious runtimes.
        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(attackby_react));
        RegisterSignal(parent, COMSIG_ATOM_FIRE_ACT, nameof(flame_react));
        RegisterSignal(parent, COMSIG_ATOM_TOUCHED_SPARKS, nameof(sparks_react));
        RegisterSignal(parent, COMSIG_ATOM_BULLET_ACT, nameof(projectile_react));
        RegisterSignal(parent, COMSIG_ATOM_TOOL_ACT(TOOL_WELDER), nameof(welder_react));
        if (isturf(parent))
            RegisterSignal(parent, COMSIG_TURF_EXPOSE, nameof(hotspots_react));
        return null;
    }

    // tg: code/datums/components/combustible_flooder.dm:26
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_ATOM_ATTACKBY);
        UnregisterSignal(parent, COMSIG_ATOM_FIRE_ACT);
        UnregisterSignal(parent, COMSIG_ATOM_BULLET_ACT);
        UnregisterSignal(parent, COMSIG_ATOM_TOOL_ACT(TOOL_WELDER));
        if (isturf(parent))
            UnregisterSignal(parent, COMSIG_TURF_EXPOSE);
    }

    /// Do the flooding. Trigger temperature is the temperature we will flood at if we dont have a temp set at the start. Trigger referring to whatever triggered it.
    // tg: code/datums/components/combustible_flooder.dm:35
    public virtual async DmTask flood(mob user = null, object trigger_temperature = null)
    {
        float delete_parent = TRUE;
        turf_open flooded_turf = get_turf(parent) as turf_open;

        // We do this check early so closed turfs are still be able to flood.
        if (isturf(parent)) // Walls and floors.
        {
            turf parent_turf = parent as turf;
            flooded_turf = await parent_turf.ScrapeAway(1, CHANGETURF_INHERIT_AIR | CHANGETURF_FORCEOP) as turf_open; // Ensure that we always forcefully replace the turf, even if the baseturf has the same type
            delete_parent = FALSE;
        }

        object or__1 = temp_amount;
        // TURF_TEMPERATURE(temperature) "TEMP=[temperature]" (__DEFINES/atmospherics/atmos_mapping_helpers.dm:3), inline
        flooded_turf.atmos_spawn_air(Dm.text("[]=[];[]", gas_id, gas_amount, Dm.text("TEMP=[]", Dm.truthy(or__1) ? or__1 : trigger_temperature)));

        // Logging-related
        string admin_message = Dm.text("[] ignited in []", flooded_turf, ADMIN_VERBOSEJMP(flooded_turf));
        string log_message = Dm.text("ignited [] at []", flooded_turf, AREACOORD(flooded_turf));
        if (user != null)
        {
            admin_message += Dm.text(" by [] at []", ADMIN_LOOKUPFLW(user), ADMIN_COORDJMP(flooded_turf));
            user.log_message(log_message, LOG_ATTACK, log_globally: FALSE); //only individual log
        }
        else
        {
            log_message = Dm.text("[] ", key_name(user)) + log_message + Dm.text(" by fire at []", AREACOORD(flooded_turf));
            admin_message += Dm.text(" by fire at []", ADMIN_COORDJMP(flooded_turf));
            log_attack(log_message);
        }
        message_admins(admin_message);

        if (Dm.truthy(delete_parent) && !QDELETED(parent))
        {
            if (isobj(parent))
            {
                obj obj_parent = parent as obj;
                await obj_parent.deconstruct(disassembled: FALSE);
            }
            else
                qdel(parent); // For things with the explodable component like plasma mats this isn't necessary, but there's no harm.
        }
        qdel(this);
    }

    /// fire_act reaction.
    // tg: code/datums/components/combustible_flooder.dm:69
    [SignalHandler]
    public virtual void flame_react(datum source = null, object exposed_temperature = null, object exposed_volume = null)
    {
        if (Dm.num(exposed_temperature) > FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
            Waiting.NoSleep(flood(null, exposed_temperature));
    }

    /// sparks_touched reaction.
    // tg: code/datums/components/combustible_flooder.dm:76
    [SignalHandler]
    public virtual void sparks_react(datum source = null, obj_effect_particle_effect_sparks sparks = null)
    {
        if (Dm.truthy(sparks)) // this shouldn't ever be false but existence is mysterious
            Waiting.NoSleep(flood(null, FIRE_MINIMUM_TEMPERATURE_TO_SPREAD));
    }

    /// Hotspot reaction.
    // tg: code/datums/components/combustible_flooder.dm:83
    [SignalHandler]
    public virtual void hotspots_react(datum source = null, object air = null, object exposed_temperature = null)
    {
        if (Dm.num(exposed_temperature) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
            Waiting.NoSleep(flood(null, exposed_temperature));
    }

    /// Being attacked by something
    // tg: code/datums/components/combustible_flooder.dm:90
    [SignalHandler]
    public virtual void attackby_react(datum source = null, obj_item thing = null, mob user = null, object @params = null)
    {
        if (Dm.num(thing.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
            Waiting.NoSleep(flood(user, thing.get_temperature()));
    }

    /// Shot by something
    // tg: code/datums/components/combustible_flooder.dm:97
    [SignalHandler]
    public virtual void projectile_react(datum source = null, obj_projectile shot = null)
    {
        // The M7 projectiles slice
        if (shot.damage_type == BURN && Dm.num(shot.damage) > 0)
            Waiting.NoSleep(flood(shot.firer as mob, 2500));
    }

    /// Welder check. Here because tool_act is higher priority than attackby.
    // tg: code/datums/components/combustible_flooder.dm:104
    [SignalHandler]
    public virtual object welder_react(datum source = null, mob user = null, obj_item tool = null)
    {
        if (Dm.num(tool.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
        {
            Waiting.NoSleep(flood(user, tool.get_temperature()));
            return ITEM_INTERACT_BLOCKING;
        }
        return null;
    }
}
