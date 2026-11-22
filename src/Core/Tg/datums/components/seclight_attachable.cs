// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/seclight_attachable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298): the flashlight mount of helmets and guns.

// tg: code/datums/components/seclight_attachable.dm:5
public partial class datum_component_seclite_attachable : datum_component
{
    /// Static typecache of all lights we consider seclites (all lights we can attach).
    private static DmList valid_lights => typecacheof(Dm.list(typeof(obj_item_flashlight_seclite))) as DmList;

    // tg: code/datums/components/seclight_attachable.dm:30
    [DmParams("starting_light", "is_light_removable", "light_icon_state", "light_overlay_icon", "light_overlay", "overlay_x", "overlay_y")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object starting_light = Dm.arg(__extra, 0);
        object removable = Dm.arg(__extra, 1) ?? TRUE;
        object icon_state_arg = Dm.arg(__extra, 2);
        object overlay_icon = Dm.arg(__extra, 3);
        object overlay_state = Dm.arg(__extra, 4);
        object x = Dm.arg(__extra, 5) ?? 0;
        object y = Dm.arg(__extra, 6) ?? 0;
        object dot = await base.Initialize(Dm.forward(__extra, starting_light, removable, icon_state_arg, overlay_icon, overlay_state, x, y));
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        is_light_removable = Dm.truthy(removable) ? TRUE : FALSE;
        light_icon_state = icon_state_arg;
        light_overlay_icon = overlay_icon;
        light_overlay = overlay_state;
        overlay_x = Dm.num(x);
        overlay_y = Dm.num(y);

        if (starting_light is obj_item_flashlight_seclite light_to_add)
            add_light(light_to_add);
        return dot;
    }

    // tg: code/datums/components/seclight_attachable.dm:53
    public override object Destroy(float? force = null)
    {
        if (light != null)
            remove_light();
        return base.Destroy(force);
    }

    // Inheriting component allows lights to be added externally to things which already have a mount.
    // tg: code/datums/components/seclight_attachable.dm:59
    [DmParams("new_component", "original", "starting_light", "is_light_removable", "light_icon_state", "light_overlay_icon", "light_overlay", "overlay_x", "overlay_y")]
    public override async DmTask InheritComponent(datum_component new_comp = null, object i_am_original = null, params object[] __extra)
    {
        if (!Dm.truthy(i_am_original))
            return;

        object starting_light = Dm.arg(__extra, 0);
        object removable = Dm.arg(__extra, 1) ?? TRUE;
        object icon_state_arg = Dm.arg(__extra, 2);
        object overlay_icon = Dm.arg(__extra, 3);
        object overlay_state = Dm.arg(__extra, 4);
        object x = Dm.arg(__extra, 5);
        object y = Dm.arg(__extra, 6);
        is_light_removable = Dm.truthy(removable) ? TRUE : FALSE;

        // For the rest of these arguments, default to what already exists
        if (Dm.truthy(icon_state_arg))
            light_icon_state = icon_state_arg;
        if (Dm.truthy(overlay_icon))
            light_overlay_icon = overlay_icon;
        if (Dm.truthy(overlay_state))
            light_overlay = overlay_state;
        if (Dm.truthy(x))
            overlay_x = Dm.num(x);
        if (Dm.truthy(x)) // tg's slip: overlay_x guards overlay_y
            overlay_y = Dm.num(y);

        if (starting_light is obj_item_flashlight_seclite light_to_add)
            add_light(light_to_add);
    }

    // tg: code/datums/components/seclight_attachable.dm:91
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_OBJ_DECONSTRUCT, nameof(on_parent_deconstructed));
        RegisterSignal(parent, COMSIG_ATOM_EXITED, nameof(on_light_exit));
        RegisterSignal(parent, COMSIG_ATOM_TOOL_ACT(TOOL_SCREWDRIVER), nameof(on_screwdriver));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_ICON_STATE, nameof(on_update_icon_state));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_update_overlays));
        RegisterSignal(parent, COMSIG_ITEM_UI_ACTION_CLICK, nameof(on_action_click));
        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(on_attackby));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_ATOM_SABOTEUR_ACT, nameof(on_hit_by_saboteur));
        RegisterSignal(parent, COMSIG_QDELETING, nameof(on_parent_deleted));
    }

    // tg: code/datums/components/seclight_attachable.dm:103
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_OBJ_DECONSTRUCT,
            COMSIG_ATOM_EXITED,
            COMSIG_ATOM_TOOL_ACT(TOOL_SCREWDRIVER),
            COMSIG_ATOM_UPDATE_ICON_STATE,
            COMSIG_ATOM_UPDATE_OVERLAYS,
            COMSIG_ITEM_UI_ACTION_CLICK,
            COMSIG_ATOM_ATTACKBY,
            COMSIG_ATOM_EXAMINE,
            COMSIG_ATOM_SABOTEUR_ACT,
            COMSIG_QDELETING));
    }

    /// Sets a new light as our current light for our parent.
    // tg: code/datums/components/seclight_attachable.dm:118
    public virtual void add_light(obj_item_flashlight_seclite new_light = null, mob attacher = null)
    {
        if (light != null)
            throw Dm.CRASH(Dm.text("[] tried to add a new light when it already had one.", type), "code/datums/components/seclight_attachable.dm", 120);

        light = new_light;

        light.set_light_flags(Dm.bitor(light.light_flags, LIGHT_ATTACHED));
        // We may already exist within in our parent's contents... But if we don't move it over now
        if (light.loc != parent)
            Waiting.NoSleep(light.forceMove(parent as atom));

        // We already have an action for the light for some reason? Clean it up
        if (toggle_action_ref?.resolve() != null)
        {
            stack_trace(Dm.text("[] - add_light had an existing toggle action when add_light was called.", type), "datums/components/seclight_attachable.dm", 0);
            qdel(toggle_action_ref.resolve());
            toggle_action_ref = null;
        }

        // Make a new toggle light item action for our parent
        obj_item item_parent = parent as obj_item;
        datum_action_item_action_toggle_seclight toggle_action = Waiting.NoSleep(item_parent.add_item_action(typeof(datum_action_item_action_toggle_seclight))) as datum_action_item_action_toggle_seclight;
        toggle_action_ref = WEAKREF(toggle_action) as datum_weakref;

        update_light();
    }

    /// Removes the current light from our parent.
    // tg: code/datums/components/seclight_attachable.dm:142
    public virtual void remove_light()
    {
        // Our action may be linked to our parent,
        // but it's really sourced from our light. Get rid of it.
        qdel(toggle_action_ref?.resolve());
        toggle_action_ref = null;

        // It is possible the light was removed by being deleted.
        if (!QDELETED(light))
        {
            light.set_light_flags(Dm.bitand(light.light_flags, ~LIGHT_ATTACHED));
            light.update_brightness();
        }

        light = null;
        update_light();
    }

    /// Toggles the light within on or off.
    /// Returns TRUE if there is a light inside, FALSE otherwise.
    // tg: code/datums/components/seclight_attachable.dm:157
    public virtual async DmTask<object> toggle_light(mob user = null)
    {
        if (light == null)
            return FALSE;

        object successful_toggle = await light.toggle_light(user);
        if (!Dm.truthy(successful_toggle))
            return TRUE;
        user.balloon_alert(user, Dm.text("[] toggled []", light.name, Dm.truthy(light.light_on) ? "on" : "off"));
        update_light();
        return TRUE;
    }

    /// Called after the a light is added, removed, or toggles.
    /// Ensures all of our appearances look correct for the new light state.
    // tg: code/datums/components/seclight_attachable.dm:170
    public virtual void update_light()
    {
        obj_item item_parent = parent as obj_item;
        item_parent.update_appearance();
        Waiting.Detach(item_parent.update_item_action_buttons());
    }

    /// Signal proc for [COMSIG_ATOM_EXITED] that handles our light being removed or deleted from our parent.
    // tg: code/datums/components/seclight_attachable.dm:176
    [SignalHandler]
    public virtual void on_light_exit(obj_item source = null, atom_movable gone = null, object direction = null)
    {
        if (gone == light)
            remove_light();
    }

    /// Signal proc for [COMSIG_OBJ_DECONSTRUCT] that drops our light to the ground if our parent is deconstructed.
    // tg: code/datums/components/seclight_attachable.dm:183
    [SignalHandler]
    public virtual void on_parent_deconstructed(obj_item source = null, object disassembled = null)
    {
        // Our light is gone already - Probably destroyed by whatever destroyed our parent. Just remove it.
        if (QDELETED(light) || !Dm.truthy(is_light_removable))
        {
            remove_light();
            return;
        }

        // We were deconstructed in any other way, so we can just drop the light on the ground (which removes it via signal).
        Waiting.NoSleep(light.forceMove(source.drop_location() as atom));
    }

    /// Signal proc for [COMSIG_QDELETING] that deletes our light if our parent is deleted.
    // tg: code/datums/components/seclight_attachable.dm:195
    [SignalHandler]
    public virtual void on_parent_deleted(obj_item source = null, params object[] __extra)
    {
        qdel(light);
        light = null;
    }

    /// Signal proc for [COMSIG_ITEM_UI_ACTION_CLICK] that toggles our light on and off if our action button is clicked.
    // tg: code/datums/components/seclight_attachable.dm:201
    [SignalHandler]
    public virtual object on_action_click(obj_item source = null, mob user = null, datum action = null)
    {
        // This isn't OUR action specifically, we don't care.
        if (!IS_WEAKREF_OF(action, toggle_action_ref))
            return null;

        // Toggle light fails = no light attached = shouldn't be possible
        if (!Dm.truthy(Waiting.NoSleep(toggle_light(user))))
            throw Dm.CRASH(Dm.text("[] - on_action_click somehow both HAD AN ACTION and also HAD A TRIGGERABLE ACTION, without having an attached light.", type), "code/datums/components/seclight_attachable.dm", 210);

        return COMPONENT_ACTION_HANDLED;
    }

    /// Signal proc for [COMSIG_ATOM_ATTACKBY] that allows a user to attach a seclite by hitting our parent with it.
    // tg: code/datums/components/seclight_attachable.dm:215
    [SignalHandler]
    public virtual object on_attackby(obj_item source = null, obj_item attacking_item = null, mob attacker = null, DmList modifiers = null)
    {
        if (!Dm.truthy(is_type_in_typecache(attacking_item, valid_lights)))
            return null;

        if (light != null)
        {
            source.balloon_alert(attacker, I18n.T("already has \\a []!", light));
            return null;
        }

        if (!Dm.truthy(Waiting.NoSleep(attacker.transferItemToLoc(attacking_item, source))))
            return null;

        add_light(attacking_item as obj_item_flashlight_seclite, attacker);
        source.balloon_alert(attacker, I18n.T("attached []", attacking_item));
        return COMPONENT_NO_AFTERATTACK;
    }

    /// Signal proc for [COMSIG_ATOM_TOOL_ACT] via [TOOL_SCREWDRIVER] that removes any attached seclite.
    // tg: code/datums/components/seclight_attachable.dm:233
    [SignalHandler]
    public virtual object on_screwdriver(obj_item source = null, mob user = null, obj_item tool = null)
    {
        if (light == null || !Dm.truthy(is_light_removable))
            return null;

        Waiting.Detach(unscrew_light(source, user, tool)); // INVOKE_ASYNC
        return ITEM_INTERACT_BLOCKING;
    }

    /// Invoked asyncronously from [proc/on_screwdriver]. Handles removing the light from our parent.
    // tg: code/datums/components/seclight_attachable.dm:243
    public virtual async DmTask unscrew_light(obj_item source = null, mob user = null, obj_item tool = null)
    {
        tool?.play_tool_sound(source);
        source.balloon_alert(user, I18n.T("unscrewed []", light));

        obj_item_flashlight_seclite to_remove = light;

        // The forcemove here will call exited on the light, and automatically update our references / etc
        await to_remove.forceMove(source.drop_location() as atom);
        if (Dm.truthy(await source.Adjacent(user)) && !issilicon(user))
            await user.put_in_hands(to_remove);
    }

    /// Signal proc for [COMSIG_ATOM_EXAMINE] that shows our item can have / does have a seclite attached.
    // tg: code/datums/components/seclight_attachable.dm:255
    [SignalHandler]
    public virtual void on_examine(obj_item source = null, mob examiner = null, DmList examine_list = null)
    {
        if (light != null)
            examine_list.Add(I18n.T("It has \\a [] [].", light, Dm.truthy(is_light_removable) ? I18n.T("mounted on it with a few <b>screws</b>") : I18n.T("permanently mounted on it")));
        else
            examine_list.Add(I18n.T("It has a mounting point for a <b>seclite</b>."));
    }

    /// Signal proc for [COMSIG_ATOM_UPDATE_OVERLAYS] that updates our parent with our seclite overlays, if we have some.
    // tg: code/datums/components/seclight_attachable.dm:264
    [SignalHandler]
    public virtual void on_update_overlays(obj_item source = null, DmList overlays = null)
    {
        // No overlays to add, no reason to run
        if (!Dm.truthy(light_overlay) || !Dm.truthy(light_overlay_icon))
            return;
        // No light, nothing to add
        if (light == null)
            return;

        string overlay_state = Dm.text("[][]", light_overlay, Dm.truthy(light.light_on) ? "_on" : "");
        mutable_appearance flashlight_overlay = mutable_appearance(light_overlay_icon, overlay_state) as mutable_appearance;
        flashlight_overlay.pixel_w = overlay_x;
        flashlight_overlay.pixel_z = overlay_y;
        overlays.Add(flashlight_overlay);
    }

    /// Signal proc for [COMSIG_ATOM_UPDATE_ICON_STATE] that updates our parent's icon state, if we have one.
    // tg: code/datums/components/seclight_attachable.dm:281
    [SignalHandler]
    public virtual void on_update_icon_state(obj_item source = null, params object[] __extra)
    {
        // No icon state to set, no reason to run
        if (!Dm.truthy(light_icon_state))
            return;

        // Get the "base icon state" to work on
        object base_state = Dm.truthy(source.base_icon_state) ? source.base_icon_state : Dm.initial(source, (obj_item p) => p.icon_state);
        // Updates our icon state based on our light state.
        if (light != null)
        {
            source.icon_state = Dm.text("[]-[][]", base_state, light_icon_state, Dm.truthy(light.light_on) ? "-on" : "");
        }
        // Reset their icon state when if we've got no light.
        else if (!Dm.eq(source.icon_state, base_state))
        {
            // Yes, this might mess with other icon state alterations,
            // but that's the downside of using icon states over overlays.
            source.icon_state = Dm.str(base_state);
        }
    }

    //turns the light off for a few seconds.
    // tg: code/datums/components/seclight_attachable.dm:301
    [SignalHandler]
    public virtual object on_hit_by_saboteur(datum source = null, object disrupt_duration = null)
    {
        object dot = light != null ? Waiting.NoSleep(light.on_saboteur(source, disrupt_duration)) : null; // a runtime in tg with no light
        update_light();
        return dot;
    }
}
