// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish_catalog.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fish encyclopedia, a book whose FishCatalog window lists every fish with its waters, its feed, its
// size and weight, its beauty and its fishing tips. The var block is generated (D111).

///Book detailing where to get the fish and their properties.
// tg: code/modules/fishing/fish_catalog.dm:2
public partial class obj_item_book_manual_fish_catalog : obj_item_book_manual
{
    // tg: code/modules/fishing/fish_catalog.dm:9
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -7, ITEM_SLOT_HANDS));
        return dot;
    }

    // tg: code/modules/fishing/fish_catalog.dm:13
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "FishCatalog", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/fishing/fish_catalog.dm:19
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = await base.ui_static_data(user) as DmList;
        DmList fish_info = TgState.Current.obj_item_book_manual_fish_catalog__ui_static_data__fish_info;
        if (!Dm.truthy(fish_info))
        {
            fish_info = TgState.Current.obj_item_book_manual_fish_catalog__ui_static_data__fish_info = Dm.list();
            foreach (Type fish in Dm.each<Type>(subtypesof(typeof(obj_item_fish))))
            {
                if (!Dm.truthy(Dm.bitand(Dm.initial(fish, (obj_item_fish p) => p.fish_flags), FISH_FLAG_SHOW_IN_CATALOG)))
                    continue;
                DmList fish_data = Dm.list();
                fish_data["name"] = Dm.initial(fish, (obj_item_fish p) => p.name);
                fish_data["desc"] = Dm.initial(fish, (obj_item_fish p) => p.desc);
                fish_data["fluid"] = Dm.initial(fish, (obj_item_fish p) => p.required_fluid_type);
                fish_data["temp_min"] = Dm.initial(fish, (obj_item_fish p) => p.required_temperature_min);
                fish_data["temp_max"] = Dm.initial(fish, (obj_item_fish p) => p.required_temperature_max);
                fish_data["icon"] = sanitize_css_class_name($"{Dm.str(Dm.initial(fish, (obj_item_fish p) => p.icon))}{Dm.str(Dm.initial(fish, (obj_item_fish p) => p.icon_state))}");
                fish_data["color"] = Dm.initial(fish, (obj_item_fish p) => p.color);
                fish_data["source"] = Dm.truthy(Dm.initial(fish, (obj_item_fish p) => p.random_case_rarity)) ? $"{AQUARIUM_COMPANY} Fish Packs" : "Unknown";
                fish_data["size"] = Dm.initial(fish, (obj_item_fish p) => p.average_size);
                fish_data["weight"] = Dm.initial(fish, (obj_item_fish p) => p.average_weight);
                Type food_type = Dm.initial(fish, (obj_item_fish p) => p.food);
                if (food_type != typeof(datum_reagent_consumable_nutriment))
                    fish_data["feed"] = Dm.initial(food_type, (datum_reagent p) => p.name);
                else
                    fish_data["feed"] = $"{AQUARIUM_COMPANY} Fish Feed";
                fish_data["fishing_tips"] = build_fishing_tips(fish);
                fish_data["beauty"] = (SSfishing.fish_properties[fish] as DmList)?[FISH_PROPERTIES_BEAUTY_SCORE];

                Dm.append(fish_info, Dm.list(fish_data));
            }
        }

        dot["fish_info"] = fish_info;
        dot["sponsored_by"] = AQUARIUM_COMPANY;
        return dot;
    }

    // tg: code/modules/fishing/fish_catalog.dm:51
    public virtual object bait_description(object bait = null)
    {
        if (ispath(bait))
        {
            Type bait_item = (Type)bait;
            return Dm.initial(bait_item, (obj p) => p.name);
        }
        if (islist(bait))
        {
            DmList special_identifier = (DmList)bait;
            object identifier = special_identifier[FISH_BAIT_TYPE];
            if (Dm.eq(identifier, FISH_BAIT_FOODTYPE))
            {
                // FOOD_FLAGS_IC (__DEFINES/food.dm:76): a list literal, a new list at every use
                DmList FOOD_FLAGS_IC = Dm.list("Meat", "Vegetables", "Raw food", "Junk food", "Grain", "Fruits", "Dairy products", "Fried food", "Alcohol", "Sugary food", "Gross food", "Toxic food", "Pineapples", "Breakfast food", "Clothing", "Nuts", "Seafood", "Oranges", "Bugs", "Gore", "Rocks", "Eggs");
                return jointext(bitfield_to_list(Dm.num(special_identifier[FISH_BAIT_VALUE]), FOOD_FLAGS_IC) as DmList, ",");
            }
            if (Dm.eq(identifier, FISH_BAIT_REAGENT))
            {
                Type prototype = special_identifier[FISH_BAIT_VALUE] as Type;
                return $"{Dm.str(Dm.initial(prototype, (datum_reagent p) => p.name))} (at least {Dm.str(special_identifier[FISH_BAIT_AMOUNT])}u)";
            }
            stack_trace("Unknown bait identifier in fish favourite/disliked list", "code/modules/fishing/fish_catalog.dm", 64);
            return "SOMETHING VERY WEIRD";
        }
        //Here we handle descriptions of traits fish use as qualifiers
        return "something special";
    }

    // tg: code/modules/fishing/fish_catalog.dm:70
    public virtual object build_fishing_tips(object fish_type = null)
    {
        Type fishy = (Type)fish_type;
        DmList dot = Dm.list();
        //// Where can it be found - iterate fish sources, how should this handle key
        DmList spot_descriptions = Dm.list();
        // tg loops over the list's keys, the source types, and reads their vars as a type's (BYOND 515 reads a path's var as its
        // initial value); the port reads them off the preset source each key names, whose values are the same.
        foreach (object source_path in Dm.each(GLOB.preset_fish_sources))
        {
            datum_fish_source source = GLOB.preset_fish_sources[source_path] as datum_fish_source;
            if (Dm.truthy(source.catalog_description) && Dm.@in(fish_type, source.fish_table))
                Dm.append(spot_descriptions, source.catalog_description);
        }
        dot["spots"] = english_list(spot_descriptions, nothing_text: "Unknown");
        DmList fish_list_properties = SSfishing.fish_properties;
        DmList properties = fish_list_properties[fishy] as DmList;
        DmList fav_bait = properties?[FISH_PROPERTIES_FAV_BAIT] as DmList;
        DmList disliked_bait = properties?[FISH_PROPERTIES_BAD_BAIT] as DmList;
        DmList bait_list = Dm.list();
        // Favourite/Disliked bait
        foreach (object bait_type_or_trait in Dm.each(fav_bait))
            Dm.append(bait_list, bait_description(bait_type_or_trait));
        dot["favorite_bait"] = english_list(bait_list, nothing_text: "None");
        bait_list.Cut();
        foreach (object bait_type_or_trait in Dm.each(disliked_bait))
            Dm.append(bait_list, bait_description(bait_type_or_trait));
        dot["disliked_bait"] = english_list(bait_list, nothing_text: "None");
        // Fish traits description
        DmList trait_descriptions = Dm.list();
        DmList fish_traits = properties?[FISH_PROPERTIES_TRAITS] as DmList;
        float fish_difficulty = Dm.initial(fishy, (obj_item_fish p) => p.fishing_difficulty_modifier);
        foreach (object fish_trait in Dm.each(fish_traits))
        {
            datum_fish_trait trait = GLOB.fish_traits[fish_trait] as datum_fish_trait;
            Dm.append(trait_descriptions, trait.catalog_description);
            fish_difficulty += trait.added_difficulty;
        }
        if (!Dm.truthy(length(trait_descriptions)))
            Dm.append(trait_descriptions, "This fish exhibits no special behavior.");
        dot["traits"] = trait_descriptions;
        ///Difficulty descriptor
        // DM's switch ranges: the first range that holds the value wins, a value between 9 and 10 falls to the else.
        if (fish_difficulty <= 9)
            dot["difficulty"] = "Easy";
        else if (fish_difficulty >= 10 && fish_difficulty <= 19)
            dot["difficulty"] = "Medium";
        else
            dot["difficulty"] = "Hard";
        return dot;
    }

    // tg: code/modules/fishing/fish_catalog.dm:112
    public override async DmTask<object> ui_assets(mob user = null)
    {
        await DmTask.CompletedTask;
        // list(get_asset_datum(/datum/asset/spritesheet_batched/fish)): the asset transport is the client's (DV44); the client draws
        // the "fish" sheet's pictures from its own copy of tg's .dmi files (TguiSpritesheets).
        return Dm.list();
    }
}

public sealed partial class TgState
{
    // tg: code/modules/fishing/fish_catalog.dm:21  ui_static_data's var/static/fish_info (rules 10.6)
    internal DmList obj_item_book_manual_fish_catalog__ui_static_data__fish_info;
}
