// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/beacon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1a (D327): the whole file, the tracking beacon (GLOB.teleportbeacons: the bluespace anomaly's destination), but its context
// tips (add_context, a live-partial.txt row: the screentips are not now, the owner's rule of 2026-10-29). The var block is
// generated (D111).

// tg: code/game/objects/items/devices/beacon.dm:1
public partial class obj_item_beacon : obj_item
{
    // tg: code/game/objects/items/devices/beacon.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(enabled))
            Dm.append(ref GLOB.teleportbeacons, this);
        else
            icon_state = "beacon-off";
        register_context();
        return dot;
    }

    // tg: code/game/objects/items/devices/beacon.dm:22
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.teleportbeacons, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/beacon.dm:33
    public virtual void turn_off()
    {
        icon_state = "beacon-off";
        Dm.remove(ref GLOB.teleportbeacons, this);
        SEND_SIGNAL(COMSIG_BEACON_DISABLED, Dm.list(this));
    }

    // tg: code/game/objects/items/devices/beacon.dm:38
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        enabled = Dm.truthy(enabled) ? FALSE : TRUE;
        if (Dm.truthy(enabled))
        {
            icon_state = "beacon";
            Dm.append(ref GLOB.teleportbeacons, this);
        }
        else
            turn_off();
        to_chat(user, span_notice(I18n.T("You [] the beacon.", Dm.truthy(enabled) ? I18n.T("enable") : I18n.T("disable"))));
        return null;
    }

    // tg: code/game/objects/items/devices/beacon.dm:48
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        await attack_self(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }
}
