// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents/reaction_agents_reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5: the reaction chamber's buffers): the reaction agents that act when poured into another holder (the
// acidic and basic buffers' pH shift, the purity tester, the speed agent, the inversing buffer) and the prefactors. The var
// blocks are generated.

// tg: code/modules/reagents/chemistry/reagents/reaction_agents_reagents.dm:1
public partial class datum_reagent_reaction_agent : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/reaction_agents_reagents.dm:6
    public override async DmTask<object> intercept_reagents_transfer(datum_reagents target = null, object amount = null, object copy_only = null)
    {
        if (target == null)
            return FALSE;
        if (Dm.truthy(Dm.bitand(target.flags, NO_REACT)))
            return FALSE;
        if (Dm.truthy(target.has_reagent(typeof(datum_reagent_stabilizing_agent))))
            return FALSE;
        if (!Dm.truthy(target.total_volume))
            return FALSE;
        if (Dm.num(LAZYLEN(target.reagent_list)) == 1)
        {
            if (Dm.truthy(target.has_reagent(type))) //Allow dispensing into self
                return FALSE;
        }
        await DmTask.CompletedTask;
        return TRUE;
    }
}

// tg: code/modules/reagents/chemistry/reagents/reaction_agents_reagents.dm:21
public partial class datum_reagent_reaction_agent_acidic_buffer : datum_reagent_reaction_agent
{
    //Consumes self on addition and shifts ph
    // tg: code/modules/reagents/chemistry/reagents/reaction_agents_reagents.dm:31
    public override async DmTask<object> intercept_reagents_transfer(datum_reagents target = null, object amount = null, object copy_only = null)
    {
        object dot = await base.intercept_reagents_transfer(target, amount, copy_only);
        if (!Dm.truthy(dot))
            return dot;

        //do the ph change
        string message;
        if (target.ph <= ph)
            message = "The beaker froths as the buffer is added, to no effect.";
        else
        {
            message = "The beaker froths as the pH changes!";
            target.adjust_all_reagents_ph(-(Dm.num(amount) / target.total_volume) * BUFFER_IONIZING_STRENGTH);
            await target.update_total();
        }

        //give feedback & remove from holder because it's not transferred
        target.my_atom.audible_message(span_warning(I18n.T(message)));
        playsound(target.my_atom, Dm.resource("sound/effects/chemistry/bufferadd.ogg"), 50, TRUE);
        if (!Dm.truthy(copy_only))
        {
            volume = Dm.num(volume) - Dm.num(amount);
            await holder.update_total();
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/reaction_agents_reagents.dm:53
public partial class datum_reagent_reaction_agent_basic_buffer : datum_reagent_reaction_agent
{
    // tg: code/modules/reagents/chemistry/reagents/reaction_agents_reagents.dm:62
    public override async DmTask<object> intercept_reagents_transfer(datum_reagents target = null, object amount = null, object copy_only = null)
    {
        object dot = await base.intercept_reagents_transfer(target, amount, copy_only);
        if (!Dm.truthy(dot))
            return dot;

        //do the ph change
        string message;
        if (target.ph >= ph)
            message = "The beaker froths as the buffer is added, to no effect.";
        else
        {
            message = "The beaker froths as the pH changes!";
            target.adjust_all_reagents_ph((Dm.num(amount) / target.total_volume) * BUFFER_IONIZING_STRENGTH);
            await target.update_total();
        }

        //give feedback & remove from holder because it's not transferred
        target.my_atom.audible_message(span_warning(I18n.T(message)));
        playsound(target.my_atom, Dm.resource("sound/effects/chemistry/bufferadd.ogg"), 50, TRUE);
        if (!Dm.truthy(copy_only))
        {
            volume = Dm.num(volume) - Dm.num(amount);
            await holder.update_total();
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/reaction_agents_reagents.dm:96
public partial class datum_reagent_reaction_agent_purity_tester : datum_reagent_reaction_agent
{
    // tg: code/modules/reagents/chemistry/reagents/reaction_agents_reagents.dm:101
    public override async DmTask<object> intercept_reagents_transfer(datum_reagents target = null, object amount = null, object copy_only = null)
    {
        object dot = await base.intercept_reagents_transfer(target, amount, copy_only);
        if (!Dm.truthy(dot))
            return dot;
        float is_inverse = FALSE;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(target.reagent_list))
        {
            if (reagent.purity <= Dm.num(reagent.inverse_chem_val))
                is_inverse = TRUE;
        }
        if (Dm.truthy(is_inverse))
        {
            target.my_atom.audible_message(span_warning(I18n.T("The beaker bubbles violently as the reagent is added!")));
            playsound(target.my_atom, Dm.resource("sound/effects/chemistry/bufferadd.ogg"), 50, TRUE);
        }
        else
            target.my_atom.audible_message(span_warning(I18n.T("The added reagent doesn't seem to do much.")));
        if (!Dm.truthy(copy_only))
        {
            volume = Dm.num(volume) - Dm.num(amount);
            await holder.update_total();
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/reaction_agents_reagents.dm:123
public partial class datum_reagent_reaction_agent_speed_agent : datum_reagent_reaction_agent
{
    ///How much the reaction speed is sped up by - for 5u added to 100u, an additional step of 1 will be done up to a max of 2x
    // tg: code/modules/reagents/chemistry/reagents/reaction_agents_reagents.dm:121  #define SPEED_REAGENT_STRENGTH 20 (file-local)
    private const float SPEED_REAGENT_STRENGTH = 20;

    // tg: code/modules/reagents/chemistry/reagents/reaction_agents_reagents.dm:128
    public override async DmTask<object> intercept_reagents_transfer(datum_reagents target = null, object amount = null, object copy_only = null)
    {
        object dot = await base.intercept_reagents_transfer(target, amount, copy_only);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!Dm.truthy(length(target.reaction_list))) //you can add this reagent to a beaker with no ongoing reactions, so this prevents it from being used up.
            return FALSE;
        amount = Dm.num(amount) / target.reaction_list.len;
        foreach (object _reaction in Dm.each(target.reaction_list))
        {
            datum_equilibrium reaction = _reaction as datum_equilibrium;
            if (reaction == null)
            {
                stack_trace(Dm.text("[] is in the reaction list, but is not an equilibrium", _reaction), "code/modules/reagents/chemistry/reagents/reaction_agents_reagents.dm", 139); // CRASH
                return null;
            }
            float power = (Dm.num(amount) / reaction.target_vol) * SPEED_REAGENT_STRENGTH;
            power *= creation_purity;
            power = Dm.clamp(power, 0, 2);
            await reaction.react_timestep(power, creation_purity);
        }
        if (!Dm.truthy(copy_only))
        {
            volume = Dm.num(volume) - Dm.num(amount);
            await holder.update_total();
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/reaction_agents_reagents.dm:150
public partial class datum_reagent_reaction_agent_inversing_buffer : datum_reagent_reaction_agent
{
    // tg: code/modules/reagents/chemistry/reagents/reaction_agents_reagents.dm:155
    public override async DmTask<object> intercept_reagents_transfer(datum_reagents target = null, object amount = null, object copy_only = null)
    {
        object dot = await base.intercept_reagents_transfer(target, amount, copy_only);
        if (!Dm.truthy(dot))
            return dot;

        float conversion_buffer = Dm.num(amount) * 10; //Converts up to 10 units of reagent per 1 unit of inversing buffer.
        float converted = 0;
        DmList cached_reagents = target.reagent_list.Copy();
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            if (!Dm.truthy(conversion_buffer))
                return dot;
            if (reagent.purity <= Dm.num(reagent.inverse_chem_val))
            {
                //compute volume of reagent to be converted
                converted = Dm.min(Dm.num(reagent.volume), conversion_buffer);
                //remove original reagent from target
                reagent.volume = Dm.num(reagent.volume) - converted;
                await target.update_total();
                //add new inverse reagent to target
                await target.add_reagent(reagent.inverse_chem, converted, null, added_purity: reagent.get_inverse_purity(reagent.purity)); // FALSE in the data slot (tg's positional FALSE)
                //remove from buffer remaining
                conversion_buffer -= converted;
            }
        }

        //audible feedback
        if (conversion_buffer < Dm.num(amount) * 10)
        {
            target.my_atom.audible_message(span_warning(I18n.T("The beaker goes into a rolling boil as the contents begin inversing!")));
            playsound(target.my_atom, Dm.resource("sound/effects/chemistry/catalyst.ogg"), 50, TRUE);
        }
        else
            target.my_atom.audible_message(span_warning(I18n.T("The buffer fizzles with no effect.")));

        //remove inversening reagent based on total buffer removed
        float volume_to_transfer = Dm.num(amount) - (Dm.num(amount) * (1 - (conversion_buffer / (Dm.num(amount) * 10))));
        if (Dm.truthy(volume_to_transfer))
            await target.add_reagent(type, volume_to_transfer, reagtemp: holder.chem_temp, added_purity: purity, added_ph: ph);
        if (!Dm.truthy(copy_only))
        {
            volume = Dm.num(volume) - Dm.num(amount);
            await holder.update_total();
        }
        return dot;
    }
}
