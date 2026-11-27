// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/goliath/goliath_actions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Place some grappling tentacles underfoot
// tg: code/modules/mob/living/basic/lavaland/goliath/goliath_actions.dm:2
public partial class datum_action_cooldown_mob_cooldown_goliath_tentacles : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath_actions.dm:16
    public override async DmTask<object> PreActivate(atom target = null)
    {
        target = get_turf(target) as atom;
        if (Dm.num(get_dist(owner, target)) > max_range)
            return FALSE;
        return await base.PreActivate(target);
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath_actions.dm:22
    public override async DmTask<object> Activate(atom target = null)
    {
        Dm.@new<obj_effect_goliath_tentacle>(target, owner);
        foreach (object spawndir in Dm.each(GLOB.cardinals))
        {
            turf adjacent_target = get_step(target, Dm.num(spawndir)) as turf;
            if (isgroundlessturf(adjacent_target) && !islava(adjacent_target))
                continue;
            if (isopenturf(adjacent_target) || ismineralturf(adjacent_target))
                Dm.@new<obj_effect_goliath_tentacle>(adjacent_target);
        }

        if (isliving(target))
            owner.visible_message(span_warning(I18n.T("[] digs its tentacles under []!", owner, target)));
        StartCooldown();
        return TRUE;
    }
}

/// Place grappling tentacles around you to grab attackers
// tg: code/modules/mob/living/basic/lavaland/goliath/goliath_actions.dm:37
public partial class datum_action_cooldown_mob_cooldown_tentacle_burst : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath_actions.dm:49
    public override async DmTask<object> Activate(atom target = null)
    {
        DmList directions = GLOB.alldirs.Copy();
        foreach (object dir in Dm.each(directions))
        {
            turf adjacent_target = get_step(target, Dm.num(dir)) as turf;
            if (adjacent_target != null)
                Dm.@new<obj_effect_goliath_tentacle_drag>(adjacent_target, owner);
        }
        owner.visible_message(span_warning(I18n.T("[] unleashes tentacles from the ground around it!", owner)));
        StartCooldown();
        return TRUE;
    }
}

/// Summon a line of tentacles towards the target
// tg: code/modules/mob/living/basic/lavaland/goliath/goliath_actions.dm:60
public partial class datum_action_cooldown_mob_cooldown_tentacle_grasp : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath_actions.dm:72
    public override async DmTask<object> Activate(atom target = null)
    {
        Dm.@new<obj_effect_temp_visual_effect_trail_burrowed_tentacle>(owner.loc, target, owner);
        if (isliving(target))
            owner.visible_message(span_warning(I18n.T("[] reaches for [] with its tentacles!", owner, target)));
        StartCooldown();
        return TRUE;
    }
}

/// An invisible effect which chases a target, spawning tentacles every so often.
// tg: code/modules/mob/living/basic/lavaland/goliath/goliath_actions.dm:80
public partial class obj_effect_temp_visual_effect_trail_burrowed_tentacle : obj_effect_temp_visual_effect_trail
{
    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath_actions.dm:90
    [DmParams("mapload", "target", "owner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom target = Dm.arg<atom>(__extra, 0);
        mob_living owner = Dm.arg<mob_living>(__extra, 1);
        object dot = base.Initialize(mapload, Dm.forward(__extra, target, owner));
        this.owner = owner;
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath_actions.dm:94
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        owner = null;
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath_actions.dm:98
    public override async DmTask add_spawner()
    {
        await AddComponent(typeof(datum_component_spawner), Dm.named("spawn_types", Dm.list(spawned_effect)), Dm.named("max_spawned", max_spawned), Dm.named("spawn_time", spawn_interval), Dm.named("spawn_callback", Dm.@new<datum_callback>(this, nameof(on_tentacle_spawned))));
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath_actions.dm:101
    public virtual void on_tentacle_spawned(obj_effect_goliath_tentacle tentacle = null)
    {
        tentacle.set_owner(owner);
    }
}
