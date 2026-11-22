// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/pinata.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2b, D316: the pinatas drop their candy and explosives through it). The var block is generated (D111),
// except death_drop below.

///Objects or mobs with this component will drop items when taking damage.
// tg: code/datums/components/pinata.dm:2
public partial class datum_component_pinata : datum_component
{
    // tg: code/datums/components/pinata.dm:10  GAP(types): the generator types it by name, and the CTF player component's death_drop
    // (ctf_player_component.dm:12) is a path, but this one is the count of items dropped on death (pinatas.dm:25 passes destruction_loot,
    // a number), so the var is hand-written
    public object death_drop;

    // tg: code/datums/components/pinata.dm:12
    [DmParams("minimum_damage", "drop_chance", "candy", "death_drop")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object minimum_damage = Dm.arg(__extra, 0) ?? 10;
        object drop_chance = Dm.arg(__extra, 1) ?? 40;
        object candy = Dm.arg(__extra, 2) ?? Dm.list(typeof(obj_item_food_candy), typeof(obj_item_food_lollipop_cyborg), typeof(obj_item_food_gumball), typeof(obj_item_food_bubblegum), typeof(obj_item_food_chocolatebar));
        object death_drop = Dm.arg(__extra, 3) ?? 5;

        this.minimum_damage = minimum_damage;
        this.drop_chance = drop_chance;
        this.candy = candy;
        this.death_drop = death_drop;

        if (ismob(parent))
        {
            RegisterSignal(parent, COMSIG_MOB_APPLY_DAMAGE, nameof(damage_inflicted));
            RegisterSignal(parent, COMSIG_LIVING_DEATH, nameof(pinata_broken));
            return null;
        }
        if (isobj(parent))
        {
            obj parent_integrity_test = (obj)parent;
            if (Dm.truthy(parent_integrity_test.uses_integrity))
            {
                RegisterSignal(parent, COMSIG_ATOM_TAKE_DAMAGE, nameof(damage_inflicted));
                RegisterSignal(parent, COMSIG_ATOM_DESTRUCTION, nameof(pinata_broken));
                return null;
            }
        }
        return COMPONENT_INCOMPATIBLE;
    }

    // tg: code/datums/components/pinata.dm:36
    [SignalHandler]
    public virtual void damage_inflicted(atom target = null, object damage = null, object damage_type = null, params object[] __extra)
    {
        if (Dm.num(damage) < Dm.num(minimum_damage) || Dm.eq(damage_type, STAMINA) || Dm.eq(damage_type, OXY))
            return;
        if (!prob(Dm.num(drop_chance) + Dm.num(damage))) //Higher damage means less rolls but higher odds on the roll
            return;
        DmList turf_options = (DmList)get_adjacent_open_turfs((atom)parent);
        Dm.append(ref turf_options, get_turf(parent));
        if (Dm.truthy(Dm.length(turf_options)))
        {
            object dropped_item = pick(candy);
            Dm.@new((Type)dropped_item, pick(turf_options));
        }
    }

    // tg: code/datums/components/pinata.dm:48
    [SignalHandler]
    public virtual void pinata_broken(params object[] __extra)
    {
        for (float i = 1; i <= Dm.num(death_drop); i++)
        {
            object dropped_item = pick(candy);
            Dm.@new((Type)dropped_item, get_turf(parent));
        }
        qdel(this);
    }

    // tg: code/datums/components/pinata.dm:55
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_MOB_APPLY_DAMAGE,
            COMSIG_LIVING_DEATH,
            COMSIG_ATOM_TAKE_DAMAGE,
            COMSIG_ATOM_DESTRUCTION));
        return base.Destroy(force);
    }
}
