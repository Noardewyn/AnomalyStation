// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/jungle/venus_human_trap.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4b (D327): the whole file, the flowering kudzu's man-trap: the flower bud (it grows for two minutes through its four buds,
// lashes four thorny vines out to its diagonals and bears a venus human trap for a ghost), the thick vine that cuts whoever walks
// into it, the venus human trap (heals on the vines and wilts off them, drags its prey in with its tangle) and its AI controller
// (a var block: the ranged ability tree). The var blocks are generated (D111).

/**
 * Kudzu Flower Bud
 *
 * A flower created by flowering kudzu which spawns a venus human trap after a certain amount of time has passed.
 *
 * A flower created by kudzu with the flowering mutation.  Spawns a venus human trap after 2 minutes under normal circumstances.
 * Also spawns 4 vines going out in diagonal directions from the bud.  Any living creature not aligned with plants is damaged by these vines.
 * Once it grows a venus human trap, the bud itself will destroy itself.
 *
 */
/// Flower bud structure that ghost role spawns, actual spawn logic handled by /obj/effect/mob_spawn/ghost_role/venus_human_trap
//inheriting basic attack/damage stuff from alien structures
// tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:13
public partial class obj_structure_alien_resin_flower_bud : obj_structure_alien_resin
{
    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:1  #define FINAL_BUD_GROWTH_ICON 3 (file-local, rules 11.4)
    private const int FINAL_BUD_GROWTH_ICON = 3;

    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        spawner = Dm.@new<obj_effect_mob_spawn_ghost_role_venus_human_trap>(get_turf(loc));
        spawner.flower_bud = this;
        countdown = Dm.@new<obj_effect_countdown_flower_bud>(this);
        DmList anchors = Dm.list();
        Dm.append(ref anchors, locate(x - 2, y + 2, z));
        Dm.append(ref anchors, locate(x + 2, y + 2, z));
        Dm.append(ref anchors, locate(x - 2, y - 2, z));
        Dm.append(ref anchors, locate(x + 2, y - 2, z));

        foreach (turf T in Dm.each<turf>(anchors))
            Dm.append(ref vines, Beam(T, "vine", maxdistance: 5, beam_type: typeof(obj_effect_ebeam_vine)));
        finish_time = world.time + Dm.num(growth_time);
        _addtimer(Dm.@new<datum_callback>(this, nameof(bear_fruit)), growth_time, file: "code/modules/mob/living/basic/jungle/venus_human_trap.dm", line: 51);
        _addtimer(Dm.@new<datum_callback>(this, nameof(progress_growth)), Dm.num(growth_time) / 4, file: "code/modules/mob/living/basic/jungle/venus_human_trap.dm", line: 52);
        countdown.start();
        return dot;
    }

    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:55
    public override async DmTask<object> run_atom_armor(object damage_amount = null, object damage_type = null, object damage_flag = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_flag ??= 0;
        datum_spacevine_mutation_fire_proof fire_proof = Dm.locate<datum_spacevine_mutation_fire_proof>(mutations);
        if (fire_proof != null && Dm.eq(damage_type, BURN))
            damage_amount = 0;

        object dot = await base.run_atom_armor(damage_amount, damage_type, damage_flag, attack_dir, armour_penetration);
        return dot;
    }

    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:62
    public override async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        obj_item item = attacking_item;
        LAZYSET(ref attack_modifiers, SILENCE_DEFAULT_MESSAGES, TRUE);
        LAZYSET(ref attack_modifiers, FORCE_MULTIPLIER, 1);
        if (item.damtype == BURN)
        {
            // MODIFY_ATTACK_FORCE_MULTIPLIER(attack_modifiers, 4) (__DEFINES/combat.dm:427)
            if (!islist(attack_modifiers)) { attack_modifiers = Dm.list(); }
            if (!Dm.@in(FORCE_MULTIPLIER, attack_modifiers)) { attack_modifiers[FORCE_MULTIPLIER] = 1; }
            attack_modifiers[FORCE_MULTIPLIER] = Dm.mul(attack_modifiers[FORCE_MULTIPLIER], 4);
        }
        if (Dm.truthy(item.get_sharpness()))
        {
            // MODIFY_ATTACK_FORCE_MULTIPLIER(attack_modifiers, 16): alien resin applies 75% reduction to brute damage so this actually x4 damage
            if (!islist(attack_modifiers)) { attack_modifiers = Dm.list(); }
            if (!Dm.@in(FORCE_MULTIPLIER, attack_modifiers)) { attack_modifiers[FORCE_MULTIPLIER] = 1; }
            attack_modifiers[FORCE_MULTIPLIER] = Dm.mul(attack_modifiers[FORCE_MULTIPLIER], 16);
        }
        return await base.attacked_by(attacking_item, user, modifiers, attack_modifiers);
    }

    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:71
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(vines);
        QDEL_NULL(ref countdown);
        if (spawner != null) // anti harddel checks
        {
            if (!QDELETED(spawner))
                qdel(spawner);
            spawner = null;
        }
        return base.Destroy(force);
    }

    /// Tells the spawner that the venus human trap is ready
    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:81
    public virtual void bear_fruit()
    {
        visible_message(span_danger(I18n.T("The plant has borne fruit!")));
        if (spawner != null)
            spawner.bear_fruit();
    }

    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:86
    public virtual void progress_growth()
    {
        growth_icon++;
        icon_state = Dm.text("bud[]", growth_icon);
        if (growth_icon == FINAL_BUD_GROWTH_ICON)
            return;
        _addtimer(Dm.@new<datum_callback>(this, nameof(progress_growth)), Dm.num(growth_time) / 4, file: "code/modules/mob/living/basic/jungle/venus_human_trap.dm", line: 91);
    }

    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:93
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        await spawner.attack_ghost(user);
        return null;
    }
}

// tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:96
public partial class obj_effect_ebeam_vine : obj_effect_ebeam
{
    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:103  var/static/list/loc_connections (rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_effect_ebeam_vine__Initialize__loc_connections;

    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:101
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        return dot;
    }

    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:108
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable AM = null)
    {
        if (isliving(AM))
        {
            mob_living L = AM as mob_living;
            if (!Dm.truthy(isvineimmune(L)))
            {
                Waiting.NoSleep(L.adjust_brute_loss(5));
                to_chat(L, span_alert(I18n.T("You cut yourself on the thorny vines.")));
            }
        }
    }
}

/**
 * Venus Human Trap
 *
 * The result of a kudzu flower bud, these enemies use vines to drag prey close to them for attack.
 *
 * A carnivorious plant which uses vines to catch and ensnare prey.  Spawns from kudzu flower buds.
 * Each one can attach up to two temporary vines to objects or mobs and drag them around with it.
 * Attempting to attach a vine to something with a vine already attached to it will pull all movable targets closer on command.
 * Once the prey is in melee range, melee attacks from the venus human trap heals itself for 10% of its max health, assuming the target is alive.
 * Akin to certain spiders, venus human traps can also be possessed and controlled by ghosts.
 *
 */
// tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:129
public partial class mob_living_basic_venus_human_trap : mob_living_basic
{
    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:176
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_lifesteal), 5));
        DmList innate_actions = TgState.Current.mob_living_basic_venus_human_trap__innate_actions ??= Dm.list(
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_projectile_attack_vine_tangle), BB_TARGETED_ACTION));
        grant_actions_by_list(innate_actions);
        return dot;
    }

    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:184
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref glow);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:188
    public override async DmTask<object> RangedAttack(atom victim = null, object modifiers = null)
    {
        if (!Dm.truthy(combat_mode))
            return null;
        datum_action_cooldown_mob_cooldown tangle_ability = (ai_controller as datum_ai_controller)?.blackboard[BB_TARGETED_ACTION] as datum_action_cooldown_mob_cooldown;
        if (tangle_ability == null)
            return null;
        await tangle_ability.Trigger(null, null, victim);
        return null;
    }

    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:196
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        object dot = base.Life(seconds_per_tick, __extra);
        if (!Dm.truthy(dot))
            return FALSE;

        object vines_in_range = Dm.locate<obj_structure_spacevine>(Builtins.range(kudzu_off_distance_range, this));
        if (!Dm.truthy(vines_in_range) && !Dm.truthy(alert_shown))
        {
            alert_shown = TRUE;
            balloon_alert(this, I18n.T("do not leave vines!"));
        }
        else if (Dm.truthy(vines_in_range))
            alert_shown = FALSE;

        Waiting.NoSleep(adjust_brute_loss(Dm.truthy(vines_in_range) ? -weed_heal : no_weed_damage)); //every life tick take 20 damage if not near vines or heal 10 if near vines, 5 times out of weeds = u ded
        return dot;
    }
}

// tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:210
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_vine_tangle : datum_action_cooldown_mob_cooldown_projectile_attack
{
    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:223
    public override async DmTask Remove(mob remove_from = null)
    {
        QDEL_LIST(vines);
        await base.Remove(remove_from);
    }

    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:227
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        if (!ismovable(target_atom) || target_atom is obj_structure_spacevine)
            return null;
        atom_movable target_movable = target_atom as atom_movable;
        if (Dm.truthy(target_movable.anchored))
        {
            owner.balloon_alert(owner, I18n.T("can't pull!"));
            return null;
        }
        if (get_dist(owner, target_atom) > vine_grab_distance)
        {
            owner.balloon_alert(owner, I18n.T("too far!"));
            return null;
        }
        DmList target_turfs = Dm.sub(get_line(owner, target_atom), Dm.list(get_turf(owner), get_turf(target_atom))) as DmList;
        foreach (turf blockage in Dm.each<turf>(target_turfs))
        {
            if (Dm.truthy(await blockage.is_blocked_turf(exclude_mobs: TRUE)))
            {
                owner.balloon_alert(owner, I18n.T("path blocked!"));
                return null;
            }
        }

        datum_beam new_vine = owner.Beam(target_atom, icon_state: "vine", time: vine_duration * (ismob(target_atom) ? 1 : 2), beam_type: typeof(obj_effect_ebeam_vine), emissive: NONE) as datum_beam;
        object component = await target_movable.AddComponent(typeof(datum_component_leash), owner, vine_grab_distance);
        // tg passes the beam as RegisterSignal's override (a truthy value)
        RegisterSignal(new_vine, COMSIG_QDELETING, nameof(remove_vine), @override: TRUE);
        vines[new_vine] = component;
        if (isliving(target_atom))
        {
            mob_living victim = target_atom as mob_living;
            await victim.Knockdown(2 * SECONDS);
        }
        StartCooldown();
        return TRUE;
    }

    /**
     * Removes a vine from the list.
     *
     * Removes the vine from our list.
     * Called specifically when the vine is about to be destroyed, so we don't have any null references.
     * Arguments:
     * * datum/beam/vine - The vine to be removed from the list.
     */
    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:260
    [SignalHandler]
    public virtual void remove_vine(datum_beam vine = null)
    {
        qdel(vines[vine]);
        Dm.remove(ref vines, vine);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:103  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_effect_ebeam_vine__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_effect_ebeam_vine.on_entered)));

    // tg: code/modules/mob/living/basic/jungle/venus_human_trap.dm:179  var/static/list/innate_actions
    internal DmList mob_living_basic_venus_human_trap__innate_actions;
}
