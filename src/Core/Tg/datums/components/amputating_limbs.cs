// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/amputating_limbs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// This component will intercept bare-handed attacks by the owner on sufficiently injured carbons and amputate random limbs instead
// tg: code/datums/components/amputating_limbs.dm:2
public partial class datum_component_amputating_limbs : datum_component
{
    // tg: code/datums/components/amputating_limbs.dm:16
    [DmParams("surgery_time", "surgery_verb", "minimum_stat", "snip_chance", "target_zones", "pre_hit_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        await base.Initialize(__extra);
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        DmList target_zones = Dm.arg(__extra, 4) as DmList ?? Dm.list(BODY_ZONE_L_ARM, BODY_ZONE_L_LEG, BODY_ZONE_R_ARM, BODY_ZONE_R_LEG);
        if (Dm.length(target_zones) == 0)
            throw Dm.CRASH(Dm.text("[] for [] was not provided a valid list of body zones to target.", this, parent), "code/datums/components/amputating_limbs.dm", 28);

        this.surgery_time = Dm.arg(__extra, 0) ?? 5 * SECONDS;
        this.surgery_verb = Dm.arg(__extra, 1) ?? "prying";
        this.minimum_stat = Dm.arg(__extra, 2) ?? SOFT_CRIT;
        this.snip_chance = Dm.arg(__extra, 3) ?? 100f;
        this.target_zones = target_zones;
        this.pre_hit_callback = Dm.arg(__extra, 5) as datum_callback;
        return null;
    }

    // tg: code/datums/components/amputating_limbs.dm:37
    public override async DmTask RegisterWithParent()
    {
        RegisterSignals(parent, Dm.list(COMSIG_LIVING_UNARMED_ATTACK, COMSIG_HOSTILE_PRE_ATTACKINGTARGET), nameof(try_amputate));
    }

    // tg: code/datums/components/amputating_limbs.dm:40
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_LIVING_UNARMED_ATTACK, COMSIG_HOSTILE_PRE_ATTACKINGTARGET));
    }

    /// Called when you click on literally anything with your hands, see if it is an injured carbon and then try to cut it up
    // tg: code/datums/components/amputating_limbs.dm:44
    [SignalHandler]
    public virtual object try_amputate(mob_living surgeon = null, atom victim = null, object proximity = null, DmList modifiers = null)
    {
        if (!Dm.truthy(proximity) || !iscarbon(victim) || HAS_TRAIT(victim, TRAIT_NODISMEMBER) || !prob(Dm.num(snip_chance)))
            return null;

        mob_living_carbon limbed_victim = victim as mob_living_carbon;
        if (limbed_victim.stat < Dm.num(minimum_stat))
            return null;

        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(surgeon, victim)))
        {
            surgeon.balloon_alert(surgeon, "already busy!");
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        if (pre_hit_callback != null && !Dm.truthy(Waiting.NoSleep(pre_hit_callback.Invoke(victim))))
            return null;

        DmList valid_targets = Dm.list();
        foreach (obj_item_bodypart possible_target in Dm.each_cast<obj_item_bodypart>(limbed_victim.get_bodyparts()))
        {
            if (Dm.truthy(Dm.bitand(possible_target.bodypart_flags, BODYPART_UNREMOVABLE)))
                continue;
            if (!Dm.@in(possible_target.body_zone, target_zones))
                continue;
            Dm.append(ref valid_targets, possible_target);
        }

        if (Dm.length(valid_targets) == 0)
            return null;

        Waiting.Detach(amputate(surgeon, limbed_victim, pick(valid_targets) as obj_item_bodypart)); // INVOKE_ASYNC
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /// Chop one off
    // tg: code/datums/components/amputating_limbs.dm:75
    public virtual async DmTask amputate(mob_living surgeon = null, mob_living_carbon victim = null, obj_item_bodypart to_remove = null)
    {
        if (Dm.num(surgery_time) > 0)
            surgeon.visible_message(span_warning(Dm.text("[] is [] the [] off of []!", surgeon, surgery_verb, to_remove, victim)));
        if (Dm.num(surgery_time) > 0 && !Dm.truthy(await do_after(surgeon, Dm.num(surgery_time), victim)))
            return;
        await to_remove.dismember();
    }
}
