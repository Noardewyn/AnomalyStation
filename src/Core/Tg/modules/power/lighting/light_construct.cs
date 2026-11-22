// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/lighting/light_construct.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 lighting slice L1 (D168): the whole file. The light fixture frame (the tube, the bulb and the floor bulb) is wired with a
// cable, closed with a screwdriver into a fixture (keeping its backup cell), unwired with a wirecutter and wrenched apart into
// sheets; a taken-apart fixture leaves the wired frame (light.cs). Deferred leaves: the telekinetic removal of the cell
// (genetics, live-partial) and the blob (antagonists, live-partial). The var block is hand-written for `cell` (below), as the
// fixture's is (light.cs): the constructor with tg's overrides (light_construct.dm:1-9) is here.

// tg: code/modules/power/lighting/light_construct.dm:1
public partial class obj_structure_light_construct : obj_structure
{
    ///Reference for the internal cell
    // tg: code/modules/power/lighting/light_construct.dm:20
    // The generated field would be a Type (rules 3.2 V3: some other type assigns a typepath to a var named `cell`), which a
    // power cell instance does not fit: the hand-written member keeps tg's declared type (see obj_machinery_light.cell).
    public obj_item_stock_parts_power_store cell;

    // tg: code/modules/power/lighting/light_construct.dm:1
    public obj_structure_light_construct()
    {
        name = "light fixture frame";
        desc = "A light fixture under construction.";
        icon = Dm.resource("icons/obj/lighting.dmi");
        icon_state = "tube-construct-stage1";
        anchored = TRUE;
        layer = WALL_OBJ_LAYER;
        max_integrity = 200;
        armor_type = typeof(datum_armor_structure_light_construct);
    }

    // tg: code/modules/power/lighting/light_construct.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload) && !Dm.truthy(Waiting.NoSleep(find_and_mount_on_atom(mark_for_late_init: TRUE))))
            return INITIALIZE_HINT_LATELOAD;
        return dot;
    }

    // tg: code/modules/power/lighting/light_construct.dm:36
    // set waitfor = FALSE (inherited from /atom/LateInitialize): the mount adds a component, so this body is the wrapper of
    // waiting-model.md 5.4.
    [ShouldNotOverride]
    public override void LateInitialize(params object[] __extra)
    {
        Waiting.WaitforFalse(LateInitialize__body(__extra));
    }

    private async DmTask LateInitialize__body(object[] __extra)
    {
        await find_and_mount_on_atom(late_init: TRUE);
    }

    // tg: code/modules/power/lighting/light_construct.dm:39
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref cell);
        return base.Destroy(force);
    }

    // tg: code/modules/power/lighting/light_construct.dm:43
    public override object get_turfs_to_mount_on()
    {
        return Dm.list(get_step(this, dir));
    }

    // tg: code/modules/power/lighting/light_construct.dm:46
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cell;
    }

    // tg: code/modules/power/lighting/light_construct.dm:49
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        float sw__1 = stage;
        if (sw__1 == LIGHT_CONSTRUCT_EMPTY)
            Dm.append(ref dot, span_notice(I18n.T("It's an empty frame with no wires.")));
        else if (sw__1 == LIGHT_CONSTRUCT_WIRED)
            Dm.append(ref dot, span_notice(I18n.T("It is wired, but the bolts are not screwed in.")));
        else if (sw__1 == LIGHT_CONSTRUCT_CLOSED)
            Dm.append(ref dot, span_notice(I18n.T("The casing is closed.")));
        if (Dm.truthy(cell_connectors))
        {
            if (cell != null)
                Dm.append(ref dot, span_notice(I18n.T("You see [] inside the casing.", cell)));
            else
                Dm.append(ref dot, span_notice(I18n.T("The casing has no power cell for backup power.")));
        }
        else
            Dm.append(ref dot, span_danger(I18n.T("This casing doesn't support power cells for backup power.")));
        return dot;
    }

    // tg: code/modules/power/lighting/light_construct.dm:66
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (cell == null)
            return null;
        user.visible_message(span_notice(I18n.T("[] removes [] from []!", user, cell, this)), span_notice(I18n.T("You remove [].", cell)));
        await user.put_in_hands(cell);
        cell = null;
        add_fingerprint(user);
        return null;
    }

    // tg: code/modules/power/lighting/light_construct.dm:74  (attack_tk: live-partial stub, genetics)

    // tg: code/modules/power/lighting/light_construct.dm:83
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);
        if (tool is obj_item_stock_parts_power_store_cell)
        {
            if (!Dm.truthy(cell_connectors))
            {
                to_chat(user, span_warning(I18n.T("This [] can't support a power cell!", name)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
            {
                to_chat(user, span_warning(I18n.T("[] is stuck to your hand!", tool)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (cell != null)
            {
                to_chat(user, span_warning(I18n.T("There is a power cell already installed!")));
                return ITEM_INTERACT_BLOCKING;
            }

            user.visible_message(span_notice(I18n.T("[] hooks up [] to [].", user, tool, this)),
                span_notice(I18n.T("You add [] to [].", tool, this)));
            playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE); // tg: code/modules/power/lighting/light_construct.dm:100
            await tool.forceMove(this);
            cell = tool as obj_item_stock_parts_power_store;
            add_fingerprint(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_light)
        {
            to_chat(user, span_warning(I18n.T("This [] isn't finished being setup!", name)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (stage == LIGHT_CONSTRUCT_EMPTY && tool is obj_item_stack_cable_coil coil)
        {
            if (!Dm.truthy(await coil.use(1)))
            {
                to_chat(user, span_warning(I18n.T("You need one length of cable to wire []!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            icon_state = Dm.text("[]-construct-stage2", fixture_type);
            stage = LIGHT_CONSTRUCT_WIRED;
            user.visible_message(span_notice(I18n.T("[] adds wires to [].", user.name, this)),
                span_notice(I18n.T("You add wires to [].", this)));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/power/lighting/light_construct.dm:123
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        float sw__1 = stage;
        if (sw__1 == LIGHT_CONSTRUCT_EMPTY)
        {
            if (cell != null)
            {
                to_chat(user, span_warning(I18n.T("You have to remove the cell first!")));
                return ITEM_INTERACT_BLOCKING;
            }
            to_chat(user, span_notice(I18n.T("You begin deconstructing []...", this)));
            if (!Dm.truthy(await tool.use_tool(this, user, 30, volume: 50)))
                return ITEM_INTERACT_BLOCKING;
            user.visible_message(span_notice(I18n.T("[] deconstructs [].", user.name, this)),
                span_notice(I18n.T("You deconstruct [].", this)),
                span_hear(I18n.T("You hear a ratchet.")));
            playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 75, TRUE); // tg: code/modules/power/lighting/light_construct.dm:135
            await deconstruct();
            return ITEM_INTERACT_SUCCESS;
        }
        else if (sw__1 == LIGHT_CONSTRUCT_WIRED)
        {
            to_chat(usr, span_warning(I18n.T("You have to remove the wires first!")));
            return ITEM_INTERACT_BLOCKING;
        }
        return NONE;
    }

    // tg: code/modules/power/lighting/light_construct.dm:143
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (stage != LIGHT_CONSTRUCT_WIRED)
            return NONE;
        user.visible_message(span_notice(I18n.T("[] closes []'s casing.", user.name, this)),
            span_notice(I18n.T("You close []'s casing.", this)),
            span_hear(I18n.T("You hear screwing.")));
        tool.play_tool_sound(this, 75);
        string sw__1 = fixture_type;
        if (sw__1 == "tube")
            new_light = Dm.@new<obj_machinery_light_empty>(loc);
        else if (sw__1 == "bulb")
            new_light = Dm.@new<obj_machinery_light_small_empty>(loc);
        else if (sw__1 == "floor")
            new_light = Dm.@new<obj_machinery_light_floor_empty>(loc);
        await new_light.setDir(dir);
        await new_light.find_and_mount_on_atom();
        transfer_fingerprints_to(new_light);
        if (!QDELETED(cell))
        {
            new_light.cell = cell;
            await cell.forceMove(new_light);
            cell = null;
        }
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/lighting/light_construct.dm:167
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (stage != LIGHT_CONSTRUCT_WIRED)
            return NONE;
        stage = LIGHT_CONSTRUCT_EMPTY;
        icon_state = Dm.text("[]-construct-stage1", fixture_type);
        Dm.@new<obj_item_stack_cable_coil>(drop_location(), 1, TRUE); // tg: 1, "red": the stale colour lands in `merge` (cable.dm:515), truthy
        user.visible_message(span_notice(I18n.T("[] removes the wiring from [].", user.name, this)),
            span_notice(I18n.T("You remove the wiring from [].", this)),
            span_hear(I18n.T("You hear clicking.")));
        tool.play_tool_sound(this, 100);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/lighting/light_construct.dm:179  (blob_act: live-partial stub, antagonists)

    // tg: code/modules/power/lighting/light_construct.dm:183
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_iron>(loc, sheets_refunded);
        if (stage == LIGHT_CONSTRUCT_WIRED)
            Dm.@new<obj_item_stack_cable_coil>(drop_location(), 1, TRUE); // tg: 1, "red": the stale colour lands in `merge` (cable.dm:515), truthy
    }

    // tg: code/modules/power/lighting/light_construct.dm:179
    public override async DmTask<object> blob_act(obj_structure_blob attacking_blob = null)
    {
        if (attacking_blob != null && attacking_blob.loc == loc)
            await deconstruct(FALSE);
        return null;
    }

    // tg: code/modules/power/lighting/light_construct.dm:74
    public override async DmTask<object> attack_tk(mob user = null)
    {
        if (cell == null)
            return null;
        to_chat(user, span_notice(I18n.T("You telekinetically remove [].", cell)));
        obj_item_stock_parts_power_store cell_reference = cell as obj_item_stock_parts_power_store;
        cell = null;
        await cell_reference.forceMove(drop_location() as atom);
        return await cell_reference.attack_tk(user);
    }
}

// tg: code/modules/power/lighting/light_construct.dm:194
public partial class obj_structure_light_construct_floor : obj_structure_light_construct
{
    // tg: code/modules/power/lighting/light_construct.dm:200
    public override object get_turfs_to_mount_on()
    {
        return Dm.list(get_turf(this));
    }

    // tg: code/modules/power/lighting/light_construct.dm:203
    public override object is_mountable_turf(turf target = null)
    {
        return Dm.num(!isgroundlessturf(target));
    }
}
