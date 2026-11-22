// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/curtains.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps ST2 (D333): the whole file (the shower curtain drawn open and shut by hand, its colour from a crayon's picker, wrenched and cut
// apart; the bounty curtains; the cloth and fancy curtains; the theatre's mechanical curtains that the curtain button opens and shuts).
// The var blocks are generated (D111).

/**
 * Shower Curtains
 */
// tg: code/game/objects/structures/curtains.dm:4
public partial class obj_structure_curtain : obj_structure
{
    // tg: code/game/objects/structures/curtains.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        // see-through curtains should let emissives shine through
        if (!Dm.truthy(opaque_closed))
            blocks_emissive = EMISSIVE_BLOCK_NONE;
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_INVERTED_DEMOLITION, INNATE_TRAIT);
        return dot;
    }

    // tg: code/game/objects/structures/curtains.dm:29
    public virtual async DmTask toggle()
    {
        open = Dm.not(open);
        if (Dm.truthy(open))
        {
            layer = SIGN_LAYER;
            await set_opacity(FALSE);
        }
        else
        {
            layer = WALL_OBJ_LAYER;
            if (Dm.truthy(opaque_closed))
                await set_opacity(TRUE);
        }

        update_appearance();
    }

    // tg: code/game/objects/structures/curtains.dm:41
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]-[]", icon_type, Dm.truthy(open) ? "open" : "closed");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/curtains.dm:45
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_toy_crayon)))
            return NONE;
        color = await tgui_color_picker(user, "", I18n.T("Choose Color"), color);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/curtains.dm:51
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool, time: 5 * SECONDS);
        return TRUE;
    }

    // tg: code/game/objects/structures/curtains.dm:56
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        await base.wirecutter_act(user, tool);
        if (Dm.truthy(anchored))
            return TRUE;

        user.visible_message(span_warning(I18n.T("[] cuts apart [].", user, this)),
            span_notice(I18n.T("You start to cut apart [].", this)), span_hear(I18n.T("You hear cutting.")));
        if (Dm.truthy(await tool.use_tool(this, user, 50, volume: 100)) && !Dm.truthy(anchored))
        {
            to_chat(user, span_notice(I18n.T("You cut apart [].", this)));
            await deconstruct();
        }

        return TRUE;
    }

    // tg: code/game/objects/structures/curtains.dm:70
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        playsound(loc, Dm.resource("sound/effects/curtain.ogg"), 50, TRUE);
        await toggle();
        return dot;
    }

    // tg: code/game/objects/structures/curtains.dm:77
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_cloth>(loc, 2);
        Dm.@new<obj_item_stack_sheet_plastic>(loc, 2);
        Dm.@new<obj_item_stack_rods>(loc);
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/curtains.dm:82
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        object sw__1 = damage_type;
        if (Dm.eq(sw__1, BRUTE))
        {
            if (Dm.truthy(damage_amount))
                playsound(this.loc, Dm.resource("sound/items/weapons/slash.ogg"), 80, TRUE);
            else
                playsound(loc, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
        }
        else if (Dm.eq(sw__1, BURN))
            playsound(loc, Dm.resource("sound/items/tools/welder.ogg"), 80, TRUE);
        return null;
    }
}

// tg: code/game/objects/structures/curtains.dm:99
public partial class obj_structure_curtain_bounty_start_closed : obj_structure_curtain_bounty
{
    // tg: code/game/objects/structures/curtains.dm:102
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(open))
            Waiting.NoSleep(toggle());
        return dot;
    }
}

// tg: code/game/objects/structures/curtains.dm:107
public partial class obj_structure_curtain_cloth : obj_structure_curtain
{
    // tg: code/game/objects/structures/curtains.dm:113
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_cloth>(loc, 4);
        Dm.@new<obj_item_stack_rods>(loc);
        await DmTask.CompletedTask;
    }
}

// tg: code/game/objects/structures/curtains.dm:121
public partial class obj_structure_curtain_cloth_fancy_mechanical : obj_structure_curtain_cloth_fancy
{
    // tg: code/game/objects/structures/curtains.dm:124
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.curtains, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/curtains.dm:128
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.curtains, this);
        return dot;
    }

    // tg: code/game/objects/structures/curtains.dm:132
    public override async DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        id = Dm.text("[]_[]", port.shuttle_id, id);
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/objects/structures/curtains.dm:135
    public virtual async DmTask open__proc()
    {
        icon_state = Dm.text("[]-open", icon_type);
        layer = SIGN_LAYER;
        SET_PLANE_IMPLICIT(this, GAME_PLANE);
        set_density(FALSE);
        open = TRUE;
        await set_opacity(FALSE);
    }

    // tg: code/game/objects/structures/curtains.dm:143
    public virtual async DmTask close()
    {
        icon_state = Dm.text("[]-closed", icon_type);
        layer = WALL_OBJ_LAYER;
        set_density(TRUE);
        open = FALSE;
        if (Dm.truthy(opaque_closed))
            await set_opacity(TRUE);
    }

    // tg: code/game/objects/structures/curtains.dm:151
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/game/objects/structures/curtains.dm:154
public partial class obj_structure_curtain_cloth_fancy_mechanical_start_closed : obj_structure_curtain_cloth_fancy_mechanical
{
    // tg: code/game/objects/structures/curtains.dm:157
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(close());
        return dot;
    }
}
