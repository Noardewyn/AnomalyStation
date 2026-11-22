// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/see_through_maps.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// PARTIAL: only the map names (the #defines) translated, which the flora's get_seethrough_map returns (gaps ST1); the
// see_through_maps coordinate lists are read only by the see-through component (datums/components/seethrough.dm), which the
// first-person view does not add (DV499).
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    //For these defines, check also above for their actual shapes in-game and maybe get a better idea

    ///Default shape. It's one tile above the atom
    // tg: code/__HELPERS/see_through_maps.dm:4  #define SEE_THROUGH_MAP_DEFAULT "default"
    public const string SEE_THROUGH_MAP_DEFAULT = "default";
    ///A 3x3 area 2 tiles above the atom (trees love to be this shape)
    // tg: code/__HELPERS/see_through_maps.dm:6  #define SEE_THROUGH_MAP_THREE_X_THREE "3x3"
    public const string SEE_THROUGH_MAP_THREE_X_THREE = "3x3";
    ///2 tiles above the atom
    // tg: code/__HELPERS/see_through_maps.dm:8  #define SEE_THROUGH_MAP_DEFAULT_TWO_TALL "default_two_tall"
    public const string SEE_THROUGH_MAP_DEFAULT_TWO_TALL = "default_two_tall";
    ///two rows of three tiles above the atom (small but thick trees love these)
    // tg: code/__HELPERS/see_through_maps.dm:10  #define SEE_THROUGH_MAP_THREE_X_TWO "3x2"
    public const string SEE_THROUGH_MAP_THREE_X_TWO = "3x2";
    ///One row of three tiles above the atom, but offset one tile to the left because of how billboards work
    // tg: code/__HELPERS/see_through_maps.dm:12  #define SEE_THROUGH_MAP_BILLBOARD "billboard"
    public const string SEE_THROUGH_MAP_BILLBOARD = "billboard";
    ///Two rows of three wide, but offset one tile to the left because thats how shipping containers work
    // tg: code/__HELPERS/see_through_maps.dm:14  #define SEE_THROUGH_MAP_SHIPPING_CONTAINER "shipping_container"
    public const string SEE_THROUGH_MAP_SHIPPING_CONTAINER = "shipping_container";
    ///Seethrough component for the ratvar wreck, in shape of the ratvar wreck
    // tg: code/__HELPERS/see_through_maps.dm:16  #define SEE_THROUGH_MAP_RATVAR_WRECK "ratvar"
    public const string SEE_THROUGH_MAP_RATVAR_WRECK = "ratvar";
}
