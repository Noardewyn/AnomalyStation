// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/embedded_controller/access_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the engineering package E8, D287): the access buttons and access consoles of two-door airlocks (Meta's virology
// and xenobiology). A button, for those with its access, opens its bolted door when both are shut, or closes and bolts the other
// door and opens its own two seconds later; the console does the same for either door through the AirlockButtonController window.
// The base type's and the window's var blocks are generated; the button's controller and the console's two airlocks are
// hand-written (types-converter.md 3.2, "Hand": the generated `controller` is a Type by V3, `interior_airlock` an object), so
// their constructors with tg's overrides are here too.

// tg: code/game/machinery/embedded_controller/access_controller.dm:1
public partial class obj_machinery_door_buttons : obj_machinery
{
    // tg: code/game/machinery/embedded_controller/access_controller.dm:10
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        return await attack_hand(user);
    }

    // tg: code/game/machinery/embedded_controller/access_controller.dm:13
    public virtual void find_objects_by_tag()
    {
        return;
    }

    // tg: code/game/machinery/embedded_controller/access_controller.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/machinery/embedded_controller/access_controller.dm:20
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        find_objects_by_tag();
    }

    // tg: code/game/machinery/embedded_controller/access_controller.dm:24
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        req_access = Dm.list();
        req_one_access = Dm.list();
        playsound(this, SFX_SPARKS, 100, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        balloon_alert(user, I18n.T("access controller shorted"));
        return TRUE;
    }
}

// tg: code/game/machinery/embedded_controller/access_controller.dm:34
public partial class obj_machinery_door_buttons_access_button : obj_machinery_door_buttons
{
    // tg: code/game/machinery/embedded_controller/access_controller.dm:42
    public obj_machinery_door_buttons_airlock_controller controller;

    // tg: code/game/machinery/embedded_controller/access_controller.dm:34
    public obj_machinery_door_buttons_access_button()
    {
        icon = Dm.resource("icons/obj/machines/wallmounts.dmi");
        icon_state = "access_button_standby";
        base_icon_state = "access_button";
        name = "access button";
        desc = "A button used for the explicit purpose of opening an airlock.";
    }

    // tg: code/game/machinery/embedded_controller/access_controller.dm:45
    public override void find_objects_by_tag()
    {
        foreach (obj_machinery_door_buttons_airlock_controller A in Dm.each_cast<obj_machinery_door_buttons_airlock_controller>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_buttons_airlock_controller))))
        {
            if (Dm.eq(A.idSelf, idSelf))
            {
                controller = A;
                RegisterSignal(controller, COMSIG_PREQDELETED, nameof(remove_object));
                break;
            }
        }
        foreach (obj_machinery_door_airlock I in Dm.each_cast<obj_machinery_door_airlock>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_airlock))))
        {
            if (Dm.eq(I.id_tag, idDoor))
            {
                door = I;
                RegisterSignal(door, COMSIG_PREQDELETED, nameof(remove_object));
                break;
            }
        }
    }

    // tg: code/game/machinery/embedded_controller/access_controller.dm:57
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(busy))
            return null;
        if (!Dm.truthy(await allowed__proc(user)))
        {
            to_chat(user, span_warning(I18n.T("Access denied.")));
            return null;
        }
        if (controller != null && !Dm.truthy(controller.busy) && door != null)
        {
            if (Dm.truthy(Dm.bitand(controller.machine_stat, NOPOWER)))
                return null;
            busy = TRUE;
            update_appearance();
            if (Dm.truthy(door.density))
            {
                if (controller.exterior_airlock == null || controller.interior_airlock == null)
                    await controller.only_open(door);
                else
                {
                    if (Dm.truthy(controller.exterior_airlock.density) && Dm.truthy(controller.interior_airlock.density))
                        await controller.only_open(door);
                    else
                        await controller.cycle_close(door);
                }
            }
            else
                await controller.only_close(door);
            await use_energy(active_power_usage);
            _addtimer(Dm.@new<datum_callback>(this, nameof(not_busy)), 2 * SECONDS, file: "code/game/machinery/embedded_controller/access_controller.dm", line: 79);
        }
        return null;
    }

    // tg: code/game/machinery/embedded_controller/access_controller.dm:81
    public virtual void not_busy()
    {
        busy = FALSE;
        update_appearance();
    }

    // tg: code/game/machinery/embedded_controller/access_controller.dm:85
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            icon_state = Dm.text("[]_off", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.text("[]_[]", base_icon_state, Dm.truthy(busy) ? "cycle" : "standby");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/embedded_controller/access_controller.dm:92
    public virtual void remove_object(datum source = null)
    {
        // SIGNAL_HANDLER

        if (source == door)
        {
            door = null;
            return;
        }
        if (source == controller)
            controller = null;
    }

    // tg: code/game/machinery/embedded_controller/access_controller.dm:101
    public override object Destroy(float? force = null)
    {
        door = null;
        controller = null;
        return base.Destroy(force);
    }
}

// tg: code/game/machinery/embedded_controller/access_controller.dm:106
public partial class obj_machinery_door_buttons_airlock_controller : obj_machinery_door_buttons
{
    ///our interior airlock
    // tg: code/game/machinery/embedded_controller/access_controller.dm:121
    public obj_machinery_door_airlock interior_airlock;
    ///our exterior airlock
    // tg: code/game/machinery/embedded_controller/access_controller.dm:123
    public obj_machinery_door_airlock exterior_airlock;

    // tg: code/game/machinery/embedded_controller/access_controller.dm:106
    public obj_machinery_door_buttons_airlock_controller()
    {
        name = "access console";
        desc = "A small console that can cycle opening between two airlocks.";
        icon = Dm.resource("icons/obj/machines/wallmounts.dmi");
        icon_state = "access_control_standby";
        base_icon_state = "access_control";
        interaction_flags_machine = INTERACT_MACHINE_WIRES_IF_OPEN | INTERACT_MACHINE_ALLOW_SILICON | INTERACT_MACHINE_OPEN_SILICON;
        mouse_over_pointer = MOUSE_HAND_POINTER;
    }

    ///set our doors to null upon deletion
    // tg: code/game/machinery/embedded_controller/access_controller.dm:126
    public virtual void remove_door(datum source = null)
    {
        // SIGNAL_HANDLER

        if (source == interior_airlock)
        {
            interior_airlock = null;
            return;
        }

        if (source == exterior_airlock)
            exterior_airlock = null;
    }

    ///proc called when we want to open doors without any cycling involved
    // tg: code/game/machinery/embedded_controller/access_controller.dm:137
    public virtual async DmTask only_open(obj_machinery_door_airlock target_door = null)
    {
        if (target_door == null)
            return;
        busy = TRUE;
        update_appearance();
        await open_door(target_door);
    }

    ///proc called when we want to close doors without any cycling involved
    // tg: code/game/machinery/embedded_controller/access_controller.dm:145
    public virtual async DmTask only_close(obj_machinery_door_airlock target_door = null)
    {
        if (target_door == null)
            return;
        busy = TRUE;
        await close_door(target_door);
    }

    ///proc that handles closing doors
    // tg: code/game/machinery/embedded_controller/access_controller.dm:152
    public virtual async DmTask<object> close_door(obj_machinery_door_airlock target_door = null, float? turn_idle_on_terminate = null)
    {
        turn_idle_on_terminate ??= TRUE;
        busy = TRUE;
        if (target_door == null || Dm.truthy(target_door.density))
        {
            go_idle();
            return FALSE;
        }
        update_appearance();
        target_door.safe = FALSE; //Door crushies, manual door after all. Set every time in case someone changed it, safe doors can end up waiting forever.
        target_door.unbolt();
        if (!Dm.truthy(await target_door.close()) || Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            go_idle();
            return FALSE;
        }
        target_door?.bolt();

        if (Dm.truthy(turn_idle_on_terminate))
            go_idle();

        return TRUE;
    }

    ///proc called when we want to close doors with cycling
    // tg: code/game/machinery/embedded_controller/access_controller.dm:171
    public virtual async DmTask<object> cycle_close(obj_machinery_door_airlock target_door = null)
    {
        if (exterior_airlock == null || interior_airlock == null)
            return null;
        if (Dm.eq(exterior_airlock.density, interior_airlock.density) || !Dm.truthy(target_door.density))
            return null;
        busy = TRUE;
        update_appearance();
        obj_machinery_door_airlock opposite_airlock = (target_door == exterior_airlock ? interior_airlock : exterior_airlock);

        if (!Dm.truthy(await close_door(opposite_airlock, turn_idle_on_terminate: FALSE)))
        {
            go_idle();
            return null;
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(cycle_open), target_door), 2 * SECONDS, file: "code/game/machinery/embedded_controller/access_controller.dm", line: 184);
        return null;
    }

    ///proc called when we want to open doors with cycling
    // tg: code/game/machinery/embedded_controller/access_controller.dm:186
    public virtual async DmTask<object> cycle_open(obj_machinery_door_airlock target_door = null)
    {
        if (target_door == null)
        {
            go_idle();
            return null;
        }
        obj_machinery_door_airlock opposite_airlock = (target_door == exterior_airlock ? interior_airlock : exterior_airlock);
        if (opposite_airlock == null || !Dm.truthy(opposite_airlock.density) || !Dm.truthy(opposite_airlock.locked))
        {
            go_idle();
            return null;
        }
        busy = TRUE;
        await open_door(target_door);
        return null;
    }

    ///proc that handles opening and unbolting the door
    // tg: code/game/machinery/embedded_controller/access_controller.dm:196
    public virtual async DmTask<object> open_door(obj_machinery_door_airlock target_door = null)
    {
        if (!Dm.truthy(target_door.density))
        {
            go_idle();
            return null;
        }

        target_door.unbolt();
        if (!Dm.truthy(await target_door.open()) || Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            go_idle();
            return null;
        }

        target_door?.bolt();
        go_idle();
        return null;
    }

    ///unsets our busy state and update our appearance
    // tg: code/game/machinery/embedded_controller/access_controller.dm:209
    public virtual void go_idle()
    {
        busy = FALSE;
        update_appearance();
    }

    // tg: code/game/machinery/embedded_controller/access_controller.dm:213
    public override void find_objects_by_tag()
    {
        foreach (obj_machinery_door door in Dm.each_cast<obj_machinery_door>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door))))
        {
            if (idInterior == null || idExterior == null)
                break;
            // the airlock vars hold what tg's do on every map: tg declares them airlocks; a windoor with the tag stays unlinked here
            if (Dm.eq(door.id_tag, idInterior))
            {
                interior_airlock = door as obj_machinery_door_airlock;
                RegisterSignal(interior_airlock, COMSIG_PREQDELETED, nameof(remove_door));
            }
            if (Dm.eq(door.id_tag, idExterior))
            {
                exterior_airlock = door as obj_machinery_door_airlock;
                RegisterSignal(exterior_airlock, COMSIG_PREQDELETED, nameof(remove_door));
            }
        }
    }

    // tg: code/game/machinery/embedded_controller/access_controller.dm:224
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            icon_state = Dm.text("[]_off", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.text("[]_[]", base_icon_state, Dm.truthy(busy) ? "process" : "standby");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/embedded_controller/access_controller.dm:231
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AirlockButtonController", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/embedded_controller/access_controller.dm:238
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["interior_door"] = interior_airlock != null ? REF(interior_airlock) : null;
        data["exterior_door"] = exterior_airlock != null ? REF(exterior_airlock) : null;
        data["busy"] = busy;
        data["interior_door_closed"] = interior_airlock?.density;
        data["exterior_door_closed"] = exterior_airlock?.density;
        return data;
    }

    // tg: code/game/machinery/embedded_controller/access_controller.dm:247
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot) || Dm.truthy(busy))
            return TRUE;

        if (@params["requested_door"] == null)
            return TRUE;

        // var/atom/requested_door, opposite_door: they only ever hold the two airlocks
        obj_machinery_door_airlock requested_door;
        obj_machinery_door_airlock opposite_door;

        if (Dm.eq(REF(interior_airlock), @params["requested_door"]))
        {
            requested_door = interior_airlock;
            opposite_door = exterior_airlock;
        }
        else
        {
            requested_door = exterior_airlock;
            opposite_door = interior_airlock;
        }

        if (Dm.eq(action, "open"))
        {
            if (opposite_door != null && !Dm.truthy(opposite_door.density))
                await cycle_close(requested_door);
            else
                await only_open(requested_door);
        }
        else if (Dm.eq(action, "close"))
            await only_close(requested_door);

        return TRUE;
    }
}
