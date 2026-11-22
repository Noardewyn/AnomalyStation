// Translated from tgstation (snapshot 1d99bd7f): code/game/atom/atom_defense.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M5 slice 1: atom integrity, armour and damage). The vars armor_type and armor (:5-6) are generated
// (Generated/Types/atom.g.cs, D111); the others are declared here with the slot signatures of slots.tsv (M2 F1, wave 3).
// The DV27 guard (D132) covers the damage procs: damage reaching a type whose own tg take_damage, run_atom_armor,
// play_attack_sound, atom_break, atom_destruction or the obj deconstruct procs are not translated does nothing
// (InertSeam.MissingDamageOverride), never its parent's code.

// tg: code/game/atom/atom_defense.dm:1
public partial class atom : datum
{
    ///any atom that uses integrity and can be damaged must set this to true, otherwise the integrity procs will throw an error
    // tg: code/game/atom/atom_defense.dm:3
    public float uses_integrity = FALSE;

    // tg: code/game/atom/atom_defense.dm:8  VAR_PRIVATE/atom_integrity //defaults to max_integrity
    [DmPrivate]
    public object atom_integrity;
    // tg: code/game/atom/atom_defense.dm:9
    public float max_integrity = 500;
    // tg: code/game/atom/atom_defense.dm:10
    public float integrity_failure = 0; //0 if we have no special broken behavior, otherwise is a percentage of at what point the atom breaks. 0.5 being 50%
    ///Damage under this value will be completely ignored
    // tg: code/game/atom/atom_defense.dm:12
    public float damage_deflection = 0;

    // tg: code/game/atom/atom_defense.dm:14
    public float resistance_flags = NONE; // INDESTRUCTIBLE | LAVA_PROOF | FIRE_PROOF | ON_FIRE | UNACIDABLE | ACID_PROOF

    /// The essential proc to call when an atom must receive damage of any kind.
    // tg: code/game/atom/atom_defense.dm:17
    public virtual async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_type ??= BRUTE;
        damage_flag ??= "";
        sound_effect ??= TRUE;
        armour_penetration ??= 0;
        // DEVIATION(DV27): damage reaching a type whose own damage procs are not translated does nothing (D132).
        if (InertSeam.MissingDamageOverride(this))
            return null;
        object dot = null;
        if (!Dm.truthy(uses_integrity))
            throw Dm.CRASH(Dm.text("[] had /atom/proc/take_damage() called on it without it being a type that has uses_integrity = TRUE!", this), "code/game/atom/atom_defense.dm", 19);
        if (QDELETED(this))
            throw Dm.CRASH(Dm.text("[] taking damage after deletion", this), "code/game/atom/atom_defense.dm", 21);
        if (Dm.num(atom_integrity) <= 0)
            throw Dm.CRASH(Dm.text("[] taking damage while having <= 0 integrity", this), "code/game/atom/atom_defense.dm", 23);
        if (Dm.truthy(sound_effect))
            play_attack_sound(damage_amount, damage_type, damage_flag);
        if (Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
            return dot;
        damage_amount = await run_atom_armor(damage_amount, damage_type, damage_flag, attack_dir, armour_penetration);
        if (Dm.num(damage_amount) < DAMAGE_PRECISION)
            return dot;
        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_TAKE_DAMAGE]) && Dm.truthy(Dm.bitand(_SendSignal(COMSIG_ATOM_TAKE_DAMAGE, Dm.list(this, damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration)), COMPONENT_NO_TAKE_DAMAGE)))
            return dot;

        dot = damage_amount;

        float previous_atom_integrity = Dm.num(atom_integrity);

        update_integrity(previous_atom_integrity - Dm.num(damage_amount));

        float integrity_failure_amount = integrity_failure * max_integrity;

        //BREAKING FIRST
        if (Dm.truthy(integrity_failure) && previous_atom_integrity > integrity_failure_amount && Dm.num(atom_integrity) <= integrity_failure_amount)
            await atom_break(damage_flag);

        //DESTROYING SECOND
        if (Dm.num(atom_integrity) <= 0 && previous_atom_integrity > 0)
            await atom_destruction(damage_flag);
        return dot;
    }

    /// Proc for recovering atom_integrity. Returns the amount repaired by
    // tg: code/game/atom/atom_defense.dm:51
    public virtual async DmTask<object> repair_damage(object amount = null)
    {
        // DEVIATION(DV27): a repair reaching a type whose own repair_damage or atom_fix is not translated does nothing (D132).
        if (InertSeam.MissingRepairOverride(this))
            return null;
        object dot = null;
        if (Dm.num(amount) <= 0) // We only recover here
            return dot;
        float new_integrity = Dm.min(max_integrity, Dm.num(atom_integrity) + Dm.num(amount));
        dot = new_integrity - Dm.num(atom_integrity);

        update_integrity(new_integrity);

        if (Dm.truthy(integrity_failure) && Dm.num(atom_integrity) > integrity_failure * max_integrity)
            await atom_fix();
        return dot;
    }

    /// Handles the integrity of an atom changing. This must be called instead of changing integrity directly.
    // tg: code/game/atom/atom_defense.dm:63
    public virtual object update_integrity(object new_value = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE)
        if (!Dm.truthy(uses_integrity))
            throw Dm.CRASH(Dm.text("/atom/proc/update_integrity() was called on [] when it doesn't use integrity!", this), "code/game/atom/atom_defense.dm", 66);
        object old_value = atom_integrity;
        new_value = Dm.max(0, Dm.num(new_value));
        if (Dm.eq(atom_integrity, new_value))
            return null;
        atom_integrity = new_value;
        on_update_integrity(old_value, new_value);
        return new_value;
    }

    /// Handle updates to your atom's integrity
    // tg: code/game/atom/atom_defense.dm:76
    public virtual void on_update_integrity(object old_value = null, object new_value = null)
    {
        // SHOULD_NOT_SLEEP(TRUE), SHOULD_CALL_PARENT(TRUE)
        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_INTEGRITY_CHANGED]))
            _SendSignal(COMSIG_ATOM_INTEGRITY_CHANGED, Dm.list(this, old_value, new_value));
    }

    /// This mostly exists to keep atom_integrity private. Might be useful in the future.
    // tg: code/game/atom/atom_defense.dm:82
    [ShouldBePure]
    public virtual object get_integrity()
    {
        return atom_integrity;
    }

    /// Similar to get_integrity, but returns the percentage as [0-1] instead.
    // tg: code/game/atom/atom_defense.dm:87
    [ShouldBePure]
    public virtual object get_integrity_percentage()
    {
        return round(Dm.num(atom_integrity) / max_integrity, 0.01f);
    }

    ///returns the damage value of the attack after processing the atom's various armor protections
    // tg: code/game/atom/atom_defense.dm:92  (damage_flag: object, decided in Appendix A: tg passes a text flag, MELEE or FIRE)
    public virtual async DmTask<object> run_atom_armor(object damage_amount = null, object damage_type = null, object damage_flag = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_flag ??= 0;
        armour_penetration ??= 0;
        if (!Dm.truthy(uses_integrity))
            throw Dm.CRASH(Dm.text("/atom/proc/run_atom_armor was called on [] without being implemented as a type that uses integrity!", this), "code/game/atom/atom_defense.dm", 94);
        if (Dm.eq(damage_flag, MELEE) && Dm.num(damage_amount) < damage_deflection)
            return 0;
        if (!Dm.eq(damage_type, BRUTE) && !Dm.eq(damage_type, BURN))
            return 0;
        float armor_protection = 0;
        if (Dm.truthy(damage_flag))
            armor_protection = Dm.num(get_armor_rating(damage_flag));
        if (Dm.truthy(armor_protection)) //Only apply weak-against-armor/hollowpoint effects if there actually IS armor.
            armor_protection = clamp(PENETRATE_ARMOUR(armor_protection, armour_penetration.Value), Dm.min(armor_protection, 0), 100);
        return round(Dm.num(damage_amount) * (100 - armor_protection) * 0.01f, DAMAGE_PRECISION);
    }

    ///the sound played when the atom is damaged.
    // tg: code/game/atom/atom_defense.dm:107  (damage_flag: object, decided in Appendix A, as run_atom_armor's)
    public virtual object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        object sw__1 = damage_type;
        if (Dm.eq(sw__1, BRUTE))
        {
            if (Dm.truthy(damage_amount))
                playsound(this, Dm.resource("sound/items/weapons/smash.ogg"), 50, TRUE);
            else
                playsound(this, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
        }
        else if (Dm.eq(sw__1, BURN))
            playsound(loc, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        return null;
    }

    ///Called to get the damage that hulks will deal to the atom.
    // tg: code/game/atom/atom_defense.dm:118
    public virtual object hulk_damage()
    {
        return 150; //the damage hulks do on punches to this atom, is affected by melee armor
    }

    // tg: code/game/atom/atom_defense.dm:121  //used by attack_alien, attack_animal (damage_flag: object, decided in Appendix A)
    public virtual async DmTask<object> attack_generic(mob user = null, float? damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, float? armor_penetration = null)
    {
        damage_amount ??= 0;
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        sound_effect ??= 1;
        armor_penetration ??= 0;
        if (!Dm.truthy(uses_integrity))
            throw Dm.CRASH("unimplemented /atom/proc/attack_generic()!", "code/game/atom/atom_defense.dm", 123);
        await user.do_attack_animation(this);
        user.changeNext_move(CLICK_CD_MELEE);
        return await take_damage(damage_amount, damage_type, damage_flag, sound_effect, get_dir(this, user), armor_penetration);
    }

    /// Called after the atom takes damage and integrity is below integrity_failure level
    // tg: code/game/atom/atom_defense.dm:129
    public virtual async DmTask<object> atom_break(object damage_flag = null)
    {
        // SHOULD_CALL_PARENT(TRUE)
        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_BREAK]))
            _SendSignal(COMSIG_ATOM_BREAK, Dm.list(this, damage_flag));
        return null;
    }

    /// Called when integrity is repaired above the breaking point having been broken before
    // tg: code/game/atom/atom_defense.dm:134
    public virtual async DmTask atom_fix()
    {
        // SHOULD_CALL_PARENT(TRUE)
        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_FIX]))
            _SendSignal(COMSIG_ATOM_FIX, Dm.list(this));
    }

    ///what happens when the atom's integrity reaches zero.
    // tg: code/game/atom/atom_defense.dm:139
    public virtual async DmTask atom_destruction(object damage_flag = null)
    {
        // SHOULD_CALL_PARENT(TRUE)
        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_DESTRUCTION]))
            _SendSignal(COMSIG_ATOM_DESTRUCTION, Dm.list(this, damage_flag));
    }

    ///changes max_integrity while retaining current health percentage, returns TRUE if the atom got broken.
    // tg: code/game/atom/atom_defense.dm:144
    public virtual async DmTask<object> modify_max_integrity(object new_max = null, float? can_break = null, string damage_type = null)
    {
        can_break ??= TRUE;
        damage_type ??= BRUTE;
        if (!Dm.truthy(uses_integrity))
            throw Dm.CRASH(Dm.text("/atom/proc/modify_max_integrity() was called on [] when it doesn't use integrity!", this), "code/game/atom/atom_defense.dm", 146);
        float current_integrity = Dm.num(atom_integrity);
        float current_max = max_integrity;

        if (current_integrity != 0 && current_max != 0)
        {
            float percentage = current_integrity / current_max;
            current_integrity = Dm.max(1, round(percentage * Dm.num(new_max))); //don't destroy it as a result
            atom_integrity = current_integrity;
        }

        max_integrity = Dm.num(new_max);

        if (Dm.truthy(can_break) && Dm.truthy(integrity_failure) && current_integrity <= integrity_failure * max_integrity)
        {
            // DEVIATION(DV27): a type whose own atom_break is not translated is not broken (D132).
            if (InertSeam.MissingOverride(this, "atom_break"))
                return FALSE;
            await atom_break(damage_type);
            return TRUE;
        }
        return FALSE;
    }

    /// A cut-out proc for [/atom/proc/bullet_act] so living mobs can have their own armor behavior checks without causing issues with needing their own on_hit call
    // tg: code/game/atom/atom_defense.dm:163
    public virtual object check_projectile_armor(object def_zone = null, obj_projectile impacting_projectile = null, object is_silent = null)
    {
        if (Dm.truthy(uses_integrity))
            return clamp(PENETRATE_ARMOUR(Dm.num(get_armor_rating(impacting_projectile.armor_flag)), impacting_projectile.armour_penetration), 0, 100);
        return 0;
    }
}

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/combat.dm:362  #define PENETRATE_ARMOUR(armour, penetration) (penetration >= 100 ? 0 : 100 * (armour - penetration) / (100 - penetration))
    // (M5 slice 1: a member, rules 11.1)
    public static float PENETRATE_ARMOUR(float armour, float penetration)
        => penetration >= 100 ? 0 : 100 * (armour - penetration) / (100 - penetration);
}
