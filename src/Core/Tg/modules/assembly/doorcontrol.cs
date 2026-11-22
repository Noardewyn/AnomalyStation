// Translated from tgstation (snapshot 1d99bd7f): code/modules/assembly/doorcontrol.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4a P4b): the controller assembly (its init, examine, screentips and the ID copy), the blast door controller
// (every pod door with its id opens or closes) and the airlock controller (open, ID scan, bolts, shock and safety by its
// specialfunctions) are translated: what a Meta door button runs. M5 slice 10: the multitool's ID setting (the controller's
// number input; the blast door controller's list of pod door ids); the mass driver and flasher
// controllers are after the core version (their activate rows there, M10+). Engineering E8 (D287): the igniter controller. Gaps MD2: the
// crematorium controller. Gaps ST2: the curtain controller (the theatre's mechanical curtains).
//
// GAP(lead): VARSET_CALLBACK (__HELPERS/varset_callback.dm:3) needs the global proc ___callbackvarset, which is not
// declared; the controllers' cooldown timers call cooldown__varset below instead, which does for a datum what
// ___callbackvarset does (datum.vars[var_name] = var_value).

// tg: code/modules/assembly/doorcontrol.dm:1
public partial class obj_item_assembly_control : obj_item_assembly
{
    // tg: code/modules/assembly/doorcontrol.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/modules/assembly/doorcontrol.dm:22
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;
        if (istype(held_item, type) && Dm.truthy(copyable))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Copy ID";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/modules/assembly/doorcontrol.dm:28
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (Dm.truthy(generically_adjustable))
            Dm.append(ref dot, span_notice(I18n.T("You can use a [] to adjust its ID.", EXAMINE_HINT(I18n.T("multitool")))));

        if (Dm.truthy(copyable))
            Dm.append(ref dot, span_notice(I18n.T("You can interact with another controller to copy its ID.")));

        if (!Dm.truthy(id))
            return dot;

        if (!Dm.eq(id, -1))
            Dm.append(ref dot, span_notice(I18n.T("Its channel ID is '[]'.", id)));
        return dot;
    }

    // tg: code/modules/assembly/doorcontrol.dm:42
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(generically_adjustable))
            return null;

        object change_id = await tgui_input_number(user, I18n.T("Set the []'s ID", name), I18n.T("Controller ID"), id, 100);
        if (!Dm.truthy(change_id) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await usr.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return null;
        id = change_id;
        to_chat(user, span_notice(I18n.T("You change the ID to [].", id)));
        return null;
    }

    // tg: code/modules/assembly/doorcontrol.dm:52
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = NONE;
        if (!Dm.truthy(copyable))
            return dot;

        if (interacting_with is obj_item_assembly_control other)
        {
            id = other.id;
            balloon_alert(user, I18n.T("id changed"));
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    /// VARSET_CALLBACK(src, cooldown, value)'s target (see the GAP(lead) in the header): ___callbackvarset's datum branch
    /// (varset_callback.dm:8-23) for this var.
    public virtual void cooldown__varset(object var_value = null)
    {
        cooldown = Dm.num(var_value);
    }

}

// tg: code/modules/assembly/doorcontrol.dm:62
public partial class obj_item_assembly_control_blast_door : obj_item_assembly_control
{
    // tg: code/modules/assembly/doorcontrol.dm:67
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (Dm.truthy(id) && Dm.eq(id, -1))
            Dm.append(ref dot, span_notice(I18n.T("Interact with a blast door or shutter to generate a new ID")));
        return dot;
    }

    // tg: code/modules/assembly/doorcontrol.dm:72
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        DmList door_ids = Dm.list();
        DmList display_ids = Dm.list("UNIQUE");
        foreach (obj_machinery_door_poddoor M in Dm.each_cast<obj_machinery_door_poddoor>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_poddoor))))
        {
            if (!Dm.truthy(M.id) || Dm.@in(M.id, door_ids))
                continue;
            Dm.append(door_ids, Dm.str(M.id));
            if (!ReferenceEquals(M.owner?.resolve(), user))
                continue;
            var door_area = (area)get_area(M);
            Dm.append(display_ids, Dm.str(door_area.name) + "(" + Dm.str(M.id) + ")");
        }

        object change_id = await tgui_input_list(user, I18n.T("Set Controller ID"), I18n.T("Controller ID"), display_ids);
        if (!Dm.truthy(change_id) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return null;

        if (Dm.eq(change_id, "UNIQUE"))
            id = -1;
        else
        {
            float start = Dm.findtext(change_id, "(") + 1;
            float end = Dm.length(change_id) - 1; // tg's: the ")" itself, so copytext below drops an id's last character (kept as tg's)
            if (start == end)
                id = Dm.copytext(change_id, start, start + 1); // "[change_id[start]]": the one character
            else
                id = Dm.copytext(change_id, start, end);
        }
        balloon_alert(user, I18n.T("id changed"));
        if (!Dm.eq(id, -1))
            to_chat(user, span_notice(I18n.T("You change the ID to [].", id)));
        else
            to_chat(user, span_notice(I18n.T("You now must interact with a pod door to generate a unique ID.")));
        return null;
    }

    // tg: code/modules/assembly/doorcontrol.dm:103
    public override async DmTask<object> activate(mob activator = null)
    {
        object openclose = null;
        if (Dm.truthy(cooldown))
            return null;
        cooldown = TRUE;
        foreach (obj_machinery_door_poddoor M in Dm.each_cast<obj_machinery_door_poddoor>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_poddoor))))
        {
            if (Dm.eq(M.id, id))
            {
                if (openclose == null || !Dm.truthy(sync_doors))
                    openclose = M.density;
                // INVOKE_ASYNC(M, openclose ? TYPE_PROC_REF(/obj/machinery/door/poddoor, open) : TYPE_PROC_REF(.., close))
                if (Dm.truthy(openclose))
                    Waiting.Detach(M.open());
                else
                    Waiting.Detach(M.close());
            }
        }
        // VARSET_CALLBACK(src, cooldown, FALSE): see the GAP(lead) in the file header
        _addtimer(Dm.@new<datum_callback>(this, nameof(cooldown__varset), FALSE), 1 * SECONDS, file: "code/modules/assembly/doorcontrol.dm", line: 113);
        return null;
    }
}

// tg: code/modules/assembly/doorcontrol.dm:115  (gaps ST2: the theatre's mechanical curtains)
public partial class obj_item_assembly_control_curtain : obj_item_assembly_control
{
    // tg: code/modules/assembly/doorcontrol.dm:119
    public override async DmTask<object> activate(mob activator = null)
    {
        object openclose = null;
        if (Dm.truthy(cooldown))
            return null;
        cooldown = TRUE;
        foreach (obj_structure_curtain_cloth_fancy_mechanical M in Dm.each<obj_structure_curtain_cloth_fancy_mechanical>(GLOB.curtains))
        {
            if (Dm.eq(M.id, id))
            {
                if (openclose == null || !Dm.truthy(sync_doors))
                    openclose = M.density;
                // INVOKE_ASYNC(M, openclose ? TYPE_PROC_REF(/obj/structure/curtain/cloth/fancy/mechanical, open) : TYPE_PROC_REF(.., close))
                if (Dm.truthy(openclose))
                    Waiting.Detach(M.open__proc());
                else
                    Waiting.Detach(M.close());
            }
        }
        // VARSET_CALLBACK(src, cooldown, FALSE): see the GAP(lead) in the file header
        _addtimer(Dm.@new<datum_callback>(this, nameof(cooldown__varset), FALSE), 0.5f * SECONDS, file: "code/modules/assembly/doorcontrol.dm", line: 129);
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/assembly/doorcontrol.dm:132
public partial class obj_item_assembly_control_airlock : obj_item_assembly_control
{
    /*
    Bitflag, 1= open (OPEN)
                2= idscan (IDSCAN)
                4= bolts (BOLTS)
                8= shock (SHOCK)
                16= door safties (SAFE)
    */

    // tg: code/modules/assembly/doorcontrol.dm:145
    public override async DmTask<object> activate(mob activator = null)
    {
        if (Dm.truthy(cooldown))
            return null;
        cooldown = TRUE;
        bool doors_need_closing = false;
        DmList open_or_close = Dm.list();
        foreach (obj_machinery_door_airlock D in Dm.each_cast<obj_machinery_door_airlock>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_airlock))))
        {
            if (Dm.eq(D.id_tag, id))
            {
                if (Dm.truthy(Dm.bitand(specialfunctions, OPEN)))
                {
                    Dm.append(ref open_or_close, D);
                    if (!Dm.truthy(D.density))
                        doors_need_closing = true;
                }
                if (Dm.truthy(Dm.bitand(specialfunctions, IDSCAN)))
                    D.aiDisabledIdScanner = Dm.num(!Dm.truthy(D.aiDisabledIdScanner));
                if (Dm.truthy(Dm.bitand(specialfunctions, BOLTS)))
                {
                    if (!Dm.truthy(D.wires.is_cut(WIRE_BOLTS)) && Dm.truthy(D.hasPower()))
                    {
                        if (Dm.truthy(D.locked))
                            D.unlock();
                        else
                            D.@lock();
                        D.update_appearance();
                    }
                }
                if (Dm.truthy(Dm.bitand(specialfunctions, SHOCK)))
                {
                    if (Dm.truthy(D.secondsElectrified))
                        await D.set_electrified(MACHINE_ELECTRIFIED_PERMANENT, usr as mob);
                    else
                        await D.set_electrified(MACHINE_NOT_ELECTRIFIED, usr as mob);
                }
                if (Dm.truthy(Dm.bitand(specialfunctions, SAFE)))
                    D.safe = Dm.num(!Dm.truthy(D.safe));
            }
        }

        foreach (obj_machinery_door_airlock D in Dm.each_cast<obj_machinery_door_airlock>(open_or_close))
        {
            // INVOKE_ASYNC(D, doors_need_closing ? TYPE_PROC_REF(/obj/machinery/door/airlock, close) : TYPE_PROC_REF(.., open))
            if (doors_need_closing)
                Waiting.Detach(D.close());
            else
                Waiting.Detach(D.open());
        }

        // VARSET_CALLBACK(src, cooldown, FALSE): see the GAP(lead) in the file header
        _addtimer(Dm.@new<datum_callback>(this, nameof(cooldown__varset), FALSE), 1 * SECONDS, file: "code/modules/assembly/doorcontrol.dm", line: 177);
        return null;
    }
}

// tg: code/modules/assembly/doorcontrol.dm:180
public partial class obj_item_assembly_control_massdriver : obj_item_assembly_control
{
    // tg: code/modules/assembly/doorcontrol.dm:194
    public virtual async DmTask activate_stage2()
    {
        Dm.NotTranslated("code/modules/assembly/doorcontrol.dm:194", "activate_stage2: the mass driver controller (mass drivers, M10+)");
    }

    // tg: code/modules/assembly/doorcontrol.dm:201
    public virtual void activate_stage3()
    {
        Dm.NotTranslated("code/modules/assembly/doorcontrol.dm:201", "activate_stage3: the mass driver controller (mass drivers, M10+)");
    }
}

// tg: code/modules/assembly/doorcontrol.dm:245
public partial class obj_item_assembly_control_crematorium : obj_item_assembly_control
{
    // tg: code/modules/assembly/doorcontrol.dm:249  (gaps MD2: the crematorium's button)
    public override async DmTask<object> activate(mob activator = null)
    {
        if (Dm.truthy(cooldown))
            return null;
        cooldown = TRUE;
        foreach (obj_structure_bodycontainer_crematorium C in Dm.each<obj_structure_bodycontainer_crematorium>(GLOB.crematoriums))
        {
            if (Dm.eq(C.id, id))
                await C.cremate(usr as mob);
        }

        // VARSET_CALLBACK(src, cooldown, FALSE): see the GAP(lead) in the file header
        _addtimer(Dm.@new<datum_callback>(this, nameof(cooldown__varset), FALSE), 5 * SECONDS, file: "code/modules/assembly/doorcontrol.dm", line: 257);
        return null;
    }
}

// tg: code/modules/assembly/doorcontrol.dm:210
public partial class obj_item_assembly_control_igniter : obj_item_assembly_control
{
    // tg: code/modules/assembly/doorcontrol.dm:216  (the engineering package E8, D287: the ignition switches of the incinerators and xenobiology)
    public override async DmTask<object> activate(mob activator = null)
    {
        if (Dm.truthy(cooldown))
            return null;
        cooldown = TRUE;
        foreach (obj_machinery_sparker M in Dm.each_cast<obj_machinery_sparker>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_sparker))))
        {
            if (Dm.eq(M.id, id))
                Waiting.Detach(M.ignite()); // INVOKE_ASYNC(M, TYPE_PROC_REF(/obj/machinery/sparker, ignite))
        }

        foreach (obj_machinery_igniter M in Dm.each_cast<obj_machinery_igniter>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_igniter))))
        {
            if (Dm.eq(M.id, id))
                Waiting.DetachSync(() => M.toggle()); // INVOKE_ASYNC(M, TYPE_PROC_REF(/obj/machinery/igniter, toggle))
        }

        // VARSET_CALLBACK(src, cooldown, FALSE): see the GAP(lead) in the file header
        _addtimer(Dm.@new<datum_callback>(this, nameof(cooldown__varset), FALSE), 3 * SECONDS, file: "code/modules/assembly/doorcontrol.dm", line: 228);
        return null;
    }
}
