// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/fearful/effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N3b, D316: the fear system's effects). The panic attack's tunnel vision (add_fov_trait and remove_fov_trait,
// living_fov.dm) is the 2D field of view cone, not ported (the 3D view is the client's; as the eyes' FOV, not-translated.md): Deferred
// leaves. The var blocks are generated (D111).

// Terror effect handlers

public partial class datum_terror_handler
{
    /// Macro to scale a base value from 0x to ?x based on the current terror level, clamped to the base value
    // tg: code/datums/components/fearful/effects.dm:4  (a file-local macro that every handler of the file uses: rules 11.4)
    internal static float FEAR_SCALING(float terror_buildup, float @base, float min, float max) => clamp(@base * (terror_buildup - min) / (max - min), 0, @base);
}

/// Causes mild jittering, scaling with current terror level
// tg: code/datums/components/fearful/effects.dm:7
public partial class datum_terror_handler_jittering : datum_terror_handler
{
    // tg: code/datums/components/fearful/effects.dm:12
    public override async DmTask<object> tick(object seconds_per_tick = null, object terror_buildup = null)
    {
        object dot = await base.tick(seconds_per_tick, terror_buildup);
        float buildup = Dm.num(terror_buildup);
        if (IS_UNCONSCIOUS(owner))
            return dot;

        if (buildup < TERROR_BUILDUP_FEAR)
            return dot;

        // If we're terrified, keep constant jittering and dizzyness
        if (buildup > TERROR_BUILDUP_TERROR)
        {
            await owner.adjust_dizzy_up_to(10 * SECONDS * Dm.num(seconds_per_tick), 10 * SECONDS);
            await owner.adjust_jitter_up_to(10 * SECONDS * Dm.num(seconds_per_tick), 10 * SECONDS);
            return dot;
        }

        if (!SPT_PROB(1 + FEAR_SCALING(buildup, 4, TERROR_BUILDUP_FEAR, TERROR_BUILDUP_TERROR), seconds_per_tick)) // 1% to 5% chance
            return dot;

        // COOLDOWN_FINISHED(src, message_cd), inline (rules 11.1 (a))
        if (message_cd <= world.time && !Dm.truthy(owner.has_status_effect(typeof(datum_status_effect_jitter)))) // Don't display the message if we're already shaking
        {
            to_chat(owner, span_warning(I18n.T("You can't stop shaking...")));
            message_cd = world.time + TERROR_MESSAGE_CD; // COOLDOWN_START(src, message_cd, TERROR_MESSAGE_CD)
        }

        await owner.set_jitter_if_lower(20 * SECONDS);
        await owner.set_dizzy_if_lower(20 * SECONDS);
        return dot;
    }
}

/// Stutter when afraid
// tg: code/datums/components/fearful/effects.dm:37
public partial class datum_terror_handler_stuttering : datum_terror_handler
{
    // tg: code/datums/components/fearful/effects.dm:41
    public override async DmTask<object> tick(object seconds_per_tick = null, object terror_buildup = null)
    {
        object dot = await base.tick(seconds_per_tick, terror_buildup);
        float buildup = Dm.num(terror_buildup);
        if (IS_UNCONSCIOUS(owner))
            return dot;

        if (buildup < TERROR_BUILDUP_FEAR)
            return dot;

        if (buildup > TERROR_BUILDUP_TERROR || SPT_PROB(1 + FEAR_SCALING(buildup, 4, TERROR_BUILDUP_FEAR, TERROR_BUILDUP_TERROR), seconds_per_tick))
            await owner.set_stutter_if_lower(10 * SECONDS);
        return dot;
    }
}

/// Can randomly give you some oxyloss, and cause a heart attack past TERROR_BUILDUP_HEART_ATTACK
// tg: code/datums/components/fearful/effects.dm:53
public partial class datum_terror_handler_heart_problems : datum_terror_handler
{
    // tg: code/datums/components/fearful/effects.dm:58
    public override async DmTask<object> tick(object seconds_per_tick = null, object terror_buildup = null)
    {
        object dot = await base.tick(seconds_per_tick, terror_buildup);
        float buildup = Dm.num(terror_buildup);
        if (IS_UNCONSCIOUS(owner))
            return dot;

        if (buildup < TERROR_BUILDUP_FEAR)
            return dot;

        if (!SPT_PROB(1 + FEAR_SCALING(buildup, 4, TERROR_BUILDUP_FEAR, TERROR_BUILDUP_PANIC), seconds_per_tick)) // 1% to 5% chance
            return dot;

        if (buildup < TERROR_BUILDUP_HEART_ATTACK || !prob(15))
        {
            await owner.adjust_oxy_loss(8);
            if (!(message_cd <= world.time)) // COOLDOWN_FINISHED(src, message_cd)
                return dot;
            message_cd = world.time + TERROR_MESSAGE_CD; // COOLDOWN_START(src, message_cd, TERROR_MESSAGE_CD)
            if (buildup < TERROR_BUILDUP_FEAR)
                to_chat(owner, span_warning(I18n.T("Your heart skips a beat.")));
            else
                to_chat(owner, span_userdanger(I18n.T("You feel your heart lurching in your chest...")));
            return dot;
        }

        owner.visible_message(
            span_warning(I18n.T("[] clutches [] chest for a moment, then collapses to the floor.", owner, owner.p_their())),
            span_alert(I18n.T("The shadows begin to creep up from the corners of your vision, and then there is nothing...")),
            span_hear(I18n.T("You hear something heavy collide with the ground.")));
        await owner.apply_status_effect(typeof(datum_status_effect_heart_attack));
        await owner.Unconscious(20 * SECONDS);
        return dot;
    }
}

/// Low chance to vomit when terrified, increases significantly during panic attacks
// tg: code/datums/components/fearful/effects.dm:88
public partial class datum_terror_handler_vomiting : datum_terror_handler
{
    // tg: code/datums/components/fearful/effects.dm:92
    public override async DmTask<object> tick(object seconds_per_tick = null, object terror_buildup = null)
    {
        object dot = await base.tick(seconds_per_tick, terror_buildup);
        float buildup = Dm.num(terror_buildup);
        if (IS_UNCONSCIOUS(owner))
            return dot;

        if (buildup < TERROR_BUILDUP_TERROR)
            return dot;

        if (SPT_PROB((buildup >= TERROR_BUILDUP_PANIC) ? 3 : 1, seconds_per_tick))
        {
            to_chat(owner, span_warning(I18n.T("You feel sick...")));
            // Vomit blood if we're *really* freaking out
            _addtimer(Dm.@new<datum_callback>(owner, nameof(mob_living_carbon.vomit), buildup >= TERROR_BUILDUP_PASSIVE_MAXIMUM ? TRUE : FALSE), 5 * SECONDS,
                file: "code/datums/components/fearful/effects.dm", line: 104);
        }
        return dot;
    }
}

/// Causes tunnel vision, blurry eyes and periodic panic attacks when panicking
// tg: code/datums/components/fearful/effects.dm:106
public partial class datum_terror_handler_panic : datum_terror_handler
{
    // tg: code/datums/components/fearful/effects.dm:118
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        breath_loop = Dm.@new<datum_looping_sound_breathing>(owner, Dm.named("_direct", TRUE));
    }

    // tg: code/datums/components/fearful/effects.dm:122
    public override object Destroy(float? force = null)
    {
        Dm.Deferred("code/datums/components/fearful/effects.dm:123", "Destroy: owner.remove_fov_trait(type, FOV_270_DEGREES), the 2D field of view (living_fov.dm)");
        QDEL_NULL(ref breath_loop);
        deltimer(panic_end_timer);
        return base.Destroy(force);
    }

    // tg: code/datums/components/fearful/effects.dm:128
    public override async DmTask<object> tick(object seconds_per_tick = null, object terror_buildup = null)
    {
        object dot = await base.tick(seconds_per_tick, terror_buildup);
        float buildup = Dm.num(terror_buildup);
        if (IS_UNCONSCIOUS(owner))
        {
            stop_panic_attack();
            active = FALSE;
            Dm.Deferred("code/datums/components/fearful/effects.dm:133", "tick: owner.remove_fov_trait(type, FOV_270_DEGREES), the 2D field of view (living_fov.dm)");
            return dot;
        }

        if (buildup < TERROR_BUILDUP_PANIC)
        {
            if (Dm.truthy(active_attack)) // No you don't
                return TERROR_BUILDUP_PANIC - buildup;
            active = FALSE;
            Dm.Deferred("code/datums/components/fearful/effects.dm:140", "tick: owner.remove_fov_trait(type, FOV_270_DEGREES), the 2D field of view (living_fov.dm)");
            return dot;
        }

        if (!Dm.truthy(active))
        {
            active = TRUE;
            to_chat(owner, span_userdanger(I18n.T("You feel your heart racing!")));
            Dm.Deferred("code/datums/components/fearful/effects.dm:146", "tick: owner.add_fov_trait(type, FOV_270_DEGREES), the terror induced tunnel vision (the 2D field of view, living_fov.dm)");
        }

        owner.playsound_local(owner, Dm.resource("sound/effects/health/slowbeat.ogg"), 40, FALSE, channel: CHANNEL_HEARTBEAT, use_reverb: FALSE);
        if (SPT_PROB(5, seconds_per_tick))
            await owner.set_eye_blur_if_lower(10 * SECONDS);

        if (Dm.truthy(active_attack))
        {
            owner.losebreath += 0.25f; // Miss 1/4 breaths
            return dot;
        }

        if (SPT_PROB(2 + FEAR_SCALING(buildup, 3, TERROR_BUILDUP_PANIC, TERROR_BUILDUP_MAXIMUM), seconds_per_tick))
            dot = Dm.add(dot, await panic_attack(buildup));
        return dot;
    }

    // tg: code/datums/components/fearful/effects.dm:159
    public virtual async DmTask<object> panic_attack(object terror_buildup = null)
    {
        active_attack = TRUE;
        await owner.emote("gasp");
        await owner.Knockdown(0.5f * SECONDS);
        breath_loop.start();
        panic_end_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(stop_panic_attack)), rand(3 * SECONDS, 5 * SECONDS), TIMER_UNIQUE | TIMER_STOPPABLE,
            file: "code/datums/components/fearful/effects.dm", line: 164);
        owner.visible_message(span_warning(I18n.T("[] drops to the floor for a moment, clutching their chest.", owner)), span_alert(I18n.T("Your heart lurches in your chest. You can't take much more of this!")));
        return PANIC_ATTACK_TERROR_AMOUNT;
    }

    // tg: code/datums/components/fearful/effects.dm:168
    public virtual void stop_panic_attack()
    {
        breath_loop.stop();
        active_attack = FALSE;
        deltimer(panic_end_timer);
        panic_end_timer = null;
    }
}

// tg: code/datums/components/fearful/effects.dm:174
public partial class datum_terror_handler_startle : datum_terror_handler
{
    // tg: code/datums/components/fearful/effects.dm:178
    public override async DmTask<object> tick(object seconds_per_tick = null, object terror_buildup = null)
    {
        object dot = await base.tick(seconds_per_tick, terror_buildup);
        float buildup = Dm.num(terror_buildup);
        if (IS_UNCONSCIOUS(owner) || !(startle_cd <= world.time)) // COOLDOWN_FINISHED(src, startle_cd)
            return dot;

        if (buildup < TERROR_BUILDUP_FEAR || buildup - component.last_tick_buildup < TERROR_STARTLE_MINIMUM_DIFFERENCE)
            return dot;

        // The more scared we are, and the more fear we acquired at once last tick, the higher the probability of us being startled is
        // Not SPT_PROB or FEAR_SCALING because this is only triggered during ticks when we actually acquire fear
        if (!prob(15 * (buildup - component.last_tick_buildup) / TERROR_STARTLE_MINIMUM_DIFFERENCE * buildup / TERROR_BUILDUP_FEAR))
            return dot;

        startle_cd = world.time + TERROR_STARTLE_COOLDOWN; // COOLDOWN_START(src, startle_cd, TERROR_STARTLE_COOLDOWN)
        var sw__1 = rand(1, 3);
        if (sw__1 == 1)
        {
            to_chat(owner, span_warning(I18n.T("You are startled!")));
            await owner.emote("jump");
            await owner.Immobilize(0.1f * SECONDS * (buildup / TERROR_BUILDUP_FEAR));
        }
        else if (sw__1 == 2)
        {
            await owner.emote("scream");
            await owner.say("AAAAH!!", forced: "phobia");
            if (!prob(15 * (buildup / TERROR_BUILDUP_FEAR)))
                return dot;
            var held_item = owner.get_active_held_item() as obj_item;
            if (Dm.truthy(await owner.dropItemToGround(held_item)))
            {
                owner.visible_message(
                    span_danger(I18n.T("[] drops \\the []!", owner.name, held_item)),
                    span_warning(I18n.T("You drop \\the []!", held_item)), null, COMBAT_MESSAGE_RANGE);
            }
        }
        else if (sw__1 == 3)
        {
            to_chat(owner, span_warning(I18n.T("You lose your balance!")));
            await owner.adjust_staggered_up_to(2 * SECONDS * (buildup / TERROR_BUILDUP_FEAR), 20 * SECONDS);
            await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_spooked));
            _addtimer(Dm.@new<datum_callback>(this, nameof(speed_up)), 3 * SECONDS, TIMER_STOPPABLE | TIMER_DELETE_ME,
                file: "code/datums/components/fearful/effects.dm", line: 212);
        }
        return dot;
    }

    // tg: code/datums/components/fearful/effects.dm:215
    public virtual void speed_up()
    {
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_spooked));
    }
}

/// Forces you to stop, drop, and roll when on fire and sufficiently panicked
// tg: code/datums/components/fearful/effects.dm:219
public partial class datum_terror_handler_force_stop_drop_roll : datum_terror_handler
{
    // tg: code/datums/components/fearful/effects.dm:222
    public override async DmTask<object> tick(object seconds_per_tick = null, object terror_buildup = null)
    {
        float buildup = Dm.num(terror_buildup);
        if (buildup < TERROR_BUILDUP_FEAR || !Dm.truthy(owner.on_fire) || !Dm.truthy(owner.can_resist()))
            return 0;

        if (!SPT_PROB(5 + FEAR_SCALING(buildup, 10, TERROR_BUILDUP_FEAR, TERROR_BUILDUP_MAXIMUM), seconds_per_tick)) // 5% to 15% chance
            return 0;

        // basically forces you to stop, drop, and roll, and prevents you from cancelling it
        float forced_roll_duration = FEAR_SCALING(buildup, 67.5f * SECONDS, TERROR_BUILDUP_FEAR, TERROR_BUILDUP_MAXIMUM);
        await owner.Immobilize(forced_roll_duration);
        await owner.Knockdown(forced_roll_duration);
        if (Dm.truthy(owner.has_status_effect(typeof(datum_status_effect_stop_drop_roll))))
            to_chat(owner, span_warning(I18n.T("You panic, and focus everything into rolling the fire out!")));
        else
        {
            to_chat(owner, span_warning(I18n.T("You panic and immediately drop to the ground, trying to roll the fire out!")));
            await owner.apply_status_effect(typeof(datum_status_effect_stop_drop_roll), TRUE);
        }

        return -1 * TERROR_BUILDUP_PASSIVE_DECREASE; // + immediate terror decrease because you think it's helping!
    }
}
