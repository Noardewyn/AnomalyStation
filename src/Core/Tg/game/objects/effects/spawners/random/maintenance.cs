// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spawners/random/maintenance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 I1a: the maintenance loot spawners pick from GLOB.maintenance_loot (maintenance_loot.cs) with the world RNG (D31) and
// delete themselves (the spawner's INITIALIZE_HINT_QDEL, misc.dm:34). The station traits that fill or empty maintenance
// are read from SSstation, an inert stub until M10, so no trait is set and the loot count is the spawner's own. The
// examine line is translated (D291). The var blocks are generated.

// tg: code/game/objects/effects/spawners/random/maintenance.dm:4
public partial class obj_effect_spawner_random_maintenance : obj_effect_spawner_random
{
    // tg: code/game/objects/effects/spawners/random/maintenance.dm:1  #define MAINT_LOOT "maintenance_loot" (#undef at :135)
    private const string MAINT_LOOT = "maintenance_loot";

    // tg: code/game/objects/effects/spawners/random/maintenance.dm:2  #define DUMPSTER_LOOT "dumpster_loot" (#undef at :136)
    private const string DUMPSTER_LOOT = "dumpster_loot";

    // tg: code/game/objects/effects/spawners/random/maintenance.dm:22
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("This spawner has an effective loot count of [].", get_effective_lootcount())));
        return dot;
    }

    // tg: code/game/objects/effects/spawners/random/maintenance.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        switch (loot_table)
        {
            case MAINT_LOOT:
                loot = GLOB.maintenance_loot;
                break;
            case DUMPSTER_LOOT:
                loot = GLOB.dumpster_loot;
                break;
        }
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/effects/spawners/random/maintenance.dm:34
    public override void skew_loot_weights(DmList loot_list = null, object exponent = null)
    {
        ///We only need to skew the weights once, since it's a global list used by all maint spawners.
        if (Dm.truthy(skew_loot_weights__already_done))
        {
            switch (loot_table)
            {
                case MAINT_LOOT:
                    if (loot_list == GLOB.maintenance_loot)
                        return;
                    break;
                case DUMPSTER_LOOT:
                    if (loot_list == GLOB.dumpster_loot)
                        return;
                    break;
            }
        }

        skew_loot_weights__already_done = TRUE;
        base.skew_loot_weights(loot_list, exponent);
    }

    // tg: code/game/objects/effects/spawners/random/maintenance.dm:36  var/static/already_done = FALSE (a TgState field, rules 10.6)
    private static ref object skew_loot_weights__already_done => ref TgState.Current.obj_effect_spawner_random_maintenance__skew_loot_weights__already_done;

    // tg: code/game/objects/effects/spawners/random/maintenance.dm:49
    public virtual void hide()
    {
        SetInvisibility(INVISIBILITY_OBSERVER);
        alpha = 100;
    }

    // tg: code/game/objects/effects/spawners/random/maintenance.dm:53
    public virtual object get_effective_lootcount()
    {
        float effective_lootcount = spawn_loot_count;

        // SSstation is an inert stub until M10 (station traits), so it holds neither trait and the count stays the spawner's
        if (HAS_TRAIT(SSstation, STATION_TRAIT_FILLED_MAINT))
            effective_lootcount = FLOOR(spawn_loot_count * 1.5f, 1);

        else if (HAS_TRAIT(SSstation, STATION_TRAIT_EMPTY_MAINT))
            effective_lootcount = FLOOR(spawn_loot_count * 0.5f, 1);

        return effective_lootcount;
    }

    // tg: code/game/objects/effects/spawners/random/maintenance.dm:64
    public override async DmTask spawn_loot(object lootcount_override = null)
    {
        if (lootcount_override == null)
            lootcount_override = get_effective_lootcount();
        await base.spawn_loot(lootcount_override);

        // In addition, closets that are closed will have the maintenance loot inserted inside.
        foreach (obj_structure_closet closet in Dm.each<obj_structure_closet>(get_turf(this)))
        {
            if (!Dm.truthy(closet.opened))
                await closet.take_contents();
        }
    }
}

/// A subtype of maintenance loot spawner that does not spawn any decals, for when you want to place them on chasm turfs and such
/// decals such as ashes will cause NeverShouldHaveComeHere() to fail on such turfs, which creates annoying rng based CI failures
// tg: code/game/objects/effects/spawners/random/maintenance.dm:15
public partial class obj_effect_spawner_random_maintenance_no_decals : obj_effect_spawner_random_maintenance
{
    // tg: code/game/objects/effects/spawners/random/maintenance.dm:17
    public override object can_spawn(object loot = null)
    {
        if (ispath(loot, typeof(obj_effect_decal)))
            return FALSE;
        return base.can_spawn(loot);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/spawners/random/maintenance.dm:36  /obj/effect/spawner/random/maintenance/skew_loot_weights var/static/already_done
    internal object obj_effect_spawner_random_maintenance__skew_loot_weights__already_done = FALSE;
}
