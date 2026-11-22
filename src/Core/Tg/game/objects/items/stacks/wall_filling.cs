// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/wall_filling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SK1 (D333): the file whole. The plastitanium wall fillings and the plastitanium glass window fillings: reskinned in hand
// through a radial menu of their kinds (the survival pod, the dark and red dark shuttle walls, the pod window), examined for their
// worth as a throwing weapon, welded back into two sheets of what they were made from. The var blocks are generated (D111).

// Wall-fillings; items you can re-skin in hand and place onto griders to create different wall types.
// tg: code/game/objects/items/stacks/wall_filling.dm:2
public partial class obj_item_stack_wall_filling : obj_item_stack
{
    // tg: code/game/objects/items/stacks/wall_filling.dm:26
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Use while in your hand to change what type of [] you want.", this)));
        if (Dm.truthy(throwforce) && !Dm.truthy(is_cyborg)) //do not want to divide by zero or show the message to borgs who can't throw
        {
            string damage_value = null;
            float throws = MathF.Ceiling(MAX_LIVING_HEALTH / Dm.num(throwforce)); //throws to crit a human
            if (throws >= 1 && throws <= 3)
                damage_value = I18n.T("superb");
            else if (throws >= 4 && throws <= 6)
                damage_value = I18n.T("great");
            else if (throws >= 7 && throws <= 9)
                damage_value = I18n.T("good");
            else if (throws >= 10 && throws <= 12)
                damage_value = I18n.T("fairly decent");
            else if (throws >= 13 && throws <= 15)
                damage_value = I18n.T("mediocre");
            if (damage_value == null)
                return dot;
            Dm.append(ref dot, span_notice(I18n.T("Those could work as a [] throwing weapon.", damage_value)));
        }
        return dot;
    }

    // tg: code/game/objects/items/stacks/wall_filling.dm:46
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.eq(tool.tool_behaviour, TOOL_WELDER))
            return await base.item_interaction(user, tool, modifiers, __extra);
        if (made_from == null)
        {
            to_chat(user, span_warning(I18n.T("You can not reform this!")));
            stack_trace(Dm.text("A wall filling of type [] doesn't have its made_from set.", type), "code/game/objects/items/stacks/wall_filling.dm", 51);
            return null;
        }
        if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 40)))
            return null;
        var new_item = Dm.@new(made_from, user.drop_location(), 2) as obj_item;
        user.visible_message(span_notice(I18n.T("[] shaped [] into [] with [].", user, this, new_item, tool)),
            span_notice(I18n.T("You shaped [] into [] with [].", this, new_item, tool)),
            span_hear(I18n.T("You hear welding.")));
        object holding = user.is_holding(this);
        await use(1);
        if (Dm.truthy(holding) && QDELETED(this))
            await user.put_in_hands(new_item);
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Yup, literally copied over from tile reskinning, except we don't need a dir as walls don't rotate, so, that much simpler.
     */
    // tg: code/game/objects/items/stacks/wall_filling.dm:71
    public virtual object wall_reskin_list(DmList values = null)
    {
        object dot = null;
        string string_id = values.Join("-");
        dot = GLOB.wall_reskin_lists[string_id];
        if (Dm.truthy(dot))
            return dot;
        foreach (object path in Dm.each(values.Copy()))
        {
            Type type_cast_path = path as Type;
            values[path] = Dm.image(icon: Dm.initial(type_cast_path, (atom a) => a.icon), icon_state: Dm.initial(type_cast_path, (atom a) => a.icon_state));
        }
        return GLOB.wall_reskin_lists[string_id] = values;
    }

    // tg: code/game/objects/items/stacks/wall_filling.dm:81
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object choice = await show_radial_menu(user, this, wall_reskin_types, radius: 48, require_near: TRUE);
        if (!Dm.truthy(choice) || Dm.eq(choice, type))
            return null;
        var made = Dm.@new(choice as Type, user.drop_location(), amount) as obj_item;
        await moveToNullspace();
        if (!QDELETED(made))
            await user.put_in_active_hand(made);
        qdel(this);
        return null;
    }

    // tg: code/game/objects/items/stacks/wall_filling.dm:91
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (wall_reskin_types != null)
            wall_reskin_types = wall_reskin_list(wall_reskin_types) as DmList;
        return dot;
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/wall_filling.dm:66  GLOBAL_LIST_EMPTY(wall_reskin_lists)
    public DmList wall_reskin_lists;

    // tg: code/game/objects/items/stacks/wall_filling.dm:66  (GLOBAL_MANAGED(wall_reskin_lists, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalwall_reskin_lists()
    {
        wall_reskin_lists = Dm.list();
        Dm.append(ref gvars_datum_init_order, "wall_reskin_lists");
    }
}
