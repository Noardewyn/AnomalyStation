// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/speechmod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the mutations' and organs' hooks that wait with their hosts (they run as tg's when reached) (gaps package C4).

/// Used to apply certain speech patterns
/// Can be used on organs, wearables, mutations and mobs
// tg: code/datums/components/speechmod.dm:3
public partial class datum_component_speechmod : datum_component
{
    // tg: code/datums/components/speechmod.dm:20
    [DmParams("replacements", "end_string", "end_string_chance", "slots", "uppercase", "should_modify_speech")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!ismob(parent) && !isitem(parent) && parent is not datum_mutation)
            return COMPONENT_INCOMPATIBLE;

        this.replacements = Dm.arg(__extra, 0) as DmList ?? Dm.list();
        this.end_string = Dm.arg(__extra, 1) ?? "";
        this.end_string_chance = Dm.num(Dm.arg(__extra, 2) ?? 100f);
        this.slots = Dm.arg(__extra, 3);
        this.uppercase = Dm.num(Dm.arg(__extra, 4) ?? FALSE);
        this.should_modify_speech = Dm.arg(__extra, 5) as datum_callback;

        if (parent is datum_mutation)
        {
            RegisterSignal(parent, COMSIG_MUTATION_GAINED, nameof(on_mutation_gained));
            RegisterSignal(parent, COMSIG_MUTATION_LOST, nameof(on_mutation_lost));
            return null;
        }

        atom owner = parent as atom;

        if (parent is datum_status_effect effect)
        {
            targeted = effect.owner;
            RegisterSignal(targeted, COMSIG_MOB_SAY, nameof(handle_speech));
            return null;
        }

        if (ismob(parent))
        {
            targeted = parent as mob;
            RegisterSignal(targeted, COMSIG_MOB_SAY, nameof(handle_speech));
            return null;
        }

        if (ismob(owner.loc))
        {
            targeted = owner.loc as mob;
            RegisterSignal(targeted, COMSIG_MOB_SAY, nameof(handle_speech));
        }

        if (isorgan(parent))
        {
            RegisterSignal(parent, COMSIG_ORGAN_IMPLANTED, nameof(on_implanted));
            RegisterSignal(parent, COMSIG_ORGAN_REMOVED, nameof(on_removed));
        }
        else
        {
            RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_equipped));
            RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(on_unequipped));
        }
        return null;
    }

    // tg: code/datums/components/speechmod.dm:62
    [SignalHandler]
    public virtual object handle_speech(datum source = null, DmList speech_args = null)
    {
        string message = speech_args[SPEECH_MESSAGE] as string;
        if (Dm.eq(Dm.copytext_char(message, 1, 2), "*"))
            return null;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(source._listen_lookup?[COMSIG_TRY_MODIFY_SPEECH]) ? NONE : source._SendSignal(COMSIG_TRY_MODIFY_SPEECH, Dm.list(source))), PREVENT_MODIFY_SPEECH)))
            return null;
        if (should_modify_speech != null && !Dm.truthy(Waiting.NoSleep(should_modify_speech.Invoke(source, speech_args))))
            return null;

        foreach (object to_replace in Dm.each(replacements))
        {
            object replacement = replacements[to_replace];
            if (replacement is DmList replacement_list)
                replacement = pick(replacement_list);

            message = replacetextEx(message, to_replace, replacement) as string;
        }
        message = trim(message) as string;
        if (prob(Dm.num(end_string_chance)))
            message = Dm.text("[][]", message, end_string is DmList end_list ? pick(end_list) : end_string);
        speech_args[SPEECH_MESSAGE] = trim(message);

        if (Dm.truthy(uppercase))
            return COMPONENT_UPPERCASE_SPEECH;
        return null;
    }

    // tg: code/datums/components/speechmod.dm:86
    [SignalHandler]
    public virtual void on_equipped(datum source = null, mob_living user = null, object slot = null)
    {
        if (slots != null && !Dm.truthy(Dm.bitand(slot, slots)))
        {
            if (targeted != null)
            {
                UnregisterSignal(targeted, COMSIG_MOB_SAY);
                targeted = null;
            }
            return;
        }
        if (targeted == user)
            return;
        targeted = user;
        RegisterSignal(targeted, COMSIG_MOB_SAY, nameof(handle_speech));
    }

    // tg: code/datums/components/speechmod.dm:100
    [SignalHandler]
    public virtual void on_unequipped(datum source = null, mob_living user = null)
    {
        if (targeted == null)
            return;
        UnregisterSignal(targeted, COMSIG_MOB_SAY);
        targeted = null;
    }

    // tg: code/datums/components/speechmod.dm:108
    [SignalHandler]
    public virtual void on_implanted(datum source = null, mob_living_carbon receiver = null)
    {
        if (targeted == receiver)
            return;
        targeted = receiver;
        RegisterSignal(targeted, COMSIG_MOB_SAY, nameof(handle_speech));
    }

    // tg: code/datums/components/speechmod.dm:116
    [SignalHandler]
    public virtual void on_removed(datum source = null, mob_living_carbon former_owner = null)
    {
        if (targeted == null)
            return;
        UnregisterSignal(targeted, COMSIG_MOB_SAY);
        targeted = null;
    }

    // tg: code/datums/components/speechmod.dm:124
    [SignalHandler]
    public virtual void on_mutation_gained(datum source = null, mob_living_carbon_human owner = null)
    {
        if (targeted == owner)
            return;
        targeted = owner;
        RegisterSignal(targeted, COMSIG_MOB_SAY, nameof(handle_speech));
    }

    // tg: code/datums/components/speechmod.dm:132
    [SignalHandler]
    public virtual void on_mutation_lost(datum source = null, mob_living_carbon_human owner = null)
    {
        if (targeted == null)
            return;
        UnregisterSignal(targeted, COMSIG_MOB_SAY);
        targeted = null;
    }

    // tg: code/datums/components/speechmod.dm:140
    public override object Destroy(float? force = null)
    {
        should_modify_speech = null;
        return base.Destroy(force);
    }
}
