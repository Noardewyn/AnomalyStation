// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/olfaction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): transcendent olfaction, remembering a scent from fingerprints and following its trail. The var blocks are generated (D111).

// tg: code/datums/mutations/olfaction.dm:1
public partial class datum_mutation_olfaction : datum_mutation
{
    // tg: code/datums/mutations/olfaction.dm:12
    public override async DmTask<object> setup()
    {
        object dot = await base.setup();
        if (dot is not datum_action_cooldown_spell_olfaction to_modify) // null or invalid
            return dot;

        to_modify.sensitivity = GET_MUTATION_SYNCHRONIZER(this);
        return dot;
    }
}

// tg: code/datums/mutations/olfaction.dm:20
public partial class datum_action_cooldown_spell_olfaction : datum_action_cooldown_spell
{
    // tg: code/datums/mutations/olfaction.dm:34
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        await DmTask.CompletedTask;
        if (!isliving(cast_on))
            return FALSE;

        mob_living living_cast_on = cast_on as mob_living;
        if (ishuman(living_cast_on) && living_cast_on.get_bodypart(BODY_ZONE_HEAD) == null)
        {
            to_chat(owner, span_warning(I18n.T("You have no nose!")));
            return FALSE;
        }

        if (HAS_TRAIT(living_cast_on, TRAIT_ANOSMIA)) //Anosmia quirk holders can't smell anything
        {
            to_chat(owner, span_warning(I18n.T("You can't smell!")));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/datums/mutations/olfaction.dm:49
    public override async DmTask<object> cast(atom target = null)
    {
        mob_living cast_on = target as mob_living;
        object dot = await base.cast(target);
        // Can we sniff? is there miasma in the air?
        datum_gas_mixture air = await cast_on.loc.return_air() as datum_gas_mixture;

        if (Dm.truthy(air.moles[typeof(datum_gas_miasma)]))
        {
            cast_on.adjust_disgust(sensitivity * 45);
            to_chat(cast_on, span_warning(I18n.T("With your overly sensitive nose, you get a whiff of stench and feel sick! Try moving to a cleaner area!")));
            return dot;
        }

        atom sniffed = cast_on.get_active_held_item() as atom;
        if (sniffed != null)
            await pick_up_target(cast_on, sniffed);
        else
            follow_target(cast_on);
        return dot;
    }

    /// Attempt to pick up a new target based on the fingerprints on [sniffed].
    // tg: code/datums/mutations/olfaction.dm:67
    public virtual async DmTask pick_up_target(mob_living caster = null, atom sniffed = null)
    {
        mob_living_carbon old_target = tracking_ref?.resolve() as mob_living_carbon;
        DmList possibles = Dm.list();
        DmList prints = GET_ATOM_FINGERPRINTS(sniffed);
        if (prints != null)
        {
            foreach (mob_living_carbon to_check in Dm.each_cast<mob_living_carbon>(GLOB.carbon_list))
            {
                if (Dm.truthy(prints[md5(to_check.dna?.unique_identity)]))
                    Dm.combine(ref possibles, to_check);
            }
        }

        // There are no finger prints on the atom, so nothing to track
        if (!Dm.truthy(length(possibles)))
        {
            to_chat(caster, span_warning(I18n.T("Despite your best efforts, there are no scents to be found on []...", sniffed)));
            return;
        }

        mob_living_carbon new_target = await tgui_input_list(caster, I18n.T("Scent to remember"), I18n.T("Scent Tracking"), sort_names(possibles) as DmList) as mob_living_carbon;
        if (QDELETED(this) || QDELETED(caster))
            return;

        if (QDELETED(new_target))
        {
            // We don't have a new target OR an old target
            if (QDELETED(old_target))
            {
                to_chat(caster, span_warning(I18n.T("You decide against remembering any scents. Instead, you notice your own nose in your peripheral vision. This goes on to remind you of that one time you started breathing manually and couldn't stop. What an awful day that was.")));
                tracking_ref = null;
            }
            // We don't have a new target, but we have an old target to fall back on
            else
            {
                to_chat(caster, span_notice(I18n.T("You return to tracking []. The hunt continues.", old_target)));
                on_the_trail(caster);
            }
            return;
        }

        // We have a new target to track
        to_chat(caster, span_notice(I18n.T("You pick up the scent of []. The hunt begins.", new_target)));
        tracking_ref = WEAKREF(new_target) as datum_weakref;
        on_the_trail(caster);
    }

    /// Attempt to follow our current tracking target.
    // tg: code/datums/mutations/olfaction.dm:106
    public virtual void follow_target(mob_living caster = null)
    {
        mob_living_carbon current_target = tracking_ref?.resolve() as mob_living_carbon;
        // Either our weakref failed to resolve (our target's gone),
        // or we never had a target in the first place
        if (QDELETED(current_target))
        {
            to_chat(caster, span_warning(I18n.T("You're not holding anything to smell, and you haven't smelled anything you can track. You smell your skin instead; it's kinda salty.")));
            tracking_ref = null;
            return;
        }

        on_the_trail(caster);
    }

    /// Actually go through and give the user a hint of the direction our target is.
    // tg: code/datums/mutations/olfaction.dm:119
    public virtual void on_the_trail(mob_living caster = null)
    {
        mob_living_carbon current_target = tracking_ref?.resolve() as mob_living_carbon;
        //Using get_turf to deal with those pesky closets that put your x y z to 0
        turf current_target_turf = get_turf(current_target);
        turf caster_turf = get_turf(caster);
        if (current_target == null)
        {
            to_chat(caster, span_warning(I18n.T("You're not tracking a scent, but the game thought you were. Something's gone wrong! Report this as a bug.")));
            stack_trace(Dm.text("[] - on_the_trail was called when no tracking target was set.", type), "code/datums/mutations/olfaction.dm", 128);
            tracking_ref = null;
            return;
        }

        if (current_target == caster)
        {
            to_chat(caster, span_warning(I18n.T("You smell out the trail to yourself. Yep, it's you.")));
            return;
        }

        if (caster_turf.z < current_target_turf.z)
        {
            to_chat(caster, span_warning(I18n.T("The trail leads... way up above you? Huh. They must be really, really far away.")));
            return;
        }
        else if (caster_turf.z > current_target_turf.z)
        {
            to_chat(caster, span_warning(I18n.T("The trail leads... way down below you? Huh. They must be really, really far away.")));
            return;
        }

        string direction_text = span_bold(Dm.text("[]", dir2text(get_dir(caster_turf, current_target_turf))));
        if (Dm.truthy(direction_text))
            to_chat(caster, span_notice(I18n.T("You consider []'s scent. The trail leads [].", current_target, direction_text)));
    }
}
