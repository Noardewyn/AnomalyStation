// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/area_contents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Responsible for managing the sizes of area.contained_turfs and area.turfs_to_uncontain
 * These lists do not check for duplicates, which is fine, but it also means they can balloon in size over time
 * as a consequence of repeated changes in area in a space
 * They additionally may not always resolve often enough to avoid memory leaks
 * This is annoying, so lets keep an eye on them and cut them down to size if needed
 */
// tg: code/controllers/subsystem/area_contents.dm:9 SUBSYSTEM_DEF(area_contents)
[DmPath("/datum/controller/subsystem/area_contents")]
public partial class datum_controller_subsystem_area_contents : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/area_contents.dm:1  #define ALLOWED_LOOSE_TURFS 100 (file-local, #undef at :72)
    private const int ALLOWED_LOOSE_TURFS = 100;

    // tg: code/controllers/subsystem/area_contents.dm:10-12
    public datum_controller_subsystem_area_contents()
    {
        name = "Area Contents";
        ss_flags = SS_NO_INIT;
        runlevels = Dm.bitor(RUNLEVEL_LOBBY, RUNLEVELS_DEFAULT);
    }

    // tg: code/controllers/subsystem/area_contents.dm:13
    public DmList currentrun;
    // tg: code/controllers/subsystem/area_contents.dm:14
    public DmList marked_for_clearing;

    // tg: code/controllers/subsystem/area_contents.dm:14 (object-creating defaults, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        marked_for_clearing = Dm.list();
    }

    // The New() that SUBSYSTEM_DEF(area_contents) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSarea_contents)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/area_contents.dm:9
    public override async DmTask New(params object[] __extra)
    {
        if (SSarea_contents != this)
        {
            if (SSarea_contents is datum_controller_subsystem_area_contents)
            {
                Recover();
                qdel(SSarea_contents);
            }
            SSarea_contents = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/area_contents.dm:16
    public override object stat_entry(object msg = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/area_contents.dm:16", "stat_entry: stat panels are not translated (translation-rules.md 13)");
        return null;
    }


    // tg: code/controllers/subsystem/area_contents.dm:29
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        if (!Dm.truthy(resumed))
            currentrun = GLOB.areas.Copy();

        while (Dm.truthy(length(currentrun)))
        {
            area test = currentrun[length(currentrun)] as area;
            for (float area_zlevel = 1, area_zlevel__end = length(test.turfs_to_uncontain_by_zlevel); area_zlevel <= area_zlevel__end; area_zlevel++)
            {
                if (length(test.turfs_to_uncontain_by_zlevel[area_zlevel]) > ALLOWED_LOOSE_TURFS)
                {
                    Dm.combine(ref marked_for_clearing, test);
                    break;
                }
            }
            currentrun.len--;
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0))
                return;
        }

        // Alright, if we've done a scan on all our areas, it's time to knock the existing ones down to size
        while (Dm.truthy(length(marked_for_clearing)))
        {
            area clear = marked_for_clearing[length(marked_for_clearing)] as area;

            for (float area_zlevel = 1, area_zlevel__end = length(clear.turfs_to_uncontain_by_zlevel); area_zlevel <= area_zlevel__end; area_zlevel++)
            {
                if (!Dm.truthy(length(clear.turfs_to_uncontain_by_zlevel[area_zlevel])))
                    continue;
                if (length(clear.turfs_by_zlevel) < area_zlevel)
                {
                    stack_trace(Dm.text("[]([])'s turfs_by_zlevel is length [] but we are being asked to remove turfs from zlevel [] from it.", clear, clear.type, length(clear.turfs_by_zlevel), area_zlevel), "code/controllers/subsystem/area_contents.dm", 51);
                    clear.turfs_to_uncontain_by_zlevel[area_zlevel] = Dm.list();
                    continue;
                }

                // The operation of cutting large lists can be expensive
                // It scales almost directly with the size of the list we're cutting with
                // Because of this, we're gonna stick to cutting 1 entry at a time
                // There's no reason to batch it I promise, this is faster. No overtime too
                float amount_cut = 0;
                DmList cut_from = clear.turfs_to_uncontain_by_zlevel[area_zlevel] as DmList;
                for (float amount_cut__it = 1, amount_cut__end = length(cut_from); amount_cut__it <= amount_cut__end; amount_cut__it++)
                {
                    amount_cut = amount_cut__it;
                    Dm.remove_at(clear.turfs_by_zlevel, area_zlevel, cut_from[amount_cut]);
                    if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0))
                    {
                        cut_from.Cut(1, amount_cut + 1);
                        return;
                    }
                }
                // avoid double removals if we return early on a subsequent z-level
                clear.turfs_to_uncontain_by_zlevel[area_zlevel] = Dm.list();
            }

            clear.turfs_to_uncontain_by_zlevel = Dm.list();
            marked_for_clearing.len--;
        }
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/area_contents.dm:9  SUBSYSTEM_DEF(area_contents): GLOBAL_REAL(SSarea_contents, /datum/controller/subsystem/area_contents)
    internal datum_controller_subsystem_area_contents SSarea_contents;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_area_contents SSarea_contents => ref TgState.Current.SSarea_contents;
}
