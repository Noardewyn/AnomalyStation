// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/lesserform.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Lesser Form, the changeling's monkey form and the way back to a stored profile, with its
// button's name and icon following the form. The var block is generated (D111).

// tg: code/modules/antagonists/changeling/powers/lesserform.dm:1
public partial class datum_action_changeling_lesserform : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/lesserform.dm:12
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.Grant(granted_to, __extra);
        if (owner == null)
            return dot;
        RegisterSignals(granted_to, Dm.list(COMSIG_HUMAN_MONKEYIZE, COMSIG_MONKEY_HUMANIZE), nameof(changed_form));
        return dot;
    }

    // tg: code/modules/antagonists/changeling/powers/lesserform.dm:18
    public override async DmTask Remove(mob remove_from = null)
    {
        UnregisterSignal(remove_from, Dm.list(COMSIG_HUMAN_MONKEYIZE, COMSIG_MONKEY_HUMANIZE));
        await base.Remove(remove_from);
    }

    //Transform into a monkey.
    // tg: code/modules/antagonists/changeling/powers/lesserform.dm:23
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        if (user == null || HAS_TRAIT(user, TRAIT_NO_TRANSFORM))
            return FALSE;
        await base.sting_action(user, target);
        return ismonkey(user) ? await unmonkey(user as mob_living_carbon_human) : await become_monkey(user as mob_living_carbon_human);
    }

    /// Stop being a monkey
    // tg: code/modules/antagonists/changeling/powers/lesserform.dm:30
    public virtual async DmTask<object> unmonkey(mob_living_carbon_human user = null)
    {
        if (Dm.truthy(Dm.bitand(user.movement_type, VENTCRAWLING)))
        {
            user.balloon_alert(user, I18n.T("can't transform in pipes!"));
            return FALSE;
        }
        datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
        datum_changeling_profile chosen_form = await select_form(changeling, user) as datum_changeling_profile;
        if (chosen_form == null)
            return FALSE;
        to_chat(user, span_notice(I18n.T("We transform our appearance.")));
        datum_dna chosen_dna = chosen_form.dna;
        datum_species chosen_species = chosen_dna.species as datum_species;
        user.humanize(species: chosen_species, instant: transform_instantly);

        await changeling.transform(user, chosen_form);
        return TRUE;
    }

    /// Returns the form to transform back into, automatically selects your only profile if you only have one
    // tg: code/modules/antagonists/changeling/powers/lesserform.dm:47
    public virtual async DmTask<object> select_form(datum_antagonist_changeling changeling = null, mob_living_carbon_human user = null)
    {
        if (changeling == null)
            return null;
        if (length(changeling.stored_profiles) == 1)
            return changeling.first_profile;
        return changeling != null ? await changeling.select_dna() : null;
    }

    /// Become a monkey
    // tg: code/modules/antagonists/changeling/powers/lesserform.dm:55
    public virtual async DmTask<object> become_monkey(mob_living_carbon_human user = null)
    {
        to_chat(user, span_warning(I18n.T("Our genes cry out!")));
        user.monkeyize(instant: transform_instantly);
        return TRUE;
    }

    /// Called when you become a human or monkey, whether or not it was voluntary
    // tg: code/modules/antagonists/changeling/powers/lesserform.dm:61
    [SignalHandler]
    public virtual void changed_form()
    {
        Waiting.NoSleep(build_all_button_icons(update_flags: Dm.bitor(UPDATE_BUTTON_NAME, UPDATE_BUTTON_ICON)));
    }

    // tg: code/modules/antagonists/changeling/powers/lesserform.dm:65
    public override void update_button_name(atom_movable_screen_movable_action_button button = null, float? force = null, params object[] __extra)
    {
        if (ismonkey(owner))
        {
            name = I18n.T("Human Form");
            desc = I18n.T("We change back into a human. Costs 5 chemicals.");
        }
        else
        {
            name = Dm.initial(this, (datum_action_changeling_lesserform p) => p.name);
            desc = Dm.initial(this, (datum_action_changeling_lesserform p) => p.desc);
        }
        base.update_button_name(button, force, __extra);
    }

    // tg: code/modules/antagonists/changeling/powers/lesserform.dm:74
    public override void apply_button_icon(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        button_icon_state = ismonkey(owner) ? "human_form" : Dm.initial(this, (datum_action_changeling_lesserform p) => p.button_icon_state);
        base.apply_button_icon(current_button, force);
    }
}
