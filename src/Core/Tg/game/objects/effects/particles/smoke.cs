// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/particles/smoke.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed with its module (a later milestone). Holds the [DmPath] of a type that an M2 slot signature or
// var block names, in the earliest .dme file that declares it (rules 2.3); written by the M2 contracts step
// (docs/ai/specs/m2-ownership.md). Only the type is declared.

// tg: code/game/objects/effects/particles/smoke.dm:2
[DmPath("/particles/smoke")]
public partial class particles_smoke : particles
{
}

// tg: code/game/objects/effects/particles/smoke.dm:18
[DmPath("/particles/smoke/burning")]
public partial class particles_smoke_burning : particles_smoke
{
}

// tg: code/game/objects/effects/particles/smoke.dm:21
[DmPath("/particles/smoke/burning/small")]
public partial class particles_smoke_burning_small : particles_smoke_burning
{
}
