// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/wormhole_jaunter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The wormhole jaunter: used in hand it opens a jaunt tunnel to a random teleport beacon on the station; worn on the belt it saves
// its wearer from a chasm by throwing them through the tunnel at once; an EMP may set it off.

/**********************Jaunter**********************/
// tg: code/modules/mining/equipment/wormhole_jaunter.dm:2
public partial class obj_item_wormhole_jaunter : obj_item
{
    // tg: code/modules/mining/equipment/wormhole_jaunter.dm:17
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        user.visible_message(span_notice(I18n.T("[] activates \\the []!", user.name, this)));
        SSblackbox.record_feedback("tally", "jaunter", 1, "User"); // user activated
        await activate(user, TRUE);
        return null;
    }

    // tg: code/modules/mining/equipment/wormhole_jaunter.dm:22
    public virtual object turf_check(mob user = null)
    {
        turf device_turf = get_turf(this);
        if (device_turf == null || Dm.truthy(is_centcom_level(device_turf.z)) || Dm.truthy(is_reserved_level(device_turf.z)))
        {
            if (user != null)
                to_chat(user, span_notice(I18n.T("You're having difficulties getting \\the [] to work.", this)));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/mining/equipment/wormhole_jaunter.dm:30
    public virtual object get_destinations()
    {
        DmList destinations = Dm.list();

        foreach (obj_item_beacon B in Dm.each<obj_item_beacon>(GLOB.teleportbeacons))
        {
            turf T = get_turf(B);
            if (Dm.truthy(is_station_level(T.z)))
                Dm.append(ref destinations, B);
        }

        return destinations;
    }

    // tg: code/modules/mining/equipment/wormhole_jaunter.dm:40
    public virtual object can_jaunter_teleport()
    {
        var destinations = (DmList)get_destinations();
        return destinations.len > 0 ? TRUE : FALSE;
    }

    // tg: code/modules/mining/equipment/wormhole_jaunter.dm:44
    public virtual async DmTask<object> activate(mob user = null, object adjacent = null, object teleport = null)
    {
        if (!Dm.truthy(turf_check(user)))
            return FALSE;

        if (!Dm.truthy(can_jaunter_teleport()))
        {
            if (user != null)
                to_chat(user, span_notice(I18n.T("\\The [] found no beacons in the world to anchor a wormhole to.", this)));
            else
                visible_message(span_notice(I18n.T("\\The [] found no beacons in the world to anchor a wormhole to!", this)));
            return FALSE;
        }

        var destinations = (DmList)get_destinations();
        object chosen_beacon = pick(destinations);

        var tunnel = Dm.@new<obj_effect_portal_jaunt_tunnel>(get_turf(this), 100, null, FALSE, get_turf(chosen_beacon));
        if (Dm.truthy(teleport))
            await tunnel.teleport(user);
        else if (Dm.truthy(adjacent))
            await try_move_adjacent(tunnel);

        qdel(this);
        return TRUE;
    }

    // tg: code/modules/mining/equipment/wormhole_jaunter.dm:67
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object power = severity;
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;

        bool triggered = false;
        if (Dm.eq(power, 1))
            triggered = true;
        else if (Dm.eq(power, 2) && prob(50))
            triggered = true;

        var M = loc as mob;
        if (M != null && triggered)
        {
            M.visible_message(span_userdanger(I18n.T("Your [] overloads and activates!", name)));
            SSblackbox.record_feedback("tally", "jaunter", 1, "EMP"); // EMP accidental activation
            await activate(M, FALSE, TRUE);
        }
        else if (triggered)
        {
            visible_message(span_warning(I18n.T("\\The [] overloads and activates!", this)));
            await activate();
        }
        return dot;
    }

    // tg: code/modules/mining/equipment/wormhole_jaunter.dm:87
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_BELT)))
            RegisterSignal(user, COMSIG_MOVABLE_CHASM_DROPPED, nameof(chasm_react));
    }

    // tg: code/modules/mining/equipment/wormhole_jaunter.dm:92
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        UnregisterSignal(user, COMSIG_MOVABLE_CHASM_DROPPED);
        return dot;
    }

    // tg: code/modules/mining/equipment/wormhole_jaunter.dm:96
    [SignalHandler]
    public virtual object chasm_react(mob_living user = null, turf chasm = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(activate(user, FALSE, TRUE))))
            return null;

        to_chat(user, span_userdanger(I18n.T("Your [] activates, saving you from \\the []!", this, chasm)));
        chasm.visible_message(span_boldwarning(I18n.T("[] falls into \\the []!", user, chasm))); // To freak out any bystanders
        SSblackbox.record_feedback("tally", "jaunter", 1, "Chasm"); // Chasm automatic activation
        return COMPONENT_NO_CHASM_DROP;
    }
}

//jaunter tunnel
// tg: code/modules/mining/equipment/wormhole_jaunter.dm:109
public partial class obj_effect_portal_jaunt_tunnel : obj_effect_portal
{
    // tg: code/modules/mining/equipment/wormhole_jaunter.dm:118
    public override async DmTask<object> teleport(atom_movable moving = null, object force = null)
    {
        atom_movable M = moving;
        object dot = await base.teleport(M, force);
        if (Dm.truthy(dot))
        {
            // KERPLUNK
            playsound(M, Dm.resource("sound/items/weapons/resonator_blast.ogg"), 50, TRUE);
            if (iscarbon(M))
            {
                var L = (mob_living_carbon)M;
                await L.Paralyze(60);
                if (ishuman(L))
                {
                    shake_camera(L, 20, 1);
                    _addtimer(Dm.@new<datum_callback>(L, nameof(mob_living_carbon.vomit)), 2 * SECONDS, file: "code/modules/mining/equipment/wormhole_jaunter.dm", line: 128);
                }
            }
        }
        return dot;
    }
}
