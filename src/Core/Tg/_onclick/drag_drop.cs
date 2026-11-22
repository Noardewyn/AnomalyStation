// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/drag_drop.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (drag and drop, protocol version 17; docs/ai/specs/interaction.md section 8). BYOND's own halves are the engine
// part's: /client/MouseDrop's and /client/MouseDrag's `..()` (MouseDrop__d0, MouseDrag__d0 in Builtin/client.builtin.cs) and
// /atom/MouseDrag's root (Builtin/atom.builtin.cs). The host calls them from the client's Drop action (ClientSeam.Drop): BYOND
// calls MouseDrag on every move of a drag and MouseDrop on the release; the client sends only the drop, so the seam runs tg's
// MouseDrag once before the MouseDrop (ClientSeam.Drop says how the drag's start time is kept). The host sends no MouseDown or
// MouseUp (the press and the release of a click travel as one Click action): the autoclick they start (full-auto weapons,
// CanItemAutoclick) arrives with those weapons (docs/ai/specs/not-translated.md). The interaction guard of D132 is at the top of
// mouse_drop_dragged and mouse_drop_receive (interaction.md 7, DV27): an inert type's own handler is refused, never run as its
// parent's.

/*
	MouseDrop:

	Called on the atom you're dragging.  In a lot of circumstances we want to use the
	receiving object instead, so that's the default action.  This allows you to drag
	almost anything into a trash can.
*/
// The [DmPath] of /atom is on its engine part Builtin/atom.builtin.cs (rules 2.3).
public partial class atom : datum
{
    // tg: code/_onclick/drag_drop.dm:8
    [ShouldNotOverride]
    public virtual async DmTask<object> MouseDrop(atom over = null, object src_location = null, object over_location = null, object src_control = null, object over_control = null, object @params = null)
    {
        if (!Dm.truthy(usr) || !Dm.truthy(over))
            return null;
        if (usr is mob dragger && (DevCommands.ShieldsFrom(dragger, this) || DevCommands.ShieldsFrom(dragger, over)))
            return null; // DEVIATION(DV258): the dev invulnerability (no strip menu, no dragging the mob away)

        object usrclient = GET_CLIENT(usr);
        object proximity_check = usrclient switch
        {
            client player => await player.check_drag_proximity(this, over, src_location, over_location, src_control, over_control, @params),
            datum_client_interface mock => mock.check_drag_proximity(this, over, src_location, over_location, src_control, over_control, @params),
            _ => throw new DmRuntimeException("Cannot execute null.check_drag_proximity()."),
        };
        if (Dm.truthy(proximity_check))
            return proximity_check;

        await base_mouse_drop_handler(over, src_location, over_location, @params);
        return null;
    }

    /**
     * Called when all sanity checks for mouse dropping have passed. Handles adjacency & other sanity checks before delegating the event
     * down to lower level handlers. Do not override unless you are trying to create hud & screen elements which do not require proximity
     * or other checks
     */
    // tg: code/_onclick/drag_drop.dm:27
    [DmProtected, ShouldNotOverride]
    public virtual async DmTask base_mouse_drop_handler(atom over = null, object src_location = null, object over_location = null, object @params = null)
    {
        mob user = usr;

        if (Dm.truthy(Dm.bitand(!Dm.truthy(this._listen_lookup?[COMSIG_MOUSEDROP_ONTO]) ? (object)NONE : this._SendSignal(COMSIG_MOUSEDROP_ONTO, Dm.list(this, over, user)), COMPONENT_CANCEL_MOUSEDROP_ONTO)))
            return;

        if (Dm.truthy(Dm.bitand(!Dm.truthy(over._listen_lookup?[COMSIG_MOUSEDROPPED_ONTO]) ? (object)NONE : over._SendSignal(COMSIG_MOUSEDROPPED_ONTO, Dm.list(over, this, user, @params)), COMPONENT_CANCEL_MOUSEDROPPED_ONTO)))
            return;

        // DEVIATION(DV138): D225's tg-first rule; a pair tg leaves empty (no signal cancelled, no own handler proc) is the port's drag
        // gesture (DragGestures, D224-D226: made of tg's own actions), where tg would reach its two empty root handlers.
        if (DragGestures.Applies(this, over, user))
        {
            await DragGestures.Run((obj_item)this, over, user, @params);
            return;
        }

        // only if both dragged object & receiver agree to do checks do we proceed
        object combined_atom_flags = Dm.bitor(interaction_flags_atom, over.interaction_flags_atom);
        if (!Dm.truthy(Dm.bitand(combined_atom_flags, INTERACT_ATOM_MOUSEDROP_IGNORE_CHECKS)))
        {
            //Check for adjacency
            if (!Dm.truthy(Dm.bitand(combined_atom_flags, INTERACT_ATOM_MOUSEDROP_IGNORE_ADJACENT)) && (!Dm.truthy(await IsReachableBy(user)) || !Dm.truthy(await over.IsReachableBy(user))))
                return; // should stop you from dragging through windows

            if (!Dm.truthy(Dm.bitand(combined_atom_flags, INTERACT_ATOM_MOUSEDROP_IGNORE_USABILITY)))
            {
                //Bypass adjacency cause we already checked for it above
                if (!Dm.truthy(await user.can_perform_action(this, Dm.bitor(Dm.bitor(interaction_flags_mouse_drop, over.interaction_flags_mouse_drop), BYPASS_ADJACENCY))))
                    return; // is the mob not able to drag the object with both sides conditions applied
            }
        }

        await mouse_drop_dragged(over, user, src_location, over_location, @params);

        await over.mouse_drop_receive(this, user, @params);
    }

    /// The proc that should be overridden by subtypes to handle mouse drop. Called on the atom being dragged
    // tg: code/_onclick/drag_drop.dm:55
    [DmProtected]
    public virtual async DmTask<object> mouse_drop_dragged(atom over = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        // D132 (DV27): an inert type's own mouse_drop_dragged is refused, not run as this base (interaction.md 7).
        if (InertSeam.MissingOverride(this, "mouse_drop_dragged", user))
            return null;
        return null;
    }

    /// The proc that should be overridden by subtypes to handle mouse drop. Called on the atom receiving a dragged object
    // tg: code/_onclick/drag_drop.dm:61
    [DmProtected]
    public virtual async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        // D132 (DV27): an inert type's own mouse_drop_receive is refused, not run as this base (interaction.md 7).
        if (InertSeam.MissingOverride(this, "mouse_drop_receive", user))
            return null;
        return null;
    }
}

// tg: code/_onclick/drag_drop.dm:69
public partial class client : datum
{
    /// Distance in pixels that we consider "acceptable" from the initial click to the release
    /// Note: this does not account for the position of the object, just where it is on the screen
    // tg: code/_onclick/drag_drop.dm:93  #define LENIENCY_DISTANCE 16 (file-local)
    private const int LENIENCY_DISTANCE = 16;
    /// Accepted time in seconds between the initial click and drag release
    /// Go higher then this and we just don't care anymore
    // tg: code/_onclick/drag_drop.dm:96  #define LENIENCY_TIME (0.1 SECONDS) (file-local)
    private const float LENIENCY_TIME = 0.1f * SECONDS;

    /// Handles treating drags as clicks if they're within some conditions
    /// Does some other stuff adjacent to trying to figure out what the user actually "wanted" to click
    /// Returns TRUE if it caused a click, FALSE otherwise
    // tg: code/_onclick/drag_drop.dm:69
    public virtual async DmTask<object> check_drag_proximity(atom dragging = null, atom over = null, object src_location = null, object over_location = null, object src_control = null, object over_control = null, object @params = null)
    {
        // We will swap which thing we're trying to check for clickability based off the type
        // Assertion is if you drag a turf to anything else, you really just wanted to click the anything else
        // And slightly misseed. I'm not interested in making this game pixel percise, so if it fits our other requirements
        // Lets just let that through yeah?
        atom attempt_click = dragging;
        atom click_from = over;
        object location_to_use = src_location;
        object control_to_use = src_control;
        if (isturf(attempt_click) && !isturf(over))
        {
            // swapppp
            attempt_click = over;
            click_from = dragging;
            location_to_use = over_location;
            control_to_use = over_control;
        }

        if (Dm.truthy(is_drag_clickable(attempt_click, click_from, @params)))
        {
            await Click(attempt_click, location_to_use as atom, control_to_use, @params);
            return TRUE;
        }
        return FALSE;
    }

    /// Does the logic for checking if a drag counts as a click or not
    /// Returns true if it does, false otherwise
    // tg: code/_onclick/drag_drop.dm:100
    public virtual object is_drag_clickable(atom dragging = null, atom over = null, object @params = null)
    {
        if (dragging == over)
            return TRUE;
        if (world.time - drag_start > LENIENCY_TIME) // Time's up bestie
            return FALSE;
        if (!Dm.truthy(get_turf(dragging))) // If it isn't in the world, drop it. This is for things that can move, and we assume hud elements will not have this problem
            return FALSE;
        // Basically, are you trying to buckle someone down, or drag them onto you?
        // If so, we know you must be right about what you want
        if (ismovable(over))
        {
            atom_movable over_movable = over as atom_movable;
            // The buckle bit will cover most mobs, for stupid reasons. still useful here tho
            if (Dm.truthy(over_movable.can_buckle) || over_movable == eye)
                return FALSE;
        }

        DmList modifiers = params2list(@params);
        DmList old_offsets = screen_loc_to_offset(LAZYACCESS(drag_details, SCREEN_LOC), view);
        DmList new_offsets = screen_loc_to_offset(LAZYACCESS(modifiers, SCREEN_LOC), view);

        float distance = sqrt(pow(Dm.num(old_offsets[1]) - Dm.num(new_offsets[1]), 2) + pow(Dm.num(old_offsets[2]) - Dm.num(new_offsets[2]), 2));
        if (distance > LENIENCY_DISTANCE)
            return FALSE;

        return TRUE;
    }

    // tg: code/_onclick/drag_drop.dm:129
    public virtual async DmTask MouseDown(datum @object = null, object location = null, object control = null, object @params = null)
    {
        if (QDELETED(@object)) //Yep, you can click on qdeleted things before they have time to nullspace. Fun.
            return;
        if (Dm.truthy(this._listen_lookup?[COMSIG_CLIENT_MOUSEDOWN]))
            this._SendSignal(COMSIG_CLIENT_MOUSEDOWN, Dm.list(this, @object, location, control, @params));
        if (Dm.truthy(mouse_down_icon))
            mouse_pointer_icon = mouse_down_icon;
        object delay = mob.CanMobAutoclick(@object, location, @params);
        if (Dm.truthy(delay))
        {
            selected_target[1] = @object;
            selected_target[2] = @params;
            while (Dm.truthy(selected_target[1]))
            {
                await Click(selected_target[1] as atom, location as atom, control, selected_target[2]);
                await sleep(Dm.num(delay));
            }
        }
    }

    // tg: code/_onclick/drag_drop.dm:143
    public virtual void MouseUp(object @object = null, object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(Dm.bitand(!Dm.truthy(this._listen_lookup?[COMSIG_CLIENT_MOUSEUP]) ? (object)NONE : this._SendSignal(COMSIG_CLIENT_MOUSEUP, Dm.list(this, @object, location, control, @params)), COMPONENT_CLIENT_MOUSEUP_INTERCEPT)))
            click_intercept_time = world.time;
        if (Dm.truthy(mouse_up_icon))
            mouse_pointer_icon = mouse_up_icon;
        selected_target[1] = null;
    }

    // tg: code/_onclick/drag_drop.dm:173
    public virtual async DmTask MouseDrag(atom src_object = null, atom over_object = null, object src_location = null, object over_location = null, object src_control = null, object over_control = null, object @params = null)
    {
        DmList modifiers = params2list(@params);
        if (Dm.truthy(LAZYACCESS(modifiers, MIDDLE_CLICK)))
        {
            if (Dm.truthy(src_object) && !Dm.eq(src_location, over_location))
            {
                middragtime = world.time;
                middle_drag_atom_ref = WEAKREF(src_object) as datum_weakref;
            }
            else
            {
                middragtime = 0;
                middle_drag_atom_ref = null;
            }
        }
        if (!Dm.truthy(drag_start)) // If we're just starting to drag
        {
            drag_start = world.time;
            drag_details = modifiers.Copy();
        }
        mouseParams = @params as string;
        mouse_location_ref = WEAKREF(over_location as datum) as datum_weakref;
        mouse_object_ref = WEAKREF(over_object) as datum_weakref;
        if (Dm.truthy(selected_target[1]) && Dm.truthy(over_object?.IsAutoclickable()))
        {
            selected_target[1] = over_object;
            selected_target[2] = @params;
        }
        if (Dm.truthy(this._listen_lookup?[COMSIG_CLIENT_MOUSEDRAG]))
            this._SendSignal(COMSIG_CLIENT_MOUSEDRAG, Dm.list(this, src_object, over_object, src_location, over_location, src_control, over_control, @params));
        await MouseDrag__d0(src_object, over_object, src_location, over_location, src_control, over_control, @params);
    }

    // tg: code/_onclick/drag_drop.dm:194
    [ShouldNotOverride]
    public virtual async DmTask MouseDrop(atom src_object = null, atom over_object = null, object src_location = null, object over_location = null, object src_control = null, object over_control = null, object @params = null)
    {
        if (IS_WEAKREF_OF(src_object, middle_drag_atom_ref))
        {
            middragtime = 0;
            middle_drag_atom_ref = null;
        }
        await MouseDrop__d0(src_object, over_object, src_location, over_location, src_control, over_control, @params);
        drag_start = 0;
        drag_details = null;
    }
}

// tg: code/_onclick/drag_drop.dm:151
public partial class mob : atom_movable
{
    // tg: code/_onclick/drag_drop.dm:151
    public virtual object CanMobAutoclick(object @object = null, object location = null, object @params = null)
    {
        return null;
    }
}

// tg: code/_onclick/drag_drop.dm:153
public partial class mob_living_carbon : mob_living
{
    // tg: code/_onclick/drag_drop.dm:153
    public override object CanMobAutoclick(object @object = null, object location = null, object @params = null)
    {
        object dot = null;
        if (!Dm.truthy(((atom)@object).IsAutoclickable()))
            return dot;
        obj_item h = get_active_held_item() as obj_item;
        if (Dm.truthy(h))
            dot = h.CanItemAutoclick(@object, location, @params);
        return dot;
    }
}

// tg: code/_onclick/drag_drop.dm:160
public partial class obj_item : obj
{
    // tg: code/_onclick/drag_drop.dm:160
    public virtual object CanItemAutoclick(object @object = null, object location = null, object @params = null)
    {
        return null;
    }
}

// tg: code/_onclick/drag_drop.dm:162
public partial class atom : datum
{
    // tg: code/_onclick/drag_drop.dm:162
    public virtual object IsAutoclickable()
    {
        return TRUE;
    }
}

// tg: code/_onclick/drag_drop.dm:165
public partial class atom_movable_screen : atom_movable
{
    // tg: code/_onclick/drag_drop.dm:165
    public override object IsAutoclickable()
    {
        return FALSE;
    }
}

// tg: code/_onclick/drag_drop.dm:168
public partial class atom_movable_screen_click_catcher : atom_movable_screen
{
    // tg: code/_onclick/drag_drop.dm:168
    public override object IsAutoclickable()
    {
        return TRUE;
    }
}
