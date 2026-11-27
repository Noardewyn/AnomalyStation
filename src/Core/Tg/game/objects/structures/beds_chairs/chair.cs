// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/beds_chairs/chair.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 7 (furniture): the chair, the stool, the bar stool and the office chair (the subset Meta uses; the comfy chairs
// are var-only subtypes and turn live with the chair): init, rotation (the simple_rotation element: alt-click), the
// wrench's secondary act and atom_deconstruct into the build stack, the layer and the facing of a buckled mob, picking a
// chair up by dragging it onto oneself, and the chair item (/obj/item/chair: planting it back, the hit reaction and the
// smash). M8y slice A (D218, D223): the buckling (buckle_feedback, unbuckle_feedback, post_buckle_mob, post_unbuckle_mob,
// user_buckle_mob, the stools' sitter offsets), the wooden chair, the shuttle seat (its restraint overlays and messages);
// pew.cs and sofa.cs are the pews and the sofas. Generated live-partial stubs (docs/ai/specs/live-partial.txt):
// the latejoin chair (JoinPlayerHere, shuttles M10). Gaps AS1: electrify_self (the shock kit making an electric chair, with its
// overlay, the seat's and the office chair's shifted a pixel; datums/components/electrified_buckle.cs). Gaps ST5 (D333): Nar'Sie's
// touch (a chair becomes a wooden one), telekinesis turning a chair, the chair item's suicide, the brass chair (alt-click spins it
// eight times on the fast processing, the servo's step at each turn, its cogs' noise when dragged), the folding plastic chair (a fat
// sitter breaks it in six seconds) and the handrail (held onto, upright, until the hands are blocked). The var blocks are generated.

// tg: code/game/objects/structures/beds_chairs/chair.dm:1
public partial class obj_structure_chair : obj_structure
{
    // tg: code/game/objects/structures/beds_chairs/chair.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(0.2f))
        {
            name = Dm.text("tactical []", name);
            fishing_modifier -= 8;
        }
        MakeRotate();
        if (Dm.truthy(can_buckle) && Dm.truthy(fishing_modifier))
            Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), fishing_modifier));
        return dot;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:32
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
                span_notice(I18n.T("[] sits down on [].", buckler, this)),
                span_notice(I18n.T("You sit down on [].", this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }
        else
        {
            being_buckled.visible_message(
                span_notice(I18n.T("[] sits [] down on [].", buckler, being_buckled, this)),
                span_notice(I18n.T("[] sits you down on [].", buckler, this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:49
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
                span_notice(I18n.T("[] stands up from [].", unbuckler, this)),
                span_notice(I18n.T("You stand up from [].", this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }
        else
        {
            being_unbuckled.visible_message(
                span_notice(I18n.T("[] stands [] up from [].", unbuckler, being_unbuckled, this)),
                span_notice(I18n.T("[] stands you up from [].", unbuckler, this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:66
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It's held together by a couple of <b>bolts</b>.")));
        if (!Dm.truthy(has_buckled_mobs()) && Dm.truthy(can_buckle))
            Dm.append(ref dot, span_notice(I18n.T("While standing on [], drag and drop your sprite onto [] to buckle to it.", this, this)));
        return dot;
    }

    ///This proc adds the rotate component, overwrite this if you for some reason want to change some specific args.
    // tg: code/game/objects/structures/beds_chairs/chair.dm:73
    public virtual void MakeRotate()
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation), Dm.bitor(ROTATION_IGNORE_ANCHORED, ROTATION_GHOSTS_ALLOWED)));
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:76
    public override object Destroy(float? force = null)
    {
        if (SSjob?.latejoin_trackers != null) //These may be here due to the arrivals shuttle
            Dm.remove(ref SSjob.latejoin_trackers, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:80
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        if (buildstacktype != null)
            Dm.@new(buildstacktype, loc, buildstackamount);
        else
        {
            foreach (datum_material mat in Dm.each<datum_material>(custom_materials))
                Dm.@new(mat.sheet_type, loc, FLOOR(Dm.num(custom_materials[mat]) / SHEET_MATERIAL_AMOUNT, 1));
        }
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:87
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:95
    public override async DmTask<object> attackby(obj_item W = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (istype(W, typeof(obj_item_assembly_shock_kit)) && !HAS_TRAIT(this, TRAIT_ELECTRIFIED_BUCKLE))
        {
            await electrify_self(W as obj_item_assembly_shock_kit, user);
            return null;
        }
        return await base.attackby(W, user, modifiers, attack_modifiers);
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:90  (gaps ST5)
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        obj_structure_chair_wood W = Dm.@new<obj_structure_chair_wood>(get_turf(this));
        await W.setDir(dir);
        qdel(this);
        return null;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:136  (gaps ST5)
    public override async DmTask<object> attack_tk(mob user = null)
    {
        if (!Dm.truthy(anchored) || Dm.truthy(has_buckled_mobs()) || !isturf(user.loc))
            return await base.attack_tk(user);
        await setDir(turn(dir, -90));
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:101
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(has_buckled_mobs()))
            return dot;
        mutable_appearance armrest = (mutable_appearance)mutable_appearance(icon, Dm.text("[]_armrest", icon_state), ABOVE_MOB_LAYER, this, appearance_flags: KEEP_APART);
        Dm.Deferred("code/game/objects/structures/beds_chairs/chair.dm:105", "update_overlays: emissive_blocker of the armrest (DEVIATION DV12)");
        if (Dm.truthy(cached_color_filter)) // M6 slice 7b (D156): the colour filter reaches the client
            armrest = filter_appearance_recursive(armrest, cached_color_filter);
        Dm.append(ref dot, armrest);
        return dot;
    }

    ///allows each chair to request the electrified_buckle component with overlays that dont look ridiculous
    // tg: code/game/objects/structures/beds_chairs/chair.dm:113
    [ShouldCallParent]
    public virtual async DmTask electrify_self(obj_item_assembly_shock_kit input_shock_kit = null, mob user = null, DmList overlays_from_child_procs = null)
    {
        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(input_shock_kit)))
            return;
        if (overlays_from_child_procs == null || overlays_from_child_procs.Count == 0)
        {
            mutable_appearance echair_overlay = mutable_appearance(Dm.resource("icons/obj/chairs.dmi"), "echair_over", OBJ_LAYER, this, appearance_flags: KEEP_APART) as mutable_appearance;
            await AddComponent(typeof(datum_component_electrified_buckle), Dm.bitor(Dm.bitor(SHOCK_REQUIREMENT_ITEM, SHOCK_REQUIREMENT_LIVE_CABLE), SHOCK_REQUIREMENT_SIGNAL_RECEIVED_TOGGLE), input_shock_kit, Dm.list(echair_overlay), FALSE);
        }
        else
            await AddComponent(typeof(datum_component_electrified_buckle), Dm.bitor(Dm.bitor(SHOCK_REQUIREMENT_ITEM, SHOCK_REQUIREMENT_LIVE_CABLE), SHOCK_REQUIREMENT_SIGNAL_RECEIVED_TOGGLE), input_shock_kit, overlays_from_child_procs, FALSE);

        if (HAS_TRAIT(this, TRAIT_ELECTRIFIED_BUCKLE))
            to_chat(user, span_notice(I18n.T("You connect the shock kit to \\the [], electrifying it ", this)));
        else
        {
            await user.put_in_active_hand(input_shock_kit);
            to_chat(user, span_notice(I18n.T("You cannot fit the shock kit onto \\the []!", this)));
        }
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:130
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item weapon = null)
    {
        await base.wrench_act_secondary(user, weapon);
        weapon.play_tool_sound(this);
        await deconstruct(disassembled: TRUE);
        return TRUE;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:143
    public virtual void handle_rotation(object direction = null)
    {
        handle_layer();
        if (Dm.truthy(has_buckled_mobs()))
        {
            foreach (object m in Dm.each(buckled_mobs))
            {
                mob_living buckled_mob = m as mob_living;
                Waiting.NoSleep(buckled_mob.setDir(direction));
            }
        }
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:150
    public virtual void handle_layer()
    {
        if (Dm.truthy(has_buckled_mobs()) && Dm.eq(dir, NORTH))
            layer = ABOVE_MOB_LAYER;
        else
            layer = OBJ_LAYER;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:156
    [DmParams("M")]
    public override async DmTask post_buckle_mob(mob_living M = null)
    {
        await base.post_buckle_mob(M);
        handle_layer();
        if (Dm.truthy(has_armrest))
            update_appearance();
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:162
    [DmParams()]
    public override async DmTask post_unbuckle_mob(mob_living unbuckled_mob = null)
    {
        await base.post_unbuckle_mob(unbuckled_mob);
        handle_layer();
        if (Dm.truthy(has_armrest))
            update_appearance();
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:168
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        handle_rotation(newdir);
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:347
    public override async DmTask<object> mouse_drop_dragged(atom over_object = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        if (!isliving(user) || over_object != user)
            return null;
        if (item_chair == null || Dm.truthy(has_buckled_mobs()))
            return null;
        if (Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1)))
        {
            to_chat(user, span_notice(I18n.T("You try to pick up \\the [], but it fades away!", this)));
            qdel(this);
            return null;
        }

        user.visible_message(span_notice(I18n.T("[] grabs \\the [].", user, name)), span_notice(I18n.T("You grab \\the [].", name)));
        obj_item chair_item = Dm.@new(item_chair, loc) as obj_item;
        await chair_item.set_custom_materials(custom_materials);
        await TransferComponents(chair_item);
        chair_item.update_integrity(get_integrity());
        await user.put_in_hands(chair_item);
        qdel(this);
        return null;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:365
    public override async DmTask<object> user_buckle_mob(mob_living M = null, mob user = null, float? check_loc = null)
    {
        check_loc ??= TRUE;
        return await base.user_buckle_mob(M, user, check_loc);
    }
}

// tg: code/game/objects/structures/beds_chairs/chair.dm:181
public partial class obj_structure_chair_wood : obj_structure_chair
{
    // tg: code/game/objects/structures/beds_chairs/chair.dm:193  (gaps ST5)
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/game/objects/structures/beds_chairs/chair.dm:228
public partial class obj_structure_chair_comfy_shuttle : obj_structure_chair_comfy
{
    // tg: code/game/objects/structures/beds_chairs/chair.dm:239
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:243
    public override async DmTask electrify_self(obj_item_assembly_shock_kit input_shock_kit = null, mob user = null, DmList overlays_from_child_procs = null)
    {
        if (overlays_from_child_procs == null)
        {
            mutable_appearance echair_overlay = mutable_appearance(Dm.resource("icons/obj/chairs.dmi"), "echair_over", OBJ_LAYER, this, appearance_flags: KEEP_APART) as mutable_appearance;
            echair_overlay.pixel_x = -1;
            overlays_from_child_procs = Dm.list(echair_overlay);
        }
        await base.electrify_self(input_shock_kit, user, overlays_from_child_procs);
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:250
    public override void buckle_feedback(mob_living being_buckled = null, mob buckler = null)
    {
        if (being_buckled == buckler)
        {
            being_buckled.visible_message(
                span_notice(I18n.T("[] sits down on [], pulling the overhead restraint down to secure []self.", buckler, this, buckler.p_them())),
                span_notice(I18n.T("You sit down on [], pulling the overhead restraint down to secure yourself.", this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }
        else
        {
            being_buckled.visible_message(
                span_notice(I18n.T("[] sits [] down on [], pulling the overhead restraint down to secure [].", buckler, being_buckled, this, buckler.p_them())),
                span_notice(I18n.T("[] sits you down on [], pulling the overhead restraint down to secure you.", buckler, this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:264
    [DmParams("being_unbuckled", "unbuckler")]
    public override void unbuckle_feedback(mob_living being_unbuckled = null, mob unbuckler = null)
    {
        if (being_unbuckled == unbuckler)
        {
            being_unbuckled.visible_message(
                span_notice(I18n.T("[] flips the overhead restraint up, standing up from [].", unbuckler, this)),
                span_notice(I18n.T("You flip the overhead restraint up, standing up from [].", this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }
        else
        {
            being_unbuckled.visible_message(
                span_notice(I18n.T("[] flips the overhead restraint up, standing [] up from [].", unbuckler, being_unbuckled, this)),
                span_notice(I18n.T("[] flips the overhead restraint up, standing you up from [].", unbuckler, this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:278
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(has_buckled_mobs()))
        {
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_down_front", icon_state), ABOVE_MOB_LAYER + 0.01f));
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_down_behind", icon_state), layer + 0.01f));
        }
        else
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_up", icon_state), layer + 0.01f));
        return dot;
    }
}

// tg: code/game/objects/structures/beds_chairs/chair.dm:297
public partial class obj_structure_chair_office : obj_structure_chair
{
    // tg: code/game/objects/structures/beds_chairs/chair.dm:306
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noisy_movement)));
        return dot;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:310
    public override async DmTask electrify_self(obj_item_assembly_shock_kit input_shock_kit = null, mob user = null, DmList overlays_from_child_procs = null)
    {
        if (overlays_from_child_procs == null)
        {
            mutable_appearance echair_overlay = mutable_appearance(Dm.resource("icons/obj/chairs.dmi"), "echair_over", OBJ_LAYER, this, appearance_flags: KEEP_APART) as mutable_appearance;
            echair_overlay.pixel_x = -1;
            overlays_from_child_procs = Dm.list(echair_overlay);
        }
        await base.electrify_self(input_shock_kit, user, overlays_from_child_procs);
    }
}

//Stool
// tg: code/game/objects/structures/beds_chairs/chair.dm:327
public partial class obj_structure_chair_stool : obj_structure_chair
{
    // tg: code/game/objects/structures/beds_chairs/chair.dm:335
    [DmParams("Mob")]
    public override async DmTask post_buckle_mob(mob_living Mob = null)
    {
        Mob.add_offsets(type, z_add: 4);
        await base.post_buckle_mob(Mob);
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:339
    [DmParams("Mob")]
    public override DmTask post_unbuckle_mob(mob_living Mob = null)
    {
        Mob.remove_offsets(type);
        return DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:344  (gaps ST5)
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/game/objects/structures/beds_chairs/chair.dm:368
public partial class obj_structure_chair_stool_bar : obj_structure_chair_stool
{
    // tg: code/game/objects/structures/beds_chairs/chair.dm:374
    [DmParams("Mob")]
    public override async DmTask post_buckle_mob(mob_living Mob = null)
    {
        await base.post_buckle_mob(Mob);
        Mob.add_offsets(type, z_add: 7);
    }
}


// tg: code/game/objects/structures/beds_chairs/chair.dm:391
public partial class obj_item_chair : obj_item
{
    // tg: code/game/objects/structures/beds_chairs/chair.dm:417
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/objects/structures/beds_chairs/chair.dm:419", "Initialize: AddElement(/datum/element/cuffable_item): cuffing a mob to the chair (restraints, M6)");
        return dot;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:421  (gaps ST5)
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins hitting []self with \\the []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        playsound(this, hitsound, 50, TRUE);
        await DmTask.CompletedTask;
        return BRUTELOSS;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:426  (gaps ST5)
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        obj_item_chair_wood W = Dm.@new<obj_item_chair_wood>(get_turf(this));
        await W.setDir(dir);
        qdel(this);
        return null;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:431
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await plant(user);
        return null;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:434
    public virtual async DmTask plant(mob user = null)
    {
        turf turf = user.loc as turf;
        if (!(user.loc is turf) || isgroundlessturf(turf))
        {
            to_chat(user, span_warning(I18n.T("You need ground to plant this on!")));
            return;
        }
        if (!Dm.truthy(await user.dropItemToGround(this)))
        {
            to_chat(user, span_warning(I18n.T("[] is stuck to your hand!", this)));
            return;
        }
        if (Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1)))
        {
            to_chat(user, span_notice(I18n.T("You try to place down \\the [], but it fades away!", this)));
            qdel(this);
            return;
        }

        foreach (obj @object in Dm.each<obj>(turf))
        {
            if (@object is obj_structure_chair)
            {
                to_chat(user, span_warning(I18n.T("There is already a chair here!")));
                return;
            }
            if (Dm.truthy(@object.density) && !Dm.truthy(Dm.bitand(@object.flags_1, ON_BORDER_1)))
            {
                to_chat(user, span_warning(I18n.T("There is already something here!")));
                return;
            }
        }

        user.visible_message(span_notice(I18n.T("[] rights \\the [].", user, name)), span_notice(I18n.T("You right \\the [].", name)));
        obj_structure_chair chair = Dm.@new(origin_type, turf) as obj_structure_chair;
        await chair.set_custom_materials(custom_materials);
        await TransferComponents(chair);
        await chair.setDir(user.dir);
        chair.update_integrity(get_integrity());
        qdel(this);
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:463
    public virtual async DmTask smash(mob_living user = null)
    {
        Type stack_type = Dm.initial(origin_type, (obj_structure_chair c) => c.buildstacktype);
        if (stack_type == null)
            return;
        float remaining_mats = Dm.initial(origin_type, (obj_structure_chair c) => c.buildstackamount);
        remaining_mats--; //Part of the chair was rendered completely unusable. It magically disappears. Maybe make some dirt?
        if (Dm.truthy(remaining_mats))
        {
            for (float M = 1; M <= remaining_mats; M++)
                Dm.@new(stack_type, get_turf(loc));
        }
        else if (Dm.truthy(custom_materials[await SSmaterials.get_material(typeof(datum_material_iron))]))
            Dm.@new(typeof(obj_item_stack_rods), get_turf(loc), 2);
        qdel(this);
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:476
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if ((Dm.eq(attack_type, UNARMED_ATTACK) && prob(hit_reaction_chance)) || (Dm.eq(attack_type, LEAP_ATTACK) && prob(hit_reaction_chance)))
        {
            owner.visible_message(span_danger(I18n.T("[] fends off [] with []!", owner, attack_text, this)));
            if (Dm.truthy(await take_chair_damage(damage, damage_type, MELEE))) // Our chair takes our incoming damage for us, which can result in it smashing.
                await smash(owner);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:484
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!ishuman(target))
            return;

        mob_living_carbon_human give_this_fucker_the_chair = (mob_living_carbon_human)target;

        // Here we determine if our attack is against a vulnerable target
        bool vulnerable_hit = Dm.truthy(check_behind(user, give_this_fucker_the_chair));

        // If our attack is against a vulnerable target, we do additional damage to the chair
        float damage_to_inflict = vulnerable_hit ? (Dm.num(force) * 5) : (Dm.num(force) * 2.5f);

        if (!Dm.truthy(await take_chair_damage(damage_to_inflict, damtype, MELEE))) // If we would do enough damage to bring our chair's integrity to 0, we instead go past the check to smash it against our target
            return;

        user.visible_message(span_danger(I18n.T("[] smashes [] to pieces against []", user, this, give_this_fucker_the_chair)));
        if (!HAS_TRAIT(give_this_fucker_the_chair, TRAIT_BRAWLING_KNOCKDOWN_BLOCKED))
        {
            if (vulnerable_hit || Dm.truthy(give_this_fucker_the_chair.get_timed_status_effect_duration(typeof(datum_status_effect_staggered))))
            {
                await give_this_fucker_the_chair.Knockdown(2 * SECONDS, daze_amount: daze_amount);
                if (Dm.num(give_this_fucker_the_chair.health) < Dm.num(give_this_fucker_the_chair.maxHealth) * 0.5f)
                    await give_this_fucker_the_chair.adjust_confusion(10 * SECONDS);
            }
        }

        await smash(user as mob_living);
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:508
    public virtual async DmTask<object> take_chair_damage(object damage_to_inflict = null, object damage_type = null, object armor_flag = null)
    {
        if (Dm.num(damage_to_inflict) >= Dm.num(atom_integrity))
            return TRUE;
        await take_damage(damage_to_inflict, damage_type as string, armor_flag);
        return FALSE;
    }
}

public static partial class GlobalProcs
{
    ///Checks to see if `atom/source` is behind `atom/target`
    // tg: code/__HELPERS/game.dm:280
    public static object check_behind(atom source = null, atom target = null)
    {
        // Let's see if source is behind target
        // "Behind" is defined as 3 tiles directly to the back of the target
        // x . .
        // x > .
        // x . .

        // No tactical spinning allowed
        if (HAS_TRAIT(target, TRAIT_SPINNING))
            return TRUE;

        // We'll take "same tile" as "behind" for ease
        if (target.loc == source.loc)
            return TRUE;

        // We'll also assume lying down is behind, as mob directions when lying are unclear
        if (isliving(target))
        {
            mob_living living_target = (mob_living)target;
            if (Dm.eq(living_target.body_position, LYING_DOWN))
                return TRUE;
        }

        // Exceptions aside, let's actually check if they're, yknow, behind
        object dir_target_to_source = get_dir(target, source);
        if (Dm.truthy(Dm.bitand(target.dir, REVERSE_DIR(Dm.num(dir_target_to_source)))))
            return TRUE;

        return FALSE;
    }
}

// tg: code/game/objects/structures/beds_chairs/chair.dm:615
public partial class obj_structure_chair_mime : obj_structure_chair
{
    // tg: code/game/objects/structures/beds_chairs/chair.dm:626
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_WRENCH, TOOL_ACT_SECONDARY));
        return dot;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:630
    public override DmTask post_buckle_mob(mob_living M = null)
    {
        M.add_offsets(type, z_add: 5);
        return DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:633
    public override DmTask post_unbuckle_mob(mob_living M = null)
    {
        M.remove_offsets(type);
        return DmTask.CompletedTask;
    }
}

// tg: code/game/objects/structures/beds_chairs/chair.dm:518  (gaps ST5)
public partial class obj_item_chair_stool : obj_item_chair
{
    // tg: code/game/objects/structures/beds_chairs/chair.dm:541
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        await DmTask.CompletedTask;
        return null; //sturdy enough to ignore a god
    }
}

// tg: code/game/objects/structures/beds_chairs/chair.dm:544  (gaps ST5)
public partial class obj_item_chair_wood : obj_item_chair
{
    // tg: code/game/objects/structures/beds_chairs/chair.dm:556
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/game/objects/structures/beds_chairs/chair.dm:570  (gaps ST5)
public partial class obj_structure_chair_bronze : obj_structure_chair
{
    // tg: code/game/objects/structures/beds_chairs/chair.dm:584
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noisy_movement), Dm.resource("sound/machines/clockcult/integration_cog_install.ogg"), 50));
        return dot;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:588
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:592
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        Waiting.NoSleep(setDir(turn(dir, -90)));
        playsound(this, Dm.resource("sound/effects/servostep.ogg"), 50, FALSE);
        turns++;
        if (Dm.num(turns) >= 8)
            STOP_PROCESSING(SSfastprocess, this);
        return null;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:599
    public override void MakeRotate()
    {
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:602
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        turns = 0;
        if (!Dm.truthy(Dm.bitand(datum_flags, DF_ISPROCESSING)))
        {
            user.visible_message(span_notice(I18n.T("[] spins [] around, and the last vestiges of Ratvarian technology keeps it spinning FOREVER.", user, this)),
                span_notice(I18n.T("Automated spinny chairs. The pinnacle of ancient Ratvarian technology.")));
            START_PROCESSING(SSfastprocess, this);
        }
        else
        {
            user.visible_message(span_notice(I18n.T("[] stops []'s uncontrollable spinning.", user, this)),
                span_notice(I18n.T("You grab [] and stop its wild spinning.", this)));
            STOP_PROCESSING(SSfastprocess, this);
        }
        await DmTask.CompletedTask;
        return CLICK_ACTION_SUCCESS;
    }
}

// tg: code/game/objects/structures/beds_chairs/chair.dm:636  (gaps ST5)
public partial class obj_structure_chair_plastic : obj_structure_chair
{
    // tg: code/game/objects/structures/beds_chairs/chair.dm:648
    [DmParams("Mob")]
    public override async DmTask post_buckle_mob(mob_living Mob = null)
    {
        Mob.add_offsets(type, z_add: 2);
        await base.post_buckle_mob(Mob);
        if (iscarbon(Mob))
            Waiting.Detach(snap_check((mob_living_carbon)Mob));
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:654
    [DmParams("Mob")]
    public override DmTask post_unbuckle_mob(mob_living Mob = null)
    {
        Mob.remove_offsets(type);
        return DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:657
    public virtual async DmTask snap_check(mob_living_carbon Mob = null)
    {
        if (Dm.num(Mob.nutrition) >= NUTRITION_LEVEL_FAT)
        {
            to_chat(Mob, span_warning(I18n.T("The chair begins to pop and crack, you're too heavy!")));
            if (Dm.truthy(await do_after(Mob, 6 * SECONDS, show_progress: FALSE)))
            {
                Mob.visible_message(span_notice(I18n.T("The plastic chair snaps under []'s weight!", Mob)));
                Dm.@new<obj_effect_decal_cleanable_plastic>(loc);
                qdel(this);
            }
        }
    }
}

// tg: code/game/objects/structures/beds_chairs/chair.dm:693  (gaps ST5)
public partial class obj_structure_handrail : obj_structure
{
    // tg: code/game/objects/structures/beds_chairs/chair.dm:706
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers);
        if (Dm.truthy(dot))
            return dot;
        return await mouse_buckle_handling(user as mob_living, user as mob_living);
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:709
    public override async DmTask<object> is_user_buckle_possible(mob_living target = null, mob user = null, float? check_loc = null)
    {
        check_loc ??= TRUE;
        return Dm.num(Dm.truthy(await base.is_user_buckle_possible(target, user, check_loc)) && user == target && !HAS_TRAIT(target, TRAIT_HANDS_BLOCKED));
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:712
    [DmParams("buckled_mob")]
    public override DmTask post_buckle_mob(mob_living buckled_mob = null)
    {
        RegisterSignal(buckled_mob, SIGNAL_ADDTRAIT(TRAIT_HANDS_BLOCKED), nameof(stop_buckle));

        float z_offset = 0;
        float w_offset = 0;
        if (Dm.truthy(Dm.bitand(dir, NORTH)))
            z_offset = -4;
        else if (Dm.truthy(Dm.bitand(dir, SOUTH)))
            z_offset = 8;
        if (Dm.truthy(Dm.bitand(dir, EAST)))
            w_offset = -8;
        else if (Dm.truthy(Dm.bitand(dir, WEST)))
            w_offset = 8;

        buckled_mob.add_offsets(type, z_add: z_offset, w_add: w_offset);
        return DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:728
    [DmParams("unbuckled_mob")]
    public override DmTask post_unbuckle_mob(mob_living unbuckled_mob = null)
    {
        UnregisterSignal(unbuckled_mob, SIGNAL_ADDTRAIT(TRAIT_HANDS_BLOCKED));
        unbuckled_mob.remove_offsets(type);
        return DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:732
    [SignalHandler]
    public virtual void stop_buckle(mob_living source = null, params object[] __extra)
    {
        source.visible_message(
            span_warning(I18n.T("[] loses [] grip on []!", source, source.p_their(), this)),
            span_warning(I18n.T("You lose your grip on []!", this)),
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
            vision_distance: COMBAT_MESSAGE_RANGE);
        Waiting.NoSleep(unbuckle_mob(source, TRUE, TRUE));
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:742
    public override void buckle_feedback(mob_living being_buckled = null, mob buckler = null)
    {
        buckler.visible_message(
            span_notice(I18n.T("[] grabs [] tight, keeping []self upright.", buckler, this, buckler.p_them())),
            span_notice(I18n.T("You grab [] tight, keeping yourself upright.", this)),
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
            vision_distance: COMBAT_MESSAGE_RANGE);
    }

    // tg: code/game/objects/structures/beds_chairs/chair.dm:750
    public override void unbuckle_feedback(mob_living unbuckled_mob = null, mob unbuckler = null)
    {
        mob_living being_unbuckled = unbuckled_mob;
        if (being_unbuckled == unbuckler)
        {
            being_unbuckled.visible_message(
                span_notice(I18n.T("[] lets go of [].", unbuckler, this)),
                span_notice(I18n.T("You let go of [].", this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
                vision_distance: COMBAT_MESSAGE_RANGE);
        }
        else
        {
            being_unbuckled.visible_message(
                span_warning(I18n.T("[] forces [] to let go of []!", unbuckler, being_unbuckled, this)),
                span_warning(I18n.T("[] forces you to let go of []!", unbuckler, this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
                vision_distance: COMBAT_MESSAGE_RANGE);
        }
    }
}
