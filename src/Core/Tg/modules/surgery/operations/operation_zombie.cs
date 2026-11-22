// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_zombie.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated. The Romerol tumor it grows
// (/obj/item/organ/zombie_infection) stays a deferred leaf until the zombies (not-translated.md section 118); the operation, its
// reagent checks, messages and the reagents it uses up run.

// tg: code/modules/surgery/operations/operation_zombie.dm:1
public partial class datum_surgery_operation_limb_bionecrosis : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_zombie.dm:19
    public override async DmTask<object> get_default_radial_image()
    {
        return image(await get_dynamic_human_appearance(species_path: typeof(datum_species_zombie)));
    }

    // tg: code/modules/surgery/operations/operation_zombie.dm:22
    public override object all_required_strings()
    {
        object dot = base.all_required_strings();
        Dm.append(ref dot, I18n.T("the limb must have a brain present"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_zombie.dm:26
    public override object any_required_strings()
    {
        object dot = base.any_required_strings();
        foreach (Type chem in Dm.each_cast<Type>(zombie_chems))
            Dm.append(ref dot, I18n.T("the patient or tool must contain >1u []", Dm.initial(chem, (datum_reagent o) => o.name)));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_zombie.dm:31
    public override object all_blocked_strings()
    {
        object dot = base.all_blocked_strings();
        Dm.append(ref dot, I18n.T("the limb must not already have a Romerol tumor"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_zombie.dm:35
    public override object state_check(atom_movable operating_on = null)
    {
        if (Dm.truthy(Dm.locate<obj_item_organ_zombie_infection>(operating_on)))
            return FALSE;
        if (!Dm.truthy(Dm.locate<obj_item_organ_brain>(operating_on)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_zombie.dm:42
    public override object snowflake_check_availability(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, object operated_zone = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        foreach (object chem in Dm.each(zombie_chems))
        {
            if (Dm.num(((datum_reagents)(tool as atom)?.reagents)?.get_reagent_amount(chem)) > 1)
                return TRUE;
            if (Dm.num(((datum_reagents)limb.owner.reagents)?.get_reagent_amount(chem)) > 1)
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/surgery/operations/operation_zombie.dm:50
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to grow a romerol tumor in []'s brain...", limb.owner)),
            span_notice(I18n.T("[] begins to tinker with []'s brain...", surgeon, limb.owner)),
            span_notice(I18n.T("[] begins to perform surgery on []'s brain.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("Your head pounds with unimaginable pain!")); // Same message as other brain surgeries
    }

    // tg: code/modules/surgery/operations/operation_zombie.dm:60
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You succeed in growing a romerol tumor in []'s brain.", limb.owner)),
            span_notice(I18n.T("[] successfully grows a romerol tumor in []'s brain!", surgeon, limb.owner)),
            span_notice(I18n.T("[] completes the surgery on []'s brain.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("Your head goes totally numb for a moment, the pain is overwhelming!"));
        if (Dm.truthy(Dm.locate<obj_item_organ_zombie_infection>(limb))) // they got another one mid surgery? whatever
            return null;
        Dm.Deferred("code/modules/surgery/operations/operation_zombie.dm:71", "on_success: new /obj/item/organ/zombie_infection().Insert(limb.owner), the Romerol tumor (the zombies)");
        foreach (object chem in Dm.each(zombie_chems))
        {
            if ((tool as atom)?.reagents is datum_reagents tool_reagents)
                await tool_reagents.remove_reagent(chem, 1);
            if (limb.owner.reagents is datum_reagents owner_reagents)
                await owner_reagents.remove_reagent(chem, 1);
        }
        return null;
    }
}
