// Translated from tgstation (snapshot 1d99bd7f): code/datums/materials/material_slots/generic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DE1 (D333): the whole file, with the cleric mace that uses them (weaponry/melee/misc.dm): the weapon head (strength and
// conductivity, its material's hit and throw effects) and the handle (integrity, armour, conductivity, attack speed and throw
// range, its material's touch effects on the hand that holds it). The var blocks are generated (D111).

// Generic slots for weapons

/// Generic main/parent type for all weapon heads
// tg: code/datums/materials/material_slots/generic.dm:4
public partial class datum_material_slot_weapon_head : datum_material_slot
{
    // tg: code/datums/materials/material_slots/generic.dm:8
    public override async DmTask<object> on_applied(atom target = null, datum_material material = null, object amount = null, object multiplier = null)
    {
        // Weapon head controls strength and conductivity
        if (!Dm.truthy(Dm.bitand(target.material_flags, MATERIAL_EFFECTS)))
            return FALSE;

        // Effect signals
        RegisterSignal(target, COMSIG_MOVABLE_IMPACT, nameof(on_throw_impact));
        RegisterSignal(target, COMSIG_MOVABLE_IMPACT_ZONE, nameof(on_throw_impact_living));
        RegisterSignal(target, COMSIG_ITEM_ATTACK, nameof(on_item_attack));
        RegisterSignal(target, COMSIG_ITEM_ATTACK_ATOM, nameof(on_item_attack));
        RegisterSignal(target, COMSIG_ITEM_ATTACK_ZONE, nameof(on_item_attack_living));

        if (!Dm.truthy(Dm.bitand(target.material_flags, MATERIAL_AFFECT_STATISTICS)))
            return FALSE;

        obj_item target_item = (obj_item)target; // tg's parameter is obj/item/target (rules 3.3)
        // Damage
        target_item.change_material_strength(material, amount, multiplier);

        // Conductivity
        float conductivity = Dm.num(material.get_property(MATERIAL_ELECTRICAL));
        float siemens_modifier = round(Dm.pow(max(0, conductivity - 1), 1.18f) * 0.15f, 0.01f);
        float siemens_mult = 1 + (siemens_modifier - 1) * Dm.num(multiplier);
        target_item.siemens_coefficient *= max(0, siemens_mult);

        if (target_item.siemens_coefficient == 0)
            Dm.mask(ref target_item.obj_flags, Dm.bitnot(CONDUCTS_ELECTRICITY));
        return null;
    }

    // tg: code/datums/materials/material_slots/generic.dm:35
    public override async DmTask<object> on_removed(atom target = null, datum_material material = null, object amount = null, object multiplier = null)
    {
        UnregisterSignal(target, interaction_signals);

        if (!Dm.truthy(Dm.bitand(target.material_flags, MATERIAL_AFFECT_STATISTICS)) || !Dm.truthy(Dm.bitand(target.material_flags, MATERIAL_EFFECTS)))
            return FALSE;

        obj_item target_item = (obj_item)target; // tg's parameter is obj/item/target (rules 3.3)
        // Damage
        target_item.change_material_strength(material, amount, multiplier, remove: TRUE);

        // Conductivity
        float conductivity = Dm.num(material.get_property(MATERIAL_ELECTRICAL));
        float siemens_modifier = round(Dm.pow(max(0, conductivity - 1), 1.18f) * 0.15f, 0.01f);
        float siemens_mult = 1 + (siemens_modifier - 1) * Dm.num(multiplier);
        if (siemens_mult > 0)
            target_item.siemens_coefficient /= siemens_mult;

        if (target_item.siemens_coefficient > 0 && Dm.truthy(Dm.bitand(Dm.initial(target_item, (obj p) => p.obj_flags), CONDUCTS_ELECTRICITY)) && !Dm.truthy(Dm.bitand(target_item.obj_flags, CONDUCTS_ELECTRICITY)))
            Dm.combine(ref target_item.obj_flags, CONDUCTS_ELECTRICITY);
        return null;
    }

    // tg: code/datums/materials/material_slots/generic.dm:36  var/static/list/interaction_signals (on_removed's)
    private static DmList interaction_signals => TgState.Current.datum_material_slot_weapon_head_on_removed__interaction_signals ??= Dm.list(
        COMSIG_MOVABLE_IMPACT,
        COMSIG_MOVABLE_IMPACT_ZONE,
        COMSIG_ITEM_ATTACK,
        COMSIG_ITEM_ATTACK_ATOM,
        COMSIG_ITEM_ATTACK_ZONE
    );

    // tg: code/datums/materials/material_slots/generic.dm:61
    [SignalHandler]
    public virtual void on_throw_impact(obj_item source = null, atom hit_atom = null, datum_thrownthing throwing_datum = null, object caught = null)
    {
        if (!Dm.truthy(caught) && !isliving(hit_atom))
            Waiting.NoSleep(affect_throw_impact(source, hit_atom, astype(throwing_datum.thrower?.resolve(), typeof(mob_living)) as mob_living));
    }

    // tg: code/datums/materials/material_slots/generic.dm:66
    [SignalHandler]
    public virtual void on_item_attack(obj_item source = null, atom_movable target = null, mob_living user = null)
    {
        // Living mobs use a different signal
        if (!isliving(target))
            Waiting.NoSleep(affect_target(source, target, user));
    }

    // tg: code/datums/materials/material_slots/generic.dm:72
    [SignalHandler]
    public virtual void on_item_attack_living(obj_item source = null, mob_living target = null, mob_living user = null, object def_zone = null)
    {
        object skin_contact = body_zone2cover_flags(def_zone);
        foreach (obj_item worn_item in Dm.each<obj_item>(target.get_equipped_items(INCLUDE_ABSTRACT)))
        {
            Dm.mask(ref skin_contact, Dm.bitnot(worn_item.body_parts_covered));
            if (!Dm.truthy(skin_contact))
                break;
        }

        Waiting.NoSleep(affect_target(source, target, user, def_zone, Dm.num(Dm.truthy(skin_contact))));
    }

    // tg: code/datums/materials/material_slots/generic.dm:83
    [SignalHandler]
    public virtual void on_throw_impact_living(obj_item source = null, mob_living target = null, object def_zone = null, object blocked = null, datum_thrownthing throwing_datum = null)
    {
        object skin_contact = body_zone2cover_flags(def_zone);
        foreach (obj_item worn_item in Dm.each<obj_item>(target.get_equipped_items(INCLUDE_ABSTRACT)))
        {
            Dm.mask(ref skin_contact, Dm.bitnot(worn_item.body_parts_covered));
            if (!Dm.truthy(skin_contact))
                break;
        }

        Waiting.NoSleep(affect_throw_impact(source, target, astype(throwing_datum.thrower?.resolve(), typeof(mob_living)) as mob_living, def_zone, Dm.num(Dm.truthy(skin_contact))));
    }

    // tg: code/datums/materials/material_slots/generic.dm:94
    public virtual async DmTask affect_target(obj_item source = null, atom target = null, mob_living user = null, object def_zone = null, float? skin_contact = null)
    {
        skin_contact ??= TRUE;
        datum_material source_mat = await source.get_material_from_slot(type) as datum_material;
        source_mat.SEND_SIGNAL(COMSIG_MATERIAL_EFFECT_HIT, Dm.list(source_mat, source, target, user, def_zone, skin_contact));
    }

    // tg: code/datums/materials/material_slots/generic.dm:98
    public virtual async DmTask affect_throw_impact(obj_item source = null, atom target = null, mob_living user = null, object def_zone = null, float? skin_contact = null)
    {
        skin_contact ??= TRUE;
        datum_material source_mat = await source.get_material_from_slot(type) as datum_material;
        source_mat.SEND_SIGNAL(COMSIG_MATERIAL_EFFECT_THROW_IMPACT, Dm.list(source_mat, source, target, user, def_zone, skin_contact));
    }
}

/// Main type for all weapon handles
// tg: code/datums/materials/material_slots/generic.dm:103
public partial class datum_material_slot_handle : datum_material_slot
{
    // tg: code/datums/materials/material_slots/generic.dm:107
    public override async DmTask<object> on_applied(atom target = null, datum_material material = null, object amount = null, object multiplier = null)
    {
        // Handle controls integrity, armor, conductivity and wieldiness stats-wise
        if (!Dm.truthy(Dm.bitand(target.material_flags, MATERIAL_EFFECTS)))
            return FALSE;

        // Effect signals
        RegisterSignal(target, COMSIG_MOVABLE_IMPACT, nameof(on_throw_impact));
        RegisterSignal(target, COMSIG_ITEM_ATTACK, nameof(on_item_attack));
        RegisterSignal(target, COMSIG_ITEM_ATTACK_ATOM, nameof(on_item_attack));
        RegisterSignal(target, COMSIG_ITEM_ATTACK_SELF, nameof(on_item_attack_self));

        if (!Dm.truthy(Dm.bitand(target.material_flags, MATERIAL_AFFECT_STATISTICS)))
            return FALSE;

        obj_item target_item = (obj_item)target; // tg's parameter is obj/item/target (rules 3.3)
        // Armor/integrity
        float integrity_mod = Dm.num(material.get_property(MATERIAL_INTEGRITY));
        await target_item.modify_max_integrity(ceil(target_item.max_integrity * integrity_mod));
        DmList armor_mods = material.get_armor_modifiers(multiplier) as DmList;
        target_item.set_armor(target_item.get_armor().generate_new_with_multipliers(armor_mods));

        // Conductivity
        float conductivity = Dm.num(material.get_property(MATERIAL_ELECTRICAL));
        float siemens_modifier = round(Dm.pow(max(0, conductivity - 1), 1.18f) * 0.15f, 0.01f);
        float siemens_mult = 1 + (siemens_modifier - 1) * Dm.num(multiplier);
        target_item.siemens_coefficient *= max(0, siemens_mult);

        if (target_item.siemens_coefficient == 0)
            Dm.mask(ref target_item.obj_flags, Dm.bitnot(CONDUCTS_ELECTRICITY));

        // Wielding
        object density = material.get_property(MATERIAL_DENSITY);
        object hardness = material.get_property(MATERIAL_HARDNESS);
        // Can be faster/slower by 2 dcs
        target_item.attack_speed += MATERIAL_PROPERTY_DIVERGENCE(density, 4, 6) * 0.5f * Dm.num(multiplier);
        target_item.throw_range += ((Dm.num(hardness) - 4) - (Dm.num(density) - 4) * 2) * Dm.num(multiplier);
        return FALSE;
    }

    // tg: code/datums/materials/material_slots/generic.dm:144
    public override async DmTask<object> on_removed(atom target = null, datum_material material = null, object amount = null, object multiplier = null)
    {
        UnregisterSignal(target, Dm.list(COMSIG_MOVABLE_IMPACT, COMSIG_ITEM_ATTACK, COMSIG_ITEM_ATTACK_ATOM, COMSIG_ITEM_ATTACK_SELF));

        if (!Dm.truthy(Dm.bitand(target.material_flags, MATERIAL_AFFECT_STATISTICS)) || !Dm.truthy(Dm.bitand(target.material_flags, MATERIAL_EFFECTS)))
            return FALSE;

        obj_item target_item = (obj_item)target; // tg's parameter is obj/item/target (rules 3.3)
        // Armor/integrity
        float integrity_mod = Dm.num(material.get_property(MATERIAL_INTEGRITY));

        await target_item.modify_max_integrity(ceil(target_item.max_integrity * integrity_mod));
        DmList armor_mods = material.get_armor_modifiers(multiplier) as DmList;
        foreach ((object armor_type, object value) in Dm.pairs(armor_mods))
        {
            if (Dm.num(value) != 0) // Needs to be restored to initial values in finalize effects, sorry
                armor_mods[armor_type] = 1 / Dm.num(value);
        }
        target_item.set_armor(target_item.get_armor().generate_new_with_multipliers(armor_mods));

        // Conductivity
        float conductivity = Dm.num(material.get_property(MATERIAL_ELECTRICAL));
        float siemens_modifier = round(Dm.pow(max(0, conductivity - 1), 1.18f) * 0.15f, 0.01f);
        float siemens_mult = 1 + (siemens_modifier - 1) * Dm.num(multiplier);
        if (siemens_mult > 0)
            target_item.siemens_coefficient /= siemens_mult;

        if (target_item.siemens_coefficient > 0 && Dm.truthy(Dm.bitand(Dm.initial(target_item, (obj p) => p.obj_flags), CONDUCTS_ELECTRICITY)) && !Dm.truthy(Dm.bitand(target_item.obj_flags, CONDUCTS_ELECTRICITY)))
            Dm.combine(ref target_item.obj_flags, CONDUCTS_ELECTRICITY);

        // Wielding
        object density = material.get_property(MATERIAL_DENSITY);
        object hardness = material.get_property(MATERIAL_HARDNESS);
        target_item.attack_speed -= MATERIAL_PROPERTY_DIVERGENCE(density, 4, 6) * 0.5f * Dm.num(multiplier);
        target_item.throw_range -= ((Dm.num(hardness) - 4) - (Dm.num(density) - 4) * 2) * Dm.num(multiplier);
        return FALSE;
    }

    // tg: code/datums/materials/material_slots/generic.dm:177
    [SignalHandler]
    public virtual void on_item_attack(obj_item source = null, atom_movable target = null, mob_living user = null)
    {
        Waiting.NoSleep(affect_user(source, user, user));
    }

    // tg: code/datums/materials/material_slots/generic.dm:181
    [SignalHandler]
    public virtual void on_item_attack_self(obj_item source = null, mob_living user = null)
    {
        Waiting.NoSleep(affect_user(source, user, user));
    }

    // tg: code/datums/materials/material_slots/generic.dm:185
    [SignalHandler]
    public virtual void on_throw_impact(obj_item source = null, atom hit_atom = null, datum_thrownthing throwing_datum = null, object caught = null)
    {
        if (Dm.truthy(caught))
            Waiting.NoSleep(affect_user(source, hit_atom as mob_living, astype(throwing_datum.thrower?.resolve(), typeof(mob_living)) as mob_living));
    }

    // tg: code/datums/materials/material_slots/generic.dm:190
    public virtual async DmTask affect_user(obj_item source = null, mob_living user = null, mob_living initiator = null)
    {
        datum_material source_mat = await source.get_material_from_slot(type) as datum_material;
        string arm_dir = IS_LEFT_INDEX(user.active_hand_index) ? BODY_ZONE_L_ARM : BODY_ZONE_R_ARM;
        if (!ishuman(user))
        {
            source_mat.SEND_SIGNAL(COMSIG_MATERIAL_EFFECT_TOUCH, Dm.list(source_mat, source, user, initiator, arm_dir, TRUE));
            return;
        }

        mob_living_carbon_human as_human = (mob_living_carbon_human)user;
        obj_item_bodypart hand = as_human.has_hand_for_held_index(as_human.get_held_index_of_item(source)) as obj_item_bodypart;
        if (hand == null) // ???
        {
            source_mat.SEND_SIGNAL(COMSIG_MATERIAL_EFFECT_TOUCH, Dm.list(source_mat, source, user, initiator, arm_dir, FALSE)); // ...no hand, no skin contact?
            return;
        }

        DmList hand_covers = as_human.get_clothing_on_part(hand) as DmList;
        float hand_covered = FALSE;
        foreach (obj_item worn_item in Dm.each<obj_item>(hand_covers))
        {
            if (Dm.truthy(Dm.bitand(worn_item.body_parts_covered, HANDS)))
            {
                hand_covered = TRUE;
                break;
            }
        }

        source_mat.SEND_SIGNAL(COMSIG_MATERIAL_EFFECT_TOUCH, Dm.list(source_mat, source, user, initiator, hand.body_zone, Dm.not(hand_covered)));
    }
}

public sealed partial class TgState
{
    // tg: code/datums/materials/material_slots/generic.dm:36  var/static/list/interaction_signals (on_removed's)
    internal DmList datum_material_slot_weapon_head_on_removed__interaction_signals;
}
