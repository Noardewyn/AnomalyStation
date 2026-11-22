// Translated from tgstation (snapshot 1d99bd7f): code/datums/keybinding/living.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only /datum/keybinding/living (with can_use) and the rest, toggle_combat_mode, toggle_move_intent and
// toggle_move_intent_alternative bindings translated (M2 P1: the client's actions, scope 2f); resist (M4b slice 3: stop,
// drop and roll); give (M9 slice F: the offering); the other living bindings (look up and down, ...) come with their mechanics; the throw mode
// bindings are M4a P2's (below).

// tg: code/datums/keybinding/living.dm:1
[DmPath("/datum/keybinding/living")]
public partial class datum_keybinding_living : datum_keybinding
{
    // tg: code/datums/keybinding/living.dm:2-3  the var overrides of the type block (rules 10.2)
    public datum_keybinding_living()
    {
        // GAP: CATEGORY_HUMAN (code/datums/keybinding/_defines.dm:6, #define CATEGORY_HUMAN "HUMAN") and WEIGHT_MOB
        // (:17, #define WEIGHT_MOB 40) are global #defines outside code/__DEFINES, which the defines converter does not
        // emit; their values are written here until they are.
        category = "HUMAN";
        weight = 40;
    }

    // tg: code/datums/keybinding/living.dm:5
    public override object can_use(client user = null)
    {
        return Dm.num(isliving(user.mob));
    }
}

// tg: code/datums/keybinding/living.dm:8
public partial class datum_keybinding_living_resist : datum_keybinding_living
{
    // tg: code/datums/keybinding/living.dm:15
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        mob_living owner = user.mob as mob_living;
        owner.resist();
        if (Dm.truthy(owner.hud_used?.screen_objects[HUD_MOB_RESIST]))
        {
            atom_movable_screen resist_button = (atom_movable_screen)owner.hud_used.screen_objects[HUD_MOB_RESIST];
            resist_button.icon_state = Dm.text("[]_on", resist_button.base_icon_state);
        }
        return TRUE;
    }

    // tg: code/datums/keybinding/living.dm:25
    public override async DmTask<object> up(client user = null, turf target = null)
    {
        object dot = await base.up(user, target);
        if (Dm.truthy(dot))
            return dot;
        mob_living owner = user.mob as mob_living;
        if (Dm.truthy(owner.hud_used?.screen_objects[HUD_MOB_RESIST]))
        {
            atom_movable_screen resist_button = (atom_movable_screen)owner.hud_used.screen_objects[HUD_MOB_RESIST];
            resist_button.icon_state = resist_button.base_icon_state as string;
        }
        return dot;
    }
}

// tg: code/datums/keybinding/living.dm:75
[DmPath("/datum/keybinding/living/rest")]
public partial class datum_keybinding_living_rest : datum_keybinding_living
{
    // tg: code/datums/keybinding/living.dm:77-80  the var overrides of the type block (rules 10.2); hotkey_keys (:76) creates a list:
    // dm_init_vars below
    public datum_keybinding_living_rest()
    {
        name = "rest";
        full_name = "Rest";
        description = "Lay down, or get up.";
        keybind_signal = COMSIG_KB_LIVING_REST_DOWN;
    }

    // tg: code/datums/keybinding/living.dm:76  the object-creating override (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        hotkey_keys = Dm.list("U");
    }

    // tg: code/datums/keybinding/living.dm:82
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = null;
        dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        mob_living living_mob = user.mob as mob_living;
        await living_mob.toggle_resting();
        return TRUE;
    }
}

// tg: code/datums/keybinding/living.dm:90
[DmPath("/datum/keybinding/living/toggle_combat_mode")]
public partial class datum_keybinding_living_toggle_combat_mode : datum_keybinding_living
{
    // tg: code/datums/keybinding/living.dm:92-95  the var overrides of the type block (rules 10.2); hotkey_keys (:91) creates a list:
    // dm_init_vars below
    public datum_keybinding_living_toggle_combat_mode()
    {
        name = "toggle_combat_mode";
        full_name = "Toggle Combat Mode";
        description = "Toggles combat mode. Like Help/Harm but cooler.";
        keybind_signal = COMSIG_KB_LIVING_TOGGLE_COMBAT_DOWN;
    }

    // tg: code/datums/keybinding/living.dm:91  the object-creating override (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        hotkey_keys = Dm.list("F");
    }

    // tg: code/datums/keybinding/living.dm:98
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = null;
        dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        mob_living user_mob = user.mob as mob_living;
        user_mob.set_combat_mode(Dm.not(user_mob.combat_mode), FALSE);
        return dot;
    }
}

// tg: code/datums/keybinding/living.dm:133
[DmPath("/datum/keybinding/living/toggle_move_intent")]
public partial class datum_keybinding_living_toggle_move_intent : datum_keybinding_living
{
    // tg: code/datums/keybinding/living.dm:135-138  the var overrides of the type block (rules 10.2); hotkey_keys (:134) creates a list:
    // dm_init_vars below
    public datum_keybinding_living_toggle_move_intent()
    {
        name = "toggle_move_intent";
        full_name = "Hold to toggle move intent";
        description = "Held down to cycle to the other move intent, release to cycle back";
        keybind_signal = COMSIG_KB_LIVING_TOGGLEMOVEINTENT_DOWN;
    }

    // tg: code/datums/keybinding/living.dm:134  the object-creating override (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        hotkey_keys = Dm.list("C");
    }

    // tg: code/datums/keybinding/living.dm:140
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = null;
        dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        mob_living M = user.mob as mob_living;
        await M.toggle_move_intent();
        return TRUE;
    }

    // tg: code/datums/keybinding/living.dm:148
    public override async DmTask<object> up(client user = null, turf target = null)
    {
        object dot = null;
        dot = await base.up(user, target);
        mob_living M = user.mob as mob_living;
        await M.toggle_move_intent();
        return TRUE;
    }
}

// tg: code/datums/keybinding/living.dm:154
[DmPath("/datum/keybinding/living/toggle_move_intent_alternative")]
public partial class datum_keybinding_living_toggle_move_intent_alternative : datum_keybinding_living
{
    // tg: code/datums/keybinding/living.dm:156-159  the var overrides of the type block (rules 10.2); hotkey_keys (:155) creates a list:
    // dm_init_vars below
    public datum_keybinding_living_toggle_move_intent_alternative()
    {
        name = "toggle_move_intent_alt";
        full_name = "press to cycle move intent";
        description = "Pressing this cycle to the opposite move intent, does not cycle back";
        keybind_signal = COMSIG_KB_LIVING_TOGGLEMOVEINTENTALT_DOWN;
    }

    // tg: code/datums/keybinding/living.dm:155  the object-creating override (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        hotkey_keys = Dm.list(UNBOUND_KEY);
    }

    // tg: code/datums/keybinding/living.dm:161
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = null;
        dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        mob_living M = user.mob as mob_living;
        await M.toggle_move_intent();
        return TRUE;
    }
}

// The M4a throw mode bindings (M4a P2); their var blocks are generated (types-converter.md 3.2).

// tg: code/datums/keybinding/living.dm:176
public partial class datum_keybinding_living_toggle_throw_mode : datum_keybinding_living
{
    // tg: code/datums/keybinding/living.dm:176
    [DmParams("user")]
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = null;
        dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        mob_living living_user = user.mob as mob_living;
        living_user.toggle_throw_mode();
        return TRUE;
    }
}

// tg: code/datums/keybinding/living.dm:191
public partial class datum_keybinding_living_hold_throw_mode : datum_keybinding_living
{
    // tg: code/datums/keybinding/living.dm:191
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = null;
        dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        mob_living living_user = user.mob as mob_living;
        living_user.throw_mode_on(THROW_MODE_HOLD);
        return dot;
    }

    // tg: code/datums/keybinding/living.dm:198
    public override async DmTask<object> up(client user = null, turf target = null)
    {
        object dot = null;
        dot = await base.up(user, target);
        if (Dm.truthy(dot))
            return dot;
        mob_living living_user = user.mob as mob_living;
        living_user.throw_mode_off(THROW_MODE_HOLD);
        return dot;
    }
}

// tg: code/datums/keybinding/living.dm:205  (M9 slice F: protocol version 41's code 15)
public partial class datum_keybinding_living_give : datum_keybinding_living
{
    // tg: code/datums/keybinding/living.dm:212
    public override object can_use(client user = null)
    {
        object dot = base.can_use(user);
        if (!Dm.truthy(dot))
            return FALSE;
        if (user.mob == null)
            return FALSE;
        obj_item @object = user.mob.get_active_held_item() as obj_item;
        if (@object == null)
            return FALSE;
        if (HAS_TRAIT(@object, TRAIT_BORG_GIVE))
            return TRUE;
        if (!HAS_TRAIT(user.mob, TRAIT_CAN_HOLD_ITEMS))
            return FALSE;
        return TRUE;
    }

    // tg: code/datums/keybinding/living.dm:227
    [DmParams("user")]
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        mob_living living_user = user.mob as mob_living;
        obj_item held_item = living_user.get_active_held_item() as obj_item;
        if (held_item == null)
            return dot;
        if (!HAS_TRAIT(living_user, TRAIT_CAN_HOLD_ITEMS) && !HAS_TRAIT(held_item, TRAIT_BORG_GIVE))
            return dot;
        await living_user.give();
        return dot;
    }
}

