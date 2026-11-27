// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/computers/item/laptop.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The screentip context (add_context) is a live-partial stub, as every item's (D264).

// tg: code/modules/modular_computers/computers/item/laptop.dm:1
public partial class obj_item_modular_computer_laptop : obj_item_modular_computer
{
    // tg: code/modules/modular_computers/computers/item/laptop.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(start_open) && !Dm.truthy(screen_on))
            Waiting.NoSleep(toggle_open());
        RegisterSignal(this, COMSIG_SPEED_POTION_APPLIED, nameof(on_speed_potioned));
        Waiting.NoSleep(AddElement(typeof(datum_element_drag_pickup)));
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/laptop.dm:35
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(screen_on))
            Dm.append(ref dot, span_notice(I18n.T("Alt-click to close it.")));
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/laptop.dm:40  add_context: live-partial (the screentips, D264).

    /// Signal handler for [COMSIG_SPEED_POTION_APPLIED]. Speed potion removes the open slowdown
    // tg: code/modules/modular_computers/computers/item/laptop.dm:51
    public virtual void on_speed_potioned(datum source = null)
    {
        // SIGNAL_HANDLER
        // Don't need to touch the actual slowdown here, since the speed potion does it for us
        slowdown_open = 0;
    }

    // tg: code/modules/modular_computers/computers/item/laptop.dm:56
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(screen_on))
        {
            icon_state = icon_state_closed;
            return null;
        }
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/modular_computers/computers/item/laptop.dm:62
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        if (!Dm.truthy(screen_on))
        {
            cut_overlays();
            return null;
        }
        return await base.update_overlays(__extra);
    }

    // tg: code/modules/modular_computers/computers/item/laptop.dm:68
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(screen_on))
            await try_toggle_open(user as mob_living);
        else
            return await base.attack_self(user, modifiers, __extra);
        return null;
    }

    // tg: code/modules/modular_computers/computers/item/laptop.dm:74  GAME_VERB_SRC(/obj/item/modular_computer/laptop, open_computer, view(1), "Toggle Open", null): the body
    public virtual async DmTask __gvb_open_computer(DmList structured_args = null)
    {
        await try_toggle_open(usr as mob_living);
    }

    // tg: code/modules/modular_computers/computers/item/laptop.dm:74  the verb proc
    public virtual void open_computer(params object[] __extra)
    {
        Waiting.Detach(__gvb_open_computer(Dm.list()));
    }

    // tg: code/modules/modular_computers/computers/item/laptop.dm:78
    public virtual async DmTask try_toggle_open(mob_living user = null)
    {
        if (issilicon(user))
            return;
        if (!isturf(loc) && !ismob(loc)) // No opening it in backpack.
            return;
        if (!Dm.truthy(await user.can_perform_action(this)))
            return;

        await toggle_open(user);
    }

    // tg: code/modules/modular_computers/computers/item/laptop.dm:89
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(screen_on))
            return CLICK_ACTION_BLOCKING;
        await try_toggle_open(user as mob_living); // Close it.
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/modular_computers/computers/item/laptop.dm:95
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;

        await attack_self(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/modular_computers/computers/item/laptop.dm:103
    public virtual async DmTask toggle_open(mob_living user = null)
    {
        if (Dm.truthy(screen_on))
        {
            to_chat(user, span_notice(I18n.T("You close \\the [].", this)));
            slowdown -= slowdown_open;
            update_weight_class(Dm.initial(this, (obj_item_modular_computer_laptop l) => l.w_class));
            drag_slowdown = Dm.initial(this, (obj_item_modular_computer_laptop l) => l.drag_slowdown);
        }
        else
        {
            to_chat(user, span_notice(I18n.T("You open \\the [].", this)));
            slowdown += slowdown_open;
            update_weight_class(w_class_open);
            drag_slowdown = slowdown_open;
        }

        if (isliving(loc))
        {
            mob_living localmob = loc as mob_living;
            await localmob.update_equipment_speed_mods();
            await localmob.update_pull_movespeed();
        }

        screen_on = Dm.truthy(screen_on) ? FALSE : TRUE;
        update_appearance();
    }

    // tg: code/modules/modular_computers/computers/item/laptop.dm:123
    public override object get_messenger_ending()
    {
        return "Sent from my UNIX Laptop";
    }
}
