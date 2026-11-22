// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/plant_genes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the gene base, the reagent genes, the trait base and the slippery skin. Gaps P2 (D298): the other traits (the
// smoke trait F1 left among them). The investigate and combat logs are comments (M10+); the battery's context tip is D264's.

// tg: code/modules/hydroponics/plant_genes.dm:3
public partial class datum_plant_gene : datum
{
    // tg: code/modules/hydroponics/plant_genes.dm:17
    public virtual object get_name()
    {
        return name;
    }

    // tg: code/modules/hydroponics/plant_genes.dm:27
    public virtual object can_add(obj_item_seeds our_seed = null)
    {
        return TRUE;
    }

    // tg: code/modules/hydroponics/plant_genes.dm:32
    public virtual object Copy()
    {
        datum_plant_gene new_gene = Dm.@new(type) as datum_plant_gene;
        new_gene.mutability_flags = mutability_flags;
        return new_gene;
    }

    // tg: code/modules/hydroponics/plant_genes.dm:42
    public virtual void on_new_seed(obj_item_seeds new_seed = null)
    {
        return; // Not implemented
    }

    // tg: code/modules/hydroponics/plant_genes.dm:51
    public virtual void on_removed(obj_item_seeds old_seed = null)
    {
        return; // Not implemented
    }
}

// tg: code/modules/hydroponics/plant_genes.dm:56
public partial class datum_plant_gene_reagent : datum_plant_gene
{
    // tg: code/modules/hydroponics/plant_genes.dm:63
    public override object get_name()
    {
        string formatted_name = null;
        if (!Dm.truthy(Dm.bitand(mutability_flags, PLANT_GENE_REMOVABLE)))
            formatted_name += "Fragile ";
        formatted_name += Dm.text("[] production []%", Dm.initial(reagent_id, "name"), rate * 100);
        return formatted_name;
    }

    /*
     * Set our reagent's ID and name to the passed reagent.
     *
     * new_reagent_id - typepath of the reagent we're setting this gene to
     */
    // tg: code/modules/hydroponics/plant_genes.dm:76
    public virtual void set_reagent(object new_reagent_id = null)
    {
        reagent_id = new_reagent_id as Type;

        // GLOB.chemical_reagents_list is not built (the holder's GAP, _globalvars/lists/reagents.cs): the reagent's name from its type
        name = Dm.str(Dm.initial(reagent_id, "name"));
    }

    // tg: code/modules/hydroponics/plant_genes.dm:83
    [DmParams("new_reagent_id", "new_reagent_rate")]
    public override async DmTask New(params object[] __extra)
    {
        object new_reagent_id = Dm.arg(__extra, 0);
        object new_reagent_rate = Dm.arg(__extra, 1) ?? 0.04f;
        await base.New(__extra);
        if (Dm.truthy(new_reagent_id))
        {
            set_reagent(new_reagent_id);
            rate = Dm.num(new_reagent_rate);
        }
    }

    // tg: code/modules/hydroponics/plant_genes.dm:89
    public override object Copy()
    {
        object dot = base.Copy();
        datum_plant_gene_reagent new_reagent_gene = dot as datum_plant_gene_reagent;
        new_reagent_gene.name = name;
        new_reagent_gene.reagent_id = reagent_id;
        new_reagent_gene.rate = rate;
        return dot;
    }

    // tg: code/modules/hydroponics/plant_genes.dm:97
    public override object can_add(obj_item_seeds our_seed = null)
    {
        if (!Dm.truthy(base.can_add(our_seed)))
            return FALSE;
        foreach (datum_plant_gene_reagent seed_reagent in Dm.each<datum_plant_gene_reagent>(our_seed.genes))
        {
            if (seed_reagent.reagent_id == reagent_id && seed_reagent.rate <= rate)
                return FALSE; // We can upgrade reagent genes if our rate is greater than the one already in the plant.
        }
        return TRUE;
    }

    /**
     * Intends to compare a reagent gene with a set of seeds, and if the seeds contain the same gene, with more production rate, upgrades the rate to the highest of the two.
     *
     * Called when plants are crossbreeding, this looks for two matching reagent_ids, where the rates are greater, in order to upgrade.
     */
    // tg: code/modules/hydroponics/plant_genes.dm:111
    public virtual object try_upgrade_gene(obj_item_seeds seed = null)
    {
        foreach (datum_plant_gene_reagent reagent in Dm.each<datum_plant_gene_reagent>(seed.genes))
        {
            if (reagent.reagent_id != reagent_id || reagent.rate <= rate)
                continue;
            rate = reagent.rate;
            return TRUE;
        }
        return FALSE;
    }
}

// tg: code/modules/hydroponics/plant_genes.dm:122
public partial class datum_plant_gene_trait : datum_plant_gene
{
    // tg: code/modules/hydroponics/plant_genes.dm:130
    public override object Copy()
    {
        object dot = base.Copy();
        datum_plant_gene_trait new_trait_gene = dot as datum_plant_gene_trait;
        new_trait_gene.rate = rate;
        return dot;
    }

    // tg: code/modules/hydroponics/plant_genes.dm:136
    public override object get_name() // Used for manipulator display and gene disk name.
    {
        string formatted_name = null;
        if (!Dm.truthy(Dm.bitand(mutability_flags, PLANT_GENE_REMOVABLE)))
        {
            if (!Dm.truthy(Dm.bitand(mutability_flags, PLANT_GENE_GRAFTABLE)))
                formatted_name += "Immutable ";
            else
                formatted_name += "Essential ";
        }
        formatted_name += name;
        return formatted_name;
    }

    /*
     * Checks if we can add the trait to the seed in question.
     *
     * source_seed - the seed genes we're adding the trait too
     */
    // tg: code/modules/hydroponics/plant_genes.dm:151
    public override object can_add(obj_item_seeds source_seed = null)
    {
        if (!Dm.truthy(base.can_add(source_seed)))
            return FALSE;

        if (Dm.truthy(is_type_in_list(source_seed, seed_blacklist)))
            return FALSE;

        foreach (datum_plant_gene_trait trait in Dm.each<datum_plant_gene_trait>(source_seed.genes))
        {
            if (Dm.truthy(Dm.bitand(trait_ids, trait.trait_ids)))
                return FALSE;
            if (type == trait.type)
                return FALSE;
        }

        return TRUE;
    }

    /*
     * on_new_plant is called for every plant trait on an /obj/item/grown or /obj/item/food/grown when initialized.
     *
     * our_plant - the source plant being created
     * newloc - the loc of the plant
     */
    // tg: code/modules/hydroponics/plant_genes.dm:173
    public virtual async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        // Plants should always have seeds, but if a plant gene is somehow being instantiated on a plant with no seed, stop initializing genes
        // (Plants hold their genes on their seeds, so we can't really add them to something that doesn't exist)
        if (our_plant.get_plant_seed() == null)
        {
            stack_trace(Dm.text("[] ([]) has a nulled seed value while trying to initialize []!", our_plant, our_plant.type, this), "code/modules/hydroponics/plant_genes.dm", 177);
            return FALSE;
        }

        // Add on any bonus lines on examine
        if (Dm.truthy(description) && Dm.truthy(Dm.bitand(trait_flags, TRAIT_SHOW_EXAMINE)))
            RegisterSignal(our_plant, COMSIG_ATOM_EXAMINE, nameof(examine));
        return TRUE;
    }

    // tg: code/modules/hydroponics/plant_genes.dm:191
    public virtual void on_plant_in_tray(obj_machinery_hydroponics tray = null, obj_item_seeds seed = null)
    {
        return;
    }

    // tg: code/modules/hydroponics/plant_genes.dm:201
    public virtual void on_unplanted_from_tray(obj_machinery_hydroponics tray = null, obj_item_seeds seed = null)
    {
        return;
    }

    // tg: code/modules/hydroponics/plant_genes.dm:205
    [SignalHandler]
    public virtual void examine(obj_item our_plant = null, mob examiner = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_info(Dm.text("[]", description)));
    }
}

// tg: code/modules/hydroponics/plant_genes.dm:271
public partial class datum_plant_gene_trait_slip : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:277
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        obj_item_food_grown grown_plant = our_plant as obj_item_food_grown;
        if (grown_plant != null && ispath(grown_plant.trash_type, typeof(obj_item_grown)))
            return dot;

        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        float stun_len = Dm.num(our_seed.potency) * rate;

        if (our_plant is not obj_item_grown_bananapeel && (our_plant.reagents == null || !Dm.truthy(((datum_reagents)our_plant.reagents).has_reagent(typeof(datum_reagent_lube)))))
            stun_len /= 3;

        await our_plant.AddComponent(typeof(datum_component_slippery), Dm.min(stun_len, 140), NONE, Dm.@new<datum_callback>(this, nameof(handle_slip), our_plant));
        return dot;
    }

    /// On slip, sends a signal that our plant was slipped on out.
    // tg: code/modules/hydroponics/plant_genes.dm:295
    public virtual void handle_slip(obj_item our_plant = null, mob slipped_target = null)
    {
        if (Dm.truthy(our_plant?._listen_lookup?[COMSIG_PLANT_ON_SLIP]))
            our_plant._SendSignal(COMSIG_PLANT_ON_SLIP, Dm.list(our_plant, slipped_target));
    }
}

/// Allows the plant to be squashed when thrown or slipped on, leaving a colored mess and trash type item behind.
// tg: code/modules/hydroponics/plant_genes.dm:212
public partial class datum_plant_gene_trait_squash : datum_plant_gene_trait
{
    // Register a signal that our plant can be squashed on add.
    // tg: code/modules/hydroponics/plant_genes.dm:219
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        RegisterSignal(our_plant, COMSIG_PLANT_ON_SLIP, nameof(squash_plant));
        RegisterSignal(our_plant, COMSIG_MOVABLE_IMPACT, nameof(squash_plant_if_not_caught));
        RegisterSignal(our_plant, COMSIG_ITEM_ATTACK_SELF, nameof(squash_plant));
        return dot;
    }

    /*
     * Signal proc to squash the plant this trait belongs to, causing a smudge, exposing the target to reagents, and deleting it,
     *
     * Arguments
     * our_plant - the plant this trait belongs to.
     * target - the atom being hit by this squashed plant.
     */
    // tg: code/modules/hydroponics/plant_genes.dm:235
    [SignalHandler]
    public virtual void squash_plant(object our_plant = null, atom target = null)
    {
        obj_item plant = our_plant as obj_item;
        turf our_turf = get_turf(target);
        Waiting.NoSleep(plant.forceMove(our_turf));
        if (plant is obj_item_food_grown grown)
        {
            if (ispath(grown.splat_type, typeof(obj_effect_decal_cleanable_food_plant_smudge)))
            {
                obj plant_smudge = Dm.@new(grown.splat_type, our_turf) as obj;
                plant_smudge.name = Dm.text("[] smudge", grown.name);
                if (Dm.truthy(grown.filling_color))
                    plant_smudge.color = grown.filling_color;
            }
            else if (grown.splat_type != null)
            {
                Dm.@new(grown.splat_type, our_turf);
            }
        }
        else
        {
            obj_effect_decal_cleanable_food_plant_smudge misc_smudge = Dm.@new<obj_effect_decal_cleanable_food_plant_smudge>(our_turf);
            misc_smudge.name = Dm.text("[] smudge", plant.name);
            misc_smudge.color = "#82b900";
        }

        plant.visible_message(span_warning(I18n.T("[] is squashed.", plant)), span_hear(I18n.T("You hear a smack.")));
        if (Dm.truthy(plant._listen_lookup?[COMSIG_PLANT_ON_SQUASH]))
            plant._SendSignal(COMSIG_PLANT_ON_SQUASH, Dm.list(plant, target));

        datum_reagents plant_reagents = plant.reagents as datum_reagents;
        if (plant_reagents != null)
            Waiting.NoSleep(plant_reagents.expose(our_turf));
        foreach (object things in Dm.each(our_turf))
        {
            if (plant_reagents != null)
                Waiting.NoSleep(plant_reagents.expose(things as atom));
        }

        qdel(plant);
    }

    // tg: code/modules/hydroponics/plant_genes.dm:262
    [SignalHandler]
    public virtual void squash_plant_if_not_caught(datum source = null, atom hit_atom = null, datum_thrownthing throwing_datum = null, object caught = null)
    {
        if (!Dm.truthy(caught))
            squash_plant(source, hit_atom);
    }
}

/*
 * Cell recharging trait. Charges all mob's power cells to (potency*rate)% mark when eaten.
 * Generates sparks on squash.
 * Small (potency * rate) chance to shock squish or slip target for (potency * rate) damage.
 * Also affects plant batteries see capatative cell production datum
 */
// tg: code/modules/hydroponics/plant_genes.dm:312
public partial class datum_plant_gene_trait_cell_charge : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:319
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        if (our_seed.get_gene(typeof(datum_plant_gene_trait_squash)) != null)
            // If we have the squash gene, let that handle slipping
            RegisterSignal(our_plant, COMSIG_PLANT_ON_SQUASH, nameof(zap_target));
        else
            RegisterSignal(our_plant, COMSIG_PLANT_ON_SLIP, nameof(zap_target));

        RegisterSignal(our_plant, COMSIG_FOOD_EATEN, nameof(recharge_cells));
        return dot;
    }

    /*
     * Zaps the target with a stunning shock.
     *
     * our_plant - our source plant, shocking the target
     * target - the atom being zapped by our plant
     */
    // tg: code/modules/hydroponics/plant_genes.dm:339
    [SignalHandler]
    public virtual void zap_target(obj_item our_plant = null, atom target = null)
    {
        if (!iscarbon(target))
            return;

        our_plant.investigate_log(Dm.text("zapped [] at []. Last touched by: [].", key_name(target), AREACOORD(target), our_plant.fingerprintslast), INVESTIGATE_BOTANY);
        mob_living_carbon target_carbon = target as mob_living_carbon;
        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        float power = Dm.num(our_seed.potency) * rate;
        if (prob(power))
            Waiting.NoSleep(target_carbon.electrocute_act(Dm.round(power), our_plant, 1, SHOCK_NOGLOVES));
    }

    /*
     * Recharges every cell the person is holding for a bit based on plant potency.
     *
     * our_plant - our source plant, that we consumed to charge the cells
     * eater - the mob that bit the plant
     * feeder - the mob that feed the eater the plant
     */
    // tg: code/modules/hydroponics/plant_genes.dm:359
    [SignalHandler]
    public virtual void recharge_cells(obj_item our_plant = null, mob_living eater = null, mob feeder = null)
    {
        to_chat(eater, span_notice(I18n.T("You feel energized as you bite into [].", our_plant)));
        bool batteries_recharged = false;
        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        foreach (obj_item_stock_parts_power_store found_cell in Dm.each<obj_item_stock_parts_power_store>(assoc_to_values(eater.get_all_cells() as DmList)))
        {
            float newcharge = Dm.min(Dm.num(our_seed.potency) * 0.01f * Dm.num(found_cell.maxcharge), Dm.num(found_cell.maxcharge));
            if (Dm.num(found_cell.charge) < newcharge)
            {
                found_cell.charge = newcharge;
                if (found_cell.loc is obj cell_location)
                    cell_location.update_appearance(); //update power meters and such
                found_cell.update_appearance();
                batteries_recharged = true;
            }
        }
        if (batteries_recharged)
            to_chat(eater, span_notice(I18n.T("Your batteries are recharged!")));
    }
}

/*
 * Makes the plant glow. Makes the plant in tray glow, too.
 * Adds (1.4 + potency * rate) light range and (potency * (rate + 0.01)) light_power to products.
 */
// tg: code/modules/hydroponics/plant_genes.dm:381
public partial class datum_plant_gene_trait_glow : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:392
    public virtual object glow_range(obj_item_seeds seed = null)
    {
        return 1.4f + Dm.num(seed.potency) * rate;
    }

    // tg: code/modules/hydroponics/plant_genes.dm:395
    public virtual object glow_power(obj_item_seeds seed = null)
    {
        return Dm.max(Dm.num(seed.potency) * (rate + 0.01f), 0.1f);
    }

    // tg: code/modules/hydroponics/plant_genes.dm:398
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        our_plant.light_system = OVERLAY_LIGHT;
        await our_plant.AddComponent(typeof(datum_component_overlay_lighting), glow_range(our_seed), glow_power(our_seed), glow_color);
        return dot;
    }
}

/*
 * Makes plant emit darkness. (Purple-ish shadows)
 * Adds - (potency * (rate * 0.2)) light power to products.
 */
// tg: code/modules/hydroponics/plant_genes.dm:411
public partial class datum_plant_gene_trait_glow_shadow : datum_plant_gene_trait_glow
{
    // tg: code/modules/hydroponics/plant_genes.dm:417
    public override object glow_power(obj_item_seeds seed = null)
    {
        return -Dm.max(Dm.num(seed.potency) * (rate * 0.2f), 0.2f);
    }
}

/*
 * Makes plant teleport people when squashed or slipped on.
 * Teleport radius is roughly potency / 10.
 */
// tg: code/modules/hydroponics/plant_genes.dm:461
public partial class datum_plant_gene_trait_teleport : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:468
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        if (our_seed.get_gene(typeof(datum_plant_gene_trait_squash)) != null)
            // If we have the squash gene, let that handle slipping
            RegisterSignal(our_plant, COMSIG_PLANT_ON_SQUASH, nameof(squash_teleport));
        else
            RegisterSignal(our_plant, COMSIG_PLANT_ON_SLIP, nameof(slip_teleport));
        return dot;
    }

    /*
     * When squashed, makes the target teleport.
     *
     * our_plant - our plant, being squashed, and teleporting the target
     * target - the atom targeted by the squash
     */
    // tg: code/modules/hydroponics/plant_genes.dm:486
    [SignalHandler]
    public virtual void squash_teleport(obj_item our_plant = null, atom target = null)
    {
        if (!isliving(target))
            return;

        our_plant.investigate_log(Dm.text("squash-teleported [] at []. Last touched by: [].", key_name(target), AREACOORD(target), our_plant.fingerprintslast), INVESTIGATE_BOTANY);
        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        float teleport_radius = Dm.max(Dm.round(Dm.num(our_seed.potency) / 10), 1);
        turf T = get_turf(target);
        Dm.@new<obj_effect_decal_cleanable_molten_object>(T); //Leave a pile of goo behind for dramatic effect...
        Waiting.NoSleep(do_teleport(target as atom_movable, T, teleport_radius, channel: TELEPORT_CHANNEL_BLUESPACE));
    }

    /*
     * When slipped on, makes the target teleport and either teleport the source again or delete it.
     *
     * our_plant - our plant being slipped on
     * target - the carbon targeted that was slipped and was teleported
     */
    // tg: code/modules/hydroponics/plant_genes.dm:505
    [SignalHandler]
    public virtual void slip_teleport(obj_item our_plant = null, mob_living_carbon target = null)
    {
        our_plant.investigate_log(Dm.text("slip-teleported [] at []. Last touched by: [].", key_name(target), AREACOORD(target), our_plant.fingerprintslast), INVESTIGATE_BOTANY);
        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        float teleport_radius = Dm.max(Dm.round(Dm.num(our_seed.potency) / 10), 1);
        turf T = get_turf(target);
        to_chat(target, span_warning(I18n.T("You slip through spacetime!")));
        Waiting.NoSleep(do_teleport(target, T, teleport_radius, channel: TELEPORT_CHANNEL_BLUESPACE));
        if (prob(50))
        {
            Waiting.NoSleep(do_teleport(our_plant, T, teleport_radius, channel: TELEPORT_CHANNEL_BLUESPACE));
        }
        else
        {
            Dm.@new<obj_effect_decal_cleanable_molten_object>(T); //Leave a pile of goo behind for dramatic effect...
            qdel(our_plant);
        }
    }
}

/**
 * A plant trait that causes the plant's capacity to double.
 *
 * When harvested, the plant's individual capacity is set to double its default.
 * However, the plant's maximum yield is also halved, only up to 5.
 */
// tg: code/modules/hydroponics/plant_genes.dm:526
public partial class datum_plant_gene_trait_maxchem : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:534
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        if (our_plant is obj_item_food_grown grown_plant)
            //Grown foods use the edible component so we need to change their max_volume var
            grown_plant.max_volume = Dm.num(grown_plant.max_volume) * rate;
        else if (our_plant.reagents is datum_reagents holder)
            //Grown inedibles however just use a reagents holder, so.
            holder.maximum_volume = Dm.num(holder.maximum_volume) * rate;
        return dot;
    }
}

/*
 * Allows a plant to be turned into a battery when cabling is applied.
 * 100 potency plants are made into 2 mj batteries.
 * Plants with electrical activity has their capacities massively increased (up to 40 mj at 100 potency)
 */
// tg: code/modules/hydroponics/plant_genes.dm:563
public partial class datum_plant_gene_trait_battery : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:572
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        our_plant.flags_1 = Dm.bitor(Dm.num(our_plant.flags_1), HAS_CONTEXTUAL_SCREENTIPS_1);
        RegisterSignal(our_plant, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_requesting_context_from_item));
        RegisterSignal(our_plant, COMSIG_ATOM_ATTACKBY, nameof(make_battery));
        return dot;
    }

    /*
     * Signal proc for [COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM] to add context to plant batteries.
     */
    // tg: code/modules/hydroponics/plant_genes.dm:584
    [SignalHandler]
    public virtual object on_requesting_context_from_item(obj_item source = null, DmList context = null, obj_item held_item = null, mob_living user = null)
    {
        if (held_item is not obj_item_stack_cable_coil cabling)
            return NONE;

        if (Dm.num(cabling.amount) < cables_needed_per_battery)
            return NONE;

        context[SCREENTIP_CONTEXT_LMB] = Dm.text("Make [] battery", source.name);
        return CONTEXTUAL_SCREENTIP_SET;
    }

    /*
     * When a plant with this gene is hit (attackby) with cables, we turn it into a battery.
     *
     * our_plant - our plant being hit
     * hit_item - the item we're hitting the plant with
     * user - the person hitting the plant with an item
     */
    // tg: code/modules/hydroponics/plant_genes.dm:609
    [SignalHandler]
    public virtual void make_battery(obj_item our_plant = null, obj_item hit_item = null, mob user = null)
    {
        if (hit_item is not obj_item_stack_cable_coil cabling)
            return;

        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        if (!Dm.truthy(Waiting.NoSleep(cabling.use(cables_needed_per_battery))))
        {
            to_chat(user, span_warning(I18n.T("You need five lengths of cable to make a [] battery!", our_plant)));
            return;
        }

        to_chat(user, span_notice(I18n.T("You add some cable to [] and slide it inside the battery encasing.", our_plant)));
        obj_item_stock_parts_power_store_cell_potato pocell = Dm.@new<obj_item_stock_parts_power_store_cell_potato>(user.loc);
        pocell.icon = our_plant.icon; // Just in case the plant icons get spread out in different files eventually, this trait won't cause error sprites (also yay downstreams)
        pocell.icon_state = our_plant.icon_state;
        pocell.maxcharge = Dm.num(our_seed.potency) * 0.02f * STANDARD_CELL_CHARGE;

        // The secret of potato supercells!
        datum_plant_gene_trait_cell_charge electrical_gene = our_seed.get_gene(typeof(datum_plant_gene_trait_cell_charge)) as datum_plant_gene_trait_cell_charge;
        if (electrical_gene != null) // Cell charge max is now 40MJ or otherwise known as 400KJ (Same as bluespace power cells)
            pocell.maxcharge = Dm.num(pocell.maxcharge) * (electrical_gene.rate * 100);

        pocell.charge = pocell.maxcharge;
        pocell.name = Dm.text("[] battery", our_plant.name);
        pocell.desc = Dm.text("A rechargeable plant-based power cell. This one has a rating of [], and you should not swallow it.", display_energy(pocell.maxcharge));
        qdel(our_plant);
    }
}

/*
 * Injects a number of chemicals from the plant when you throw it at someone or they slip on it.
 * At 0 potency it can inject 1 unit of its chemicals, while at 100 potency it can inject 20 units.
 */
// tg: code/modules/hydroponics/plant_genes.dm:641
public partial class datum_plant_gene_trait_stinging : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:649
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        RegisterSignal(our_plant, COMSIG_PLANT_ON_SLIP, nameof(prickles_inject));
        RegisterSignal(our_plant, COMSIG_MOVABLE_IMPACT, nameof(prickles_inject));
        return dot;
    }

    /*
     * Injects a target with a number of reagents from our plant.
     *
     * our_plant - our plant that's injecting someone
     * target - the atom being hit on thrown or slipping on our plant
     */
    // tg: code/modules/hydroponics/plant_genes.dm:663
    [SignalHandler]
    public virtual void prickles_inject(obj_item our_plant = null, atom target = null)
    {
        datum_reagents plant_reagents = our_plant.reagents as datum_reagents;
        if (!isliving(target) || !Dm.truthy(plant_reagents?.total_volume))
            return;

        mob_living living_target = target as mob_living;
        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        if (living_target.reagents != null && Dm.truthy(living_target.can_inject()))
        {
            float injecting_amount = Dm.max(1, Dm.num(our_seed.potency) * 0.2f); // Minimum of 1, max of 20
            Waiting.NoSleep(plant_reagents.trans_to(living_target, injecting_amount, methods: INJECT));
            to_chat(target, span_danger(I18n.T("You are pricked by []!", our_plant)));
            log_combat(our_plant, living_target, Dm.text("pricked and attempted to inject reagents from [] to []. Last touched by: [].", our_plant, living_target, our_plant.fingerprintslast));
            our_plant.investigate_log(Dm.text("pricked and injected [] and injected [] reagents at []. Last touched by: [].", key_name(living_target), injecting_amount, AREACOORD(living_target), our_plant.fingerprintslast), INVESTIGATE_BOTANY);
        }
    }
}

/// Explodes into reagent-filled smoke when squashed.
// tg: code/modules/hydroponics/plant_genes.dm:679
public partial class datum_plant_gene_trait_smoke : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:685
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        RegisterSignal(our_plant, COMSIG_PLANT_ON_SQUASH, nameof(make_smoke));
        return dot;
    }

    /*
     * Makes a cloud of reagent smoke.
     *
     * our_plant - our plant being squashed and smoked
     * target - the atom the plant was squashed on
     */
    // tg: code/modules/hydroponics/plant_genes.dm:698
    [SignalHandler]
    public virtual void make_smoke(obj_item our_plant = null, atom target = null)
    {
        our_plant.investigate_log(Dm.text("made smoke at []. Last touched by: [].", AREACOORD(target), our_plant.fingerprintslast), INVESTIGATE_BOTANY);
        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        turf splat_location = get_turf(target);
        float range = Dm.sqrt(Dm.num(our_seed.potency) * 0.1f);
        Waiting.NoSleep(do_chem_smoke(Dm.round(range), our_plant, splat_location, carry: our_plant.reagents as datum_reagents, silent: FALSE, log: TRUE));
        Waiting.NoSleep(((datum_reagents)our_plant.reagents).clear_reagents());
    }
}

/// Makes the plant and its seeds fireproof. From lavaland plants.
// tg: code/modules/hydroponics/plant_genes.dm:709
public partial class datum_plant_gene_trait_fire_resistance : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:715
    public override void on_new_seed(obj_item_seeds new_seed = null)
    {
        if (!Dm.truthy(Dm.bitand(new_seed.resistance_flags, FIRE_PROOF)))
            new_seed.resistance_flags = Dm.bitor(new_seed.resistance_flags, FIRE_PROOF);
    }

    // tg: code/modules/hydroponics/plant_genes.dm:719
    public override void on_removed(obj_item_seeds old_seed = null)
    {
        if (Dm.truthy(Dm.bitand(old_seed.resistance_flags, FIRE_PROOF)))
            old_seed.resistance_flags = Dm.bitand(old_seed.resistance_flags, ~FIRE_PROOF);
    }

    // tg: code/modules/hydroponics/plant_genes.dm:723
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(Dm.bitand(our_plant.resistance_flags, FIRE_PROOF)))
            our_plant.resistance_flags = Dm.bitor(our_plant.resistance_flags, FIRE_PROOF);
        return dot;
    }
}

/// Invasive spreading lets the plant jump to other trays, and the spreading plant won't replace plants of the same type.
// tg: code/modules/hydroponics/plant_genes.dm:732
public partial class datum_plant_gene_trait_invasive : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:738
    public override void on_new_seed(obj_item_seeds new_seed = null)
    {
        RegisterSignal(new_seed, COMSIG_SEED_ON_GROW, nameof(try_spread));
    }

    // tg: code/modules/hydroponics/plant_genes.dm:741
    public override void on_removed(obj_item_seeds old_seed = null)
    {
        UnregisterSignal(old_seed, COMSIG_SEED_ON_GROW);
    }

    /*
     * Attempt to find an adjacent tray we can spread to.
     *
     * our_seed - our plant's seed, what spreads to other trays
     * our_tray - the hydroponics tray we're currently in
     */
    // tg: code/modules/hydroponics/plant_genes.dm:750
    [SignalHandler]
    public virtual void try_spread(obj_item_seeds our_seed = null, obj_machinery_hydroponics our_tray = null)
    {
        if (prob(100 - (5 * (11 - Dm.num(our_seed.production)))))
            return;

        foreach (object step_dir in Dm.each(GLOB.alldirs))
        {
            obj_machinery_hydroponics spread_tray = Dm.locate<obj_machinery_hydroponics>(get_step(our_tray, Dm.num(step_dir)));
            if (spread_tray != null && prob(15))
            {
                if (!Dm.truthy(Waiting.NoSleep(our_tray.Adjacent(spread_tray))))
                    continue; //Don't spread through things we can't go through.

                Waiting.Detach(spread_seed(spread_tray, our_tray));
            }
        }
    }

    /*
     * Actually spread the plant to the tray we found in try_spread.
     *
     * target_tray - the tray we're spreading to
     * origin_tray - the tray we're currently in
     */
    // tg: code/modules/hydroponics/plant_genes.dm:770
    public virtual async DmTask<object> spread_seed(obj_machinery_hydroponics target_tray = null, obj_machinery_hydroponics origin_tray = null)
    {
        if (target_tray.myseed != null) // Check if there's another seed in the next tray.
        {
            if (target_tray.myseed.type == origin_tray.myseed.type && target_tray.plant_status != HYDROTRAY_PLANT_DEAD)
                return FALSE; // It should not destroy its own kind.
            target_tray.visible_message(span_warning(I18n.T("The [] is overtaken by []!", target_tray.myseed.plantname, origin_tray.myseed.plantname)));
            qdel(target_tray.myseed);
            target_tray.myseed = null;
        }
        await target_tray.set_seed(origin_tray.myseed.Copy() as obj_item_seeds);
        target_tray.age = 0;
        target_tray.set_plant_health(target_tray.myseed.endurance);
        target_tray.lastcycle = world.time;
        target_tray.set_weedlevel(0, update_icon: FALSE); // Reset
        target_tray.set_pestlevel(0); // Reset
        target_tray.visible_message(span_warning(I18n.T("The [] spreads!", origin_tray.myseed.plantname)));
        if (target_tray.myseed != null)
            target_tray.name = Dm.text("[] ([])", Dm.initial(target_tray, (atom a) => a.name), target_tray.myseed.plantname);
        else
            target_tray.name = Dm.initial(target_tray, (atom a) => a.name);

        return TRUE;
    }
}

/**
 * A plant trait that causes the plant's food reagents to ferment instead.
 *
 * In practice, it replaces the plant's nutriment and vitamins with half as much of its fermented reagent.
 * This exception is executed in seeds.dm under 'prepare_result'.
 *
 * Incompatible with auto-juicing composition.
 */
// tg: code/modules/hydroponics/plant_genes.dm:798
public partial class datum_plant_gene_trait_brewing : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:805
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        RegisterSignal(our_plant, COMSIG_PLANT_ON_HARVEST, nameof(brewing_harvest));
        return dot;
    }

    // tg: code/modules/hydroponics/plant_genes.dm:812
    [SignalHandler]
    public virtual void brewing_harvest(obj_item our_plant = null, obj_item_seeds our_seed = null)
    {
        if (our_plant is not obj_item_food_grown grown_plant)
            return;

        Waiting.NoSleep(grown_plant.ferment());
    }
}

/**
 * Similar to auto-distilling, but instead of brewing the plant's contents it juices it.
 *
 * Incompatible with auto-distilling composition.
 */
// tg: code/modules/hydroponics/plant_genes.dm:826
public partial class datum_plant_gene_trait_juicing : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:833
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        RegisterSignal(our_plant, COMSIG_PLANT_ON_HARVEST, nameof(juicing_harvest));
        return dot;
    }

    // tg: code/modules/hydroponics/plant_genes.dm:840
    [SignalHandler]
    public virtual void juicing_harvest(obj_item our_plant = null, obj_item_seeds our_seed = null)
    {
        // FALSE is used to differentiate from null
        // (the port's juicer slot is typed atom/movable, so FALSE cannot be passed: null makes juice default to the plant's loc,
        // which at harvest is a turf that juice's istype(juicer) refuses, as tg's FALSE does)
        Waiting.NoSleep(our_plant.juice(juicer: null));
    }
}

/**
 * Plays a laughter sound when someone slips on it.
 * Like the sitcom component but for plants.
 * Just like slippery skin, if we have a trash type this only functions on that. (Banana peels)
 */
// tg: code/modules/hydroponics/plant_genes.dm:850
public partial class datum_plant_gene_trait_plant_laughter : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:859
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        if (our_plant is obj_item_food_grown grown_plant && ispath(grown_plant.trash_type, typeof(obj_item_grown)))
            return dot;

        RegisterSignal(our_plant, COMSIG_PLANT_ON_SLIP, nameof(laughter));
        return dot;
    }

    /*
     * Play a sound effect from our plant.
     *
     * our_plant - the source plant that was slipped on
     * target - the atom that slipped on the plant
     */
    // tg: code/modules/hydroponics/plant_genes.dm:876
    [SignalHandler]
    public virtual void laughter(obj_item our_plant = null, atom target = null)
    {
        our_plant.audible_message(span_notice(I18n.T("[] lets out burst of laughter.", our_plant)));
        playsound(our_plant, pick(sounds), 100, FALSE, SHORT_RANGE_SOUND_EXTRARANGE);
    }
}

/**
 * A plant trait that causes the plant to gain aesthetic googly eyes.
 *
 * Has no functional purpose outside of causing japes, adds eyes over the plant's sprite, which are adjusted for size by potency.
 */
// tg: code/modules/hydroponics/plant_genes.dm:885
public partial class datum_plant_gene_trait_eyes : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:896
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        googly = mutable_appearance(Dm.resource("icons/obj/service/hydroponics/harvest.dmi"), "eyes", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
        our_plant.add_overlay(googly);
        return dot;
    }
}

/// Makes the plant embed on thrown impact.
// tg: code/modules/hydroponics/plant_genes.dm:904
public partial class datum_plant_gene_trait_sticky : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:913
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        obj_item_seeds our_seed = our_plant.get_plant_seed() as obj_item_seeds;
        our_plant.throwforce = Dm.num(our_seed.potency) / 20;
        datum_embedding plant_embed = our_plant.get_embed() as datum_embedding;
        if (plant_embed == null)
        {
            if (our_seed.get_gene(typeof(datum_plant_gene_trait_stinging)) != null)
                our_plant.set_embed(typeof(datum_embedding_spiky_plant));
            else
                our_plant.set_embed(typeof(datum_embedding_sticky_plant));
            return dot;
        }

        plant_embed.ignore_throwspeed_threshold = TRUE;
        if (our_seed.get_gene(typeof(datum_plant_gene_trait_stinging)) != null)
            return dot;

        plant_embed.pain_mult = 0;
        plant_embed.jostle_pain_mult = 0;
        return dot;
    }
}

/**
 * This trait automatically heats up the plant's chemical contents when harvested.
 * This requires nutriment to fuel. 1u nutriment = 25 K.
 */
// tg: code/modules/hydroponics/plant_genes.dm:946
public partial class datum_plant_gene_trait_chem_heating : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:955
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        RegisterSignal(our_plant, COMSIG_PLANT_ON_HARVEST, nameof(chem_heating_harvest));
        return dot;
    }

    // tg: code/modules/hydroponics/plant_genes.dm:962
    [SignalHandler]
    public virtual void chem_heating_harvest(obj_item our_plant = null, obj_item_seeds our_seed = null)
    {
        datum_reagents holder = our_plant.reagents as datum_reagents;
        float num_nutriment = Dm.num(holder.get_reagent_amount(typeof(datum_reagent_consumable_nutriment)));
        our_plant.visible_message(span_notice(I18n.T("[] releases freezing air, consuming its nutriments to heat its contents.", our_plant)));
        Waiting.NoSleep(holder.remove_reagent(typeof(datum_reagent_consumable_nutriment), num_nutriment));
        holder.chem_temp = Dm.min(1000, (Dm.num(holder.chem_temp) + num_nutriment * 25));
        Waiting.NoSleep(holder.handle_reactions());
        playsound(our_plant, Dm.resource("sound/effects/wounds/sizzle2.ogg"), 5);
    }
}

/**
 * This trait is the opposite of above - it cools down the plant's chemical contents on harvest.
 * This requires nutriment to fuel. 1u nutriment = -5 K.
 */
// tg: code/modules/hydroponics/plant_genes.dm:975
public partial class datum_plant_gene_trait_chem_cooling : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:984
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        RegisterSignal(our_plant, COMSIG_PLANT_ON_HARVEST, nameof(chem_cooling_harvest));
        return dot;
    }

    // tg: code/modules/hydroponics/plant_genes.dm:991
    [SignalHandler]
    public virtual void chem_cooling_harvest(obj_item our_plant = null, obj_item_seeds our_seed = null)
    {
        datum_reagents holder = our_plant.reagents as datum_reagents;
        float num_nutriment = Dm.num(holder.get_reagent_amount(typeof(datum_reagent_consumable_nutriment)));
        our_plant.visible_message(span_notice(I18n.T("[] releases a blast of hot air, consuming its nutriments to cool its contents.", our_plant)));
        Waiting.NoSleep(holder.remove_reagent(typeof(datum_reagent_consumable_nutriment), num_nutriment));
        holder.chem_temp = Dm.max(3, (Dm.num(holder.chem_temp) + num_nutriment * -5));
        Waiting.NoSleep(holder.handle_reactions());
        playsound(our_plant, Dm.resource("sound/effects/space_wind.ogg"), 50);
    }
}

/// Traits for flowers, makes plants not decompose.
// tg: code/modules/hydroponics/plant_genes.dm:1014
public partial class datum_plant_gene_trait_preserved : datum_plant_gene_trait
{
    // tg: code/modules/hydroponics/plant_genes.dm:1020
    public override async DmTask<object> on_new_plant(obj_item our_plant = null, object newloc = null)
    {
        object dot = await base.on_new_plant(our_plant, newloc);
        if (!Dm.truthy(dot))
            return dot;

        if (our_plant is obj_item_food_grown grown_plant)
            grown_plant.preserved_food = TRUE;
        return dot;
    }
}
