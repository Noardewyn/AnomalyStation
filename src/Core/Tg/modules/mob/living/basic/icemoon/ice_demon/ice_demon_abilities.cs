// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/icemoon/ice_demon/ice_demon_abilities.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// The ice blast projectile is a var block (generated).

// tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon_abilities.dm:10
public partial class datum_action_cooldown_mob_cooldown_ice_demon_teleport : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon_abilities.dm:19
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        if (isclosedturf(get_turf(target_atom)))
        {
            owner.balloon_alert(owner, "blocked!");
            return FALSE;
        }
        animate(owner, Dm.named("transform", Dm.matrix().Scale(0.8f)), Dm.named("time", time_delay), Dm.named("easing", SINE_EASING));
        _addtimer(Dm.@new<datum_callback>(this, nameof(teleport_to_turf), target_atom), time_delay, file: "code/modules/mob/living/basic/icemoon/ice_demon/ice_demon_abilities.dm", line: 24);
        StartCooldown();
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon_abilities.dm:28
    public virtual async DmTask teleport_to_turf(atom target = null)
    {
        animate(owner, Dm.named("transform", Dm.matrix()), Dm.named("time", 0.5f * SECONDS), Dm.named("easing", SINE_EASING));
        await do_teleport(teleatom: owner, destination: target, channel: TELEPORT_CHANNEL_BLUESPACE, forced: TRUE);
    }
}

// tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon_abilities.dm:32
public partial class datum_action_cooldown_mob_cooldown_slippery_ice_floors : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon_abilities.dm:44
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        for (float i = 0; i <= Dm.num(radius); i++)
        {
            DmList list_of_turfs = border_diamond_range_turfs(owner, i);
            _addtimer(Dm.@new<datum_callback>(this, nameof(spawn_icy_floors), list_of_turfs), i * spread_duration, file: "code/modules/mob/living/basic/icemoon/ice_demon/ice_demon_abilities.dm", line: 48);
        }
        StartCooldown();
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon_abilities.dm:51
    public virtual void spawn_icy_floors(DmList list_of_turfs = null)
    {
        if (Dm.length(list_of_turfs) == 0)
            return;
        foreach (turf location in Dm.each<turf>(list_of_turfs))
        {
            if (location == null)
                continue;
            if (isclosedturf(location) || isspaceturf(location))
                continue;
            Dm.@new<obj_effect_temp_visual_slippery_ice>(location);
        }
    }
}

// tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon_abilities.dm:61
public partial class obj_effect_temp_visual_slippery_ice : obj_effect_temp_visual
{
    // Kept as tg has it: the phase-in period is passed to the callback, not to addtimer, so the ice turns slippery a tick
    // after it appears rather than after it phased in (ice_demon_abilities.dm:77).
    // tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon_abilities.dm:73
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        animate(this, Dm.named("alpha", 160), Dm.named("time", phase_in_period));
        animate(Dm.named("alpha", 0), Dm.named("time", duration - phase_in_period)); /// slowly fade out of existence
        _addtimer(Dm.@new<datum_callback>(this, nameof(add_slippery_component), phase_in_period), file: "code/modules/mob/living/basic/icemoon/ice_demon/ice_demon_abilities.dm", line: 77); //only become slippery after we phased in
        return dot;
    }

    // tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon_abilities.dm:79
    public virtual void add_slippery_component()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_slippery), 2 * SECONDS));
    }
}

// tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon_abilities.dm:82
public partial class datum_action_cooldown_spell_conjure_limit_summons_create_afterimages : datum_action_cooldown_spell_conjure_limit_summons
{
    // tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon_abilities.dm:94
    public override async DmTask post_summon(atom summoned_object = null, atom cast_on = null)
    {
        await base.post_summon(summoned_object, cast_on);
        await summoned_object.AddComponent(typeof(datum_component_joint_damage), Dm.named("overlord_mob", owner));
    }
}
