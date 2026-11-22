// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/pet_bonus.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/**
 * # Pet bonus element!
 *
 * Bespoke element that plays a fun message, sends a heart out, and gives a stronger mood bonus when you pet this animal.
 * I may have been able to make this work for carbons, but it would have been interjecting on some help mode interactions anyways.
 */
// tg: code/datums/elements/pet_bonus.dm:8
public partial class datum_element_pet_bonus : datum_element
{
    // tg: code/datums/elements/pet_bonus.dm:16
    [DmParams("target", "emote_name", "moodlet")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object emote_name = Dm.arg(__extra, 0);
        Type moodlet = Dm.arg<Type>(__extra, 1) ?? typeof(datum_mood_event_pet_animal);
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;

        this.emote_name = emote_name;
        this.moodlet = moodlet;
        RegisterSignal(target, COMSIG_ATOM_ATTACK_HAND, nameof(on_attack_hand));
        return dot;
    }

    // tg: code/datums/elements/pet_bonus.dm:25
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, COMSIG_ATOM_ATTACK_HAND);
        return dot;
    }

    // tg: code/datums/elements/pet_bonus.dm:29
    [SignalHandler]
    public virtual void on_attack_hand(mob_living pet = null, mob_living petter = null, DmList modifiers = null)
    {
        if (IS_UNCONSCIOUS(pet) || Dm.truthy(petter.combat_mode) || Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            return;

        Dm.@new<obj_effect_temp_visual_heart>(get_turf(pet));
        if (Dm.truthy(pet._listen_lookup?[COMSIG_ANIMAL_PET]))
            pet._SendSignal(COMSIG_ANIMAL_PET, Dm.list(pet, petter, modifiers));
        if (Dm.truthy(emote_name) && prob(33))
            Waiting.Detach(pet.emote(emote_name));
        Waiting.Detach(petter.add_mood_event("petting_bonus", moodlet, pet));
    }
}
