// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/wormholes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2c (D327): the whole file: four hundred wormholes open on the station's floors, jump to other floors every few seconds and
// throw whoever walks into one out of another. The var blocks are generated (D111).

public partial class datum_controller_global_vars
{
    // tg: code/modules/events/wormholes.dm:1  GLOBAL_LIST_EMPTY(all_wormholes)
    public DmList all_wormholes; // So we can pick wormholes to teleport to
    public virtual void InitGlobalall_wormholes()
    {
        all_wormholes = Dm.list();
        Dm.append(ref gvars_datum_init_order, "all_wormholes");
    }
}

// tg: code/modules/events/wormholes.dm:15
public partial class datum_round_event_wormholes : datum_round_event
{
    // tg: code/modules/events/wormholes.dm:23
    public override async DmTask<object> setup()
    {
        announce_when = rand(0, 20);
        end_when = rand(40, 80);
        return null;
    }

    // tg: code/modules/events/wormholes.dm:27
    public override async DmTask<object> start()
    {
        foreach (turf_open_floor valid in Dm.each<turf_open_floor>(GLOB.station_turfs))
            Dm.append(pick_turfs, valid);

        for (int i = 1; i <= number_of_wormholes; i++)
        {
            var T = (turf)pick(pick_turfs);
            Dm.append(wormholes, Dm.@new<obj_effect_portal_wormhole>(T, 0, null, FALSE));
            playsound(T, SFX_PORTAL_CREATED, 20, TRUE, SILENCED_SOUND_EXTRARANGE); // much much quieter
        }
        return null;
    }

    // tg: code/modules/events/wormholes.dm:37
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce("Space-time anomalies detected on the station. There is no additional data.", "Anomaly Alert", ANNOUNCER_SPANOMALIES);
    }

    // tg: code/modules/events/wormholes.dm:40
    public override async DmTask tick()
    {
        if (activeFor % shift_frequency == 0)
        {
            foreach (obj_effect_portal_wormhole O in Dm.each_cast<obj_effect_portal_wormhole>(wormholes))
            {
                var T = (turf)pick(pick_turfs);
                if (isopenturf(T))
                {
                    await O.forceMove(T);
                    playsound(T, SFX_PORTAL_CREATED, 20, TRUE, SILENCED_SOUND_EXTRARANGE);
                }
            }
        }
    }

    // tg: code/modules/events/wormholes.dm:49
    public override async DmTask end()
    {
        QDEL_LIST(wormholes);
        wormholes = null;
    }
}

// tg: code/modules/events/wormholes.dm:52
public partial class obj_effect_portal_wormhole : obj_effect_portal
{
    // tg: code/modules/events/wormholes.dm:61
    [DmParams("mapload", "_creator", "_lifespan", "_linked", "automatic_link", "hard_target_override")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.all_wormholes, this);
        return dot;
    }

    // tg: code/modules/events/wormholes.dm:65
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        Dm.remove(ref GLOB.all_wormholes, this);
        return dot;
    }

    // tg: code/modules/events/wormholes.dm:69
    public override async DmTask<object> teleport(atom_movable M = null, object force = null)
    {
        if (iseffect(M)) //sparks don't teleport
            return null;
        if (Dm.truthy(M.anchored))
        {
            if (!(ismecha(M) && Dm.truthy(mech_sized)))
                return null;
        }

        if (ismovable(M))
        {
            if (GLOB.all_wormholes.len > 0)
            {
                var P = pick(GLOB.all_wormholes) as obj_effect_portal_wormhole;
                if (P != null && isturf(P.loc))
                    hard_target = (turf)P.loc;
            }
            if (hard_target == null)
                return null;
            turf start_turf = get_turf(M);
            if (Dm.truthy(await do_teleport(M, hard_target, 1, null, null, channel: TELEPORT_CHANNEL_WORMHOLE))) ///You will appear adjacent to the beacon
            {
                playsound(start_turf, SFX_PORTAL_ENTER, 50, 1, SHORT_RANGE_SOUND_EXTRARANGE);
                playsound(hard_target, SFX_PORTAL_ENTER, 50, 1, SHORT_RANGE_SOUND_EXTRARANGE);
            }
        }
        return null;
    }
}
