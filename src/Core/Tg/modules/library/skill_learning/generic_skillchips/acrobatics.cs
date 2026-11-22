// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/skill_learning/generic_skillchips/acrobatics.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file. The Spinesthetics chip (and its K1SS prototype): flips and spins past their cooldown, at the cost of
// the chip's integrity, with sparks, a smoking or burning head, a short circuit and, at the end, an exploding head. The smoke is a
// particle holder over the head (the client draws no BYOND particles, as the cigarettes' smoke). The var blocks are generated (D111).

// tg: code/modules/library/skill_learning/generic_skillchips/acrobatics.dm:1
public partial class obj_item_skillchip_acrobatics : obj_item_skillchip
{
    // tg: code/modules/library/skill_learning/generic_skillchips/acrobatics.dm:23
    public override async DmTask on_activate(mob_living_carbon user = null, float? silent = null)
    {
        silent ??= FALSE;
        await base.on_activate(user, silent);
        RegisterSignal(user, COMSIG_MOB_EMOTE_COOLDOWN_CHECK, nameof(whowee));
        await AddElement(typeof(datum_element_obj_regen), 0.01f); // 1% regen per SSobj tick.
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/acrobatics.dm:28
    public override async DmTask on_deactivate(mob_living_carbon user = null, float? silent = null)
    {
        await base.on_deactivate(user, silent);
        UnregisterSignal(user, COMSIG_MOB_EMOTE_COOLDOWN_CHECK);
        RemoveElement(typeof(datum_element_obj_regen), 0.01f);
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/acrobatics.dm:33
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref sparks);
        QDEL_NULL(ref particle_effect);
        return base.Destroy(force);
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/acrobatics.dm:38
    [SignalHandler]
    public virtual object whowee(mob_living_carbon bozo = null, object emote_key = null, object emote_intentional = null)
    {
        if (!Dm.@in(emote_key, affected_emotes))
            return null;

        if (Dm.truthy(allowed_usage))
        {
            allowed_usage--;
            _addtimer(Dm.@new<datum_callback>(this, nameof(charge)), reload_charge, file: "code/modules/library/skill_learning/generic_skillchips/acrobatics.dm", line: 46);
            return COMPONENT_EMOTE_COOLDOWN_BYPASS;
        }

        Waiting.NoSleep(take_damage(1, sound_effect: FALSE));

        if (!Dm.truthy(sparks))
        {
            sparks = Dm.@new<datum_effect_system_basic_spark_spread>(this, 5, FALSE);
            sparks.attach(this);
        }

        // minimum roll is by default capped at 50, with the min value lowering as integrity is reduced.
        float mintegrity = clamp(50 - (100 - Dm.num(get_integrity())), 1, 100);
        float sw__1 = rand(mintegrity, Dm.num(get_integrity())); // 1 to 100 but gets worse every time
        // CRIT FAIL
        if (sw__1 == 1)
        {
            bozo.visible_message(span_userdanger(I18n.T("[]'s head suddenly explodes outwards!", bozo)));

            Waiting.NoSleep(explosion(bozo, light_impact_range: 2, adminlog: TRUE, explosion_cause: this));
            // WITNESS THE GORE
            foreach (mob_living splashed in Dm.each<mob_living>(view(2, bozo)))
            {
                if (Dm.truthy(bozo.has_status_effect(typeof(datum_status_effect_grouped_blindness))))
                    to_chat(splashed, span_userdanger(I18n.T("You're splashed with something")));
                else
                    to_chat(splashed, span_userdanger(I18n.T("You are blinded by a shower of blood!")));
                Waiting.NoSleep(splashed.Stun(1 * SECONDS));
                Waiting.NoSleep(splashed.Knockdown(2 * SECONDS));
                Waiting.NoSleep(splashed.set_eye_blur_if_lower(15 * SECONDS));
                Waiting.NoSleep(splashed.adjust_confusion(4 * SECONDS));
            }

            // GORE
            obj_item_bodypart bozopart = bozo.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
            if (Dm.truthy(bozopart))
            {
                datum_wound_cranial_fissure crit_wound = Dm.@new<datum_wound_cranial_fissure>();
                Waiting.NoSleep(crit_wound.apply_wound(bozopart));
            }
            /*
                var/list/droppage_candidates = bozo.get_organs_for_zone(BODY_ZONE_HEAD, include_children = TRUE)
                if(droppage_candidates)
                    var/obj/thing_to_drop = pick(droppage_candidates)
                    thing_to_drop.forceMove(bozo.drop_location())
            */ //WHY DOESNTY IT OWRK

            // does not necessarily kill you directly. instead it causes cranial fissure + something to drop from your head. could be eyes, tongue, ears, brain, even implants
            Dm.@new<obj_effect_gibspawner_generic>(get_turf(bozo), bozo);
            sparks.amount = 15;
            Waiting.NoSleep(sparks.start());

            qdel(this);
        }
        // last chance to stop
        else if (sw__1 >= 7 && sw__1 <= 9)
        {
            bozo.visible_message(
                span_danger(I18n.T("[] seems to short circuit!", bozo)),
                span_userdanger(I18n.T("Your brain short circuits!")));
            // if they're susceptible to electrocution, confuse them
            if (Dm.truthy(Waiting.NoSleep(bozo.electrocute_act(15, bozo, 1, Dm.bitor(SHOCK_NOGLOVES, SHOCK_NOSTUN)))))
            {
                Waiting.NoSleep(bozo.adjust_confusion(15 * SECONDS));
                Waiting.NoSleep(bozo.set_eye_blur_if_lower(10 * SECONDS));
            }
            // but the rest of the effects will happen either way
            Waiting.NoSleep(bozo.adjust_organ_loss(ORGAN_SLOT_BRAIN, 20 - Dm.num(get_integrity())));
            sparks.amount = 5;
            Waiting.NoSleep(sparks.start());
        }
        // brain Smoking. you should probably stop now
        else if (sw__1 >= 13 && sw__1 <= 15)
        {
            // if already hot, light 'em up
            Type particle_path = typeof(particles_smoke_steam_mild);
            if (Dm.truthy(bozo.has_status_effect(typeof(datum_status_effect_temperature_over_time_chip_overheat))))
            {
                Waiting.NoSleep(bozo.adjust_fire_stacks(11 - Dm.num(get_integrity())));
                bozo.ignite_mob();
                bozo.visible_message(
                    span_danger(I18n.T("[]'s head lights up!", bozo)),
                    span_userdanger(I18n.T("Your head hurts so much, it feels like it's on fire!")));
                Waiting.Detach(bozo.emote("scream")); // ASYNC
                // the holder's type is never a particle type (tg compares the two; kept): this never returns
                if (particle_effect?.GetType() == particle_path)
                    return null;
                particle_path = typeof(particles_smoke_steam_bad);
            }
            else
            {
                bozo.visible_message(
                    span_danger(I18n.T("[]'s head starts smoking!", bozo)),
                    span_userdanger(I18n.T("Your brain feels like it's on fire!")));

                // increase smokiness if already smoking (as above, the holder's type is never a particle type: kept)
                if (particle_effect?.GetType() == typeof(particles_smoke_steam_mild))
                    particle_path = typeof(particles_smoke_steam);
                else
                    particle_path = typeof(particles_smoke_steam_mild);
            }

            Waiting.NoSleep(bozo.adjust_confusion(4 * SECONDS));
            Waiting.NoSleep(bozo.set_eye_blur_if_lower(3 * SECONDS));

            particle_effect = Dm.@new<obj_effect_abstract_particle_holder>(bozo, particle_path);
            // roughly head position.
            // dont know how to make this not hardcoded
            particle_effect.set_particle_position(-2, 12, 0);
            Waiting.NoSleep(bozo.apply_status_effect(typeof(datum_status_effect_temperature_over_time_chip_overheat), 15 * SECONDS));
            datum qdel_in__item = particle_effect;
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 15 * SECONDS > GC_FILTER_QUEUE ? WEAKREF(qdel_in__item) : qdel_in__item), 15 * SECONDS, file: "code/modules/library/skill_learning/generic_skillchips/acrobatics.dm", line: 143);
            sparks.amount = 10;
            Waiting.NoSleep(sparks.start());
        }
        // hey, something isn't right...
        else if (sw__1 >= 16 && sw__1 <= 50)
        {
            bozo.visible_message(
                span_warning(I18n.T("[]'s head sparks.", bozo)));

            sparks.amount = rand(1, 2);
            Waiting.NoSleep(sparks.start());
        }

        return COMPONENT_EMOTE_COOLDOWN_BYPASS;
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/acrobatics.dm:158
    public virtual void charge()
    {
        allowed_usage++;
    }
}
