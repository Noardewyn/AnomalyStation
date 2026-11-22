// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/pie.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate G4b, D316): the banana cream pie that splats (the pie cannon's ammo); the other pies come with the kitchen.
// The var blocks are generated (D111).

// tg: code/modules/food_and_drinks/food/pie.dm:61
public partial class obj_item_food_pie_cream : obj_item_food_pie
{
    // tg: code/modules/food_and_drinks/food/pie.dm:74
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_splat), Dm.named("hit_callback", Dm.@new<datum_callback>(this, nameof(stun_and_blur)))));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/pie.dm:78
    public virtual async DmTask stun_and_blur(mob_living victim = null, object can_splat_on = null)
    {
        if (Dm.truthy(stunning))
            await victim.Paralyze(2 * SECONDS); //splat!
        if (Dm.truthy(can_splat_on))
            await victim.adjust_eye_blur(2 * SECONDS);
        victim.visible_message(span_warning(I18n.T("[] is creamed by []!", victim, this)), span_userdanger(I18n.T("You've been creamed by []!", this)));
        playsound(victim, SFX_DESECRATION, 50, TRUE);
    }
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_pie : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/pie.dm:17
    public override async DmTask make_processable()
    {
        if (slice_type != null)
            await AddElement(typeof(datum_element_processable), TOOL_KNIFE, slice_type, yield, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

// Gaps H2 (D333): the dough, bread, cake and pie bases' storage and custom-food holders (food_storage, ingredients_holder).

// tg: code/modules/food_and_drinks/food/pie.dm:30
public partial class obj_item_food_pie_plain : obj_item_food_pie
{
    // tg: code/modules/food_and_drinks/food/pie.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), typeof(obj_item_food_pie_empty), CUSTOM_INGREDIENT_ICON_FILL, Dm.named("max_ingredients", 8)));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/pie.dm:51
public partial class obj_item_food_pieslice_empty : obj_item_food_pieslice
{
    // tg: code/modules/food_and_drinks/food/pie.dm:56
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), null, CUSTOM_INGREDIENT_ICON_FILL, Dm.named("max_ingredients", 8)));
        return dot;
    }
}
