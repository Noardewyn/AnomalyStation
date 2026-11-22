// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/screen_objects/map_view.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M10 slice D, D268). The popup plane master groups and the map's parallax are 2D presentation the 3D client does not
// draw (Dm.Deferred at each place); the client draws the turfs in vis_contents as a second 3D view in the tgui window (the camera
// console's), and the server's interest takes them as a second eye (docs/ai/specs/multiplayer.md section 7).

/**
 * A screen object, which acts as a container for turfs and other things
 * you want to show on the map, which you usually attach to "vis_contents".
 * Additionally manages the plane masters required to display said container contents
 */
// tg: code/_onclick/hud/screen_objects/map_view.dm:6
public partial class atom_movable_screen_map_view : atom_movable_screen
{
    // The New() that INITIALIZE_IMMEDIATE(/atom/movable/screen/map_view) defines (__DEFINES/subsystems.dm:96; inline, rules 11.1 (c), (d))
    // tg: code/_onclick/hud/screen_objects/map_view.dm:6
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        await base.New(Dm.forward(__extra, loc));
        if (!Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
        {
            float previous_initialized_value = SSatoms.initialized;
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
            DmList args = Dm.args(Dm.forward(__extra, loc));
            args[1] = TRUE;
            SSatoms.InitAtom(this, FALSE, args);
            SSatoms.initialized = previous_initialized_value;
        }
    }

    // tg: code/_onclick/hud/screen_objects/map_view.dm:19
    public override object Destroy(float? force = null)
    {
        foreach (datum_weakref client_ref in Dm.each<datum_weakref>(viewers_to_huds?.Copy()))
            hide_from_client(client_ref.resolve() as client);
        display_ref = null;

        return base.Destroy(force);
    }

    // tg: code/_onclick/hud/screen_objects/map_view.dm:26
    public virtual void generate_view(object map_key = null)
    {
        // Map keys have to start and end with an A-Z character,
        // and definitely NOT with a square bracket or even a number.
        // I wasted 6 hours on this. :agony:
        // -- Stylemistake
        assigned_map = map_key;
        set_position(1, 1);
    }

    // tg: code/_onclick/hud/screen_objects/map_view.dm:34
    public virtual void set_display(atom display_atom = null)
    {
        if (display_atom == null)
        {
            display_ref = null;
            // viewer.delete_parallax(assigned_map) for every viewer: the map's parallax (2D presentation)
            Dm.Deferred("code/_onclick/hud/screen_objects/map_view.dm:41", "set_display: delete_parallax of the map (2D presentation, not drawn by the 3D client)");
            return;
        }

        datum_weakref new_display_ref = WEAKREF(display_atom) as datum_weakref;
        if (new_display_ref == display_ref)
            return;
        display_ref = new_display_ref;
        // per viewer: create_parallax(assigned_map).set_perspective(display_atom) and the popup plane group's set_perspective
        Dm.Deferred("code/_onclick/hud/screen_objects/map_view.dm:50", "set_display: the map's parallax and popup planes' perspective (2D presentation, not drawn by the 3D client)");
    }

    /**
     * Generates and displays the map view to a client
     * Make sure you at least try to pass tgui_window if map view needed on UI,
     * so it will wait a signal from TGUI, which tells windows is fully visible.
     *
     * If you use map view not in TGUI, just call it as usualy.
     * If UI needs planes, call display_to_client.
     *
     * * show_to - Mob which needs map view
     * * window - Optional. TGUI window which needs map view
     */
    // tg: code/_onclick/hud/screen_objects/map_view.dm:79
    public virtual void display_to(mob show_to = null, datum_tgui_window window = null)
    {
        if (window != null && !Dm.truthy(window.visible))
            RegisterSignal(window, COMSIG_TGUI_WINDOW_VISIBLE, nameof(display_on_ui_visible));
        else
            display_to_client(show_to.client);
    }

    // tg: code/_onclick/hud/screen_objects/map_view.dm:85
    public virtual void display_on_ui_visible(datum_tgui_window window = null, client show_to = null)
    {
        display_to_client(show_to);
        UnregisterSignal(window, COMSIG_TGUI_WINDOW_VISIBLE);
    }

    // tg: code/_onclick/hud/screen_objects/map_view.dm:90
    public virtual object display_to_client(client show_to = null)
    {
        show_to.register_map_obj(this);
        // We need to add planesmasters to the popup, otherwise
        // blending fucks up massively. Any planesmaster on the main screen does
        // NOT apply to map popups. If there's ever a way to make planesmasters
        // omnipresent, then this wouldn't be needed.
        // We lazy load this because there's no point creating all these if none's gonna see em

        // Store this info in a client -> hud pattern, so ghosts closing the window nukes the right group
        datum_weakref client_ref = WEAKREF(show_to) as datum_weakref;
        viewers_to_huds ??= Dm.list();
        if (Dm.truthy(viewers_to_huds[client_ref]))
            return null;

        // the popup plane master group (new /datum/plane_master_group/popup), its perspective and the map's parallax: 2D presentation
        Dm.Deferred("code/_onclick/hud/screen_objects/map_view.dm:104", "display_to_client: the popup plane master group and the map's parallax (2D presentation, not drawn by the 3D client)");
        viewers_to_huds[client_ref] = WEAKREF(show_to.mob.hud_used);
        return null;
    }

    // tg: code/_onclick/hud/screen_objects/map_view.dm:117
    public virtual void hide_from(mob hide_from = null)
        => hide_from_client(hide_from?.canon_client);

    // tg: code/_onclick/hud/screen_objects/map_view.dm:120
    public virtual void hide_from_client(client hide_from = null)
    {
        if (hide_from == null)
            return;
        hide_from.clear_map(assigned_map);

        datum_weakref client_ref = WEAKREF(hide_from) as datum_weakref;
        // Make sure we clear the *right* hud
        viewers_to_huds?.Remove(client_ref);
        // hide_from.delete_parallax(assigned_map) and qdel of the popup plane group: 2D presentation (never made, display_to_client)
    }
}
