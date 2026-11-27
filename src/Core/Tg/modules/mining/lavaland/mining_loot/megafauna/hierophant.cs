// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// Hierophant club

// tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:3
public partial class obj_item_hierophant_club : obj_item
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        blink = Dm.@new<datum_action_innate_dash_hierophant>(this);
        update_appearance(UPDATE_OVERLAYS);

        RegisterSignals(blink, Dm.list(COMSIG_DASH_ACTION_CHARGED, COMSIG_DASH_ACTION_DASHED), nameof(on_action_updated));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:40
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref blink);
        return base.Destroy(force);
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:44
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (beacon != null)
            Dm.append(ref dot, span_hierophant_warning(I18n.T("The beacon is currently detached.")));
        else
            Dm.append(ref dot, span_hierophant_warning(I18n.T("There is a beacon attached at the back end of the handle.")));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:51
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        await blink.Grant(user, this);
        await user.update_icons();
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:56
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        await blink.Remove(user);
        await user.update_icons();
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:61
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        await say(I18n.T("Xverwpsgexmrk..."), forced: "hierophant club suicide");
        user.visible_message(span_suicide(I18n.T("[] holds [] into the air! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        Dm.@new<obj_effect_temp_visual_hierophant_telegraph>(get_turf(user));
        playsound(user, Dm.resource("sound/machines/airlock/airlockopen.ogg"), 75, TRUE);
        user.visible_message(span_hierophant_warning(I18n.T("[] fades out, leaving [] belongings behind!", user, user.p_their())));
        foreach (obj_item user_item in Dm.each_cast<obj_item>(user.get_all_gear(FALSE, FALSE)))
            await user.dropItemToGround(user_item);
        foreach (turf blast_turf in Dm.each_cast<turf>(RANGE_TURFS(1, user)))
            Dm.@new<obj_effect_temp_visual_hierophant_blast_visual>(blast_turf, user, TRUE);
        await user.dropItemToGround(this); //Drop us last, so it goes on top of their stuff
        qdel(user);
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:74
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        blink_activated = Dm.truthy(blink_activated) ? FALSE : TRUE;
        balloon_alert(user, Dm.truthy(blink_activated) ? I18n.T("blinking enabled") : I18n.T("blinking disabled"));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:79
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        // If our target is the beacon and the hierostaff is next to the beacon, we're trying to pick it up.
        if (interacting_with == beacon || !isturf(interacting_with.loc))
            return NONE;

        if (!Dm.truthy(blink_activated))
            return NONE;

        if (Dm.truthy(await blink.teleport(user, interacting_with)))
            return ITEM_INTERACT_SUCCESS;
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:91
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(blink_activated))
        {
            await blink.teleport(user, interacting_with);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    /// When dash action is used or recharges, update icon state
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:98
    [SignalHandler]
    public virtual void on_action_updated()
    {
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:102
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (QDELETED(beacon))
            Dm.append(ref dot, "hierophant_beacon");
        if (Dm.truthy(blink?.current_charges))
            Dm.append(ref dot, "hierophant_ready");
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:109
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        object dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape);
        if (Dm.truthy(blink?.current_charges))
            Dm.append(ref dot, "hierophant_ready");
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:115
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (Dm.truthy(teleporting))
        {
            balloon_alert(user, I18n.T("already in use!"));
            return null;
        }

        if (!Dm.truthy(user.is_holding(this)))
        {
            to_chat(user, span_warning(beacon != null ? I18n.T("You need to hold the club in your hands to teleport with it!") : I18n.T("You need to hold the club in your hands to detach the beacon!")));
            return null;
        }

        if (beacon == null)
        {
            await deploy_beacon(user);
            return null;
        }

        if (get_dist(user, beacon) <= 2)
        {
            balloon_alert(user, I18n.T("too close to the beacon!"));
            return null;
        }

        turf beacon_turf = get_turf(beacon);
        if (beacon_turf == null || Dm.truthy(await beacon_turf.is_blocked_turf(TRUE)))
        {
            balloon_alert(user, I18n.T("the beacon is blocked!"));
            return null;
        }

        if (!isturf(user.loc))
        {
            balloon_alert(user, I18n.T("not enough room to teleport!"));
            return null;
        }

        turf user_turf = get_turf(user);
        teleporting = TRUE;
        await user.update_mob_action_buttons();
        user.visible_message(span_hierophant_warning(I18n.T("[] starts to glow faintly...", user)), span_hierophant_warning(I18n.T("You begin channeling []'s power...", this)));
        beacon.icon_state = "hierophant_tele_on";
        obj_effect_temp_visual_hierophant_telegraph_edge user_telegraph = Dm.@new<obj_effect_temp_visual_hierophant_telegraph_edge>(user_turf);
        obj_effect_temp_visual_hierophant_telegraph_edge beacon_telegraph = Dm.@new<obj_effect_temp_visual_hierophant_telegraph_edge>(beacon_turf);
        if (!Dm.truthy(await do_after(user, 4 * SECONDS, user)))
        {
            if (user != null)
                balloon_alert(user, I18n.T("interrupted!"));
            await stop_teleport(user);
            qdel(user_telegraph);
            qdel(beacon_telegraph);
            return null;
        }

        if (beacon == null)
        {
            balloon_alert(user, I18n.T("interrupted!"));
            await stop_teleport(user);
            return null;
        }

        if (Dm.truthy(await beacon_turf.is_blocked_turf(TRUE)))
        {
            balloon_alert(user, I18n.T("the beacon is blocked!"));
            await stop_teleport(user);
            return null;
        }

        Dm.@new<obj_effect_temp_visual_hierophant_telegraph>(user_turf, user);
        Dm.@new<obj_effect_temp_visual_hierophant_telegraph>(beacon_turf, user);
        playsound(user_turf, Dm.resource("sound/machines/airlock/airlockopen.ogg"), 200, TRUE);
        playsound(beacon_turf, Dm.resource("sound/effects/magic/wand_teleport.ogg"), 200, TRUE);

        Dm.@new<obj_effect_temp_visual_hierophant_telegraph_teleport>(user_turf, user);
        Dm.@new<obj_effect_temp_visual_hierophant_telegraph_teleport>(beacon_turf, user);

        foreach (turf turf_near_user_turf in Dm.each_cast<turf>(RANGE_TURFS(1, user_turf)))
            Dm.@new<obj_effect_temp_visual_hierophant_blast_visual>(turf_near_user_turf, user, TRUE);

        foreach (turf turf_near_beacon in Dm.each_cast<turf>(RANGE_TURFS(1, beacon_turf)))
            Dm.@new<obj_effect_temp_visual_hierophant_blast_visual>(turf_near_beacon, user, TRUE);

        foreach (mob_living victim in Dm.each<mob_living>(range(1, user_turf)))
            Waiting.Detach(teleport_mob(user_turf, beacon_turf, victim, user)); // INVOKE_ASYNC

        _addtimer(Dm.@new<datum_callback>(this, nameof(stop_teleport), user), 0.6f * SECONDS, file: "code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm", line: 183);
        return null;
    }

    /// Just to cut down on copypasta
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:186
    public virtual async DmTask stop_teleport(mob user = null)
    {
        teleporting = FALSE;
        if (beacon != null)
            beacon.icon_state = "hierophant_tele_off";
        if (user != null)
            await user.update_mob_action_buttons();
    }

    /// Teleports mobs after a short animation
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:194
    public virtual async DmTask teleport_mob(turf user_turf = null, turf beacon_turf = null, mob victim = null, mob user = null)
    {
        turf target_turf = get_step(beacon_turf, get_dir(user_turf, victim));
        if (target_turf == null || Dm.truthy(await target_turf.is_blocked_turf(TRUE)))
            return;
        animate(victim, Dm.named("alpha", 0), Dm.named("time", 0.2f * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)));
        await sleep(0.2f * SECONDS);
        victim.visible_message(span_hierophant_warning(I18n.T("[] fades out!", victim)));
        object success = await do_teleport(victim, target_turf, no_effects: TRUE, channel: TELEPORT_CHANNEL_MAGIC);
        animate(victim, Dm.named("alpha", 255), Dm.named("time", 0.2f * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)));
        victim.visible_message(span_hierophant_warning(I18n.T("[] fades in!", victim)));
        if (user != victim && Dm.truthy(success))
            log_combat(user, victim, "teleported", null, Dm.text("from []", AREACOORD(user_turf)));
    }

    /// Attempts to place a return beacon at user's feet
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:208
    public virtual async DmTask deploy_beacon(mob user = null)
    {
        if (!isopenturf(user.loc) && !isopenspaceturf(user.loc))
        {
            to_chat(user, span_warning(I18n.T("You need to be on solid ground to detach the beacon!")));
            return;
        }

        user.visible_message(span_hierophant_warning(I18n.T("[] starts fiddling with []'s pommel...", user, this)), span_notice(I18n.T("You start detaching the hierophant beacon...")));
        balloon_alert(user, I18n.T("detaching the beacon..."));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, user)))
        {
            balloon_alert(user, I18n.T("interrupted!"));
            return;
        }

        // Already dropped one
        if (beacon != null)
            return;

        turf user_turf = get_turf(user);
        Dm.@new<obj_effect_temp_visual_hierophant_telegraph_teleport>(user_turf, user);
        beacon = Dm.@new<obj_effect_hierophant>(user_turf);
        playsound(beacon, Dm.resource("sound/effects/magic/blind.ogg"), 200, TRUE, -4);
        RegisterSignal(beacon, COMSIG_QDELETING, nameof(beacon_destroyed));

        await user.update_mob_action_buttons();
        user.visible_message(span_hierophant_warning(I18n.T("[] places a strange machine beneath [] feet!", user, user.p_their())), span_hierophant(I18n.T("You detach the hierophant beacon, allowing you to teleport yourself and any allies to it at any time!")));
        to_chat(user, span_hierophant(I18n.T("You can remove the beacon to place it again by striking it with the club.")));
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:234
    [SignalHandler]
    public virtual void beacon_destroyed(datum source = null)
    {
        beacon = null;
        if (ismob(loc))
            to_chat(loc, span_hierophant(I18n.T("With a loud snap, a new beacon appears at []'s pommel.", this)));
        else
            visible_message(span_hierophant(I18n.T("With a loud snap, a new beacon appears at []'s pommel.", this)));
        playsound(this, Dm.resource("sound/effects/magic/blind.ogg"), 50, TRUE, -4);
        update_appearance(UPDATE_OVERLAYS);
    }
}

// tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:246
public partial class datum_action_innate_dash_hierophant : datum_action_innate_dash
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:243  #define HIEROPHANT_BLINK_RANGE 5 (file-local)
    private const float HIEROPHANT_BLINK_RANGE = 5;

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:257
    public override async DmTask<object> teleport(mob user = null, atom target = null)
    {
        float dist = get_dist(user, target);
        if (dist > HIEROPHANT_BLINK_RANGE)
        {
            user.balloon_alert(user, I18n.T("too far!"));
            return FALSE;
        }

        return await base.teleport(user, target);
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/hierophant.dm:265
    public override async DmTask charge()
    {
        await base.charge();
        if (target is obj_item_hierophant_club club)
            club.update_appearance(UPDATE_ICON_STATE);
    }
}
