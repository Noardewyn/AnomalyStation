// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/implant_explosive.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3): the microbomb, the macrobomb and its fake, the tactical deniability implant; the deathmatch
// microbomb has no procs of its own. The admins' message and the death investigation log are not-translated leaves (the admin
// tools, M10+). The var blocks are generated (D111).

/**
 * Note that we can stack explosive implants and thus increase the payload's devastation radius. (https://github.com/tgstation/tgstation/pull/50674)
 * That's why the three devastation values for the microbomb implant are balanced around in such a way
 * that buying one macrobomb equals to buying 10 microbombs and stacking them.
 */
// tg: code/game/objects/items/implants/implant_explosive.dm:13
public partial class obj_item_implant_explosive : obj_item_implant
{
    // tg: code/game/objects/items/implants/implant_explosive.dm:7  #define MICROBOMB_DELAY 0.7 SECONDS
    public const float MICROBOMB_DELAY = 0.7f * SECONDS;

    // tg: code/game/objects/items/implants/implant_explosive.dm:53
    [SignalHandler]
    public virtual void on_death(datum source = null, object gibbed = null)
    {
        // There may be other signals that want to handle mob's death
        // and the process of activating destroys the body, so let the other
        // signal handlers at least finish. Also, the "delayed explosion"
        // uses sleeps, which is bad for signal handlers to do.
        Waiting.Detach(activate("death"));
    }

    // tg: code/game/objects/items/implants/implant_explosive.dm:62
    [DmParams("cause")]
    public override async DmTask<object> activate(params object[] __extra)
    {
        object cause = Dm.arg(__extra, 0);
        object dot = await base.activate(__extra);
        if (!Dm.truthy(cause) || !Dm.truthy(imp_in) || Dm.truthy(active))
            return FALSE;
        if (Dm.eq(cause, "action_button"))
        {
            if (Dm.truthy(popup))
                return FALSE;
            popup = TRUE;
            object response = await tgui_alert(imp_in, I18n.T("Are you sure you want to activate your []? This will cause you to explode!", name), I18n.T("[] Confirmation", name), Dm.list("Yes", "No"));
            popup = FALSE;
            if (!Dm.eq(response, "Yes"))
                return FALSE;
        }
        if (Dm.eq(cause, "death") && HAS_TRAIT(imp_in, TRAIT_PREVENT_IMPLANT_AUTO_EXPLOSION))
            return FALSE;
        if (Dm.truthy(announce_activation))
            to_chat(imp_in, span_notice(I18n.T("You activate your [].", name)));
        active = TRUE;
        var boomturf = get_turf(imp_in) as turf;
        message_admins(Dm.text("[] has activated their [] at [], with cause of [].", ADMIN_LOOKUPFLW(imp_in), name, ADMIN_VERBOSEJMP(boomturf), cause));
        //If the delay is shorter or equal to the default delay, just blow up already jeez
        if (delay <= MICROBOMB_DELAY && Dm.truthy(instant_explosion))
        {
            await explode();
            return dot;
        }
        await timed_explosion();
        return dot;
    }

    // tg: code/game/objects/items/implants/implant_explosive.dm:91
    [DmParams("target", "user", "silent", "force")]
    public override async DmTask<object> implant(mob_living target = null, mob user = null, float? silent = null, float? force = null, params object[] __extra)
    {
        silent ??= FALSE;
        force ??= FALSE;
        foreach (object target_implant in Dm.each(target.implants))
        {
            if (target_implant is obj_item_implant_explosive) //we don't use our own type here, because macrobombs inherit this proc and need to be able to upgrade microbombs
            {
                obj_item_implant_explosive other_implant = (obj_item_implant_explosive)target_implant;
                if (Dm.truthy(other_implant.master_implant) && Dm.truthy(master_implant)) //we cant have two master implants at once
                {
                    target.balloon_alert(user, I18n.T("cannot fit implant!"));
                    return FALSE;
                }
                if (Dm.truthy(master_implant))
                    merge_implants(this, other_implant);
                else
                {
                    merge_implants(other_implant, this);
                    return TRUE;
                }
            }
        }

        object dot = await base.implant(target, user, silent, force, __extra);
        if (Dm.truthy(dot))
            RegisterSignal(target, COMSIG_LIVING_DEATH, nameof(on_death));
        return dot;
    }

    // tg: code/game/objects/items/implants/implant_explosive.dm:108
    public override async DmTask<object> removed(mob_living target = null, float? silent = null, float? special = null)
    {
        silent ??= FALSE;
        special ??= FALSE;
        object dot = await base.removed(target, silent, special);
        if (Dm.truthy(dot))
            UnregisterSignal(target, COMSIG_LIVING_DEATH);
        return dot;
    }

    /**
     * Merges two explosive implants together, adding the stats of the latter to the former before qdeling the latter implant.
     * kept_implant = the implant that is kept
     * stat_implant = the implant which has its stats added to kept_implant, before being deleted.
     */
    // tg: code/game/objects/items/implants/implant_explosive.dm:118
    public virtual void merge_implants(obj_item_implant_explosive kept_implant = null, obj_item_implant_explosive stat_implant = null)
    {
        kept_implant.explosion_devastate += stat_implant.explosion_devastate;
        kept_implant.explosion_heavy += stat_implant.explosion_heavy;
        kept_implant.explosion_light += stat_implant.explosion_light;
        kept_implant.delay = Dm.min(kept_implant.delay + stat_implant.delay, 30 * SECONDS);
        qdel(stat_implant);
    }

    /**
     * Explosive activation sequence for implants with a delay longer than 0.7 seconds.
     * Make the implantee beep a few times, keel over and explode. Usually to a devastating effect.
     */
    // tg: code/game/objects/items/implants/implant_explosive.dm:129
    public virtual async DmTask timed_explosion()
    {
        if (imp_in == null)
            visible_message(span_warning(I18n.T("[] starts beeping ominously!", this)));
        else
        {
            imp_in.visible_message(span_warning(I18n.T("[] starts beeping ominously!", imp_in)));
            if (Dm.truthy(notify_ghosts))
                GlobalProcs.notify_ghosts(
                    I18n.T("[] is about to detonate their explosive implant!", imp_in.real_name),
                    source: this,
                    header: I18n.T("Tick Tick Tick..."),
                    notify_flags: NOTIFY_CATEGORY_NOFLASH,
                    ghost_sound: "sound/machines/warning-buzzer.ogg",
                    notify_volume: 75);
        }

        playsound(loc, Dm.resource("sound/items/timer.ogg"), 30, FALSE);
        if (!Dm.truthy(panic_beep_sound))
            await sleep(delay * 0.25f);
        if (imp_in != null && !IS_UNCONSCIOUS_OR_CRIT(imp_in) && !Dm.truthy(no_paralyze))
        {
            imp_in.visible_message(span_warning(I18n.T("[] doubles over in pain!", imp_in)));
            await imp_in.Paralyze(14 * SECONDS);
        }

        if (!Dm.truthy(panic_beep_sound))
        {
            for (int index = 1; index <= 3; index++) // Total of 4 bomb beeps, and we've already beeped once
            {
                //for extra spice
                float beep_volume = 30 + (5 * index);
                playsound(loc, Dm.resource("sound/items/timer.ogg"), beep_volume, vary: FALSE);
                await sleep(delay * 0.25f);
            }
            await explode();
        }
        else
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(explode)), delay, file: "code/game/objects/items/implants/implant_explosive.dm", line: 162);
            while (delay > 1) //so we dont accidentally enter an infinite sleep
            {
                float beep_volume = 35;
                playsound(loc, Dm.resource("sound/items/timer.ogg"), beep_volume, vary: FALSE);
                await sleep(delay * 0.2f);
                delay -= delay * 0.2f;
                beep_volume += 5;
            }
        }
    }

    ///When called, just explodes
    // tg: code/game/objects/items/implants/implant_explosive.dm:172
    public virtual async DmTask explode(atom override_explode_target = null)
    {
        explosion_devastate = Dm.floor(explosion_devastate);
        explosion_heavy = Dm.floor(explosion_heavy);
        explosion_light = Dm.floor(explosion_light);
        await explosion(override_explode_target ?? this, devastation_range: explosion_devastate, heavy_impact_range: explosion_heavy, light_impact_range: explosion_light, flame_range: explosion_light, flash_range: explosion_light, explosion_cause: this);
        mob_living kill_mob = isliving(override_explode_target) ? (mob_living)override_explode_target : imp_in;
        if (kill_mob != null)
        {
            kill_mob.investigate_log("has been gibbed by an explosive implant.", INVESTIGATE_DEATHS);
            await kill_mob.gib(Dm.bitor(DROP_ORGANS, DROP_BODYPARTS));
        }
        qdel(this);
    }
}

// Version with the same appearance and delay as a macrobomb, but is actually a microbomb with a surprise!
// tg: code/game/objects/items/implants/implant_explosive.dm:194
public partial class obj_item_implant_explosive_macro_fake : obj_item_implant_explosive_macro
{
    // tg: code/game/objects/items/implants/implant_explosive.dm:199
    public override async DmTask explode(atom override_explode_target = null)
    {
        await honkerblast(
            origin: override_explode_target ?? this,
            light_range: Dm.initial(typeof(obj_item_implant_explosive_macro), (obj_item_implant_explosive_macro m) => m.explosion_light) * 0.5f,
            medium_range: Dm.initial(typeof(obj_item_implant_explosive_macro), (obj_item_implant_explosive_macro m) => m.explosion_heavy) * 0.5f,
            heavy_range: Dm.initial(typeof(obj_item_implant_explosive_macro), (obj_item_implant_explosive_macro m) => m.explosion_devastate) * 0.5f);
        await base.explode(override_explode_target);
    }
}

///Microbomb which prevents you from going into critical condition but also explodes after a timer when you reach critical condition in the first place.
// tg: code/game/objects/items/implants/implant_explosive.dm:209
public partial class obj_item_implant_explosive_deniability : obj_item_implant_explosive
{
    // tg: code/game/objects/items/implants/implant_explosive.dm:231
    [DmParams("target", "user", "silent", "force")]
    public override async DmTask<object> implant(mob_living target = null, mob user = null, float? silent = null, float? force = null, params object[] __extra)
    {
        object dot = await base.implant(target, user, silent, force, __extra);
        if (Dm.truthy(dot))
        {
            RegisterSignal(target, COMSIG_LIVING_HEALTH_UPDATE, nameof(check_health));
            target.add_traits(Dm.list(TRAIT_NOSOFTCRIT, TRAIT_NOHARDCRIT), IMPLANT_TRAIT);
        }
        return dot;
    }

    // tg: code/game/objects/items/implants/implant_explosive.dm:237
    public override async DmTask<object> removed(mob_living target = null, float? silent = null, float? special = null)
    {
        object dot = await base.removed(target, silent, special);
        if (Dm.truthy(dot))
        {
            UnregisterSignal(target, COMSIG_LIVING_HEALTH_UPDATE);
            target.remove_traits(Dm.list(TRAIT_NOSOFTCRIT, TRAIT_NOHARDCRIT), IMPLANT_TRAIT);
        }
        return dot;
    }

    // tg: code/game/objects/items/implants/implant_explosive.dm:243
    [SignalHandler]
    public virtual void check_health(mob_living source = null)
    {
        if (source.health < source.crit_threshold)
            Waiting.Detach(activate("deniability"));
    }
}
