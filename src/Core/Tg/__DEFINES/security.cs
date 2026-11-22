// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/security.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps SC2, D333): the wanted status list macro of the security records. The constants of this file are generated
// (Defines); the defines converter does not emit list macros, so it is a method here that builds a new list at every use, as the
// macro's expansion does (the precedent of __DEFINES/access.cs).

public static partial class GlobalProcs
{
    /// List of available wanted statuses
    // tg: code/__DEFINES/security.dm:53  #define WANTED_STATUSES(...) list( ...)
    public static DmList WANTED_STATUSES() => Dm.list(WANTED_NONE, WANTED_SUSPECT, WANTED_ARREST, WANTED_PRISONER, WANTED_PAROLE, WANTED_DISCHARGED);
}
