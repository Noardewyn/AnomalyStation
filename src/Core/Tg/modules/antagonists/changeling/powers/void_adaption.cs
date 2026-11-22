// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/void_adaption.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Void Adaption (the cold, pressure and breathing immunities, the chemical regeneration slowed
// while the environment would hurt). The var block is generated (D111); gain_traits is a TgState field (rules 10.6).

// tg: code/modules/antagonists/changeling/powers/void_adaption.dm:1
public partial class datum_action_changeling_void_adaption : datum_action_changeling
{
    /// Traits we apply to become immune to the environment
    // tg: code/modules/antagonists/changeling/powers/void_adaption.dm:13  var/static/list/gain_traits
    public static ref DmList gain_traits => ref TgState.Current.datum_action_changeling_void_adaption__gain_traits;

    // tg: code/modules/antagonists/changeling/powers/void_adaption.dm:19
    public override async DmTask on_purchase(mob user = null, object is_respec = null)
    {
        await base.on_purchase(user, is_respec);
        user.add_traits(gain_traits, REF(this));
        RegisterSignal(user, COMSIG_LIVING_LIFE, nameof(check_environment));
    }

    // tg: code/modules/antagonists/changeling/powers/void_adaption.dm:24
    public override async DmTask Remove(mob remove_from = null)
    {
        remove_from.remove_traits(gain_traits, REF(this));
        UnregisterSignal(remove_from, COMSIG_LIVING_LIFE);
        if (Dm.truthy(currently_active))
            on_removed_adaption(remove_from as mob_living, I18n.T("Our cells relax, despite the danger!"));
        await base.Remove(remove_from);
    }

    /// Checks if we would be providing any useful benefit at present
    // tg: code/modules/antagonists/changeling/powers/void_adaption.dm:32
    [SignalHandler]
    public virtual void check_environment(mob_living void_adapted = null)
    {
        DmList active_reasons = Dm.list();

        datum_gas_mixture environment = Waiting.NoSleep(void_adapted.loc.return_air()) as datum_gas_mixture;
        if (environment != null)
        {
            object vulnerable_temperature = void_adapted.get_body_temp_cold_damage_limit();
            object affected_temperature = environment.return_temperature();
            if (ishuman(void_adapted))
            {
                mob_living_carbon_human special_boy = void_adapted as mob_living_carbon_human;
                object cold_protection = special_boy.get_cold_protection(affected_temperature);
                vulnerable_temperature = Dm.mul(vulnerable_temperature, Dm.sub(1, cold_protection));

                object affected_pressure = special_boy.calculate_affecting_pressure(environment.return_pressure());
                if (Dm.lt(affected_pressure, HAZARD_LOW_PRESSURE))
                    Dm.append(ref active_reasons, "vacuum");
            }

            if (Dm.lt(affected_temperature, vulnerable_temperature))
                Dm.append(ref active_reasons, "cold");
        }

        float should_be_active = Dm.not(Dm.not(length(active_reasons)));
        if (currently_active == should_be_active)
            return;

        if (!Dm.truthy(should_be_active))
        {
            on_removed_adaption(void_adapted, I18n.T("Our cells relax in safer air."));
            return;
        }
        datum_antagonist_changeling changeling_data = IS_CHANGELING(void_adapted) as datum_antagonist_changeling;
        to_chat(void_adapted, span_changeling(I18n.T("Our cells harden themselves against the [].", pick(active_reasons))));
        changeling_data?.chem_recharge_slowdown -= recharge_slowdown;
        currently_active = TRUE;
    }

    /// Called when we stop being adapted
    // tg: code/modules/antagonists/changeling/powers/void_adaption.dm:66
    public virtual void on_removed_adaption(mob_living former = null, object message = null)
    {
        datum_antagonist_changeling changeling_data = IS_CHANGELING(former) as datum_antagonist_changeling;
        to_chat(former, span_changeling(Dm.str(message)));
        changeling_data?.chem_recharge_slowdown += recharge_slowdown;
        currently_active = FALSE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/antagonists/changeling/powers/void_adaption.dm:13  var/static/list/gain_traits
    internal DmList datum_action_changeling_void_adaption__gain_traits = Dm.list(TRAIT_NO_BREATHLESS_DAMAGE, TRAIT_RESISTCOLD, TRAIT_RESISTLOWPRESSURE, TRAIT_SNOWSTORM_IMMUNE);
}
