// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/decals/cleanable/aliens.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M6 slice 5 (gibbing): the whole file (the xeno gibs an alien's gibbing leaves, the acid pools). The var blocks are
// generated (D111). The acid's own look of a decal (it never dries, set_up_blood) is the xeno blood type's, which comes with
// the aliens (blood_types.cs).

// tg: code/game/objects/effects/decals/cleanable/aliens.dm:3
public partial class obj_effect_decal_cleanable_blood_xeno : obj_effect_decal_cleanable_blood
{
    // tg: code/game/objects/effects/decals/cleanable/aliens.dm:9
    public override object get_default_blood_type()
    {
        return get_blood_type(typeof(datum_blood_type_xeno));
    }
}

// tg: code/game/objects/effects/decals/cleanable/aliens.dm:12
public partial class obj_effect_decal_cleanable_blood_splatter_xeno : obj_effect_decal_cleanable_blood_splatter
{
    // tg: code/game/objects/effects/decals/cleanable/aliens.dm:16
    public override object get_default_blood_type()
    {
        return get_blood_type(typeof(datum_blood_type_xeno));
    }
}

// tg: code/game/objects/effects/decals/cleanable/aliens.dm:19
public partial class obj_effect_decal_cleanable_blood_gibs_xeno : obj_effect_decal_cleanable_blood_gibs
{
    // tg: code/game/objects/effects/decals/cleanable/aliens.dm:26
    public override object get_default_blood_type()
    {
        return get_blood_type(typeof(datum_blood_type_xeno));
    }
}

// tg: code/game/objects/effects/decals/cleanable/aliens.dm:61
public partial class obj_effect_decal_cleanable_blood_tracks_xeno : obj_effect_decal_cleanable_blood_tracks
{
    // tg: code/game/objects/effects/decals/cleanable/aliens.dm:64
    public override object get_default_blood_type()
    {
        return get_blood_type(typeof(datum_blood_type_xeno));
    }
}
