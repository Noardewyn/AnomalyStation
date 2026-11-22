// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/bloodysoles.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M6 slice 1 (blood in the world): the whole file, the shoes and bare feet that pick blood up from pools and leave bloody
// footprints, and the bloody feet overlay. The root's var block is hand-written: tg stores a non-carbon living mob in
// `var/mob/living/carbon/wielder` (the bot subtype, bloodysoles.dm:362), so the var is `object` (rules 3.2, typed vars that
// hold wider values) and each member access casts to the class that declares the member. The subtypes' var blocks are
// generated (D111) but for the feet's static bloody_feet, which lives in TgState (rules 10.6).

/**
 * Component for clothing items that can pick up blood from decals and spread it around everywhere when walking, such as shoes or suits with integrated shoes.
 */
// tg: code/datums/components/bloodysoles.dm:4
[DmPath("/datum/component/bloodysoles")]
public partial class datum_component_bloodysoles : datum_component
{
    /// The ITEM_SLOT_* slot the item is equipped on, if it is.
    public object equipped_slot; // tg: code/datums/components/bloodysoles.dm:6
    /// What percentage of the bloodiness is deposited on the ground per step
    public float blood_dropped_per_step = 3; // tg: code/datums/components/bloodysoles.dm:8
    /// Bloodiness on our clothines
    [DmFinal]
    public float total_bloodiness = 0; // tg: code/datums/components/bloodysoles.dm:10
    /// Either the mob carrying the item, or the mob itself for the /feet component subtype
    [DmFinal]
    public object wielder; // tg: code/datums/components/bloodysoles.dm:12
    /// The world.time when we last picked up blood
    [DmFinal]
    public object last_pickup; // tg: code/datums/components/bloodysoles.dm:14
    /// How much blood can we hold maximum
    public float max_bloodiness = BLOOD_ITEM_MAX; // tg: code/datums/components/bloodysoles.dm:16
    /// Multiplier on how much blood taken from pools
    public float share_mod = 1; // tg: code/datums/components/bloodysoles.dm:18

    // tg: code/datums/components/bloodysoles.dm:20
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isclothing(parent))
            return COMPONENT_INCOMPATIBLE;

        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_equip));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(on_drop));
        RegisterSignal(parent, COMSIG_COMPONENT_CLEAN_ACT, nameof(on_clean));
        return null;
    }

    // tg: code/datums/components/bloodysoles.dm:28
    public override object Destroy(float? force = null)
    {
        wielder = null;
        return base.Destroy(force);
    }

    /**
     * Unregisters from the wielder if necessary
     */
    // tg: code/datums/components/bloodysoles.dm:35
    public virtual void unregister()
    {
        if (!QDELETED(wielder as datum))
        {
            UnregisterSignal((datum)wielder, COMSIG_MOVABLE_MOVED);
            UnregisterSignal((datum)wielder, COMSIG_STEP_ON_BLOOD);
        }
        wielder = null;
        equipped_slot = null;
    }

    /**
     * Returns true if the parent item is obscured by something else that the wielder is wearing
     */
    // tg: code/datums/components/bloodysoles.dm:45
    public virtual object is_obscured()
    {
        return (Dm.truthy(Dm.bitand(hidden_slots_to_inventory_slots(((mob_living_carbon)wielder).covered_slots), equipped_slot)) || Dm.truthy(is_under_feet_covered())) ? TRUE : FALSE;
    }

    /**
     * Returns true if the parent item is worn in the ITEM_SLOT_ICLOTHING slot and the
     * wielder is wearing something on their shoes.
     *
     * Allows for jumpsuits to cover feet without getting all bloodied when their wearer
     * is wearing shoes.
     */
    // tg: code/datums/components/bloodysoles.dm:55
    public virtual object is_under_feet_covered()
    {
        if (!Dm.truthy(Dm.bitand(equipped_slot, ITEM_SLOT_ICLOTHING)))
            return FALSE;

        return (((mob)wielder).get_item_by_slot(ITEM_SLOT_FEET) != null) ? TRUE : FALSE;
    }

    /**
     * Run to update the icon of the parent
     */
    // tg: code/datums/components/bloodysoles.dm:64
    public virtual void update_icon()
    {
        obj_item parent_item = parent as obj_item;
        parent_item.update_slot_icon();
    }

    /// Called whenever the value of bloody_soles changes to update our icon and behavior
    // tg: code/datums/components/bloodysoles.dm:69
    public virtual void change_blood_amount(float some_amount)
    {
        total_bloodiness = clamp(round(total_bloodiness + some_amount, 0.1f), 0, max_bloodiness);
        update_icon();

        if (!Dm.truthy(wielder))
            return;

        if (total_bloodiness <= BLOOD_FOOTPRINTS_MIN * 2) // Need twice that amount to make footprints
            UnregisterSignal((datum)wielder, COMSIG_MOVABLE_MOVED);
        else
            RegisterSignal((datum)wielder, COMSIG_MOVABLE_MOVED, nameof(on_moved), @override: TRUE);
    }

    /**
     * Run to equally share the blood between us and a decal
     */
    // tg: code/datums/components/bloodysoles.dm:84
    public virtual object share_blood(obj_effect_decal_cleanable_blood pool)
    {
        // Share the blood between our boots and the blood pool
        float new_total_bloodiness = min(max_bloodiness, share_mod * (pool.bloodiness + total_bloodiness) / 2);
        if (new_total_bloodiness == total_bloodiness || new_total_bloodiness == 0)
            return FALSE;

        float delta = new_total_bloodiness - total_bloodiness;
        Waiting.NoSleep(pool.adjust_bloodiness(-1 * delta));
        change_blood_amount(delta);

        if (!ishuman(parent))
        {
            atom to_bloody_atom = parent as atom;
            Waiting.NoSleep(to_bloody_atom.add_blood_DNA(GET_ATOM_BLOOD_DNA(pool)));
            return TRUE;
        }

        object bloody_slots = Dm.bitor(Dm.bitor(ITEM_SLOT_OCLOTHING, ITEM_SLOT_ICLOTHING), ITEM_SLOT_FEET);
        mob_living_carbon_human to_bloody = parent as mob_living_carbon_human;
        if (to_bloody.body_position == LYING_DOWN)
            Dm.combine(ref bloody_slots, Dm.bitor(Dm.bitor(ITEM_SLOT_HEAD, ITEM_SLOT_MASK), ITEM_SLOT_GLOVES));

        Waiting.NoSleep(to_bloody.add_blood_DNA_to_items(GET_ATOM_BLOOD_DNA(pool), bloody_slots));
        return TRUE;
    }

    /**
     * Adds blood to an existing (or new) footprint
     */
    // tg: code/datums/components/bloodysoles.dm:110
    public virtual void add_blood_to_footprint(obj_effect_decal_cleanable_blood_footprints footprint, float bloodiness_to_add, object exiting = null, object no_dna = null)
    {
        add_parent_to_footprint(footprint);
        Waiting.NoSleep(footprint.adjust_bloodiness(bloodiness_to_add));
        if (!Dm.truthy(no_dna))
            Waiting.NoSleep(footprint.add_blood_DNA(get_blood_dna()));
        float new_alpha = min(BLOODY_FOOTPRINT_BASE_ALPHA + (255 - BLOODY_FOOTPRINT_BASE_ALPHA) * footprint.bloodiness / (BLOOD_ITEM_MAX * BLOOD_TO_UNITS_MULTIPLIER), 255);
        if (new_alpha > footprint.alpha)
            footprint.alpha = new_alpha;
        if (Dm.truthy(exiting))
            footprint.exited_dirs = Dm.bitor(footprint.exited_dirs, ((atom)wielder).dir);
        else
            footprint.entered_dirs = Dm.bitor(footprint.entered_dirs, ((atom)wielder).dir);
        footprint.update_appearance();
    }

    /// Fetches this component's blood DNA
    // tg: code/datums/components/bloodysoles.dm:125
    public virtual DmList get_blood_dna()
    {
        atom atom_parent = parent as atom;
        return GET_ATOM_BLOOD_DNA(atom_parent);
    }

    /**
     * Adds the parent type to the footprint's shoe_types var
     */
    // tg: code/datums/components/bloodysoles.dm:132
    public virtual void add_parent_to_footprint(obj_effect_decal_cleanable_blood_footprints footprint)
    {
        LAZYOR(ref footprint.shoe_types, parent.type);
    }

    /**
     * Called when the parent item is equipped by someone
     *
     * Used to register our wielder
     */
    // tg: code/datums/components/bloodysoles.dm:140  (SIGNAL_HANDLER is dropped, rules 7.9)
    [SignalHandler]
    public virtual void on_equip(datum source = null, mob equipper = null, object slot = null)
    {
        if (!iscarbon(equipper))
            return;
        obj_item parent_item = parent as obj_item;
        if (!Dm.truthy(Dm.bitand(parent_item.slot_flags, slot)))
        {
            unregister();
            return;
        }

        equipped_slot = slot;
        wielder = equipper;
        if (total_bloodiness > BLOOD_FOOTPRINTS_MIN * 2)
            RegisterSignal((datum)wielder, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        RegisterSignal((datum)wielder, COMSIG_STEP_ON_BLOOD, nameof(on_step_blood));
    }

    /**
     * Called when the parent item has been dropped
     *
     * Used to deregister our wielder
     */
    // tg: code/datums/components/bloodysoles.dm:161  (SIGNAL_HANDLER is dropped, rules 7.9)
    [SignalHandler]
    public virtual void on_drop(datum source = null, mob dropper = null)
    {
        unregister();
    }

    /**
     * Called when the wielder has moved
     *
     * Used to make bloody footprints on the ground
     */
    // tg: code/datums/components/bloodysoles.dm:170  (SIGNAL_HANDLER is dropped, rules 7.9)
    [SignalHandler]
    public virtual void on_moved(datum source = null, atom old_loc = null, object Dir = null, object Forced = null)
    {
        if (total_bloodiness <= 0)
            return;
        if (QDELETED(wielder as datum) || Dm.truthy(is_obscured()))
            return;
        mob_living living_wielder = (mob_living)wielder;
        if (living_wielder.body_position == LYING_DOWN || Dm.truthy(Dm.bitand(living_wielder.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
            return;

        atom parent_atom = parent as atom;
        float blood_used = round(total_bloodiness / 3, 0.01f);
        // This is more of a sanity check than an actual concern, but *just* in case
        object blood_flags = has_blood_flag(get_blood_dna(), Dm.bitor(BLOOD_ADD_DNA, BLOOD_COVER_TURFS));

        // Add footprints in old loc if we have enough cream
        if (blood_used >= BLOOD_FOOTPRINTS_MIN)
        {
            turf old_loc_turf = get_turf(old_loc);
            obj_effect_decal_cleanable_blood_footprints old_loc_prints = Dm.locate<obj_effect_decal_cleanable_blood_footprints>(old_loc_turf);
            if (!Dm.truthy(Dm.bitand(blood_flags, BLOOD_COVER_TURFS)))
            {
                if (Dm.truthy(Dm.bitand(blood_flags, BLOOD_ADD_DNA)) && old_loc_prints != null)
                    Waiting.NoSleep(old_loc_prints.add_blood_DNA(get_blood_dna()));
            }
            else if (old_loc_prints != null)
                add_blood_to_footprint(old_loc_prints, 0, TRUE); // Add no actual blood, just update sprite
            else if (Dm.locate<obj_effect_decal_cleanable_blood>(old_loc_turf) != null)
            {
                // No footprints in the tile we left, but there was some other blood pool there. Add exit footprints on it
                change_blood_amount(-1 * blood_used);
                old_loc_prints = Dm.@new<obj_effect_decal_cleanable_blood_footprints>(old_loc_turf, null, get_blood_dna());
                old_loc_prints.alpha = 0;
                if (!QDELETED(old_loc_prints)) // prints merged
                    add_blood_to_footprint(old_loc_prints, blood_used, TRUE, no_dna: TRUE);
                blood_used = round(total_bloodiness / 3, 0.01f);
            }
        }

        // If we picked up the blood on this tick in on_step_blood, don't make footprints at the same place
        if (Dm.eq(last_pickup, world.time))
            return;

        if (blood_used < BLOOD_FOOTPRINTS_MIN)
            return;

        // Create new footprints
        turf new_loc_turf = get_turf(parent_atom);
        obj_effect_decal_cleanable_blood_footprints new_loc_prints = Dm.locate<obj_effect_decal_cleanable_blood_footprints>(new_loc_turf);
        if (new_loc_prints != null)
        {
            if (Dm.truthy(Dm.bitand(blood_flags, BLOOD_COVER_TURFS)))
            {
                add_blood_to_footprint(new_loc_prints, 0, FALSE); // Add no actual blood, just update sprite
                return;
            }
            if (Dm.truthy(Dm.bitand(blood_flags, BLOOD_ADD_DNA)))
                Waiting.NoSleep(new_loc_prints.add_blood_DNA(get_blood_dna()));
            return;
        }

        if (!Dm.truthy(Dm.bitand(blood_flags, BLOOD_COVER_TURFS)))
            return;

        change_blood_amount(-1 * blood_used);
        new_loc_prints = Dm.@new<obj_effect_decal_cleanable_blood_footprints>(new_loc_turf, null, get_blood_dna());
        new_loc_prints.alpha = 0;
        if (!QDELETED(new_loc_prints)) // prints merged
            add_blood_to_footprint(new_loc_prints, blood_used, FALSE, no_dna: TRUE);
    }

    /**
     * Called when the wielder steps in a pool of blood
     *
     * Used to make the parent item bloody
     */
    // tg: code/datums/components/bloodysoles.dm:235  (SIGNAL_HANDLER is dropped, rules 7.9)
    [SignalHandler]
    public virtual void on_step_blood(datum source = null, obj_effect_decal_cleanable pool = null)
    {
        if (QDELETED(wielder as datum) || Dm.truthy(is_obscured()) || Dm.truthy(Dm.bitand(((mob_living)wielder).movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
            return;

        /// The character is agile enough to not mess their clothing and hands just from one blood splatter at floor
        if (HAS_TRAIT((datum)wielder, TRAIT_LIGHT_STEP))
            return;

        // Don't share from other feetprints, not super realistic but I think it ruins the effect a bit
        if (istype(pool, typeof(obj_effect_decal_cleanable_blood_footprints)))
            return;

        share_blood(pool as obj_effect_decal_cleanable_blood);
        last_pickup = world.time;
    }

    /**
     * Called when the parent item is being washed
     */
    // tg: code/datums/components/bloodysoles.dm:255  (SIGNAL_HANDLER is dropped, rules 7.9)
    [SignalHandler]
    public virtual object on_clean(datum source = null, object clean_types = null)
    {
        if (!Dm.truthy(Dm.bitand(clean_types, CLEAN_TYPE_BLOOD)))
            return NONE;

        total_bloodiness = 0;
        obj_item_clothing_shoes parent_shoes = parent as obj_item_clothing_shoes;
        if (parent_shoes == null) // if we are wearing shoes, wash() will already be calling update_worn_shoes() so we don't have to do it twice
            update_icon();
        return Dm.bitor(COMPONENT_CLEANED, COMPONENT_CLEANED_GAIN_XP);
    }
}

/**
 * Like its parent but can be applied to carbon mobs instead of clothing items
 */
// tg: code/datums/components/bloodysoles.dm:270
public partial class datum_component_bloodysoles_feet : datum_component_bloodysoles
{
    // tg: code/datums/components/bloodysoles.dm:274  var/static/mutable_appearance/bloody_feet (a type-level static, rules 10.6)
    private static ref mutable_appearance bloody_feet => ref TgState.Current.datum_component_bloodysoles_feet__bloody_feet;

    // tg: code/datums/components/bloodysoles.dm:279
    [DmParams("new_blood")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList new_blood = Dm.arg<DmList>(__extra, 0);
        if (!iscarbon(parent))
            return COMPONENT_INCOMPATIBLE;

        wielder = parent;

        if (bloody_feet == null)
            bloody_feet = (mutable_appearance)mutable_appearance(Dm.resource("icons/effects/blood.dmi"), "shoeblood", SHOES_LAYER);

        RegisterSignal(parent, COMSIG_COMPONENT_CLEAN_ACT, nameof(on_clean));
        RegisterSignal(parent, COMSIG_STEP_ON_BLOOD, nameof(on_step_blood));
        RegisterSignals(parent, Dm.list(COMSIG_MOB_EQUIPPED_ITEM, COMSIG_MOB_UNEQUIPPED_ITEM), nameof(shoecover));

        if (Dm.truthy(new_blood))
        {
            blood_DNA = new_blood;
            update_icon();
        }
        return null;
    }

    // tg: code/datums/components/bloodysoles.dm:296
    [DmParams("soles", "original", "new_blood")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        DmList new_blood = Dm.arg<DmList>(__extra, 0);
        if (!Dm.truthy(Dm.length(new_blood)))
            return;

        LAZYOR(ref blood_DNA, new_blood);
        update_icon();
    }

    // tg: code/datums/components/bloodysoles.dm:303
    public override object share_blood(obj_effect_decal_cleanable_blood pool)
    {
        object dot = base.share_blood(pool);
        if (!Dm.truthy(dot))
            return dot;
        LAZYOR(ref blood_DNA, GET_ATOM_BLOOD_DNA(pool));
        update_icon();
        return dot;
    }

    // tg: code/datums/components/bloodysoles.dm:310
    public override void update_icon()
    {
        mob_living_carbon carbon_wielder = wielder as mob_living_carbon;
        if (!ishuman(carbon_wielder) || HAS_TRAIT(carbon_wielder, TRAIT_NO_BLOOD_OVERLAY))
            return;

        carbon_wielder.remove_overlay(SHOES_LAYER);
        // DEVIATION(DV572): no bloody feet without both legs (tg's update_worn_shoes' own rule): the overlay hung in the air under a cut-off leg
        if (!Dm.truthy(total_bloodiness) || Dm.truthy(is_obscured()) || carbon_wielder.num_legs < 2)
        {
            Waiting.NoSleep(carbon_wielder.update_worn_shoes());
            return;
        }

        bloody_feet.color = carbon_wielder.get_blood_dna_color();
        carbon_wielder.overlays_standing[SHOES_LAYER] = bloody_feet;
        carbon_wielder.apply_overlay(SHOES_LAYER);
    }

    // tg: code/datums/components/bloodysoles.dm:323
    public override void add_parent_to_footprint(obj_effect_decal_cleanable_blood_footprints footprint)
    {
        if (!ishuman(wielder))
        {
            LAZYSET(ref footprint.species_types, "unknown", TRUE);
            return;
        }

        // Find any leg of our human and add that to the footprint, instead of the default which is to just add the human type
        foreach (obj_item_bodypart_leg affecting in Dm.each<obj_item_bodypart_leg>(((mob_living_carbon)wielder).get_bodyparts()))
        {
            if (!Dm.truthy(affecting.bodypart_disabled))
                LAZYSET(ref footprint.species_types, affecting.limb_id, TRUE);
        }
    }

    // tg: code/datums/components/bloodysoles.dm:333
    public override object is_under_feet_covered()
    {
        return (((mob)wielder).get_item_by_slot(ITEM_SLOT_FEET) != null) ? TRUE : FALSE;
    }

    // tg: code/datums/components/bloodysoles.dm:336
    public override void on_moved(datum source = null, atom old_loc = null, object Dir = null, object Forced = null)
    {
        if (((mob_living)wielder).num_legs >= 2)
            base.on_moved(source, old_loc, Dir, Forced);
    }

    // tg: code/datums/components/bloodysoles.dm:340
    public override void on_step_blood(datum source = null, obj_effect_decal_cleanable pool = null)
    {
        if (((mob_living)wielder).num_legs >= 2)
            base.on_step_blood(source, pool);
    }

    // tg: code/datums/components/bloodysoles.dm:344  (SIGNAL_HANDLER is dropped, rules 7.9)
    [SignalHandler]
    public virtual void shoecover(datum source = null, obj_item item = null)
    {
        if (Dm.truthy(Dm.bitand(item.body_parts_covered, FEET)) || Dm.truthy(Dm.bitand(item.flags_inv, HIDESHOES)))
            update_icon();
    }

    // tg: code/datums/components/bloodysoles.dm:349
    public override DmList get_blood_dna()
    {
        return blood_DNA;
    }
}

/**
 * Simplified version of the kind applied to carbons for simple/basic mobs, primarily robots
 */
// tg: code/datums/components/bloodysoles.dm:355
public partial class datum_component_bloodysoles_bot : datum_component_bloodysoles
{
    // tg: code/datums/components/bloodysoles.dm:359
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        wielder = parent;
        RegisterSignal((datum)wielder, COMSIG_STEP_ON_BLOOD, nameof(on_step_blood));
        return null;
    }

    // tg: code/datums/components/bloodysoles.dm:365
    public override object is_obscured()
    {
        return FALSE;
    }

    // tg: code/datums/components/bloodysoles.dm:368
    public override object is_under_feet_covered()
    {
        return FALSE;
    }

    // tg: code/datums/components/bloodysoles.dm:371
    public override void add_parent_to_footprint(obj_effect_decal_cleanable_blood_footprints footprint)
    {
        LAZYSET(ref footprint.species_types, "bot", TRUE);
    }

    // tg: code/datums/components/bloodysoles.dm:374
    public override void update_icon()
    {
        // Future idea: Bot blood overlays
        return;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/bloodysoles.dm:274  var/static/mutable_appearance/bloody_feet
    internal mutable_appearance datum_component_bloodysoles_feet__bloody_feet;
}
