// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/wall_healer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MD1: the whole file. The DeForest first aid station (MetaStation has 16, Lavaland 2) heals a hand put into it: every few
// seconds a tenth of its brute, burn, toxin and blood pools for a price (free for the medical staff and on red alert), its pools
// refill while nobody uses it, its four reservoirs show them, it hands out gauze on a right click and takes gauze to restock, and an
// emag turns it into a stabbing machine. The emergency station is free and quicker. The var blocks are generated (D111).
// Not here: the reservoirs' glow on the emissive plane (DEVIATION DV12).

// tg: code/game/machinery/wall_healer.dm:4
public partial class obj_machinery_wall_healer : obj_machinery
{
    // tg: code/game/machinery/wall_healer.dm:1  #define WALL_HEALER_OFFSET 32
    internal const float WALL_HEALER_OFFSET = 32;

    // tg: code/game/machinery/wall_healer.dm:66
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload))
        {
            num_bandages = 0;
            brute_healing = 0;
            burn_healing = 0;
            tox_healing = 0;
            blood_healing = 0;
            update_appearance();
        }
        if (circuit_value is obj_item_circuitboard board && Dm.truthy(Dm.bitand(board.obj_flags, EMAGGED))) // the board instance (GAP(types): circuit is a Type, _machinery.cs)
            obj_flags = Dm.bitor(obj_flags, EMAGGED);
        Waiting.NoSleep(init_payment());
        register_context();
        Waiting.NoSleep(find_and_mount_on_atom());
        return dot;
    }

    // tg: code/game/machinery/wall_healer.dm:81
    public override object Destroy(float? force = null)
    {
        clear_using_mob();
        QDEL_LAZYLIST(ref stocked_bandages);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/wall_healer.dm:86
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Heal self";
            context[SCREENTIP_CONTEXT_RMB] = "Get gauze";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (held_item is obj_item_stack_medical_wrap_gauze)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Restock";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (Dm.eq(held_item?.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] panel", Dm.truthy(panel_open) ? "Close" : "Open");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (Dm.eq(held_item?.tool_behaviour, TOOL_CROWBAR) && Dm.truthy(can_crowbar_deconstruct()))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return NONE;
    }

    // Someone please add generic support for constructing wall mounted objects thanks
    // tg: code/game/machinery/wall_healer.dm:103
    public override async DmTask on_construction(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(user.dir, NORTH)))
            pixel_y = Dm.num(pixel_y) + WALL_HEALER_OFFSET;
        else if (Dm.truthy(Dm.bitand(user.dir, SOUTH)))
            pixel_y = Dm.num(pixel_y) - WALL_HEALER_OFFSET;

        if (Dm.truthy(Dm.bitand(user.dir, EAST)))
            pixel_x = Dm.num(pixel_x) + WALL_HEALER_OFFSET;
        else if (Dm.truthy(Dm.bitand(user.dir, WEST)))
            pixel_x = Dm.num(pixel_x) - WALL_HEALER_OFFSET;

        if (!Dm.truthy(await find_and_mount_on_atom()))
        {
            stack_trace(Dm.text("Got to on_construction for [], but failed to mount on a wall! This should be asserted from construction requirements.", GetType()), "code/game/machinery/wall_healer.dm", 115);
            loc.balloon_alert(user, I18n.T("no wall to install on!"));
            await deconstruct(TRUE);
        }
    }

    // tg: code/game/machinery/wall_healer.dm:119
    public virtual object refill_healing_pool(object percent = null)
    {
        float pct = Dm.num(percent ?? 100f);
        float amount_refilled = 0;

        float initial_brute = Dm.initial(this, (obj_machinery_wall_healer p) => p.brute_healing);
        float pre_brute_healing = brute_healing;
        brute_healing = Math.Min(brute_healing + initial_brute * (pct / 100), initial_brute);
        amount_refilled += brute_healing - pre_brute_healing;

        float initial_burn = Dm.initial(this, (obj_machinery_wall_healer p) => p.burn_healing);
        float pre_burn_healing = burn_healing;
        burn_healing = Math.Min(burn_healing + initial_burn * (pct / 100), initial_burn);
        amount_refilled += burn_healing - pre_burn_healing;

        float initial_tox = Dm.initial(this, (obj_machinery_wall_healer p) => p.tox_healing);
        float pre_tox_healing = tox_healing;
        tox_healing = Math.Min(tox_healing + initial_tox * (pct / 100), initial_tox);
        amount_refilled += tox_healing - pre_tox_healing;

        float initial_blood = Dm.initial(this, (obj_machinery_wall_healer p) => p.blood_healing);
        float pre_blood_healing = blood_healing;
        blood_healing = Math.Min(blood_healing + initial_blood * (pct / 100), initial_blood);
        amount_refilled += blood_healing - pre_blood_healing;

        if (amount_refilled > 0)
            update_appearance();

        return amount_refilled;
    }

    // tg: code/game/machinery/wall_healer.dm:143
    public virtual async DmTask init_payment()
    {
        // Cost depends on service (so just use 0 here)
        // SSeconomy?: a test harness world that makes machines without Master has none (rules 10.6, as the payment component)
        await AddComponent(typeof(datum_component_payment), 0, SSeconomy?.get_dep_account(ACCOUNT_MED), PAYMENT_FRIENDLY);
        desc = Dm.text("[] []", desc, I18n.T("Charges by the second, though all costs are waived on red alert."));
    }

    // tg: code/game/machinery/wall_healer.dm:148
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        float total_bandages = num_bandages + LAZYLEN(stocked_bandages);
        string retrieve = total_bandages > 0
            ? I18n.T(" [] a bandage with [].", is_free(user as mob_living) ? I18n.T("Purchase") : I18n.T("Retrieve"), EXAMINE_HINT(I18n.T("right-click")))
            : "";
        Dm.append(ref dot, span_notice(I18n.T("It has [] bandage\\s stocked.[]", total_bandages, retrieve)));
        if (current_user != null)
        {
            Dm.append(ref dot, span_notice(current_hand != null
                ? I18n.T("[] currently has [] [] in it.", current_user, current_user.p_their(), current_hand.plaintext_zone)
                : I18n.T("[] currently is using it.", current_user)));
        }
        return dot;
    }

    // tg: code/game/machinery/wall_healer.dm:156
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (Dm.truthy(panel_open))
            Dm.append(ref dot, "open");

        float brute_state = 7 - Dm.round(7 * (brute_healing / Dm.initial(this, (obj_machinery_wall_healer p) => p.brute_healing)), 1);
        var brute = (mutable_appearance)mutable_appearance(icon, Dm.text("bar[]", brute_state), alpha: alpha, appearance_flags: RESET_COLOR);
        brute.color = Dm.initial(typeof(datum_reagent_medicine_c2_libital), (datum_reagent p) => p.color);
        // no offset necessary
        Dm.append(ref dot, brute);

        float burn_state = 7 - Dm.round(7 * (burn_healing / Dm.initial(this, (obj_machinery_wall_healer p) => p.burn_healing)), 1);
        var burn = (mutable_appearance)mutable_appearance(icon, Dm.text("bar[]", burn_state), alpha: alpha, appearance_flags: RESET_COLOR);
        burn.color = Dm.initial(typeof(datum_reagent_medicine_c2_aiuri), (datum_reagent p) => p.color);
        burn.pixel_w = Dm.num(burn.pixel_w) + 4;
        Dm.append(ref dot, burn);

        float tox_state = 7 - Dm.round(7 * (tox_healing / Dm.initial(this, (obj_machinery_wall_healer p) => p.tox_healing)), 1);
        var tox = (mutable_appearance)mutable_appearance(icon, Dm.text("bar[]", tox_state), alpha: alpha, appearance_flags: RESET_COLOR);
        tox.color = Dm.initial(typeof(datum_reagent_medicine_c2_syriniver), (datum_reagent p) => p.color);
        tox.pixel_w = Dm.num(tox.pixel_w) + 8;
        Dm.append(ref dot, tox);

        float blood_state = 7 - Dm.round(7 * (blood_healing / Dm.initial(this, (obj_machinery_wall_healer p) => p.blood_healing)), 1);
        var blood = (mutable_appearance)mutable_appearance(icon, Dm.text("bar[]", blood_state), alpha: alpha, appearance_flags: RESET_COLOR);
        blood.color = Dm.initial(typeof(datum_reagent_blood), (datum_reagent p) => p.color);
        blood.pixel_w = Dm.num(blood.pixel_w) + 12;
        Dm.append(ref dot, blood);

        // DEVIATION(DV12): if(is_operational) . += emissive_appearance(icon, "bar_emissive", src, alpha = src.alpha), the
        // reservoirs' glow on the emissive plane, is not created (lighting planes and emissives are not ported).
        Dm.append(ref dot, mutable_appearance(icon, "bar_shadow", alpha: alpha, appearance_flags: RESET_COLOR));
        return dot;
    }

    // tg: code/game/machinery/wall_healer.dm:190
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;

        playsound(this, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        visible_message(span_warning(I18n.T("Sparks fly out of []!", this)));
        balloon_alert(user, I18n.T("safeties disabled"));
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        if (circuit_value is obj_item_circuitboard board)
            board.obj_flags = Dm.bitor(board.obj_flags, EMAGGED);
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/game/machinery/wall_healer.dm:201
    public override DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
        => screwdriver_act_secondary(user, tool);

    // tg: code/game/machinery/wall_healer.dm:204
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/wall_healer.dm:207
    public override DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
        => crowbar_act_secondary(user, tool);

    // tg: code/game/machinery/wall_healer.dm:210
    public override DmTask<object> crowbar_act_secondary(mob_living user = null, obj_item tool = null)
        => default_deconstruction_crowbar(user, tool);

    /// We want user to be right up to the wall mount to use it
    /// However people may often map the machine over a table
    /// In those contexts, they should be allowed to reach over the table
    // tg: code/game/machinery/wall_healer.dm:216
    public virtual async DmTask<object> loc_check(mob checking)
    {
        turf turf_loc = get_turf(this);
        if (Dm.truthy(await turf_loc.is_blocked_turf()))
            return await checking.Adjacent(turf_loc);
        return ReferenceEquals(checking.loc, turf_loc) ? TRUE : FALSE;
    }

    // tg: code/game/machinery/wall_healer.dm:222
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        object dot = await base.mouse_drop_receive(dropped, user, @params);
        if (Dm.truthy(dot))
            return dot;
        if (!isliving(user) || !ishuman(dropped))
        {
            balloon_alert(user, I18n.T("incompatible!"));
            return FALSE;
        }
        var who_put_user_in = (mob_living)user;
        var new_user = (mob_living)dropped;
        if (!Dm.truthy(await loc_check(new_user)))
        {
            balloon_alert(who_put_user_in, ReferenceEquals(new_user, who_put_user_in) ? I18n.T("get closer!") : I18n.T("bring [] closer!", new_user.p_them()));
            return FALSE;
        }

        if (Dm.truthy(await do_after(user, 1 * SECONDS, this)))
            await other_put_users_hand_in(new_user, who_put_user_in);
        return TRUE;
    }

    // tg: code/game/machinery/wall_healer.dm:239
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!ishuman(user))
        {
            balloon_alert(user, I18n.T("incompatible!"));
            return FALSE;
        }
        if (!Dm.truthy(await loc_check(user)))
        {
            balloon_alert(user, I18n.T("get closer!"));
            return FALSE;
        }
        if (Dm.truthy(await do_after(user, 0.5f * SECONDS, this)))
            await user_put_in_own_hand((mob_living)user);
        return TRUE;
    }

    // tg: code/game/machinery/wall_healer.dm:253
    public virtual async DmTask user_put_in_own_hand(mob_living user)
    {
        if (ReferenceEquals(user, current_user))
        {
            clear_using_mob();
            if (ReferenceEquals(user.get_active_hand(), current_hand))
            {
                user.visible_message(
                    span_notice(I18n.T("[] removes [] hand from [].", user, user.p_their(), this)),
                    span_notice(I18n.T("You remove your hand from [].", this)),
                    span_hear(I18n.T("You hear a click.")),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
                    vision_distance: 5);
            }
            else
            {
                add_fingerprint(user);
                user.visible_message(
                    span_notice(I18n.T("[] removes [] hand from [] and puts it in [] other hand.", user, user.p_their(), this, user.p_their())),
                    span_notice(I18n.T("You remove your hand from [] and put it in your other hand.", this)),
                    span_hear(I18n.T("You hear a click.")),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
                    vision_distance: 5);
                set_using_mob(user);
            }
            return;
        }
        else if (current_user != null)
        {
            user.visible_message(
                span_notice(I18n.T("[] tries to put [] hand in [], but [] is already using it.", user, user.p_their(), this, current_user)),
                span_notice(I18n.T("You try to put your hand in [], but [] is already using it.", this, current_user)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
                vision_distance: 5);
            return;
        }

        add_fingerprint(user);
        if (Dm.truthy(is_operational))
        {
            user.visible_message(
                span_notice(I18n.T("[] puts [] hand in [], and immediately some kind of sensor scans [] arm.", user, user.p_their(), this, user.p_their())),
                span_notice(I18n.T("You put your hand in [], and immediately some kind of sensor scans your arm.", this)),
                span_hear(I18n.T("You hear a click.")),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
                vision_distance: 5);
        }
        else
        {
            user.visible_message(
                span_notice(I18n.T("[] puts [] hand in [], but it doesn't respond. Seems to be out of order.", user, user.p_their(), this)),
                span_notice(I18n.T("You put your hand in [], but it doesn't respond.", this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
                vision_distance: 5);
        }
        set_using_mob(user);
    }

    // tg: code/game/machinery/wall_healer.dm:302
    public virtual async DmTask other_put_users_hand_in(mob_living user, mob_living who_put_user_in)
    {
        if (ReferenceEquals(who_put_user_in, user))
        {
            await user_put_in_own_hand(user);
            return;
        }

        if (ReferenceEquals(current_user, user))
        {
            clear_using_mob();
            if (ReferenceEquals(user.get_active_hand(), current_hand))
            {
                to_chat(who_put_user_in, span_notice(I18n.T("You remove []'s hand from [].", user, this)));
                user.visible_message(
                    span_notice(I18n.T("[] removes []'s hand from [].", who_put_user_in, user, this)),
                    span_notice(I18n.T("[] remove your hand from [].", who_put_user_in, this)),
                    span_hear(I18n.T("You hear a click.")),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
                    vision_distance: 5,
                    ignored_mobs: who_put_user_in);
            }
            else
            {
                to_chat(who_put_user_in, span_notice(I18n.T("You remove []'s hand from [] and put it in [] other hand.", user, this, user.p_their())));
                user.visible_message(
                    span_notice(I18n.T("[] removes [] hand from [] and puts it in [] other hand.", who_put_user_in, user.p_their(), this, user.p_their())),
                    span_notice(I18n.T("[] removes your hand from [] and puts it in your other hand.", who_put_user_in, this)),
                    span_hear(I18n.T("You hear a click.")),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
                    vision_distance: 5,
                    ignored_mobs: who_put_user_in);
                add_fingerprint(user);
                set_using_mob(who_put_user_in);
            }
            return;
        }

        if (current_user != null)
        {
            to_chat(who_put_user_in, span_notice(I18n.T("You try to put []'s hand in [], but [] is already using it.", user, this, current_user)));
            user.visible_message(
                span_notice(I18n.T("[] tries to put []'s hand in [], but [] is already using it.", who_put_user_in, user, this, current_user)),
                span_notice(I18n.T("[] tries to put your hand in [], but [] is already using it.", who_put_user_in, this, current_user)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
                vision_distance: 5,
                ignored_mobs: who_put_user_in);
            return;
        }

        add_fingerprint(who_put_user_in);
        if (Dm.truthy(is_operational))
        {
            to_chat(who_put_user_in, span_notice(I18n.T("You put []'s hand in [], and immediately some kind of sensor scans [] arm.", user, this, user.p_their())));
            user.visible_message(
                span_notice(I18n.T("[] puts [] hand in [], and immediately some kind of sensor scans [] arm.", who_put_user_in, user.p_their(), this, user.p_their())),
                span_notice(I18n.T("[] puts your hand in [], and immediately some kind of sensor scans your arm.", who_put_user_in, this)),
                span_hear(I18n.T("You hear a click.")),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
                vision_distance: 5,
                ignored_mobs: who_put_user_in);
        }
        else
        {
            to_chat(who_put_user_in, span_notice(I18n.T("You put []'s hand in [], but it doesn't respond. Seems to be out of order.", user, this)));
            user.visible_message(
                span_notice(I18n.T("[] puts [] hand in [], but it doesn't respond. Seems to be out of order.", who_put_user_in, user.p_their(), this)),
                span_notice(I18n.T("[] puts your hand in [], but it doesn't respond.", who_put_user_in, this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
                vision_distance: 5,
                ignored_mobs: who_put_user_in);
        }
        set_using_mob(user);
    }

    // tg: code/game/machinery/wall_healer.dm:365
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (!Dm.eq(dot, SECONDARY_ATTACK_CALL_NORMAL) || !isliving(user))
            return dot;
        var living_user = (mob_living)user;
        if (!Dm.truthy(is_operational))
        {
            to_chat(user, span_warning(I18n.T("You try to retrieve some gauze, but [] doesn't respond.", this)));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        if (num_bandages + LAZYLEN(stocked_bandages) <= 0)
        {
            to_chat(user, span_warning(I18n.T("You try to retrieve some gauze, but [] seems to be out of stock.", this)));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        if (Dm.truthy(Dm.bitand(attempt_charge(this, user, extra_fees: (float)Math.Floor(per_bandage_cost)), COMPONENT_OBJ_CANCEL_CHARGE)))
        {
            if (!Dm.truthy(living_user.get_idcard()))
                to_chat(user, span_warning(I18n.T("No ID card found. Aborting.")));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && prob(99))
        {
            to_chat(user, span_warning(I18n.T("You try to retrieve some gauze, but it gets all jammed up in the access port.")));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }

        var bandage = LAZYACCESS(stocked_bandages, 1) as obj_item_stack_medical_wrap_gauze;
        if (bandage == null)
        {
            num_bandages--;
            bandage = Dm.@new<obj_item_stack_medical_wrap_gauze>(user.drop_location(), 1);
        }
        await user.put_in_hands(bandage);
        user.visible_message(
            span_notice(I18n.T("[] retrieves [] from [].", user, bandage, this)),
            span_notice(I18n.T("You retrieve [] from [].", bandage, this)),
            span_hear(I18n.T("You hear a click.")),
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
            vision_distance: 5);
        playsound(this, Dm.resource("sound/machines/machine_vend.ogg"), 50, TRUE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/machinery/wall_healer.dm:399
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        atom drop_loc = drop_location() as atom;
        foreach (obj_item_stack_medical_wrap_gauze bandage in Dm.each<obj_item_stack_medical_wrap_gauze>(stocked_bandages?.Copy()))
            await bandage.forceMove(drop_loc);
        if (num_bandages > 0)
            Dm.@new<obj_item_stack_medical_wrap_gauze>(drop_loc, num_bandages);
        return null;
    }

    // tg: code/game/machinery/wall_healer.dm:406
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_stack_medical_wrap_gauze bandage)
            return NONE;
        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
        {
            to_chat(user, span_warning(I18n.T("You try to restock [] with [], but it seems stuck to your hand.", this, tool)));
            return ITEM_INTERACT_BLOCKING;
        }
        user.visible_message(
            span_notice(I18n.T("[] restocks [] with [].", user, this, tool)),
            span_notice(I18n.T("You restock [] with [].", this, tool)),
            span_hear(I18n.T("You hear a click.")),
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
            vision_distance: 5);
        while (Dm.num(bandage.amount) > 1)
        {
            var split_bandage = (obj_item_stack_medical_wrap_gauze)await bandage.split_stack(1);
            LAZYADD(ref stocked_bandages, split_bandage);
            await split_bandage.forceMove(this);
        }
        LAZYADD(ref stocked_bandages, bandage);
        await bandage.forceMove(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/wall_healer.dm:428
    public virtual void set_using_mob(mob_living user)
    {
        if (!Dm.eq(last_user_ref, REF(user)))
            injection_cooldown = 0; // COOLDOWN_RESET(src, injection_cooldown)

        antispam_counter = 0;
        last_user_ref = null;

        current_user = user;
        RegisterSignal(user, COMSIG_MOVABLE_MOVED, nameof(user_moved));
        RegisterSignal(user, COMSIG_QDELETING, nameof(clear_using_mob));

        current_hand = user.get_active_hand() as obj_item_bodypart;
        if (current_hand != null)
            RegisterSignals(current_hand, Dm.list(COMSIG_BODYPART_REMOVED, COMSIG_QDELETING), nameof(clear_using_mob));

        // COOLDOWN_TIMELEFT(src, injection_cooldown)
        injection_bar = Dm.@new<datum_progressbar_wall_healer>(user, injection_cd_length, this, Math.Max(0, injection_cooldown - world.time));
    }

    // tg: code/game/machinery/wall_healer.dm:445
    [SignalHandler]
    public virtual void clear_using_mob(params object[] __extra)
    {
        if (current_hand != null)
        {
            UnregisterSignal(current_hand, COMSIG_BODYPART_REMOVED);
            UnregisterSignal(current_hand, COMSIG_QDELETING);
            current_hand = null;
        }
        if (current_user != null)
        {
            last_user_ref = REF(current_user);
            UnregisterSignal(current_user, COMSIG_MOVABLE_MOVED);
            UnregisterSignal(current_user, COMSIG_QDELETING);
            UnregisterSignal(current_user, COMSIG_CARBON_REMOVE_LIMB);
            current_user = null;
        }
        QDEL_NULL(ref injection_bar);
    }

    // tg: code/game/machinery/wall_healer.dm:459
    [SignalHandler]
    public virtual void user_moved(params object[] __extra)
    {
        if (ReferenceEquals(current_user.loc, loc))
            return;
        if (!QDELING(current_user))
        {
            current_user.visible_message(
                span_notice(I18n.T("[] removes [] hand from [].", current_user, current_user.p_their(), this)),
                span_notice(I18n.T("You remove your hand from [].", this)),
                span_hear(I18n.T("You hear a click.")),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
                vision_distance: 5);
        }
        clear_using_mob();
    }

    // tg: code/game/machinery/wall_healer.dm:473
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (current_user != null && !ReferenceEquals(current_user.loc, loc))
            clear_using_mob();
        return dot;
    }

    // tg: code/game/machinery/wall_healer.dm:478
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        LAZYREMOVE(ref stocked_bandages, gone);
        return dot;
    }

    /// Checks if the machine is free for the given mob
    // tg: code/game/machinery/wall_healer.dm:483
    public virtual bool is_free(mob_living for_who)
    {
        if (Dm.num(SSsecurity_level.get_current_level_as_number()) >= SEC_LEVEL_RED)
            return true; // always free on red alert
        if (for_who == null)
            return false;
        var card = for_who.get_idcard(TRUE) as obj_item_card_id;
        if (card?.registered_account?.account_job != null && Dm.eq(card.registered_account.account_job.paycheck_department, payment_department))
            return true; // free for doctors
        return false;
    }

    // tg: code/game/machinery/wall_healer.dm:493
    public override object attempt_charge(atom sender = null, atom target = null, float? extra_fees = null)
    {
        if (is_free(target as mob_living))
            return NONE;
        return base.attempt_charge(sender, target, extra_fees);
    }

    // tg: code/game/machinery/wall_healer.dm:498
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process__body(dot), dot);
    }

    private async DmTask<object> process__body(DotBox<object> dot)
    {
        if (!Dm.truthy(is_operational))
        {
            // puts off recharging until operational again
            recharge_cooldown = world.time + recharge_cd_length * 0.5f; // COOLDOWN_START
            return dot.Value;
        }
        if (current_user == null)
        {
            if (recharge_cooldown <= world.time /* COOLDOWN_FINISHED */ && Dm.num(refill_healing_pool(10)) > 0)
            {
                recharge_cooldown = world.time + recharge_cd_length; // COOLDOWN_START
                playsound(this, Dm.resource("sound/machines/defib/defib_ready.ogg"), 50, FALSE, SHORT_RANGE_SOUND_EXTRARANGE);
            }
            return dot.Value;
        }

        if (!(injection_cooldown <= world.time)) // COOLDOWN_FINISHED
            return dot.Value;

        injection_cooldown = world.time + injection_cd_length; // COOLDOWN_START
        antispam_counter++;

        bool arm_check = current_hand == null
            ? Dm.truthy(Dm.bitand(current_user.mob_biotypes, MOB_ORGANIC))
            : Dm.truthy(IS_ORGANIC_LIMB(current_hand));
        if (!arm_check)
        {
            playsound(this, Dm.resource("sound/machines/defib/defib_saftyOff.ogg"), 50, FALSE, SHORT_RANGE_SOUND_EXTRARANGE);
            if (antispam_counter % 3 == 1)
                to_chat(current_user, span_notice(I18n.T("Nothing happens. Seems [] doesn't recognize non-organic [].", this, current_hand != null ? I18n.T("limbs") : I18n.T("beings"))));
            return dot.Value;
        }

        if (!Dm.truthy(current_user.can_inject(null, current_hand)))
        {
            playsound(this, Dm.resource("sound/machines/defib/defib_saftyOff.ogg"), 50, FALSE, SHORT_RANGE_SOUND_EXTRARANGE);
            if (antispam_counter % 3 == 1)
                to_chat(current_user, span_notice(I18n.T("Nothing happens. Seems [] can't find any exposed flesh to work on.", this)));
            return dot.Value;
        }

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            await current_user.apply_damage(33, BRUTE, current_hand, sharpness: SHARP_POINTY);
            playsound(this, Dm.resource("sound/machines/defib/defib_failed.ogg"), 50, FALSE, SHORT_RANGE_SOUND_EXTRARANGE);
            if (antispam_counter % 2 == 1)
                to_chat(current_user, span_warning(I18n.T("You feel a sharp pain as the machine malfunctions, stabbing you with several instruments and needles!")));
            await use_energy(500 * JOULES);
            await add_mob_blood(current_user);
            return dot.Value;
        }

        float brute_healing_now = Dm.round(Math.Min(Math.Min(Dm.initial(this, (obj_machinery_wall_healer p) => p.brute_healing) * 0.1f, brute_healing), Dm.num(current_user.get_brute_loss())), DAMAGE_PRECISION);
        float burn_healing_now = Dm.round(Math.Min(Math.Min(Dm.initial(this, (obj_machinery_wall_healer p) => p.burn_healing) * 0.1f, burn_healing), Dm.num(current_user.get_fire_loss())), DAMAGE_PRECISION);
        float tox_healing_now = Dm.round(Math.Min(Math.Min(Dm.initial(this, (obj_machinery_wall_healer p) => p.tox_healing) * 0.1f, tox_healing), Dm.num(current_user.get_tox_loss())), DAMAGE_PRECISION);
        float blood_healing_now = Dm.round(Math.Min(Math.Min(Dm.initial(this, (obj_machinery_wall_healer p) => p.blood_healing) * 0.1f, blood_healing), Math.Max(0, BLOOD_VOLUME_OKAY - Dm.num(current_user.get_blood_volume()))), 0.1f);

        float cost = Dm.round(per_heal_cost * (brute_healing_now + burn_healing_now + tox_healing_now + blood_healing_now), 1);
        if (Dm.truthy(Dm.bitand(attempt_charge(this, current_user, extra_fees: cost), COMPONENT_OBJ_CANCEL_CHARGE)))
        {
            playsound(this, Dm.resource("sound/machines/defib/defib_saftyOff.ogg"), 50, FALSE, SHORT_RANGE_SOUND_EXTRARANGE);
            // attempt charge sends a chat message on fail, except if the user has no ID card
            if (antispam_counter % 3 == 1 && !Dm.truthy(current_user.get_idcard()))
                to_chat(current_user, span_warning(I18n.T("No ID card found. Aborting.")));
            return dot.Value;
        }

        float amount_healed = 0;
        if (brute_healing_now != 0)
        {
            amount_healed += Dm.num(await current_user.adjust_brute_loss(-brute_healing_now, required_bodytype: BODYTYPE_ORGANIC));
            brute_healing -= brute_healing_now;
            await add_mob_blood(current_user);
        }
        if (burn_healing_now != 0)
        {
            amount_healed += Dm.num(await current_user.adjust_fire_loss(-burn_healing_now, required_bodytype: BODYTYPE_ORGANIC));
            burn_healing -= burn_healing_now;
        }
        if (tox_healing_now != 0)
        {
            amount_healed += Dm.num(await current_user.adjust_tox_loss(-tox_healing_now, required_biotype: MOB_ORGANIC));
            tox_healing -= tox_healing_now;
        }
        if (blood_healing_now != 0)
        {
            amount_healed += Dm.num(current_user.adjust_blood_volume(blood_healing_now, maximum: BLOOD_VOLUME_OKAY));
            blood_healing -= blood_healing_now;
            await add_mob_blood(current_user);
        }

        if (amount_healed != 0)
        {
            playsound(this, Dm.resource("sound/machines/defib/defib_SaftyOn.ogg"), 50, FALSE, SHORT_RANGE_SOUND_EXTRARANGE);
            if (antispam_counter % 2 == 1)
                to_chat(current_user, span_notice(I18n.T("Several instruments and syringes work on your []. You feel a bit better.", current_hand?.plaintext_zone ?? I18n.T("body"))));
            update_appearance();
            await use_energy(200 * JOULES); // just some background power drain. we don't really care about whether this is actually successful
            return dot.Value;
        }

        playsound(this, Dm.resource("sound/machines/defib/defib_saftyOff.ogg"), 50, FALSE, SHORT_RANGE_SOUND_EXTRARANGE);
        if (antispam_counter % 3 != 1)
            return dot.Value;
        bool missed_brute_healing = brute_healing_now > 0 && !Dm.truthy(current_user.get_brute_loss());
        bool missed_burn_healing = burn_healing_now > 0 && !Dm.truthy(current_user.get_fire_loss());
        bool missed_tox_healing = tox_healing_now > 0 && !Dm.truthy(current_user.get_tox_loss());
        bool missed_blood_healing = blood_healing_now > 0 && Dm.num(current_user.get_blood_volume()) >= BLOOD_VOLUME_OKAY;
        if (missed_brute_healing || missed_burn_healing || missed_tox_healing || missed_blood_healing)
        {
            to_chat(current_user, span_notice(I18n.T("Nothing happens. Seems like [] needs to recharge.", this)));
            return dot.Value;
        }
        to_chat(current_user, span_notice(I18n.T("Nothing happens. Seems like you're in good enough shape.")));
        return dot.Value;
    }
}

/// Subtype of progress bar used by the wall healer to show time until next injection
/// This subtype only exists so we can shove fastprocess processing off of the machine itself
// tg: code/game/machinery/wall_healer.dm:588
public partial class datum_progressbar_wall_healer : datum_progressbar
{
    // tg: code/game/machinery/wall_healer.dm:590
    [DmParams("User", "goal_number", "target", "starting_amount")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        START_PROCESSING(SSfastprocess, this);
    }

    // tg: code/game/machinery/wall_healer.dm:594
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/wall_healer.dm:598
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (bar_loc is not obj_machinery_wall_healer healer)
        {
            stack_trace(Dm.text("[] instantiated on a non-wall-healer target [] ([])", GetType(), bar_loc != null ? bar_loc : "null", bar_loc != null ? bar_loc.GetType() : "null"), "code/game/machinery/wall_healer.dm", 601);
            return PROCESS_KILL;
        }

        // COOLDOWN_FINISHED(healer, injection_cooldown) ? 0 : COOLDOWN_TIMELEFT(healer, injection_cooldown)
        update(healer.injection_cooldown <= world.time ? 0 : healer.injection_cooldown - world.time);
        return null;
    }
}

// tg: code/game/machinery/wall_healer.dm:608
public partial class obj_machinery_wall_healer_free : obj_machinery_wall_healer
{
    // tg: code/game/machinery/wall_healer.dm:614
    public override async DmTask init_payment()
    {
        await DmTask.CompletedTask;
    }
}
