// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/legion/legion_tumour.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Left behind when a legion infects you, for medical enrichment
// tg: code/modules/mob/living/basic/lavaland/legion/legion_tumour.dm:2
public partial class obj_item_organ_legion_tumour : obj_item_organ
{
    /// Spooky sounds to play as you start to turn
    // tg: code/modules/mob/living/basic/lavaland/legion/legion_tumour.dm:23 (var/static/list/spooky_sounds)
    public static DmList spooky_sounds => TgState.Current.obj_item_organ_legion_tumour__spooky_sounds ??= Dm.list(
        Dm.resource("sound/mobs/non-humanoids/hiss/lowHiss1.ogg"),
        Dm.resource("sound/mobs/non-humanoids/hiss/lowHiss2.ogg"),
        Dm.resource("sound/mobs/non-humanoids/hiss/lowHiss3.ogg"),
        Dm.resource("sound/mobs/non-humanoids/hiss/lowHiss4.ogg"));

    // tg: code/modules/mob/living/basic/lavaland/legion/legion_tumour.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        animate_pulse();
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion_tumour.dm:34
    public override async DmTask on_begin_failure()
    {
        animate_pulse();
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion_tumour.dm:37
    public override void on_failure_recovery()
    {
        animate_pulse();
    }

    /// Do a heartbeat animation depending on if we're failing or not
    // tg: code/modules/mob/living/basic/lavaland/legion/legion_tumour.dm:41
    public virtual void animate_pulse()
    {
        animate(this, Dm.named("transform", Dm.matrix())); // Stop any current animation

        float speed_divider = Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)) ? 2 : 1;

        animate(this, Dm.named("transform", Dm.matrix().Scale(1.1f)), Dm.named("time", 0.5f * SECONDS / speed_divider), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)), Dm.named("loop", -1), Dm.named("flags", ANIMATION_PARALLEL));
        animate(Dm.named("transform", Dm.matrix()), Dm.named("time", 0.5f * SECONDS / speed_divider), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN)));
        animate(Dm.named("transform", Dm.matrix()), Dm.named("time", 2 * SECONDS / speed_divider));
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion_tumour.dm:50
    public override async DmTask Remove(mob_living_carbon egg_owner = null, float? special = null, object movement_flags = null)
    {
        await base.Remove(egg_owner, special, movement_flags);
        stage = 0;
        elapsed_time = 0;
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion_tumour.dm:55
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        owner.log_message(Dm.text("has received [] which will eventually turn them into a Legion.", this), LOG_VICTIM);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion_tumour.dm:59
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(await try_apply(target_mob, user)))
        {
            qdel(this);
            return null;
        }
        return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }

    /// Smear it on someone like a regen core, why not. Make sure they're alive though.
    // tg: code/modules/mob/living/basic/lavaland/legion/legion_tumour.dm:66
    public virtual async DmTask<object> try_apply(mob_living target = null, mob user = null)
    {
        if (!Dm.truthy(await user.Adjacent(target)) || !isliving(target))
            return FALSE;

        if (!IS_UNCONSCIOUS(target) && !Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
        {
            await target.add_mood_event("legion_core", typeof(datum_mood_event_healsbadman));
            await target.apply_status_effect(applied_status);

            if (target != user)
                target.visible_message(span_notice(I18n.T("[] splatters [] with []... Disgusting tendrils pull [] wounds shut!", user, target, this, target.p_their())));
            else
                to_chat(user, span_notice(I18n.T("You smear [] on yourself. Disgusting tendrils pull your wounds closed.", this)));
            return TRUE;
        }

        if (!ishuman(target))
            return FALSE;

        log_combat(user, target, "used a Legion Tumour on", this, "as they are in crit, this will turn them into a Legion.");
        target.visible_message(span_boldwarning(I18n.T("[] splatters [] with []... and it springs into horrible life!", user, target, this)));
        mob_living_basic_mining_legion_brood skull = Dm.@new<mob_living_basic_mining_legion_brood>(target.loc);
        await skull.melee_attack(target);
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion_tumour.dm:89
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (QDELETED(this) || QDELETED(owner))
            return;

        if (stage >= 2)
        {
            if (SPT_PROB(stage / 5, seconds_per_tick))
            {
                to_chat(owner, span_notice(I18n.T("You feel a bit better.")));
                await owner.apply_status_effect(applied_status); // It's not all bad!
            }
            if (SPT_PROB(1, seconds_per_tick))
                await owner.emote("twitch");
        }

        float sw__1 = stage;
        if (sw__1 == 2 || sw__1 == 3)
        {
            if (SPT_PROB(1, seconds_per_tick))
                to_chat(owner, span_danger(I18n.T("Your chest spasms!")));
            if (SPT_PROB(1, seconds_per_tick))
                to_chat(owner, span_danger(I18n.T("You feel weak.")));
            if (SPT_PROB(1, seconds_per_tick))
                Dm.output_to(owner, sound(pick(spooky_sounds))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
            if (SPT_PROB(2, seconds_per_tick))
                await owner.vomit();
        }
        else if (sw__1 == 4 || sw__1 == 5)
        {
            if (SPT_PROB(2, seconds_per_tick))
                to_chat(owner, span_danger(I18n.T("Something flexes under your skin.")));
            if (SPT_PROB(2, seconds_per_tick))
            {
                if (prob(40))
                    Dm.output_to(owner, sound(Dm.resource("sound/music/antag/bloodcult/ghost_whisper.ogg"))); // SEND_SOUND
                else
                    Dm.output_to(owner, sound(pick(spooky_sounds))); // SEND_SOUND
            }
            if (SPT_PROB(3, seconds_per_tick))
            {
                await owner.vomit(vomit_type: typeof(obj_effect_decal_cleanable_vomit_old_black_bile));
                if (prob(50))
                {
                    turf check_turf = get_step(owner.loc, owner.dir);
                    atom land_turf = Dm.truthy(await check_turf.is_blocked_turf()) ? owner.loc : check_turf;
                    mob_living_basic_mining_legion_brood child = Dm.@new<mob_living_basic_mining_legion_brood>(land_turf);
                    child.assign_creator(owner, copy_full_faction: FALSE);
                }
            }

            if (SPT_PROB(3, seconds_per_tick))
            {
                to_chat(owner, span_danger(I18n.T("Your muscles ache.")));
                await owner.take_bodypart_damage(3);
            }
        }

        if (stage == 5)
        {
            if (SPT_PROB(10, seconds_per_tick))
                await infest();
            return;
        }

        elapsed_time += Dm.num(seconds_per_tick) * SECONDS * (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)) ? 3 : 1); // Let's call it "matured" rather than failed
        if (elapsed_time < Dm.num(growth_time))
            return;
        stage++;
        elapsed_time = 0;
        if (stage == 5)
            to_chat(owner, span_bolddanger(I18n.T("Something is moving under your skin!")));
    }

    /// Consume our host
    // tg: code/modules/mob/living/basic/lavaland/legion/legion_tumour.dm:145
    public virtual async DmTask infest()
    {
        if (QDELETED(this) || QDELETED(owner))
            return;
        owner.log_message("has been turned into a Legion by their tumour.", LOG_VICTIM);
        owner.visible_message(span_boldwarning(I18n.T("Black tendrils burst from []'s flesh, covering them in amorphous flesh!", owner)));
        mob_living_basic_mining_legion new_legion = Dm.@new(spawn_type, owner.loc) as mob_living_basic_mining_legion;
        await new_legion.consume(owner as mob_living_carbon_human);
        qdel(this);
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion_tumour.dm:154
    public override async DmTask on_find(mob_living finder = null)
    {
        await base.on_find(finder);
        to_chat(finder, span_warning(I18n.T("There's an enormous tumour in []'s []!", owner, zone)));
        if (stage < 4)
        {
            to_chat(finder, span_notice(I18n.T("Its tendrils seem to twitch towards the light.")));
            return;
        }
        to_chat(finder, span_notice(I18n.T("Its pulsing tendrils reach all throughout the body.")));
        if (prob(stage * 2))
            await infest();
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion_tumour.dm:164
    public override object feel_for_damage(object self_aware = null)
    {
        // keep stealthy for now, revisit later
        return "";
    }
}

public sealed partial class TgState
{
    internal DmList obj_item_organ_legion_tumour__spooky_sounds;
}
