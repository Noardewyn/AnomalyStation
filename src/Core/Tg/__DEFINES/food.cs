// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/food.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

// PARTIAL (M5 slice 8): the list macro FOOD_FLAGS (the crafting window's food types); the flags themselves are generated
// (Generated/Defines/__DEFINES/food.g.cs); the other list macros come with the code that uses them. Gaps S1 (D291): the globals
// food_quality_description and food_buffs (the edible component).

public static partial class GlobalProcs
{
    /// A list of food type names, in order of their flags
    // tg: code/__DEFINES/food.dm:49  #define FOOD_FLAGS list(...) (a fresh list at every use, as the macro expands)
    public static DmList FOOD_FLAGS => Dm.list(
        "MEAT",
        "VEGETABLES",
        "RAW",
        "JUNKFOOD",
        "GRAIN",
        "FRUIT",
        "DAIRY",
        "FRIED",
        "ALCOHOL",
        "SUGAR",
        "GROSS",
        "TOXIC",
        "PINEAPPLE",
        "BREAKFAST",
        "CLOTH",
        "NUTS",
        "SEAFOOD",
        "ORANGES",
        "BUGS",
        "GORE",
        "STONE",
        "EGG"
    );
}

// tg: code/__DEFINES/food.dm:127 (interleaved types, rules 1.9)
public partial class datum_controller_global_vars
{
    /// Labels for food quality
    // tg: code/__DEFINES/food.dm:127  GLOBAL_ALIST_INIT(food_quality_description, alist(...))
    public DmList food_quality_description;

    // tg: code/__DEFINES/food.dm:127  (GLOBAL_MANAGED(food_quality_description, alist(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalfood_quality_description()
    {
        food_quality_description = Dm.alist(
            Dm.kv(Defines.FOOD_QUALITY_NORMAL, "okay"),
            Dm.kv(Defines.FOOD_QUALITY_NICE, "nice"),
            Dm.kv(Defines.FOOD_QUALITY_GOOD, "good"),
            Dm.kv(Defines.FOOD_QUALITY_VERYGOOD, "very good"),
            Dm.kv(Defines.FOOD_QUALITY_FANTASTIC, "fantastic"),
            Dm.kv(Defines.FOOD_QUALITY_AMAZING, "amazing"),
            Dm.kv(Defines.FOOD_QUALITY_TOP, "divine"));
        Dm.append(ref gvars_datum_init_order, "food_quality_description");
    }

    /// Weighted lists of crafted food buffs randomly given according to crafting_complexity unless the food has a specific buff
    // tg: code/__DEFINES/food.dm:139  GLOBAL_ALIST_INIT(food_buffs, alist(...))
    public DmList food_buffs;

    // tg: code/__DEFINES/food.dm:139  (GLOBAL_MANAGED(food_buffs, alist(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalfood_buffs()
    {
        food_buffs = Dm.alist(
            Dm.kv(Defines.FOOD_COMPLEXITY_1, Dm.list(Dm.kv(typeof(datum_status_effect_food_haste), 1))),
            Dm.kv(Defines.FOOD_COMPLEXITY_2, Dm.list(Dm.kv(typeof(datum_status_effect_food_haste), 1))),
            Dm.kv(Defines.FOOD_COMPLEXITY_3, Dm.list(Dm.kv(typeof(datum_status_effect_food_haste), 1))),
            Dm.kv(Defines.FOOD_COMPLEXITY_4, Dm.list(Dm.kv(typeof(datum_status_effect_food_haste), 1))),
            Dm.kv(Defines.FOOD_COMPLEXITY_5, Dm.list(Dm.kv(typeof(datum_status_effect_food_haste), 1))));
        Dm.append(ref gvars_datum_init_order, "food_buffs");
    }
}
