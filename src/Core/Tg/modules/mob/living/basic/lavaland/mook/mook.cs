// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/mook/mook.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


//Fragile but highly aggressive wanderers that pose a large threat in numbers.
//They'll attempt to leap at their target from afar using their hatchets.
// tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:3
public partial class mob_living_basic_mining_mook : mob_living_basic_mining
{
    /// Things we want to find to heal
    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:48 (var/static/list/heal_targets)
    public static DmList heal_targets => TgState.Current.mob_living_basic_mining_mook__heal_targets ??= Dm.list(typeof(mob_living_basic_mining_mook_worker));

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(
            typeof(datum_element_change_force_on_death),
            Dm.named("move_resist", MOVE_RESIST_DEFAULT)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_ai_retaliate_advanced), Dm.@new<datum_callback>(this, nameof(attack_intruder))));
        grant_actions_by_list(get_innate_abilities());

        ore_overlay = mutable_appearance(icon, "mook_ore_overlay") as mutable_appearance;

        Waiting.NoSleep(AddComponent(typeof(datum_component_ai_listen_to_weather)));
        Waiting.NoSleep(AddElement(typeof(datum_element_wall_tearer), Dm.named("allow_reinforced", FALSE)));
        RegisterSignal(this, COMSIG_KB_MOB_DROPITEM_DOWN, nameof(drop_ore));

        if (Dm.truthy(is_healer))
            grant_healer_abilities();

        Waiting.NoSleep(AddComponent(typeof(datum_component_obeys_commands), pet_commands));
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_MOOK_HEAL_TARGETS, typecacheof(heal_targets));
        return dot;
    }

    /// Returns a list of actions and blackboard keys to pass into `grant_actions_by_list`.
    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:73
    public virtual DmList get_innate_abilities()
    {
        return TgState.Current.mob_living_basic_mining_mook__get_innate_abilities__innate_abilities ??= Dm.list(
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_mook_ability_mook_jump), BB_MOOK_JUMP_ABILITY));
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:79
    public virtual void grant_healer_abilities()
    {
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_healing_touch),
            Dm.named("heal_brute", melee_damage_upper),
            Dm.named("heal_burn", melee_damage_upper),
            Dm.named("heal_time", 0),
            Dm.named("valid_targets_typecache", typecacheof(Dm.list(typeof(mob_living_basic_mining_mook))))));
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:88
    public override async DmTask<object> Entered(atom_movable mover = null, atom old_loc = null, DmList old_locs = null)
    {
        if (mover is obj_item_stack_ore)
        {
            held_ore = mover as obj;
            (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_SIMPLE_CARRY_ITEM, mover);
            update_appearance(UPDATE_OVERLAYS);
        }

        return await base.Entered(mover, old_loc, old_locs);
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:96
    public override async DmTask<object> Exited(atom_movable mover = null, object direction = null)
    {
        object dot = await base.Exited(mover, direction);
        if (held_ore != mover)
            return dot;
        held_ore = null;
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:103
    public override async DmTask<object> early_melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.early_melee_attack(target, modifiers, ignore_cooldown);
        if (Dm.truthy(dot))
            return dot;
        return await attack_sequence(target);
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:109
    public virtual async DmTask<object> attack_sequence(atom target = null)
    {
        if (target is obj_item_stack_ore && held_ore == null)
        {
            obj_item ore_target = target as obj_item;
            await ore_target.forceMove(this);
            return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
        }

        if (target is obj_structure_ore_container_material_stand)
        {
            if (held_ore != null)
                await held_ore.forceMove(target);
            return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
        }

        if (target is obj_structure_bonfire fire_target)
        {
            if (!Dm.truthy(fire_target.burning))
                await fire_target.start_burning();
            return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
        }
        return null;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:126
    public virtual void change_combatant_state(object state = null)
    {
        attack_state = Dm.num(state);
        update_appearance();
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:130
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref held_ore);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:134
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (stat == DEAD)
            return dot;
        switch (attack_state)
        {
            case MOOK_ATTACK_NEUTRAL:
                icon_state = "mook";
                break;
            case MOOK_ATTACK_WARMUP:
                icon_state = "mook_warmup";
                break;
            case MOOK_ATTACK_ACTIVE:
                icon_state = "mook_leap";
                break;
            case MOOK_ATTACK_STRIKE:
                icon_state = "mook_strike";
                break;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:148
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (stat == DEAD)
            return dot;

        if (attack_state != MOOK_ATTACK_NEUTRAL || held_ore == null)
            return dot;

        Dm.append(ref dot, ore_overlay);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:158
    public override async DmTask<object> throw_at(atom target = null, object range = null, object speed = null, atom thrower = null, float? spin = null, float? diagonals_first = null, datum_callback callback = null, float? force = null, float? gentle = null, object quickstart = null, Type throw_datum_typepath = null, params object[] __extra)
    {
        change_combatant_state(state: MOOK_ATTACK_ACTIVE);
        return await base.throw_at(target, range, speed, thrower, spin, diagonals_first, callback, force, gentle, quickstart, throw_datum_typepath, __extra);
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:162
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        change_combatant_state(state: MOOK_ATTACK_NEUTRAL);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:166
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);

        if (Dm.truthy(dot))
            return TRUE;

        if (!(mover is mob_living_basic_mining_mook mook_moover))
            return FALSE;

        if (mook_moover.attack_state == MOOK_ATTACK_ACTIVE)
            return TRUE;
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:179
    [SignalHandler]
    public virtual object drop_ore(mob_living user = null)
    {
        if (held_ore == null)
            return null;
        Waiting.NoSleep(dropItemToGround(held_ore as obj_item));
        return COMSIG_KB_ACTIVATED;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:187
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        desc = "A deceased primitive. Upon closer inspection, it was suffering from severe cellular degeneration and its garments are machine made..."; //Can you guess the twist
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:191
    public virtual void attack_intruder(mob_living intruder = null)
    {
        if (intruder is mob_living_basic_mining_mook)
            return;
        foreach (mob_living_basic_mining_mook villager in Dm.each<mob_living_basic_mining_mook>(oview(this, 9)))
            (villager.ai_controller as datum_ai_controller)?.set_blackboard_key_assoc_lazylist(BB_BASIC_MOB_RETALIATE_LIST, intruder, world.time);
    }
}

// tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:196
public partial class mob_living_basic_mining_mook_worker : mob_living_basic_mining_mook
{
    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:206
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        neutral_stance = mutable_appearance(icon, "mook_axe_overlay") as mutable_appearance;
        attack_stance = mutable_appearance(icon, "axe_strike_overlay") as mutable_appearance;
        update_appearance();
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:212
    public override DmList get_innate_abilities()
    {
        DmList worker_innate_abilites = TgState.Current.mob_living_basic_mining_mook_worker__get_innate_abilities__worker_innate_abilites;

        if (worker_innate_abilites == null)
        {
            worker_innate_abilites = Dm.list();
            TgState.Current.mob_living_basic_mining_mook_worker__get_innate_abilities__worker_innate_abilites = worker_innate_abilites;
            Dm.append(ref worker_innate_abilites, base.get_innate_abilities());
            Dm.append(ref worker_innate_abilites, Dm.list(
                Dm.kv(typeof(datum_action_cooldown_mob_cooldown_mook_ability_mook_leap), BB_MOOK_LEAP_ABILITY)));
        }

        return worker_innate_abilites;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:224
    public override async DmTask<object> attack_sequence(atom target = null)
    {
        object dot = await base.attack_sequence(target);
        if (Dm.truthy(Dm.bitand(dot, COMPONENT_HOSTILE_NO_ATTACK)))
            return dot;

        if (attack_state == MOOK_ATTACK_STRIKE)
            return COMPONENT_HOSTILE_NO_ATTACK;

        change_combatant_state(state: MOOK_ATTACK_STRIKE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(change_combatant_state), MOOK_ATTACK_NEUTRAL), 0.3f * SECONDS, file: "code/modules/mob/living/basic/lavaland/mook/mook.dm", line: 232);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:235
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (stat == DEAD)
            return dot;

        switch (attack_state)
        {
            case MOOK_ATTACK_STRIKE:
                Dm.append(ref dot, attack_stance);
                break;
            case MOOK_ATTACK_NEUTRAL:
                Dm.append(ref dot, neutral_stance);
                break;
        }
        return dot;
    }
}

// tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:246
public partial class mob_living_basic_mining_mook_worker_bard : mob_living_basic_mining_mook_worker
{
    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:257
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        neutral_stance = mutable_appearance(icon, "bard_overlay") as mutable_appearance;
        attack_stance = mutable_appearance(icon, "bard_strike") as mutable_appearance;
        held_guitar = Dm.@new<obj_item_instrument_guitar>(this);
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_SONG_INSTRUMENT, held_guitar);
        update_appearance();
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:265
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref held_guitar);
        return base.Destroy(force);
    }
}

// tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:269
public partial class mob_living_basic_mining_mook_worker_tribal_chief : mob_living_basic_mining_mook_worker
{
    ///overlay in our neutral state
    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:277 (var/static/mutable_appearance/chief_neutral)
    public static mutable_appearance chief_neutral => TgState.Current.mob_living_basic_mining_mook_worker_tribal_chief__chief_neutral ??= mutable_appearance(Dm.resource("icons/mob/simple/jungle/mook.dmi"), "mook_chief") as mutable_appearance;
    ///overlay in our striking state
    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:279 (var/static/mutable_appearance/chief_strike)
    public static mutable_appearance chief_strike => TgState.Current.mob_living_basic_mining_mook_worker_tribal_chief__chief_strike ??= mutable_appearance(Dm.resource("icons/mob/simple/jungle/mook.dmi"), "mook_chief_strike") as mutable_appearance;
    ///overlay in our active state
    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:281 (var/static/mutable_appearance/chief_active)
    public static mutable_appearance chief_active => TgState.Current.mob_living_basic_mining_mook_worker_tribal_chief__chief_active ??= mutable_appearance(Dm.resource("icons/mob/simple/jungle/mook.dmi"), "mook_chief_leap") as mutable_appearance;
    ///overlay in our warmup state
    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:283 (var/static/mutable_appearance/chief_warmup)
    public static mutable_appearance chief_warmup => TgState.Current.mob_living_basic_mining_mook_worker_tribal_chief__chief_warmup ??= mutable_appearance(Dm.resource("icons/mob/simple/jungle/mook.dmi"), "mook_chief_warmup") as mutable_appearance;
    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:284 (var/static/list/bonfire_targets)
    public static DmList bonfire_targets => TgState.Current.mob_living_basic_mining_mook_worker_tribal_chief__bonfire_targets ??= Dm.list(typeof(obj_structure_bonfire));

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:286
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BONFIRE_TARGETS, typecacheof(bonfire_targets));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:291
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (stat == DEAD)
            return dot;
        switch (attack_state)
        {
            case MOOK_ATTACK_NEUTRAL:
                Dm.append(ref dot, chief_neutral);
                break;
            case MOOK_ATTACK_WARMUP:
                Dm.append(ref dot, chief_warmup);
                break;
            case MOOK_ATTACK_ACTIVE:
                Dm.append(ref dot, chief_active);
                break;
            case MOOK_ATTACK_STRIKE:
                Dm.append(ref dot, chief_strike);
                break;
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:48 (var/static/list/heal_targets)
    internal DmList mob_living_basic_mining_mook__heal_targets;
    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:73 (var/static/list/innate_abilities)
    internal DmList mob_living_basic_mining_mook__get_innate_abilities__innate_abilities;
    // tg: code/modules/mob/living/basic/lavaland/mook/mook.dm:211 (var/static/list/worker_innate_abilites)
    internal DmList mob_living_basic_mining_mook_worker__get_innate_abilities__worker_innate_abilites;
    internal mutable_appearance mob_living_basic_mining_mook_worker_tribal_chief__chief_neutral;
    internal mutable_appearance mob_living_basic_mining_mook_worker_tribal_chief__chief_strike;
    internal mutable_appearance mob_living_basic_mining_mook_worker_tribal_chief__chief_active;
    internal mutable_appearance mob_living_basic_mining_mook_worker_tribal_chief__chief_warmup;
    internal DmList mob_living_basic_mining_mook_worker_tribal_chief__bonfire_targets;
}
