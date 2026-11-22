// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/conjure/_conjure.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The conjure base; /limit_summons stays inert with its spells (section 113) (gaps package C6).

// tg: code/modules/spells/spell_types/conjure/_conjure.dm:2
public partial class datum_action_cooldown_spell_conjure : datum_action_cooldown_spell
{
    // tg: code/modules/spells/spell_types/conjure/_conjure.dm:22
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        return isturf(cast_on.loc) ? TRUE : FALSE;
    }

    // tg: code/modules/spells/spell_types/conjure/_conjure.dm:25
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = await base.cast(cast_on);
        if (Dm.truthy(create_summon_timer) && !Dm.truthy(await do_after(owner, Dm.num(create_summon_timer), cast_on.loc)))
        {
            owner?.balloon_alert(owner, "need to stay still!");
            return dot;
        }
        DmList to_summon_in = Dm.list();
        foreach (turf summon_turf in Dm.each<turf>(range(summon_radius, cast_on)))
        {
            if (Dm.truthy(summon_respects_density) && Dm.truthy(summon_turf.density))
                continue;
            Dm.append(ref to_summon_in, summon_turf);
        }

        for (int i = 1; i <= summon_amount; i++)
        {
            if (Dm.length(to_summon_in) == 0)
                break;

            Type summoned_object_type = pick(summon_type) as Type;
            turf spawn_place = pick(to_summon_in) as turf;
            if (Dm.truthy(summon_respects_prev_spawn_points))
                Dm.remove(ref to_summon_in, spawn_place);

            if (ispath(summoned_object_type, typeof(turf)))
            {
                if (isclosedturf(spawn_place))
                {
                    await spawn_place.ChangeTurf(summoned_object_type, flags: CHANGETURF_INHERIT_AIR);
                    return dot;
                }
                if (ispath(summoned_object_type, typeof(turf_closed)))
                {
                    if (Dm.truthy(spawn_place.overfloor_placed))
                        await spawn_place.ChangeTurf(summoned_object_type, flags: CHANGETURF_INHERIT_AIR);
                    else
                        await spawn_place.place_on_top(summoned_object_type, flags: CHANGETURF_INHERIT_AIR);
                    return dot;
                }
                await (spawn_place as turf_open).replace_floor(summoned_object_type, flags: CHANGETURF_INHERIT_AIR);
                return dot;
            }

            atom summoned_object = Dm.@new(summoned_object_type, spawn_place) as atom;

            summoned_object.flags_1 = Dm.num(Dm.bitor(summoned_object.flags_1, ADMIN_SPAWNED_1));
            ADD_TRAIT(summoned_object, TRAIT_SPAWNED_MOB, INNATE_TRAIT);
            if (summon_lifespan > 0)
                _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), summoned_object), summon_lifespan, file: "code/modules/spells/spell_types/conjure/_conjure.dm", line: 63); // QDEL_IN

            await post_summon(summoned_object, cast_on);
        }
        return dot;
    }

    /// Called on atoms summoned after they are created, allows extra variable editing and such of created objects
    // tg: code/modules/spells/spell_types/conjure/_conjure.dm:68
    public virtual async DmTask post_summon(atom summoned_object = null, atom cast_on = null)
    {
    }
}
