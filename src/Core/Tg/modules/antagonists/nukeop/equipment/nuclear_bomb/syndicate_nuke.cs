// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/nukeop/equipment/nuclear_bomb/syndicate_nuke.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1a, D316: the operatives' nuke; their team comes with N1b). The var block is generated (D111).

// tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/syndicate_nuke.dm:1
public partial class obj_machinery_nuclearbomb_syndicate : obj_machinery_nuclearbomb
{
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/syndicate_nuke.dm:3
    public override Type get_cinematic_type(object detonation_status = null)
    {
        switch (Dm.num(detonation_status))
        {
            // The nuke detonated on station
            case DETONATION_HIT_STATION:
                // And it was detonated by nuke ops
                if (Dm.truthy(length(get_antag_minds(typeof(datum_antagonist_nukeop)))))
                {
                    if (is_infiltrator_docked_at_syndiebase())
                        // And the ops escaped, they won!
                        return typeof(datum_cinematic_nuke_ops_victory);
                    else
                        // And the ops failed to escape, mutually assured destruction!
                        return typeof(datum_cinematic_nuke_mutual_destruction);
                }

                // It was detonated by something or someone what wasn't nuke ops,
                // throw it to the default explosion animation (self destruct)
                else
                    return base.get_cinematic_type(detonation_status);

            // The nuke detonated on station Z, but in space
            case DETONATION_NEAR_MISSED_STATION:
                return typeof(datum_cinematic_nuke_ops_miss);

            // The nuke detonated off station Z, and/or on the syndicate base
            case DETONATION_HIT_SYNDIE_BASE:
            case DETONATION_MISSED_STATION:
                return typeof(datum_cinematic_nuke_far_explosion);
        }

        stack_trace(Dm.text("[] - get_cinematic_type got a detonation_status it was not expecting. (Got: [])", type, detonation_status),
            file: "code/modules/antagonists/nukeop/equipment/nuclear_bomb/syndicate_nuke.dm", line: 29);
        return typeof(datum_cinematic_nuke_far_explosion);
    }
}
