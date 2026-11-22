// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/mod_actions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file whole. The MODsuit's action buttons: deploy (a part, or the whole suit on a right click), activate
// (a second click within 3 seconds, or a right click, turns the suit on or off), the module radial, the panel, each with an AI
// twin for a suit's AI assistant; and a module pinned to a mob's action bar, which shows the selected, active and cooldown
// marks of its module. The var blocks are generated (D111). The circuit pin (pinnable/circuit) is wiremod's (section 164).

// tg: code/modules/mod/mod_actions.dm:1
public partial class datum_action_item_action_mod : datum_action_item_action
{
    // tg: code/modules/mod/mod_actions.dm:9
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        object Target = Dm.arg(__extra, 0);
        await base.New(__extra);
        if (!istype(Target, typeof(obj_item_mod_control)))
        {
            qdel(this);
            return;
        }
        if (Dm.truthy(ai_action))
            background_icon_state = ACTION_BUTTON_DEFAULT_BACKGROUND;
    }

    // tg: code/modules/mod/mod_actions.dm:17
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        mob user = grant_to;
        obj_item_mod_control mod = target as obj_item_mod_control;
        if (Dm.truthy(ai_action) && user != mod.ai_assistant)
            return null;
        else if (!Dm.truthy(ai_action) && user == mod.ai_assistant)
            return null;
        return await base.Grant(grant_to, __extra);
    }

    // tg: code/modules/mod/mod_actions.dm:25
    public override async DmTask Remove(mob remove_from = null)
    {
        mob user = remove_from;
        obj_item_mod_control mod = target as obj_item_mod_control;
        if (Dm.truthy(ai_action) && user != mod.ai_assistant)
            return;
        else if (!Dm.truthy(ai_action) && user == mod.ai_assistant)
            return;
        await base.Remove(remove_from);
    }

    // tg: code/modules/mod/mod_actions.dm:33
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        obj_item_mod_control mod = target as obj_item_mod_control;
        if (Dm.truthy(mod.malfunctioning) && prob(MOD_MALFUNCTION_PROB))
        {
            mod.balloon_alert(usr, I18n.T("button malfunctions!"));
            return FALSE;
        }
        return TRUE;
    }
}

// tg: code/modules/mod/mod_actions.dm:40
public partial class datum_action_item_action_mod_deploy : datum_action_item_action_mod
{
    // tg: code/modules/mod/mod_actions.dm:45
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        object dot = await base.do_effect(trigger_flags);
        if (!Dm.truthy(dot))
            return dot;
        obj_item_mod_control mod = target as obj_item_mod_control;
        if (Dm.truthy(Dm.bitand(trigger_flags, TRIGGER_SECONDARY_ACTION)))
            await mod.quick_deploy(usr);
        else
            await mod.choose_deploy(usr);
        return dot;
    }
}

// tg: code/modules/mod/mod_actions.dm:59
public partial class datum_action_item_action_mod_activate : datum_action_item_action_mod
{
    // tg: code/modules/mod/mod_actions.dm:65
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        object dot = await base.do_effect(trigger_flags);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(Dm.bitand(trigger_flags, TRIGGER_SECONDARY_ACTION)) && !Dm.truthy(ready))
        {
            ready = TRUE;
            button_icon_state = "activate-ready";
            await build_all_button_icons();
            _addtimer(Dm.@new<datum_callback>(this, nameof(reset_ready)), 3 * SECONDS, file: "code/modules/mod/mod_actions.dm", line: 72);
            return dot;
        }
        obj_item_mod_control mod = target as obj_item_mod_control;
        await reset_ready();
        await mod.toggle_activate(usr);
        return dot;
    }

    /// Resets the state requiring to be doubleclicked again.
    // tg: code/modules/mod/mod_actions.dm:80
    public virtual async DmTask reset_ready()
    {
        ready = FALSE;
        button_icon_state = Dm.initial(this, nameof(button_icon_state)) as string;
        await build_all_button_icons();
    }
}

// tg: code/modules/mod/mod_actions.dm:88
public partial class datum_action_item_action_mod_module : datum_action_item_action_mod
{
    // tg: code/modules/mod/mod_actions.dm:93
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        object dot = await base.do_effect(trigger_flags);
        if (!Dm.truthy(dot))
            return dot;
        obj_item_mod_control mod = target as obj_item_mod_control;
        await mod.quick_module(usr);
        return dot;
    }
}

// tg: code/modules/mod/mod_actions.dm:103
public partial class datum_action_item_action_mod_panel : datum_action_item_action_mod
{
    // tg: code/modules/mod/mod_actions.dm:108
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        object dot = await base.do_effect(trigger_flags);
        if (!Dm.truthy(dot))
            return dot;
        obj_item_mod_control mod = target as obj_item_mod_control;
        await mod.ui_interact(usr);
        return dot;
    }
}

// tg: code/modules/mod/mod_actions.dm:118
public partial class datum_action_item_action_mod_pinnable : datum_action_item_action_mod
{
    // tg: code/modules/mod/mod_actions.dm:122
    [DmParams("Target", "user")]
    public override async DmTask New(params object[] __extra)
    {
        object Target = Dm.arg(__extra, 0);
        mob user = Dm.arg<mob>(__extra, 1);
        await base.New(__extra);
        obj_item_mod_control mod = Target as obj_item_mod_control;
        if (user == mod.ai_assistant)
            ai_action = TRUE;
        pinner = user;
        RegisterSignal(user, COMSIG_QDELETING, nameof(pinner_deleted));
    }

    // tg: code/modules/mod/mod_actions.dm:130
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        if (pinner != grant_to)
            return null;
        return await base.Grant(grant_to, __extra);
    }

    /// If the guy whose UI we are pinned to got deleted
    // tg: code/modules/mod/mod_actions.dm:136
    [SignalHandler]
    public virtual void pinner_deleted()
    {
        pinner = null;
        qdel(this);
    }
}

// tg: code/modules/mod/mod_actions.dm:142
public partial class datum_action_item_action_mod_pinnable_module : datum_action_item_action_mod_pinnable
{
    // tg: code/modules/mod/mod_actions.dm:150
    [DmParams("Target", "user", "linked_module")]
    public override async DmTask New(params object[] __extra)
    {
        mob user = Dm.arg<mob>(__extra, 1);
        obj_item_mod_module linked_module = Dm.arg<obj_item_mod_module>(__extra, 2);
        button_icon = linked_module.icon;
        button_icon_state = linked_module.icon_state;
        await base.New(__extra);
        module = linked_module;
        module.pinned_to[REF(user)] = this;
        if (Dm.truthy(Dm.bitand(linked_module.allow_flags, MODULE_ALLOW_INCAPACITATED)))
        {
            // clears check hands and check conscious
            check_flags = NONE;
        }
        name = I18n.T("Activate []", capitalize(linked_module.name));
        desc = I18n.T("Quickly activate [].", linked_module);
        RegisterSignals(linked_module, Dm.list(
            COMSIG_MODULE_ACTIVATED,
            COMSIG_MODULE_DEACTIVATED,
            COMSIG_MODULE_USED
        ), nameof(module_interacted_with));
        RegisterSignal(linked_module, COMSIG_MODULE_COOLDOWN_STARTED, nameof(cooldown_started));
    }

    // tg: code/modules/mod/mod_actions.dm:168
    public override object Destroy(float? force = null)
    {
        deltimer(cooldown_timer);
        UnregisterSignal(module, Dm.list(
            COMSIG_MODULE_ACTIVATED,
            COMSIG_MODULE_DEACTIVATED,
            COMSIG_MODULE_COOLDOWN_STARTED,
            COMSIG_MODULE_USED
        ));
        Dm.remove(ref module.pinned_to, REF(pinner));
        module = null;
        pinner = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mod/mod_actions.dm:181
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        object dot = await base.do_effect(trigger_flags);
        if (!Dm.truthy(dot))
            return dot;
        await module.on_select(owner as mob);
        return dot;
    }

    // tg: code/modules/mod/mod_actions.dm:187
    public override void apply_button_overlay(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        current_button.cut_overlays();
        if (Dm.truthy(@override))
        {
            base.apply_button_overlay(current_button, force);
            return;
        }

        obj_item_mod_control mod = target as obj_item_mod_control;
        if (module == mod.selected_module)
            current_button.add_overlay(Dm.image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "module_selected", layer: FLOAT_LAYER - 0.1f));
        else if (Dm.truthy(module.active))
            current_button.add_overlay(Dm.image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "module_active", layer: FLOAT_LAYER - 0.1f));
        if (!(module.cooldown_timer <= world.time)) // !COOLDOWN_FINISHED(module, cooldown_timer)
            current_button.add_overlay(Dm.image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "module_cooldown"));
        base.apply_button_overlay(current_button, force);
    }

    // tg: code/modules/mod/mod_actions.dm:201
    [SignalHandler]
    public virtual void module_interacted_with(datum source = null)
    {
        Waiting.NoSleep(build_all_button_icons(Dm.num(Dm.bitor(UPDATE_BUTTON_OVERLAY, UPDATE_BUTTON_STATUS))));
    }

    // tg: code/modules/mod/mod_actions.dm:206
    [SignalHandler]
    public virtual void cooldown_started(datum source = null, object cooldown_time = null)
    {
        deltimer(cooldown_timer);
        Waiting.NoSleep(build_all_button_icons(UPDATE_BUTTON_OVERLAY));
        if (Dm.eq(cooldown_time, 0))
            return;
        cooldown_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(build_all_button_icons), UPDATE_BUTTON_OVERLAY), Dm.num(cooldown_time) + 1, TIMER_STOPPABLE, file: "code/modules/mod/mod_actions.dm", line: 213);
    }
}
