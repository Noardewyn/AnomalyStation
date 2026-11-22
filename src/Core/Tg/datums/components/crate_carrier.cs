// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/crate_carrier.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// Component for simplemobs and basicmobs that allow them to carry crates.
// tg: code/datums/components/crate_carrier.dm:4
public partial class datum_component_crate_carrier : datum_component
{
    // tg: code/datums/components/crate_carrier.dm:13
    [DmParams("crate_limit", "carriable_types")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        await base.Initialize(__extra);
        if (!isanimal_or_basicmob(parent))
            return COMPONENT_INCOMPATIBLE;

        this.crate_limit = Dm.num(Dm.arg(__extra, 0) ?? 3f);

        DmList carriable_types = Dm.arg(__extra, 1) as DmList;
        // tg's var/static default_cache is only a cache of this value
        this.carriable_cache = typecacheof(carriable_types ?? Dm.list(typeof(obj_structure_closet_crate))) as DmList;
        return null;
    }

    // tg: code/datums/components/crate_carrier.dm:27
    public override object Destroy(float? force = null)
    {
        LAZYCLEARLIST(crates_in_hand);
        return base.Destroy(force);
    }

    // tg: code/datums/components/crate_carrier.dm:31
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_LIVING_UNARMED_ATTACK, nameof(on_unarm_attack));
        RegisterSignal(parent, COMSIG_LIVING_DEATH, nameof(on_death));
    }

    // tg: code/datums/components/crate_carrier.dm:36
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_LIVING_UNARMED_ATTACK, COMSIG_LIVING_DEATH, COMSIG_ATOM_EXAMINE));
    }

    /// Signal proc for COMSIG_ATOM_EXAMINE to show when we're carrying crates
    // tg: code/datums/components/crate_carrier.dm:40
    [SignalHandler]
    public virtual void on_examine(mob_living source = null, mob examiner = null, DmList examine_list = null)
    {
        float num_crates = Dm.length(crates_in_hand);
        if (num_crates > 0)
            Dm.append(ref examine_list, span_notice(Dm.text("[] carrying [].", source.p_Theyre(), num_crates == 1 ? "a crate" : Dm.text("[] crates", num_crates))));
    }

    /// Signal proc for COMSIG_LIVING_UNARMED_ATTACK to allow mobs to pick up or drop crates
    // tg: code/datums/components/crate_carrier.dm:48
    [SignalHandler]
    public virtual object on_unarm_attack(mob_living source = null, atom target = null, object proximity = null, DmList modifiers = null)
    {
        if (Dm.truthy(source.combat_mode))
            return null;

        if (Dm.truthy(is_type_in_typecache(target, carriable_cache)))
        {
            atom_movable movable_target = target as atom_movable;
            if (Dm.length(crates_in_hand) >= crate_limit)
            {
                source.balloon_alert(source, "too many crates!");
                return COMPONENT_CANCEL_ATTACK_CHAIN;
            }

            foreach (mob_living inside_mob in Dm.each<mob_living>(movable_target.get_all_contents()))
            {
                if (inside_mob.mob_size < MOB_SIZE_HUMAN)
                    continue;
                source.balloon_alert(source, "crate too heavy!");
                return COMPONENT_CANCEL_ATTACK_CHAIN;
            }

            LAZYADD(ref crates_in_hand, target);
            Waiting.NoSleep(movable_target.forceMove(source));
            source.balloon_alert(source, "grabbed crate");
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        if (isopenturf(target) && Dm.length(crates_in_hand) > 0)
        {
            drop_all_crates(target);
            source.balloon_alert(source, "dropped crate");
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }
        return null;
    }

    /// Signal proc for COMSIG_LIVING_DEATH, so we drop crates on death or gib
    // tg: code/datums/components/crate_carrier.dm:75
    [SignalHandler]
    public virtual void on_death(mob_living source = null, object gibbed = null)
    {
        drop_all_crates(source.drop_location() as atom);
    }

    /// Drops all the crates in our crate list.
    // tg: code/datums/components/crate_carrier.dm:81
    public virtual void drop_all_crates(atom drop_to = null)
    {
        foreach (obj_structure_closet_crate held_crate in Dm.each_cast<obj_structure_closet_crate>(crates_in_hand))
        {
            Waiting.NoSleep(held_crate.forceMove(drop_to));
            LAZYREMOVE(ref crates_in_hand, held_crate);
        }
    }
}
