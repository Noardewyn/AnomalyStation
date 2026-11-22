// Translated from tgstation (snapshot 1d99bd7f): code/modules/pai/door_jack.dm and camera.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6 (D326): the whole files.

public partial class mob_living_silicon_pai : mob_living_silicon
{
    // tg: code/modules/pai/door_jack.dm:1
    private const int CABLE_LENGTH = 2;

    /**
     * Switch that handles door jack operations.
     *
     * @param {string} mode - The requested operation of the door jack.
     *
     * @returns {boolean} - TRUE if the door jack state was switched, FALSE otherwise.
     */
    // tg: code/modules/pai/door_jack.dm:10
    public virtual async DmTask<object> door_jack(object mode = null)
    {
        if (mode == null)
            return FALSE;
        switch ((int)Dm.num(mode))
        {
            case PAI_DOOR_JACK_CABLE:
                await extend_cable();
                return TRUE;
            case PAI_DOOR_JACK_HACK:
                await hack_door();
                return TRUE;
            case PAI_DOOR_JACK_CANCEL:
                QDEL_NULL(ref hacking_cable);
                visible_message(span_notice(I18n.T("The cable retracts into the pAI.")));
                return TRUE;
        }
        return FALSE;
    }

    /**
     * #Extend cable supporting proc
     *
     * When doorjack is installed, allows the pAI to drop
     * a cable which is placed either on the floor or in
     * someone's hands based (on distance).
     *
     * @returns {boolean} - TRUE if the cable was dropped, FALSE otherwise.
     */
    // tg: code/modules/pai/door_jack.dm:35
    public virtual async DmTask<object> extend_cable()
    {
        QDEL_NULL(ref hacking_cable); //clear any old cables
        hacking_cable = Dm.@new<obj_item_pai_cable>();
        RegisterSignal(hacking_cable, COMSIG_QDELETING, nameof(on_hacking_cable_del));
        mob_living_carbon hacker = get_holder();
        if (hacker != null && Dm.truthy(await hacker.put_in_hands(hacking_cable))) //important to double check since get_holder can return non-null values that aren't carbons.
        {
            hacker.visible_message(span_notice(I18n.T("A port on [] opens to reveal a cable, which [] quickly grabs.", this, hacker)), span_notice(I18n.T("A port on [] opens to reveal a cable, which you quickly grab.", this)), span_hear(I18n.T("You hear the soft click of a plastic component and manage to catch the falling cable.")));
            track_pai();
            track_thing(hacking_cable);
            return TRUE;
        }
        await hacking_cable.forceMove(drop_location() as atom);
        hacking_cable.visible_message(message: span_notice(I18n.T("A port on [] opens to reveal a cable, which promptly falls to the floor.", this)), blind_message: span_hear(I18n.T("You hear the soft click of a plastic component fall to the ground.")));
        track_pai();
        track_thing(hacking_cable);
        return TRUE;
    }

    /** Tracks the associated pai */
    // tg: code/modules/pai/door_jack.dm:52
    public virtual void track_pai()
    {
        RegisterSignal(this, COMSIG_MOVABLE_MOVED, nameof(handle_move));
        RegisterSignal(card, COMSIG_MOVABLE_MOVED, nameof(handle_move));
    }

    /** Untracks the associated pai */
    // tg: code/modules/pai/door_jack.dm:57
    public virtual void untrack_pai()
    {
        UnregisterSignal(this, COMSIG_MOVABLE_MOVED);
        UnregisterSignal(card, COMSIG_MOVABLE_MOVED);
    }

    /** Tracks the associated hacking_cable */
    // tg: code/modules/pai/door_jack.dm:62
    public virtual void track_thing(atom_movable thing = null)
    {
        RegisterSignal(thing, COMSIG_MOVABLE_MOVED, nameof(handle_move));
        DmList locations = get_nested_locs(thing, include_turf: FALSE) as DmList;
        foreach (atom_movable location in Dm.each<atom_movable>(locations))
            RegisterSignal(location, COMSIG_MOVABLE_MOVED, nameof(handle_move));
    }

    /** Untracks the associated hacking */
    // tg: code/modules/pai/door_jack.dm:69
    public virtual void untrack_thing(atom_movable thing = null)
    {
        UnregisterSignal(thing, COMSIG_MOVABLE_MOVED);
        DmList locations = get_nested_locs(thing, include_turf: FALSE) as DmList;
        foreach (atom_movable location in Dm.each<atom_movable>(locations))
            UnregisterSignal(location, COMSIG_MOVABLE_MOVED);
    }

    /**
     * A periodic check to see if the source pAI is nearby.
     * Deletes the extended cable if the source pAI is not nearby.
     */
    // tg: code/modules/pai/door_jack.dm:79
    public virtual void handle_move(atom_movable source = null, atom old_loc = null)
    {
        if (ismovable(old_loc))
            untrack_thing((atom_movable)old_loc);
        if (hacking_cable != null && !IN_GIVEN_RANGE(this, hacking_cable, CABLE_LENGTH))
        {
            retract_cable();
            return;
        }
        if (ismovable(source.loc))
            track_thing((atom_movable)source.loc);
    }

    /**
     * Handles deleting the hacking cable and notifying the user.
     */
    // tg: code/modules/pai/door_jack.dm:91
    public virtual object retract_cable()
    {
        balloon_alert(this, "cable retracted");
        QDEL_NULL(ref hacking_cable);
        return TRUE;
    }

    /**
     * #Door jacking supporting proc
     *
     * After a 15 second timer, the door will crack open,
     * provided they don't move out of the way.
     *
     * @returns {boolean} - TRUE if the door was jacked, FALSE otherwise.
     */
    // tg: code/modules/pai/door_jack.dm:104
    public virtual async DmTask<object> hack_door()
    {
        if (hacking_cable == null)
            return FALSE;
        if (hacking_cable.hacking_machine == null)
        {
            balloon_alert(this, "nothing connected");
            return FALSE;
        }
        playsound(this, Dm.resource("sound/machines/airlock/airlock_alien_prying.ogg"), 50, TRUE);
        balloon_alert(this, "overriding...");
        // Now begin hacking
        if (!Dm.truthy(await do_after(this, 15 * SECONDS, hacking_cable.hacking_machine, timed_action_flags: NONE)))
        {
            balloon_alert(this, "failed! retracting...");
            QDEL_NULL(ref hacking_cable);
            return FALSE;
        }
        if (hacking_cable?.hacking_machine == null)
            return FALSE;
        var door = (obj_machinery_door)hacking_cable.hacking_machine;
        balloon_alert(this, "success");
        await door.open();
        QDEL_NULL(ref hacking_cable);
        return TRUE;
    }

    /**
     * All inclusive camera proc. Zooms, snaps, prints.
     *
     * @param {mob} user - The pAI requesting the camera.
     *
     * @param {string} mode - The camera option to toggle.
     *
     * @returns {boolean} - TRUE if the camera worked.
     */
    // tg: code/modules/pai/camera.dm:39
    public virtual async DmTask<object> use_camera(mob user = null, object mode = null)
    {
        if (aicamera == null || mode == null)
            return FALSE;
        switch ((int)Dm.num(mode))
        {
            case PAI_PHOTO_MODE_CAMERA:
                aicamera.toggle_camera_mode(user);
                break;
            case PAI_PHOTO_MODE_PRINTER:
                var paicam = (obj_item_camera_siliconcam_pai_camera)aicamera;
                await paicam.pai_print(user);
                break;
            case PAI_PHOTO_MODE_ZOOM:
                await aicamera.adjust_zoom(user);
                break;
        }
        return TRUE;
    }
}

// tg: code/modules/pai/camera.dm:1
public partial class obj_item_camera_siliconcam_pai_camera : obj_item_camera_siliconcam
{
    // tg: code/modules/pai/camera.dm:5
    public override async DmTask after_picture(mob user = null, datum_picture picture = null)
    {
        object number = Dm.length(stored);
        picture.picture_name = I18n.T("Image [] (taken by [])", number, loc.name);
        stored[picture] = TRUE;
        playsound(this, SFX_POLAROID, 75, TRUE, -3);
        balloon_alert(user, "image recorded");
        await DmTask.CompletedTask;
    }

    /**
     * Handles selecting and printing stored images.
     *
     * @param {mob} user - The pAI.
     *
     * @returns {boolean} - TRUE if the pAI prints an image,
     * 	FALSE otherwise.
     */
    // tg: code/modules/pai/camera.dm:20
    public virtual async DmTask<object> pai_print(mob user = null)
    {
        var pai = (mob_living_silicon_pai)loc;
        var selection = await selectpicture(user) as datum_picture;
        if (selection == null)
        {
            balloon_alert(user, "invalid image");
            return FALSE;
        }
        await printpicture(user, selection);
        user.visible_message(span_notice(I18n.T("A picture appears on top of the chassis of []!", pai.name)), span_notice(I18n.T("You print a photograph.")));
        return TRUE;
    }
}
