// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_organ_manip.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

/// Adding or removing specific organs
// tg: code/modules/surgery/operations/operation_organ_manip.dm:4
public partial class datum_surgery_operation_limb_organ_manipulation : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_organ_manip.dm:1  #define OPERATION_REMOVED_ORGAN "removed_organ"
    private const string OPERATION_REMOVED_ORGAN = "removed_organ";

    // tg: code/modules/surgery/operations/operation_organ_manip.dm:44
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        implements = (DmList)Dm.add(remove_implements, insert_implements);
    }

    // tg: code/modules/surgery/operations/operation_organ_manip.dm:48
    public override object get_recommended_tool()
    {
        return I18n.T("[] / organ", base.get_recommended_tool());
    }

    // tg: code/modules/surgery/operations/operation_organ_manip.dm:51
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/obj/medical/surgery_ui.dmi"), "surgery_any");
    }

    /// Checks that the passed organ can be inserted/removed
    // tg: code/modules/surgery/operations/operation_organ_manip.dm:55
    public virtual object organ_check(obj_item_bodypart limb = null, obj_item_organ organ = null)
    {
        return TRUE;
    }

    /// Checks that the passed organ can be inserted/removed in the specified zones
    // tg: code/modules/surgery/operations/operation_organ_manip.dm:59
    public virtual object zone_check(obj_item_organ organ = null, object limb_zone = null, object operated_zone = null)
    {
        // SHOULD_CALL_PARENT(TRUE)

        if (Dm.truthy(LAZYLEN(organ.valid_zones)))
        {
            // allows arm implants to be inserted into either arm
            if (!Dm.@in(limb_zone, organ.valid_zones))
                return FALSE;
            // but disallows arm implants from being inserted into the torso
            if (!Dm.@in(operated_zone, organ.valid_zones))
                return FALSE;
        }
        else
        {
            // allows appendixes to be inserted into chest
            if (!Dm.eq(limb_zone, deprecise_zone(organ.zone)))
                return FALSE;
            // but disallows appendixes from being inserted into the chest cavity
            if (!Dm.eq(operated_zone, organ.zone))
                return FALSE;
        }

        return TRUE;
    }

    /// Get a list of organs that can be removed from the limb in the specified zone
    // tg: code/modules/surgery/operations/operation_organ_manip.dm:79
    public virtual object get_removable_organs(obj_item_bodypart limb = null, object operated_zone = null)
    {
        DmList removable_organs = Dm.list();
        foreach (obj_item_organ organ in Dm.each<obj_item_organ>(limb))
        {
            if (!Dm.truthy(organ_check(limb, organ)) || Dm.truthy(Dm.bitand(organ.organ_flags, ORGAN_UNREMOVABLE)))
                continue;
            if (!Dm.truthy(zone_check(organ, limb.body_zone, operated_zone)))
                continue;
            Dm.append(ref removable_organs, organ);
        }

        return removable_organs;
    }

    /// Check if removing an organ is possible
    // tg: code/modules/surgery/operations/operation_organ_manip.dm:91
    public virtual object is_remove_available(obj_item_bodypart limb = null, object operated_zone = null)
    {
        return Dm.num(length(get_removable_organs(limb, operated_zone)) > 0);
    }

    /// Check if inserting an organ is possible
    // tg: code/modules/surgery/operations/operation_organ_manip.dm:95
    public virtual object is_insert_available(obj_item_bodypart limb = null, obj_item tool = null, object operated_zone = null)
    {
        // Handling the case  of arbitrary items as organs
        if (!isorgan(tool))
        {
            if (!Dm.truthy(LAZYLEN(random_item_organ_pool)))
                return FALSE;
            if (tool.w_class >= WEIGHT_CLASS_NORMAL)
                return FALSE;
            if (Dm.truthy(Dm.bitand(tool.item_flags, Dm.bitor(Dm.bitor(ABSTRACT, DROPDEL), HAND_ITEM))))
                return FALSE;
            if (!Dm.eq(limb.body_zone, deprecise_zone(operated_zone)))
                return FALSE;
            // Iterate over the organs we can have insert as dummy organs and check if any are applicable here
            DmList organ_pool = LAZYCOPY(random_item_organ_pool);
            foreach (Type option in Dm.each_cast<Type>(organ_pool))
            {
                if (!Dm.eq(Dm.initial(option, (obj_item_organ o) => o.zone), operated_zone) || Dm.truthy(Dm.locate_type(option, limb)))
                {
                    LAZYREMOVE(ref organ_pool, option);
                    continue;
                }
            }

            return Dm.num(LAZYLEN(organ_pool) > 0);
        }

        // Normal organ insertion handling
        obj_item_organ organ = (obj_item_organ)tool;
        if (!Dm.truthy(organ_check(limb, organ)) || Dm.truthy(Dm.bitand(organ.organ_flags, ORGAN_UNUSABLE)))
            return FALSE;

        foreach (obj_item_organ other_organ in Dm.each<obj_item_organ>(limb))
        {
            if (Dm.eq(other_organ.slot, organ.slot))
                return FALSE;
        }

        if (!Dm.truthy(zone_check(organ, limb.body_zone, operated_zone)))
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_organ_manip.dm:129
    public virtual object is_inserting(obj_item tool = null)
    {
        return is_type_in_list(tool, insert_implements);
    }

    // tg: code/modules/surgery/operations/operation_organ_manip.dm:132
    public override object snowflake_check_availability(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, object operated_zone = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        return Dm.truthy(is_inserting(tool as obj_item)) ? is_insert_available(limb, tool as obj_item, operated_zone) : is_remove_available(limb, operated_zone);
    }

    // tg: code/modules/surgery/operations/operation_organ_manip.dm:135
    public override async DmTask<object> get_radial_options(atom_movable operating_on = null, obj_item tool = null, object operating_zone = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        return Dm.truthy(is_inserting(tool)) ? get_insert_options(limb, tool) : get_remove_options(limb, operating_zone);
    }

    // tg: code/modules/surgery/operations/operation_organ_manip.dm:138
    public virtual object get_remove_options(obj_item_bodypart limb = null, object operating_zone = null)
    {
        DmList options = Dm.list();
        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(get_removable_organs(limb, operating_zone)))
        {
            // tg reads the type of a typepath here (actually_removing.type): a typepath's prototype is of its own type
            Type actually_removing = Dm.truthy(Dm.bitand(organ.organ_flags, ORGAN_FAKE)) ? ((datum)organ.contents[1]).type : organ.type;
            string remove_key = Dm.text("[]_remove", actually_removing);
            datum_radial_menu_choice option = LAZYACCESS(cached_organ_manipulation_options, remove_key) as datum_radial_menu_choice;
            if (option == null)
            {
                option = Dm.@new<datum_radial_menu_choice>();
                option.image = image(Dm.resource("icons/hud/surgery_radial.dmi"), "base");
                Dm.append(((image)option.image).overlays, add_radial_overlays(actually_removing));
                option.name = I18n.T("remove []", Dm.initial(actually_removing, (obj_item o) => o.name));
                option.info = I18n.T("Remove [] from the [].", Dm.initial(actually_removing, (obj_item o) => o.name), Dm.truthy(limb.owner) ? I18n.T("patient") : I18n.T("limb"));
                LAZYSET(ref cached_organ_manipulation_options, remove_key, option);
            }

            options[option] = Dm.list(Dm.kv(Dm.text("[]", OPERATION_ACTION), "remove"), Dm.kv(Dm.text("[]", OPERATION_REMOVED_ORGAN), organ));
        }

        return options;
    }

    // tg: code/modules/surgery/operations/operation_organ_manip.dm:156
    public virtual object get_insert_options(obj_item_bodypart limb = null, obj_item organ = null)
    {
        datum_radial_menu_choice option = LAZYACCESS(cached_organ_manipulation_options, Dm.text("[]_insert", organ.type)) as datum_radial_menu_choice;
        if (option == null)
        {
            option = Dm.@new<datum_radial_menu_choice>();
            option.image = image(Dm.resource("icons/hud/surgery_radial.dmi"), "base");
            Dm.append(((image)option.image).overlays, add_radial_overlays(Dm.list(image(Dm.resource("icons/hud/screen_gen.dmi"), "arrow_large_still"), organ.type)));
            option.name = I18n.T("insert []", Dm.initial(organ, (obj_item o) => o.name));
            option.info = I18n.T("insert [] into the [].", Dm.initial(organ, (obj_item o) => o.name), Dm.truthy(limb.owner) ? I18n.T("patient") : I18n.T("limb"));
            LAZYSET(ref cached_organ_manipulation_options, Dm.text("[]_insert", organ.type), option);
        }

        DmList result = Dm.list();
        result[option] = Dm.list(Dm.kv(Dm.text("[]", OPERATION_ACTION), "insert"));
        return result;
    }

    // tg: code/modules/surgery/operations/operation_organ_manip.dm:172
    public override object operate_check(mob_living patient = null, atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        if (!Dm.truthy(base.operate_check(patient, operating_on, surgeon, tool, operation_args)))
            return FALSE;

        object sw__1 = operation_args[OPERATION_ACTION];
        if (Dm.eq(sw__1, "remove"))
        {
            obj_item_organ organ = operation_args[OPERATION_REMOVED_ORGAN] as obj_item_organ;
            if (QDELETED(organ) || !Dm.@in(organ, limb))
                return FALSE;
        }
        else if (Dm.eq(sw__1, "insert"))
        {
            if (!Dm.truthy(is_insert_available(limb, tool as obj_item, operation_args[OPERATION_TARGET_ZONE])))
                return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_organ_manip.dm:188
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object sw__1 = operation_args[OPERATION_ACTION];
        if (Dm.eq(sw__1, "remove"))
        {
            obj_item_organ organ = operation_args[OPERATION_REMOVED_ORGAN] as obj_item_organ;
            play_operation_sound(limb, surgeon, tool, remove_preop_sound);
            display_results(
                surgeon,
                limb.owner,
                span_notice(I18n.T("You begin to remove [] from []...", organ, FORMAT_LIMB_OWNER(limb))),
                span_notice(I18n.T("[] begins to remove [] from [].", surgeon, organ, (object)limb.owner ?? limb)),
                span_notice(I18n.T("[] begins to remove something from [].", surgeon, (object)limb.owner ?? limb)));
            await display_pain(limb.owner, I18n.T("You feel a tugging sensation in your []!", limb.plaintext_zone));
        }
        else if (Dm.eq(sw__1, "insert"))
        {
            play_operation_sound(limb, surgeon, tool, insert_preop_sound);
            display_results(
                surgeon,
                limb.owner,
                span_notice(I18n.T("You begin to insert [] into []...", tool, FORMAT_LIMB_OWNER(limb))),
                span_notice(I18n.T("[] begins to insert [] into [].", surgeon, isorgan(tool) ? ((obj_item)tool).name : tool, (object)limb.owner ?? limb)),
                span_notice(I18n.T("[] begins to insert something into [].", surgeon, (object)limb.owner ?? limb)));
            await display_pain(limb.owner, I18n.T("You can feel something being placed in your []!", limb.plaintext_zone));
        }
    }

    // tg: code/modules/surgery/operations/operation_organ_manip.dm:210
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object sw__1 = operation_args[OPERATION_ACTION];
        if (Dm.eq(sw__1, "remove"))
        {
            play_operation_sound(limb, surgeon, tool, remove_success_sound);
            await on_success_remove_organ(limb, surgeon, operation_args[OPERATION_REMOVED_ORGAN] as obj_item_organ, tool as obj_item);
        }
        else if (Dm.eq(sw__1, "insert"))
        {
            play_operation_sound(limb, surgeon, tool, insert_success_sound);
            await on_success_insert_organ(limb, surgeon, operation_args[OPERATION_TARGET_ZONE], tool as obj_item);
        }
        if (HAS_MIND_TRAIT(surgeon, TRAIT_MORBID))
            await surgeon.add_mood_event("morbid_abominable_surgery_success", typeof(datum_mood_event_morbid_abominable_surgery_success));
        return null;
    }

    // tg: code/modules/surgery/operations/operation_organ_manip.dm:221
    public virtual async DmTask on_success_remove_organ(obj_item_bodypart limb = null, mob_living surgeon = null, obj_item_organ organ = null, obj_item tool = null)
    {
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You successfully extract [] from [].", organ, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] successfully extracts [] from []!", surgeon, organ, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] successfully extracts something from []!", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("Your [] throbs with pain, you can't feel your [] anymore!", limb.plaintext_zone, organ.name));
        log_combat(surgeon, (atom)limb.owner ?? limb, Dm.text("surgically removed [] from", organ.name));
        if (Dm.truthy(limb.owner))
            await organ.Remove(limb.owner);
        else
            await organ.bodypart_remove(limb);
        await organ.forceMove((limb.owner?.drop_location() is var or__1 && Dm.truthy(or__1) ? or__1 : limb.drop_location()) as atom);
        await organ.on_surgical_removal(surgeon, limb, tool);
    }

    // tg: code/modules/surgery/operations/operation_organ_manip.dm:238
    public virtual async DmTask on_success_insert_organ(obj_item_bodypart limb = null, mob_living surgeon = null, object operated_zone = null, obj_item tool = null)
    {
        obj_item_organ organ = null;
        if (isorgan(tool))
            organ = (obj_item_organ)tool;

        else
        {
            foreach (Type option in Dm.each_cast<Type>(shuffle(random_item_organ_pool)))
            {
                if (Dm.eq(Dm.initial(option, (obj_item_organ o) => o.zone), operated_zone) && !Dm.truthy(Dm.locate_type(option, limb)))
                {
                    organ = (obj_item_organ)Dm.@new(option);
                    await surgeon.temporarilyRemoveItemFromInventory(tool, TRUE);
                    await organ.AddComponent(typeof(datum_component_arbitrary_item_organ), tool);
                    break;
                }
            }
        }

        if (organ == null)
            throw Dm.CRASH(Dm.text("Somehow failed to find a valid organ to insert (inserting: [])", (object)tool ?? "null"), "code/modules/surgery/operations/operation_organ_manip.dm", 254);

        if (organ.loc == surgeon)
            await surgeon.temporarilyRemoveItemFromInventory(organ, TRUE);
        await organ.pre_surgical_insertion(surgeon, limb.owner, limb.body_zone);
        if (Dm.truthy(limb.owner))
            await organ.Insert(limb.owner);
        else
            await organ.bodypart_insert(limb);
        organ.on_surgical_insertion(surgeon, limb);
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You successfully insert [] into [].", tool, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] successfully inserts [] into [].", surgeon, tool, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] successfully inserts something into [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("Your [] throbs with pain as your new [] comes to life!", limb.plaintext_zone, organ.name));
    }
}

// tg: code/modules/surgery/operations/operation_organ_manip.dm:271
public partial class datum_surgery_operation_limb_organ_manipulation_internal : datum_surgery_operation_limb_organ_manipulation
{
    // tg: code/modules/surgery/operations/operation_organ_manip.dm:279
    public override object organ_check(obj_item_bodypart limb = null, obj_item_organ organ = null)
    {
        if (Dm.truthy(Dm.bitand(organ.organ_flags, ORGAN_EXTERNAL)))
            return FALSE;
        // chest organs and the brain require bone sawed
        if (Dm.eq(organ.zone, BODY_ZONE_CHEST) || Dm.eq(organ.slot, ORGAN_SLOT_BRAIN))
            return Dm.num(!Dm.truthy(LIMB_HAS_BONES(limb)) || LIMB_HAS_SURGERY_STATE(limb, SURGERY_BONE_SAWED));
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_organ_manip.dm:287
    public override object any_required_strings()
    {
        return Dm.add(base.any_required_strings(), Dm.list(
            I18n.T("if operating on [], the bone MUST be sawed", bone_locked_organs),
            I18n.T("otherwise, the state of the bone doesn't matter")));
    }
}

/// Abductor subtype that works through clothes and lets you extract the heart without sawing bones
// tg: code/modules/surgery/operations/operation_organ_manip.dm:303
public partial class datum_surgery_operation_limb_organ_manipulation_internal_abductor : datum_surgery_operation_limb_organ_manipulation_internal
{
    // tg: code/modules/surgery/operations/operation_organ_manip.dm:310
    public override object organ_check(obj_item_bodypart limb = null, obj_item_organ organ = null)
    {
        return Dm.eq(organ.slot, ORGAN_SLOT_HEART) ? TRUE : base.organ_check(limb, organ); // Hearts can always be removed, it doesn't check for bone state
    }
}

// All external organ manipulation requires bones sawed
// tg: code/modules/surgery/operations/operation_organ_manip.dm:314
public partial class datum_surgery_operation_limb_organ_manipulation_external : datum_surgery_operation_limb_organ_manipulation
{
    // tg: code/modules/surgery/operations/operation_organ_manip.dm:322
    public override object organ_check(obj_item_bodypart limb = null, obj_item_organ organ = null)
    {
        return Dm.bitand(organ.organ_flags, ORGAN_EXTERNAL);
    }
}
