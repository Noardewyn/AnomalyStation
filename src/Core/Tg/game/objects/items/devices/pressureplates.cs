// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/pressureplates.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The pressure plate: an assembly attached to it (a signaler, a timer...) is activated a second after
// something steps on it ("You feel something click beneath you!"); hidden under a floor tile it arms itself (the undertile element's
// COMSIG_OBJ_HIDE; tg's "pp_overlay" tile picture is not drawn, DV529), Ctrl-click switches it, use in hand takes the assembly back.
// The puzzle plate is anchored, always on, and completes its puzzle link. The var blocks are generated (D111).

// tg: code/game/objects/items/devices/pressureplates.dm:1
public partial class obj_item_pressure_plate : obj_item
{
    // tg: code/game/objects/items/devices/pressureplates.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        tile_overlay = image(icon: Dm.resource("icons/turf/floors.dmi"), icon_state: "pp_overlay");
        if (Dm.truthy(roundstart_signaller))
        {
            obj_item_assembly_signaler signaller = Dm.@new<obj_item_assembly_signaler>(this);
            signaller.code = roundstart_signaller_code;
            signaller.set_frequency(roundstart_signaller_freq);
            assembly = signaller;
        }

        if (Dm.truthy(undertile_pressureplate))
            // DEVIATION(DV529): tg passes tile_overlay = tile_overlay too; an /image has no ref() in the port (object-model.md section 3,
            // an open item) and SSdcs keys a bespoke element by its arguments' refs, so the element is made without the tile's picture
            Waiting.NoSleep(AddElement(typeof(datum_element_undertile), Dm.named("use_anchor", TRUE)));
        RegisterSignal(this, COMSIG_OBJ_HIDE, nameof(ToggleActive));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        return dot;
    }

    // tg: code/game/objects/items/devices/pressureplates.dm:43  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_item_pressure_plate__Initialize__loc_connections;

    // tg: code/game/objects/items/devices/pressureplates.dm:48
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable AM = null)
    {
        if (!Dm.truthy(can_trigger) || !Dm.truthy(active))
            return;
        if (Dm.truthy(trigger_item) && !istype(AM, specific_item))
            return;
        if (Dm.truthy(trigger_mob) && isliving(AM))
        {
            mob_living L = (mob_living)AM;
            to_chat(L, span_warning(I18n.T("You feel something click beneath you!")));
        }
        else if (!Dm.truthy(trigger_item))
            return;
        can_trigger = FALSE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(trigger)), trigger_delay, file: "code/game/objects/items/devices/pressureplates.dm", line: 60);
    }

    // tg: code/game/objects/items/devices/pressureplates.dm:62
    public virtual async DmTask trigger()
    {
        can_trigger = TRUE;
        if (assembly is obj_item_assembly)
            await assembly.activate();
    }

    // tg: code/game/objects/items/devices/pressureplates.dm:67
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!isassembly(tool) || Dm.truthy(assembly) || !Dm.truthy(removable_assembly))
            return NONE;
        obj_item_assembly new_assembly = (obj_item_assembly)tool;
        if (!Dm.truthy(Dm.bitand(new_assembly.assembly_behavior, ASSEMBLY_FUNCTIONAL_OUTPUT)))
        {
            to_chat(user, span_warning(I18n.T("\\The [] doesn't seem like it would do much of anything inside of []...", tool, this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;
        assembly = (obj_item_assembly)tool;
        tool.SEND_SIGNAL(COMSIG_ASSEMBLY_ADDED_TO_PRESSURE_PLATE, Dm.list(tool, this, user));
        to_chat(user, span_notice(I18n.T("You attach [] to []!", tool, this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/pressureplates.dm:81
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        mob_living L = user as mob_living;
        if (Dm.truthy(removable_assembly) && assembly is obj_item_assembly)
        {
            to_chat(L, span_notice(I18n.T("You remove [] from [].", assembly, this)));
            assembly.SEND_SIGNAL(COMSIG_ASSEMBLY_REMOVED_FROM_PRESSURE_PLATE, Dm.list(assembly, this, L));
            if (!Dm.truthy(await L.put_in_hands(assembly)))
                await assembly.forceMove(get_turf(this));
            assembly = null;
        }
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/devices/pressureplates.dm:90
    public override async DmTask<object> item_ctrl_click(mob user = null)
    {
        if (Dm.truthy(@protected))
        {
            to_chat(user, span_warning(I18n.T("You can't quite seem to turn this pressure plate off...")));
            return CLICK_ACTION_BLOCKING;
        }
        active = Dm.truthy(active) ? FALSE : TRUE;
        if (Dm.truthy(active))
            to_chat(user, span_notice(I18n.T("You turn [] on.", this)));
        else
            to_chat(user, span_notice(I18n.T("You turn [] off.", this)));
        return CLICK_ACTION_SUCCESS;
    }

    ///Called from COMSIG_OBJ_HIDE to toggle the active part, because yeah im not making a special exception on the element to support it
    // tg: code/game/objects/items/devices/pressureplates.dm:102
    [SignalHandler]
    public virtual void ToggleActive(datum source = null, object underfloor_accessibility = null)
    {
        active = Dm.num(Dm.num(underfloor_accessibility) < UNDERFLOOR_VISIBLE);
    }
}

// tg: code/game/objects/items/devices/pressureplates.dm:107
public partial class obj_item_pressure_plate_puzzle : obj_item_pressure_plate
{
    // tg: code/game/objects/items/devices/pressureplates.dm:117
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (puzzle_id != null)
            SSqueuelinks.add_to_queue(this, puzzle_id, queue_size);
        return dot;
    }

    // tg: code/game/objects/items/devices/pressureplates.dm:122
    public override async DmTask trigger()
    {
        can_trigger = FALSE;
        SEND_SIGNAL(COMSIG_PUZZLE_COMPLETED, Dm.list(this));
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/devices/pressureplates.dm:43  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_item_pressure_plate__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_item_pressure_plate.on_entered)));
}
