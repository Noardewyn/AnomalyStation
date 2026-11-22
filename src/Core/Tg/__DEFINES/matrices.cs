// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/matrices.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only COLOR_MATRIX_GRAYSCALE translated (the M7 supermatter slice: a dusted mob greys out, death.dm:135) and
// COLOR_MATRIX_INVERT (gaps P2: the anti-cannabis); the rest
// of matrices.dm comes with its users.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/matrices.dm:7  #define COLOR_MATRIX_INVERT list(...) (a new list at each use)
    public static DmList COLOR_MATRIX_INVERT() => Dm.list(-1, 0, 0, 0, 0, -1, 0, 0, 0, 0, -1, 0, 0, 0, 0, 1, 1, 1, 1, 0);

    ///Grayscale
    // tg: code/__DEFINES/matrices.dm:11  #define COLOR_MATRIX_GRAYSCALE list(...) (a new list at each use)
    public static DmList COLOR_MATRIX_GRAYSCALE() => Dm.list(0.33f, 0.33f, 0.33f, 0, 0.59f, 0.59f, 0.59f, 0, 0.11f, 0.11f, 0.11f, 0, 0, 0, 0, 1, 0, 0, 0, 0);
}
