// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/quality_food_ingredient.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333: what is baked, microwaved, grilled, fried or dried from a quality ingredient keeps its
// quality). The var block is generated (D111).

///An element that adds extra food quality to any edible that was made from an atom with this attached.
// tg: code/datums/elements/quality_food_ingredient.dm:2
public partial class datum_element_quality_food_ingredient : datum_element
{
    // tg: code/datums/elements/quality_food_ingredient.dm:8
    [DmParams("target", "complexity_increase")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object complexity_increase = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, __extra);
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;
        if (HAS_TRAIT_FROM(target, TRAIT_QUALITY_FOOD_INGREDIENT, REF(this))) //It already has this element attached.
            return dot;

        this.complexity_increase = Dm.num(complexity_increase);

        RegisterSignal(target, COMSIG_ATOM_USED_IN_CRAFT, nameof(used_in_craft));
        RegisterSignal(target, COMSIG_ITEM_BAKED, nameof(item_baked));
        RegisterSignal(target, COMSIG_ITEM_MICROWAVE_COOKED, nameof(microwaved_from));
        RegisterSignal(target, COMSIG_ITEM_GRILLED, nameof(item_grilled));
        RegisterSignal(target, COMSIG_ITEM_DRIED, nameof(item_dried));
        RegisterSignals(target, Dm.list(COMSIG_ITEM_BARBEQUE_GRILLED, COMSIG_ITEM_FRIED), nameof(simply_cooked));
        RegisterSignal(target, COMSIG_ITEM_USED_AS_INGREDIENT, nameof(used_as_ingredient));
        return dot;
    }

    // tg: code/datums/elements/quality_food_ingredient.dm:25
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, Dm.list(
            COMSIG_ATOM_USED_IN_CRAFT,
            COMSIG_ITEM_BAKED,
            COMSIG_ITEM_MICROWAVE_COOKED,
            COMSIG_ITEM_GRILLED,
            COMSIG_ITEM_BARBEQUE_GRILLED,
            COMSIG_ITEM_FRIED,
            COMSIG_ITEM_USED_AS_INGREDIENT,
            COMSIG_ITEM_DRIED,
            COMSIG_FOOD_GET_EXTRA_COMPLEXITY
        ));
        REMOVE_TRAIT(source, TRAIT_QUALITY_FOOD_INGREDIENT, REF(this));
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/quality_food_ingredient.dm:40
    [SignalHandler]
    public virtual void used_in_craft(datum source = null, atom result = null)
    {
        Waiting.NoSleep(add_quality(result));
    }

    // tg: code/datums/elements/quality_food_ingredient.dm:44
    [SignalHandler]
    public virtual void item_baked(datum source = null, atom baked_result = null)
    {
        Waiting.NoSleep(add_quality(baked_result));
    }

    // tg: code/datums/elements/quality_food_ingredient.dm:48
    [SignalHandler]
    public virtual void microwaved_from(datum source = null, atom result = null)
    {
        Waiting.NoSleep(add_quality(result));
    }

    // tg: code/datums/elements/quality_food_ingredient.dm:52
    [SignalHandler]
    public virtual void item_grilled(datum source = null, atom grill_result = null)
    {
        Waiting.NoSleep(add_quality(grill_result));
    }

    // tg: code/datums/elements/quality_food_ingredient.dm:56
    [SignalHandler]
    public virtual void item_dried(datum source = null, atom result = null)
    {
        Waiting.NoSleep(add_quality(result));
    }

    // tg: code/datums/elements/quality_food_ingredient.dm:60
    [SignalHandler]
    public virtual void simply_cooked(datum source = null)
    {
        //The target of the food quality and the source are the same, there's no need to re-add the whole element.
        RegisterSignal(source, COMSIG_FOOD_GET_EXTRA_COMPLEXITY, nameof(add_complexity), TRUE);
        ADD_TRAIT(source, TRAIT_QUALITY_FOOD_INGREDIENT, REF(this));
    }

    // tg: code/datums/elements/quality_food_ingredient.dm:66
    [SignalHandler]
    public virtual void used_as_ingredient(datum source = null, atom container = null)
    {
        Waiting.NoSleep(add_quality(container));
    }

    // tg: code/datums/elements/quality_food_ingredient.dm:70
    public virtual async DmTask add_quality(atom target = null)
    {
        await target.AddElement(typeof(datum_element_quality_food_ingredient), complexity_increase);
        RegisterSignal(target, COMSIG_FOOD_GET_EXTRA_COMPLEXITY, nameof(add_complexity), TRUE);
        ADD_TRAIT(target, TRAIT_QUALITY_FOOD_INGREDIENT, REF(this));
    }

    // tg: code/datums/elements/quality_food_ingredient.dm:75
    [SignalHandler]
    public virtual void add_complexity(datum source = null, DmList complexity = null)
    {
        complexity[1] = Dm.num(complexity[1]) + complexity_increase;
    }
}
