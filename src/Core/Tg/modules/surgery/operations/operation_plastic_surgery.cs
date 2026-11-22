// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_plastic_surgery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// tg: code/modules/surgery/operations/operation_plastic_surgery.dm:3
public partial class datum_surgery_operation_limb_plastic_surgery : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_plastic_surgery.dm:1  #define OPERATION_NEW_NAME "chosen_name"
    private const string OPERATION_NEW_NAME = "chosen_name";

    // tg: code/modules/surgery/operations/operation_plastic_surgery.dm:18
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("operate on head (target head)")), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_plastic_surgery.dm:21
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_scalpel));
    }

    // tg: code/modules/surgery/operations/operation_plastic_surgery.dm:24
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        return Dm.num(Dm.eq(limb.body_zone, BODY_ZONE_HEAD));
    }

    // tg: code/modules/surgery/operations/operation_plastic_surgery.dm:27
    public override async DmTask<object> pre_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        if (HAS_TRAIT_FROM(limb, TRAIT_DISFIGURED, TRAIT_GENERIC))
            return TRUE; //skip name selection if fixing disfigurement

        DmList names = Dm.list();
        if (HAS_MIND_TRAIT(surgeon, TRAIT_ABDUCTOR_KNOWLEDGE))
        {
            for (float j = 1, j__end = 9; j <= j__end; j++)
                Dm.append(ref names, Dm.text("Subject []-[]-[]", Dm.eq(limb.owner.gender, MALE) ? "i" : "o", pick("a", "b", "c", "d", "e"), rand(10000, 99999)));

            if (limb.owner != null)
                Dm.append(ref names, limb.owner.generate_random_mob_name(TRUE)); //give one normal name in case they want to do regular plastic surgery
            else
                Dm.append(ref names, generate_random_name_species_based(pick(MALE, FEMALE), TRUE, GLOB.species_list[limb.limb_id] is var sp1 && Dm.truthy(sp1) ? sp1 : typeof(datum_species_human)));
        }
        else
        {
            bool advanced = LIMB_HAS_SURGERY_STATE(limb, SURGERY_PLASTIC_APPLIED);
            obj_item offhand = surgeon.get_inactive_held_item() as obj_item;
            if (istype(offhand, typeof(obj_item_photo)) && advanced)
            {
                obj_item_photo disguises = (obj_item_photo)offhand;
                foreach (object namelist in Dm.each(disguises.picture?.names_seen))
                    Dm.append(ref names, namelist);
            }
            else
            {
                if (advanced)
                    to_chat(surgeon, span_warning(I18n.T("You have no picture to base the appearance on!")));

                for (float i = 1, i__end = 10; i <= i__end; i++)
                {
                    if (limb.owner != null)
                        Dm.append(ref names, limb.owner.generate_random_mob_name(TRUE));
                    else
                        Dm.append(ref names, generate_random_name_species_based(pick(MALE, FEMALE), TRUE, GLOB.species_list[limb.limb_id] is var sp2 && Dm.truthy(sp2) ? sp2 : typeof(datum_species_human)));
                }
            }
        }

        operation_args[OPERATION_NEW_NAME] = await tgui_input_list(surgeon, I18n.T("New name to assign"), I18n.T("Plastic Surgery"), names);
        return Dm.num(Dm.truthy(operation_args[OPERATION_NEW_NAME]));
    }

    // tg: code/modules/surgery/operations/operation_plastic_surgery.dm:63
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to alter []'s appearance...", (object)limb.owner ?? limb)),
            span_notice(I18n.T("[] begins to alter []'s appearance.", surgeon, (object)limb.owner ?? limb)),
            span_notice(I18n.T("[] begins to make an incision in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel a slicing pain across your face!"));
    }

    // tg: code/modules/surgery/operations/operation_plastic_surgery.dm:73
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart_head limb = operating_on as obj_item_bodypart_head;
        if (!istype(limb, typeof(obj_item_bodypart_head)))
            throw Dm.CRASH(Dm.text("Plastic surgery finished on a non-head limb []!", operating_on), "code/modules/surgery/operations/operation_plastic_surgery.dm", 75);

        if (HAS_TRAIT_FROM(limb, TRAIT_DISFIGURED, TRAIT_GENERIC))
        {
            REMOVE_TRAIT(limb, TRAIT_DISFIGURED, TRAIT_GENERIC);
            display_results(
                surgeon,
                limb.owner,
                span_notice(I18n.T("You successfully restore []'s appearance.", (object)limb.owner ?? limb)),
                span_notice(I18n.T("[] successfully restores []'s appearance!", surgeon, (object)limb.owner ?? limb)),
                span_notice(I18n.T("[] finishes the operation on []", surgeon, limb.owner != null ? I18n.T("[]'s face.", limb.owner) : limb)));
            await display_pain(limb.owner, I18n.T("The pain fades, your face feels normal again!"));
            return null;
        }

        object oldname = limb.owner?.real_name is var rn && Dm.truthy(rn) ? rn : limb.real_name;
        if (limb.owner != null)
            limb.owner.real_name = operation_args[OPERATION_NEW_NAME];
        else
            limb.real_name = Dm.text("[]", operation_args[OPERATION_NEW_NAME]);

        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You alter []'s appearance completely, [] [] now [].", oldname, limb.owner.p_they(), limb.owner.p_are(), operation_args[OPERATION_NEW_NAME])),
            span_notice(I18n.T("[] alters []'s appearance completely, [] [] now []!", surgeon, oldname, limb.owner.p_they(), limb.owner.p_are(), operation_args[OPERATION_NEW_NAME])),
            span_notice(I18n.T("[] finishes the operation on [].", surgeon, limb.owner != null ? I18n.T("[]'s face.", limb.owner) : limb)));
        await display_pain(limb.owner, I18n.T("The pain fades, your face feels new and unfamiliar!"));
        if (ishuman(limb.owner))
        {
            mob_living_carbon_human human_target = (mob_living_carbon_human)limb.owner;
            human_target.update_ID_card();
        }
        if (HAS_MIND_TRAIT(surgeon, TRAIT_MORBID))
            await surgeon.add_mood_event("morbid_abominable_surgery_success", typeof(datum_mood_event_morbid_abominable_surgery_success));
        await limb.remove_surgical_state(SURGERY_PLASTIC_APPLIED);
        return null;
    }

    // tg: code/modules/surgery/operations/operation_plastic_surgery.dm:109
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_warning(I18n.T("Your screw up, leaving []'s appearance disfigured!", (object)limb.owner ?? limb)),
            span_warning(I18n.T("[] screws up, disfiguring []'s appearance!", surgeon, (object)limb.owner ?? limb)),
            span_notice(I18n.T("[] finishes the operation on [].", surgeon, limb.owner != null ? I18n.T("[]'s face.", limb.owner) : limb)));
        await display_pain(limb.owner, I18n.T("Your face feels horribly scarred and deformed!"));
        ADD_TRAIT(limb, TRAIT_DISFIGURED, TRAIT_GENERIC);
    }
}

// tg: code/modules/surgery/operations/operation_plastic_surgery.dm:123
public partial class datum_surgery_operation_limb_add_plastic : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_plastic_surgery.dm:136
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_stack_sheet_plastic));
    }

    // tg: code/modules/surgery/operations/operation_plastic_surgery.dm:139
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        return Dm.num(Dm.eq(limb.body_zone, BODY_ZONE_HEAD));
    }

    // tg: code/modules/surgery/operations/operation_plastic_surgery.dm:142
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to apply plastic to []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to apply plastic to [].", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to perform surgery on [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel a strange sensation as something is applied to your face!"));
    }

    // tg: code/modules/surgery/operations/operation_plastic_surgery.dm:152
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        await limb.add_surgical_state(SURGERY_PLASTIC_APPLIED);
        return dot;
    }
}
