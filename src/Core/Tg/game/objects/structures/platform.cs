// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/platform.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST4 (D333): the file whole. The raised platforms (iron, rusty, the shuttle's titanium and plastitanium, the material ones,
// silver, gold, bronze, uranium, wood and the stage, bamboo, sandstone, cult, pizza, paper): climbed onto and walked across like a
// table (the climb_walkable, climbable and elevation elements: twelve pixels up), their turf ignoring slowdown, slips and immersion,
// the table's footstep and smash; smoothed with their kind and drawn with their frame's face overlay; taken apart with a screwdriver
// or a wrench on the right button into their sheets or their materials' sheets. The var block is generated (D111).

/// A raised platform you can stand on top of
// tg: code/game/objects/structures/platform.dm:4
public partial class obj_structure_platform : obj_structure
{
    // tg: code/game/objects/structures/platform.dm:43
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        register_context();
        update_appearance(UPDATE_OVERLAYS);
        Waiting.NoSleep(AddElement(typeof(datum_element_climb_walkable)));
        Waiting.NoSleep(AddElement(typeof(datum_element_climbable)));
        Waiting.NoSleep(AddElement(typeof(datum_element_elevation), Dm.named("pixel_shift", 12)));
        Waiting.NoSleep(AddElement(typeof(datum_element_give_turf_traits), string_list(TgState.Current.obj_structure_platform__turf_traits)));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep_override), Dm.named("footstep", footstep), Dm.named("priority", STEP_SOUND_TABLE_PRIORITY)));
        Waiting.NoSleep(AddElement(typeof(datum_element_table_smash)));
        return dot;
    }

    // tg: code/game/objects/structures/platform.dm:55
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);

        if (held_item == null)
            return NONE;

        if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_RMB] = "Disassemble";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_RMB] = "Deconstruct";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }

        return Dm.truthy(dot) ? dot : NONE;
    }

    // tg: code/game/objects/structures/platform.dm:70
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        to_chat(user, span_notice(I18n.T("You start disassembling []...", this)));
        if (Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 50)))
            await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/platform.dm:76
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        to_chat(user, span_notice(I18n.T("You start deconstructing []...", this)));
        if (Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50)))
        {
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            await deconstruct(TRUE);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/platform.dm:83
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList ?? Dm.list();
        if (Dm.truthy(frame_icon))
            Dm.append(dot, mutable_appearance(frame_icon, Dm.text("[]-[]", base_icon_state, smoothing_junction), appearance_flags: KEEP_APART));
        return dot;
    }

    // tg: code/game/objects/structures/platform.dm:88
    public override object set_smoothed_icon_state(object new_junction = null)
    {
        object dot = base.set_smoothed_icon_state(new_junction);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/objects/structures/platform.dm:92
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        turf target_turf = drop_location() as turf;
        if (sheet_type != null)
            Dm.@new(sheet_type, target_turf, sheet_amount);
        else
        {
            foreach (datum_material mat in Dm.each<datum_material>(custom_materials))
                Dm.@new(mat.sheet_type, target_turf, FLOOR(Dm.num(custom_materials[mat]) / SHEET_MATERIAL_AMOUNT, 1));
        }
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/platform.dm:31  var/static/list/turf_traits
    internal DmList obj_structure_platform__turf_traits = Dm.list(TRAIT_TURF_IGNORE_SLOWDOWN, TRAIT_TURF_IGNORE_SLIPPERY, TRAIT_IMMERSE_STOPPED);
}
