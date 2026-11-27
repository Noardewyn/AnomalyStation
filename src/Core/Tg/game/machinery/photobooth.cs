// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/photobooth.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The screentip context (add_context) is a live-partial stub, as every machine's (D264).

/**
 * Photobooth
 * A machine used to change occupant's security record photos, working similarly to a
 * camera, but doesn't give any physical photo to the user.
 * Links to buttons for remote control.
 */
// tg: code/game/machinery/photobooth.dm:7
public partial class obj_machinery_photobooth : obj_machinery
{
    // tg: code/game/machinery/photobooth.dm:43
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/machinery/photobooth.dm:45", "Initialize: register_context(): screentip contexts (M10+)");
        return dot;
    }

    // tg: code/game/machinery/photobooth.dm:47
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (Dm.truthy(taking_pictures))
        {
            balloon_alert(user, I18n.T("machine busy!"));
            return dot;
        }
        if (Dm.truthy(state_open))
            await close_machine();
        else
            await open_machine();
        return dot;
    }

    // tg: code/game/machinery/photobooth.dm:56
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(taking_pictures))
        {
            balloon_alert(user, I18n.T("machine busy!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        if (occupant != null)
        {
            if (Dm.truthy(await allowed__proc(user)))
                Waiting.Detach(start_taking_pictures());
            else
                balloon_alert(user, I18n.T("access denied!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        return await base.attack_hand_secondary(user, modifiers);
    }

    // tg: code/game/machinery/photobooth.dm:68  add_context: live-partial (the screentips, D264).

    // tg: code/game/machinery/photobooth.dm:78
    public override async DmTask<object> close_machine(atom_movable target = null, float? density_to_set = null)
    {
        // tg's close_machine(mob/user, density_to_set): the first argument is the user to tell, as the base's is the one to shut in
        mob user = target as mob;
        if (Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("close panel first!"));
            return null;
        }
        playsound(this, Dm.resource("sound/effects/curtain.ogg"), 50, TRUE);
        return await base.close_machine(target, density_to_set ?? TRUE);
    }

    // tg: code/game/machinery/photobooth.dm:85
    public override async DmTask<object> open_machine(float? drop = null, float? density_to_set = null)
    {
        playsound(this, Dm.resource("sound/effects/curtain.ogg"), 50, TRUE);
        return await base.open_machine(drop ?? TRUE, density_to_set ?? FALSE);
    }

    // tg: code/game/machinery/photobooth.dm:89
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
            icon_state = Dm.text("[]_off", base_icon_state);
        else if (Dm.truthy(state_open))
            icon_state = Dm.text("[]_open", base_icon_state);
        else
            icon_state = Dm.text("[]_closed", base_icon_state);
        return dot;
    }

    // tg: code/game/machinery/photobooth.dm:98
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.truthy(Dm.bitand(machine_stat, MAINT)) || Dm.truthy(panel_open))
            Dm.append(ref dot, Dm.text("[]_panel", base_icon_state));
        return dot;
    }

    // tg: code/game/machinery/photobooth.dm:103
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return Dm.truthy(has_buckled_mobs()) ? NONE : default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/photobooth.dm:106
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/photobooth.dm:109
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        req_access = Dm.list(); //in case someone sets this to something
        req_one_access = Dm.list();
        balloon_alert(user, I18n.T("beeps softly"));
        Dm.combine(ref obj_flags, EMAGGED);
        return TRUE;
    }

    /**
     * Handles the effects of taking pictures of the user, calling finish_taking_pictures
     * to actually update the records.
     */
    // tg: code/game/machinery/photobooth.dm:122
    public virtual async DmTask start_taking_pictures()
    {
        taking_pictures = TRUE;
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            mob_living_carbon carbon_occupant = occupant as mob_living_carbon;
            for (float i = 1; i <= 5; i++) //play a ton of sounds to mimic it blinding you
            {
                playsound(this, SFX_POLAROID, 75, TRUE);
                if (carbon_occupant != null)
                    await carbon_occupant.flash_act(5);
                await sleep(0.2f * SECONDS);
            }
            if (carbon_occupant != null)
                await carbon_occupant.emote("scream");
            finish_taking_pictures();
            return;
        }
        if (!Dm.truthy(await do_after(occupant, 2 * SECONDS, this, timed_action_flags: IGNORE_HELD_ITEM))) //gives them time to put their hand items away.
        {
            taking_pictures = FALSE;
            return;
        }
        playsound(this, Dm.resource("sound/items/polaroid/polaroid1.ogg"), 75, TRUE);
        flash();
        if (!Dm.truthy(await do_after(occupant, 3 * SECONDS, this, timed_action_flags: IGNORE_HELD_ITEM)))
        {
            taking_pictures = FALSE;
            return;
        }
        playsound(this, Dm.resource("sound/items/polaroid/polaroid2.ogg"), 75, TRUE);
        flash();
        if (!Dm.truthy(await do_after(occupant, 2 * SECONDS, this, timed_action_flags: IGNORE_HELD_ITEM)))
        {
            taking_pictures = FALSE;
            return;
        }
        finish_taking_pictures();
    }

    ///Updates the records (if possible), giving feedback, and spitting the user out if all's well.
    // tg: code/game/machinery/photobooth.dm:154
    public virtual void finish_taking_pictures()
    {
        taking_pictures = FALSE;
        if (!Dm.truthy(GLOB.manifest.change_pictures(occupant.name, occupant as mob_living, add_height_chart: add_height_chart)))
        {
            balloon_alert(occupant as mob, I18n.T("record not found!"));
            return;
        }
        balloon_alert(occupant as mob, I18n.T("records updated"));
        Waiting.NoSleep(open_machine());
    }

    ///Mimicing the camera, gives a flash effect by turning the light on and calling flash_end.
    // tg: code/game/machinery/photobooth.dm:163
    public virtual void flash()
    {
        Waiting.NoSleep(set_light_on(TRUE));
        _addtimer(Dm.@new<datum_callback>(this, nameof(flash_end)), FLASH_LIGHT_DURATION, Dm.bitor(TIMER_OVERRIDE, TIMER_UNIQUE), file: "code/game/machinery/photobooth.dm", line: 165);
    }

    ///Called by a timer to turn the light off to end the flash effect.
    // tg: code/game/machinery/photobooth.dm:168
    public virtual void flash_end()
    {
        Waiting.NoSleep(set_light_on(FALSE));
    }
}

// tg: code/game/machinery/photobooth.dm:172
public partial class obj_machinery_button_photobooth : obj_machinery_button
{
    // tg: code/game/machinery/photobooth.dm:179
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (device_instance != null) // tg's `device` (buttons.cs: device_instance)
        {
            obj_item_assembly_control_photobooth_control ours = device_instance as obj_item_assembly_control_photobooth_control;
            ours.id = id;
        }
        return dot;
    }

    // tg: code/game/machinery/photobooth.dm:185
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        obj_item_multitool multitool = tool as obj_item_multitool;
        if (multitool.buffer != null && !istype(multitool.buffer, typeof(obj_machinery_photobooth)))
            return dot;
        obj_item_assembly_control_photobooth_control controller = device_instance as obj_item_assembly_control_photobooth_control;
        controller.booth_machine_ref = WEAKREF(multitool.buffer) as datum_weakref;
        id = null;
        controller.id = null;
        balloon_alert(user, I18n.T("linked to []", multitool.buffer));
        return dot;
    }
}

// tg: code/game/machinery/photobooth.dm:194
public partial class obj_item_assembly_control_photobooth_control : obj_item_assembly_control
{
    // tg: code/game/machinery/photobooth.dm:199
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/machinery/photobooth.dm:203
    public override void LateInitialize(params object[] __extra)
    {
        find_machine();
    }

    /// Locate the photobooth we're linked via ID
    // tg: code/game/machinery/photobooth.dm:207
    public virtual object find_machine()
    {
        foreach (obj_machinery_photobooth booth in Dm.each<obj_machinery_photobooth>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_photobooth))))
        {
            if (Dm.eq(booth.button_id, id))
                booth_machine_ref = WEAKREF(booth) as datum_weakref;
        }
        if (booth_machine_ref != null)
            return TRUE;
        return FALSE;
    }

    // tg: code/game/machinery/photobooth.dm:215
    public override async DmTask<object> activate(mob activator = null)
    {
        if (booth_machine_ref == null)
            return null;
        obj_machinery_photobooth machine = booth_machine_ref.resolve() as obj_machinery_photobooth;
        if (machine == null)
            return null;
        if (Dm.truthy(machine.taking_pictures))
        {
            balloon_alert(activator, I18n.T("machine busy!"));
            return null;
        }
        await machine.start_taking_pictures();
        return null;
    }
}
