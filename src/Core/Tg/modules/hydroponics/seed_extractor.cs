// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/seed_extractor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file: seedify, the seed extractor and its SeedExtractor window. add_context is a live-partial row (D264).

public static partial class GlobalProcs
{
    /**
     * Finds and extracts seeds from an object
     *
     * Checks if the object is such that creates a seed when extracted.  Used by seed
     * extractors or posably anything that would create seeds in some way.  The seeds
     * are dropped either at the extractor, if it exists, or where the original object
     * was and it qdel's the object
     *
     * Arguments:
     * * O - Object containing the seed, can be the loc of the dumping of seeds
     * * t_max - Amount of seed copies to dump, -1 is ranomized
     * * extractor - Seed Extractor, used as the dumping loc for the seeds and seed multiplier
     * * user - checks if we can remove the object from the inventory
     * *
     */
    // tg: code/modules/hydroponics/seed_extractor.dm:16
    public static async DmTask<object> seedify(obj_item @object = null, object t_max = null, obj_machinery_seed_extractor extractor = null, mob_living user = null)
    {
        //try to get the seed from this item
        obj_item_seeds seed = @object.get_plant_seed() as obj_item_seeds;
        if (seed == null)
            return null;

        //generate a random multiplier if value is not specified
        DmList seeds = Dm.list();
        float max = Dm.num(t_max);
        if (max == -1)
        {
            if (extractor != null)
                max = rand(1, 4) * extractor.seed_multiplier;
            else
                max = rand(1, 4);
        }
        //drop location for the newly generated seeds
        atom seedloc = @object.loc;
        if (extractor != null)
            seedloc = extractor.loc;

        //multiply the seeds and delete the item
        if (user != null && !Dm.truthy(await user.temporarilyRemoveItemFromInventory(@object))) //couldn't drop the item
            return null;
        for (float _ = 0; _ <= max; _++)
        {
            obj_item_seeds t_prod = seed.Copy() as obj_item_seeds;
            seeds.Add(t_prod);
            await t_prod.forceMove(seedloc);
        }
        qdel(@object);
        return seeds;
    }
}

// tg: code/modules/hydroponics/seed_extractor.dm:45
public partial class obj_machinery_seed_extractor : obj_machinery
{
    // tg: code/modules/hydroponics/seed_extractor.dm:58
    [DmParams("mapload", "new_seed")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/modules/hydroponics/seed_extractor.dm:80
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        foreach (datum_stock_part_matter_bin matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            max_seeds = Dm.num(Dm.initial(this, "max_seeds")) * Dm.num(matter_bin.tier);
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            seed_multiplier = Dm.num(Dm.initial(this, "seed_multiplier")) * Dm.num(servo.tier);
        return dot;
    }

    // tg: code/modules/hydroponics/seed_extractor.dm:87
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(in_range(user, this)) || isobserver(user))
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Extracting <b>[] to []</b> seed(s) per piece of produce.<br>Machine can store up to <b>[]</b> seeds.", seed_multiplier, seed_multiplier * 4, max_seeds)));
        return dot;
    }

    // tg: code/modules/hydroponics/seed_extractor.dm:92
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]_open", base_icon_state) : base_icon_state as string;
        return dot;
    }

    // tg: code/modules/hydroponics/seed_extractor.dm:96
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/hydroponics/seed_extractor.dm:101
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/hydroponics/seed_extractor.dm:104
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_pry_open(user, tool, close_after_pry: TRUE, deconstruct_on_fail: TRUE);
    }

    // tg: code/modules/hydroponics/seed_extractor.dm:107
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_storage_bag_plants)
        {
            float loaded = 0;
            foreach (obj_item_seeds to_store in Dm.each<obj_item_seeds>(tool.contents))
            {
                if (contents.len >= max_seeds)
                {
                    to_chat(user, span_warning(I18n.T("[] is full.", this)));
                    break;
                }
                if (!Dm.truthy(await add_seed(to_store, tool)))
                    continue;
                loaded += 1;
            }

            if (Dm.truthy(loaded))
            {
                to_chat(user, span_notice(I18n.T("You put as many seeds from [] into [] as you can.", tool, this)));
                return ITEM_INTERACT_SUCCESS;
            }
            to_chat(user, span_warning(I18n.T("There are no seeds in [].", tool)));
            return ITEM_INTERACT_BLOCKING;
        }

        DmList generated_seeds = await seedify(tool, -1, this, user) as DmList;
        if (generated_seeds != null)
        {
            if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            {
                //find all seeds lying on the turf and add them to the machine
                foreach (obj_item_seeds seed in Dm.each_cast<obj_item_seeds>(generated_seeds))
                {
                    //machine is full
                    if (contents.len >= max_seeds)
                    {
                        to_chat(user, span_warning(I18n.T("[] is full.", this)));
                        break;
                    }
                    //add seed to machine. second argument is null which means just force move into the machine
                    await add_seed(seed);
                }
            }
            to_chat(user, span_notice(I18n.T("You extract some seeds.")));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_seeds)
        {
            if (contents.len >= max_seeds)
            {
                to_chat(user, span_warning(I18n.T("[] is full.", this)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (Dm.truthy(await add_seed(tool as obj_item_seeds, user)))
            {
                to_chat(user, span_notice(I18n.T("You add [] to [].", tool, this)));
                return ITEM_INTERACT_SUCCESS;
            }

            to_chat(user, span_warning(I18n.T("You can't seem to add [] to [].", tool, this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(tool.tool_behaviour) || !Dm.truthy(user.combat_mode)) // Using the wrong tool shouldn't assume you want to turn it into seeds.
        {
            to_chat(user, span_warning(I18n.T("You can't extract any seeds from []!", tool)));
            return ITEM_INTERACT_BLOCKING;
        }

        return NONE;
    }

    /**
     * Generate seed string
     *
     * Creates a string based of the traits of a seed.  We use this string as a bucket for all
     * seeds that match as well as the key the ui uses to get the seed.  We also use the key
     * for the data shown in the ui.  Javascript parses this string to display
     *
     * Arguments:
     * * O - seed to generate the string from
     */
    // tg: code/modules/hydroponics/seed_extractor.dm:166
    public virtual object generate_seed_hash(obj_item_seeds O = null)
    {
        string genes = list2params(O.genes);
        return md5(Dm.text("[][][][][][][][][]", O.name, O.lifespan, O.endurance, O.maturation, O.production, O.yield, O.potency, O.instability, genes));
    }

    /** Add Seeds Proc.
     *
     * Adds the seeds to the contents and to an associated list that pregenerates the data
     * needed to go to the ui handler
     *
     * to_add - what seed are we adding?
     * taking_from - where are we taking the seed from? A mob, a bag, etc? If null its means it's just laying on the turf so force move it in
     **/
    // tg: code/modules/hydroponics/seed_extractor.dm:178
    public virtual async DmTask<object> add_seed(obj_item_seeds to_add = null, atom taking_from = null)
    {
        object seed_id = generate_seed_hash(to_add);
        DmList seed_data = null;
        bool has_seed_data = false; // so we remember to add a seed obj weakref to piles[seed_id] at the end of the proc. That way if some reason we runtime in this proc it won't incorrectly add data to the list
        if (Dm.truthy(piles[seed_id]))
        {
            has_seed_data = true;
        }
        else
        {
            seed_data = await generate_seed_data_from(to_add) as DmList;
            seed_data["name"] = capitalize(replacetext(to_add.name, "pack of ", ""));
            seed_data["icon"] = Dm.initial(to_add, (atom a) => a.icon);
            seed_data["icon_state"] = Dm.initial(to_add, (atom a) => a.icon_state);
            seed_data["refs"] = Dm.list(WEAKREF(to_add));
        }

        if (taking_from != null)
        {
            if (taking_from is mob mob_loc)
            {
                if (!Dm.truthy(await mob_loc.transferItemToLoc(to_add, this)))
                    return FALSE;
            }

            else if (!Dm.truthy(taking_from.atom_storage?.attempt_remove(to_add, this, silent: TRUE)))
            {
                return FALSE;
            }
        }
        else
        {
            await to_add.forceMove(this);
        }

        // do this at the end, in case any of the previous steps failed
        if (has_seed_data)
            (((DmList)piles[seed_id])["refs"] as DmList).Add(WEAKREF(to_add));
        else
            piles[seed_id] = seed_data;

        return TRUE;
    }

    // tg: code/modules/hydroponics/seed_extractor.dm:210
    public override object ui_state(mob user = null)
    {
        return GLOB.notcontained_state;
    }

    // tg: code/modules/hydroponics/seed_extractor.dm:213
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "SeedExtractor", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/hydroponics/seed_extractor.dm:219
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList seeds = Dm.list();
        foreach (object seed_id in Dm.each(piles.Copy()))
        {
            if (!Dm.truthy(length(((DmList)piles[seed_id])["refs"])))
            {
                piles.Remove(seed_id); // This shouldn't happen but still
                continue;
            }
            DmList seed_data = piles[seed_id] as DmList;
            seed_data = seed_data.Copy();
            seed_data["key"] = seed_id;
            seed_data["amount"] = length(seed_data["refs"]);
            seed_data.Remove("refs");
            Dm.append(ref seeds, Dm.list(seed_data));
        }
        DmList dot = Dm.list();
        dot["seeds"] = seeds;
        return dot;
    }

    // tg: code/modules/hydroponics/seed_extractor.dm:234
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

    // tg: code/modules/hydroponics/seed_extractor.dm:248
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action as string)
        {
            case "scrap":
            {
                object item = @params?["item"];
                if (Dm.truthy(piles[item]))
                {
                    piles.Remove(item);
                    dot = TRUE;
                }
                break;
            }
            case "take":
            {
                object item = @params?["item"];
                if (Dm.truthy(piles[item]) && Dm.num(length(piles[item])) > 0)
                {
                    datum_weakref found_seed_weakref = ((DmList)((DmList)piles[item])["refs"])[1] as datum_weakref;
                    obj_item_seeds found_seed = found_seed_weakref.resolve() as obj_item_seeds;
                    if (found_seed == null)
                        return dot;

                    ((DmList)((DmList)piles[item])["refs"]).Remove(found_seed_weakref);
                    if (usr != null)
                    {
                        mob user = usr;
                        if (Dm.truthy(await user.put_in_hands(found_seed)))
                            to_chat(user, span_notice(I18n.T("You take [] out of the slot.", found_seed)));
                        else
                            to_chat(user, span_notice(I18n.T("[] falls onto the floor.", found_seed)));
                    }
                    else
                    {
                        await found_seed.forceMove(drop_location() as atom);
                        visible_message(span_notice(I18n.T("[] falls onto the floor.", found_seed)), null, span_hear(I18n.T("You hear a soft clatter.")), COMBAT_MESSAGE_RANGE);
                    }
                    dot = TRUE;
                }
                break;
            }
        }
        return dot;
    }
}
