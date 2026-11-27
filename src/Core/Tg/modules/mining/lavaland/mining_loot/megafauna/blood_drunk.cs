// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/megafauna/blood_drunk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// Cleaving saw

// tg: code/modules/mining/lavaland/mining_loot/megafauna/blood_drunk.dm:3
public partial class obj_item_melee_cleaving_saw : obj_item_melee
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/blood_drunk.dm:118  var/static/list/cleaving_saw_cleave_angles = list(0, -45, 45) (a proc-level static, rules 10.6)
    private static ref DmList attack__cleaving_saw_cleave_angles => ref TgState.Current.obj_item_melee_cleaving_saw__attack__cleaving_saw_cleave_angles;

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/blood_drunk.dm:35
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_transforming),
            Dm.named("transform_cooldown_time", CLICK_CD_MELEE * 0.25f),
            Dm.named("force_on", open_force),
            Dm.named("throwforce_on", open_throwforce),
            Dm.named("sharpness_on", sharpness),
            Dm.named("hitsound_on", hitsound),
            Dm.named("w_class_on", w_class),
            Dm.named("attack_verb_continuous_on", Dm.list("cleaves", "swipes", "slashes", "chops")),
            Dm.named("attack_verb_simple_on", Dm.list("cleave", "swipe", "slash", "chop"))));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/blood_drunk.dm:50
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE)
            ? I18n.T("It is open, will cleave enemies in a wide arc and deal additional damage to fauna.")
            : I18n.T("It is closed, and can be used for rapid consecutive attacks that cause fauna to bleed.")));
        Dm.append(ref dot, span_notice(I18n.T("Both modes will build up existing bleed effects, doing a burst of high damage if the bleed is built up high enough.")));
        Dm.append(ref dot, span_notice(I18n.T("Transforming it immediately after an attack causes the next attack to come out faster.")));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/blood_drunk.dm:56
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE)
            ? I18n.T("[] is closing [] on [] neck! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())
            : I18n.T("[] is opening [] into [] chest! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        await attack_self(user);
        return BRUTELOSS;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/blood_drunk.dm:61
    public override async DmTask<object> melee_attack_chain(mob user = null, atom target = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.melee_attack_chain(user, target, modifiers, attack_modifiers);
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            user.changeNext_move(CLICK_CD_MELEE * 0.5f); //when closed, it attacks very rapidly
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/blood_drunk.dm:66
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living target = target_mob;
        bool is_open = HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE);
        if (!is_open || Dm.truthy(swiping) || !Dm.truthy(target.density) || get_turf(target) == get_turf(user))
        {
            foreach (object found_faction in Dm.each(target.get_faction()))
            {
                if (!Dm.@in(found_faction, nemesis_factions))
                    continue;
                if (is_open)
                {
                    DmList modifier_list = attack_modifiers as DmList;
                    modifier_list[FORCE_MODIFIER] = Dm.num(modifier_list[FORCE_MODIFIER]) + faction_bonus_force; // MODIFY_ATTACK_FORCE
                }
                await nemesis_effects(user, target);
                break;
            }
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        }

        turf user_turf = get_turf(user);
        float dir_to_target = get_dir(user_turf, get_turf(target));
        swiping = TRUE;
        foreach (object i in Dm.each(attack__cleaving_saw_cleave_angles)) //so that the animation animates towards the target clicked and not towards a side target
        {
            turf turf = get_step(user_turf, turn(dir_to_target, Dm.num(i)));
            foreach (mob_living living_target in Dm.each<mob_living>(turf))
            {
                if (Dm.truthy(await user.Adjacent(living_target)) && !Dm.eq(living_target.body_position, LYING_DOWN))
                    await melee_attack_chain(user, living_target);
            }
        }
        swiping = FALSE;
        return null;
    }

    /*
     * If we're attacking [target]s in our nemesis list, apply unique effects.
     *
     * user - the mob attacking with the saw
     * target - the mob being attacked
     */
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/blood_drunk.dm:94
    public virtual async DmTask nemesis_effects(mob_living user = null, mob_living target = null)
    {
        if (istype(target, typeof(mob_living_simple_animal_hostile_asteroid_elite)))
            return;
        datum_status_effect_stacking_saw_bleed existing_bleed = target.has_status_effect(typeof(datum_status_effect_stacking_saw_bleed)) as datum_status_effect_stacking_saw_bleed;
        if (existing_bleed != null)
            existing_bleed.add_stacks(bleed_stacks_per_hit);
        else
            await target.apply_status_effect(typeof(datum_status_effect_stacking_saw_bleed), bleed_stacks_per_hit);
    }

    /*
     * Signal proc for [COMSIG_TRANSFORMING_ON_TRANSFORM].
     *
     * Gives feedback and makes the nextmove after transforming much quicker.
     */
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/blood_drunk.dm:109
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        user.changeNext_move(CLICK_CD_MELEE * 0.25f);
        if (user != null)
            balloon_alert(user, Dm.truthy(active) ? I18n.T("opened []", this) : I18n.T("closed []", this));
        playsound(this, Dm.resource("sound/effects/magic/clockwork/fellowship_armory.ogg"), 35, TRUE, frequency: 90000 - (Dm.num(active) * 30000));
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }
}

// Wildhunter's butchering knife

// tg: code/modules/mining/lavaland/mining_loot/megafauna/blood_drunk.dm:121
public partial class obj_item_knife_hunting_wildhunter : obj_item_knife_hunting
{
    //best butchering tool
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/blood_drunk.dm:136
    public override void set_butchering()
    {
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_butchering),
            Dm.named("speed", 1.5f * SECONDS),
            Dm.named("effectiveness", 125),
            Dm.named("bonus_modifier", 0)));
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/blood_drunk.dm:144
    public override void make_stabby()
    {
        return;
    }

    //cut those trophies
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/blood_drunk.dm:148
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with is not obj_item_crusher_trophy trophy)
            return NONE;
        if (!Dm.truthy(Dm.length(trophy.wildhunter_drops)))
            return NONE;
        balloon_alert(user, I18n.T("cutting trophy..."));
        if (!Dm.truthy(await do_after(user, 4 * SECONDS, trophy)))
            return ITEM_INTERACT_BLOCKING;
        foreach ((object path, object count) in Dm.pairs(trophy.wildhunter_drops))
        {
            if (ispath(path, typeof(obj_item_stack)))
            {
                Dm.@new((System.Type)path, trophy.drop_location(), count);
                continue;
            }

            for (int i = 1; i <= Dm.num(count); i++)
                Dm.@new((System.Type)path, trophy.drop_location());
        }
        qdel(trophy);
        return ITEM_INTERACT_SUCCESS;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/blood_drunk.dm:118
    internal DmList obj_item_melee_cleaving_saw__attack__cleaving_saw_cleave_angles = Dm.list(0, -45, 45);
}
