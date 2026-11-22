// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/components/npc_friendly.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the glitch antagonists' friendship with the domains' NPC factions. The var block is
// generated (D111).

/// Makes a mob friendly with most NPC factions
// tg: code/modules/bitrunning/components/npc_friendly.dm:2
public partial class datum_component_npc_friendly : datum_component
{
    /// The list of factions to add to the player
    // tg: code/modules/bitrunning/components/npc_friendly.dm:4  var/static/list/npc_factions (rules 10.6)
    private static DmList npc_factions => TgState.Current.datum_component_npc_friendly__npc_factions;

    // tg: code/modules/bitrunning/components/npc_friendly.dm:20
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object dot = await base.Initialize(__extra);

        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        mob_living player = parent as mob_living;

        previous_factions = player.get_faction() as DmList;
        player.add_faction(npc_factions);
        return dot;
    }

    // tg: code/modules/bitrunning/components/npc_friendly.dm:31
    public override object Destroy(float? force = null)
    {
        mob_living player = parent as mob_living;
        if (!QDELETED(parent))
            player.set_faction(previous_factions);
        return base.Destroy(force);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/bitrunning/components/npc_friendly.dm:4
    internal DmList datum_component_npc_friendly__npc_factions = Dm.list(
        FACTION_BOSS,
        FACTION_CARP,
        FACTION_HIVEBOT,
        FACTION_HOSTILE,
        FACTION_MIMIC,
        FACTION_PIRATE,
        FACTION_SPIDER,
        FACTION_STICKMAN,
        ROLE_ALIEN,
        ROLE_GLITCH,
        ROLE_SYNDICATE);
}
