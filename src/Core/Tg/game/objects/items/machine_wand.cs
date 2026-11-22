// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/machine_wand.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole but its screentips (add_context: a live-partial row, D291). The machine wand: a machine or a bot
// clicked (from afar too) sends a bug flying to it (tg's "fly", homing in), which takes control of it (the "fly-surrounding" overlay
// on the machine) unless its AI wire is cut; used in hand the wand opens the controlled machine's own window (or interacts with it),
// held in the active hand it gives the AI's and the silicons' access; Alt-click lets go; an EMP on the machine or the bug disables
// the wand for two minutes. The `controller` var of the bug holds the wand (the converter typed it as a path): hand-declared here, so
// hand mode keeps the generated constructor here too. The var blocks are generated (D111).

// tg: code/game/objects/items/machine_wand.dm:4
public partial class obj_item_machine_remote : obj_item
{
    ///When EMPed, how long the remote will be disabled for by default.
    private const float EMP_TIMEOUT_DURATION = 2 * MINUTES; // tg: machine_wand.dm:2

    // tg: code/game/objects/items/machine_wand.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        bug_appearance = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "fly-surrounding", ABOVE_WINDOW_LAYER) as mutable_appearance;
        register_context();
        return dot;
    }

    // tg: code/game/objects/items/machine_wand.dm:27
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (user.get_active_held_item() == this)
        {
            ADD_TRAIT(user, TRAIT_AI_ACCESS, HELD_ITEM_TRAIT);
            ADD_TRAIT(user, TRAIT_SILICON_ACCESS, HELD_ITEM_TRAIT);
        }
    }

    // tg: code/game/objects/items/machine_wand.dm:33
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (user.get_active_held_item() != this)
        {
            REMOVE_TRAIT(user, TRAIT_AI_ACCESS, HELD_ITEM_TRAIT);
            REMOVE_TRAIT(user, TRAIT_SILICON_ACCESS, HELD_ITEM_TRAIT);
        }
        return dot;
    }

    // tg: code/game/objects/items/machine_wand.dm:39
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        if (Dm.truthy(controlling_machine_or_bot))
            remove_old_machine();
        QDEL_NULL(ref moving_bug);
        QDEL_NULL(ref bug_appearance);
        return dot;
    }

    // tg: code/game/objects/items/machine_wand.dm:46
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(controlling_machine_or_bot))
            Dm.append(ref dot, span_notice(I18n.T("It is currently controlling []. Use in-hand to interact with it.", controlling_machine_or_bot)));
        return dot;
    }

    // tg: code/game/objects/items/machine_wand.dm:58
    [SignalHandler]
    public virtual void on_control_destroy(obj_machinery source = null)
    {
        remove_old_machine();
    }

    // tg: code/game/objects/items/machine_wand.dm:62
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (!(timeout_time <= world.time)) // !COOLDOWN_FINISHED(src, timeout_time), inline (rules 11.1 (a))
        {
            playsound(this, Dm.resource("sound/machines/synth/synth_no.ogg"), 30, TRUE);
            await say(I18n.T("Remote control disabled temporarily. Please try again soon."));
            return FALSE;
        }
        if (!Dm.truthy(controlling_machine_or_bot))
            return null;
        if (Dm.truthy(await controlling_machine_or_bot.ui_interact(user, ui)))
            return null;
        await controlling_machine_or_bot.interact(user); //no ui, interact instead (to open windoors and such)
        return null;
    }

    // tg: code/game/objects/items/machine_wand.dm:73
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(controlling_machine_or_bot))
            return await controlling_machine_or_bot.ui_act(action, @params, ui, state);
        return dot;
    }

    // tg: code/game/objects/items/machine_wand.dm:78
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(moving_bug)) //we have a bug in transit, so let's kill it.
        {
            QDEL_NULL(ref moving_bug);
            return CLICK_ACTION_BLOCKING;
        }
        if (!Dm.truthy(controlling_machine_or_bot))
            return CLICK_ACTION_BLOCKING;
        await say(I18n.T("Remote control over [] stopped.", controlling_machine_or_bot));
        remove_old_machine();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/machine_wand.dm:88
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION) || (!ismachinery(interacting_with) && !isbot(interacting_with)))
            return NONE;
        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/machine_wand.dm:93
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!(timeout_time <= world.time)) // !COOLDOWN_FINISHED(src, timeout_time), inline (rules 11.1 (a))
        {
            playsound(this, Dm.resource("sound/machines/synth/synth_no.ogg"), 30, TRUE);
            await say(I18n.T("Remote control disabled temporarily. Please try again soon."));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!ismachinery(interacting_with) && !isbot(interacting_with))
            return NONE;
        if (Dm.truthy(moving_bug)) //we have a bug in transit already, so let's kill it.
            QDEL_NULL(ref moving_bug);
        turf spawning_turf = Dm.truthy(controlling_machine_or_bot) ? get_turf(controlling_machine_or_bot) : get_turf(this);
        moving_bug = Dm.@new<obj_effect_bug_moving>(spawning_turf, this, interacting_with);
        remove_old_machine();
        return ITEM_INTERACT_SUCCESS;
    }

    ///Sets a controlled machine to a new machine, if possible. Checks if AIs can even control it.
    // tg: code/game/objects/items/machine_wand.dm:108
    public virtual async DmTask set_controlled_machine(obj_machinery new_machine = null)
    {
        if (controlling_machine_or_bot == new_machine)
            return;
        remove_old_machine();
        if (new_machine is obj_machinery_power_apc new_apc)
        {
            if (Dm.truthy(new_apc.aidisabled))
            {
                await say(I18n.T("AI wire cut, machine uncontrollable."));
                return;
            }
        }
        else if (new_machine is obj_machinery_door_airlock new_airlock)
        {
            if (!Dm.truthy(new_airlock.canAIControl()))
            {
                await say(I18n.T("AI wire cut, machine uncontrollable."));
                return;
            }
        }
        controlling_machine_or_bot = new_machine;
        controlling_machine_or_bot.add_overlay(bug_appearance);
        RegisterSignal(controlling_machine_or_bot, COMSIG_QDELETING, nameof(on_control_destroy));
        RegisterSignal(controlling_machine_or_bot, COMSIG_ATOM_EMP_ACT, nameof(on_machine_emp));
    }

    ///Removes the machine being controlled as the current machine, taking its signals and overlays with it.
    // tg: code/game/objects/items/machine_wand.dm:128
    public virtual void remove_old_machine()
    {
        if (!Dm.truthy(controlling_machine_or_bot))
            return;
        UnregisterSignal(controlling_machine_or_bot, Dm.list(COMSIG_ATOM_EMP_ACT, COMSIG_QDELETING));
        controlling_machine_or_bot.cut_overlay(bug_appearance);
        controlling_machine_or_bot = null;
    }

    ///Called when the machine we're controlling is EMP, removing our control from it.
    // tg: code/game/objects/items/machine_wand.dm:136
    [SignalHandler]
    public virtual void on_machine_emp(datum source = null, object severity = null, object protection = null)
    {
        if (Dm.truthy(Dm.bitand(severity, EMP_PROTECT_CONTENTS)))
            return;
        disable_remote(EMP_TIMEOUT_DURATION);
    }

    // tg: code/game/objects/items/machine_wand.dm:142
    public virtual void disable_remote(object timeout_duration = null)
    {
        remove_old_machine();
        timeout_time = world.time + Dm.num(timeout_duration); // COOLDOWN_START(src, timeout_time, timeout_duration), inline
    }
}

///The effect of the bug moving towards the selected machinery to mess with.
// tg: code/game/objects/items/machine_wand.dm:147
public partial class obj_effect_bug_moving : obj_effect
{
    private const float EMP_TIMEOUT_DURATION = 2 * MINUTES; // tg: machine_wand.dm:2

    ///The controller that's sending us out to the machine.
    // tg: code/game/objects/items/machine_wand.dm:158  var/obj/item/machine_remote/controller
    public obj_item_machine_remote controller;

    // tg: code/game/objects/items/machine_wand.dm:148  (the generated constructor, kept here: hand mode, see the header)
    public obj_effect_bug_moving()
    {
        name = "bug";
        desc = "Where da bug goin?";
        icon_state = "fly";
        obj_flags = 2;
        max_integrity = 20;
        uses_integrity = 1;
        plane = -3;
        layer = 5;
        movement_type = 16;
    }

    // tg: code/game/objects/items/machine_wand.dm:162
    [DmParams("mapload", "controller", "thing_moving_towards")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item_machine_remote controller__1 = Dm.arg<obj_item_machine_remote>(__extra, 0);
        atom_movable thing_moving_towards__1 = Dm.arg<atom_movable>(__extra, 1);
        object dot = base.Initialize(mapload, Dm.forward(__extra, controller__1, thing_moving_towards__1));
        if (!Dm.truthy(controller__1))
            throw Dm.CRASH("a moving bug has been created by something that isn't a machine remote controller!", "code/game/objects/items/machine_wand.dm", 165);
        if (!Dm.truthy(thing_moving_towards__1))
            throw Dm.CRASH("a moving bug has been created but isn't moving towards anything!", "code/game/objects/items/machine_wand.dm", 167);
        this.controller = controller__1;
        this.thing_moving_towards = thing_moving_towards__1;
        datum_move_loop loop = GLOB.move_manager.home_onto(this, thing_moving_towards__1, delay: 5, flags: MOVEMENT_LOOP_NO_DIR_UPDATE) as datum_move_loop;
        RegisterSignal(loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(reached_destination_check));
        RegisterSignal(thing_moving_towards__1, COMSIG_QDELETING, nameof(on_machine_del));
        return dot;
    }

    // tg: code/game/objects/items/machine_wand.dm:174
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(controller))
        {
            controller.moving_bug = null;
            controller = null;
        }
        thing_moving_towards = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/machine_wand.dm:181
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        controller.disable_remote(EMP_TIMEOUT_DURATION);
        qdel(this);
        return dot;
    }

    // tg: code/game/objects/items/machine_wand.dm:188
    [SignalHandler]
    public virtual void reached_destination_check(datum_move_loop source = null, object result = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(Adjacent(thing_moving_towards))))
            return;
        Waiting.NoSleep(controller.set_controlled_machine(thing_moving_towards as obj_machinery));
        qdel(this);
    }

    // tg: code/game/objects/items/machine_wand.dm:195
    [SignalHandler]
    public virtual void on_machine_del(datum_move_loop source = null)
    {
        qdel(this);
    }
}
