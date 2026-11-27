// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/icemoon/polar_bear/polar_bear.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// The magic polar bear and the bear's controller are var blocks (generated).

// The taxonomy of polar vs space bear left to imagination of the reader
// tg: code/modules/mob/living/basic/icemoon/polar_bear/polar_bear.dm:2
public partial class mob_living_basic_mining_polarbear : mob_living_basic_mining
{
    // tg: code/modules/mob/living/basic/icemoon/polar_bear/polar_bear.dm:42
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        add_traits(Dm.list(TRAIT_SPACEWALK, TRAIT_SWIMMER, TRAIT_FENCE_CLIMBER, TRAIT_SNOWSTORM_IMMUNE), INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_BEAR, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f)); // inert until the cell lines (cytology)
        Waiting.NoSleep(AddElement(typeof(datum_element_change_force_on_death), Dm.named("move_force", MOVE_FORCE_DEFAULT), Dm.named("move_resist", MOVE_RESIST_DEFAULT), Dm.named("pull_force", PULL_FORCE_DEFAULT)));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), Dm.named("footstep_type", FOOTSTEP_MOB_CLAW)));
        return dot;
    }
}
