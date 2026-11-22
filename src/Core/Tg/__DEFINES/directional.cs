// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/directional.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the constants (TEXT_NORTH, TEXT_SOUTH, TEXT_EAST, TEXT_WEST) are generated; the function-like macros
// are GlobalProcs members (rules 11.1). MAPPING_DIRECTIONAL_HELPERS and MAPPING_DIAGONAL_HELPERS (:32-70) declare
// mapping subtypes of the path they are given: they are no member, each use is translated as the type blocks it expands
// to, in the mirror file of the use.

public static partial class GlobalProcs
{
    ///Returns true if the dir is diagonal, false otherwise
    // tg: code/__DEFINES/directional.dm:18  #define ISDIAGONALDIR(d) (d&(d-1))
    public static float ISDIAGONALDIR(float d) => Dm.bitand(d, d - 1);

    ///True if the dir is north or south, false therwise
    // tg: code/__DEFINES/directional.dm:20  #define NSCOMPONENT(d)   (d&(NORTH|SOUTH))
    public static float NSCOMPONENT(float d) => Dm.bitand(d, Dm.bitor(NORTH, SOUTH));

    ///True if the dir is east/west, false otherwise
    // tg: code/__DEFINES/directional.dm:22  #define EWCOMPONENT(d)   (d&(EAST|WEST))
    public static float EWCOMPONENT(float d) => Dm.bitand(d, Dm.bitor(EAST, WEST));

    ///Flips the dir for north/south directions
    // tg: code/__DEFINES/directional.dm:24  #define NSDIRFLIP(d)     (d^(NORTH|SOUTH))
    public static float NSDIRFLIP(float d) => Dm.bitxor(d, Dm.bitor(NORTH, SOUTH));

    ///Flips the dir for east/west directions
    // tg: code/__DEFINES/directional.dm:26  #define EWDIRFLIP(d)     (d^(EAST|WEST))
    public static float EWDIRFLIP(float d) => Dm.bitxor(d, Dm.bitor(EAST, WEST));

    /// Inverse direction, taking into account UP|DOWN if necessary.
    // tg: code/__DEFINES/directional.dm:29  #define REVERSE_DIR(dir) ( ((dir & 85) << 1) | ((dir & 170) >> 1) )
    public static float REVERSE_DIR(float dir) => Dm.bitor(Dm.shl(Dm.bitand(dir, 85), 1), Dm.shr(Dm.bitand(dir, 170), 1));
}
