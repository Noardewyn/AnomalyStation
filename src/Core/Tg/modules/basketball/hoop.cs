// Translated from tgstation (snapshot 1d99bd7f): code/modules/basketball/hoop.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps ST2 (D333): the whole file (the basketball hoop: dunking an item or an aggressively grabbed mob into it, throws scored by the
// distance and the aim, the two- and three-pointers on its scoreboard of tg's day-counter digits beside the backboard, turned by a
// wrench, the score reset by a ctrl-click; the minigame's hoops that refuse their own team's balls). The minigame itself
// (controller.dm, its stadium maps, teams and referee) is not ported: not-translated.md 204. The digits' emissive copies are not made
// (DV12). The crafting recipe is var-only. The var blocks are generated (D111).

// tg: code/modules/basketball/hoop.dm:12
public partial class obj_structure_hoop : obj_structure
{
    // tg: code/modules/basketball/hoop.dm:1  #define PICKUP_RESTRICTION_TIME 3 SECONDS (#undef'd at its end)
    internal const float PICKUP_RESTRICTION_TIME = 3 * SECONDS; // so other players can pickup the ball after someone scores

    /// The chance to score a ball into the hoop based on distance
    // tg: code/modules/basketball/hoop.dm:25  var/static/list/throw_range_success
    public static ref DmList throw_range_success => ref TgState.Current.obj_structure_hoop__throw_range_success;

    // tg: code/modules/basketball/hoop.dm:27
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation), Dm.bitor(ROTATION_REQUIRE_WRENCH, ROTATION_IGNORE_ANCHORED), Dm.named("post_rotation_proccall", nameof(post_rotation))));
        update_appearance();
        register_context();
        return dot;
    }

    // tg: code/modules/basketball/hoop.dm:33
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        context[SCREENTIP_CONTEXT_CTRL_LMB] = "Reset score";
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/modules/basketball/hoop.dm:37
    public virtual void post_rotation(mob user = null, object degrees = null)
    {
        update_appearance();
    }

    // tg: code/modules/basketball/hoop.dm:40
    public virtual object score(obj_item_toy_basketball ball = null, mob_living baller = null, object points = null)
    {
        // we still play buzzer sound regardless of the object
        playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 100, FALSE);

        if (ball == null)
            return null;

        total_score += Dm.num(points);
        update_appearance();
        // whoever scored doesn't get to pickup the ball instantly
        ball.pickup_cooldown = world.time + PICKUP_RESTRICTION_TIME; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline (rules 11.1 (a))

        Dm.combine(ref ball.pickup_restriction_ckeys, baller.ckey);
        return TRUE;
    }

    // tg: code/modules/basketball/hoop.dm:55
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        float dir_offset_w = 0;
        float dir_offset_z = 0;

        switch (Dm.num(dir))
        {
            case NORTH:
                dir_offset_z = -32;
                break;
            case SOUTH:
                dir_offset_z = 32;
                break;
            case EAST:
                dir_offset_w = -32;
                break;
            case WEST:
                dir_offset_w = 32;
                break;
        }

        mutable_appearance scoreboard = mutable_appearance(Dm.resource("icons/obj/signs.dmi"), "basketball_scorecard") as mutable_appearance;
        scoreboard.pixel_w = dir_offset_w;
        scoreboard.pixel_z = dir_offset_z;
        Dm.append(ref dot, scoreboard);

        float ones = Dm.mod(total_score, 10);
        mutable_appearance ones_overlay = mutable_appearance(Dm.resource("icons/obj/signs.dmi"), Dm.text("days_[]", ones), layer + 0.01f) as mutable_appearance;
        ones_overlay.pixel_w = 4;
        // DEVIATION(DV12): var/mutable_appearance/emissive_ones_overlay = emissive_appearance('icons/obj/signs.dmi', "days_[ones]", src,
        // alpha = src.alpha), pixel_w 4, the digit's glow on the emissive plane, is not created
        // scoreboard.add_overlay(ones_overlay): /image/proc/add_overlay(x) is `overlays |= x` (controllers/subsystem/overlays.dm:113)
        Dm.combine(ref scoreboard.overlays, ones_overlay);

        float tens = Dm.mod(total_score / 10, 10);
        mutable_appearance tens_overlay = mutable_appearance(Dm.resource("icons/obj/signs.dmi"), Dm.text("days_[]", tens), layer + 0.01f) as mutable_appearance;
        tens_overlay.pixel_w = -5;

        // DEVIATION(DV12): emissive_tens_overlay (pixel_w -5), the tens digit's glow, is not created
        Dm.combine(ref scoreboard.overlays, tens_overlay); // scoreboard.add_overlay(tens_overlay), as the ones
        return dot;
    }

    // tg: code/modules/basketball/hoop.dm:93
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await user.can_perform_action(this, Dm.bitor(NEED_HANDS, FORBID_TELEKINESIS_REACH))))
            return NONE; // TK users aren't allowed to dunk

        if (!Dm.truthy(await user.transfer_item_to_turf(tool, drop_location() as turf)))
            return ITEM_INTERACT_BLOCKING;

        float dunk_dir = get_dir(user, this);

        float dunk_pixel_z = Dm.truthy(Dm.bitand(dunk_dir, SOUTH)) ? -16 : 16;
        float dunk_pixel_w = Dm.truthy(Dm.bitand(dunk_dir, EAST)) ? 16 : (Dm.truthy(Dm.bitand(dunk_dir, WEST)) ? -16 : 0);

        animate(user, Dm.named("pixel_w", dunk_pixel_w), Dm.named("pixel_z", dunk_pixel_z), Dm.named("time", 0.5f * SECONDS), Dm.named("easing", Dm.bitor(BOUNCE_EASING, Dm.bitor(EASE_IN, EASE_OUT))), Dm.named("flags", Dm.bitor(ANIMATION_PARALLEL, ANIMATION_RELATIVE)));
        animate(Dm.named("pixel_w", -dunk_pixel_w), Dm.named("pixel_z", -dunk_pixel_z), Dm.named("time", 0.5f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));

        visible_message(span_warning(I18n.T("[] dunks [] into \\the []!", user, tool, this)));
        await user.add_mood_event("basketball", typeof(datum_mood_event_basketball_dunk));
        score(tool as obj_item_toy_basketball, user, 2);

        if (istype(tool, typeof(obj_item_toy_basketball)))
            await user.adjust_stamina_loss(STAMINA_COST_DUNKING);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/basketball/hoop.dm:116
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        mob_living baller = (mob_living)user;
        if (!(baller.pulling != null && isliving(baller.pulling)))
            return await base.attack_hand(user, modifiers, __extra);

        mob_living loser = (mob_living)baller.pulling;
        if (Dm.num(baller.grab_state) < GRAB_AGGRESSIVE)
        {
            to_chat(baller, span_warning(I18n.T("You need a better grip to do that!")));
            return dot;
        }
        await loser.forceMove(loc as atom);
        await loser.Paralyze(100);
        visible_message(span_danger(I18n.T("[] dunks [] into \\the []!", baller, loser, this)));
        playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 100, FALSE);
        await baller.adjust_stamina_loss(STAMINA_COST_DUNKING_MOB);
        await baller.stop_pulling();
        return dot;
    }

    // tg: code/modules/basketball/hoop.dm:135
    public override async DmTask<object> click_ctrl(mob user = null)
    {
        user.balloon_alert_to_viewers(I18n.T("resetting score..."));
        playsound(this, Dm.resource("sound/machines/locktoggle.ogg"), 50, TRUE);
        if (Dm.truthy(await do_after(user, 5 * SECONDS, target: this)))
        {
            total_score = 0;
            update_appearance();
        }
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/basketball/hoop.dm:143
    public override async DmTask<object> hitby(atom_movable AM = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        if (!isitem(AM))
            return await base.hitby(AM, skipcatch, hitpush, blocked, throwingdatum);

        float distance = clamp(Dm.num(throwingdatum.dist_travelled) + 1, 1, throw_range_success.len);
        float score_chance = Dm.num(throw_range_success[distance]);
        obj_structure_hoop backboard = throwingdatum.initial_target?.resolve() as obj_structure_hoop;
        bool click_on_hoop = true;
        mob_living thrower = throwingdatum?.get_thrower() as mob_living;

        if (thrower == null)
            return null;

        // aim penalty for not clicking directly on the hoop when shooting
        if (backboard == null || backboard != this)
        {
            click_on_hoop = false;
            score_chance *= 0.5f;
        }

        // aim penalty for spinning while shooting
        if (HAS_TRAIT(thrower, TRAIT_SPINNING))
            score_chance *= 0.5f;

        if (prob(score_chance))
        {
            await AM.forceMove(get_turf(this) as atom);
            // is it a 3 pointer shot
            float points = (distance > 2) ? 3 : 2;
            await thrower.add_mood_event("basketball", typeof(datum_mood_event_basketball_score));
            score(AM as obj_item_toy_basketball, thrower, points);
            visible_message(span_warning(I18n.T("[] [] lands in [].", click_on_hoop ? I18n.T("Swish!") : "", AM, this)));
        }
        else
            visible_message(span_danger(click_on_hoop ? I18n.T("[] bounces off of []'s rim!", AM, this) : I18n.T("[] bounces off of []'s backboard!", AM, this)));
        return null;
    }
}

// Special hoops for the minigame
// tg: code/modules/basketball/hoop.dm:176
public partial class obj_structure_hoop_minigame : obj_structure_hoop
{
    // tg: code/modules/basketball/hoop.dm:180
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        return NONE;
    }

    // No resetting the score for minigame hoops
    // tg: code/modules/basketball/hoop.dm:184
    public override async DmTask<object> click_ctrl(mob user = null)
    {
        await DmTask.CompletedTask;
        return CLICK_ACTION_BLOCKING;
    }

    // tg: code/modules/basketball/hoop.dm:187
    public override object score(obj_item_toy_basketball ball = null, mob_living baller = null, object points = null)
    {
        bool is_team_hoop = !Dm.@in(baller.ckey, team_ckeys);
        if (is_team_hoop)
        {
            baller.balloon_alert_to_viewers(I18n.T("cant score own hoop!"));
            return null;
        }

        if (Dm.truthy(base.score(ball, baller, points)))
            Dm.combine(ref ball.pickup_restriction_ckeys, team_ckeys);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/basketball/hoop.dm:25
    internal DmList obj_structure_hoop__throw_range_success = Dm.list(95, 80, 65, 50, 35, 20);
}
