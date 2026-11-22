// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/scanners/plant_analyzer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J3 (D298): the analyzer as a scanner of plant people's health and chemicals. Gaps P2 (D298): the tray, seed, plant and graft
// scans with the PlantAnalyzer window. The context tips stay a row (D264). The var blocks are generated.

// tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:5
public partial class obj_item_plant_analyzer : obj_item
{
    private const int PLANT_ANALYZER_STAT_TAB = 1;
    private const int PLANT_ANALYZER_CHEM_TAB = 2;

    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // register_item_context(): the context tips (screentips, not now)
        return dot;
    }

    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:33
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    /// When we use the analyzer in hand - try to show the results of the last scan
    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:68
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(user) || !Dm.truthy(user.can_read(this)) || Dm.truthy(user.is_blind()))
            return null;
        if (last_scan_data != null)
            return await base.interact(user, __extra);
        return null;
    }

    /// When we attack something, try to scan something we hit with left click. Left-clicking uses scans for stats
    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:75
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (isliving(interacting_with))
        {
            playsound(this, SFX_INDUSTRIAL_SCAN, 20, TRUE, -2, TRUE, FALSE);
            mob_living living_target = (mob_living)interacting_with;
            if (Dm.truthy(Dm.bitand(living_target.mob_biotypes, MOB_PLANT)))
            {
                await plant_biotype_health_scan(living_target, user as mob_living_carbon_human);
                return ITEM_INTERACT_SUCCESS;
            }
            return ITEM_INTERACT_BLOCKING;
        }

        shown_tab = PLANT_ANALYZER_STAT_TAB;
        return await analyze(user, interacting_with);
    }

    /// Same as above, but with right click. Right-clicking scans for chemicals.
    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:87
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await do_plant_chem_scan(interacting_with, user);
    }

    /*
     * Scan the target on chemical scan mode. This prints chemical genes and reagents to the user.
     *
     * scan_target - the atom we're scanning
     * user - the user doing the scanning.
     *
     * returns FALSE if it's not an object or item that does something when we scan it.
     * returns TRUE if we can scan the object, and outputs the message to the USER.
     */
    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:96
    public virtual async DmTask<object> do_plant_chem_scan(atom scan_target = null, mob user = null)
    {
        if (isliving(scan_target))
        {
            mob_living living_target = (mob_living)scan_target;
            if (Dm.truthy(Dm.bitand(living_target.mob_biotypes, MOB_PLANT)))
            {
                await plant_biotype_chem_scan(living_target, user as mob_living_carbon_human);
                return ITEM_INTERACT_SUCCESS;
            }
            return ITEM_INTERACT_BLOCKING;
        }

        shown_tab = PLANT_ANALYZER_CHEM_TAB;
        return await analyze(user, scan_target);
    }

    /*
     * Scan a living mob's (with MOB_PLANT biotype) health with the plant analyzer. No wound scanning, though.
     *
     * scanned_mob - the living mob being scanned
     * user - the person doing the scanning
     */
    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:113
    public virtual async DmTask plant_biotype_health_scan(mob_living scanned_mob = null, mob_living_carbon_human user = null)
    {
        user.visible_message(
            span_notice(I18n.T("[] analyzes []'s vitals.", user, scanned_mob)),
            span_notice(I18n.T("You analyze []'s vitals.", scanned_mob)));

        healthscan(user, scanned_mob, scanpower: SCANPOWER_ADVANCED);
        add_fingerprint(user);
    }

    /*
     * Scan a living mob's (with MOB_PLANT biotype) chemical contents with the plant analyzer.
     *
     * scanned_mob - the living mob being scanned
     * user - the person doing the scanning
     */
    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:128
    public virtual async DmTask plant_biotype_chem_scan(mob_living scanned_mob = null, mob_living_carbon_human user = null)
    {
        user.visible_message(
            span_notice(I18n.T("[] analyzes []'s bloodstream.", user, scanned_mob)),
            span_notice(I18n.T("You analyze []'s bloodstream.", scanned_mob)));
        chemscan(user, scanned_mob);
        add_fingerprint(user);
    }

    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:136
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "PlantAnalyzer", "Plant Analyzer");
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:142
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action as string)
        {
            case "setTab":
                object tab = @params?["tab"];
                if (Dm.eq(tab, PLANT_ANALYZER_STAT_TAB) || Dm.eq(tab, PLANT_ANALYZER_CHEM_TAB))
                    shown_tab = Dm.num(tab);
                return TRUE;
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:153
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["active_tab"] = shown_tab;
        Dm.combine(ref data, last_scan_data);
        return data;
    }

    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:159
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["cycle_seconds"] = HYDROTRAY_CYCLE_DELAY / 10;
        DmList trait_db = Dm.list();
        foreach (datum_plant_gene trait in Dm.each_cast<datum_plant_gene>(GLOB.plant_traits))
        {
            DmList trait_data = Dm.list(Dm.list(
                Dm.kv("path", trait.type),
                Dm.kv("name", trait.get_name()),
                Dm.kv("icon", trait.icon),
                Dm.kv("description", trait.description)));
            Dm.append(ref trait_db, trait_data);
        }
        data["trait_db"] = trait_db;
        return data;
    }

    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:173
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body()
    {
        atom real_last_tray_scanned = last_tray_scanned?.resolve() as atom;
        if (QDELETED(real_last_tray_scanned))
            return PROCESS_KILL;

        if (Dm.truthy(await loc.Adjacent(real_last_tray_scanned)))
            await analyze(null, real_last_tray_scanned);
        return null;
    }

    /// Called when our analyzer is used on something
    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:182
    public virtual async DmTask<object> analyze(mob user = null, atom target = null)
    {
        obj_item_graft graft = null;
        obj_item_seeds seed = null;
        obj_machinery_hydroponics tray = null;

        if (user != null)
            playsound(this, SFX_INDUSTRIAL_SCAN, 20, TRUE, -2, TRUE, FALSE);

        if (target is obj_machinery_hydroponics target_tray)
        {
            tray = target_tray;
            seed = tray.myseed;
        }
        else if (target is obj_structure_glowshroom shroom_plant)
        {
            seed = shroom_plant.myseed as obj_item_seeds;
        }
        else if (target is obj_item_graft target_graft)
        {
            graft = target_graft;
        }
        else if (target is obj_item scanned_object)
        {
            if (scanned_object is obj_item_seeds scanned_seed)
                seed = scanned_seed;
            else if (scanned_object.get_plant_seed() != null)
                seed = scanned_object.get_plant_seed() as obj_item_seeds;
        }

        if (seed == null && tray == null && graft == null)
            return NONE;

        if (user != null)
        {
            if (!Dm.truthy(user.can_read(this)))
                return ITEM_INTERACT_BLOCKING;
            START_PROCESSING(SSobj, this);
            last_tray_scanned = WEAKREF(tray) as datum_weakref; // sets it to null if no tray
        }

        last_scan_data = Dm.list(
            Dm.kv("tray_data", null),
            Dm.kv("seed_data", null),
            Dm.kv("plant_data", null),
            Dm.kv("graft_data", null));

        if (tray != null)
        {
            turf tray_turf = get_turf(tray);
            datum_reagents tray_reagents = tray.reagents as datum_reagents;
            DmList tray_data = Dm.list(
                Dm.kv("plant_health", tray.plant_health),
                Dm.kv("plant_age", tray.age),
                Dm.kv("is_dead", tray.plant_status == HYDROTRAY_PLANT_DEAD ? TRUE : FALSE),
                Dm.kv("name", tray.name),
                Dm.kv("icon", tray.icon),
                Dm.kv("icon_state", tray.icon_state),
                Dm.kv("water", tray.waterlevel),
                Dm.kv("water_max", tray.maxwater),
                Dm.kv("nutri", tray_reagents.total_volume),
                Dm.kv("nutri_max", tray.maxnutri),
                Dm.kv("yield_mod", tray.yieldmod),
                Dm.kv("being_pollinated", tray.being_pollinated),
                Dm.kv("self_sustaining", tray.self_sustaining),
                Dm.kv("light_level", Dm.truthy(tray_turf?.get_lumcount()) ? tray_turf.get_lumcount() : 0),
                Dm.kv("weeds", tray.weedlevel),
                Dm.kv("weeds_max", MAX_TRAY_WEEDS),
                Dm.kv("pests", tray.pestlevel),
                Dm.kv("pests_max", MAX_TRAY_PESTS),
                Dm.kv("toxins", tray.toxic),
                Dm.kv("toxins_max", MAX_TRAY_TOXINS),
                Dm.kv("reagents", Dm.list()));
            DmList tray_reagent_list = tray_data["reagents"] as DmList;
            foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(tray_reagents.reagent_list))
            {
                Dm.append(ref tray_reagent_list, Dm.list(Dm.list(
                    Dm.kv("name", reagent.name),
                    Dm.kv("volume", Dm.round(Dm.num(reagent.volume), CHEMICAL_QUANTISATION_LEVEL)),
                    Dm.kv("color", reagent.color))));
            }
            last_scan_data["tray_data"] = tray_data;
        }

        if (seed != null)
            last_scan_data["seed_data"] = await make_seed_data(seed);

        if (isitem(target) && target.reagents is datum_reagents target_reagents)
        {
            DmList plant_reagents = Dm.list();
            foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(target_reagents.reagent_list))
            {
                Dm.append(ref plant_reagents, Dm.list(Dm.list(
                    Dm.kv("name", reagent.name),
                    Dm.kv("volume", Dm.round(Dm.num(reagent.volume), CHEMICAL_QUANTISATION_LEVEL)),
                    Dm.kv("color", reagent.color))));
            }
            last_scan_data["plant_data"] = Dm.list(Dm.kv("reagents", plant_reagents));
        }

        if (graft != null && graft.plant_dna is obj_item_seeds dna) // a graft made without a mother plant holds a seed path: a runtime in tg
        {
            last_scan_data["graft_data"] = Dm.list(
                Dm.kv("name", dna.name),
                Dm.kv("icon", graft.icon),
                Dm.kv("icon_state", graft.icon_state),
                Dm.kv("yield", dna.yield),
                Dm.kv("production", dna.production),
                Dm.kv("lifespan", dna.lifespan),
                Dm.kv("endurance", dna.endurance),
                Dm.kv("weed_rate", dna.weed_rate),
                Dm.kv("weed_chance", dna.weed_chance),
                Dm.kv("graft_gene", dna.graft_gene)); // tg: graft.plant_dna.graft_gene.type, of a var holding a typepath: the path itself
        }

        if (user != null)
            await ui_interact(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:286
    public virtual async DmTask<object> make_seed_data(obj_item_seeds seed = null)
    {
        Type product = seed.product;
        DmList seed_data = Dm.list(
            Dm.kv("name", seed.plantname),
            Dm.kv("icon", seed.growing_icon),
            Dm.kv("icon_state", seed.icon_harvest),
            Dm.kv("product", product == null ? null : Dm.initial(product, (atom a) => a.name)),
            Dm.kv("product_icon", product == null ? null : Dm.initial(product, (atom a) => a.icon)),
            Dm.kv("product_icon_state", product == null ? null : Dm.initial(product, (atom a) => a.icon_state)),
            Dm.kv("potency", seed.potency),
            Dm.kv("yield", seed.yield),
            Dm.kv("instability", seed.instability),
            Dm.kv("maturation", seed.maturation),
            Dm.kv("production", seed.production),
            Dm.kv("lifespan", seed.lifespan),
            Dm.kv("endurance", seed.endurance),
            Dm.kv("weed_rate", seed.weed_rate),
            Dm.kv("weed_chance", seed.weed_chance),
            Dm.kv("graft_gene", seed.graft_gene ?? typeof(datum_plant_gene_trait_repeated_harvest)));
        DmList removable_traits = Dm.list();
        DmList core_traits = Dm.list();
        seed_data["distill_reagent"] = "";
        seed_data["juice_name"] = "";
        seed_data["grind_results"] = Dm.list();
        foreach (datum_plant_gene_trait trait in Dm.each<datum_plant_gene_trait>(seed.genes))
        {
            if (Dm.truthy(Dm.bitand(trait.mutability_flags, PLANT_GENE_REMOVABLE)))
                Dm.append(ref removable_traits, trait.type);
            else
                Dm.append(ref core_traits, trait.type);
        }
        seed_data["removable_traits"] = removable_traits;
        seed_data["core_traits"] = core_traits;
        DmList reagents = Dm.list();
        foreach (datum_plant_gene_reagent reagent in Dm.each<datum_plant_gene_reagent>(seed.genes))
            Dm.append(ref reagents, Dm.list(Dm.list(Dm.kv("name", reagent.name), Dm.kv("rate", reagent.rate))));
        seed_data["reagents"] = reagents;
        datum_plant_gene_trait_maxchem volume_trait = Dm.locate_type(typeof(datum_plant_gene_trait_maxchem), seed.genes) as datum_plant_gene_trait_maxchem;
        datum_plant_gene_trait_modified_volume volume_unit_trait = Dm.locate_type(typeof(datum_plant_gene_trait_modified_volume), seed.genes) as datum_plant_gene_trait_modified_volume;
        seed_data["volume_mod"] = volume_trait != null ? volume_trait.rate : 1;
        seed_data["volume_units"] = volume_unit_trait != null ? volume_unit_trait.new_capacity : PLANT_REAGENT_VOLUME;
        DmList mutatelist = Dm.list();
        foreach (Type mutant in Dm.each_cast<Type>(seed.mutatelist))
            Dm.append(ref mutatelist, Dm.initial(mutant, (obj_item_seeds s) => s.plantname));
        seed_data["mutatelist"] = mutatelist;
        if (product != null)
        {
            DmList cache = product_grinder_results;
            if (!Dm.truthy(cache[product]))
            {
                DmList entry = Dm.list();
                cache[product] = entry;
                obj_item_food_grown grown_product = Dm.@new(product) as obj_item_food_grown;
                obj_item product_item = grown_product ?? Dm.@new(product) as obj_item; // tg types it /obj/item/food/grown but some products are /obj/item/grown
                Type product_distill_reagent = grown_product?.distill_reagent;
                object product_juice_typepath = product_item.juice_typepath();
                entry["distill_reagent"] = product_distill_reagent == null ? null : Dm.initial(product_distill_reagent, (datum_reagent r) => r.name);
                entry["juice_name"] = product_juice_typepath is Type juice_type ? Dm.initial(juice_type, (datum_reagent r) => r.name) : null;
                DmList grind_names = Dm.list();
                foreach (Type reagent in Dm.each_cast<Type>(await product_item.grind_results()))
                    Dm.append(ref grind_names, Dm.initial(reagent, (datum_reagent r) => r.name));
                entry["grind_results"] = grind_names;
                qdel(product_item);
            }
            DmList cached = cache[product] as DmList;
            seed_data["distill_reagent"] = cached["distill_reagent"];
            seed_data["juice_name"] = cached["juice_name"];
            seed_data["grind_results"] = cached["grind_results"];
        }

        DmList unique_labels = Dm.list();
        DmList unique_collapsibles = Dm.list();
        DmList unique_data = seed.get_unique_analyzer_data() as DmList;
        foreach (object label in Dm.each(unique_data))
        {
            DmList row = Dm.list(Dm.list(Dm.kv("label", label), Dm.kv("data", unique_data[label])));
            if (islist(unique_data[label]))
                Dm.append(ref unique_collapsibles, row);
            else
                Dm.append(ref unique_labels, row);
        }
        seed_data["unique_labels"] = unique_labels;
        seed_data["unique_collapsibles"] = unique_collapsibles;
        return seed_data;
    }

    /// Cached data for the product grinder results (var/static, rules 10.6)
    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:24
    public static ref DmList product_grinder_results => ref TgState.Current.obj_item_plant_analyzer__product_grinder_results;
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/devices/scanners/plant_analyzer.dm:24  var/static/list/product_grinder_results = list()
    internal DmList obj_item_plant_analyzer__product_grinder_results = Dm.list();
}
