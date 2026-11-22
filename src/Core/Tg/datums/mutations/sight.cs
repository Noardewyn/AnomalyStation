// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/sight.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): near sightness, blindness, thermal vision and its eye damage, x-ray vision, laser eyes, illiteracy
// and night vision. The var blocks are generated (D111).

//Nearsightedness restricts your vision by several tiles.
// tg: code/datums/mutations/sight.dm:2
public partial class datum_mutation_nearsight : datum_mutation
{
    // tg: code/datums/mutations/sight.dm:9
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        await owner.become_nearsighted(GENETIC_MUTATION);
        return dot;
    }

    // tg: code/datums/mutations/sight.dm:15
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        owner.cure_nearsighted(GENETIC_MUTATION);
        return dot;
    }
}

///Blind makes you blind. Who knew?
// tg: code/datums/mutations/sight.dm:21
public partial class datum_mutation_blind : datum_mutation
{
    // tg: code/datums/mutations/sight.dm:28
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        await owner.become_blind(GENETIC_MUTATION);
        return dot;
    }

    // tg: code/datums/mutations/sight.dm:34
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        owner.cure_blind(GENETIC_MUTATION);
        return dot;
    }
}

///Thermal Vision lets you see mobs through walls
// tg: code/datums/mutations/sight.dm:40
public partial class datum_mutation_thermal : datum_mutation
{
    // tg: code/datums/mutations/sight.dm:54
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;

        // Something went wront and we still have the thermal vision from our power, no cheating.
        REMOVE_TRAIT(owner, TRAIT_THERMAL_VISION, GENETIC_MUTATION);
        return dot;
    }

    // tg: code/datums/mutations/sight.dm:61
    public override async DmTask<object> setup()
    {
        object dot = await base.setup();
        if (dot is not datum_action_cooldown_spell_thermal_vision to_modify) // null or invalid
            return dot;

        to_modify.eye_damage = Dm.initial(typeof(datum_action_cooldown_spell_thermal_vision), (datum_action_cooldown_spell_thermal_vision t) => t.eye_damage) * GET_MUTATION_SYNCHRONIZER(this) * GET_MUTATION_POWER(this);
        to_modify.thermal_duration = Dm.initial(typeof(datum_action_cooldown_spell_thermal_vision), (datum_action_cooldown_spell_thermal_vision t) => t.thermal_duration) * GET_MUTATION_POWER(this);
        return dot;
    }
}

// tg: code/datums/mutations/sight.dm:70
public partial class datum_action_cooldown_spell_thermal_vision : datum_action_cooldown_spell
{
    // tg: code/datums/mutations/sight.dm:84
    public override async DmTask Remove(mob remove_from = null)
    {
        REMOVE_TRAIT(remove_from, TRAIT_THERMAL_VISION, GENETIC_MUTATION);
        await base.Remove(remove_from);
    }

    // tg: code/datums/mutations/sight.dm:88
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        await DmTask.CompletedTask;
        return isliving(cast_on) && !HAS_TRAIT(cast_on, TRAIT_THERMAL_VISION) ? TRUE : FALSE;
    }

    // tg: code/datums/mutations/sight.dm:91
    public override async DmTask<object> cast(atom target = null)
    {
        mob_living cast_on = target as mob_living;
        object dot = await base.cast(target);
        ADD_TRAIT(cast_on, TRAIT_THERMAL_VISION, GENETIC_MUTATION);
        to_chat(cast_on, span_info(I18n.T("You focus your eyes intensely, as your vision becomes filled with heat signatures.")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(deactivate), cast_on), thermal_duration, file: "code/datums/mutations/sight.dm", line: 95);
        return dot;
    }

    // tg: code/datums/mutations/sight.dm:97
    public virtual async DmTask deactivate(mob_living cast_on = null)
    {
        if (QDELETED(cast_on) || !HAS_TRAIT_FROM(cast_on, TRAIT_THERMAL_VISION, GENETIC_MUTATION))
            return;

        REMOVE_TRAIT(cast_on, TRAIT_THERMAL_VISION, GENETIC_MUTATION);
        to_chat(cast_on, span_info(I18n.T("You blink a few times, your vision returning to normal as a dull pain settles in your eyes.")));

        if (iscarbon(cast_on))
        {
            mob_living_carbon carbon_cast_on = cast_on as mob_living_carbon;
            await carbon_cast_on.adjust_organ_loss(ORGAN_SLOT_EYES, eye_damage);
        }
    }
}

///X-ray Vision lets you see through walls.
// tg: code/datums/mutations/sight.dm:109
public partial class datum_mutation_xray : datum_mutation
{
    // tg: code/datums/mutations/sight.dm:116
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        ADD_TRAIT(owner, TRAIT_XRAY_VISION, GENETIC_MUTATION);
        return dot;
    }

    // tg: code/datums/mutations/sight.dm:122
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        REMOVE_TRAIT(owner, TRAIT_XRAY_VISION, GENETIC_MUTATION);
        return dot;
    }
}

///Laser Eyes lets you shoot lasers from your eyes!
// tg: code/datums/mutations/sight.dm:129
public partial class datum_mutation_laser_eyes : datum_mutation
{
    // tg: code/datums/mutations/sight.dm:140
    public override async DmTask<object> on_acquiring(mob_living_carbon_human H = null)
    {
        object dot = await base.on_acquiring(H);
        if (!Dm.truthy(dot))
            return dot;
        RegisterSignal(H, COMSIG_MOB_ATTACK_RANGED, nameof(on_ranged_attack));
        return dot;
    }

    // tg: code/datums/mutations/sight.dm:146
    public override async DmTask<object> on_losing(mob_living_carbon_human H = null)
    {
        object dot = await base.on_losing(H);
        if (Dm.truthy(dot))
            return dot;
        UnregisterSignal(H, COMSIG_MOB_ATTACK_RANGED);
        return dot;
    }

    ///Triggers on COMSIG_MOB_ATTACK_RANGED. Does the projectile shooting.
    // tg: code/datums/mutations/sight.dm:153
    [SignalHandler]
    public virtual void on_ranged_attack(mob_living_carbon_human source = null, atom target = null, object modifiers = null)
    {
        if (!Dm.truthy(source.combat_mode))
            return;
        to_chat(source, span_warning(I18n.T("You shoot with your laser eyes!")));
        source.changeNext_move(CLICK_CD_RANGE);
        Waiting.NoSleep(source.newtonian_move(get_angle(source, target)));
        obj_projectile_beam_laser_laser_eyes LE = Dm.@new<obj_projectile_beam_laser_laser_eyes>(source.loc);
        LE.firer = source;
        LE.def_zone = Dm.str(ran_zone(source.zone_selected));
        Waiting.NoSleep(LE.aim_projectile(target, source, modifiers as DmList));
        Waiting.Detach(LE.fire()); // INVOKE_ASYNC
        playsound(source, Dm.resource("sound/items/weapons/taser2.ogg"), 75, TRUE);
    }
}

// tg: code/datums/mutations/sight.dm:174
public partial class datum_mutation_illiterate : datum_mutation
{
    // tg: code/datums/mutations/sight.dm:182
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        ADD_TRAIT(owner, TRAIT_ILLITERATE, GENETIC_MUTATION);
        return dot;
    }

    // tg: code/datums/mutations/sight.dm:188
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        REMOVE_TRAIT(owner, TRAIT_ILLITERATE, GENETIC_MUTATION);
        return dot;
    }
}

// tg: code/datums/mutations/sight.dm:193
public partial class datum_mutation_night_vision : datum_mutation
{
    // tg: code/datums/mutations/sight.dm:201
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        ADD_TRAIT(owner, TRAIT_NIGHT_VISION, GENETIC_MUTATION);
        return dot;
    }

    // tg: code/datums/mutations/sight.dm:207
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        REMOVE_TRAIT(owner, TRAIT_NIGHT_VISION, GENETIC_MUTATION);
        return dot;
    }
}

