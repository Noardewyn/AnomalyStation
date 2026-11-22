// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/rot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): a rotting corpse or dried gibs give a random advanced disease to whoever touches, bumps or
// steps on them once the rot has set in; formaldehyde, cryostylane, the cold and husking stop the rot, a revival ends it. The var
// block is generated (D111).

// tg: code/datums/components/rot.dm:6
public partial class datum_component_rot : datum_component
{
    private const float REAGENT_BLOCKER = 1 << 0;
    private const float TEMPERATURE_BLOCKER = 1 << 1;
    private const float HUSK_BLOCKER = 1 << 2;

    // tg: code/datums/components/rot.dm:20  var/static/list/loc_connections
    public static DmList loc_connections => TgState.Current.datum_component_rot__loc_connections;

    // tg: code/datums/components/rot.dm:25
    [DmParams("delay", "scaling", "severity")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object delay = Dm.arg(__extra, 0);
        object scaling = Dm.arg(__extra, 1);
        object severity = Dm.arg(__extra, 2);
        object dot = await base.Initialize(Dm.forward(__extra, delay, scaling, severity));
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;
        if (isliving(parent))
        {
            mob_living living_parent = (mob_living)parent;
            //I think this can break in cases where someone becomes a robot post death, but I uh, I don't know how to account for that
            if (!Dm.truthy(Dm.bitand(living_parent.mob_biotypes, Dm.bitor(MOB_ORGANIC, MOB_UNDEAD))))
                return COMPONENT_INCOMPATIBLE;
        }

        start_delay = Dm.num(delay);
        scaling_delay = Dm.num(scaling);
        strength = Dm.num(severity);

        RegisterSignals(parent, Dm.list(COMSIG_ATOM_HULK_ATTACK, COMSIG_ATOM_ATTACK_ANIMAL, COMSIG_ATOM_ATTACK_HAND), nameof(rot_react_touch));
        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(rot_hit_react));
        if (ismovable(parent))
        {
            await AddComponent(typeof(datum_component_connect_loc_behalf), parent, loc_connections);
            RegisterSignal(parent, COMSIG_MOVABLE_BUMP, nameof(rot_react));
        }
        if (isliving(parent))
        {
            RegisterSignal(parent, COMSIG_LIVING_REVIVE, nameof(react_to_revive)); //mobs stop this when they come to life
            RegisterSignal(parent, COMSIG_LIVING_GET_PULLED, nameof(rot_react_touch));
        }
        if (iscarbon(parent))
        {
            mob_living_carbon carbon_parent = (mob_living_carbon)parent;
            RegisterSignal(carbon_parent.reagents as datum, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(check_reagent));
            RegisterSignals(parent, Dm.list(SIGNAL_ADDTRAIT(TRAIT_HUSK), SIGNAL_REMOVETRAIT(TRAIT_HUSK)), nameof(check_husk_trait));
            check_reagent(carbon_parent.reagents as datum_reagents);
            check_husk_trait();
        }
        if (ishuman(parent))
        {
            mob_living_carbon_human human_parent = (mob_living_carbon_human)parent;
            RegisterSignal(parent, COMSIG_HUMAN_CORETEMP_CHANGE, nameof(check_for_temperature));
            check_for_temperature(null, 0, human_parent.coretemperature);
        }

        start_up(NONE); //If nothing's blocking it, start
        return dot;
    }

    // tg: code/datums/components/rot.dm:61
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        if (ismovable(parent))
            qdel(GetComponent(typeof(datum_component_connect_loc_behalf)));
    }

    ///One of two procs that modifies blockers, this one handles removing a blocker and potentially restarting the rot
    // tg: code/datums/components/rot.dm:67
    public virtual void start_up(object blocker_type = null)
    {
        Dm.mask(ref blockers, Dm.bitnot(blocker_type)); //Yeet the type
        if (Dm.truthy(blockers) || Dm.truthy(active))  //If it's not empty
            return;
        start_time = world.time;
        active = TRUE;
    }

    ///One of two procs that modifies blockers, this one handles adding a blocker and potentially ending the rot
    // tg: code/datums/components/rot.dm:75
    public virtual void rest(object blocker_type = null)
    {
        float old_blockers = blockers;
        Dm.combine(ref blockers, blocker_type);
        if (Dm.truthy(old_blockers) || !Dm.truthy(active)) //If it had anything before this
            return;
        start_delay = max((Dm.num(start_time) + start_delay) - world.time, 0); //Account for the time spent rotting
        active = FALSE;
    }

    // tg: code/datums/components/rot.dm:84
    [SignalHandler]
    public virtual void react_to_revive()
    {
        // SIGNAL_HANDLER
        qdel(this);
    }

    // tg: code/datums/components/rot.dm:88
    [SignalHandler]
    public virtual void check_reagent(datum_reagents source = null)
    {
        // SIGNAL_HANDLER

        if (Dm.truthy(source.has_reagent(typeof(datum_reagent_toxin_formaldehyde), 15)) || Dm.truthy(source.has_reagent(typeof(datum_reagent_cryostylane))))
        {
            rest(REAGENT_BLOCKER);
            return;
        }
        start_up(REAGENT_BLOCKER);
    }

    // tg: code/datums/components/rot.dm:96
    [SignalHandler]
    public virtual void check_for_temperature(datum source = null, object old_temp = null, object new_temp = null)
    {
        // SIGNAL_HANDLER
        if (Dm.num(new_temp) <= T0C - 10)
        {
            rest(TEMPERATURE_BLOCKER);
            return;
        }
        start_up(TEMPERATURE_BLOCKER);
    }

    // tg: code/datums/components/rot.dm:103
    [SignalHandler]
    public virtual void check_husk_trait()
    {
        // SIGNAL_HANDLER
        if (HAS_TRAIT(parent, TRAIT_HUSK))
        {
            rest(HUSK_BLOCKER);
            return;
        }
        start_up(HUSK_BLOCKER);
    }

    // tg: code/datums/components/rot.dm:110
    [SignalHandler]
    public virtual void rot_hit_react(datum source = null, obj_item hit_with = null, mob_living attacker = null, object @params = null)
    {
        // SIGNAL_HANDLER
        rot_react_touch(source, attacker);
    }

    // tg: code/datums/components/rot.dm:114
    [SignalHandler]
    public virtual void rot_react_touch(datum source = null, mob_living react_to = null)
    {
        // SIGNAL_HANDLER
        rot_react(source, react_to, pick(GLOB.arm_zones));
    }

    /// Triggered when something enters the component's parent.
    // tg: code/datums/components/rot.dm:119
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        // SIGNAL_HANDLER
        rot_react(source, arrived as mob_living);
    }

    ///The main bit of logic for the rot component, does a temperature check and has a chance to infect react_to
    // tg: code/datums/components/rot.dm:124
    [SignalHandler]
    public virtual void rot_react(object source = null, mob_living react_to = null, object target_zone = null)
    {
        // SIGNAL_HANDLER
        if (!isliving(react_to))
            return;

        // Don't infect if you're chilled (I'd like to link this with the signals, but I can't come up with a good way to pull it off)
        atom atom_parent = (atom)parent;
        datum_gas_mixture our_mix = Waiting.NoSleep(atom_parent.return_air()) as datum_gas_mixture;
        if (Dm.num(our_mix?.temperature) <= T0C - 10)
            return;

        if (!Dm.truthy(active))
            return;

        float time_delta = world.time - Dm.num(start_time);
        // Wait a bit before decaying
        if (time_delta < start_delay)
            return;

        float time_scaling = min((time_delta - start_delay) / scaling_delay, 1);

        if (!prob(Dm.num(strength) * 1 * time_scaling))
            return;

        //We're running just under the "worst disease", since we don't want these to be too strong
        datum_disease_advance_random rand_disease = Dm.@new<datum_disease_advance_random>(rand(4 * Dm.num(strength) * time_scaling), rand(Dm.num(strength) * 5 * time_scaling));
        rand_disease.name = "Unknown";
        Waiting.NoSleep(react_to.ContactContractDisease(rand_disease, target_zone));
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/rot.dm:20  /datum/component/rot's var/static/list/loc_connections
    internal DmList datum_component_rot__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(datum_component_rot.on_entered)));
}
