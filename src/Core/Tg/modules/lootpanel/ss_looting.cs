// Translated from tgstation (snapshot 1d99bd7f): code/modules/lootpanel/ss_looting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package G1 (D291): the whole file but the stat line. The var block (name, wait, flags, priority, runlevels, backlog, processing) is
// generated.

/// Queues image generation for search objects without icons
// tg: code/modules/lootpanel/ss_looting.dm:2 SUBSYSTEM_DEF(looting)
public partial class datum_controller_subsystem_looting : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(looting) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSlooting)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/modules/lootpanel/ss_looting.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSlooting != this)
        {
            if (SSlooting is datum_controller_subsystem_looting)
            {
                Recover();
                qdel(SSlooting);
            }
            SSlooting = this;
        }
        PreInit();
    }

    // tg: code/modules/lootpanel/ss_looting.dm:12
    public override object stat_entry(object msg = null)
    {
        Dm.NotTranslated("code/modules/lootpanel/ss_looting.dm:12", "stat_entry: stat panels are not translated (translation-rules.md 13)");
        return null;
    }

    // tg: code/modules/lootpanel/ss_looting.dm:17
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        if (!Dm.truthy(length(backlog)) && !Dm.truthy(length(processing)))
            return;

        if (!Dm.truthy(resumed))
        {
            processing = backlog;
            backlog = Dm.list();
        }

        while (Dm.truthy(length(processing)))
        {
            datum_lootpanel panel = processing[processing.len] as datum_lootpanel;
            if (QDELETED(panel) || !Dm.truthy(length(panel.to_image)))
            {
                processing.len--;
                continue;
            }

            if (!Dm.truthy(await panel.process_images()))
                Dm.append(backlog, panel);

            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;

            processing.len--;
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/lootpanel/ss_looting.dm:2  SUBSYSTEM_DEF(looting): GLOBAL_REAL(SSlooting, /datum/controller/subsystem/looting)
    internal datum_controller_subsystem_looting SSlooting;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_looting SSlooting => ref TgState.Current.SSlooting;
}
