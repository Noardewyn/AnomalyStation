// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/crafting/tailoring.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 8 (crafting): the file's procs. The recipes themselves are var blocks (generated, D111).

// tg: code/datums/components/crafting/tailoring.dm:610
public partial class datum_crafting_recipe_balloon_helmet : datum_crafting_recipe
{
    // tg: code/datums/components/crafting/tailoring.dm:618
    public override object check_requirements(mob user = null, DmList collected_requirements = null)
    {
        return HAS_TRAIT(user, TRAIT_BALLOON_SUTRA) ? TRUE : FALSE;
    }
}

// tg: code/datums/components/crafting/tailoring.dm:621
public partial class datum_crafting_recipe_balloon_tophat : datum_crafting_recipe
{
    // tg: code/datums/components/crafting/tailoring.dm:629
    public override object check_requirements(mob user = null, DmList collected_requirements = null)
    {
        return HAS_TRAIT(user, TRAIT_BALLOON_SUTRA) ? TRUE : FALSE;
    }
}

// tg: code/datums/components/crafting/tailoring.dm:632
public partial class datum_crafting_recipe_balloon_vest : datum_crafting_recipe
{
    // tg: code/datums/components/crafting/tailoring.dm:640
    public override object check_requirements(mob user = null, DmList collected_requirements = null)
    {
        return HAS_TRAIT(user, TRAIT_BALLOON_SUTRA) ? TRUE : FALSE;
    }
}
