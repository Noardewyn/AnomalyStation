// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/syndicatebeacon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G2 (D316): syndicatebeacon.dm whole: the singularity beacon (bolted on a powered cable node it draws
// power and calls every singularity on its level), the No Escape beacon with its cell, and the suspicious beacons that drop a
// beacon, a syndicate bomb, an EMP bomb, a power sink, a clown bomb or a horse where they are used. The No Escape beacon's
// black hole shuttle event is created by the shuttle's add_shuttle_event; the event itself waits for its package. Its var
// block is hand-written: the generator types `cell` by its name as a Type (tg holds the typepath until Initialize).

////////////////////////////////////////
//Singularity beacon
////////////////////////////////////////
// tg: code/game/machinery/syndicatebeacon.dm:4
public partial class obj_machinery_power_singularity_beacon : obj_machinery_power
{
    // tg: code/game/machinery/syndicatebeacon.dm:22
    public virtual async DmTask Activate(mob user = null)
    {
        if (Dm.num(surplus()) < 1500)
        {
            if (user != null)
                to_chat(user, span_notice(I18n.T("The connected wire doesn't have enough current.")));
            return;
        }
        icon_state = Dm.text("[]1", icontype);
        active = TRUE;
        if (user != null)
            to_chat(user, span_notice(I18n.T("You activate the beacon.")));
    }

    // tg: code/game/machinery/syndicatebeacon.dm:33
    public virtual void Deactivate(mob user = null)
    {
        foreach (datum_component_singularity singulo in Dm.each_cast<datum_component_singularity>(GLOB.singularities))
        {
            if (singulo.target == this)
                singulo.target = null;
        }
        icon_state = Dm.text("[]0", icontype);
        active = FALSE;
        if (user != null)
            to_chat(user, span_notice(I18n.T("You deactivate the beacon.")));
    }

    // tg: code/game/machinery/syndicatebeacon.dm:45
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(anchored))
        {
            if (Dm.truthy(active)) // return active ? Deactivate(user) : Activate(user): both return nothing
                Deactivate(user);
            else
                await Activate(user);
            return null;
        }
        else
            to_chat(user, span_warning(I18n.T("You need to screw \\the [] to the floor first!", this)));
        return dot;
    }

    // tg: code/game/machinery/syndicatebeacon.dm:54
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = TRUE;
        if (Dm.truthy(active))
        {
            to_chat(user, span_warning(I18n.T("You need to deactivate \\the [] first!", this)));
            return dot;
        }

        if (Dm.truthy(anchored))
        {
            tool.play_tool_sound(this, 50);
            await set_anchored(FALSE);
            to_chat(user, span_notice(I18n.T("You unbolt \\the [] from the floor and detach it from the cable.", this)));
            disconnect_from_network();
            return dot;
        }
        else
        {
            if (!Dm.truthy(connect_to_network()))
            {
                to_chat(user, span_warning(I18n.T("\\The [] must be placed over an exposed, powered cable node!", this)));
                return dot;
            }
            tool.play_tool_sound(this, 50);
            await set_anchored(TRUE);
            to_chat(user, span_notice(I18n.T("You bolt \\the [] to the floor and attach it to the cable.", this)));
            return dot;
        }
    }

    // tg: code/game/machinery/syndicatebeacon.dm:75
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        user.visible_message(
            I18n.T("[] messes with \\the [] for a bit.", user, this),
            span_notice(I18n.T("You can't fit the screwdriver into \\the []'s bolts! Try using a wrench.", this)));
        return TRUE;
    }

    // tg: code/game/machinery/syndicatebeacon.dm:81
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(active))
            Deactivate();
        return base.Destroy(force);
    }

    //stealth direct power usage
    // tg: code/game/machinery/syndicatebeacon.dm:87
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(), null); // set waitfor = FALSE, inherited from /datum/process (waiting-model.md 5.4)
    }

    private async DmTask<object> process__body()
    {
        if (!Dm.truthy(active))
            return null;

        if (Dm.num(surplus()) >= energy_used)
        {
            add_load(energy_used);
            if (singularity_beacon_cd <= world.time) // COOLDOWN_FINISHED(src, singularity_beacon_cd)
            {
                singularity_beacon_cd = world.time + 8 * SECONDS; // COOLDOWN_START(src, singularity_beacon_cd, 8 SECONDS)
                foreach (datum_component_singularity singulo_component in Dm.each_cast<datum_component_singularity>(GLOB.singularities))
                {
                    atom singulo = singulo_component.parent as atom;
                    if (Dm.eq(singulo.z, z))
                    {
                        singulo_component.target = this;
                        await say(I18n.T("[] is now [] standard lengths away to the []", singulo, get_dist(this, singulo), dir2text(get_dir(this, singulo))));
                    }
                }
            }
        }
        else
        {
            Deactivate();
            await say(I18n.T("Insufficient charge detected - powering down"));
        }
        return null;
    }
}

// Used for the No Escape final objective that attracts a singularity to the escape shuttle
// needs to be charged with an inducer to work
// tg: code/game/machinery/syndicatebeacon.dm:105
public partial class obj_machinery_power_singularity_beacon_syndicate_no_escape : obj_machinery_power_singularity_beacon_syndicate
{
    /// The cell we spawn with
    // tg: code/game/machinery/syndicatebeacon.dm:110  (the typepath until Initialize, the cell itself after it)
    public object cell = typeof(obj_item_stock_parts_power_store_cell_super_empty);

    // tg: code/game/machinery/syndicatebeacon.dm:105
    public obj_machinery_power_singularity_beacon_syndicate_no_escape()
    {
        name = "ominous beacon";
        desc = "This looks very suspicious...";
        processing_flags = START_PROCESSING_MANUALLY;
    }

    private obj_item_stock_parts_power_store_cell cell_item => cell as obj_item_stock_parts_power_store_cell;

    // tg: code/game/machinery/syndicatebeacon.dm:115
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        cell = Dm.@new((Type)cell, this);
        return dot;
    }

    // tg: code/game/machinery/syndicatebeacon.dm:119
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(active))
            Deactivate();
        object cell__1 = cell;
        cell = null;
        qdel(cell__1); // QDEL_NULL(cell)
        // destroying the beacon doesn't automatically stop the event
        no_escape_event = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/syndicatebeacon.dm:127
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(I18n.T("\\The [] is [].", this, Dm.truthy(active) ? I18n.T("on") : I18n.T("off")));
        if (cell != null)
            dot.Add(I18n.T("The charge meter reads []%.", cell_item != null ? round(Dm.num(cell_item.percent()), 1) : 0));
        return dot;
    }

    // tg: code/game/machinery/syndicatebeacon.dm:133
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cell;
    }

    // tg: code/game/machinery/syndicatebeacon.dm:136
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(active)) // return active ? Deactivate(user) : Activate(user): both return nothing
            Deactivate(user);
        else
            await Activate(user);
        return null;
    }

    // tg: code/game/machinery/syndicatebeacon.dm:139
    public override async DmTask Activate(mob user = null)
    {
        if (!Dm.truthy(cell_item.charge__proc()))
        {
            await say(I18n.T("Insufficient charge detected"));
            return;
        }

        icon_state = Dm.text("[]1", icontype);
        active = TRUE;
        begin_processing();
        if (user != null)
            to_chat(user, span_notice(I18n.T("You activate the beacon.")));
    }

    // tg: code/game/machinery/syndicatebeacon.dm:150
    public override void Deactivate(mob user = null)
    {
        icon_state = Dm.text("[]0", icontype);
        active = FALSE;
        end_processing();
        if (user != null)
            to_chat(user, span_notice(I18n.T("You deactivate the beacon.")));
    }

    // tg: code/game/machinery/syndicatebeacon.dm:157
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = TRUE;

        tool.play_tool_sound(this, 50);
        if (Dm.truthy(anchored))
        {
            await set_anchored(FALSE);
            to_chat(user, span_notice(I18n.T("You unbolt \\the [] from the floor.", this)));
            return dot;
        }
        else
        {
            await set_anchored(TRUE);
            to_chat(user, span_notice(I18n.T("You bolt \\the [] to the floor.", this)));
            return dot;
        }
    }

    // tg: code/game/machinery/syndicatebeacon.dm:170
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    // tg: code/game/machinery/syndicatebeacon.dm:173
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
            return dot;
        if (cell_item != null)
            await cell_item.emp_act(severity);
        return dot;
    }

    // tg: code/game/machinery/syndicatebeacon.dm:179
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(no_escape_process__body(), null); // set waitfor = FALSE, inherited from /datum/process (waiting-model.md 5.4)
    }

    private async DmTask<object> no_escape_process__body()
    {
        if (Dm.truthy(cell_item.charge__proc()))
        {
            await cell_item.use(energy_used, Dm.named("force", TRUE));

            if (no_escape_event == null)
            {
                area escape_shuttle_area = get_area(this) as area;
                // beacon must be on the traveling escape shuttle (not a pod)
                if (istype(escape_shuttle_area, typeof(area_shuttle_escape)) && Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ESCAPE) && Dm.truthy(SSshuttle.emergency.is_in_shuttle_bounds(this)))
                {
                    obj_docking_port_mobile port = SSshuttle.emergency;
                    no_escape_event = port.add_shuttle_event(typeof(datum_shuttle_event_simple_spawner_black_hole_no_escape)) as datum_shuttle_event_simple_spawner_black_hole_no_escape;
                    no_escape_event.beacon = this;
                }
            }
        }
        else
        {
            Deactivate();
            await say(I18n.T("Insufficient charge detected - powering down"));
        }
        return null;
    }
}

// SINGULO BEACON SPAWNER
// tg: code/game/machinery/syndicatebeacon.dm:203
public partial class obj_item_sbeacondrop : obj_item
{
    // tg: code/game/machinery/syndicatebeacon.dm:215
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (user != null)
        {
            to_chat(user, span_notice(I18n.T("Locked In.")));
            Dm.@new(droptype, user.loc);
            playsound(this, Dm.resource("sound/effects/pop.ogg"), 100, TRUE, TRUE);
            qdel(this);
        }
        return null;
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/game/machinery/syndicatebeacon.dm:42
public partial class obj_machinery_power_singularity_beacon : obj_machinery_power
{
    // tg: code/game/machinery/syndicatebeacon.dm:42
    public override async DmTask<object> attack_ai(mob user = null)
    {
        return null;
    }
}
