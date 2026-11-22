// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/decals/cleanable/robots.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M6 slice 5 (gibbing): the whole file (the robot debris a robot's gibbing leaves, the motor oil decals). The var blocks are
// generated (D111). The oil's own look of a decal (it never dries, set_up_blood) is the oil blood type's, which comes with the
// robotic species (blood_types.cs).

// tg: code/game/objects/effects/decals/cleanable/robots.dm:1
public partial class obj_effect_decal_cleanable_blood_gibs_robot_debris : obj_effect_decal_cleanable_blood_gibs
{
    // tg: code/game/objects/effects/decals/cleanable/robots.dm:10
    public override object get_default_blood_type()
    {
        return get_blood_type(typeof(datum_blood_type_oil));
    }

    // tg: code/game/objects/effects/decals/cleanable/robots.dm:13
    public override void create_splatter()
    {
        if (prob(40))
            base.create_splatter();
    }

    // tg: code/game/objects/effects/decals/cleanable/robots.dm:17
    [SignalHandler]
    public override void spread_movement_effects(datum_move_loop source = null)
    {
        if (Dm.truthy(NeverShouldHaveComeHere(loc as turf)))
            return;
        if (prob(40))
            Dm.@new<obj_effect_decal_cleanable_blood_splatter>(loc, null, GET_ATOM_BLOOD_DNA(this));
        else if (prob(10))
            do_sparks(3, TRUE, this);
    }

    // Doesn't have overlay support as of now
    // tg: code/game/objects/effects/decals/cleanable/robots.dm:26
    public override async DmTask<object> update_blood_color()
    {
        color = null;
        return null;
    }
}

// tg: code/game/objects/effects/decals/cleanable/robots.dm:42
public partial class obj_effect_decal_cleanable_blood_oil : obj_effect_decal_cleanable_blood
{
    // tg: code/game/objects/effects/decals/cleanable/robots.dm:48
    public override object get_default_blood_type()
    {
        return get_blood_type(typeof(datum_blood_type_oil));
    }
}

// tg: code/game/objects/effects/decals/cleanable/robots.dm:51
public partial class obj_effect_decal_cleanable_blood_oil_slippery : obj_effect_decal_cleanable_blood_oil
{
    // tg: code/game/objects/effects/decals/cleanable/robots.dm:51
    [DmParams("mapload", "diseases", "blood_or_dna")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_slippery), 80, Dm.bitor(NO_SLIP_WHEN_WALKING, SLIDE)));
        return dot;
    }
}

// tg: code/game/objects/effects/decals/cleanable/robots.dm:55
public partial class obj_effect_decal_cleanable_blood_splatter_oil : obj_effect_decal_cleanable_blood_splatter
{
    // tg: code/game/objects/effects/decals/cleanable/robots.dm:58
    public override object get_default_blood_type()
    {
        return get_blood_type(typeof(datum_blood_type_oil));
    }
}
