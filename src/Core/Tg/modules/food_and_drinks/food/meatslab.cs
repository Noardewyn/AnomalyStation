// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/meatslab.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed with its module (a later milestone). Holds the [DmPath] of a type that an M2 slot signature or
// var block names, in the earliest .dme file that declares it (rules 2.3); written by the M2 contracts step
// (docs/ai/specs/m2-ownership.md). Only the type is declared.

// tg: code/modules/food_and_drinks/food/meatslab.dm:1
[DmPath("/obj/item/food/meat")]
public partial class obj_item_food_meat : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:10 (gaps H2: the meat's blood walk and bloody spreader)
    [DmParams("mapload", "blood_dna_list")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        DmList blood_dna_list = Dm.arg(__extra, 0) as DmList ?? Dm.list(Dm.kv("meaty DNA", get_blood_type(typeof(datum_blood_type_meat))));
        object dot = base.Initialize(mapload, Dm.forward(__extra, blood_dna_list));

        if (blood_decal_type == null || !Dm.truthy(Dm.length(custom_materials)))
            return dot;

        Waiting.NoSleep(AddComponent(
            typeof(datum_component_blood_walk),
            Dm.named("blood_type", blood_decal_type),
            Dm.named("blood_spawn_chance", 45),
            Dm.named("transfer_blood_dna", TRUE),
            Dm.named("max_blood", Dm.num(custom_materials[custom_materials[1]]) / SHEET_MATERIAL_AMOUNT),
            Dm.named("blood_dna_info", blood_dna_list)
        ));

        Waiting.NoSleep(AddComponent(
            typeof(datum_component_bloody_spreader),
            Dm.named("blood_left", Dm.num(custom_materials[custom_materials[1]]) / SHEET_MATERIAL_AMOUNT),
            Dm.named("blood_dna", blood_dna_list)
        ));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:31
[DmPath("/obj/item/food/meat/slab")]
public partial class obj_item_food_meat_slab : obj_item_food_meat
{
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:209
public partial class obj_item_food_meat_slab_bugmeat : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:214
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_raptor_food), Dm.named("health_modifier", 2f), Dm.named("color_chances", string_list(Dm.list(Dm.kv(typeof(datum_raptor_color_green), 2f))))));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:219
public partial class obj_item_food_meat_slab_mouse : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:223
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_MOUSE, CELL_VIRUS_TABLE_GENERIC_MOB));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:229
public partial class obj_item_food_meat_slab_corgi : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:233
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_CORGI, CELL_VIRUS_TABLE_GENERIC_MOB));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:238
public partial class obj_item_food_meat_slab_mothroach : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:242
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_VIRUS_TABLE_GENERIC_MOB));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:247
public partial class obj_item_food_meat_slab_pug : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:251
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_PUG, CELL_VIRUS_TABLE_GENERIC_MOB));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:277
public partial class obj_item_food_meat_slab_bear : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:290
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_BEAR, CELL_VIRUS_TABLE_GENERIC_MOB));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:468
public partial class obj_item_food_meat_slab_chicken : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:481
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_CHICKEN, CELL_VIRUS_TABLE_GENERIC_MOB));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:58
[DmPath("/obj/item/food/meat/slab/human")]
public partial class obj_item_food_meat_slab_human : obj_item_food_meat_slab
{
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_meat_slab : obj_item_food_meat
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:47
    public override async DmTask make_dryable()
    {
        await AddElement(typeof(datum_element_dryable), typeof(obj_item_food_sosjerky_healthy));
    }

    // tg: code/modules/food_and_drinks/food/meatslab.dm:50
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_steak_plain), rand(30 * SECONDS, 90 * SECONDS), TRUE, TRUE); //Add medium rare later maybe?
    }

    // tg: code/modules/food_and_drinks/food/meatslab.dm:53
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_meat_rawcutlet_plain), MEATSLAB_PROCESSED_AMOUNT, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

public partial class obj_item_food_meat_slab_gondola : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:415
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_meat_rawcutlet_gondola), 3, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }

    // tg: code/modules/food_and_drinks/food/meatslab.dm:418
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_steak_gondola), rand(30 * SECONDS, 90 * SECONDS), TRUE, TRUE); //Add medium rare later maybe?
    }
}

public partial class obj_item_food_meat_slab_human : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:64
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_steak_plain_human), rand(30 * SECONDS, 90 * SECONDS), TRUE, TRUE); //Add medium rare later maybe?
    }

    // tg: code/modules/food_and_drinks/food/meatslab.dm:67
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_meat_rawcutlet_plain_human), 3, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

public partial class obj_item_food_meat_slab_human_mutant_lizard : obj_item_food_meat_slab_human_mutant
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:110
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_steak_plain_human_lizard), rand(30 * SECONDS, 90 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meat_slab_human_mutant_skeleton : obj_item_food_meat_slab_human_mutant
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:157
    public override async DmTask make_processable()
    {
        return; //skeletons don't have cutlets
    }

    // tg: code/modules/food_and_drinks/food/meatslab.dm:160
    public override async DmTask make_grillable()
    {
        return;
    }
}

public partial class obj_item_food_meat_slab_synthmeat : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:189
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_steak_plain_synth), rand(30 * SECONDS, 90 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meat_slab_meatproduct : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:200
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_steak_meatproduct), rand(30 * SECONDS, 90 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meat_slab_killertomato : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:265
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_steak_killertomato), rand(70 * SECONDS, 85 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/food_and_drinks/food/meatslab.dm:268
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_meat_rawcutlet_killertomato), 3, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

public partial class obj_item_food_meat_slab_bear : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:284
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_meat_rawcutlet_bear), 3, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }

    // tg: code/modules/food_and_drinks/food/meatslab.dm:287
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_steak_bear), rand(40 * SECONDS, 70 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meat_slab_xeno : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:307
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_meat_rawcutlet_xeno), 3, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }

    // tg: code/modules/food_and_drinks/food/meatslab.dm:310
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_steak_xeno), rand(40 * SECONDS, 70 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meat_slab_spider : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:326
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_meat_rawcutlet_spider), 3, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }

    // tg: code/modules/food_and_drinks/food/meatslab.dm:329
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_steak_spider), rand(40 * SECONDS, 70 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meat_slab_penguin : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:431
    public override async DmTask make_processable()
    {
        await base.make_processable();
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_meat_rawcutlet_penguin), 3, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }

    // tg: code/modules/food_and_drinks/food/meatslab.dm:435
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_steak_penguin), rand(30 * SECONDS, 90 * SECONDS), TRUE, TRUE); //Add medium rare later maybe?
    }
}

public partial class obj_item_food_meat_slab_rawcrab : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:450
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_crab), rand(30 * SECONDS, 90 * SECONDS), TRUE, TRUE); //Add medium rare later maybe?
    }
}

public partial class obj_item_food_meat_slab_chicken : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:475
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_meat_rawcutlet_chicken), 3, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }

    // tg: code/modules/food_and_drinks/food/meatslab.dm:478
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_steak_chicken), rand(30 * SECONDS, 90 * SECONDS), TRUE, TRUE); //Add medium rare later maybe? (no this is chicken
    }
}

public partial class obj_item_food_meat_slab_pig : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:499
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_steak_plain_pig), rand(30 * SECONDS, 90 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meat_slab_blood_worm : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:526
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_steak_blood_worm), rand(30 * SECONDS, 90 * SECONDS), TRUE, TRUE); //Add medium rare later maybe?
    }
}

public partial class obj_item_food_meat_rawbacon : obj_item_food_meat
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:386
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_bacon), rand(25 * SECONDS, 45 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meat_rawcutlet : obj_item_food_meat
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:667
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_cutlet_plain), rand(35 * SECONDS, 50 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meat_rawcutlet_plain_human : obj_item_food_meat_rawcutlet_plain
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:688
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_cutlet_plain_human), rand(35 * SECONDS, 50 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meat_rawcutlet_killertomato : obj_item_food_meat_rawcutlet
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:709
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_cutlet_killertomato), rand(35 * SECONDS, 50 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meat_rawcutlet_bear : obj_item_food_meat_rawcutlet
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:720
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_cutlet_bear), rand(35 * SECONDS, 50 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meat_rawcutlet_xeno : obj_item_food_meat_rawcutlet
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:728
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_cutlet_xeno), rand(35 * SECONDS, 50 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meat_rawcutlet_spider : obj_item_food_meat_rawcutlet
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:737
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_cutlet_spider), rand(35 * SECONDS, 50 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meat_rawcutlet_gondola : obj_item_food_meat_rawcutlet
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:744
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_cutlet_gondola), rand(35 * SECONDS, 50 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meat_rawcutlet_penguin : obj_item_food_meat_rawcutlet
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:751
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_cutlet_penguin), rand(35 * SECONDS, 50 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meat_rawcutlet_chicken : obj_item_food_meat_rawcutlet
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:758
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_meat_cutlet_chicken), rand(35 * SECONDS, 50 * SECONDS), TRUE, TRUE);
    }
}

// Gaps H2 (D333): the cooked meats' own procs (the steaks' and cutlets' names, the goliath meat).

// tg: code/modules/food_and_drinks/food/meatslab.dm:332
public partial class obj_item_food_meat_slab_goliath : obj_item_food_meat_slab
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:345
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_raptor_food), Dm.named("attack_modifier", 0.5f), Dm.named("speed_modifier", -0.05f), Dm.named("color_chances", string_list(Dm.list(Dm.kv(typeof(datum_raptor_color_red), 5))))));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/meatslab.dm:349
    public override async DmTask burn__proc()
    {
        visible_message(span_notice(I18n.T("[] finishes cooking!", this)));
        Dm.@new<obj_item_food_meat_steak_goliath>(loc);
        qdel(this);
    }
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:530
public partial class obj_item_food_meat_steak : obj_item_food_meat
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:544
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ITEM_MICROWAVE_COOKED_RESULT, nameof(on_microwave_cooked));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/meatslab.dm:548
    [SignalHandler]
    public virtual void on_microwave_cooked(datum source = null, atom source_item = null, float? cooking_efficiency = null)
    {
        name = I18n.T("[] steak", source_item.name);
    }
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:555
public partial class obj_item_food_meat_steak_plain_human : obj_item_food_meat_steak_plain
{
    ///Make sure the steak has the correct name
    // tg: code/modules/food_and_drinks/food/meatslab.dm:560
    public override void on_microwave_cooked(datum source = null, atom source_item = null, float? cooking_efficiency = null)
    {
        base.on_microwave_cooked(source, source_item, cooking_efficiency);
        if (source_item is not obj_item_food_meat)
            return;

        obj_item_food_meat origin_meat = (obj_item_food_meat)source_item;
        subjectname = origin_meat.subjectname;
        subjectjob = origin_meat.subjectjob;
        if (Dm.truthy(subjectname))
            name = I18n.T("[] meatsteak", origin_meat.subjectname);
        else if (Dm.truthy(subjectjob))
            name = I18n.T("[] meatsteak", origin_meat.subjectjob);
    }
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:592
public partial class obj_item_food_meat_steak_goliath : obj_item_food_meat_steak
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:600
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_raptor_food), Dm.named("attack_modifier", 1), Dm.named("speed_modifier", -0.075f), Dm.named("color_chances", string_list(Dm.list(Dm.kv(typeof(datum_raptor_color_red), 7))))));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:652
public partial class obj_item_food_meat_rawcutlet : obj_item_food_meat
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:663
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_food_storage)));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/meatslab.dm:670
    public override async DmTask OnCreatedFromProcessing(mob_living user = null, obj_item work_tool = null, DmList chosen_option = null, atom original_atom = null)
    {
        await base.OnCreatedFromProcessing(user, work_tool, chosen_option, original_atom);
        if (original_atom is not obj_item_food_meat_slab)
            return;
        obj_item_food_meat_slab original_slab = (obj_item_food_meat_slab)original_atom;
        mutable_appearance filling = mutable_appearance(icon, "rawcutlet_coloration") as mutable_appearance;
        filling.color = original_slab.slab_color;
        add_overlay(filling);
        name = I18n.T("raw [] cutlet", original_atom.name);
        meat_type = original_atom.name;
    }
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:684
public partial class obj_item_food_meat_rawcutlet_plain_human : obj_item_food_meat_rawcutlet_plain
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:691
    public override async DmTask OnCreatedFromProcessing(mob_living user = null, obj_item item = null, DmList chosen_option = null, atom original_atom = null)
    {
        await base.OnCreatedFromProcessing(user, item, chosen_option, original_atom);
        if (original_atom is not obj_item_food_meat)
            return;
        obj_item_food_meat origin_meat = (obj_item_food_meat)original_atom;
        subjectname = origin_meat.subjectname;
        subjectjob = origin_meat.subjectjob;
        if (Dm.truthy(subjectname))
            name = I18n.T("raw [] cutlet", origin_meat.subjectname);
        else if (Dm.truthy(subjectjob))
            name = I18n.T("raw [] cutlet", origin_meat.subjectjob);
    }
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:712
public partial class obj_item_food_meat_rawcutlet_bear : obj_item_food_meat_rawcutlet
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:716
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_BEAR, CELL_VIRUS_TABLE_GENERIC_MOB));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:754
public partial class obj_item_food_meat_rawcutlet_chicken : obj_item_food_meat_rawcutlet
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:761
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_CHICKEN, CELL_VIRUS_TABLE_GENERIC_MOB));
        return dot;
    }
}

//Cooked cutlets

// tg: code/modules/food_and_drinks/food/meatslab.dm:767
public partial class obj_item_food_meat_cutlet : obj_item_food_meat
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:779  (the earlier of the two definitions in this type; the later one at
    // :783 calls it through ..(), rules 2.5)
    public object Initialize__d0(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ITEM_MICROWAVE_COOKED_RESULT, nameof(on_microwave_cooked));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/meatslab.dm:783
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = Initialize__d0(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_food_storage)));
        return dot;
    }

    ///This proc handles setting up the correct meat name for the cutlet, this should definitely be changed with the food rework.
    // tg: code/modules/food_and_drinks/food/meatslab.dm:788
    [SignalHandler]
    public virtual void on_microwave_cooked(datum source = null, atom source_item = null, object cooking_efficiency = null)
    {
        if (source_item is not obj_item_food_meat_rawcutlet)
            return;

        obj_item_food_meat_rawcutlet original_cutlet = (obj_item_food_meat_rawcutlet)source_item;
        name = I18n.T("[] cutlet", original_cutlet.meat_type);
    }
}

// tg: code/modules/food_and_drinks/food/meatslab.dm:799
public partial class obj_item_food_meat_cutlet_plain_human : obj_item_food_meat_cutlet_plain
{
    // tg: code/modules/food_and_drinks/food/meatslab.dm:803
    public override void on_microwave_cooked(datum source = null, atom source_item = null, object cooking_efficiency = null)
    {
        base.on_microwave_cooked(source, source_item, cooking_efficiency);
        if (source_item is not obj_item_food_meat)
            return;

        obj_item_food_meat origin_meat = (obj_item_food_meat)source_item;
        if (Dm.truthy(subjectname))
            name = I18n.T("[] []", origin_meat.subjectname, Dm.initial(this, (atom a) => a.name));
        else if (Dm.truthy(subjectjob))
            name = I18n.T("[] []", origin_meat.subjectjob, Dm.initial(this, (atom a) => a.name));
    }
}
