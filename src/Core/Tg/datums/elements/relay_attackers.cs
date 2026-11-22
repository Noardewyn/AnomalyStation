// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/relay_attackers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/**
 * This element registers to a shitload of signals which can signify "someone attacked me".
 * If anyone does it sends a single "someone attacked me" signal containing details about who done it.
 * This prevents other components and elements from having to register to the same list of a million signals, should be more maintainable in one place.
 */
// tg: code/datums/elements/relay_attackers.dm:6
public partial class datum_element_relay_attackers : datum_element
{
    // tg: code/datums/elements/relay_attackers.dm:8
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!HAS_TRAIT(target, TRAIT_RELAYING_ATTACKER)) // Little bit gross but we want to just apply this shit from a bunch of places
        {
            // Boy this sure is a lot of ways to tell us that someone tried to attack us
            RegisterSignal(target, COMSIG_ATOM_AFTER_ATTACKEDBY, nameof(after_attackby));
            RegisterSignals(target, Dm.list(COMSIG_ATOM_ATTACK_HAND, COMSIG_ATOM_ATTACK_PAW, COMSIG_MOB_ATTACK_ALIEN), nameof(on_attack_generic));
            RegisterSignals(target, Dm.list(COMSIG_ATOM_ATTACK_BASIC_MOB, COMSIG_ATOM_ATTACK_ANIMAL), nameof(on_attack_npc));
            RegisterSignal(target, COMSIG_PROJECTILE_PREHIT, nameof(on_bullet_act));
            RegisterSignal(target, COMSIG_ATOM_PREHITBY, nameof(on_hitby));
            RegisterSignal(target, COMSIG_ATOM_HULK_ATTACK, nameof(on_attack_hulk));
            RegisterSignal(target, COMSIG_ATOM_ATTACK_MECH, nameof(on_attack_mech));
        }
        ADD_TRAIT(target, TRAIT_RELAYING_ATTACKER, REF(this));
        return dot;
    }

    // tg: code/datums/elements/relay_attackers.dm:22
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(
            COMSIG_ATOM_AFTER_ATTACKEDBY,
            COMSIG_ATOM_ATTACK_HAND,
            COMSIG_ATOM_ATTACK_PAW,
            COMSIG_ATOM_ATTACK_BASIC_MOB,
            COMSIG_ATOM_ATTACK_ANIMAL,
            COMSIG_MOB_ATTACK_ALIEN,
            COMSIG_PROJECTILE_PREHIT,
            COMSIG_ATOM_PREHITBY,
            COMSIG_ATOM_HULK_ATTACK,
            COMSIG_ATOM_ATTACK_MECH
        ));
        REMOVE_TRAIT(source, TRAIT_RELAYING_ATTACKER, REF(this));
        return dot;
    }

    // tg: code/datums/elements/relay_attackers.dm:38
    [SignalHandler]
    public virtual void after_attackby(atom target = null, obj_item weapon = null, mob attacker = null, DmList modifiers = null)
    {
        if (Dm.truthy(weapon.force))
            relay_attacker(target, attacker, Dm.eq(weapon.damtype, STAMINA) ? ATTACKER_STAMINA_ATTACK : ATTACKER_DAMAGING_ATTACK, get_dir(target, attacker));
    }

    // tg: code/datums/elements/relay_attackers.dm:43
    [SignalHandler]
    public virtual void on_attack_generic(atom target = null, mob_living attacker = null, DmList modifiers = null)
    {
        // Check for a shove.
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
        {
            relay_attacker(target, attacker, ATTACKER_SHOVING, get_dir(target, attacker));
            return;
        }

        // Else check for combat mode.
        if (Dm.truthy(attacker.combat_mode))
            relay_attacker(target, attacker, ATTACKER_DAMAGING_ATTACK, get_dir(target, attacker));
    }

    // tg: code/datums/elements/relay_attackers.dm:55
    [SignalHandler]
    public virtual void on_attack_npc(atom target = null, mob_living attacker = null)
    {
        if (Dm.num(attacker.melee_damage_upper) > 0)
            relay_attacker(target, attacker, ATTACKER_DAMAGING_ATTACK, get_dir(target, attacker));
    }

    /// Even if another component blocked this hit, someone still shot at us
    // tg: code/datums/elements/relay_attackers.dm:61
    [SignalHandler]
    public virtual void on_bullet_act(atom target = null, obj_projectile hit_projectile = null)
    {
        if (!Dm.truthy(hit_projectile.is_hostile_projectile()))
            return;
        if (!ismob(hit_projectile.firer))
            return;
        // tg's own precedence: ATTACK_RANGED | (damage_type == STAMINA) is the condition of the ternary
        relay_attacker(target, hit_projectile.firer as atom, Dm.truthy(Dm.bitor(ATTACK_RANGED, Dm.num(Dm.eq(hit_projectile.damage_type, STAMINA)))) ? ATTACKER_STAMINA_ATTACK : ATTACKER_DAMAGING_ATTACK, get_dir(target, hit_projectile));
    }

    /// Even if another component blocked this hit, someone still threw something
    // tg: code/datums/elements/relay_attackers.dm:70
    [SignalHandler]
    public virtual void on_hitby(atom target = null, atom_movable hit_atom = null, datum_thrownthing throwingdatum = null)
    {
        if (hit_atom is not obj_item hit_item)
            return;
        if (!Dm.truthy(hit_item.throwforce))
            return;
        if (throwingdatum?.get_thrower() is not atom thrown_by)
            return;
        // tg's own precedence, as above
        relay_attacker(target, thrown_by, Dm.truthy(Dm.bitor(ATTACK_RANGED, Dm.num(Dm.eq(hit_item.damtype, STAMINA)))) ? ATTACKER_STAMINA_ATTACK : ATTACKER_DAMAGING_ATTACK, get_dir(target, hit_atom));
    }

    // tg: code/datums/elements/relay_attackers.dm:81
    [SignalHandler]
    public virtual void on_attack_hulk(atom target = null, mob attacker = null)
    {
        relay_attacker(target, attacker, ATTACKER_DAMAGING_ATTACK, get_dir(target, attacker));
    }

    // tg: code/datums/elements/relay_attackers.dm:85
    [SignalHandler]
    public virtual void on_attack_mech(atom target = null, obj_vehicle_sealed_mecha mecha_attacker = null, mob_living pilot = null, object mecha_attack_cooldown = null)
    {
        relay_attacker(target, mecha_attacker, ATTACKER_DAMAGING_ATTACK, get_dir(target, mecha_attacker));
    }

    /// Send out a signal identifying whoever just attacked us (usually a mob but sometimes a mech or turret)
    // tg: code/datums/elements/relay_attackers.dm:90
    public virtual void relay_attacker(atom victim = null, atom attacker = null, object attack_flags = null, object direction = null)
    {
        if (Dm.truthy(victim._listen_lookup?[COMSIG_ATOM_WAS_ATTACKED]))
            victim._SendSignal(COMSIG_ATOM_WAS_ATTACKED, Dm.list(victim, attacker, attack_flags, direction));
    }
}
