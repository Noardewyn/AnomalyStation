// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file but the gas cloud's particles (the particle holder is a skeleton and the client draws
// no particles, as the cigarettes' smoke) and the atmos investigate log. The var blocks are generated.

/// Not particularly resistant, but versatile due to the selection of gases it can generate.
// tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:2
public partial class mob_living_basic_guardian_gaseous : mob_living_basic_guardian
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ATOM_PRE_PRESSURE_PUSH, nameof(pre_pressure_moved));
        gas = Dm.@new<datum_action_cooldown_mob_cooldown_expel_gas>(this);
        gas.owner_has_control = FALSE; // It's nicely integrated with the Guardian UI, no need to have two buttons
        Waiting.NoSleep(gas.Grant(this));
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:25
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref gas);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:29
    public override async DmTask toggle_modes()
    {
        await gas.Trigger();
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:32
    public override async DmTask set_summoner(mob_living to_who = null, float? different_person = null)
    {
        await base.set_summoner(to_who, different_person);
        if (QDELETED(this))
            return;
        ADD_TRAIT(summoner, TRAIT_NOFIRE, REF(this));
        RegisterSignal(summoner, COMSIG_LIVING_LIFE, nameof(on_summoner_life));
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:39
    public override async DmTask cut_summoner(float? different_person = null)
    {
        if (summoner != null)
        {
            REMOVE_TRAIT(summoner, TRAIT_NOFIRE, REF(this));
            UnregisterSignal(summoner, COMSIG_LIVING_LIFE);
        }
        await base.cut_summoner(different_person);
    }

    /// Maintain our summoner at a stable body temperature
    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:46
    [SignalHandler]
    public virtual void on_summoner_life(mob_living source = null, object seconds_per_tick = null)
    {
        source.adjust_bodytemperature(get_temp_change_amount((Dm.num(summoner.get_body_temp_normal()) - summoner.bodytemperature), temp_stabilization_rate * Dm.num(seconds_per_tick)));
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:50
    public override async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.melee_attack(target, modifiers, ignore_cooldown);
        if (!Dm.truthy(dot) || !isliving(target))
            return dot;
        do_sparks(1, TRUE, target);
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:56
    public override async DmTask recall_effects()
    {
        await base.recall_effects();
        if (summoner != null)
            UnregisterSignal(summoner, COMSIG_ATOM_PRE_PRESSURE_PUSH);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:61
    public override async DmTask manifest_effects()
    {
        await base.manifest_effects();
        if (summoner != null)
            RegisterSignal(summoner, COMSIG_ATOM_PRE_PRESSURE_PUSH, nameof(pre_pressure_moved));
    }

    /// We stand firm in the face of gas
    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:67
    [SignalHandler]
    public virtual object pre_pressure_moved(datum source = null)
    {
        return COMSIG_ATOM_BLOCKS_PRESSURE;
    }
}


/// Expel a range of gases
// tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:73
public partial class datum_action_cooldown_mob_cooldown_expel_gas : datum_action_cooldown_mob_cooldown
{
    /// Associative list of types of gases to moles we create every life tick.
    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:85
    private static DmList possible_gases => TgState.Current.datum_action_cooldown_mob_cooldown_expel_gas__possible_gases ??= Dm.list(
        Dm.kv(typeof(datum_gas_oxygen), 50),
        Dm.kv(typeof(datum_gas_nitrogen), 750), //overpressurizing is hard!.
        Dm.kv(typeof(datum_gas_water_vapor), 1), //you need incredibly little water vapor for the effects to kick in
        Dm.kv(typeof(datum_gas_nitrous_oxide), 15),
        Dm.kv(typeof(datum_gas_carbon_dioxide), 50),
        Dm.kv(typeof(datum_gas_plasma), 3),
        Dm.kv(typeof(datum_gas_bz), 10));

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:95
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        object dot = await base.Grant(granted_to, __extra);
        if (owner == null)
            return dot;
        RegisterSignal(owner, COMSIG_GUARDIAN_RECALLED, nameof(stop_gas));
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:101
    public override async DmTask Remove(mob removed_from = null)
    {
        UnregisterSignal(owner, Dm.list(COMSIG_GUARDIAN_RECALLED, COMSIG_LIVING_LIFE));
        await base.Remove(removed_from);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:105
    public override async DmTask<object> Activate(atom target = null)
    {
        StartCooldown(360 * SECONDS);
        // Regeneated each time just in case someone fucks with our list
        DmList gas_selection = Dm.list("None");
        foreach (Type gas in Dm.each_cast<Type>(possible_gases))
            gas_selection[Dm.initial(gas, (datum_gas p) => p.name)] = gas;

        object picked_gas = await tgui_input_list(owner, "Select a gas to emit.", "Gas Producer", gas_selection);
        StartCooldown();
        if (Dm.eq(picked_gas, "None"))
        {
            stop_gas();
            return null;
        }

        object gas_type = gas_selection[picked_gas];
        if (picked_gas == null || gas_type == null)
            return null;

        if (isguardian(owner))
        {
            mob_living_basic_guardian guardian_owner = (mob_living_basic_guardian)owner;
            if (!Dm.truthy(guardian_owner.is_deployed()))
            {
                to_chat(owner, span_warning("You cannot release gas without being summoned!"));
                return null;
            }
        }

        to_chat(owner, span_bolddanger(Dm.text("You start releasing [].", picked_gas)));
        owner.investigate_log(Dm.text("set their gas type to [].", picked_gas), INVESTIGATE_ATMOS);
        bool had_gas = active_gas != null;
        active_gas = gas_type;
        if (mob_smoke == null)
        {
            // mob_smoke = new(owner, /particles/smoke/steam/guardian); mob_smoke.color = initial(chosen_gas.primary_color) (:133-135): the
            // gas cloud's particles; the particle holder is a skeleton and the client draws no particles (as the cigarettes' smoke)
            Dm.Deferred("code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:133", "expel_gas Activate: the gas cloud's particle holder (particles, M10+)");
        }
        if (!had_gas)
            RegisterSignal(owner, COMSIG_LIVING_LIFE, nameof(on_life));
        return null;
    }

    /// Turns off the gas
    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:140
    [SignalHandler]
    public virtual void stop_gas()
    {
        if (active_gas != null)
            to_chat(this, span_notice("You stop releasing gas.")); // tg sends it to the action (src), not the owner: nobody reads it
        active_gas = null;
        QDEL_NULL(ref mob_smoke);
        UnregisterSignal(owner, COMSIG_LIVING_LIFE);
    }

    /// Release gas every life tick while active
    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:149
    [SignalHandler]
    public virtual void on_life(datum source = null, object seconds_per_tick = null)
    {
        if (active_gas == null)
            return; // We shouldn't even be registered at this point but just in case

        if (isguardian(owner))
        {
            mob_living_basic_guardian guardian_owner = (mob_living_basic_guardian)owner;
            if (!Dm.truthy(guardian_owner.is_deployed()))
            {
                stop_gas();
                return;
            }
        }

        datum_gas_mixture mix_to_spawn = Dm.@new<datum_gas_mixture>();
        mix_to_spawn.add_gas(active_gas);
        mix_to_spawn.moles[active_gas] = Dm.num(possible_gases[active_gas]) * Dm.num(seconds_per_tick);
        mix_to_spawn.temperature = T20C;
        turf_open our_turf = get_turf(owner) as turf_open;
        Waiting.NoSleep(our_turf.assume_air(mix_to_spawn));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/gaseous.dm:85  var/static/list/possible_gases
    internal DmList datum_action_cooldown_mob_cooldown_expel_gas__possible_gases;
}
