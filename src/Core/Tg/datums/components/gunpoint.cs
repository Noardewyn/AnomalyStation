// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/gunpoint.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice D: the gunpoint component (a right click with a gun on a man holds him up), whole but for the witnessed memory (memories, M10)
// and the rocket holdup achievement (achievements, M10+). The var block is generated (D111).

/// How long it takes from the gunpoint is initiated to reach stage 2
// tg: code/datums/components/gunpoint.dm:2  #define GUNPOINT_DELAY_STAGE_2 (2.5 SECONDS) (file-local)
/// How long it takes from stage 2 starting to move up to stage 3
// tg: code/datums/components/gunpoint.dm:4  #define GUNPOINT_DELAY_STAGE_3 (7.5 SECONDS) (file-local)
/// If the projectile doesn't have a wound_bonus of CANT_WOUND, we add (this * the stage mult) to their wound_bonus and exposed_wound_bonus upon triggering
// tg: code/datums/components/gunpoint.dm:6  #define GUNPOINT_BASE_WOUND_BONUS 5 (file-local)
/// How much the damage and wound bonus mod is multiplied when you're on stage 1 / 2 / 3
// tg: code/datums/components/gunpoint.dm:8-12  #define GUNPOINT_MULT_STAGE_1 1.25, GUNPOINT_MULT_STAGE_2 2, GUNPOINT_MULT_STAGE_3 2.5 (file-local)

// tg: code/datums/components/gunpoint.dm:15
public partial class datum_component_gunpoint : datum_component
{
    private const float GUNPOINT_DELAY_STAGE_2 = 2.5f * SECONDS;
    private const float GUNPOINT_DELAY_STAGE_3 = 7.5f * SECONDS;
    private const float GUNPOINT_BASE_WOUND_BONUS = 5;
    private const float GUNPOINT_MULT_STAGE_1 = 1.25f;
    private const float GUNPOINT_MULT_STAGE_2 = 2;
    private const float GUNPOINT_MULT_STAGE_3 = 2.5f;

    // *extremely bad russian accent* no!
    // tg: code/datums/components/gunpoint.dm:33
    [DmParams("targ", "wep")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        mob_living targ = Dm.arg<mob_living>(__extra, 0);
        obj_item_gun wep = Dm.arg<obj_item_gun>(__extra, 1);
        object dot = await base.Initialize(Dm.forward(__extra, targ, wep));
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        mob_living shooter = parent as mob_living;
        target = targ;
        weapon = wep;

        RegisterSignals(targ, Dm.list(
            COMSIG_MOB_ATTACK_HAND,
            COMSIG_MOB_ITEM_ATTACK,
            COMSIG_MOVABLE_MOVED,
            COMSIG_MOB_FIRED_GUN,
            COMSIG_MOVABLE_SET_GRAB_STATE,
            COMSIG_LIVING_START_PULL), nameof(trigger_reaction));
        RegisterSignal(targ, COMSIG_ATOM_EXAMINE, nameof(examine_target));
        RegisterSignal(targ, COMSIG_LIVING_PRE_MOB_BUMP, nameof(block_bumps_target));
        RegisterSignals(targ, Dm.list(COMSIG_LIVING_DISARM_HIT, COMSIG_LIVING_GET_PULLED), nameof(cancel));
        RegisterSignals(weapon, Dm.list(COMSIG_ITEM_DROPPED, COMSIG_ITEM_EQUIPPED), nameof(cancel));

        float distance = max(Dm.num(get_dist(shooter, target)), 1); // treat 0 distance as adjacent
        string distance_description = (distance <= 1 ? "point blank " : "");

        shooter.visible_message(span_danger(Dm.text("[] aims [] []at []!", shooter, weapon, distance_description, target)),
            span_danger(Dm.text("You aim [] []at []!", weapon, distance_description, target)), ignored_mobs: target);
        to_chat(target, span_userdanger(Dm.text("[] aims [] []at you!", shooter, weapon, distance_description)));

        await shooter.Immobilize(0.75f * SECONDS / distance);
        if (!HAS_TRAIT(target, TRAIT_NOFEAR_HOLDUPS))
        {
            await target.Immobilize(0.75f * SECONDS / distance);
            await target.emote("gaspshock", intentional: FALSE);
            add_memory_in_range(target, 7, typeof(datum_memory_held_at_gunpoint), Dm.named("protagonist", target), Dm.named("deuteragonist", shooter), Dm.named("antagonist", weapon));
        }

        await shooter.apply_status_effect(typeof(datum_status_effect_holdup), shooter);
        await target.apply_status_effect(typeof(datum_status_effect_grouped_heldup), REF(shooter));
        target.do_alert_animation();
        target.playsound_local(target.loc, Dm.resource("sound/machines/chime.ogg"), 50, TRUE);
        await target.add_mood_event("gunpoint", typeof(datum_mood_event_gunpoint));

        if (istype(weapon, typeof(obj_item_gun_ballistic_rocketlauncher)) && weapon.chambered != null)
        {
            if (!IS_UNCONSCIOUS_OR_CRIT(target) && shooter.client != null)
                Dm.Deferred("code/datums/components/gunpoint.dm:79", "Initialize: the nuclear operative rocket holdup achievement (nuke ops and achievements, M10+)");
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(update_stage), 2), GUNPOINT_DELAY_STAGE_2, file: "code/datums/components/gunpoint.dm", line: 82);
        return dot;
    }

    // tg: code/datums/components/gunpoint.dm:84
    public override object Destroy(float? force = null)
    {
        mob_living shooter = parent as mob_living;
        shooter.remove_status_effect(typeof(datum_status_effect_holdup));
        target.remove_status_effect(typeof(datum_status_effect_grouped_heldup), REF(shooter));
        target.clear_mood_event("gunpoint");
        return base.Destroy(force);
    }

    // tg: code/datums/components/gunpoint.dm:91
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(check_deescalate));
        RegisterSignal(parent, COMSIG_MOB_APPLY_DAMAGE, nameof(flinch));
        RegisterSignal(parent, COMSIG_MOB_ATTACK_HAND, nameof(check_shove));
        RegisterSignal(parent, COMSIG_MOB_UPDATE_SIGHT, nameof(check_deescalate));
        RegisterSignals(parent, Dm.list(COMSIG_LIVING_START_PULL, COMSIG_MOVABLE_BUMP), nameof(check_bump));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(examine));
        RegisterSignal(parent, COMSIG_LIVING_PRE_MOB_BUMP, nameof(block_bumps_parent));
        RegisterSignal(parent, COMSIG_LIVING_DISARM_HIT, nameof(cancel));
    }

    // tg: code/datums/components/gunpoint.dm:101
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_MOVABLE_MOVED);
        UnregisterSignal(parent, COMSIG_MOB_APPLY_DAMAGE);
        UnregisterSignal(parent, COMSIG_MOB_UPDATE_SIGHT);
        UnregisterSignal(parent, COMSIG_MOB_ATTACK_HAND);
        UnregisterSignal(parent, Dm.list(COMSIG_LIVING_START_PULL, COMSIG_MOVABLE_BUMP));
        UnregisterSignal(parent, COMSIG_ATOM_EXAMINE);
        UnregisterSignal(parent, COMSIG_LIVING_PRE_MOB_BUMP);
        UnregisterSignal(parent, COMSIG_LIVING_DISARM_HIT);
    }

    ///If the shooter bumps the target, cancel the holdup to avoid cheesing and forcing the charged shot
    // tg: code/datums/components/gunpoint.dm:112
    [SignalHandler]
    public virtual void check_bump(atom B = null, atom A = null)
    {
        if (A != target)
            return;
        mob_living shooter = parent as mob_living;
        shooter.visible_message(span_danger(Dm.text("[] bumps into [] and fumbles [] aim!", shooter, target, shooter.p_their())),
            span_danger(Dm.text("You bump into [] and fumble your aim!", target)), ignored_mobs: target);
        to_chat(target, span_userdanger(Dm.text("[] bumps into you and fumbles [] aim!", shooter, shooter.p_their())));
        qdel(this);
    }

    ///If the shooter shoves or grabs the target, cancel the holdup to avoid cheesing and forcing the charged shot
    // tg: code/datums/components/gunpoint.dm:123
    [SignalHandler]
    public virtual void check_shove(mob_living_carbon shooter = null, mob shooter_again = null, mob_living T = null, object attacker_style = null, object modifiers = null)
    {
        if (T != target || Dm.truthy(LAZYACCESS(modifiers as DmList, RIGHT_CLICK)))
            return;
        shooter.visible_message(span_danger(Dm.text("[] bumps into [] and fumbles [] aim!", shooter, target, shooter.p_their())),
            span_danger(Dm.text("You bump into [] and fumble your aim!", target)), ignored_mobs: target);
        to_chat(target, span_userdanger(Dm.text("[] bumps into you and fumbles [] aim!", shooter, shooter.p_their())));
        qdel(this);
    }

    ///Update the damage multiplier for whatever stage we're entering into
    // tg: code/datums/components/gunpoint.dm:133
    public virtual void update_stage(object new_stage = null)
    {
        if (Dm.truthy(check_deescalate()))
            return;
        stage = Dm.num(new_stage);
        if (stage == 2)
        {
            to_chat(parent, span_danger(Dm.text("You steady [] on [].", weapon, target)));
            to_chat(target, span_userdanger(Dm.text("[] has steadied [] on you!", parent, weapon)));
            damage_mult = GUNPOINT_MULT_STAGE_2;
            _addtimer(Dm.@new<datum_callback>(this, nameof(update_stage), 3), GUNPOINT_DELAY_STAGE_3, file: "code/datums/components/gunpoint.dm", line: 141);
        }
        else if (stage == 3)
        {
            to_chat(parent, span_danger(Dm.text("You have fully steadied [] on [].", weapon, target)));
            to_chat(target, span_userdanger(Dm.text("[] has fully steadied [] on you!", parent, weapon)));
            damage_mult = GUNPOINT_MULT_STAGE_3;
        }
    }

    ///Cancel the holdup if the shooter moves out of sight or out of range of the target
    // tg: code/datums/components/gunpoint.dm:149
    [SignalHandler]
    public virtual object check_deescalate(params object[] __extra)
    {
        if (!Dm.truthy(can_see(parent as atom, target, GUNPOINT_SHOOTER_STRAY_RANGE)))
        {
            cancel();
            return TRUE;
        }
        return null;
    }

    ///Bang bang, we're firing a charged shot off
    // tg: code/datums/components/gunpoint.dm:157
    [SignalHandler]
    public virtual void trigger_reaction(params object[] __extra)
    {
        Waiting.Detach(async_trigger_reaction());
    }

    // tg: code/datums/components/gunpoint.dm:161
    public virtual async DmTask async_trigger_reaction()
    {
        mob_living shooter = parent as mob_living;
        shooter.remove_status_effect(typeof(datum_status_effect_holdup)); // try doing these before the trigger gets pulled since the target (or shooter even) may not exist after pulling the trigger, dig?
        target.remove_status_effect(typeof(datum_status_effect_grouped_heldup), REF(shooter));
        target.clear_mood_event("gunpoint");

        if (Dm.truthy(point_of_no_return))
            return;
        point_of_no_return = TRUE;

        if (weapon.chambered != null && weapon.chambered.loaded_projectile != null)
        {
            weapon.chambered.loaded_projectile.damage *= damage_mult;
            if (weapon.chambered.loaded_projectile.wound_bonus != CANT_WOUND)
            {
                weapon.chambered.loaded_projectile.wound_bonus += damage_mult * GUNPOINT_BASE_WOUND_BONUS;
                weapon.chambered.loaded_projectile.exposed_wound_bonus += damage_mult * GUNPOINT_BASE_WOUND_BONUS;
            }
        }

        object fired = await weapon.fire_gun(target, shooter);
        if (!Dm.truthy(fired) && weapon.chambered?.loaded_projectile != null)
        {
            weapon.chambered.loaded_projectile.damage /= damage_mult;
            if (weapon.chambered.loaded_projectile.wound_bonus != CANT_WOUND)
            {
                weapon.chambered.loaded_projectile.wound_bonus -= damage_mult * GUNPOINT_BASE_WOUND_BONUS;
                weapon.chambered.loaded_projectile.exposed_wound_bonus -= damage_mult * GUNPOINT_BASE_WOUND_BONUS;
            }
        }

        qdel(this);
    }

    ///Shooter canceled their shot, either by dropping/equipping their weapon, leaving sight/range, or clicking on the alert
    // tg: code/datums/components/gunpoint.dm:190
    [SignalHandler]
    public virtual void cancel(params object[] __extra)
    {
        mob_living shooter = parent as mob_living;
        shooter.visible_message(span_danger(Dm.text("[] breaks [] aim on []!", shooter, shooter.p_their(), target)),
            span_danger(Dm.text("You are no longer aiming [] at []." , weapon, target)), ignored_mobs: target);
        to_chat(target, span_userdanger(Dm.text("[] breaks [] aim on you!", shooter, shooter.p_their())));
        qdel(this);
    }

    ///If the shooter is hit by an attack, they have a 50% chance to flinch and fire. If it hit the arm holding the trigger, it's an 80% chance to fire instead
    // tg: code/datums/components/gunpoint.dm:199
    [SignalHandler]
    public virtual void flinch(mob_living source = null, object damage_amount = null, object damagetype = null, object def_zone = null, object blocked = null, object wound_bonus = null, object exposed_wound_bonus = null, object sharpness = null, object attack_direction = null, object attacking_item = null)
    {
        if (!Dm.truthy(attack_direction)) // No fliching from yourself
            return;

        float flinch_chance = 50;
        string gun_hand = IS_LEFT_INDEX(Dm.num(source.get_held_index_of_item(weapon))) ? BODY_ZONE_L_ARM : BODY_ZONE_R_ARM;

        if (isbodypart(def_zone))
        {
            obj_item_bodypart hitting = def_zone as obj_item_bodypart;
            def_zone = hitting.body_zone;
        }

        if (Dm.eq(def_zone, gun_hand))
            flinch_chance = 80;

        if (prob(flinch_chance))
        {
            source.visible_message(
                span_danger(Dm.text("[] flinches!", source)),
                span_danger("You flinch!")
            );
            Waiting.Detach(async_trigger_reaction());
        }
    }

    ///Shows if the parent is holding someone at gunpoint
    // tg: code/datums/components/gunpoint.dm:220
    [SignalHandler]
    public virtual void examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        if (Dm.truthy(Dm.@in(user, viewers(null, target))))
            Dm.append(ref examine_list, span_boldwarning(Dm.text("[] [] holding [] at gunpoint with []!", parent, ((atom)parent).p_are(), target, weapon)));
    }

    ///Shows if the examine target is being held at gunpoint
    // tg: code/datums/components/gunpoint.dm:225
    [SignalHandler]
    public virtual void examine_target(datum source = null, mob user = null, DmList examine_list = null)
    {
        if (Dm.truthy(Dm.@in(user, viewers(null, parent))))
            Dm.append(ref examine_list, span_boldwarning(Dm.text("[] [] being held at gunpoint by []!", target, target.p_are(), parent)));
    }

    ///Prevents bumping the shooter to break gunpoint since shove does that
    // tg: code/datums/components/gunpoint.dm:230
    [SignalHandler]
    public virtual object block_bumps_parent(mob bumped = null, mob_living bumper = null)
    {
        to_chat(bumper, span_warning(Dm.text("[] [] holding [] at gunpoint, you cannot push past.", bumped, bumped.p_are(), target)));
        return COMPONENT_LIVING_BLOCK_PRE_MOB_BUMP;
    }

    ///Prevents bumping the target by an ally to cheese and force the charged shot
    // tg: code/datums/components/gunpoint.dm:235
    [SignalHandler]
    public virtual object block_bumps_target(mob bumped = null, mob_living bumper = null)
    {
        to_chat(bumper, span_warning(Dm.text("[] [] being held at gunpoint, it's not wise to push []!", bumped, bumped.p_are(), bumped.p_them())));
        return COMPONENT_LIVING_BLOCK_PRE_MOB_BUMP;
    }
}
