// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/under/jobs/security.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed with its module (a later milestone). Holds the [DmPath] of a type that M2 F1's translated defaults
// name (the job outfit's slots, the cloth that repairs clothing, the prisoner suit get_random_jumpsuit leaves out), in the
// earliest .dme file that declares it (rules 2.3). Only the type is declared.

// tg: code/modules/clothing/under/jobs/security.dm:237
[DmPath("/obj/item/clothing/under/rank/prisoner")]
public partial class obj_item_clothing_under_rank_prisoner : obj_item_clothing_under_rank
{
}

// tg: code/modules/clothing/under/jobs/security.dm:259
[DmPath("/obj/item/clothing/under/rank/prisoner/skirt")]
public partial class obj_item_clothing_under_rank_prisoner_skirt : obj_item_clothing_under_rank_prisoner
{
}
