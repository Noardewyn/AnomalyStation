// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/cursed_katana.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:8
public partial class obj_item_organ_cyberimp_arm_toolkit_shard : obj_item_organ_cyberimp_arm_toolkit
{
    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:18
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        to_chat(user, span_userdanger(I18n.T("The mass goes up your arm and goes inside it!")));
        playsound(user, Dm.resource("sound/effects/magic/demon_consume.ogg"), 50, TRUE);
        object index = user.get_held_index_of_item(this);
        await swap_zone(IS_LEFT_INDEX(Dm.num(index)) ? BODY_ZONE_L_ARM : BODY_ZONE_R_ARM);
        await user.temporarilyRemoveItemFromInventory(this, TRUE);
        await Insert(user as mob_living_carbon);
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:27
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item screwtool = null)
    {
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:30
public partial class obj_item_organ_cyberimp_arm_toolkit_shard_katana : obj_item_organ_cyberimp_arm_toolkit_shard
{
    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:35
    public override async DmTask<object> Retract()
    {
        obj_item_cursed_katana katana = active_item as obj_item_cursed_katana;
        if (katana == null || Dm.truthy(katana.shattered))
            return FALSE;
        if (!Dm.truthy(katana.drew_blood))
        {
            to_chat(owner, span_userdanger(I18n.T("[] lashes out at you in hunger!", katana)));
            playsound(owner, Dm.resource("sound/effects/magic/demon_attack1.ogg"), 50, TRUE);
            await owner.apply_damage(25, BRUTE, hand, wound_bonus: 10, sharpness: SHARP_EDGED);
        }
        katana.drew_blood = FALSE;
        await katana.wash(CLEAN_TYPE_BLOOD);
        return await base.Retract();
    }
}

// tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:47
public partial class obj_item_cursed_katana : obj_item
{
    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:1-6 (file-local #defines)
    private const string ATTACK_STRIKE = "Hilt Strike";
    private const string ATTACK_SLICE = "Wide Slice";
    private const string ATTACK_DASH = "Dash Attack";
    private const string ATTACK_CUT = "Tendon Cut";
    private const string ATTACK_CLOAK = "Dark Cloak";
    private const string ATTACK_SHATTER = "Shatter";

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:69 (var/static/list/combo_list)
    public static DmList combo_list => TgState.Current.obj_item_cursed_katana__combo_list ??= Dm.list(
        Dm.kv(ATTACK_STRIKE, Dm.list(Dm.kv(COMBO_STEPS, Dm.list(LEFT_ATTACK, LEFT_ATTACK, RIGHT_ATTACK)), Dm.kv(COMBO_PROC, nameof(strike)))),
        Dm.kv(ATTACK_SLICE, Dm.list(Dm.kv(COMBO_STEPS, Dm.list(RIGHT_ATTACK, LEFT_ATTACK, LEFT_ATTACK)), Dm.kv(COMBO_PROC, nameof(slice)))),
        Dm.kv(ATTACK_DASH, Dm.list(Dm.kv(COMBO_STEPS, Dm.list(LEFT_ATTACK, RIGHT_ATTACK, RIGHT_ATTACK)), Dm.kv(COMBO_PROC, nameof(dash)))),
        Dm.kv(ATTACK_CUT, Dm.list(Dm.kv(COMBO_STEPS, Dm.list(RIGHT_ATTACK, RIGHT_ATTACK, LEFT_ATTACK)), Dm.kv(COMBO_PROC, nameof(cut)))),
        Dm.kv(ATTACK_CLOAK, Dm.list(Dm.kv(COMBO_STEPS, Dm.list(LEFT_ATTACK, RIGHT_ATTACK, LEFT_ATTACK, RIGHT_ATTACK)), Dm.kv(COMBO_PROC, nameof(cloak)))),
        Dm.kv(ATTACK_SHATTER, Dm.list(Dm.kv(COMBO_STEPS, Dm.list(RIGHT_ATTACK, LEFT_ATTACK, RIGHT_ATTACK, LEFT_ATTACK)), Dm.kv(COMBO_PROC, nameof(shatter)))));

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:80
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Waiting.NoSleep(AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple));
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_combo_attacks),
            Dm.named("combos", combo_list),
            Dm.named("max_combo_length", 4),
            Dm.named("examine_message", span_notice(I18n.T("<i>There seem to be inscriptions on it... you could examine them closer?</i>"))),
            Dm.named("reset_message", "you return to neutral stance"),
            Dm.named("can_attack_callback", Dm.@new<datum_callback>(this, nameof(can_combo_attack)))));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:94
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, Dm.truthy(drew_blood) ? span_nicegreen(I18n.T("It's sated... for now.")) : span_danger(I18n.T("It will not be sated until it tastes blood.")));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:98
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (isturf(loc))
            qdel(this);
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:103
    public override async DmTask<object> attack__proc(mob_living target = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (target.stat != DEAD && target != user)
        {
            drew_blood = TRUE;
            if (ismining(target))
                user.changeNext_move(CLICK_CD_RAPID);
        }
        return await base.attack__proc(target, user, modifiers, attack_modifiers, __extra);
    }

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:110
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (attack_type == PROJECTILE_ATTACK || attack_type == LEAP_ATTACK || attack_type == OVERWHELMING_ATTACK)
            final_block_chance = 0; //Don't bring a sword to a gunfight, and also you aren't going to really block someone full body tackling you with a sword. Or a road roller, if one happened to hit you.
        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:115
    public virtual object can_combo_attack(mob user = null, mob_living target = null)
    {
        return Dm.num(target.stat != DEAD && target != user);
    }

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:118
    public virtual async DmTask strike(mob_living target = null, mob user = null)
    {
        user.visible_message(span_warning(I18n.T("[] strikes [] with []'s hilt!", user, target, this)),
            span_notice(I18n.T("You hilt strike []!", target)));
        to_chat(target, span_userdanger(I18n.T("You've been struck by []!", user)));
        playsound(this, Dm.resource("sound/items/weapons/genhit3.ogg"), 50, TRUE);
        RegisterSignal(target, COMSIG_MOVABLE_IMPACT, nameof(strike_throw_impact));
        atom throw_target = get_edge_target_turf(target, user.dir) as atom;
        await target.throw_at(throw_target, 5, 3, user, FALSE, gentle: TRUE);
        await target.apply_damage(damage: 17, exposed_wound_bonus: 10);
        to_chat(target, span_userdanger(I18n.T("You've been struck by []!", user)));
        await user.do_attack_animation(target, ATTACK_EFFECT_PUNCH);
    }

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:130
    [SignalHandler]
    public virtual object strike_throw_impact(mob_living source = null, atom hit_atom = null, datum_thrownthing thrownthing = null)
    {
        UnregisterSignal(source, COMSIG_MOVABLE_IMPACT);
        if (isclosedturf(hit_atom))
        {
            Waiting.NoSleep(source.apply_damage(damage: 5));
            if (ishostile(source))
            {
                mob_living_simple_animal_hostile target = source as mob_living_simple_animal_hostile;
                target.ranged_cooldown = Dm.num(target.ranged_cooldown) + 5 * SECONDS;
            }
            else if (iscarbon(source))
            {
                mob_living_carbon target = source as mob_living_carbon;
                Waiting.NoSleep(target.set_confusion_if_lower(8 * SECONDS));
            }
        }
        return NONE;
    }

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:144
    public virtual async DmTask slice(mob_living target = null, mob user = null)
    {
        user.visible_message(span_warning(I18n.T("[] does a wide slice!", user)),
            span_notice(I18n.T("You do a wide slice!")));
        playsound(this, Dm.resource("sound/items/weapons/bladeslice.ogg"), 50, TRUE);
        user.do_item_attack_animation(target, used_item: this, animation_type: ATTACK_ANIMATION_SLASH);
        turf user_turf = get_turf(user);
        object dir_to_target = get_dir(user_turf, get_turf(target));
        // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:153 (var/static/list/cursed_katana_slice_angles)
        DmList cursed_katana_slice_angles = TgState.Current.obj_item_cursed_katana__slice__cursed_katana_slice_angles ??= Dm.list(0, -45, 45, -90, 90); //so that the animation animates towards the target clicked and not towards a side target
        foreach (object iteration in Dm.each(cursed_katana_slice_angles))
        {
            turf turf = get_step(user_turf, turn(Dm.num(dir_to_target), Dm.num(iteration)));
            await user.do_attack_animation(turf, ATTACK_EFFECT_SLASH);
            foreach (mob_living additional_target in Dm.each<mob_living>(turf))
            {
                if (Dm.truthy(await user.Adjacent(additional_target)) && Dm.truthy(additional_target.density))
                {
                    await additional_target.apply_damage(damage: 15, sharpness: SHARP_EDGED, exposed_wound_bonus: 10);
                    to_chat(additional_target, span_userdanger(I18n.T("You've been sliced by []!", user)));
                }
            }
        }
        await target.apply_damage(damage: 5, sharpness: SHARP_EDGED, wound_bonus: 10);
    }

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:161
    public virtual void cloak(mob_living target = null, mob user = null)
    {
        user.alpha = 150;
        user.SetInvisibility(INVISIBILITY_OBSERVER, id: type); // so hostile mobs cant see us or target us
        RegisterSignal(user, COMSIG_MOB_UPDATE_SIGHT, nameof(on_update_sight));
        user.update_sight();
        user.visible_message(span_warning(I18n.T("[] vanishes into thin air!", user)),
            span_notice(I18n.T("You enter the dark cloak.")));
        Dm.@new<obj_effect_temp_visual_mook_dust>(get_turf(this));
        playsound(this, Dm.resource("sound/effects/magic/smoke.ogg"), 50, TRUE);
        if (ishostile(target))
        {
            mob_living_simple_animal_hostile hostile_target = target as mob_living_simple_animal_hostile;
            if (hostile_target.target == user)
                hostile_target.LoseTarget();
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(uncloak), user), 5 * SECONDS, TIMER_UNIQUE, file: "code/modules/mining/lavaland/mining_loot/cursed_katana.dm", line: 179);
    }

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:176
    [SignalHandler]
    public virtual void on_update_sight(mob source = null)
    {
        source.add_sight(SEE_SELF);
    }

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:180
    public virtual void uncloak(mob user = null)
    {
        user.alpha = 255;
        user.RemoveInvisibility(type);
        UnregisterSignal(user, COMSIG_MOB_UPDATE_SIGHT);
        user.update_sight();
        user.visible_message(span_warning(I18n.T("[] appears from thin air!", user)),
            span_notice(I18n.T("You exit the dark cloak.")));
        playsound(this, Dm.resource("sound/effects/magic/summonitems_generic.ogg"), 50, TRUE);
        Dm.@new<obj_effect_temp_visual_mook_dust>(get_turf(this));
    }

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:190
    public virtual async DmTask cut(mob_living target = null, mob user = null)
    {
        user.visible_message(span_warning(I18n.T("[] cuts []'s tendons!", user, target)),
            span_notice(I18n.T("You tendon cut []!", target)));
        to_chat(target, span_userdanger(I18n.T("Your tendons have been cut by []!", user)));
        user.do_item_attack_animation(target, used_item: this, animation_type: ATTACK_ANIMATION_SLASH);
        await target.apply_damage(damage: 15, sharpness: SHARP_EDGED, wound_bonus: 15);
        await user.do_attack_animation(target, ATTACK_EFFECT_DISARM);
        playsound(this, Dm.resource("sound/items/weapons/rapierhit.ogg"), 50, TRUE);
        datum_status_effect_stacking_saw_bleed_bloodletting status = target.has_status_effect(typeof(datum_status_effect_stacking_saw_bleed_bloodletting)) as datum_status_effect_stacking_saw_bleed_bloodletting;
        if (status == null)
            await target.apply_status_effect(typeof(datum_status_effect_stacking_saw_bleed_bloodletting), 6);
        else
            status.add_stacks(6);
    }

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:204
    public virtual async DmTask dash(mob_living target = null, mob user = null)
    {
        user.visible_message(span_warning(I18n.T("[] dashes through []!", user, target)),
            span_notice(I18n.T("You dash through []!", target)));
        to_chat(target, span_userdanger(I18n.T("[] dashes through you!", user)));
        playsound(this, Dm.resource("sound/effects/magic/blink.ogg"), 50, TRUE);
        await target.apply_damage(damage: 17, sharpness: SHARP_POINTY, exposed_wound_bonus: 10);
        turf dash_target = get_turf(target);
        for (float distance = 0, distance__end = 8; distance <= distance__end; distance++)
        {
            turf current_dash_target = dash_target;
            current_dash_target = get_step(current_dash_target, user.dir);
            if (!Dm.truthy(await current_dash_target.is_blocked_turf(TRUE)))
                dash_target = current_dash_target;
            else
                break;
        }
        Dm.@new<obj_effect_temp_visual_guardian_phase_out>(get_turf(user));
        Dm.@new<obj_effect_temp_visual_guardian_phase>(dash_target);
        await do_teleport(user, dash_target, channel: TELEPORT_CHANNEL_MAGIC);
    }

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:222
    public virtual async DmTask shatter(mob_living target = null, mob user = null)
    {
        user.visible_message(span_warning(I18n.T("[] shatters [] over []!", user, this, target)),
            span_notice(I18n.T("You shatter [] over []!", this, target)));
        to_chat(target, span_userdanger(I18n.T("[] shatters [] over you!", user, this)));
        await target.apply_damage(damage: ismining(target) ? 75 : 35, wound_bonus: 20);
        await user.do_attack_animation(target, ATTACK_EFFECT_SMASH);
        playsound(this, Dm.resource("sound/effects/glass/glassbr3.ogg"), 100, TRUE);
        shattered = TRUE;
        await moveToNullspace();
        balloon_alert(user, "katana shattered");
        _addtimer(Dm.@new<datum_callback>(this, nameof(coagulate), user), 45 * SECONDS, file: "code/modules/mining/lavaland/mining_loot/cursed_katana.dm", line: 238);
    }

    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:234
    public virtual void coagulate(mob user = null)
    {
        balloon_alert(user, "katana coagulated");
        shattered = FALSE;
        playsound(this, Dm.resource("sound/effects/magic/demon_consume.ogg"), 50, TRUE);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:69 (var/static/list/combo_list)
    internal DmList obj_item_cursed_katana__combo_list;
    // tg: code/modules/mining/lavaland/mining_loot/cursed_katana.dm:153 (var/static/list/cursed_katana_slice_angles)
    internal DmList obj_item_cursed_katana__slice__cursed_katana_slice_angles;
}
