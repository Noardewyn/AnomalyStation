// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/squashable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

///This component allows something to be when crossed, for example for cockroaches.
// tg: code/datums/components/squashable.dm:2
public partial class datum_component_squashable : datum_component
{
    private static ref DmList loc_connections => ref TgState.Current.datum_component_squashable__loc_connections;

    // tg: code/datums/components/squashable.dm:15
    [DmParams("squash_chance", "squash_damage", "squash_flags", "squash_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object dot = await base.Initialize(__extra);
        object squash_chance = Dm.arg(__extra, 0);
        object squash_damage = Dm.arg(__extra, 1);
        object squash_flags = Dm.arg(__extra, 2);
        object squash_callback = Dm.arg(__extra, 3);
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        if (Dm.truthy(squash_chance))
            this.squash_chance = Dm.num(squash_chance);
        if (Dm.truthy(squash_damage))
            this.squash_damage = Dm.num(squash_damage);
        if (Dm.truthy(squash_flags))
            this.squash_flags = Dm.num(squash_flags);
        if (this.on_squash_callback == null && Dm.truthy(squash_callback))
            on_squash_callback = Dm.@new<datum_callback>(parent, squash_callback);

        await AddComponent(typeof(datum_component_connect_loc_behalf), parent, loc_connections);
        return dot;
    }

    // tg: code/datums/components/squashable.dm:33
    public override object Destroy(float? force = null)
    {
        on_squash_callback = null;
        return base.Destroy(force);
    }

    ///Handles the squashing of the mob
    // tg: code/datums/components/squashable.dm:38
    [SignalHandler]
    public virtual void on_entered(turf source_turf = null, atom_movable crossing_movable = null)
    {
        if (parent == crossing_movable)
            return;

        mob_living parent_as_living = parent as mob_living;
        if (Dm.truthy(Dm.bitand(squash_flags, SQUASHED_DONT_SQUASH_IN_CONTENTS)) && !isturf(parent_as_living.loc))
            return;

        if (Dm.truthy(Dm.bitand(squash_flags, SQUASHED_SHOULD_BE_DOWN)) && !Dm.eq(parent_as_living.body_position, LYING_DOWN))
            return;

        bool should_squash = (Dm.truthy(Dm.bitand(squash_flags, SQUASHED_ALWAYS_IF_DEAD)) && parent_as_living.stat == DEAD) || prob(squash_chance);

        if (should_squash && on_squash_callback != null)
        {
            if (Dm.truthy(Waiting.NoSleep(on_squash_callback.Invoke(parent_as_living, crossing_movable))))
                return; //Everything worked, we're done!
        }
        if (isliving(crossing_movable))
        {
            mob_living crossing_mob = crossing_movable as mob_living;
            if (Dm.num(crossing_mob.mob_size) > MOB_SIZE_SMALL && !Dm.truthy(Dm.bitand(crossing_mob.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
            {
                if (HAS_TRAIT(crossing_mob, TRAIT_PACIFISM))
                {
                    crossing_mob.visible_message(span_notice(Dm.text("[] carefully steps over [].", crossing_mob, parent_as_living)), span_notice(Dm.text("You carefully step over [] to avoid hurting it.", parent_as_living)));
                    return;
                }
                if (should_squash)
                {
                    crossing_mob.visible_message(span_notice(Dm.text("[] squashed [].", crossing_mob, parent_as_living)), span_notice(Dm.text("You squashed [].", parent_as_living)));
                    Squish(parent_as_living);
                }
                else
                    parent_as_living.visible_message(span_notice(Dm.text("[] avoids getting crushed.", parent_as_living)));
            }
        }
        else if (isstructure(crossing_movable))
        {
            if (should_squash)
            {
                crossing_movable.visible_message(span_notice(Dm.text("[] is crushed under [].", parent_as_living, crossing_movable)));
                Squish(parent_as_living);
            }
            else
                parent_as_living.visible_message(span_notice(Dm.text("[] avoids getting crushed.", parent_as_living)));
        }
    }

    // tg: code/datums/components/squashable.dm:75
    public virtual void Squish(mob_living target = null)
    {
        if (Dm.truthy(Dm.bitand(squash_flags, SQUASHED_SHOULD_BE_GIBBED)))
            Waiting.Detach(target.gib(DROP_ALL_REMAINS));
        else
            Waiting.NoSleep(target.adjust_brute_loss(squash_damage));
    }

    // tg: code/datums/components/squashable.dm:81
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        qdel(GetComponent(typeof(datum_component_connect_loc_behalf)));
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/squashable.dm:11
    internal DmList datum_component_squashable__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(datum_component_squashable.on_entered)));
}
