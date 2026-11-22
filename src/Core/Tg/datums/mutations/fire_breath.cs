// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/fire_breath.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): Fire breath, a staggered cone of fire that throws its breather back. The var blocks are generated (D111).

// tg: code/datums/mutations/fire_breath.dm:1
public partial class datum_mutation_firebreath : datum_mutation
{
    // tg: code/datums/mutations/fire_breath.dm:14
    public override async DmTask<object> setup()
    {
        object dot = await base.setup();
        if (dot is not datum_action_cooldown_spell_cone_staggered_fire_breath to_modify) // null or invalid
            return dot;

        if (GET_MUTATION_POWER(this) <= 1) // we only care about power from here on
        {
            to_modify.cone_levels = Dm.initial(to_modify, (datum_action_cooldown_spell_cone_staggered_fire_breath f) => f.cone_levels); //resets to default if no power chromosome
            to_modify.self_throw_range = Dm.initial(to_modify, (datum_action_cooldown_spell_cone_staggered_fire_breath f) => f.self_throw_range);
            return dot;
        }

        to_modify.cone_levels += 2; // Cone fwooshes further, and...
        to_modify.self_throw_range += 1; // the breath throws the user back more
        return dot;
    }
}

// tg: code/datums/mutations/fire_breath.dm:28
public partial class datum_action_cooldown_spell_cone_staggered_fire_breath : datum_action_cooldown_spell_cone_staggered
{
    // tg: code/datums/mutations/fire_breath.dm:45
    public override async DmTask<object> before_cast(atom cast_on = null)
    {
        object dot = await base.before_cast(cast_on);
        if (Dm.truthy(Dm.bitand(dot, SPELL_CANCEL_CAST)))
            return dot;

        if (!iscarbon(cast_on))
            return dot;

        mob_living_carbon our_lizard = cast_on as mob_living_carbon;
        if (!Dm.truthy(our_lizard.is_mouth_covered()))
            return dot;

        await our_lizard.adjust_fire_stacks(cone_levels);
        our_lizard.ignite_mob();
        to_chat(our_lizard, span_warning(I18n.T("Something in front of your mouth catches fire!")));
        return dot;
    }

    // tg: code/datums/mutations/fire_breath.dm:61
    public override async DmTask after_cast(atom cast_on = null)
    {
        await base.after_cast(cast_on);
        if (!isliving(cast_on))
            return;

        mob_living living_cast_on = cast_on as mob_living;
        // When casting, throw the caster backwards a few tiles.
        float original_dir = living_cast_on.dir;
        await living_cast_on.throw_at(
            get_edge_target_turf(living_cast_on, REVERSE_DIR(living_cast_on.dir)) as atom,
            range: self_throw_range,
            speed: 2,
            gentle: TRUE);
        // Try to set us to our original direction after, so we don't end up backwards.
        await living_cast_on.setDir(original_dir);
    }

    // tg: code/datums/mutations/fire_breath.dm:78
    public override object calculate_cone_shape(object current_level = null)
    {
        // This makes the cone shoot out into a 3 wide column of flames no matter the distance
        return 3;
    }

    // tg: code/datums/mutations/fire_breath.dm:82
    public override async DmTask do_turf_cone_effect(turf target_turf = null, atom caster = null, object level = null)
    {
        await DmTask.CompletedTask;
        // Further turfs experience less exposed_temperature and exposed_volume
        Dm.@new<obj_effect_hotspot>(target_turf); // for style
        target_turf.hotspot_expose(Dm.max(500, 900 - (100 * Dm.num(level))), Dm.max(50, 200 - (50 * Dm.num(level))), 1);
    }

    // tg: code/datums/mutations/fire_breath.dm:87
    public override async DmTask do_mob_cone_effect(mob_living target_mob = null, atom caster = null, object level = null)
    {
        // Further out targets take less immediate burn damage and get less fire stacks.
        // The actual burn damage application is not blocked by fireproofing, like space dragons.
        await target_mob.apply_damage(Dm.max(10, 40 - (5 * Dm.num(level))), BURN, spread_damage: TRUE);
        await target_mob.adjust_fire_stacks(Dm.max(2, 5 - Dm.num(level)));
        target_mob.ignite_mob();
    }

    // tg: code/datums/mutations/fire_breath.dm:94
    public override async DmTask do_obj_cone_effect(obj target_obj = null, atom caster = null, object level = null)
    {
        // Further out objects experience less exposed_temperature and exposed_volume
        await target_obj.fire_act(Dm.max(500, 900 - (100 * Dm.num(level))), Dm.max(50, 200 - (50 * Dm.num(level))));
    }
}
