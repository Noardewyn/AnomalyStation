// Translated from tgstation (snapshot 1d99bd7f): code/modules/recycling/disposal/outlet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D325: the disposal system).

// the disposal outlet machine

// tg: code/modules/recycling/disposal/outlet.dm:15
public partial class obj_structure_disposaloutlet : obj_structure
{
    //how fast disposal machinery is ejecting things and how far it goes
    /// The slowest setting for disposal eject speed
    // tg: code/modules/recycling/disposal/outlet.dm:3
    public const float EJECT_SPEED_SLOW = 1;
    // tg: code/modules/recycling/disposal/outlet.dm:4
    public const float EJECT_RANGE_SLOW = 2;
    /// The default setting for disposal eject speed
    // tg: code/modules/recycling/disposal/outlet.dm:6
    public const float EJECT_SPEED_MED = 2;
    // tg: code/modules/recycling/disposal/outlet.dm:7
    public const float EJECT_RANGE_MED = 4;
    /// The fast setting for disposal eject speed
    // tg: code/modules/recycling/disposal/outlet.dm:9
    public const float EJECT_SPEED_FAST = 4;
    // tg: code/modules/recycling/disposal/outlet.dm:10
    public const float EJECT_RANGE_FAST = 6;
    /// The fastest, emag exclusive setting for disposal eject speed
    // tg: code/modules/recycling/disposal/outlet.dm:12
    public const float EJECT_SPEED_YEET = 6;
    // tg: code/modules/recycling/disposal/outlet.dm:13
    public const float EJECT_RANGE_YEET = 10;

    // tg: code/modules/recycling/disposal/outlet.dm:32
    [DmParams("mapload", "make_from")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_structure_disposalconstruct make_from = Dm.arg<obj_structure_disposalconstruct>(__extra, 0);
        object dot = base.Initialize(mapload, Dm.forward(__extra, make_from));
        if (make_from != null)
        {
            Waiting.NoSleep(setDir(make_from.dir));
            Waiting.NoSleep(make_from.forceMove(this));
            stored = make_from;
        }
        else
            stored = Dm.@new<obj_structure_disposalconstruct>(this, null, SOUTH, FALSE, this);

        target = get_ranged_target_turf(this, dir, 10) as turf;

        obj_structure_disposalpipe_trunk found_trunk = Dm.locate<obj_structure_disposalpipe_trunk>(loc);
        if (found_trunk != null)
        {
            found_trunk.set_linked(this); // link the pipe trunk to self
            trunk = found_trunk;
        }
        return dot;
    }

    // tg: code/modules/recycling/disposal/outlet.dm:48
    public override object Destroy(float? force = null)
    {
        if (trunk != null)
        {
            // preemptively expel the contents from the trunk
            // in case the outlet is deleted before expel_holder could be called.
            obj_structure_disposalholder holder = Dm.locate<obj_structure_disposalholder>(trunk);
            if (holder != null)
                Waiting.NoSleep(trunk.expel(holder));
            trunk.linked = null;
            trunk = null;
        }
        QDEL_NULL(ref stored);
        return base.Destroy(force);
    }

    // expel the contents of the holder object, then delete it
    // called when the holder exits the outlet
    // tg: code/modules/recycling/disposal/outlet.dm:62
    public virtual void expel(obj_structure_disposalholder H = null)
    {
        H.active = FALSE;
        flick("outlet-open", this);
        if ((start_eject + 30) < world.time)
        {
            start_eject = world.time;
            playsound(this, Dm.resource("sound/machines/warning-buzzer.ogg"), 50, FALSE, FALSE);
            _addtimer(Dm.@new<datum_callback>(this, nameof(expel_holder), H, TRUE), 2 * SECONDS, file: "code/modules/recycling/disposal/outlet.dm", line: 68);
        }
        else
            _addtimer(Dm.@new<datum_callback>(this, nameof(expel_holder), H), 2 * SECONDS, file: "code/modules/recycling/disposal/outlet.dm", line: 70);
    }

    // tg: code/modules/recycling/disposal/outlet.dm:72
    public virtual async DmTask expel_holder(obj_structure_disposalholder H = null, float? playsound = null)
    {
        playsound ??= FALSE;
        if (Dm.truthy(playsound))
            GlobalProcs.playsound(this, Dm.resource("sound/machines/hiss.ogg"), 50, FALSE, FALSE);

        if (QDELETED(H))
            return;

        await pipe_eject(H, dir, TRUE, target, eject_range, eject_speed);

        await H.vent_gas(loc as turf);
        qdel(H);
    }

    // tg: code/modules/recycling/disposal/outlet.dm:84
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        obj_item I = tool;
        await base.welder_act(user, I);
        if (!Dm.truthy(I.tool_start_check(user, amount: 1, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return TRUE;

        playsound(this, Dm.resource("sound/items/tools/welder2.ogg"), 100, TRUE);
        to_chat(user, span_notice(I18n.T("You start slicing the floorweld off []...", this)));
        if (Dm.truthy(await I.use_tool(this, user, 20)))
        {
            to_chat(user, span_notice(I18n.T("You slice the floorweld off [].", this)));
            await stored.forceMove(loc);
            transfer_fingerprints_to(stored);
            stored = null;
            qdel(this);
        }
        return TRUE;
    }

    // tg: code/modules/recycling/disposal/outlet.dm:100
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        switch (eject_speed)
        {
            case EJECT_SPEED_SLOW:
                Dm.append(ref dot, span_info(I18n.T("An LED image of a turtle is displayed on the side of the outlet.")));
                break;
            case EJECT_SPEED_MED:
                Dm.append(ref dot, span_info(I18n.T("An LED image of a bumblebee is displayed on the side of the outlet.")));
                break;
            case EJECT_SPEED_FAST:
                Dm.append(ref dot, span_info(I18n.T("An LED image of a speeding bullet is displayed on the side of the outlet.")));
                break;
            case EJECT_SPEED_YEET:
                Dm.append(ref dot, span_info(I18n.T("An LED image of a grawlix is displayed on the side of the outlet.")));
                break;
        }
        return dot;
    }

    // tg: code/modules/recycling/disposal/outlet.dm:112
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await base.multitool_act(user, tool);
        //if emagged it cant change the speed setting off max
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            to_chat(user, span_notice(I18n.T("The LED display flashes an error!")));
        else
        {
            to_chat(user, span_notice(I18n.T("You adjust the ejection force on \\the [].", this)));
            switch (eject_speed)
            {
                case EJECT_SPEED_SLOW:
                    eject_speed = EJECT_SPEED_MED;
                    eject_range = EJECT_RANGE_MED;
                    break;
                case EJECT_SPEED_MED:
                    eject_speed = EJECT_SPEED_FAST;
                    eject_range = EJECT_RANGE_FAST;
                    break;
                default:
                    eject_speed = EJECT_SPEED_SLOW;
                    eject_range = EJECT_RANGE_SLOW;
                    break;
            }
        }
        return TRUE;
    }

    // tg: code/modules/recycling/disposal/outlet.dm:129
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        object dot = null; // ..(): no live ancestor, null
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return dot;
        balloon_alert(user, I18n.T("ejection force maximized"));
        Dm.combine(ref obj_flags, EMAGGED);
        eject_speed = EJECT_SPEED_YEET;
        eject_range = EJECT_RANGE_YEET;
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/modules/recycling/disposal/outlet.dm:139
    public override async DmTask<object> force_pushed(atom_movable pusher = null, float? force = null, object direction = null)
    {
        force ??= MOVE_FORCE_DEFAULT;
        object dot = await base.force_pushed(pusher, force, direction);
        if (!(stored == null))
        {
            await stored.forceMove(loc);
            transfer_fingerprints_to(stored);
            stored = null;
            visible_message(span_warning(I18n.T("[] is ripped free from the floor!", this)));
            qdel(this);
        }
        return dot;
    }

    // tg: code/modules/recycling/disposal/outlet.dm:148
    public override async DmTask<object> move_crushed(atom_movable pusher = null, float? force = null, object direction = null)
    {
        force ??= MOVE_FORCE_DEFAULT;
        object dot = await base.move_crushed(pusher, force, direction);
        if (!(stored == null))
        {
            await stored.forceMove(loc);
            transfer_fingerprints_to(stored);
            stored = null;
            visible_message(span_warning(I18n.T("[] is ripped free from the floor!", this)));
            qdel(this);
        }
        return dot;
    }
}
