// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/cups/drinkingglass.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H3 (D333, the bar): the whole file: the drinking glass that takes its drink's look (the glass styles), its custom name that a
// wash removes, the shot glass and the filled glasses. The var blocks are generated (D111).

// tg: code/modules/reagents/reagent_containers/cups/drinkingglass.dm:1
public partial class obj_item_reagent_containers_cup_glass_drinkingglass : obj_item_reagent_containers_cup_glass
{
    // tg: code/modules/reagents/reagent_containers/cups/drinkingglass.dm:26
    [DmParams("mapload", "vol")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_takes_reagent_appearance),
            Dm.@new<datum_callback>(this, nameof(on_cup_change)),
            Dm.@new<datum_callback>(this, nameof(on_cup_reset)),
            Dm.named("base_container_type", base_container_type)));
        RegisterSignal(this, COMSIG_COMPONENT_CLEAN_ACT, nameof(on_cleaned));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinkingglass.dm:36
    public override void on_reagent_change(datum holder = null, params object[] __extra)
    {
        base.on_reagent_change(holder, __extra);
        if (!Dm.truthy(length(((datum_reagents)reagents).reagent_list)))
        {
            qdel(GetComponent(typeof(datum_component_rename)));
            REMOVE_TRAIT(this, TRAIT_WAS_RENAMED, SHAKER_LABEL_TRAIT); //so new drinks can rename the glass
        }
    }

    // Having our icon state change removes fill thresholds
    // tg: code/modules/reagents/reagent_containers/cups/drinkingglass.dm:43
    public override void on_cup_change(datum_glass_style_has_foodtype style = null)
    {
        base.on_cup_change(style);
        fill_icon_thresholds = null;
    }

    // And having our icon reset restores our fill thresholds
    // tg: code/modules/reagents/reagent_containers/cups/drinkingglass.dm:48
    public override void on_cup_reset()
    {
        base.on_cup_reset();
        fill_icon_thresholds ??= Dm.list(0);
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinkingglass.dm:52
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (HAS_TRAIT(this, TRAIT_WAS_RENAMED))
            Dm.append(ref dot, span_notice(I18n.T("This glass has been given a custom name. It can be removed by washing it.")));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinkingglass.dm:57
    [SignalHandler]
    public virtual object on_cleaned(obj source_component = null, obj source = null)
    {
        if (!HAS_TRAIT(this, TRAIT_WAS_RENAMED))
            return NONE;

        qdel(GetComponent(typeof(datum_component_rename)));
        REMOVE_TRAIT(this, TRAIT_WAS_RENAMED, SHAKER_LABEL_TRAIT);
        name = Dm.initial(GetType(), (obj_item_reagent_containers_cup_glass_drinkingglass g) => g.name);
        desc = Dm.initial(GetType(), (obj_item_reagent_containers_cup_glass_drinkingglass g) => g.desc);
        update_appearance(Dm.bitor(UPDATE_NAME, UPDATE_DESC));
        return Dm.bitor(COMPONENT_CLEANED, COMPONENT_CLEANED_GAIN_XP);
    }
}

//Shot glasses!//
//  This lets us add shots in here instead of lumping them in with drinks because >logic  //
//  The format for shots is the exact same as iconstates for the drinking glass, except you use a shot glass instead.  //
// tg: code/modules/reagents/reagent_containers/cups/drinkingglass.dm:76
public partial class obj_item_reagent_containers_cup_glass_drinkingglass_shotglass : obj_item_reagent_containers_cup_glass_drinkingglass
{
    // tg: code/modules/reagents/reagent_containers/cups/drinkingglass.dm:90
    public override object update_name(float? updates = null)
    {
        if (HAS_TRAIT(this, TRAIT_WAS_RENAMED))
            return null;
        object dot = base.update_name(updates);
        name = Dm.text("[]shot glass", Dm.truthy(length(((datum_reagents)reagents).reagent_list)) ? "filled " : "");
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinkingglass.dm:96
    public override object update_desc(float? updates = null)
    {
        if (HAS_TRAIT(this, TRAIT_WAS_RENAMED))
            return null;
        object dot = base.update_desc(updates);
        if (Dm.truthy(length(((datum_reagents)reagents).reagent_list)))
            desc = "The challenge is not taking as many as you can, but guessing what it is before you pass out.";
        else
            desc = "A shot glass - the universal symbol for bad decisions.";
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/cups/drinkingglass.dm:105
public partial class obj_item_reagent_containers_cup_glass_drinkingglass_filled : obj_item_reagent_containers_cup_glass_drinkingglass
{
    // tg: code/modules/reagents/reagent_containers/cups/drinkingglass.dm:108
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/cups/drinkingglass.dm:128
public partial class obj_item_reagent_containers_cup_glass_drinkingglass_filled_half_full : obj_item_reagent_containers_cup_glass_drinkingglass_filled
{
    // tg: code/modules/reagents/reagent_containers/cups/drinkingglass.dm:133
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = Dm.text("[] glass of water", pick("half full", "half empty"));
        return dot;
    }
}
