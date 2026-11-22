// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/cone/_cone.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327, with fire breath): the cone spells, and the staggered cone that reaches each level after a delay.
// The var blocks are generated (D111).

/**
 * ## Cone spells
 *
 * Cone spells shoot off as a cone from the caster.
 */
// tg: code/modules/spells/spell_types/cone/_cone.dm:6
public partial class datum_action_cooldown_spell_cone : datum_action_cooldown_spell
{
    // tg: code/modules/spells/spell_types/cone/_cone.dm:12
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = await base.cast(cast_on);
        DmList cone_turfs = get_cone_turfs(get_turf(cast_on), cast_on.dir, cone_levels) as DmList;
        // SEND_SIGNAL(src, COMSIG_SPELL_CONE_ON_CAST, cone_turfs, cast_on) (rules 11.1 (e), a statement)
        if (Dm.truthy(_listen_lookup?[COMSIG_SPELL_CONE_ON_CAST]))
            _SendSignal(COMSIG_SPELL_CONE_ON_CAST, Dm.list(this, cone_turfs, cast_on));
        await make_cone(cone_turfs, cast_on);
        return dot;
    }

    // tg: code/modules/spells/spell_types/cone/_cone.dm:18
    public virtual async DmTask make_cone(DmList cone_turfs = null, atom caster = null)
    {
        foreach (DmList turf_list in Dm.each<DmList>(cone_turfs))
            await do_cone_effects(turf_list, caster);
    }

    /// This proc does obj, mob and turf cone effects on all targets in the passed list.
    // tg: code/modules/spells/spell_types/cone/_cone.dm:23
    public virtual async DmTask do_cone_effects(DmList target_turf_list = null, atom caster = null, object level = null)
    {
        level ??= 1;
        // SEND_SIGNAL(src, COMSIG_SPELL_CONE_ON_LAYER_EFFECT, target_turf_list, caster, level) (rules 11.1 (e), a statement)
        if (Dm.truthy(_listen_lookup?[COMSIG_SPELL_CONE_ON_LAYER_EFFECT]))
            _SendSignal(COMSIG_SPELL_CONE_ON_LAYER_EFFECT, Dm.list(this, target_turf_list, caster, level));
        foreach (turf target_turf in Dm.each_cast<turf>(target_turf_list))
        {
            if (QDELETED(target_turf)) //if turf is no longer there
                continue;

            await do_turf_cone_effect(target_turf, caster, level);
            if (!isopenturf(target_turf))
                continue;

            foreach (atom_movable movable_content in Dm.each_cast<atom_movable>(target_turf))
            {
                if (isobj(movable_content))
                    await do_obj_cone_effect(movable_content as obj, caster, level);
                else if (isliving(movable_content))
                    await do_mob_cone_effect(movable_content as mob_living, caster, level);
            }
        }
    }

    ///This proc deterimines how the spell will affect turfs.
    // tg: code/modules/spells/spell_types/cone/_cone.dm:40
    public virtual async DmTask do_turf_cone_effect(turf target_turf = null, atom caster = null, object level = null)
    {
        await DmTask.CompletedTask;
    }

    ///This proc deterimines how the spell will affect objects.
    // tg: code/modules/spells/spell_types/cone/_cone.dm:44
    public virtual async DmTask do_obj_cone_effect(obj target_obj = null, atom caster = null, object level = null)
    {
        await DmTask.CompletedTask;
    }

    ///This proc deterimines how the spell will affect mobs.
    // tg: code/modules/spells/spell_types/cone/_cone.dm:48
    public virtual async DmTask do_mob_cone_effect(mob_living target_mob = null, atom caster = null, object level = null)
    {
        await DmTask.CompletedTask;
    }

    ///This proc creates a list of turfs that are hit by the cone.
    // tg: code/modules/spells/spell_types/cone/_cone.dm:52
    public virtual object get_cone_turfs(turf starter_turf = null, object dir_to_use = null, object cone_levels = null)
    {
        cone_levels ??= 3;
        DmList turfs_to_return = Dm.list();
        turf turf_to_use = starter_turf;
        turf left_turf;
        turf right_turf;
        float right_dir = 0;
        float left_dir = 0;
        switch (Dm.num(dir_to_use))
        {
            case NORTH:
                left_dir = WEST;
                right_dir = EAST;
                break;
            case SOUTH:
                left_dir = EAST;
                right_dir = WEST;
                break;
            case EAST:
                left_dir = NORTH;
                right_dir = SOUTH;
                break;
            case WEST:
                left_dir = SOUTH;
                right_dir = NORTH;
                break;
        }

        // Go though every level of the cone levels and generate the cone.
        for (float level = 1; level <= Dm.num(cone_levels); level++)
        {
            DmList level_turfs = Dm.list();
            // Our center turf always exists, it's straight ahead of the caster.
            turf_to_use = get_step(turf_to_use, Dm.num(dir_to_use));
            Dm.append(ref level_turfs, turf_to_use);
            // Level 1 only ever has 1 turf, it's a cone.
            if (level != 1)
            {
                float level_width_in_each_direction = round((Dm.num(calculate_cone_shape(level)) - 1) / 2);
                left_turf = turf_to_use;
                right_turf = turf_to_use;
                // Check turfs to the left...
                for (float left_of_center = 1; left_of_center <= level_width_in_each_direction; left_of_center++)
                {
                    if (Dm.truthy(respect_density) && Dm.truthy(left_turf.density))
                        break;
                    left_turf = get_step(left_turf, left_dir);
                    Dm.append(ref level_turfs, left_turf);
                }
                // And turfs to the right.
                for (float right_of_enter = 1; right_of_enter <= level_width_in_each_direction; right_of_enter++)
                {
                    if (Dm.truthy(respect_density) && Dm.truthy(right_turf.density))
                        break;
                    right_turf = get_step(right_turf, right_dir);
                    Dm.append(ref level_turfs, right_turf);
                }
            }
            // Add the list of all turfs on this level to the turfs to return
            Dm.append(ref turfs_to_return, Dm.list(level_turfs));

            // If we're at the last level, we're done
            if (level == Dm.num(cone_levels))
                break;
            // But if we're not at the last level, we should check that we can keep going
            if (Dm.truthy(respect_density) && Dm.truthy(turf_to_use.density))
                break;
        }

        return turfs_to_return;
    }

    /**
     * Adjusts the width of the cone at the passed level.
     * This is never called on the first level of the cone (level 1 is always 1 width)
     *
     * Return a number - the TOTAL width of the cone at the passed level.
     */
    // tg: code/modules/spells/spell_types/cone/_cone.dm:114
    public virtual object calculate_cone_shape(object current_level = null)
    {
        // Default formula: (1 (innate) -> 3 -> 5 -> 5 -> 7 -> 7 -> 9 -> 9 -> ...)
        float level = Dm.num(current_level);
        return level + (level % 2) + 1;
    }
}

/**
 * ### Staggered Cone
 *
 * Staggered Cone spells will reach each cone level
 * gradually / with a delay, instead of affecting the entire
 * cone area at once.
 */
// tg: code/modules/spells/spell_types/cone/_cone.dm:125
public partial class datum_action_cooldown_spell_cone_staggered : datum_action_cooldown_spell_cone
{
    // tg: code/modules/spells/spell_types/cone/_cone.dm:130
    public override async DmTask make_cone(DmList cone_turfs = null, atom caster = null)
    {
        await DmTask.CompletedTask;
        float level_counter = 0;
        foreach (DmList turf_list in Dm.each<DmList>(cone_turfs))
        {
            level_counter++;
            _addtimer(Dm.@new<datum_callback>(this, nameof(do_cone_effects), turf_list, caster, level_counter), delay_between_level * level_counter, file: "code/modules/spells/spell_types/cone/_cone.dm", line: 134);
        }
    }
}
