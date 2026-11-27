// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/boss/thing/thing_abilities.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:1
public partial class datum_action_cooldown_mob_cooldown_the_thing : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:6
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        if (owner is not mob_living_basic_boss_thing the_thing)
            return await base.IsAvailable(feedback);
        return Dm.truthy(await base.IsAvailable(feedback)) && Dm.@in(the_thing.phase, available_in_phases) ? TRUE : FALSE;
    }
}

//decimate

// tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:14
public partial class datum_action_cooldown_mob_cooldown_the_thing_decimate : datum_action_cooldown_mob_cooldown_the_thing
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:22
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        atom caster = target_atom;
        if (HAS_TRAIT_FROM(caster, TRAIT_IMMOBILIZED, MEGAFAUNA_TRAIT))
            return null;
        object dot = await base.Activate(caster);

        ADD_TRAIT(caster, TRAIT_IMMOBILIZED, MEGAFAUNA_TRAIT);
        caster.Shake(1.4f, 0.8f, 0.3f * SECONDS);
        caster.visible_message(span_danger(I18n.T("[] shakes violently!", caster)));

        foreach (turf_open target in Dm.each<turf_open>(Dm.sub(RANGE_TURFS(2, caster), caster.loc)))
            Dm.@new<obj_effect_temp_visual_telegraphing_exclamation_animated>(target, 1.5f * SECONDS);
        _addtimer(Dm.@new<datum_callback>(this, nameof(make_spikes), caster), 1.5f * SECONDS, file: "code/modules/mob/living/basic/boss/thing/thing_abilities.dm", line: 33);
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:35
    public virtual void make_spikes(atom caster = null)
    {
        REMOVE_TRAIT(caster, TRAIT_IMMOBILIZED, MEGAFAUNA_TRAIT);
        foreach (turf_open target in Dm.each<turf_open>(RANGE_TURFS(2, caster)))
        {
            if (Dm.locate<obj_structure_thing_boss_spike>(target) != null)
                continue;
            Dm.@new<obj_structure_thing_boss_spike>(target);
        }
    }
}

// charge

// tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:44
public partial class datum_action_cooldown_mob_cooldown_charge_the_thing : datum_action_cooldown_mob_cooldown_charge
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:49
    public override async DmTask charge_sequence(atom_movable charger = null, atom target_atom = null, object delay = null, object past = null)
    {
        if (HAS_TRAIT_FROM(owner, TRAIT_IMMOBILIZED, MEGAFAUNA_TRAIT))
            return;
        mob_living_basic_boss_thing the_thing = (mob_living_basic_boss_thing)owner;
        float charge_count = the_thing.phase != 3 ? 1 : 2;
        for (int i = 1; i <= charge_count; i++)
            await do_charge(owner, target_atom, charge_delay * i, charge_past);
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:57
    public override void do_charge_indicator(atom charger = null, atom charge_target = null)
    {
        turf target_turf = get_turf(charge_target);
        if (target_turf == null)
            return;
        Dm.@new<obj_effect_temp_visual_telegraphing_exclamation_animated>(target_turf);
        obj_effect_temp_visual_decoy decoy = Dm.@new<obj_effect_temp_visual_decoy>(charger.loc, charger);
        animate(decoy, Dm.named("alpha", 0), Dm.named("color", COLOR_RED), Dm.named("transform", Dm.mul(Dm.matrix(), 2)), Dm.named("time", 3));
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:65
    public override async DmTask hit_target(atom_movable source = null, object target = null, object damage_dealt = null)
    {
        mob_living target_mob = target as mob_living;
        target_mob.visible_message(span_danger(I18n.T("[] lunges into []!", source, target_mob)), span_userdanger(I18n.T("[] knocks you into the ground, slashing you in the process!", source)));
        await target_mob.apply_damage(Dm.num(damage_dealt), BRUTE);
        await target_mob.Knockdown(0.5f * SECONDS);
        playsound(get_turf(target_mob), Dm.resource("sound/items/weapons/rapierhit.ogg"), 100, TRUE);
        shake_camera(target_mob, 4, 3);
    }
}

// square tendrils

// tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:74
public partial class datum_action_cooldown_mob_cooldown_the_thing_big_tendrils : datum_action_cooldown_mob_cooldown_the_thing
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:82
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        atom target = target_atom;
        if (HAS_TRAIT_FROM(owner, TRAIT_IMMOBILIZED, MEGAFAUNA_TRAIT))
            return null;
        object dot = await base.Activate(target);
        target = get_turf(target);
        mob_living living_owner = (mob_living)owner;
        float delay = 1 * SECONDS;
        // tg's condition is the ternary itself, 2 or 1, so it is always true and the big telegraph always shows; kept.
        if ((Dm.num(living_owner.health) <= Dm.num(living_owner.maxHealth) / 3 ? 2 : 1) != 0)
        {
            delay += 1 * SECONDS;
            Dm.@new<obj_effect_temp_visual_telegraphing_big>(target, delay);
        }
        else
            Dm.@new<obj_effect_temp_visual_telegraphing_exclamation_animated>(target);
        _addtimer(Dm.@new<datum_callback>(this, nameof(make_spikes), target), delay, file: "code/modules/mob/living/basic/boss/thing/thing_abilities.dm", line: 94);
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:96
    public virtual void make_spikes(atom epicenter = null)
    {
        mob_living living_owner = (mob_living)owner;
        float radius = Dm.num(living_owner.health) <= Dm.num(living_owner.maxHealth) / 3 ? 2 : 1;
        foreach (turf_open target in Dm.each<turf_open>(RANGE_TURFS(radius, epicenter)))
        {
            if (Dm.locate<obj_structure_thing_boss_spike>(target) != null)
                continue;
            Dm.@new<obj_effect_temp_visual_mook_dust>(target);
            Dm.@new<obj_structure_thing_boss_spike>(target);
        }
    }
}

// shriek

// tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:108
public partial class datum_action_cooldown_mob_cooldown_the_thing_shriek : datum_action_cooldown_mob_cooldown_the_thing
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:118
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        atom caster = target_atom;
        if (HAS_TRAIT_FROM(caster, TRAIT_IMMOBILIZED, MEGAFAUNA_TRAIT))
            return null;
        object dot = await base.Activate(caster);
        ADD_TRAIT(caster, TRAIT_IMMOBILIZED, MEGAFAUNA_TRAIT);
        caster.visible_message(span_danger(I18n.T("[][] flesh starts becoming filled with holes!", caster, caster.p_s())));
        foreach (turf_open target in Dm.each<turf_open>(RANGE_TURFS(2, caster)))
            Dm.@new<obj_effect_temp_visual_telegraphing_exclamation>(target, 1.2f * SECONDS);
        _addtimer(Dm.@new<datum_callback>(this, nameof(shriek), owner), 1.2f * SECONDS, file: "code/modules/mob/living/basic/boss/thing/thing_abilities.dm", line: 126);
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:128
    public virtual async DmTask shriek(atom caster = null)
    {
        REMOVE_TRAIT(caster, TRAIT_IMMOBILIZED, MEGAFAUNA_TRAIT);
        caster.visible_message(span_danger(I18n.T("[] shrieks! The sheer frequency of the sound makes your skin hurt and you feel like your brain is on fire!", caster)));
        Dm.output_to(caster, sound(Dm.resource("sound/effects/screech.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        foreach (mob_living target in Dm.each<mob_living>(range(2, caster)))
        {
            if (target == owner)
                continue;
            await target.set_confusion_if_lower(5 * SECONDS);
            await target.set_jitter_if_lower(5 * SECONDS);
            if (target is mob_living_carbon carbon_target)
                await carbon_target.drop_all_held_items();
            Dm.output_to(target, sound(Dm.resource("sound/effects/screech.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        }
    }
}

// card. tendrils

// tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:145
public partial class datum_action_cooldown_mob_cooldown_the_thing_cardinal_tendrils : datum_action_cooldown_mob_cooldown_the_thing
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:156
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        atom targetted_turf = target_atom;
        if (HAS_TRAIT_FROM(owner, TRAIT_IMMOBILIZED, MEGAFAUNA_TRAIT))
            return null;
        object dot = await base.Activate(targetted_turf);
        targetted_turf = get_turf(targetted_turf);
        owner.Shake(1.4f, 0.8f, 0.3f * SECONDS);
        owner.visible_message(span_danger(I18n.T("[] shakes violently!", owner)));
        DmList target_turfs = find_turfs(targetted_turf) as DmList;
        foreach (turf_open target in Dm.each<turf_open>(target_turfs))
            Dm.@new<obj_effect_temp_visual_telegraphing_exclamation_animated>(target, 1.5f * SECONDS);
        _addtimer(Dm.@new<datum_callback>(this, nameof(make_spikes), target_turfs, owner), 1.5f * SECONDS, file: "code/modules/mob/living/basic/boss/thing/thing_abilities.dm", line: 166);
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:168
    public virtual object find_turfs(atom caster = null)
    {
        DmList dot = Dm.list();
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            foreach (turf potential_turf in Dm.each_cast<turf>(get_line(caster, get_ranged_target_turf(caster, direction, range) as atom)))
            {
                if (Dm.truthy(potential_turf.density))
                    break;
                Dm.append(ref dot, potential_turf);
            }
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:176
    public virtual void make_spikes(DmList target_turfs = null, atom caster = null)
    {
        REMOVE_TRAIT(caster, TRAIT_IMMOBILIZED, MEGAFAUNA_TRAIT);
        foreach (turf_open target in Dm.each_cast<turf_open>(target_turfs))
        {
            if (Dm.locate<obj_structure_thing_boss_spike>(target) != null)
                continue;
            Dm.@new<obj_effect_temp_visual_mook_dust>(target);
            Dm.@new<obj_structure_thing_boss_spike>(target);
        }
    }
}

// acid

// tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:186
public partial class datum_action_cooldown_mob_cooldown_the_thing_acid_spit : datum_action_cooldown_mob_cooldown_the_thing
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_abilities.dm:194
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        if (HAS_TRAIT_FROM(owner, TRAIT_IMMOBILIZED, MEGAFAUNA_TRAIT))
            return null;
        object dot = await base.Activate(target_atom);
        turf owner_turf = get_turf(owner);
        owner.visible_message(span_danger(I18n.T("[] spits acid!", owner)));
        DmList potential = RANGE_TURFS(4, owner_turf);

        for (float i = 1, n = rand(2, 4); i <= n; i++)
            Dm.@new<obj_effect_temp_visual_incoming_thing_acid>(pick(potential));
        return dot;
    }
}
