// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/medical.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps SC2, D333): the status list macros of the medical records. The constants of this file are generated (Defines);
// the defines converter does not emit list macros, so each is a method here that builds a new list at every use, as the macro's
// expansion does (the precedent of __DEFINES/access.cs).

public static partial class GlobalProcs
{
    /// List of available physical statuses
    // tg: code/__DEFINES/medical.dm:8  #define PHYSICAL_STATUSES list( ...)
    public static DmList PHYSICAL_STATUSES() => Dm.list(PHYSICAL_ACTIVE, PHYSICAL_DEBILITATED, PHYSICAL_UNCONSCIOUS, PHYSICAL_DECEASED);

    /// List of available mental statuses
    // tg: code/__DEFINES/medical.dm:22  #define MENTAL_STATUSES list( ...)
    public static DmList MENTAL_STATUSES() => Dm.list(MENTAL_STABLE, MENTAL_WATCH, MENTAL_UNSTABLE, MENTAL_INSANE);
}
