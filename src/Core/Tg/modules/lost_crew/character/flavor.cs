// Translated from tgstation (snapshot 1d99bd7f): code/modules/lost_crew/character/flavor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1: the recovered crew). The var blocks are generated (D111). The quirk wait for the owner (D333): the
// station class's prosthetic limb flavour is left out of the roll while its quirk is inert (DV561, damages/_damages.cs).

/// Apply some flavor to the character to make it seem more 'alive'
// tg: code/modules/lost_crew/character/flavor.dm:2
public partial class datum_corpse_flavor : datum
{
    // tg: code/modules/lost_crew/character/flavor.dm:4
    public virtual async DmTask apply_flavor(mob_living_carbon_human working_dead = null, DmList job_gear = null, DmList on_revive_and_player_occupancy = null)
    {
    }
}

/// Applies a quirk when selected
// tg: code/modules/lost_crew/character/flavor.dm:7
public partial class datum_corpse_flavor_quirk : datum_corpse_flavor
{
    // tg: code/modules/lost_crew/character/flavor.dm:10
    public override async DmTask apply_flavor(mob_living_carbon_human working_dead = null, DmList job_gear = null, DmList on_revive_and_player_occupancy = null)
    {
        await working_dead.add_quirk(quirk, announce: FALSE);
    }
}
