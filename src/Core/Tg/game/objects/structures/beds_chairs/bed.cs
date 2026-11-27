// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/beds_chairs/bed.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8y slice A (D218, D223): the whole file: the bed (its elevation, the patient lying at buckle_lying by the headrest's side,
// the lay-down messages), the medical bed (brakes, the raised mattress with a patient), the emergency medical bed folded into
// the roller bed item and deployed from it, the silicon dock, the dog beds, the dirty mattress and the double bed. Marked
// leaves: the emissive brakes (DV12).
// The var blocks are generated.

/* Beds... get your mind out of the gutter, they're for sleeping!
 * Contains:
 * Beds
 * Medical beds
 * Roller beds
 * Pet beds
 */

/// Beds
// tg: code/game/objects/structures/beds_chairs/bed.dm:10
public partial class obj_structure_bed : obj_structure
{
    // tg: code/game/objects/structures/beds_chairs/bed.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_soft_landing)));
        if (Dm.truthy(elevation))
            Waiting.NoSleep(AddElement(typeof(datum_element_elevation), Dm.named("pixel_shift", elevation)));
        update_buckle_vars(dir);
        register_context();
        return dot;
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:42
    public override void buckle_feedback(mob_living being_buckled = null, mob buckler = null)
    {
        if (HAS_TRAIT(being_buckled, TRAIT_RESTRAINED))
        {
            base.buckle_feedback(being_buckled, buckler);
            return;
        }

        if (being_buckled == buckler)
        {
            being_buckled.visible_message(
                span_notice(I18n.T("[] lays down on [].", buckler, this)),
                span_notice(I18n.T("You lay down on [].", this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }
        else
        {
            being_buckled.visible_message(
                span_notice(I18n.T("[] lays [] down on [].", buckler, being_buckled, this)),
                span_notice(I18n.T("[] lays you down on [].", buckler, this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:59
    [DmParams("being_unbuckled", "unbuckler")]
    public override void unbuckle_feedback(mob_living being_unbuckled = null, mob unbuckler = null)
    {
        if (HAS_TRAIT(being_unbuckled, TRAIT_RESTRAINED))
        {
            base.unbuckle_feedback(being_unbuckled, unbuckler);
            return;
        }

        if (being_unbuckled == unbuckler)
        {
            being_unbuckled.visible_message(
                span_notice(I18n.T("[] gets up from [].", unbuckler, this)),
                span_notice(I18n.T("You get up from [].", this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }
        else
        {
            being_unbuckled.visible_message(
                span_notice(I18n.T("[] pulls [] up from [].", unbuckler, being_unbuckled, this)),
                span_notice(I18n.T("[] pulls you up from [].", unbuckler, this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:76
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(can_deconstruct))
            Dm.append(ref dot, span_notice(I18n.T("It's held together by a couple of <b>bolts</b>.")));
        return dot;
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:81
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (Dm.truthy(held_item))
        {
            if (!Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
                return null;

            context[SCREENTIP_CONTEXT_RMB] = "Dismantle";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        else if (Dm.truthy(has_buckled_mobs()))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Unbuckle";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return null;
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:93
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        update_buckle_vars(newdir);
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:97
    public virtual void update_buckle_vars(object newdir = null)
    {
        buckle_lying = Dm.truthy(Dm.bitand(newdir, left_headrest_dirs)) ? 270 : 90;
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:100
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (build_stack_type != null)
            Dm.@new(build_stack_type, loc, build_stack_amount);
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:104
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:107
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item weapon = null)
    {
        if (!Dm.truthy(can_deconstruct))
            return NONE;
        await base.wrench_act_secondary(user, weapon);
        weapon.play_tool_sound(this);
        await deconstruct(disassembled: TRUE);
        return TRUE;
    }
}

/// Medical beds
// tg: code/game/objects/structures/beds_chairs/bed.dm:116
public partial class obj_structure_bed_medical : obj_structure_bed
{
    // tg: code/game/objects/structures/beds_chairs/bed.dm:145
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noisy_movement)));
        if (Dm.truthy(anchored))
            update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:151
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        context[SCREENTIP_CONTEXT_ALT_LMB] = Dm.text("[]", Dm.truthy(anchored) ? "Release brakes" : "Apply brakes");
        if (foldable_type != null && !Dm.truthy(has_buckled_mobs()))
            context[SCREENTIP_CONTEXT_RMB] = "Fold up";

        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:159
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("The brakes are applied. They can be released with an Alt-click.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("The brakes can be applied with an Alt-click.")));

        if (foldable_type != null)
            Dm.append(ref dot, span_notice(I18n.T("You can fold it up with a Right-click.")));
        return dot;
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:169
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(has_buckled_mobs()) && Dm.@in(user, buckled_mobs))
            return CLICK_ACTION_BLOCKING;

        anchored = Dm.truthy(anchored) ? FALSE : TRUE;
        balloon_alert(user, I18n.T("brakes []", Dm.truthy(anchored) ? I18n.T("applied") : I18n.T("released")));
        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:178
    [DmParams("buckled")]
    public override async DmTask post_buckle_mob(mob_living buckled = null)
    {
        await base.post_buckle_mob(buckled);
        set_density(TRUE);
        update_appearance();
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:183
    [DmParams("buckled")]
    public override async DmTask post_unbuckle_mob(mob_living buckled = null)
    {
        await base.post_unbuckle_mob(buckled);
        set_density(FALSE);
        update_appearance();
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:188
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(has_buckled_mobs()))
        {
            icon_state = Dm.text("[]_up", base_icon_state);
            // Push them up from the normal lying position
            if (Dm.length(buckled_mobs) > 1)
            {
                foreach (mob_living patient in Dm.each_cast<mob_living>(buckled_mobs))
                    patient.pixel_y = patient.base_pixel_y;
            }
            else
            {
                mob_living first = (mob_living)buckled_mobs[1];
                first.pixel_y = first.base_pixel_y;
            }
        }
        else
            icon_state = Dm.text("[]_down", base_icon_state);
        return dot;
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:201
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(anchored))
            return dot;

        if (Dm.truthy(has_buckled_mobs()))
        {
            Dm.append(ref dot, mutable_appearance(icon, "brakes_up"));
            // DEVIATION(DV12): . += emissive_appearance(icon, "brakes_up", src, alpha = src.alpha), the glow on the emissive plane, is not created
        }
        else
        {
            Dm.append(ref dot, mutable_appearance(icon, "brakes_down"));
            // DEVIATION(DV12): . += emissive_appearance(icon, "brakes_down", src, alpha = src.alpha)
        }
        return dot;
    }
}

// tg: code/game/objects/structures/beds_chairs/bed.dm:138
public partial class obj_structure_bed_medical_emergency : obj_structure_bed_medical
{
    // tg: code/game/objects/structures/beds_chairs/bed.dm:213
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_emergency_bed_silicon)
            return NONE;
        obj_item_emergency_bed_silicon silicon_bed = (obj_item_emergency_bed_silicon)tool;
        if (Dm.truthy(silicon_bed.loaded))
        {
            to_chat(user, span_warning(I18n.T("You already have a medical bed docked!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(has_buckled_mobs()))
        {
            if (Dm.length(buckled_mobs) == 1)
            {
                await user_unbuckle_mob(buckled_mobs[1] as mob_living, user);
                return ITEM_INTERACT_SUCCESS;
            }

            unbuckle_all_mobs();
            user.visible_message(span_notice(I18n.T("[] unbuckles all creatures from [].", user, this)));
            return ITEM_INTERACT_SUCCESS;
        }


        silicon_bed.loaded = this;
        await forceMove(silicon_bed);
        user.visible_message(span_notice(I18n.T("[] collects [].", user, this)), span_notice(I18n.T("You collect [].", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:236
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        if (!ishuman(user) || !Dm.truthy(await user.can_perform_action(this)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        if (Dm.truthy(has_buckled_mobs()))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;

        user.visible_message(span_notice(I18n.T("[] collapses [].", user, this)), span_notice(I18n.T("You collapse [].", this)));
        obj_item folding_bed = Dm.@new(foldable_type, get_turf(this)) as obj_item;
        await user.put_in_hands(folding_bed);
        qdel(this);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }
}

// tg: code/game/objects/structures/beds_chairs/bed.dm:251
public partial class obj_item_emergency_bed : obj_item
{
    // tg: code/game/objects/structures/beds_chairs/bed.dm:262
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_emergency_bed_silicon)
            return NONE;

        obj_item_emergency_bed_silicon silicon_bed = (obj_item_emergency_bed_silicon)tool;
        if (Dm.truthy(silicon_bed.loaded))
        {
            to_chat(user, span_warning(I18n.T("[] already has a roller bed loaded!", silicon_bed)));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_notice(I18n.T("[] loads [].", user, this)), span_notice(I18n.T("You load [] into [].", this, silicon_bed)));
        silicon_bed.loaded = Dm.@new<obj_structure_bed_medical_emergency>(silicon_bed);
        qdel(this); //"Load"
        return ITEM_INTERACT_SUCCESS;
    }


    // tg: code/game/objects/structures/beds_chairs/bed.dm:277
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await deploy_bed(user, user.loc);
        return null;
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:280
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (isopenturf(interacting_with))
        {
            await deploy_bed(user, interacting_with);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }


    // tg: code/game/objects/structures/beds_chairs/bed.dm:287
    public virtual async DmTask deploy_bed(mob user = null, atom location = null)
    {
        obj_structure_bed_medical_emergency deployed = Dm.@new<obj_structure_bed_medical_emergency>(location);
        deployed.add_fingerprint(user);
        qdel(this);
    }
}

// tg: code/game/objects/structures/beds_chairs/bed.dm:292
public partial class obj_item_emergency_bed_silicon : obj_item_emergency_bed // ROLLER ROBO DA!
{
    // tg: code/game/objects/structures/beds_chairs/bed.dm:297
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        loaded = Dm.@new<obj_structure_bed_medical_emergency>(this);
        return dot;
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:301
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("The dock is [].", Dm.truthy(loaded) ? I18n.T("loaded") : I18n.T("empty")));
        return dot;
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:305
    public override async DmTask deploy_bed(mob user = null, atom location = null)
    {
        if (Dm.truthy(loaded))
        {
            await loaded.forceMove(location);
            user.visible_message(span_notice(I18n.T("[] deploys [].", user, loaded)), span_notice(I18n.T("You deploy [].", loaded)));
            loaded = null;
        }
        else
            to_chat(user, span_warning(I18n.T("The dock is empty!")));
    }
}

/// Dog bed
// tg: code/game/objects/structures/beds_chairs/bed.dm:314
public partial class obj_structure_bed_dogbed : obj_structure_bed
{
    ///Used to set the owner of a dogbed, returns FALSE if called on an owned bed or an invalid one, TRUE if the possesion succeeds
    // tg: code/game/objects/structures/beds_chairs/bed.dm:360
    public virtual object update_owner(mob_living furball = null)
    {
        if (Dm.truthy(owned) || type != typeof(obj_structure_bed_dogbed)) //Only marked beds work, this is hacky but I'm a hacky man
            return FALSE; //Failed

        owned = TRUE;
        name = I18n.T("[]'s bed", furball);
        desc = I18n.T("[]'s bed! Looks comfy.", furball);
        return TRUE; // Let any callers know that this bed is ours now
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:369
    [DmParams("furball", "force", "check_loc")]
    public override async DmTask<object> buckle_mob(mob_living furball = null, float? force = null, float? check_loc = null, float? buckle_mob_flags = null)
    {
        object dot = await base.buckle_mob(furball, force, check_loc, buckle_mob_flags);
        update_owner(furball);
        return dot;
    }
}

// tg: code/game/objects/structures/beds_chairs/bed.dm:373
public partial class obj_structure_bed_maint : obj_structure_bed
{
    // tg: code/game/objects/structures/beds_chairs/bed.dm:379
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_MOLD, CELL_VIRUS_TABLE_GENERIC, rand(2, 4), 25));
        return dot;
    }
}

// Double Beds, for luxurious sleeping, i.e. the captain and maybe heads- if people use this for ERP, send them to skyrat
// tg: code/game/objects/structures/beds_chairs/bed.dm:384
public partial class obj_structure_bed_double : obj_structure_bed
{
    // tg: code/game/objects/structures/beds_chairs/bed.dm:394
    [DmParams("target")]
    public override async DmTask post_buckle_mob(mob_living target = null)
    {
        await base.post_buckle_mob(target);
        if (Dm.length(buckled_mobs) > 1 && !Dm.truthy(goldilocks)) // Push the second buckled mob a bit higher from the normal lying position
        {
            target.pixel_y += 6;
            goldilocks = target;
        }
    }

    // tg: code/game/objects/structures/beds_chairs/bed.dm:400
    [DmParams("target")]
    public override async DmTask post_unbuckle_mob(mob_living target = null)
    {
        await base.post_unbuckle_mob(target);
        if (target == goldilocks)
        {
            target.pixel_y -= 6;
            goldilocks = null;
        }
    }
}
