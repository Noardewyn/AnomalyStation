// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/boss/boss.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

//not quite simple animal megafauna but close enough
// port actual megafauna stuff once it gets used for lavaland megafauna
//im using it for stuff both of them get
// tg: code/modules/mob/living/basic/boss/boss.dm:4
public partial class mob_living_basic_boss : mob_living_basic
{
    // tg: code/modules/mob/living/basic/boss/boss.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_wall_tearer), Dm.named("tear_time", 1 * SECONDS)));
        if (Dm.truthy(gps_name))
            Waiting.NoSleep(AddComponent(typeof(datum_component_gps), gps_name));
        ADD_TRAIT(this, TRAIT_SPACEWALK, INNATE_TRAIT);
        add_traits(Dm.list(TRAIT_NO_TELEPORT, TRAIT_MARTIAL_ARTS_IMMUNE, TRAIT_LAVA_IMMUNE, TRAIT_ASHSTORM_IMMUNE, TRAIT_NO_FLOATING_ANIM), MEGAFAUNA_TRAIT);
        Waiting.NoSleep(AddComponent(typeof(datum_component_seethrough_mob)));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), string_list(regular_loot)));
        Waiting.NoSleep(handle_crusher_loot());
        Waiting.NoSleep(handle_achievements());
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/boss.dm:51
    public override async DmTask gib(float? drop_bitflags = null)
    {
        if (health > 0)
            return;
        await base.gib(drop_bitflags);
    }

    // tg: code/modules/mob/living/basic/boss/boss.dm:56
    public override async DmTask dust(object just_ash = null, object drop_items = null, object give_moodlet = null, object force = null)
    {
        if (!Dm.truthy(force) && health > 0)
            return;
        await base.dust(just_ash, drop_items, give_moodlet, force);
    }

    // tg: code/modules/mob/living/basic/boss/boss.dm:61
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (health > 0 && !Dm.truthy(gibbed)) // prevents instakills
            return null;
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/boss/boss.dm:66
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                Waiting.NoSleep(adjust_brute_loss(250));
                break;

            case EXPLODE_HEAVY:
                Waiting.NoSleep(adjust_brute_loss(100));
                break;

            case EXPLODE_LIGHT:
                Waiting.NoSleep(adjust_brute_loss(50));
                break;
        }

        return TRUE;
    }

    // tg: code/modules/mob/living/basic/boss/boss.dm:79
    public override async DmTask<object> early_melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.early_melee_attack(target, modifiers, ignore_cooldown);
        if (Dm.truthy(dot) || target is not mob_living victim)
            return dot;
        if (Dm.truthy(should_devour(victim)))
        {
            await devour(victim);
            return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
        }
        return dot;
    }

    /// Determines if this mob is worth devouring
    // tg: code/modules/mob/living/basic/boss/boss.dm:89
    public virtual object should_devour(mob_living victim = null)
    {
        return victim.stat == DEAD || (Dm.num(victim.health) <= Dm.num(victim.dead_threshold) && HAS_TRAIT(victim, TRAIT_NODEATH)) ? TRUE : FALSE;
    }

    /// Devours a target and restores health to the megafauna
    // tg: code/modules/mob/living/basic/boss/boss.dm:93
    public virtual async DmTask<object> devour(mob_living victim = null)
    {
        if (victim == null || Dm.truthy(victim.has_status_effect(typeof(datum_status_effect_gutted))))
            return FALSE;
        celebrate_kill(victim);
        if (!Dm.truthy(is_station_level(z)) || client != null) //NPC monsters won't heal while on station
            await heal_overall_damage(Dm.num(victim.maxHealth) * 0.5f);
        victim.investigate_log(Dm.text("has been devoured by [].", this), INVESTIGATE_DEATHS);
        if (iscarbon(victim))
        {
            qdel(victim.get_organ_slot(ORGAN_SLOT_LUNGS));
            qdel(victim.get_organ_slot(ORGAN_SLOT_HEART));
            qdel(victim.get_organ_slot(ORGAN_SLOT_LIVER));
        }
        await victim.adjust_brute_loss(500);
        await victim.death(); //make sure they die
        await victim.apply_status_effect(typeof(datum_status_effect_gutted));
        return TRUE;
    }

    /// Small little taunt when we epically troll someone
    // tg: code/modules/mob/living/basic/boss/boss.dm:110
    public virtual void celebrate_kill(mob_living poor_sap = null)
    {
        visible_message(
            span_danger(I18n.T("[] disembowels []!", this, poor_sap)),
            span_userdanger(I18n.T("You feast on []'s organs, restoring your health!", poor_sap)));
    }

    /// Handles adding all relevant achievements when applicable (probably when we are defeated)
    /// Achievements being null/no length is handled in the element itself.
    // tg: code/modules/mob/living/basic/boss/boss.dm:118
    public virtual async DmTask handle_achievements()
    {
        if (Dm.length(achievements) <= 0)
            return;
        await AddElement(typeof(datum_element_kill_achievement), string_list(achievements), crusher_achievement_type, victor_memory_type);
    }

    /// Handles adding crusher loot when applicable (probably when we are defeated)
    // tg: code/modules/mob/living/basic/boss/boss.dm:124
    public virtual async DmTask handle_crusher_loot()
    {
        if (crusher_loot == null)
            return;
        await AddElement(
            typeof(datum_element_crusher_loot),
            Dm.named("trophy_type", string_list(crusher_loot)),
            Dm.named("guaranteed_drop", 0.6f),
            Dm.named("drop_immediately", Dm.bitand(basic_mob_flags, DEL_ON_DEATH)));
    }
}
