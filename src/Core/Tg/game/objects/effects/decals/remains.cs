// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/decals/remains.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI1c, D326: a dead silicon's dust leaves robot remains). The var blocks are generated (D111).

// tg: code/game/objects/effects/decals/remains.dm:1
public partial class obj_effect_decal_remains : obj_effect_decal
{
    // tg: code/game/objects/effects/decals/remains.dm:6
    public override async DmTask<object> acid_act(object acidpwr = null, object acid_volume = null, params object[] __extra)
    {
        visible_message(span_warning(I18n.T("[] dissolve[] into a puddle of sizzling goop!", this, gender == PLURAL ? "" : "s")));
        playsound(this, Dm.resource("sound/items/tools/welder.ogg"), 150, TRUE);
        Dm.@new<obj_effect_decal_cleanable_greenglow>(drop_location());
        qdel(this);
        return TRUE;
    }
}

// tg: code/game/objects/effects/decals/remains.dm:13
public partial class obj_effect_decal_remains_human : obj_effect_decal_remains
{
    // tg: code/game/objects/effects/decals/remains.dm:17
    public override object NeverShouldHaveComeHere(turf here_turf = null)
    {
        return !istype(here_turf, typeof(obj_structure_closet_crate_grave_filled)) && Dm.truthy(base.NeverShouldHaveComeHere(here_turf));
    }
}

// tg: code/game/objects/effects/decals/remains.dm:20
public partial class obj_effect_decal_remains_human_smokey : obj_effect_decal_remains_human
{
    ///The reagent we will release when our remains are disturbed.
    // tg: code/game/objects/effects/decals/remains.dm:26  (hand-declared: tg declares it /datum/reagent but stores the reagent's path)
    public object that_shit_that_killed_saddam;

    // tg: code/game/objects/effects/decals/remains.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        proximity_monitor = Dm.@new<datum_proximity_monitor>(this, 1);
        DmList blocked_reagents = Dm.add(subtypesof(typeof(datum_reagent_medicine)), subtypesof(typeof(datum_reagent_consumable))) as DmList; //Boooooriiiiing
        that_shit_that_killed_saddam = get_random_reagent_id(blacklist: blocked_reagents);
        return dot;
    }

    // tg: code/game/objects/effects/decals/remains.dm:39
    public override async DmTask<object> HasProximity(atom_movable tomb_raider = null)
    {
        if (!(gas_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm), inline
            return null;

        if (iscarbon(tomb_raider))
        {
            mob_living_carbon nearby_carbon = tomb_raider as mob_living_carbon;
            if (nearby_carbon.move_intent != MOVE_INTENT_WALK || prob(5))
            {
                await release_smoke(nearby_carbon);
                gas_cooldown = world.time + gas_cooldown_length; // COOLDOWN_START (__DEFINES/cooldowns.dm), inline
            }
        }
        return null;
    }

    ///Releases a cloud of smoke based on the randomly generated reagent in Initialize().
    // tg: code/game/objects/effects/decals/remains.dm:50
    public virtual async DmTask release_smoke(mob_living smoke_releaser = null)
    {
        visible_message(span_warning(I18n.T("[] disturbs [], which releases a huge cloud of gas!", smoke_releaser, this)));
        await do_chem_smoke(2, this, get_turf(this), that_shit_that_killed_saddam, 15);
    }
}

///Subtype of smokey remains used for rare maintenance spawns.
// tg: code/game/objects/effects/decals/remains.dm:55
public partial class obj_effect_decal_remains_human_smokey_maintenance : obj_effect_decal_remains_human_smokey
{
    // tg: code/game/objects/effects/decals/remains.dm:59
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        gas_cooldown_length = Dm.num(rand(4 * MINUTES, 6 * MINUTES));
        return dot;
    }
}

// tg: code/game/objects/effects/decals/remains.dm:63
public partial class obj_effect_decal_remains_plasma : obj_effect_decal_remains
{
    // tg: code/game/objects/effects/decals/remains.dm:66
    public override object NeverShouldHaveComeHere(turf here_turf = null)
    {
        return isclosedturf(here_turf);
    }
}
