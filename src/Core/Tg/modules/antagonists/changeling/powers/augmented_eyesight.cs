// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/augmented_eyesight.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Augmented Eyesight (x-ray vision or flash protection). The var block is generated (D111).

//Augmented Eyesight: Gives you X-ray vision or protection from flashes. Also, high DNA cost because of how powerful it is.
//Possible todo: make a custom message for directing a penlight/flashlight at the eyes - not sure what would display though.

// tg: code/modules/antagonists/changeling/powers/augmented_eyesight.dm:4
public partial class datum_action_changeling_augmented_eyesight : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/augmented_eyesight.dm:18
    [DmParams("user")]
    public override async DmTask on_purchase(mob user = null, object is_respec = null) //The ability starts inactive, so we should be protected from flashes.
    {
        await base.on_purchase(user, is_respec);
        obj_item_organ_eyes ling_eyes = user.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        RegisterSignal(user, COMSIG_CARBON_GAIN_ORGAN, nameof(eye_implanted));
        RegisterSignal(user, COMSIG_CARBON_LOSE_ORGAN, nameof(eye_removed));
        if (ling_eyes != null)
        {
            ling_eyes.flash_protect = FLASH_PROTECTION_WELDER; //Adjust the user's eyes' flash protection
            to_chat(user, span_changeling(I18n.T("We adjust our eyes to protect them from bright lights.")));
        }
    }

    // tg: code/modules/antagonists/changeling/powers/augmented_eyesight.dm:28
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        if (user is not mob_living_carbon)
            return FALSE;

        obj_item_organ_eyes ling_eyes = user.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (ling_eyes == null)
        {
            user.balloon_alert(user, I18n.T("no eyes!"));
            return FALSE;
        }

        await base.sting_action(user, target);

        if (Dm.truthy(active))
        {
            active = FALSE;
            REMOVE_TRAIT(user, TRAIT_XRAY_VISION, REF(this));
            ling_eyes.flash_protect = max(ling_eyes.flash_protect += 3, FLASH_PROTECTION_WELDER);
            to_chat(user, span_changeling(I18n.T("We adjust our eyes to protect them from bright lights.")));
        }
        else
        {
            active = TRUE;
            ADD_TRAIT(user, TRAIT_XRAY_VISION, REF(this));
            ling_eyes.flash_protect = max(ling_eyes.flash_protect += -3, FLASH_PROTECTION_HYPER_SENSITIVE);
            to_chat(user, span_changeling(I18n.T("We adjust our eyes to sense prey through walls.")));
        }

        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/powers/augmented_eyesight.dm:51
    public override async DmTask Remove(mob user = null)
    {
        obj_item_organ_eyes ling_eyes = user.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (ling_eyes != null)
            ling_eyes.flash_protect = Dm.initial(ling_eyes, (obj_item_organ_eyes p) => p.flash_protect);

        REMOVE_TRAIT(user, TRAIT_XRAY_VISION, REF(this));

        UnregisterSignal(user, Dm.list(COMSIG_CARBON_GAIN_ORGAN, COMSIG_CARBON_LOSE_ORGAN));
        await base.Remove(user);
    }

    /// Signal proc to grant the correct level of flash sensitivity
    // tg: code/modules/antagonists/changeling/powers/augmented_eyesight.dm:63
    [SignalHandler]
    public virtual void eye_implanted(mob_living source = null, obj_item_organ gained = null, object special = null)
    {
        obj_item_organ_eyes ling_eyes = gained as obj_item_organ_eyes;
        if (ling_eyes is not obj_item_organ_eyes)
            return;
        if (Dm.truthy(active))
            ling_eyes.flash_protect = max(ling_eyes.flash_protect += -3, FLASH_PROTECTION_HYPER_SENSITIVE);
        else
            ling_eyes.flash_protect = max(ling_eyes.flash_protect += 3, FLASH_PROTECTION_WELDER);
    }

    /// Signal proc to remove flash sensitivity when the eyes are removed
    // tg: code/modules/antagonists/changeling/powers/augmented_eyesight.dm:75
    [SignalHandler]
    public virtual void eye_removed(mob_living source = null, obj_item_organ removed = null, object special = null)
    {
        obj_item_organ_eyes ling_eyes = removed as obj_item_organ_eyes;
        if (ling_eyes is not obj_item_organ_eyes)
            return;
        ling_eyes.flash_protect = Dm.initial(ling_eyes, (obj_item_organ_eyes p) => p.flash_protect);
        // We don't need to bother about removing or adding x-ray vision, fortunately, because they can't see anyways
    }
}
