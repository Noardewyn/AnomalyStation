// Translated from tgstation (snapshot 1d99bd7f): code/datums/materials/_material.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file. The var block is generated (D111) but for id, declared here as an
// object: tg stores the material's type in it (Initialize :96) or, for a bespoke material, a text id (:73, :79).

/*! Material datum

Simple datum which is instanced once per type and is used for every object of said material. It has a variety of variables that define behavior. Subtyping from this makes it easier to create your own materials.

*/


// tg: code/datums/materials/_material.dm:8
[DmPath("/datum/material")]
public partial class datum_material : datum
{
    /// What the material is indexed by in the SSmaterials.materials list. Defaults to the type of the material.
    // tg: code/datums/materials/_material.dm:15
    public object id = null;

    // tg: code/datums/materials/_material.dm:8 (the type's own var defaults: a hand-written var block, types-converter.md 3.2)
    public datum_material()
    {
        abstract_type = typeof(datum_material);
    }

    /** Handles initializing the material.
     *
     * Arguments:
     * - _id: The ID the material should use. Overrides the existing ID.
     */
    // tg: code/datums/materials/_material.dm:92
    public virtual async DmTask<object> Initialize(object _id = null, params object[] __extra)
    {
        if (Dm.truthy(_id))
            id = _id;
        else if (id == null)
            id = type;

        if (Dm.truthy(texture_layer_icon_state))
            cached_texture_filter_icon = Dm.icon(Dm.resource("icons/turf/composite.dmi"), texture_layer_icon_state);

        foreach (object prop_id in Dm.each(mat_properties))
        {
            datum_material_property property = SSmaterials.properties[prop_id] as datum_material_property;
            property.attach_to(this);
        }

        return TRUE;
    }

    /// This proc is called when the material is added to an object.
    /// Can be called even if the material is covered by a slot
    // tg: code/datums/materials/_material.dm:109
    [ShouldCallParent]
    public virtual async DmTask on_applied(atom source = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_MATERIAL_APPLIED]))
            this._SendSignal(COMSIG_MATERIAL_APPLIED, Dm.list(this, source, mat_amount, multiplier, from_slot));

        if (!Dm.truthy(Dm.bitand(source.material_flags, MATERIAL_EFFECTS)) || Dm.truthy(from_slot))
            return;

        if (Dm.truthy(Dm.bitand(track_flags, MATERIAL_TRACK_CONTACT)))
        {
            on_applied__turf_interactions ??= typecacheof(Dm.list(
                typeof(turf_open),
                typeof(obj_structure_platform),
                typeof(obj_structure_table),
                typeof(obj_structure_rack),
                typeof(obj_structure_bed),
                typeof(obj_structure_closet_crate),
                typeof(obj_structure_reagent_dispensers),
                typeof(obj_structure_altar)
            )) as DmList;
            if (Dm.truthy(is_type_in_typecache(source, on_applied__turf_interactions)))
            {
                await source.AddComponent(typeof(datum_component_material_turf_tracking), this);
            }

            if (isclosedturf(source))
                RegisterSignal(source, COMSIG_LIVING_DISARM_COLLIDE, nameof(on_wall_shove_collide));
        }

        if (Dm.truthy(Dm.bitand(track_flags, MATERIAL_TRACK_IMPACT)))
        {
            RegisterSignal(source, COMSIG_MOVABLE_IMPACT, nameof(on_throw_impact));
            RegisterSignal(source, COMSIG_MOVABLE_IMPACT_ZONE, nameof(on_throw_impact_living));
            RegisterSignal(source, COMSIG_ITEM_ATTACK, nameof(on_item_attack));
            RegisterSignal(source, COMSIG_ITEM_ATTACK_ATOM, nameof(on_item_attack));
            // Allow recipe crafting for stack items
            if (!isstack(source))
                RegisterSignal(source, COMSIG_ITEM_ATTACK_SELF, nameof(on_item_attack_self));
            RegisterSignal(source, COMSIG_ITEM_ATTACK_ZONE, nameof(on_item_attack_living));
        }
    }

    // tg: code/datums/materials/_material.dm:117  var/static/list/turf_interactions (a proc-level static, rules 10.6)
    private static ref DmList on_applied__turf_interactions => ref TgState.Current.datum_material__on_applied__turf_interactions;

    /// This proc is called when the material becomes the one the object is composed of the most
    /// Only called when the material isn't assigned to a slot
    // tg: code/datums/materials/_material.dm:145
    [ShouldCallParent]
    public virtual async DmTask on_main_applied(atom source = null, object mat_amount = null, object multiplier = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_MATERIAL_MAIN_APPLIED]))
            this._SendSignal(COMSIG_MATERIAL_MAIN_APPLIED, Dm.list(this, source, mat_amount, multiplier));
    }

    /// This proc is called when the material is removed from an object.
    // tg: code/datums/materials/_material.dm:150
    [ShouldCallParent]
    public virtual void on_removed(atom source = null, object mat_amount = null, object material_flags = null, object from_slot = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_MATERIAL_REMOVED]))
            this._SendSignal(COMSIG_MATERIAL_REMOVED, Dm.list(this, source, mat_amount, material_flags, from_slot));

        if (!Dm.truthy(Dm.bitand(source.material_flags, MATERIAL_EFFECTS)))
            return;

        if (Dm.truthy(Dm.bitand(track_flags, MATERIAL_TRACK_CONTACT)))
        {
            on_removed__material_signals ??= Dm.list(
                COMSIG_LIVING_DISARM_COLLIDE
            );
            UnregisterSignal(source, on_removed__material_signals);
            object tracker_components = source.GetComponents(typeof(datum_component_material_turf_tracking));
            if (istype(tracker_components, typeof(datum_component_material_turf_tracking)))
                qdel(tracker_components);
            else
            {
                // Delete all trackers linked to ourselves (might be multiple if we've got multiple slots going on)
                foreach (object tracker in Dm.each(tracker_components))
                {
                    if (((datum_component_material_turf_tracking)tracker).owner_material == this)
                        qdel(tracker);
                }
            }
        }

        if (Dm.truthy(Dm.bitand(track_flags, MATERIAL_TRACK_IMPACT)))
        {
            on_removed__material_signals__1 ??= Dm.list(
                COMSIG_MOVABLE_IMPACT,
                COMSIG_MOVABLE_IMPACT_ZONE,
                COMSIG_ITEM_ATTACK,
                COMSIG_ITEM_ATTACK_ATOM,
                COMSIG_ITEM_ATTACK_SELF,
                COMSIG_ITEM_ATTACK_ZONE
            );
            UnregisterSignal(source, on_removed__material_signals__1);
        }
    }

    // tg: code/datums/materials/_material.dm:158  var/static/list/material_signals (a proc-level static, rules 10.6)
    private static ref DmList on_removed__material_signals => ref TgState.Current.datum_material__on_removed__material_signals;
    // tg: code/datums/materials/_material.dm:172  var/static/list/material_signals (the second static of the proc, renamed __1)
    private static ref DmList on_removed__material_signals__1 => ref TgState.Current.datum_material__on_removed__material_signals__1;

    /// This proc is called when the material is no longer the one the object is composed by the most
    // tg: code/datums/materials/_material.dm:183
    [ShouldCallParent]
    public virtual void on_main_removed(atom source = null, object mat_amount = null, object multiplier = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_MATERIAL_MAIN_REMOVED]))
            this._SendSignal(COMSIG_MATERIAL_MAIN_REMOVED, Dm.list(this, source, mat_amount, multiplier));
    }

    // tg: code/datums/materials/_material.dm:187
    [SignalHandler]
    public virtual void on_wall_shove_collide(turf_closed source = null, mob_living shover = null, mob_living target = null, object shove_flags = null, obj_item weapon = null)
    {
        // If any part is exposed it makes sense it'd impact the wall
        float skin_contact = HEAD | CHEST | GROIN | LEGS | FEET | ARMS | HANDS;
        foreach (obj_item worn_item in Dm.each<obj_item>(target.get_equipped_items(INCLUDE_ABSTRACT)))
        {
            skin_contact = Dm.bitand(skin_contact, Dm.bitnot(worn_item.body_parts_covered));
            if (!Dm.truthy(skin_contact))
                break;
        }

        if (Dm.truthy(this._listen_lookup?[COMSIG_MATERIAL_EFFECT_TOUCH]))
            this._SendSignal(COMSIG_MATERIAL_EFFECT_TOUCH, Dm.list(this, source, target, shover, null, Dm.num(Dm.truthy(skin_contact))));
    }

    // tg: code/datums/materials/_material.dm:199
    [SignalHandler]
    public virtual void on_item_attack(obj_item source = null, object target = null, mob_living user = null)
    {
        impact_affect_touch(source, user, user);
        // Living mobs use a different signal
        if (!isliving(target))
            impact_affect_target(source, target as atom, user);
    }

    // tg: code/datums/materials/_material.dm:206
    [SignalHandler]
    public virtual void on_item_attack_living(obj_item source = null, mob_living target = null, mob_living user = null, object def_zone = null)
    {
        float skin_contact = Dm.num(body_zone2cover_flags(def_zone));
        foreach (obj_item worn_item in Dm.each<obj_item>(target.get_equipped_items(INCLUDE_ABSTRACT)))
        {
            skin_contact = Dm.bitand(skin_contact, Dm.bitnot(worn_item.body_parts_covered));
            if (!Dm.truthy(skin_contact))
                break;
        }

        impact_affect_target(source, target, user, def_zone, Dm.num(Dm.truthy(skin_contact)));
    }

    // tg: code/datums/materials/_material.dm:216
    [SignalHandler]
    public virtual void on_item_attack_self(obj_item source = null, mob_living user = null)
    {
        impact_affect_touch(source, user, user);
    }

    // tg: code/datums/materials/_material.dm:220
    [SignalHandler]
    public virtual void on_throw_impact(obj_item source = null, atom hit_atom = null, datum_thrownthing throwing_datum = null, object caught = null)
    {
        if (Dm.truthy(caught))
            impact_affect_touch(source, hit_atom as mob_living, astype(throwing_datum.thrower?.resolve(), typeof(mob_living)) as mob_living);
        else if (!isliving(hit_atom)) // Hit mobs have armor checking
            impact_affect_throw_impact(source, hit_atom, astype(throwing_datum.thrower?.resolve(), typeof(mob_living)) as mob_living);
    }

    // tg: code/datums/materials/_material.dm:227
    [SignalHandler]
    public virtual void on_throw_impact_living(obj_item source = null, mob_living target = null, object def_zone = null, object blocked = null, datum_thrownthing throwing_datum = null)
    {
        float skin_contact = Dm.num(body_zone2cover_flags(def_zone));
        foreach (obj_item worn_item in Dm.each<obj_item>(target.get_equipped_items(INCLUDE_ABSTRACT)))
        {
            skin_contact = Dm.bitand(skin_contact, Dm.bitnot(worn_item.body_parts_covered));
            if (!Dm.truthy(skin_contact))
                break;
        }

        impact_affect_throw_impact(source, target, astype(throwing_datum.thrower?.resolve(), typeof(mob_living)) as mob_living, def_zone, Dm.num(Dm.truthy(skin_contact)));
    }

    // tg: code/datums/materials/_material.dm:238
    public virtual void impact_affect_touch(obj_item source = null, mob_living user = null, mob_living initiator = null)
    {
        string arm_dir = IS_LEFT_INDEX(user.active_hand_index) ? BODY_ZONE_L_ARM : BODY_ZONE_R_ARM;
        if (!ishuman(user))
        {
            if (Dm.truthy(this._listen_lookup?[COMSIG_MATERIAL_EFFECT_TOUCH]))
                this._SendSignal(COMSIG_MATERIAL_EFFECT_TOUCH, Dm.list(this, source, user, initiator, arm_dir, TRUE));
            return;
        }

        mob_living_carbon_human as_human = user as mob_living_carbon_human;
        obj_item_bodypart hand = as_human.has_hand_for_held_index(as_human.get_held_index_of_item(source)) as obj_item_bodypart;
        if (!Dm.truthy(hand))
        {
            if (Dm.truthy(this._listen_lookup?[COMSIG_MATERIAL_EFFECT_TOUCH]))
                this._SendSignal(COMSIG_MATERIAL_EFFECT_TOUCH, Dm.list(this, source, user, initiator, arm_dir, FALSE));
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

        if (Dm.truthy(this._listen_lookup?[COMSIG_MATERIAL_EFFECT_TOUCH]))
            this._SendSignal(COMSIG_MATERIAL_EFFECT_TOUCH, Dm.list(this, source, user, initiator, hand.body_zone, Dm.num(!Dm.truthy(hand_covered))));
    }

    // tg: code/datums/materials/_material.dm:259
    public virtual void impact_affect_target(obj_item source = null, atom target = null, mob_living user = null, object def_zone = null, float? skin_contact = null)
    {
        skin_contact ??= TRUE;
        if (Dm.truthy(this._listen_lookup?[COMSIG_MATERIAL_EFFECT_HIT]))
            this._SendSignal(COMSIG_MATERIAL_EFFECT_HIT, Dm.list(this, source, target, user, def_zone, skin_contact));
    }

    // tg: code/datums/materials/_material.dm:262
    public virtual void impact_affect_throw_impact(obj_item source = null, atom target = null, mob_living user = null, object def_zone = null, float? skin_contact = null)
    {
        skin_contact ??= TRUE;
        if (Dm.truthy(this._listen_lookup?[COMSIG_MATERIAL_EFFECT_THROW_IMPACT]))
            this._SendSignal(COMSIG_MATERIAL_EFFECT_THROW_IMPACT, Dm.list(this, source, target, user, def_zone, skin_contact));
    }

    // tg: code/datums/materials/_material.dm:265
    public virtual async DmTask setup_glow(turf on = null)
    {
        // The starlight of a transparent material turf over space (:266-274: the plane offset and parallax checks, the
        // COMSIG_STARLIGHT_COLOR_CHANGED registration and set_light with GLOB.starlight_color): space's starlight is a
        // deferred leaf of M3 (GLOB.starlight_color is not declared), so a glass floor over space does not glow either.
        Dm.Deferred("code/datums/materials/_material.dm:265", "setup_glow: the starlight of a transparent material turf over space (starlight, as space's own, D24 lighting pass)");
    }

    // tg: code/datums/materials/_material.dm:280
    public virtual async DmTask lit_turf_deleted(turf source = null)
    {
        await source.set_light(0, 0, null);
    }


    ////Called in `/datum/component/edible/proc/on_material_effects`
    // tg: code/datums/materials/_material.dm:285
    public virtual async DmTask on_edible_applied(atom source = null, datum_component_edible edible = null)
    {
        return;
    }

    ////Called in `/datum/component/edible/proc/on_remove_material_effects`
    // tg: code/datums/materials/_material.dm:289
    public virtual async DmTask on_edible_removed(atom source = null, datum_component_edible edible = null)
    {
        return;
    }

    /**
     * This proc is called when the mat is found in an item that's consumed by accident. see /obj/item/proc/on_accidental_consumption.
     * Arguments
     * * M - person consuming the mat
     * * S - (optional) item the mat is contained in (NOT the item with the mat itself)
     */
    // tg: code/datums/materials/_material.dm:298
    [ShouldCallParent]
    public virtual async DmTask<object> on_accidental_mat_consumption(mob_living_carbon victim = null, obj_item source_item = null)
    {
        if (!Dm.truthy(material_reagent))
            return FALSE;

        float effect_multiplier = Dm.num(source_item.custom_materials[type]) / SHEET_MATERIAL_AMOUNT;
        if (!islist(material_reagent))
        {
            if (victim.reagents != null)
                await ((datum_reagents)victim.reagents).add_reagent(material_reagent, rand(6, 8) * effect_multiplier);
            if (source_item?.reagents != null)
                await ((datum_reagents)source_item.reagents).add_reagent(material_reagent, Dm.num(((datum_reagents)source_item.reagents).total_volume) * MATERIAL_REAGENT_CONSUMPTION_MULT * effect_multiplier);
            return TRUE;
        }

        foreach (object reagent_type in Dm.each(material_reagent))
        {
            float amount_mult = Dm.num(((DmList)material_reagent)[reagent_type]) / length(material_reagent);
            if (victim.reagents != null)
                await ((datum_reagents)victim.reagents).add_reagent(material_reagent, rand(6, 8) * effect_multiplier * amount_mult);
            if (source_item?.reagents != null)
                await ((datum_reagents)source_item.reagents).add_reagent(material_reagent, Dm.num(((datum_reagents)source_item.reagents).total_volume) * MATERIAL_REAGENT_CONSUMPTION_MULT * effect_multiplier * amount_mult);
        }
        return TRUE;
    }

    /** Returns the composition of this material.
     *
     * Mostly used for alloys when breaking down materials.
     *
     * Arguments:
     * - amount: The amount of the material to break down.
     */
    // tg: code/datums/materials/_material.dm:323
    public virtual async DmTask<object> return_composition(float? amount = null, object flags = null)
    {
        amount ??= 1;
        // Yes we need the parenthesis, without them BYOND stringifies src into "src" and things break.
        return Dm.list(Dm.kv(this, amount));
    }

    ///Returns the list of armor modifiers, with each element having its assoc value multiplied by the multiplier arg
    // tg: code/datums/materials/_material.dm:328
    [ShouldNotOverride]
    public virtual object get_armor_modifiers(object multiplier = null)
    {
        object density = get_property(MATERIAL_DENSITY);
        object hardness = get_property(MATERIAL_HARDNESS);
        object flexibility = get_property(MATERIAL_FLEXIBILITY);
        object reflectivity = get_property(MATERIAL_REFLECTIVITY);
        object electric = get_property(MATERIAL_ELECTRICAL);
        object thermal = get_property(MATERIAL_THERMAL);
        object chemical = get_property(MATERIAL_CHEMICAL);
        object flammability = get_property(MATERIAL_FLAMMABILITY); // Optional, might be not present
        // Welcome to hell
        DmList armor_modifiers = Dm.list(
            // Based on density, with a bonus/malus for matching flexibility
            // We cap divergence at 4 (reduced by hardness above 6 for REALLY dense stuff) to make it not extremely punishing on light fabrics or heavy materials
            // Iron at a baseline of density of 6 and flexibility of 4 has divergence of 4, so (1 + 0.2) / (0.8 + 0.4) = 1
            Dm.kv(MELEE, (1 + (Dm.num(density) - 4) * 0.1f) / (0.8f + min(4 - max(0, Dm.num(hardness) - 6), abs(Dm.num(flexibility) - Dm.num(density))) * 0.1f)),
            // Hardness and density, with flexibility actually being detrimental
            Dm.kv(BULLET, (1 + (Dm.num(density) - 4) * 0.025f + (Dm.num(hardness) - 4) * 0.075f) / (1 - max(0, Dm.num(flexibility) - 2) * 0.1f)),
            // 0.6 ~ 1 for reflectivity below 4, 1 ~ 1.4 for reflectivity above 6, reduced for transparent materials
            Dm.kv(LASER, 1 + MATERIAL_PROPERTY_DIVERGENCE(reflectivity, 4, 6) * 0.1f - (255 - Dm.num(alpha)) / 50 * 0.2f),
            // Essentially laser but with contribution split between reflectivity and inverse electric conductivity
            // Here reflectivity applies if its below 4 or above 8, and conductivity if its below 4 or above 6
            Dm.kv(ENERGY, 1 + MATERIAL_PROPERTY_DIVERGENCE(reflectivity, 4, 8) * 0.05f - MATERIAL_PROPERTY_DIVERGENCE(electric, 4, 6) * 0.1f),
            // Linearly scales from 0.2 to 1.8 with density
            Dm.kv(BOMB, 1 + (Dm.num(density) - 4) * 0.2f),
            // Each level of flammability reduces FIRE armor by 20%, with thermal conductivity reducing it by further 20% for each level above 6 and increasing for each level below 2
            Dm.kv(FIRE, max(0, 1 - max(0, (Dm.num(thermal) - 6) * 0.2f) + max(0, (2 - Dm.num(thermal)) * 0.2f) - Dm.num(flammability) * 0.2f)),
            // Linearly scales from 0.2 to 1.8 with chemical resistance
            Dm.kv(ACID, 1 + (Dm.num(chemical) - 4) * 0.2f)
        );

        foreach (object armor_key in Dm.each(armor_modifiers))
        {
            // Safety check to ensure that we don't have inverted armor values
            if (Dm.num(armor_modifiers[armor_key]) < 0)
                armor_modifiers[armor_key] = 0;
            armor_modifiers[armor_key] = GET_MATERIAL_MODIFIER(armor_modifiers[armor_key], multiplier);
        }

        return armor_modifiers;
    }

    // tg: code/datums/materials/_material.dm:367
    public virtual object get_property(object prop_id = null)
    {
        if ((mat_properties as DmList)?[prop_id] != null)
            return ((DmList)mat_properties)[prop_id];

        datum_material_property_derived derived_prop = SSmaterials.properties[prop_id] as datum_material_property_derived;
        if (!istype(derived_prop, typeof(datum_material_property_derived)))
            return null; // Property was not specified on the material and wasn't a derived one
        return derived_prop.get_value(this);
    }
}

// tg: code/datums/materials/_material.dm:276
public partial class turf : atom
{
    // tg: code/datums/materials/_material.dm:276
    public virtual void material_starlight_changed(datum source = null, object old_star = null, object new_star = null)
    {
        if (Dm.eq(light_color, old_star))
            set_light_color(new_star);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/materials/_material.dm:117  var/static/list/turf_interactions = typecacheof(list(...))
    internal DmList datum_material__on_applied__turf_interactions;
    // tg: code/datums/materials/_material.dm:158  var/static/list/material_signals = list(COMSIG_LIVING_DISARM_COLLIDE)
    internal DmList datum_material__on_removed__material_signals;
    // tg: code/datums/materials/_material.dm:172  var/static/list/material_signals = list(COMSIG_MOVABLE_IMPACT, ...)
    internal DmList datum_material__on_removed__material_signals__1;
}
