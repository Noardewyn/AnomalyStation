// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/boxes/service_boxes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the PopulateContents of Meta's containers in this file (D133), translated with the help of m4/p3w/popgen2.py
// (the pure `new /path(src)` bodies, their loops, prob and switch); the file's other procs are their milestones'.

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:9
public partial class obj_item_storage_box_drinkingglasses : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:9
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 6; i++) // tg: code/game/objects/items/storage/boxes/service_boxes.dm:10
        {
            Dm.@new<obj_item_reagent_containers_cup_glass_drinkingglass>(this); // tg: code/game/objects/items/storage/boxes/service_boxes.dm:11
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:133
public partial class obj_item_storage_box_lights_mixed : obj_item_storage_box_lights
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:133
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 14; i++) // tg: code/game/objects/items/storage/boxes/service_boxes.dm:134
        {
            Dm.@new<obj_item_light_tube>(this); // tg: code/game/objects/items/storage/boxes/service_boxes.dm:135
        }
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/service_boxes.dm:136
        {
            Dm.@new<obj_item_light_bulb>(this); // tg: code/game/objects/items/storage/boxes/service_boxes.dm:137
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:91
public partial class obj_item_storage_box_matches : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:87  (gaps J3, D298)
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_ignites_matches)));
        return dot;
    }

    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:95  (gaps J3, D298)
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        float count = Dm.length(contents);
        if (count == 10)
            icon_state = Dm.str(base_icon_state);
        else if (count >= 5 && count <= 9)
            icon_state = Dm.text("[]_almostfull", base_icon_state);
        else if (count >= 1 && count <= 4)
            icon_state = Dm.text("[]_almostempty", base_icon_state);
        else if (count == 0)
            icon_state = Dm.text("[]_e", base_icon_state);
        return dot;
    }

    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:91
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 10; i++) // tg: code/game/objects/items/storage/boxes/service_boxes.dm:92
        {
            Dm.@new<obj_item_match>(this); // tg: code/game/objects/items/storage/boxes/service_boxes.dm:93
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:53
public partial class obj_item_storage_box_mousetraps : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:53
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 6; i++) // tg: code/game/objects/items/storage/boxes/service_boxes.dm:54
        {
            Dm.@new<obj_item_assembly_mousetrap>(this); // tg: code/game/objects/items/storage/boxes/service_boxes.dm:55
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:193
public partial class obj_item_storage_box_balloons : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:199
    public override DmTask PopulateContents()
    {
        for (int i = 1; i <= 24; i++)
            Dm.@new<obj_item_toy_balloon_long>(this);
        return DmTask.CompletedTask;
    }
}

/// A box containing a skub, for easier carry because skub is a bulky item.
// tg: code/game/objects/items/storage/boxes/service_boxes.dm:257  (gaps R2: the Great Skub Contention station trait's packs)
public partial class obj_item_storage_box_stickers_skub : obj_item_storage_box_stickers
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:264
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_skub>(this);
        Dm.@new<obj_item_sticker_skub>(this);
        Dm.@new<obj_item_sticker_skub>(this);
        await DmTask.CompletedTask;
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:269  (gaps R2)
public partial class obj_item_storage_box_stickers_anti_skub : obj_item_storage_box_stickers
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:276
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 4; i++)
            Dm.@new<obj_item_sticker_anti_skub>(this);
        await DmTask.CompletedTask;
    }
}

// Gaps SR1 (D333): the file's other boxes (cups, PDAs, IDs, snap pops, bulbs and tubes, fountain pens, the dish drive kit, action
// figures, the corgi tail pins, party poppers, googly eyes, pinpointer pairs, the pierced realities, purity seals and stamps).

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:251
public partial class obj_item_storage_box_stickers_googly : obj_item_storage_box_stickers
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:251
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 6; i++)
        {
            Dm.@new<obj_item_sticker_googly>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:17
public partial class obj_item_storage_box_cups : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:17
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_reagent_containers_cup_glass_sillycup>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:27
public partial class obj_item_storage_box_pdas : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:27
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 4; i++)
        {
            Dm.@new<obj_item_modular_computer_pda_crew>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:36
public partial class obj_item_storage_box_ids : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:36
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_card_id_advanced>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:44
public partial class obj_item_storage_box_silver_ids : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:44
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_card_id_advanced_silver>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:65
public partial class obj_item_storage_box_snappops : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:65
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 8; i++)
        {
            Dm.@new<obj_item_toy_snappop>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:117
public partial class obj_item_storage_box_lights_bulbs : obj_item_storage_box_lights
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:117
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 21; i++)
        {
            Dm.@new<obj_item_light_bulb>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:125
public partial class obj_item_storage_box_lights_tubes : obj_item_storage_box_lights
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:125
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 21; i++)
        {
            Dm.@new<obj_item_light_tube>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:143
public partial class obj_item_storage_box_fountainpens : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:143
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_pen_fountain>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:152
public partial class obj_item_storage_box_dishdrive : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:152
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_circuitboard_machine_dish_drive), 1),
            Dm.kv(typeof(obj_item_screwdriver), 1),
            Dm.kv(typeof(obj_item_stack_cable_coil_five), 1),
            Dm.kv(typeof(obj_item_stack_sheet_glass), 1),
            Dm.kv(typeof(obj_item_stack_sheet_iron_five), 1),
            Dm.kv(typeof(obj_item_stock_parts_servo), 1),
            Dm.kv(typeof(obj_item_stock_parts_matter_bin), 2),
            Dm.kv(typeof(obj_item_wrench), 1));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:180
public partial class obj_item_storage_box_tail_pin : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:180
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 3; i++)
        {
            Dm.@new<obj_item_poster_tail_board>(this);
            Dm.@new<obj_item_tail_pin>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:189
public partial class obj_item_storage_box_party_poppers : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:189
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 5; i++)
        {
            Dm.@new<obj_item_reagent_containers_spray_chemsprayer_party>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:303
public partial class obj_item_storage_box_purity_seal_box : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:303
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 4; i++)
        {
            Dm.@new<obj_item_sticker_purity_seal>(this);
            Dm.@new<obj_item_sticker_purity_seal_purity_seal_2>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:165
public partial class obj_item_storage_box_actionfigure : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:170
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 4; i++)
        {
            object random_figure = pick(subtypesof(typeof(obj_item_toy_figure)));
            Dm.@new((Type)random_figure, this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:280
public partial class obj_item_storage_box_pinpointer_pairs : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:283
    public override async DmTask PopulateContents()
    {
        obj_item_pinpointer_pair A = Dm.@new<obj_item_pinpointer_pair>(this);
        obj_item_pinpointer_pair B = Dm.@new<obj_item_pinpointer_pair>(this);
        A.other_pair = B;
        B.other_pair = A;
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:290
public partial class obj_item_storage_box_heretic_box : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:294
    public override async DmTask PopulateContents()
    {
        float count = rand(1, 4);
        for (float i = 1; i <= count; i++)
            Dm.@new<obj_item_toy_reality_pierce>(this);
    }
}

// The static list of the stamp box (rules 10.6: proc-level statics live in TgState).
public sealed partial class TgState
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:315  var/static/items_inside
    internal DmList obj_item_storage_box_stamps__PopulateContents__items_inside = Dm.list(
            Dm.kv(typeof(obj_item_stamp_granted), 1),
            Dm.kv(typeof(obj_item_stamp_denied), 1),
            Dm.kv(typeof(obj_item_stamp_void), 1));
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:308
public partial class obj_item_storage_box_stamps : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:314
    public override async DmTask PopulateContents()
    {
        DmList items_inside = TgState.Current.obj_item_storage_box_stamps__PopulateContents__items_inside;
        generate_items_inside(items_inside, this);
    }
}
