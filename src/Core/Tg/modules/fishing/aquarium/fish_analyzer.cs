// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/aquarium/fish_analyzer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fish analyzer, a scanner in a case of a random colour that shows a fish's (or every fish of an aquarium's)
// health, hunger, size, weight, food, fluid and temperature, traits, evolutions and breeding wait in the FishAnalyzer window, and
// runs the fish scanning experiments. Its emissive screen glow is not drawn (DV12). The var blocks are generated (D111).

///An item that can be used to gather information on the fish, such as but not limited to: health, hunger and traits.
// tg: code/modules/fishing/aquarium/fish_analyzer.dm:2
public partial class obj_item_fish_analyzer : obj_item
{
    // tg: code/modules/fishing/aquarium/fish_analyzer.dm:28
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        case_color = rgb(rand(16, 255), rand(16, 255), rand(16, 255));
        Waiting.NoSleep(set_greyscale(colors: Dm.list(case_color)));
        object dot = base.Initialize(mapload, __extra);

        DmList fishe_signals = TgState.Current.obj_item_fish_analyzer__Initialize__fishe_signals ??= Dm.list(
            Dm.kv(COMSIG_FISH_ANALYZER_ANALYZE_STATUS, "/datum/component/experiment_handler/proc/" + nameof(datum_component_experiment_handler.try_run_handheld_experiment)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_experiment_handler),
            Dm.named("config_mode", EXPERIMENT_CONFIG_ALTCLICK),
            Dm.named("allowed_experiments", Dm.list(typeof(datum_experiment_scanning_fish))),
            Dm.named("config_flags", Dm.bitor(EXPERIMENT_CONFIG_SILENT_FAIL, EXPERIMENT_CONFIG_IMMEDIATE_ACTION)),
            Dm.named("experiment_signals", fishe_signals)));

        register_item_context();
        update_appearance();
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -3, ITEM_SLOT_HANDS));
        return dot;
    }

    // tg: code/modules/fishing/aquarium/fish_analyzer.dm:46
    public override object Destroy(float? force = null)
    {
        scanned_object = null;
        return base.Destroy(force);
    }

    // tg: code/modules/fishing/aquarium/fish_analyzer.dm:50
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("<b>Alt-Click</b> to access the Experiment Configuration UI")));
        return dot;
    }

    // tg: code/modules/fishing/aquarium/fish_analyzer.dm:54
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.str(base_icon_state);
        return dot;
    }

    // tg: code/modules/fishing/aquarium/fish_analyzer.dm:58
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        mutable_appearance @case = mutable_appearance(icon, "fish_analyzer_case") as mutable_appearance;
        @case.color = case_color;
        Dm.append(ref dot, @case);
        // DEVIATION(DV12): . += emissive_appearance(icon, "fish_analyzer_emissive", src), the screen's glow on the emissive plane
        return dot;
    }

    // tg: code/modules/fishing/aquarium/fish_analyzer.dm:65
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        atom target = interacting_with;
        if (!isfish(target) && !HAS_TRAIT(target, TRAIT_IS_AQUARIUM))
            return NONE;
        if (!Dm.truthy(user.can_read(this)) || Dm.truthy(user.is_blind()))
            return ITEM_INTERACT_BLOCKING;

        if (_listen_lookup?[COMSIG_FISH_ANALYZER_ANALYZE_STATUS] != null)
            _SendSignal(COMSIG_FISH_ANALYZER_ANALYZE_STATUS, Dm.list(this, target, user));
        if (target != scanned_object)
        {
            unregister_scanned();
            register_scanned(target);
        }
        await ui_interact(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/fishing/aquarium/fish_analyzer.dm:78
    public virtual void register_scanned(atom target = null)
    {
        scanned_object = target;
        RegisterSignal(target, COMSIG_QDELETING, nameof(on_target_deleted));
    }

    // tg: code/modules/fishing/aquarium/fish_analyzer.dm:82
    public virtual void unregister_scanned()
    {
        if (scanned_object == null)
            return;
        UnregisterSignal(scanned_object, COMSIG_QDELETING);
        scanned_object = null;
    }

    // tg: code/modules/fishing/aquarium/fish_analyzer.dm:88
    [SignalHandler]
    public virtual void on_target_deleted()
    {
        unregister_scanned();
    }

    // Whether the scanned specimen is in view of the analyzer or on the user (tg's `scanned_object in (view(7, get_turf(src)) | user.get_equipped_items(INCLUDE_HELD))`).
    private bool specimen_in_reach(mob_living user)
    {
        return Dm.@in(scanned_object, Dm.bitor(view(7, get_turf(this)), user.get_equipped_items(INCLUDE_HELD)));
    }

    // tg: code/modules/fishing/aquarium/fish_analyzer.dm:92
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (scanned_object == null)
        {
            balloon_alert(user, I18n.T("no specimen data!"));
            return TRUE;
        }
        if (user is mob_living living && !specimen_in_reach(living))
        {
            balloon_alert(user, I18n.T("specimen data lost!"));
            unregister_scanned();
            return TRUE;
        }

        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "FishAnalyzer");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/fishing/aquarium/fish_analyzer.dm:107
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (user is not mob_living living) //observers shouldn't disrupt things.
            return await base.ui_status(user, state);
        if (scanned_object == null || !specimen_in_reach(living))
        {
            balloon_alert(user, I18n.T("specimen data lost!"));
            unregister_scanned();
            return UI_CLOSE;
        }
        return await base.ui_status(user, state);
    }

    // tg: code/modules/fishing/aquarium/fish_analyzer.dm:116
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        data["fish_list"] = Dm.list();
        data["fish_scanned"] = FALSE;

        if (isfish(scanned_object))
        {
            data["fish_scanned"] = TRUE;
            return extract_fish_info(data, (obj_item_fish)scanned_object);
        }

        foreach (obj_item_fish fishie in Dm.each<obj_item_fish>(scanned_object))
            extract_fish_info(data, fishie);

        return data;
    }

    // tg: code/modules/fishing/aquarium/fish_analyzer.dm:130
    public virtual object extract_fish_info(DmList data = null, obj_item_fish fishie = null)
    {
        DmList fish_traits = Dm.list();
        DmList fish_evolutions = Dm.list();

        foreach (object evolution_type in Dm.each(fishie.evolution_types))
        {
            datum_fish_evolution evolution = GLOB.fish_evolutions[evolution_type] as datum_fish_evolution;
            Type evolution_fish = evolution.new_fish_type;
            Dm.append(ref fish_evolutions, Dm.list(Dm.list(
                Dm.kv("evolution_name", evolution.name),
                Dm.kv("evolution_icon", Dm.initial(evolution_fish, (atom p) => p.icon)),
                Dm.kv("evolution_icon_state", Dm.initial(evolution_fish, (atom p) => p.icon_state)),
                Dm.kv("evolution_probability", evolution.probability),
                Dm.kv("evolution_conditions", evolution.conditions_note))));
        }

        foreach (object trait_type in Dm.each(fishie.fish_traits))
        {
            datum_fish_trait trait = GLOB.fish_traits[trait_type] as datum_fish_trait;
            Dm.append(ref fish_traits, Dm.list(Dm.list(Dm.kv("trait_name", trait.name), Dm.kv("trait_desc", trait.catalog_description), Dm.kv("trait_inherit", trait.inheritability))));
        }

        DmList fish_list = data["fish_list"] as DmList;
        Dm.append(ref fish_list, Dm.list(Dm.list(
            Dm.kv("fish_name", fishie.name),
            Dm.kv("fish_icon", fishie.icon),
            Dm.kv("fish_icon_state", fishie.base_icon_state),
            // tg reads the name and colour of the food's typepath (fishie.food.name, fishie.food::color): its prototype's
            Dm.kv("fish_food", Dm.initial(fishie.food, (datum_reagent p) => p.name)),
            Dm.kv("fish_food_color", Dm.initial(fishie.food, (datum_reagent p) => p.color)),
            Dm.kv("fish_min_temp", fishie.required_temperature_min),
            Dm.kv("fish_max_temp", fishie.required_temperature_max),
            Dm.kv("fish_fluid_compatible", Dm.bitand(fishie.fish_flags, FISH_FLAG_SAFE_FLUID)),
            Dm.kv("fish_fluid_type", fishie.required_fluid_type),
            Dm.kv("fish_traits", fish_traits),
            Dm.kv("fish_evolutions", fish_evolutions),
            Dm.kv("fish_suitable_temp", Dm.bitand(fishie.fish_flags, FISH_FLAG_SAFE_TEMPERATURE)),
            Dm.kv("fish_health", fishie.status == FISH_DEAD ? 0f : PERCENT(Dm.num(fishie.get_health_percentage()))),
            Dm.kv("fish_size", fishie.size),
            Dm.kv("fish_weight", fishie.weight),
            Dm.kv("fish_hunger", HAS_TRAIT(fishie, TRAIT_FISH_NO_HUNGER) ? 0f : 1 - Dm.num(fishie.get_hunger())),
            Dm.kv("fish_breed_timer", round(max(Dm.num(fishie.breeding_wait) - world.time, 0) / 10)))));

        return data;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/fishing/aquarium/fish_analyzer.dm:33  Initialize's var/static/list/fishe_signals (rules 10.6)
    internal DmList obj_item_fish_analyzer__Initialize__fishe_signals;
}
