// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/bread.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate G4a, D316): the baguette and the mime's swordplay with it, the combat baguette and the throwing croissant of
// the combat bakery kit; the other breads come with the kitchen. The screentips (add_context) are a live-partial row (D264). The
// var blocks are generated (D111).

// tg: code/modules/food_and_drinks/food/bread.dm:341
public partial class obj_item_food_baguette : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/bread.dm:364
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // add_context (:368): the screentips (not now, by the owner, D264)

    // tg: code/modules/food_and_drinks/food/bread.dm:374
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (HAS_MIND_TRAIT(user, TRAIT_MIMING))
            dot.Add(span_notice(I18n.T("You can wield this like a sword by using it in your hand.")));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/bread.dm:379
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (!HAS_MIND_TRAIT(user, TRAIT_MIMING))
            return dot;
        if (Dm.truthy(fake_swordplay))
            end_swordplay(user);
        else
            begin_swordplay(user);
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/bread.dm:388
    public virtual void begin_swordplay(mob user = null)
    {
        visible_message(
            span_notice(I18n.T("[] begins wielding [] like a sword!", user, this)),
            span_notice(I18n.T("You begin wielding [] like a sword, with a firm grip on the bottom as an imaginary handle.", this)));
        ADD_TRAIT(this, TRAIT_CUSTOM_TAP_SOUND, SWORDPLAY_TRAIT);
        attack_verb_continuous = Dm.list("slashes", "cuts");
        attack_verb_simple = Dm.list("slash", "cut");
        hitsound = Dm.resource("sound/items/weapons/rapierhit.ogg");
        fake_swordplay = TRUE;

        RegisterSignal(this, COMSIG_ITEM_EQUIPPED, nameof(on_sword_equipped));
        RegisterSignal(this, COMSIG_ITEM_DROPPED, nameof(on_sword_dropped));
    }

    // tg: code/modules/food_and_drinks/food/bread.dm:402
    public virtual void end_swordplay(mob user = null)
    {
        UnregisterSignal(this, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED));

        REMOVE_TRAIT(this, TRAIT_CUSTOM_TAP_SOUND, SWORDPLAY_TRAIT);
        attack_verb_continuous = Dm.initial(this, (obj_item_food_baguette p) => p.attack_verb_continuous);
        attack_verb_simple = Dm.initial(this, (obj_item_food_baguette p) => p.attack_verb_simple);
        hitsound = Dm.initial(this, (obj_item_food_baguette p) => p.hitsound);
        fake_swordplay = FALSE;

        if (user != null)
        {
            visible_message(
                span_notice(I18n.T("[] no longer holds [] like a sword!", user, this)),
                span_notice(I18n.T("You go back to holding [] normally.", this)));
        }
    }

    // tg: code/modules/food_and_drinks/food/bread.dm:417
    [SignalHandler]
    public virtual void on_sword_dropped(datum source = null, mob user = null)
    {
        end_swordplay();
    }

    // tg: code/modules/food_and_drinks/food/bread.dm:422
    [SignalHandler]
    public virtual void on_sword_equipped(datum source = null, mob equipper = null, object slot = null)
    {
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS)))
            end_swordplay();
    }
}

/// Deadly bread used by a mime
// tg: code/modules/food_and_drinks/food/bread.dm:429
public partial class obj_item_food_baguette_combat : obj_item_food_baguette
{
    // tg: code/modules/food_and_drinks/food/bread.dm:438
    public override void begin_swordplay(mob user = null)
    {
        base.begin_swordplay(user);
        force = active_force;
        block_chance = active_block;
    }

    // tg: code/modules/food_and_drinks/food/bread.dm:443
    public override void end_swordplay(mob user = null)
    {
        base.end_swordplay(user);
        force = Dm.initial(this, (obj_item_food_baguette_combat p) => p.force);
        block_chance = Dm.initial(this, (obj_item_food_baguette_combat p) => p.block_chance);
    }
}

// Enhanced weaponised bread
// tg: code/modules/food_and_drinks/food/bread.dm:589
public partial class obj_item_food_croissant_throwing : obj_item_food_croissant
{
    // tg: code/modules/food_and_drinks/food/bread.dm:594
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_boomerang), throw_range, TRUE));
        return dot;
    }
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_breadslice_plain : obj_item_food_breadslice
{
    // tg: code/modules/food_and_drinks/food/bread.dm:73
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_griddle_toast), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_raw_frenchtoast : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/bread.dm:515
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_frenchtoast), rand(20 * SECONDS, 30 * SECONDS), TRUE);
    }
}

public partial class obj_item_food_raw_breadstick : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/bread.dm:546
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_breadstick), rand(15 * SECONDS, 20 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_raw_croissant : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/bread.dm:574
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_croissant), rand(15 * SECONDS, 20 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_bread : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/bread.dm:23
    public override async DmTask make_processable()
    {
        if (slice_type != null)
        {
            await AddElement(typeof(datum_element_processable), TOOL_KNIFE, slice_type, yield, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
            await AddElement(typeof(datum_element_processable), TOOL_SAW, slice_type, yield, 4 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"));
        }
    }
}

// Gaps H2 (D333): the dough, bread, cake and pie bases' storage and custom-food holders (food_storage, ingredients_holder).

// tg: code/modules/food_and_drinks/food/bread.dm:3
public partial class obj_item_food_bread : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/bread.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_dunkable), 10));
        Waiting.NoSleep(AddComponent(typeof(datum_component_food_storage)));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/bread.dm:29
public partial class obj_item_food_breadslice : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/bread.dm:40
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_dunkable), 10));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/bread.dm:44
public partial class obj_item_food_bread_plain : obj_item_food_bread
{
    // tg: code/modules/food_and_drinks/food/bread.dm:55
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), typeof(obj_item_food_bread_empty), CUSTOM_INGREDIENT_ICON_FILL, Dm.named("max_ingredients", 8)));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/bread.dm:59
public partial class obj_item_food_breadslice_plain : obj_item_food_breadslice
{
    // tg: code/modules/food_and_drinks/food/bread.dm:69
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), null, CUSTOM_INGREDIENT_ICON_STACK));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/bread.dm:89
public partial class obj_item_food_breadslice_moldy_bacteria : obj_item_food_breadslice_moldy
{
    // tg: code/modules/food_and_drinks/food/bread.dm:94
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_MOLD, CELL_VIRUS_TABLE_GENERIC, rand(2, 4), 25));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/bread.dm:331
public partial class obj_item_food_breadslice_empty : obj_item_food_breadslice
{
    // tg: code/modules/food_and_drinks/food/bread.dm:337
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), null, CUSTOM_INGREDIENT_ICON_FILL, Dm.named("max_ingredients", 8)));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/bread.dm:480
public partial class obj_item_food_butterdog : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/bread.dm:496
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_slippery), 8 * SECONDS));
        return dot;
    }
}
