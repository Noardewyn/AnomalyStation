// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/atmospherics/atmos_piping.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the function-like macros of the pipes' pictures (M3 I1b, D123); the constants are generated
// (Generated/Defines/__DEFINES/atmospherics/atmos_piping.g.cs).

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/atmospherics/atmos_piping.dm:16  #define CARDINAL_TO_FULLPIPES(cardinals) (cardinals)
    public static float CARDINAL_TO_FULLPIPES(float cardinals) => cardinals;

    // tg: code/__DEFINES/atmospherics/atmos_piping.dm:17  #define CARDINAL_TO_SHORTPIPES(cardinals) ((cardinals) << 4)
    public static float CARDINAL_TO_SHORTPIPES(float cardinals) => Dm.shl(cardinals, 4);

    // tg: code/__DEFINES/atmospherics/atmos_piping.dm:18  #define CARDINAL_TO_PIPECAPS(cardinals) ((cardinals) << 8)
    public static float CARDINAL_TO_PIPECAPS(float cardinals) => Dm.shl(cardinals, 8);

    // A pipe is a stub if it only has zero or one permitted direction. For a regular pipe this is nonsensical, and there are no pipe sprites for this, so it is not allowed.
    // tg: code/__DEFINES/atmospherics/atmos_piping.dm:20  #define ISSTUB(bits) !((bits) & ((bits) - 1))
    public static bool ISSTUB(float bits) => !Dm.truthy(Dm.bitand(bits, bits - 1));

    // tg: code/__DEFINES/atmospherics/atmos_piping.dm:21  #define ISNOTSTUB(bits) ((bits) & ((bits) - 1))
    public static float ISNOTSTUB(float bits) => Dm.bitand(bits, bits - 1);

    // tg: code/__DEFINES/atmospherics/atmos_piping.dm:117  #define SELECT_ATMOS_NODE_COLOR(our_node, other_node)
    public static string SELECT_ATMOS_NODE_COLOR(obj_machinery_atmospherics our_node, obj_machinery_atmospherics other_node) =>
        (other_node == null || Dm.truthy(Dm.bitand(other_node.pipe_flags, PIPING_DONT_SHARE_COLOR)) || Dm.eq(other_node.pipe_color, ATMOS_COLOR_OMNI))
            ? our_node.pipe_color
            : other_node.pipe_color;
}
