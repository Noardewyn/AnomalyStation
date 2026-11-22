using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// Owners (docs/ai/specs/m2-ownership.md): the contract is the M2 contracts step's (frozen); M2 P4 implements it with the
// ClickOn/UnarmedAttack subset (click.dm, other_mobs.dm, and tg's /client/Click of client_procs.dm:866 in a partial file
// of its own); M2 P6 calls it for ActionKind.Click.

// M4a (contracts, docs/ai/specs/interaction.md sections 2-3 and 6): the click's target is any atom the player can click in
// tg (an entity, a turf, a screen object of the player's client.screen); the params carry tg's icon-x, icon-y and
// screen-loc; Hover is tg's MouseEntered. M4a P6 implements the new bodies (and extends Click's location rule for screen
// objects), P1 translates the tg procs they reach (atom/MouseEntered, SSmouse_entered, on_mouse_enter).

public static partial class ClientSeam
{
    /// <summary>
    /// The player clicked <paramref name="target"/> (the camera ray's first entity, scope 2f): BYOND's side of a click,
    /// tg's <c>/client/Click(object, location, control, params)</c> (<c>client_procs.dm:866</c>: the click intercept, the
    /// per-minute click limit) and BYOND's default after it, <c>object.Click(location, control, params)</c>, whose tg
    /// override is <c>usr.ClickOn(src, params)</c> (<c>_onclick/click.dm:45</c>): <c>next_click</c>, the incapacitated
    /// check, <c>face_atom</c>, <c>next_move</c>, reach (<c>Adjacent</c> on tiles), <c>changeNext_move(CLICK_CD_MELEE)</c>,
    /// <c>UnarmedAttack</c> (a right click with an empty hand on a mob is the shove: <c>disarm</c>). <paramref name="params"/>
    /// is <see cref="ClickParams(string, bool, bool, bool)"/>' text. With <c>usr</c> the client's mob; the location is the target's turf and the
    /// control BYOND's map control (tg's <c>SKIN_MAPWINDOW_MAP</c>, <c>"map_screen.map"</c> in this snapshot). Detached, as BYOND starts a verb.
    /// </summary>
    public static void Click(client player, atom target, string @params)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null)
            return;
        if (target == null || target.gc_destroyed != null)
            return; // BYOND sends no click for an object that is gone
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = player.mob;
            // BYOND: a map click's location is the turf the object stands on (the turf itself for a turf); the control is
            // the map's (tg's SKIN_MAPWINDOW_MAP, "map_screen.map" in this snapshot's skin). M4a: a click on an object of the
            // player's client.screen (a HUD entry, or an item shown in an open storage) is a HUD click, whose location BYOND
            // gives as the screen_loc text. The translated Click declares `atom location`, so no text can be passed: the
            // location is null, and the screen_loc travels in the params (ClickParams' screen-loc), where tg reads
            // position data from (every screen Click of tg only forwards its location argument).
            // DEVIATION: the location argument of a HUD click is null instead of the screen_loc text (typed `atom` in the translation).
            // Protocol version 12: a clickable vis_contents child of a screen object (the item in a hand or a slot) is a HUD click too.
            atom location = IsScreenTarget(player, target) ? null : target is turf ? target : GlobalProcs.get_turf(target);
            if (@params != null && @params.Contains(AIM_PARAM))
                FlightSeam.NoteAim(player.mob, Dm.params2list(@params)); // DV416: a throw this click makes goes where the crosshair is
            Waiting.Detach(player.Click(target, location, Defines.SKIN_MAPWINDOW_MAP, @params));
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    /// <summary>
    /// Version 81 (D328): the player throws at <paramref name="target"/>: tg's click on it in throw mode, whatever the throw mode is: the mob's
    /// throw mode is tg's hold (<c>THROW_MODE_HOLD</c>, the hold_throw_mode keybinding's) for this click, and what it was again after, so tg's
    /// ClickOn makes every check of a throw (the click cooldown, the incapacitated check, <c>face_atom</c>, <c>CLICK_CD_THROW</c>,
    /// <c>throw_item</c>). A mob that cannot throw (no <c>TRAIT_CAN_THROW_ITEMS</c>: tg's throw mode stays off) clicks as tg would.
    /// <paramref name="params"/> is a left click's (<see cref="ClickParams(string, bool, bool, bool)"/>) with the camera's aim.
    /// </summary>
    public static void Throw(client player, atom target, string @params)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.mob is not mob_living thrower || !player.dm_connected || player.gc_destroyed != null || target == null || target.gc_destroyed != null)
        {
            Click(player, target, @params);
            return;
        }
        World world = World.Current;
        object caller_usr = world.usr;
        float mode = thrower.throw_mode;
        DmList clicks = GlobalProcs.SSinput?.verb_queue;
        float queuedBefore = clicks?.len ?? 0;
        try
        {
            world.usr = thrower;
            if (Dm.num(mode) < Defines.THROW_MODE_HOLD)
                thrower.throw_mode_on(Defines.THROW_MODE_HOLD);
            Click(player, target, @params);
        }
        finally
        {
            // the click ran up to its throw (throw_item turns a toggle off; a held mode stays for its key): the mode it had before
            if (!GlobalProcs.QDELETED(thrower) && Dm.num(thrower.throw_mode) == Defines.THROW_MODE_HOLD && Dm.num(mode) < Defines.THROW_MODE_HOLD)
            {
                // an overloaded tick (client/Click's _queue_verb) left the click to SSinput's next run: the mode goes off right after it there,
                // before the next tick's commands (the command phase follows the Master's drain)
                if ((clicks?.len ?? 0) > queuedBefore)
                    GlobalProcs.SSinput.queue_verb(Dm.@new<datum_callback_verb_callback>(thrower, nameof(mob_living.throw_mode_off), Defines.THROW_MODE_HOLD));
                else
                    thrower.throw_mode_off(Defines.THROW_MODE_HOLD);
            }
            world.usr = caller_usr;
        }
    }

    /// <summary>
    /// Version 51 (M10x slice C): the player double clicked <paramref name="target"/>: BYOND's <c>object.DblClick(location, control,
    /// params)</c> (tg's override is <c>usr.DblClickOn(src, params)</c>, <c>_onclick/click.dm:51</c>; tg's <c>/client</c> has no
    /// DblClick of its own), with <c>usr</c> the client's mob and the location and control of <see cref="Click"/>. BYOND calls it on
    /// the second press of a double click, after that press's own Click. Detached, as BYOND starts a verb.
    /// </summary>
    public static void DblClick(client player, atom target, string @params)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null)
            return;
        if (target == null || target.gc_destroyed != null)
            return;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = player.mob;
            atom location = IsScreenTarget(player, target) ? null : target is turf ? target : GlobalProcs.get_turf(target);
            Waiting.Detach(target.DblClick(location, Defines.SKIN_MAPWINDOW_MAP, @params));
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    /// <summary>
    /// M4a: tg's click <c>params</c> text with the version-4 keys, as BYOND builds it: <see cref="ClickParams(string, bool, bool, bool)"/>'s
    /// keys plus <c>icon-x</c> and <c>icon-y</c> (tg's <c>ICON_X</c>, <c>ICON_Y</c>: the pixel within the clicked icon, 1 to 32;
    /// left out when 0) and, for a screen object, <c>screen-loc</c> (tg's <c>SCREEN_LOC</c>: the object's own
    /// <c>screen_loc</c>, filled by the server; left out when null). BYOND's key order (M4a P6 writes it from the BYOND
    /// reference, interaction.md section 3).
    /// </summary>
    public static string ClickParams(string button, bool shift, bool ctrl, bool alt, int iconX, int iconY, string screenLoc = null)
    {
        if (iconX < 0 || iconX > ICON_SIZE_PIXELS)
            throw new ArgumentOutOfRangeException(nameof(iconX), iconX, "icon-x is 1 to 32 (0 for none).");
        if (iconY < 0 || iconY > ICON_SIZE_PIXELS)
            throw new ArgumentOutOfRangeException(nameof(iconY), iconY, "icon-y is 1 to 32 (0 for none).");
        string text = "";
        if (iconX > 0) text += Defines.ICON_X + "=" + iconX + ";";
        if (iconY > 0) text += Defines.ICON_Y + "=" + iconY + ";";
        text += ClickParams(button, shift, ctrl, alt);
        // The screen_loc is tg's own text ("WEST,CENTER-2", "EAST-1:28,CENTER-1:15"); a value that would break the params
        // list (';', '&' or '=') is left out, not escaped: no tg screen_loc has one.
        if (!string.IsNullOrEmpty(screenLoc) && screenLoc.AsSpan().IndexOfAny(';', '&', '=') < 0)
            text += ";" + Defines.SCREEN_LOC + "=" + screenLoc;
        return text;
    }

    private const int ICON_SIZE_PIXELS = 32;

    /// <summary>
    /// Protocol version 12: true when BYOND lets a click on one of <paramref name="child"/>'s pixels in another atom's <c>vis_contents</c>
    /// reach <paramref name="child"/> itself. BYOND ref <c>/atom/var/vis_flags</c>: with <c>VIS_INHERIT_ID</c> "the object will act as if it is
    /// part of the parent, for purposes of mouse interaction", so the holder takes the click; <c>mouse_opacity</c> 0 lets every click through;
    /// a <c>VIS_HIDE</c> object is not drawn. (OpenDream's renderer, DreamViewOverlay: a direct vis child keeps its own click id unless
    /// <c>VIS_INHERIT_ID</c>.)
    /// </summary>
    public static bool IsClickableVisChild(atom child)
    {
        if (child == null || child.gc_destroyed != null)
            return false;
        int flags = (int)child.vis_flags;
        return (flags & (int)Dm.VIS_INHERIT_ID) == 0 && (flags & (int)Dm.VIS_HIDE) == 0 && child.mouse_opacity != Defines.MOUSE_OPACITY_TRANSPARENT;
    }

    /// <summary>
    /// Protocol version 12: the object of <paramref name="player"/>'s <c>client.screen</c> whose <c>vis_contents</c> shows
    /// <paramref name="target"/> as a clickable child of its own (<see cref="IsClickableVisChild"/>: tg's hand and inventory slots hold their
    /// item there, <c>inventory_slot.dm:55</c>); null when none does. Only a direct child counts.
    /// </summary>
    public static atom_movable ScreenHolderOf(client player, atom target)
    {
        if (player == null || target == null || !IsClickableVisChild(target))
            return null;
        DmList screen = player.screen;
        foreach (object holder in Dm.each_cast<object>(target.vis_locs))
            if (holder is atom_movable shown && shown.gc_destroyed == null && Dm.@in(shown, screen))
                return shown;
        return null;
    }

    /// <summary>
    /// True when a click on <paramref name="target"/> is a HUD click of <paramref name="player"/>: the target is in its <c>client.screen</c>, or a
    /// clickable <c>vis_contents</c> child of an object there (<see cref="ScreenHolderOf"/>).
    /// </summary>
    public static bool IsScreenTarget(client player, atom target)
        => target != null && (Dm.@in(target, player.screen) || ScreenHolderOf(player, target) != null);

    /// <summary>
    /// M4a: the player's crosshair moved onto <paramref name="target"/> (an entity, a turf or a screen object): BYOND's
    /// <c>MouseEntered(location, control, params)</c> on the atom with <c>usr</c> the client's mob, tg's
    /// <c>/atom/MouseEntered</c> (<c>game/atom/_atom.dm:862</c>), which hands the atom to <c>SSmouse_entered</c>; its fire runs
    /// <c>on_mouse_enter(client)</c> once per tick for the last hovered atom, and the screentip it builds reaches
    /// <see cref="ScreentipSeam"/>. Not detached: tg's <c>MouseEntered</c> does not sleep.
    /// </summary>
    public static void Hover(client player, atom target)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null)
            return;
        if (target == null || target.gc_destroyed != null)
            return; // BYOND sends no MouseEntered for an object that is gone
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = player.mob;
            // Protocol version 14: BYOND calls MouseExited on the object the mouse leaves before MouseEntered on the one it
            // enters (BYOND ref /atom/proc/MouseExited); tg clears its hover states there (the suit display, an inventory
            // slot's outline, the zone selector's part, an action button's tooltip).
            atom previous = player.dm_mouse_over;
            player.dm_mouse_over = target;
            // DEVIATION(DV65): the turf the crosshair aims at, for building where the player looks.
            if (!IsScreenTarget(player, target) && GlobalProcs.get_turf(target) is turf aimed)
                player.dm_aim_turf = aimed;
            if (previous != null && previous != target)
                Exit(player, previous);
            // The location is the target's turf for a map object and the screen_loc for a HUD one (null here, as in Click);
            // the control is the map's. tg's MouseEntered only keeps the atom for SSmouse_entered; a HUD object's tooltip
            // (openToolTip, protocol version 19) needs the params (HoverParams).
            atom location = LocationOf(player, target);
            Waiting.NoSleep(target.MouseEntered(location, Defines.SKIN_MAPWINDOW_MAP, HoverParams(player, target)));
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    /// <summary>
    /// Protocol version 19: BYOND's <c>params</c> of a <c>MouseEntered</c>. BYOND gives the mouse's icon pixel and its
    /// <c>screen-loc</c>; the hover carries no pixel, so the params are the <c>screen-loc</c> of a HUD object (its own
    /// <c>screen_loc</c>, or its holder's for a clickable vis child: the item in a hand or a slot), as a click's
    /// (<see cref="ClickParams(string, bool, bool, bool, int, int, string)"/>), and empty for a map thing (whose screen-loc
    /// is none, DV45). tg's tooltip (<c>/datum/tooltip/show</c>) shows nothing without params.
    /// </summary>
    public static string HoverParams(client player, atom target)
    {
        if (player == null || target == null || !IsScreenTarget(player, target))
            return "";
        string screenLoc = Dm.@in(target, player.screen) ? (target as atom_movable)?.screen_loc : ScreenHolderOf(player, target)?.screen_loc;
        if (string.IsNullOrEmpty(screenLoc) || screenLoc.AsSpan().IndexOfAny(';', '&', '=') >= 0)
            return "";
        return Defines.SCREEN_LOC + "=" + screenLoc;
    }

    /// <summary>
    /// Protocol version 14 (M4c slice 3): the player's pointer left the hovered thing over nothing: BYOND's
    /// <c>MouseExited(location, control, params)</c> on it, with <c>usr</c> the client's mob and the location rule of
    /// <see cref="Hover"/>. Nothing happens when nothing is hovered or the hovered object is gone (BYOND exits nothing deleted).
    /// </summary>
    public static void Unhover(client player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null)
            return;
        atom previous = player.dm_mouse_over;
        player.dm_mouse_over = null;
        if (previous == null)
            return;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = player.mob;
            Exit(player, previous);
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    // BYOND's MouseExited on an object the pointer left (usr set by the caller); an object that is gone gets none.
    private static void Exit(client player, atom left)
    {
        if (left.gc_destroyed != null)
            return;
        left.MouseExited(LocationOf(player, left), Defines.SKIN_MAPWINDOW_MAP, "");
    }

    private static atom LocationOf(client player, atom target)
        => IsScreenTarget(player, target) ? null : target is turf ? target : GlobalProcs.get_turf(target);
}
