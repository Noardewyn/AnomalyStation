// Translated from tgstation (snapshot 1d99bd7f): code/modules/vending/cola.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the whole file's procs (the vending shortage, the spiked drinks station trait; Shambler's random languages). The var
// blocks are generated.

// tg: code/modules/vending/cola.dm:3
public partial class obj_machinery_vending_cola : obj_machinery_vending
{
    // tg: code/modules/vending/cola.dm:39  var/static/list/spiking_booze (rules 10.6: a type-level static lives in TgState; a list of typepaths, built at first use)
    public static DmList spiking_booze => TgState.Current.obj_machinery_vending_cola__spiking_booze ??= Dm.list(
        // Your "common" spiking booze
        Dm.kv(typeof(datum_reagent_consumable_ethanol_vodka), 5),
        Dm.kv(typeof(datum_reagent_consumable_ethanol_beer), 5),
        Dm.kv(typeof(datum_reagent_consumable_ethanol_whiskey), 5),
        Dm.kv(typeof(datum_reagent_consumable_ethanol_gin), 5),
        Dm.kv(typeof(datum_reagent_consumable_ethanol_rum), 5),
        // A bit rarer, can be dangerous if you take too much
        Dm.kv(typeof(datum_reagent_consumable_ethanol_thirteenloko), 3),
        Dm.kv(typeof(datum_reagent_consumable_ethanol_absinthe), 3),
        Dm.kv(typeof(datum_reagent_consumable_ethanol_hooch), 3),
        Dm.kv(typeof(datum_reagent_consumable_ethanol_moonshine), 3),
        // Gets funky here
        Dm.kv(typeof(datum_reagent_consumable_ethanol_beepsky_smash), 1),
        Dm.kv(typeof(datum_reagent_consumable_ethanol_gargle_blaster), 1),
        Dm.kv(typeof(datum_reagent_consumable_ethanol_neurotoxin), 1)
    );

    // tg: code/modules/vending/cola.dm:57
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload) || !Dm.truthy(is_station_level(z)) || !HAS_TRAIT(SSstation, STATION_TRAIT_VENDING_SHORTAGE))
            return dot;

        foreach (datum_data_vending_product product_record in Dm.each_cast<datum_data_vending_product>(Dm.add(Dm.add(product_records, coin_records), hidden_records)))
        {
            product_record.amount = 0;
            credits_contained += rand(1, 5);
        }
        return dot;
    }

    // tg: code/modules/vending/cola.dm:66
    public override async DmTask on_dispense(obj_item vended_item = null, float? dispense_returned = null)
    {
        // Only apply to newly dispensed items
        if (Dm.truthy(dispense_returned))
            return;
        // 35% chance that your drink will be safe, as safe pure acid and sugar that these drinks probably are can be
        if (!Dm.truthy(onstation) || !HAS_TRAIT(SSstation, STATION_TRAIT_SPIKED_DRINKS) || !prob(65))
            return;
        // Don't fill booze with more booze
        datum_reagents reagents = vended_item.reagents as datum_reagents;
        if (reagents == null || Dm.truthy(reagents.has_reagent(typeof(datum_reagent_consumable_ethanol), check_subtypes: TRUE)))
            return;
        object removed_volume = await reagents.remove_all(rand(5, Dm.num(reagents.maximum_volume) * 0.5f));
        if (!Dm.truthy(removed_volume))
            return;
        // Don't want bubbling sodas when we add some rum to cola
        ADD_TRAIT(vended_item, TRAIT_SILENT_REACTIONS, VENDING_MACHINE_TRAIT);
        await reagents.add_reagent(pick_weight(spiking_booze) as Type, removed_volume);
        await reagents.handle_reactions();
        REMOVE_TRAIT(vended_item, TRAIT_SILENT_REACTIONS, VENDING_MACHINE_TRAIT);
    }
}

// tg: code/modules/vending/cola.dm:145
public partial class obj_machinery_vending_cola_shamblers : obj_machinery_vending_cola
{
    // tg: code/modules/vending/cola.dm:171
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_active_language(get_random_spoken_language());
        return dot;
    }

    // tg: code/modules/vending/cola.dm:175
    public override async DmTask<object> speak(object message = null)
    {
        object dot = await base.speak(message);
        set_active_language(get_random_spoken_language());
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/vending/cola.dm:39  the cola vendor's spiking booze
    internal DmList obj_machinery_vending_cola__spiking_booze;
}
