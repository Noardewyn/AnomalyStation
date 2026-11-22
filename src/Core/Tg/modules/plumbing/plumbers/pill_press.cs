// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/pill_press.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. packaging_type holds a typepath, so its var is hand-declared (the generator types it
// as an object of the type), with the constructor; the other vars are generated (D111).

///We take a constant input of reagents, and produce a pill once a set volume is reached
// tg: code/modules/plumbing/plumbers/pill_press.dm:5
public partial class obj_machinery_plumbing_pill_press : obj_machinery_plumbing
{
    ///the minimum size of a pill or patch
    // tg: code/modules/plumbing/plumbers/pill_press.dm:2  #define MIN_VOLUME 5 (file-local, rules 11.4)
    private const float MIN_VOLUME = 5;

    /// All packaging types wrapped up in 1 big list
    // tg: code/modules/plumbing/plumbers/pill_press.dm:19  var/static/list/packaging_types (rules 10.6)
    public static ref DmList packaging_types => ref TgState.Current.obj_machinery_plumbing_pill_press__packaging_types;

    ///The type of packaging to use
    // tg: code/modules/plumbing/plumbers/pill_press.dm:21
    // The generated field would be an obj_item_reagent_containers (tg declares var/obj/item/reagent_containers/packaging_type for
    // initial()), which a typepath does not fit: the hand-written member holds the path, and the constructor with tg's overrides
    // (pill_press.dm:6-8) is here.
    public Type packaging_type;

    // tg: code/modules/plumbing/plumbers/pill_press.dm:5
    public obj_machinery_plumbing_pill_press()
    {
        name = "chemical press";
        desc = "A press that makes pills, patches and bottles.";
        icon_state = "pill_press";
    }

    // tg: code/modules/plumbing/plumbers/pill_press.dm:25
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);

        if (!Dm.truthy(packaging_types))
        {
            // var/datum/asset/spritesheet_batched/assets = get_asset_datum(/datum/asset/spritesheet_batched/chemmaster): the asset
            // transport is the client's (DV44); the class is the one tg's icon_class_name gives (batched_spritesheet.dm:342,
            // "[name][size_id] [sprite_name]": every container's sprite is 32x32), which the client resolves from its own .dmi files.

            DmList types = Dm.list(
                Dm.kv(CAT_PILLS, GLOB.reagent_containers[CAT_PILLS]),
                Dm.kv(CAT_PATCHES, GLOB.reagent_containers[CAT_PATCHES]),
                Dm.kv("Bottles", Dm.list(typeof(obj_item_reagent_containers_cup_bottle))));

            packaging_types = Dm.list();
            foreach (object category in Dm.each(types))
            {
                DmList packages = types[category] as DmList;

                DmList category_item = Dm.list(Dm.kv("cat_name", category));
                foreach (Type container in Dm.each_cast<Type>(packages))
                {
                    DmList package_item = Dm.list(
                        Dm.kv("class_name", Dm.text("chemmaster32x32 []", sanitize_css_class_name(Dm.text("[]", container)))),
                        Dm.kv("ref", REF(container)));
                    category_item["products"] = Dm.add(category_item["products"], Dm.list(package_item));
                }

                packaging_types = Dm.add(packaging_types, Dm.list(category_item)) as DmList;
            }
        }

        packaging_type = (GLOB.reagent_containers[CAT_PILLS] as DmList)[1] as Type;
        packaging_category = CAT_PILLS;
        max_volume = Dm.num(Dm.initial(packaging_type, (obj_item_reagent_containers r) => r.volume));
        current_volume = clamp(current_volume, MIN_VOLUME, max_volume);

        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_pill_press), layer));
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/pill_press.dm:58
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(is_operational) || ((datum_reagents)reagents).total_volume < current_volume)
            return null;

        obj_item_reagent_containers container = Dm.@new(packaging_type, this) as obj_item_reagent_containers;
        string suffix;
        switch (packaging_category)
        {
            case CAT_PILLS:
                suffix = "pill";
                break;
            case CAT_PATCHES:
                suffix = "patch";
                break;
            default:
                suffix = "bottle";
                break;
        }
        container.name = Dm.text("[] []", product_name, suffix);
        Waiting.NoSleep(((datum_reagents)reagents).trans_to(container, current_volume));
        if (istype(container, typeof(obj_item_reagent_containers_applicator_pill)))
        {
            obj_item_reagent_containers_applicator_pill pill = (obj_item_reagent_containers_applicator_pill)container;
            pill.layers_remaining = pill_duration;
        }
        Waiting.NoSleep(container.forceMove(drop_location() as atom));

        Waiting.NoSleep(use_energy(active_power_usage * Dm.num(seconds_per_tick)));
        return null;
    }

    // tg: code/modules/plumbing/plumbers/pill_press.dm:80
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/chemmaster)): the asset transport is the client's (DV44); the client
        // draws the containers from its own copy of their .dmi files (TguiSpritesheets).
        return Dm.list();
    }

    // tg: code/modules/plumbing/plumbers/pill_press.dm:85
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ChemPress", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/plumbing/plumbers/pill_press.dm:91
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        data["min_volume"] = MIN_VOLUME;
        data["packaging_types"] = packaging_types;

        return data;
    }

    // tg: code/modules/plumbing/plumbers/pill_press.dm:99
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["current_volume"] = current_volume;
        data["pill_duration"] = pill_duration;
        data["max_volume"] = max_volume;
        data["max_duration"] = PILL_MAX_LAYERS;
        data["product_name"] = product_name;
        data["packaging_type"] = REF(packaging_type);
        data["packaging_category"] = packaging_category;

        return data;
    }

    // tg: code/modules/plumbing/plumbers/pill_press.dm:112
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "change_current_volume":
            {
                object value = @params["volume"];
                if (value == null)
                    return FALSE;

                value = text2num(value);
                if (value == null)
                    return FALSE;

                current_volume = clamp(Dm.num(value), MIN_VOLUME, max_volume);
                return TRUE;
            }

            case "change_pill_duraton":
            {
                object value = @params["duration"];
                if (value == null)
                    return FALSE;

                value = text2num(value);
                if (value == null)
                    return FALSE;

                pill_duration = clamp(Dm.num(value), 0, PILL_MAX_LAYERS);
                return TRUE;
            }

            case "change_product_name":
            {
                string formatted_name = html_encode(@params["name"]);
                if (length(formatted_name) > MAX_NAME_LEN)
                    product_name = copytext(formatted_name, 1, MAX_NAME_LEN + 1);
                else
                    product_name = formatted_name;
                return TRUE;
            }

            case "change_product":
            {
                object container = @params["ref"];
                if (!Dm.truthy(container))
                    return FALSE;

                //is a valid option
                float container_found = FALSE;
                foreach (DmList category in Dm.each_cast<DmList>(packaging_types))
                {
                    if (Dm.truthy(container_found))
                        break;
                    foreach (DmList package_item in Dm.each_cast<DmList>(category["products"]))
                    {
                        if (Dm.eq(container, package_item["ref"]))
                        {
                            container_found = TRUE;
                            break;
                        }
                    }
                }
                if (!Dm.truthy(container_found))
                    return FALSE;

                //decode container & its category
                packaging_type = Dm.locate_ref(container) as Type;
                if (ispath(packaging_type, typeof(obj_item_reagent_containers_applicator_patch)))
                    packaging_category = CAT_PATCHES;
                else if (ispath(packaging_type, typeof(obj_item_reagent_containers_applicator_pill)))
                    packaging_category = CAT_PILLS;
                else
                    packaging_category = "Bottles";

                //get new volumes
                max_volume = Dm.num(Dm.initial(packaging_type, (obj_item_reagent_containers r) => r.volume));
                current_volume = clamp(current_volume, MIN_VOLUME, max_volume);
                return TRUE;
            }
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/plumbing/plumbers/pill_press.dm:19
    internal DmList obj_machinery_plumbing_pill_press__packaging_types;
}
