// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/self/forcewall.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the chapel: the psyker's psychic wall stands on it): the forcewall spell, a wall of three forcefields across its
// caster's facing. The var blocks are generated (D111).

// tg: code/modules/spells/spell_types/self/forcewall.dm:1
public partial class datum_action_cooldown_spell_forcewall : datum_action_cooldown_spell
{
    // tg: code/modules/spells/spell_types/self/forcewall.dm:18
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = null;
        dot = await base.cast(cast_on);
        foreach (turf cast_turf in Dm.each_cast<turf>(get_turfs()))
            await spawn_wall(cast_turf);
        return dot;
    }

    /// This proc returns all the turfs on which we will spawn the walls.
    // tg: code/modules/spells/spell_types/self/forcewall.dm:24
    public virtual object get_turfs()
    {
        return Dm.list(get_turf(owner), get_step(owner, turn(owner.dir, 90)), get_step(owner, turn(owner.dir, 270)));
    }

    /// This proc spawns a wall on the given turf.
    // tg: code/modules/spells/spell_types/self/forcewall.dm:28
    public virtual async DmTask spawn_wall(turf cast_turf = null)
    {
        await Dm.new_async(wall_type, cast_turf, owner, antimagic_flags);
    }
}
