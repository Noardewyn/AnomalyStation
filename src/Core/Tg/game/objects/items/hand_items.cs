// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/hand_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: /obj/item/hand_item's own procs (the self grasp, carbon_defense.dm:556, is one) and, since M9 slice F, the emote
// hand items (the circle game, the noogie, the slapper, the hand, the stealer, the kissers) and the blown kisses (with their memories since
// gaps package G4) and their moods (since gaps MS1). The achievements are owner-deferred (D332).

// tg: code/game/objects/items/hand_items.dm:2
[DmPath("/obj/item/hand_item")]
public partial class obj_item_hand_item : obj_item
{
    // tg: code/game/objects/items/hand_items.dm:9
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_STORAGE_INSERT, TRAIT_GENERIC);
        return dot;
    }

    // tg: code/game/objects/items/hand_items.dm:13
    [DmParams("target_mob", "user")]
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (Dm.truthy(user._listen_lookup?[COMSIG_LIVING_HAND_ITEM_ATTACK]))
            user._SendSignal(COMSIG_LIVING_HAND_ITEM_ATTACK, Dm.list(user, target_mob));
        return dot;
    }
}

// tg: code/game/objects/items/hand_items.dm:17
public partial class obj_item_hand_item_circlegame : obj_item_hand_item
{
    // tg: code/game/objects/items/hand_items.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!(loc is mob_living owner))
            return dot;
        RegisterSignal(owner, COMSIG_ATOM_EXAMINE, nameof(ownerExamined));
        return dot;
    }

    // tg: code/game/objects/items/hand_items.dm:31
    public override object Destroy(float? force = null)
    {
        if (loc is mob owner)
            UnregisterSignal(owner, COMSIG_ATOM_EXAMINE);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/hand_items.dm:37
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        UnregisterSignal(user, COMSIG_ATOM_EXAMINE); //loc will have changed by the time this is called, so Destroy() can't catch it
        // this is a dropdel item.
        return await base.dropped(user, silent, __extra);
    }

    /// Stage 1: The mistake is made
    // tg: code/game/objects/items/hand_items.dm:43
    [SignalHandler]
    public virtual void ownerExamined(mob_living owner = null, mob_living sucker = null)
    {
        if (!(sucker is mob_living) || !in_range(owner, sucker))
            return;
        _addtimer(Dm.@new<datum_callback>(this, nameof(waitASecond), owner, sucker), 0.4f * SECONDS, file: "code/game/objects/items/hand_items.dm", line: 48);
    }

    /// Stage 2: Fear sets in
    // tg: code/game/objects/items/hand_items.dm:51
    public virtual void waitASecond(mob_living owner = null, mob_living sucker = null)
    {
        if (QDELETED(sucker) || QDELETED(this) || QDELETED(owner))
            return;

        if (owner == sucker) // big mood
        {
            to_chat(owner, span_danger(I18n.T("Wait a second... you just looked at your own []!", this.name)));
            _addtimer(Dm.@new<datum_callback>(this, nameof(selfGottem), owner), 1 * SECONDS, file: "code/game/objects/items/hand_items.dm", line: 57);
        }
        else
        {
            to_chat(sucker, span_danger(I18n.T("Wait a second... was that a-")));
            _addtimer(Dm.@new<datum_callback>(this, nameof(GOTTEM), owner, sucker), 0.6f * SECONDS, file: "code/game/objects/items/hand_items.dm", line: 60);
        }
    }

    /// Stage 3A: We face our own failures
    // tg: code/game/objects/items/hand_items.dm:63
    public virtual async DmTask selfGottem(mob_living owner = null)
    {
        if (QDELETED(this) || QDELETED(owner))
            return;

        playsound(get_turf(owner), Dm.resource("sound/effects/hit_punch.ogg"), 50, TRUE, -1);
        owner.visible_message(span_danger(I18n.T("[] shamefully bops []self with [] [].", owner, owner.p_them(), owner.p_their(), this.name)), span_userdanger(I18n.T("You shamefully bop yourself with your [].", this.name)),
            span_hear(I18n.T("You hear a dull thud!")));
        log_combat(owner, owner, "bopped", this.name, "(self)");
        await owner.do_attack_animation(owner);
        await owner.apply_damage(100, STAMINA);
        await owner.Knockdown(10);
        qdel(this);
    }

    /// Stage 3B: We face our reckoning (unless we moved away or they're incapacitated)
    // tg: code/game/objects/items/hand_items.dm:77
    public virtual async DmTask GOTTEM(mob_living owner = null, mob_living sucker = null)
    {
        if (QDELETED(sucker))
            return;

        if (QDELETED(this) || QDELETED(owner))
        {
            to_chat(sucker, span_warning(I18n.T("Nevermind... must've been your imagination...")));
            return;
        }

        if (!in_range(owner, sucker) || !Dm.truthy(Dm.bitand(owner.mobility_flags, MOBILITY_USE)))
        {
            to_chat(sucker, span_notice(I18n.T("Phew... you moved away before [] noticed you saw [] []...", owner, owner.p_their(), this.name)));
            return;
        }

        to_chat(owner, span_warning(I18n.T("[] looks down at your [] before trying to avert [] eyes, but it's too late!", sucker, this.name, sucker.p_their())));
        to_chat(sucker, span_danger(I18n.T("<b>[] sees the fear in your eyes as you try to look away from [] []!</b>", owner, owner.p_their(), this.name)));

        owner.face_atom(sucker);
        // if(owner.client) owner.client.give_award(/datum/award/achievement/misc/gottem, owner): the achievements (M10)

        playsound(get_turf(owner), Dm.resource("sound/effects/hit_punch.ogg"), 50, TRUE, -1);
        await owner.do_attack_animation(sucker);

        if (HAS_TRAIT(owner, TRAIT_HULK))
        {
            owner.visible_message(span_danger(I18n.T("[] bops [] with [] [] much harder than intended, sending [] flying!", owner, sucker, owner.p_their(), this.name, sucker.p_them())),
                span_danger(I18n.T("You bop [] with your [] much harder than intended, sending [] flying!", sucker, this.name, sucker.p_them())), span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")), ignored_mobs: Dm.list(sucker));
            to_chat(sucker, span_userdanger(I18n.T("[] bops you incredibly hard with [] [], sending you flying!", owner, owner.p_their(), this.name)));
            await sucker.apply_damage(50, STAMINA);
            await sucker.Knockdown(50);
            log_combat(owner, sucker, "bopped", this.name, "(setup- Hulk)");
            atom throw_target = get_edge_target_turf(sucker, owner.dir) as atom;
            await sucker.throw_at(throw_target, 6, 3, owner);
        }
        else
        {
            owner.visible_message(span_danger(I18n.T("[] bops [] with [] []!", owner, sucker, owner.p_their(), this.name)), span_danger(I18n.T("You bop [] with your []!", sucker, this.name)),
                span_hear(I18n.T("You hear a dull thud!")), ignored_mobs: Dm.list(sucker));
            await sucker.apply_damage(15, STAMINA);
            log_combat(owner, sucker, "bopped", this.name, "(setup)");
            to_chat(sucker, span_userdanger(I18n.T("[] bops you with [] []!", owner, owner.p_their(), this.name)));
        }
        qdel(this);
    }
}

// tg: code/game/objects/items/hand_items.dm:117
public partial class obj_item_hand_item_noogie : obj_item_hand_item
{
    // tg: code/game/objects/items/hand_items.dm:122
    [DmParams("target", "user")]
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user_mob = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living_carbon target = target_mob as mob_living_carbon;
        mob_living_carbon_human user = user_mob as mob_living_carbon_human;
        if (!(target_mob is mob_living_carbon))
        {
            to_chat(user_mob, span_warning(I18n.T("You don't think you can give this a noogie!")));
            return null;
        }

        if (HAS_TRAIT(user_mob, TRAIT_PACIFISM))
        {
            to_chat(user_mob, span_warning(I18n.T("You can't bring yourself to noogie []! You don't want to risk harming anyone...", target)));
            return null;
        }

        if (!Dm.truthy(target?.get_bodypart(BODY_ZONE_HEAD)) || user_mob.pulling != target || user_mob.grab_state < GRAB_AGGRESSIVE || Dm.num(user_mob.get_stamina_loss()) > 80)
            return FALSE;

        obj_item_bodypart_head the_head = target.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (!Dm.truthy(Dm.bitand(the_head.biological_state, Dm.bitor(BIO_FLESH, BIO_CHITIN))))
        {
            to_chat(user_mob, span_warning(I18n.T("You can't noogie [], [] [] no skin on [] head!", target, target.p_they(), target.p_have(), target.p_their())));
            return null;
        }

        // [user] gives [target] a [prefix_desc] noogie[affix_desc]!
        float brutal_noogie = FALSE; // was it an extra hard noogie?
        string prefix_desc = "rough";
        string affix_desc = "";
        string affix_desc_target = "";

        if (HAS_TRAIT(target, TRAIT_ANTENNAE))
        {
            prefix_desc = "violent";
            affix_desc = I18n.T("on [] sensitive antennae", target.p_their());
            affix_desc_target = I18n.T("on your highly sensitive antennae");
            brutal_noogie = TRUE;
        }
        if (HAS_TRAIT(user_mob, TRAIT_HULK))
        {
            prefix_desc = "sickeningly brutal";
            brutal_noogie = TRUE;
        }

        string message_others = Dm.text("[] noogie[]", prefix_desc, affix_desc);
        string message_target = Dm.text("[] noogie[]", prefix_desc, affix_desc_target);

        user_mob.visible_message(span_danger(I18n.T("[] begins giving [] a []!", user_mob, target, message_others)), span_warning(I18n.T("You start giving [] a []!", target, message_others)), vision_distance: COMBAT_MESSAGE_RANGE, ignored_mobs: target);
        to_chat(target, span_userdanger(I18n.T("[] starts giving you a []!", user_mob, message_target)));

        if (!Dm.truthy(await do_after(user_mob, 1.5f * SECONDS, target)))
        {
            to_chat(user_mob, span_warning(I18n.T("You fail to give [] a noogie!", target)));
            to_chat(target, span_danger(I18n.T("[] fails to give you a noogie!", user_mob)));
            return null;
        }

        if (Dm.truthy(brutal_noogie))
            await target.add_mood_event("noogie_harsh", typeof(datum_mood_event_noogie_harsh));
        else
            await target.add_mood_event("noogie", typeof(datum_mood_event_noogie));

        await noogie_loop(user, target, 0);
        return null;
    }

    /// The actual meat and bones of the noogie'ing
    // tg: code/game/objects/items/hand_items.dm:173
    public virtual async DmTask<object> noogie_loop(mob_living_carbon_human user = null, mob_living_carbon target = null, float iteration = 0)
    {
        if (!Dm.truthy(target?.get_bodypart(BODY_ZONE_HEAD)) || user.pulling != target)
            return FALSE;

        if (Dm.num(user.get_stamina_loss()) > 80)
        {
            to_chat(user, span_warning(I18n.T("You're too tired to continue giving [] a noogie!", target)));
            to_chat(target, span_danger(I18n.T("[] is too tired to continue giving you a noogie!", user)));
            return null;
        }

        float damage = rand(1, 5);
        if (HAS_TRAIT(target, TRAIT_ANTENNAE))
            damage += rand(3, 7);
        if (HAS_TRAIT(user, TRAIT_HULK))
            damage += rand(3, 7);

        if (damage >= 5)
            await target.emote("scream");

        log_combat(user, target, "given a noogie to", addition: Dm.text("([] brute before armor)", damage));
        await target.apply_damage(damage, BRUTE, BODY_ZONE_HEAD);
        await user.adjust_stamina_loss(iteration + 5);
        playsound(get_turf(user), SFX_RUSTLE, 50);

        if (prob(33))
        {
            user.visible_message(span_danger(I18n.T("[] continues noogie'ing []!", user, target)), span_warning(I18n.T("You continue giving [] a noogie!", target)), vision_distance: COMBAT_MESSAGE_RANGE, ignored_mobs: target);
            to_chat(target, span_userdanger(I18n.T("[] continues giving you a noogie!", user)));
        }

        if (!Dm.truthy(await do_after(user, 1 * SECONDS + (iteration * 2), target)))
        {
            to_chat(user, span_warning(I18n.T("You fail to give [] a noogie!", target)));
            to_chat(target, span_danger(I18n.T("[] fails to give you a noogie!", user)));
            return null;
        }

        iteration++;
        return await noogie_loop(user, target, iteration);
    }
}

// tg: code/game/objects/items/hand_items.dm:209
public partial class obj_item_hand_item_slapper : obj_item_hand_item
{
    // tg: code/game/objects/items/hand_items.dm:219
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_high_fiver)));
        return dot;
    }

    // tg: code/game/objects/items/hand_items.dm:223
    [DmParams("slapped", "user")]
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user_mob = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living slapped = target_mob;
        mob_living_carbon_human user = user_mob as mob_living_carbon_human;
        object dot = await base.attack__proc(target_mob, user_mob, modifiers, attack_modifiers, __extra);
        if (Dm.truthy(user_mob._listen_lookup?[COMSIG_LIVING_SLAP_MOB]))
            user_mob._SendSignal(COMSIG_LIVING_SLAP_MOB, Dm.list(user_mob, slapped));
        if (Dm.truthy(slapped._listen_lookup?[COMSIG_LIVING_SLAPPED]))
            slapped._SendSignal(COMSIG_LIVING_SLAPPED, Dm.list(slapped, user_mob));

        if (iscarbon(slapped))
        {
            mob_living_carbon potential_tailed = slapped as mob_living_carbon;
            await potential_tailed.unwag_tail();
        }
        await user_mob.do_attack_animation(slapped);

        float slap_volume = 50;
        datum_status_effect_offering kiss_check = slapped.has_status_effect(typeof(datum_status_effect_offering)) as datum_status_effect_offering;
        if (kiss_check != null && kiss_check.offered_item is obj_item_hand_item_kisser && Dm.@in(user_mob, kiss_check.possible_takers))
        {
            user_mob.visible_message(
                span_danger(I18n.T("[] scoffs at []'s advance, winds up, and smacks [] hard to the ground!", user_mob, slapped, slapped.p_them())),
                span_notice(I18n.T("The nerve! You wind back your hand and smack [] hard enough to knock [] over!", slapped, slapped.p_them())),
                span_hear(I18n.T("You hear someone get the everloving shit smacked out of them!")),
                ignored_mobs: slapped);
            to_chat(slapped, span_userdanger(I18n.T("You see [] scoff and pull back [] arm, then suddenly you're on the ground with an ungodly ringing in your ears!", user_mob, user_mob.p_their())));
            slap_volume = 120;
            Dm.output_to(slapped, sound(Dm.resource("sound/items/weapons/flash_ring.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
            shake_camera(slapped, 2, 2);
            await slapped.Paralyze(2.5f * SECONDS);
            await slapped.adjust_confusion(7 * SECONDS);
            await slapped.adjust_stamina_loss(40);
        }
        else if (user_mob.zone_selected == BODY_ZONE_HEAD || user_mob.zone_selected == BODY_ZONE_PRECISE_MOUTH)
        {
            if (user_mob == slapped)
            {
                user_mob.visible_message(
                    span_notice(I18n.T("[] facepalms!", user_mob)),
                    span_notice(I18n.T("You facepalm.")),
                    span_hear(I18n.T("You hear a slap.")));
            }
            else
            {
                if (IS_UNCONSCIOUS(slapped))
                {
                    user_mob.visible_message(
                        span_notice(I18n.T("[] slaps [] in the face, trying to wake [] up!", user_mob, slapped, slapped.p_them())),
                        span_notice(I18n.T("You slap [] in the face, trying to wake [] up!", slapped, slapped.p_them())),
                        span_hear(I18n.T("You hear a slap.")));

                    // Worse than just help intenting people.
                    await slapped.AdjustSleeping(-7.5f * SECONDS);
                    await slapped.AdjustUnconscious(-5 * SECONDS);
                }
                else
                {
                    user_mob.visible_message(
                        span_danger(I18n.T("[] slaps [] in the face!", user_mob, slapped)),
                        span_notice(I18n.T("You slap [] in the face!", slapped)),
                        span_hear(I18n.T("You hear a slap.")));
                }
            }
        }
        else if (user_mob.zone_selected == BODY_ZONE_L_ARM || user_mob.zone_selected == BODY_ZONE_R_ARM)
        {
            user_mob.visible_message(
                span_danger(I18n.T("[] gives [] a slap on the wrist!", user_mob, slapped)),
                span_notice(I18n.T("You give [] a slap on the wrist!", slapped)),
                span_hear(I18n.T("You hear a slap.")));
        }
        else
        {
            user_mob.visible_message(
                span_danger(I18n.T("[] slaps []!", user_mob, slapped)),
                span_notice(I18n.T("You slap []!", slapped)),
                span_hear(I18n.T("You hear a slap.")));
        }
        playsound(slapped, Dm.resource("sound/items/weapons/slap.ogg"), slap_volume, TRUE, -1);
        return null;
    }

    // tg: code/game/objects/items/hand_items.dm:290
    public override async DmTask<object> pre_attack_secondary(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!Dm.truthy(await loc.Adjacent(target)) || !(target is obj_structure_table))
            return await base.pre_attack_secondary(target, user, modifiers, attack_modifiers);

        await slam_table(target as obj_structure_table, user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/items/hand_items.dm:297
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!Dm.truthy(await loc.Adjacent(target)) || !(target is obj_structure_table))
            return await base.pre_attack(target, user, modifiers, attack_modifiers);

        await slap_table(target as obj_structure_table, user);
        return TRUE;
    }

    /// Slap the table, get some attention
    // tg: code/game/objects/items/hand_items.dm:305
    public virtual async DmTask slap_table(obj_structure_table table = null, mob_living user = null)
    {
        await user.do_attack_animation(table);
        playsound(get_turf(table), Dm.resource("sound/effects/tableslam.ogg"), 40, TRUE);
        user.visible_message(span_notice(I18n.T("[] slaps [] hand on [].", user, user.p_their(), table)), span_notice(I18n.T("You slap your hand on [].", table)), vision_distance: COMBAT_MESSAGE_RANGE);

        table_smacks_left--;
        if (table_smacks_left <= 0)
            qdel(this);
    }

    /// Slam the table, demand some attention
    // tg: code/game/objects/items/hand_items.dm:315
    public virtual async DmTask slam_table(obj_structure_table table = null, mob_living user = null)
    {
        if (table_smacks_left < Dm.num(Dm.initial(this, nameof(table_smacks_left))))
        {
            await slap_table(table, user);
            return;
        }
        await user.do_attack_animation(table);

        transform = ((matrix)transform).Scale(5); // BIG slap
        if (HAS_TRAIT(user, TRAIT_HULK))
        {
            transform = ((matrix)transform).Scale(2);
            color = COLOR_GREEN;
        }

        if (Dm.truthy(user._listen_lookup?[COMSIG_LIVING_SLAM_TABLE]))
            user._SendSignal(COMSIG_LIVING_SLAM_TABLE, Dm.list(user, table));
        if (Dm.truthy(table._listen_lookup?[COMSIG_TABLE_SLAMMED]))
            table._SendSignal(COMSIG_TABLE_SLAMMED, Dm.list(table, user));

        playsound(get_turf(table), Dm.resource("sound/effects/tableslam.ogg"), 110, TRUE);
        user.visible_message(Dm.text("<b>[]</b>", span_danger(I18n.T("[] slams [] fist down on []!", user, user.p_their(), table))), Dm.text("<b>[]</b>", span_danger(I18n.T("You slam your fist down on []!", table))));
        qdel(this);
    }

    // Successful takes will qdel our hand after
    // tg: code/game/objects/items/hand_items.dm:333
    public override async DmTask<object> on_offer_taken(mob_living offerer = null, mob_living taker = null)
    {
        object dot = await base.on_offer_taken(offerer, taker);
        if (Dm.truthy(dot))
            return dot;

        qdel(this);
        return dot;
    }
}

// tg: code/game/objects/items/hand_items.dm:341
public partial class obj_item_hand_item_hand : obj_item_hand_item
{
    // tg: code/game/objects/items/hand_items.dm:346
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!Dm.truthy(await loc.Adjacent(target)) || !(user is mob_living_carbon helper) || !(target is mob_living_carbon))
            return await base.pre_attack(target, user, modifiers, attack_modifiers);

        if (Dm.truthy(helper.resting))
        {
            to_chat(helper, span_warning(I18n.T("You can't act gentlemanly when you're lying down!")));
            return TRUE;
        }
        return null;
    }

    // tg: code/game/objects/items/hand_items.dm:355
    public override async DmTask<object> pre_attack_secondary(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!Dm.truthy(await loc.Adjacent(target)) || !(user is mob_living_carbon helper) || !(target is mob_living_carbon))
            return await base.pre_attack_secondary(target, user, modifiers, attack_modifiers);

        if (Dm.truthy(helper.resting))
        {
            to_chat(helper, span_warning(I18n.T("You can't act gentlemanly when you're lying down!")));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }

        return SECONDARY_ATTACK_CALL_NORMAL;
    }

    // tg: code/game/objects/items/hand_items.dm:366
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await loc.Adjacent(target_mob)) || !(user is mob_living_carbon) || !(target_mob is mob_living_carbon))
            return TRUE;

        await user.give(target_mob);
        return TRUE;
    }

    // tg: code/game/objects/items/hand_items.dm:374
    public override async DmTask<object> on_offered(mob_living offerer_mob = null, mob_living offered_mob = null)
    {
        object dot = TRUE;

        if (!(offerer_mob is mob_living_carbon offerer))
            return dot;

        if (Dm.eq(offerer.body_position, LYING_DOWN))
        {
            to_chat(offerer, span_warning(I18n.T("You can't act gentlemanly when you're lying down!")));
            return dot;
        }

        mob_living_carbon offered = offered_mob as mob_living_carbon;
        if (offered_mob == null)
            offered = Dm.locate<mob_living_carbon>(orange(1, offerer));

        if (offered != null && Dm.eq(offered.body_position, LYING_DOWN))
        {
            offerer.visible_message(span_notice(I18n.T("[] offers [] hand to [], looking to help them up!", offerer, offerer.p_their(), offered)),
                span_notice(I18n.T("You offer [] your hand, to try to help them up!", offered)), null, 2);

            await offerer.apply_status_effect(typeof(datum_status_effect_offering_no_item_received_needs_resting), this, typeof(atom_movable_screen_alert_give_hand_helping), offered);
            return dot;
        }

        offerer.visible_message(span_notice(I18n.T("[] extends out [] hand.", offerer, offerer.p_their())),
            span_notice(I18n.T("You extend out your hand.")), null, 2);

        await offerer.apply_status_effect(typeof(datum_status_effect_offering_no_item_received), this, typeof(atom_movable_screen_alert_give_hand));
        return dot;
    }

    // tg: code/game/objects/items/hand_items.dm:401
    public override async DmTask<object> on_offer_taken(mob_living offerer_mob = null, mob_living taker_mob = null)
    {
        object dot = await base.on_offer_taken(offerer_mob, taker_mob);
        mob_living_carbon offerer = offerer_mob as mob_living_carbon;
        mob_living_carbon taker = taker_mob as mob_living_carbon;
        if (offerer == null || taker == null)
            return TRUE; // this doesn't make sense unless both are carbons
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(taker.body_position, LYING_DOWN))
        {
            await taker.help_shake_act(offerer);

            if (Dm.eq(taker.body_position, LYING_DOWN))
                return dot; // That didn't help them. Awkwaaaaard.

            offerer.visible_message(span_notice(I18n.T("[] helps [] up!", offerer, taker)), span_nicegreen(I18n.T("You help [] up!", taker)), span_hear(I18n.T("You hear someone helping someone else up!")), ignored_mobs: taker);
            to_chat(taker, span_nicegreen(I18n.T("You take []'s hand, letting [] help your up! How nice of them!", offerer, offerer.p_them())));

            offerer.add_mob_memory(typeof(datum_memory_helped_up), Dm.named("protagonist", offerer), Dm.named("deuteragonist", taker));
            taker.add_mob_memory(typeof(datum_memory_helped_up), Dm.named("protagonist", offerer), Dm.named("deuteragonist", taker));

            await offerer.add_mood_event("helping_up", typeof(datum_mood_event_helped_up), taker, TRUE); // Different IDs because you could be helped up and then help someone else up.
            await taker.add_mood_event("helped_up", typeof(datum_mood_event_helped_up), offerer, FALSE);

            qdel(this);
            return dot;
        }

        if (Dm.truthy((taker.buckled as atom_movable)?.buckle_prevents_pull))
            return dot; // Can't start pulling them if they're buckled and that prevents pulls.

        // We do a little switcheroo to ensure that it displays the pulling message that mentions
        // taking taker by their hands.
        string offerer_zone_selected = offerer.zone_selected;
        offerer.zone_selected = "r_arm";
        object did_we_pull = await offerer.start_pulling(taker); // Will return either null or FALSE. We only want to silence FALSE.
        offerer.zone_selected = offerer_zone_selected;

        if (did_we_pull != null && Dm.eq(did_we_pull, FALSE))
            return dot; // That didn't work for one reason or the other. No need to display anything.

        to_chat(offerer, span_notice(I18n.T("[] takes your hand, allowing you to pull [] along.", taker, taker.p_them())));
        to_chat(taker, span_notice(I18n.T("You take []'s hand, which allows [] to pull you along. How polite!", offerer, offerer.p_them())));

        qdel(this);
        return dot;
    }
}

// tg: code/game/objects/items/hand_items.dm:445
public partial class obj_item_hand_item_stealer : obj_item_hand_item
{
    // tg: code/game/objects/items/hand_items.dm:452
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (!ishuman(target_mob))
            return dot;
        mob_living_carbon_human target_human = (mob_living_carbon_human)target_mob;
        if (target_human == user)
        {
            to_chat(user, span_notice(I18n.T("Why would you try stealing your own shoes?")));
            return dot;
        }
        if (!Dm.truthy(target_human.shoes))
            return dot;
        if (!Dm.eq(user.body_position, LYING_DOWN))
            return dot;
        obj_item item_to_strip = target_human.shoes as obj_item;
        user.visible_message(span_warning(I18n.T("[] starts stealing []'s []!", user, target_human, item_to_strip.name)),
            span_danger(I18n.T("You start stealing []'s []...", target_human, item_to_strip.name)));
        to_chat(target_human, span_userdanger(I18n.T("[] starts stealing your []!", user, item_to_strip.name)));
        if (!Dm.truthy(await do_after(user, item_to_strip.strip_delay, target_human)))
            return dot;
        if (!Dm.truthy(await target_human.dropItemToGround(item_to_strip)))
            return dot;
        await user.put_in_hands(item_to_strip);
        user.visible_message(span_warning(I18n.T("[] stole []'s []!", user, target_human, item_to_strip.name)),
            span_notice(I18n.T("You stole []'s []!", target_human, item_to_strip.name)));
        to_chat(target_human, span_userdanger(I18n.T("[] stole your []!", user, item_to_strip.name)));
        return dot;
    }
}

// tg: code/game/objects/items/hand_items.dm:477
public partial class obj_item_hand_item_kisser : obj_item_hand_item
{
    // tg: code/game/objects/items/hand_items.dm:488
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/hand_items.dm:491
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        atom target = interacting_with;
        obj_projectile blown_kiss = Dm.@new(kiss_type, get_turf(user)) as obj_projectile;
        user.visible_message(Dm.text("<b>[]</b> blows \\a [] at []!", user, blown_kiss, target), span_notice(I18n.T("You blow \\a [] at []!", blown_kiss, target)));

        //Shooting Code:
        blown_kiss.original = target;
        blown_kiss.fired_from = user;
        blown_kiss.firer = user; // don't hit ourself that would be really annoying
        blown_kiss.impacted = Dm.list(); // list(WEAKREF(user) = TRUE): just to make sure we don't hit the wearer
        blown_kiss.impacted[WEAKREF(user)] = TRUE;
        await blown_kiss.aim_projectile(target, user);
        await blown_kiss.fire();
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/hand_items.dm:505
    public override async DmTask<object> on_offered(mob_living offerer = null, mob_living offered = null)
    {
        if (Dm.locate<mob_living_carbon>(orange(1, offerer)) == null)
            return TRUE;

        cheek_kiss = Dm.num(offerer.zone_selected != BODY_ZONE_PRECISE_MOUTH);
        offerer.visible_message(span_notice(I18n.T("[] leans in slightly, offering a kiss[]!", offerer, Dm.truthy(cheek_kiss) ? " on the cheek" : "")),
            span_notice(I18n.T("You lean in slightly, indicating you'd like to offer a kiss[]!", Dm.truthy(cheek_kiss) ? " on the cheek" : "")), null, 2);
        await offerer.apply_status_effect(typeof(datum_status_effect_offering_no_item_received), this);
        return TRUE;
    }

    // tg: code/game/objects/items/hand_items.dm:515
    public override async DmTask<object> on_offer_taken(mob_living offerer = null, mob_living taker = null)
    {
        object dot = await base.on_offer_taken(offerer, taker);
        if (Dm.truthy(dot))
            return dot;

        obj_projectile blown_kiss = Dm.@new(kiss_type, get_turf(offerer)) as obj_projectile;
        string cheek = Dm.truthy(cheek_kiss) ? " on the cheek" : "";
        offerer.visible_message(Dm.text("<b>[]</b> gives [] \\a [][]!!", offerer, taker, blown_kiss, cheek), span_notice(I18n.T("You give [] \\a [][]!", taker, blown_kiss, cheek)), ignored_mobs: taker);
        to_chat(taker, span_nicegreen(I18n.T("[] gives you \\a [][]!", offerer, blown_kiss, cheek)));
        offerer.face_atom(taker);
        taker.face_atom(offerer);
        offerer.do_item_attack_animation(taker, used_item: this, animation_type: ATTACK_ANIMATION_BLUNT);
        //We're still firing a shot here because I don't want to deal with some weird edgecase where direct impacting them with the projectile causes it to freak out because there's no angle or something
        blown_kiss.original = taker;
        blown_kiss.fired_from = offerer;
        blown_kiss.firer = offerer; // don't hit ourself that would be really annoying
        blown_kiss.impacted = Dm.list(); // list(WEAKREF(offerer) = TRUE): just to make sure we don't hit the wearer
        blown_kiss.impacted[WEAKREF(offerer)] = TRUE;
        await blown_kiss.aim_projectile(taker, offerer);
        blown_kiss.suppressed = SUPPRESSED_VERY; // this also means it's a direct offer
        await blown_kiss.fire();
        qdel(this);
        return TRUE; // so the core offering code knows to halt
    }
}

// tg: code/game/objects/items/hand_items.dm:568
public partial class obj_projectile_kiss : obj_projectile
{
    // tg: code/game/objects/items/hand_items.dm:580
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddComponent(/datum/component/parriable_projectile): the parry (a projectile hit back by a melee swing), with the parrying weapons (M10+)
        Dm.Deferred("code/game/objects/items/hand_items.dm:582", "Initialize: AddComponent(/datum/component/parriable_projectile) (parrying, M10+)");
        return dot;
    }

    // tg: code/game/objects/items/hand_items.dm:584
    public override async DmTask fire(object fire_angle = null, atom direct_target = null)
    {
        if (firer != null && !Dm.truthy(silent_blown))
            name = Dm.text("[] blown by []", name, firer);

        await base.fire(fire_angle, direct_target);
    }

    // tg: code/game/objects/items/hand_items.dm:590
    public override async DmTask<object> impact(atom A = null)
    {
        def_zone = BODY_ZONE_HEAD; // let's keep it PG, people

        if (damage > 0 || !isliving(A)) // if we do damage or we hit a nonliving thing, we don't have to worry about a harmless hit because we can't wrongly do damage anyway
            return await base.impact(A);

        await harmless_on_hit(A as mob_living);
        qdel(this);
        return FALSE;
    }

    /**
     * To get around shielded modsuits & such being set off by kisses when they shouldn't, we take a page from hallucination projectiles
     * and simply fake our on hit effects. This lets kisses remain incorporeal without having to make some new trait for this one niche situation.
     * This fake hit only happens if we can deal damage and if we hit a living thing. Otherwise, we just do normal on hit effects.
     */
    // tg: code/game/objects/items/hand_items.dm:605
    public virtual async DmTask harmless_on_hit(mob_living living_target = null)
    {
        playsound(get_turf(living_target), hitsound, 100, TRUE);
        if (!Dm.truthy(suppressed)) // direct
            living_target.visible_message(span_danger(I18n.T("[] is hit by \\a [].", living_target, this)), span_userdanger(I18n.T("You're hit by \\a []!", this)), vision_distance: COMBAT_MESSAGE_RANGE);

        living_target.add_mob_memory(typeof(datum_memory_kissed), Dm.named("deuteragonist", firer));
        await living_target.add_mood_event("kiss", typeof(datum_mood_event_kiss), firer, suppressed);
        if (isliving(firer))
        {
            mob_living kisser = firer as mob_living;
            kisser.add_mob_memory(typeof(datum_memory_kissed), Dm.named("protagonist", living_target), Dm.named("deuteragonist", firer));
        }
        await try_fluster(living_target);
    }

    // tg: code/game/objects/items/hand_items.dm:617
    public virtual async DmTask try_fluster(mob_living living_target = null)
    {
        // people with the social anxiety quirk can get flustered when hit by a kiss
        if (!HAS_TRAIT(living_target, TRAIT_ANXIOUS) || IS_UNCONSCIOUS_OR_CRIT(living_target) || Dm.truthy(living_target.is_blind()))
            return;
        if (HAS_TRAIT(living_target, TRAIT_FEARLESS) || prob(50)) // 50% chance for it to apply, also immune while on meds
            return;

        string other_msg = null;
        string self_msg = null;
        float roll = rand(1, 3);
        switch (roll)
        {
            case 1:
                other_msg = I18n.T("stumbles slightly, turning a bright red!");
                self_msg = I18n.T("You lose control of your limbs for a moment as your blood rushes to your face, turning it bright red!");
                await living_target.adjust_confusion(rand(5 * SECONDS, 10 * SECONDS));
                break;
            case 2:
                other_msg = I18n.T("stammers softly for a moment before choking on something!");
                self_msg = I18n.T("You feel your tongue disappear down your throat as you fight to remember how to make words!");
                _addtimer(Dm.@new<datum_callback>(living_target, nameof(atom_movable.say), pick("Uhhh...", "O-oh, uhm...", "I- uhhhhh??", "You too!!", "What?")), rand(0.5f * SECONDS, 1.5f * SECONDS), file: "code/game/objects/items/hand_items.dm", line: 634);
                await living_target.adjust_stutter(rand(10 * SECONDS, 30 * SECONDS));
                break;
            case 3:
                other_msg = I18n.T("locks up with a stunned look on [] face, staring at []!", living_target.p_their(), firer != null ? firer : I18n.T("the ceiling"));
                self_msg = I18n.T("Your brain completely fails to process what just happened, leaving you rooted in place staring at [] for what feels like an eternity!", firer != null ? Dm.text("[]", firer) : I18n.T("the ceiling"));
                living_target.face_atom(firer);
                await living_target.Stun(rand(3 * SECONDS, 8 * SECONDS));
                break;
        }

        living_target.visible_message(Dm.text("<b>[]</b> []", living_target, other_msg), span_userdanger(I18n.T("Whoa! []", self_msg)));
    }

    // tg: code/game/objects/items/hand_items.dm:645
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (isliving(target))
        {
            mob_living living_target = target as mob_living;
            await living_target.add_mood_event("kiss", typeof(datum_mood_event_kiss), firer, suppressed);
            await try_fluster(living_target);
        }
        return dot;
    }
}

// tg: code/game/objects/items/hand_items.dm:653
public partial class obj_projectile_kiss_death : obj_projectile_kiss
{
    // tg: code/game/objects/items/hand_items.dm:659
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!iscarbon(target))
            return dot;
        mob_living_carbon heartbreakee = target as mob_living_carbon;
        obj_item_organ_heart dont_go_breakin_my_heart = heartbreakee.get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        await dont_go_breakin_my_heart.apply_organ_damage(999);
        return dot;
    }
}

// tg: code/game/objects/items/hand_items.dm:668
public partial class obj_projectile_kiss_ink : obj_projectile_kiss
{
    // tg: code/game/objects/items/hand_items.dm:678
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        obj_projectile_ink_spit ink_spit = Dm.@new<obj_projectile_ink_spit>(target);
        await ink_spit.on_hit(target);
        if (!QDELETED(ink_spit)) // in case it somehow remains around
            qdel(ink_spit);
        return dot;
    }
}

// tg: code/game/objects/items/hand_items.dm:700
public partial class obj_projectile_kiss_french : obj_projectile_kiss
{
    // tg: code/game/objects/items/hand_items.dm:702
    public override async DmTask harmless_on_hit(mob_living living_target = null)
    {
        await base.harmless_on_hit(living_target);
        if (living_target.reagents == null)
            return;
        //Don't stack the garlic
        if (!Dm.truthy(living_target.has_reagent(typeof(datum_reagent_consumable_garlic))))
        {
            //Phwoar
            await ((datum_reagents)living_target.reagents).add_reagent(typeof(datum_reagent_consumable_garlic), 1);
        }
        living_target.visible_message(I18n.T("[] has a funny look on [] face.", living_target, living_target.p_their()), I18n.T("Wow, that is a strong after taste of garlic!"), vision_distance: COMBAT_MESSAGE_RANGE);
    }
}

// tg: code/game/objects/items/hand_items.dm:714
public partial class obj_projectile_kiss_chef : obj_projectile_kiss
{
    // If our chef's kiss hits a food item, we will improve it with love.
    // tg: code/game/objects/items/hand_items.dm:716
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!Dm.truthy(target.GetComponent(typeof(datum_component_edible))) || target.reagents == null) // IS_EDIBLE(target), __DEFINES/is_helpers.dm
            return dot;
        if (firer == null || !Dm.truthy(await target.Adjacent(firer)) || !ismob(firer))
            return dot;

        mob kisser = firer as mob;

        // From here on, no message
        suppressed = SUPPRESSED_VERY;
        if (!(kisser.mind != null && HAS_TRAIT_FROM(target, TRAIT_HANDMADE, REF(kisser.mind))))
        {
            to_chat(firer, span_warning(I18n.T("Wait a second, you didn't make this []. How can you claim it as your own?", target.name)));
            return dot;
        }
        datum_reagents target_reagents = (datum_reagents)target.reagents;
        if (Dm.truthy(target_reagents.has_reagent(typeof(datum_reagent_love))))
        {
            to_chat(firer, span_warning(I18n.T("You've already blessed [] with your heart and soul.", target.name)));
            return dot;
        }

        float amount_nutriment = Dm.num(target_reagents.get_reagent_amount(typeof(datum_reagent_consumable_nutriment), type_check: REAGENT_PARENT_TYPE));
        if (amount_nutriment <= 0)
        {
            to_chat(firer, span_warning(I18n.T("There's not enough nutrition in [] for it to be a proper meal.", target.name)));
            return dot;
        }

        to_chat(firer, span_green(I18n.T("You deliver a chef's kiss over [], declaring it perfect.", target)));
        target.visible_message(span_notice(I18n.T("[] delivers a chef's kiss over [].", firer, target)), ignored_mobs: firer);
        await target_reagents.add_reagent(typeof(datum_reagent_love), clamp(amount_nutriment / 4, 1, 10)); // clamped to about half of the most dense food I think we have (super bite burger)
        return dot;
    }
}

