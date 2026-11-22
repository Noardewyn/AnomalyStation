// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/tank_holder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps ST2 (D333): the whole file (the tank holder: its mapped tank or extinguisher made at Initialize and put in through the tank's
// container component, which sets the holder's icon state and density; a tank put in and taken out by hand, thrown things pass it,
// wrenched down, screwed apart when empty, the tank thrown into an explosion with it). The tank var holds the tank, a path in the
// subtypes until Initialize (the types converter's ObjectHoldingVars). The var blocks are generated (D111).

///?
// tg: code/game/objects/structures/tank_holder.dm:2
public partial class obj_structure_tank_holder : obj_structure
{
    // tg: code/game/objects/structures/tank_holder.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(tank))
        {
            obj_item tank_ = Dm.@new((Type)tank, (object)null) as obj_item;
            tank = null;
            tank_.SEND_SIGNAL(COMSIG_CONTAINER_TRY_ATTACH, Dm.list(tank_, this, null));
        }
        return dot;
    }

    // tg: code/game/objects/structures/tank_holder.dm:27
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref tank);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/tank_holder.dm:31
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (mover != null && Dm.truthy(mover.throwing))
            return TRUE;
        return dot;
    }

    // tg: code/game/objects/structures/tank_holder.dm:36
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("It is <b>bolted</b> to the floor.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("The <i>bolts</i> on the bottom are unsecured.")));
        if (Dm.truthy(tank))
            Dm.append(ref dot, span_notice(I18n.T("It is holding \\a [].", tank)));
        else
            Dm.append(ref dot, span_notice(I18n.T("It is empty.")));
        Dm.append(ref dot, span_notice(I18n.T("It is held together by some <b>screws</b>.")));
        return dot;
    }

    // tg: code/game/objects/structures/tank_holder.dm:48
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(user.combat_mode))
            return NONE;

        if (!Dm.truthy(tool.SEND_SIGNAL(COMSIG_CONTAINER_TRY_ATTACH, Dm.list(tool, this, user))))
        {
            to_chat(user, span_warning(I18n.T("[] does not fit in [].", tool, this)));
            return ITEM_INTERACT_BLOCKING;
        }

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tank_holder.dm:58
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        to_chat(user, span_notice(Dm.truthy(anchored) ? I18n.T("You begin to unwrench [].", this) : I18n.T("You begin to wrench [].", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 20, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(Dm.truthy(anchored) ? I18n.T("You successfully unwrench [].", this) : I18n.T("You successfully wrench [].", this)));
        await set_anchored(!Dm.truthy(anchored) ? TRUE : FALSE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tank_holder.dm:70
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await base.screwdriver_act(user, tool)))
            return TRUE;
        if (Dm.truthy(tank))
            return FALSE;
        tool.play_tool_sound(this);
        await deconstruct(TRUE);
        return TRUE;
    }

    // tg: code/game/objects/structures/tank_holder.dm:79
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        atom Tsec = drop_location() as atom;
        Dm.@new<obj_item_stack_rods>(Tsec, 2);
        if (Dm.truthy(tank))
            await ((obj_item)tank).forceMove(Tsec);
    }

    // tg: code/game/objects/structures/tank_holder.dm:85
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/structures/tank_holder.dm:88
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(tank))
            return await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(await Adjacent(user)) || issilicon(user))
            return await base.attack_hand(user, modifiers, __extra);
        obj_item held = (obj_item)tank;
        to_chat(user, span_notice(I18n.T("You take [] from [].", held, this)));
        add_fingerprint(user);
        held.add_fingerprint(user);
        await user.put_in_hands(held);
        return null;
    }

    // tg: code/game/objects/structures/tank_holder.dm:98
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == tank)
            after_detach_tank();
        return dot;
    }

    // tg: code/game/objects/structures/tank_holder.dm:103
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        if (!Dm.truthy(tank))
            return;

        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                Dm.append(ref SSexplosions.high_mov_atom, tank);
                break;
            case EXPLODE_HEAVY:
                Dm.append(ref SSexplosions.med_mov_atom, tank);
                break;
            case EXPLODE_LIGHT:
                Dm.append(ref SSexplosions.low_mov_atom, tank);
                break;
        }
        await DmTask.CompletedTask;
    }

    /// Call this after taking the tank from contents in order to update references, icon
    /// and density.
    // tg: code/game/objects/structures/tank_holder.dm:117
    public virtual void after_detach_tank()
    {
        tank = null;
        set_density(FALSE);
        icon_state = "holder";
    }
}
