// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/radiation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 radiation slice: the whole file. The var block is generated (D111). The investigate log of an irradiation is a deferred
// leaf (the investigate logs, M10).

// tg: code/controllers/subsystem/radiation.dm:1 SUBSYSTEM_DEF(radiation) (the var block is generated)
public partial class datum_controller_subsystem_radiation : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(radiation) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSradiation) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/radiation.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSradiation != this)
        {
            if (SSradiation is datum_controller_subsystem_radiation)
            {
                Recover();
                qdel(SSradiation);
            }
            SSradiation = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/radiation.dm:11
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        while (Dm.truthy(processing.len))
        {
            datum_radiation_pulse_information pulse_information = processing[1] as datum_radiation_pulse_information;

            datum_weakref source_ref = pulse_information.source_ref;
            atom source = source_ref.resolve() as atom;
            if (source == null)
            {
                processing.Cut(1, 2);
                continue;
            }

            pulse(source, pulse_information);

            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;

            processing.Cut(1, 2);
        }
    }

    // tg: code/controllers/subsystem/radiation.dm:28
    public override object stat_entry(object msg = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/radiation.dm:28", "stat_entry: stat panels are not translated (translation-rules.md 13)");
        return null;
    }

    // tg: code/controllers/subsystem/radiation.dm:32
    public virtual void pulse(atom source = null, datum_radiation_pulse_information pulse_information = null)
    {
        DmList cached_rad_insulations = Dm.list();
        DmList cached_turfs_to_process = pulse_information.turfs_to_process;
        float turfs_iterated = 0;
        float threshold = Dm.num(pulse_information.threshold);
        foreach (turf turf_to_irradiate in Dm.each_cast<turf>(cached_turfs_to_process))
        {
            turfs_iterated += 1;
            foreach (atom_movable target in Dm.each<atom_movable>(turf_to_irradiate))
            {
                if (!Dm.truthy(can_irradiate_basic(target)))
                    continue;

                float current_insulation = 1;

                foreach (turf turf_in_between in Dm.each<turf>(Dm.sub(get_line(source, target), get_turf(source))))
                {
                    object insulation = cached_rad_insulations[turf_in_between];
                    if (insulation == null)
                    {
                        // a null rad_insulation (tg's platforms) multiplies as 0, as DM's null does
                        float product = Dm.num(turf_in_between.rad_insulation);
                        foreach (atom on_turf in Dm.each_cast<atom>(turf_in_between.contents))
                            product *= Dm.num(on_turf.rad_insulation);
                        insulation = product;
                        cached_rad_insulations[turf_in_between] = insulation;
                    }

                    current_insulation *= Dm.num(insulation);

                    if (current_insulation <= threshold)
                        break;
                }

                if (Dm.truthy(target._listen_lookup?[COMSIG_IN_RANGE_OF_IRRADIATION]))
                    target._SendSignal(COMSIG_IN_RANGE_OF_IRRADIATION, Dm.list(target, pulse_information, current_insulation));

                // Check a second time, because of TRAIT_BYPASS_EARLY_IRRADIATED_CHECK
                if (HAS_TRAIT(target, TRAIT_IRRADIATED))
                    continue;

                if (current_insulation <= threshold)
                    continue;

                /// Perceived chance of target getting irradiated.
                float perceived_chance;
                /// Intensity variable which will describe the radiation pulse.
                /// It is used by perceived intensity, which diminishes over range. The chance of the target getting irradiated is determined by perceived_intensity.
                /// Intensity is calculated so that the chance of getting irradiated at half of the max range is the same as the chance parameter.
                float intensity;
                /// Diminishes over range. Used by perceived chance, which is the actual chance to get irradiated.
                float perceived_intensity;

                if (Dm.num(pulse_information.chance) < 100) // Prevents log(0) runtime if chance is 100%
                {
                    intensity = -log(1 - Dm.num(pulse_information.chance) / 100) * Dm.pow(1 + Dm.num(pulse_information.max_range) / 2, 2);
                    perceived_intensity = intensity * INVERSE(Dm.pow(1 + Dm.num(get_dist_euclidean(source, target)), 2)); // Diminishes over range.
                    perceived_intensity *= (current_insulation - threshold) * INVERSE(1 - threshold); // Perceived intensity decreases as objects that absorb radiation block its trajectory.
                    perceived_chance = 100 * (1 - Dm.pow(NUM_E, -perceived_intensity));
                }
                else
                    perceived_chance = 100;

                object irradiation_result = !Dm.truthy(target._listen_lookup?[COMSIG_IN_THRESHOLD_OF_IRRADIATION]) ? (object)NONE : target._SendSignal(COMSIG_IN_THRESHOLD_OF_IRRADIATION, Dm.list(target, pulse_information));
                if (Dm.truthy(Dm.bitand(irradiation_result, CANCEL_IRRADIATION)))
                    continue;

                if (Dm.truthy(pulse_information.minimum_exposure_time) && !Dm.truthy(Dm.bitand(irradiation_result, SKIP_MINIMUM_EXPOSURE_TIME_CHECK)))
                {
                    Waiting.NoSleep(target.AddComponent(typeof(datum_component_radiation_countdown), pulse_information.minimum_exposure_time));
                    continue;
                }

                if (!prob(perceived_chance))
                    continue;

                if (Dm.truthy(irradiate_after_basic_checks(target)))
                {
                    target.investigate_log(Dm.text("was irradiated by [].", source), INVESTIGATE_RADIATION);
                }
            }

            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                break;
        }

        cached_turfs_to_process.Cut(1, turfs_iterated + 1);
    }

    /// Will attempt to irradiate the given target, limited through IC means, such as radiation protected clothing.
    // tg: code/controllers/subsystem/radiation.dm:103
    public virtual object irradiate(atom target = null)
    {
        if (!Dm.truthy(can_irradiate_basic(target)))
            return FALSE;

        irradiate_after_basic_checks(target);
        return TRUE;
    }

    // tg: code/controllers/subsystem/radiation.dm:110
    private object irradiate_after_basic_checks(atom target = null)
    {
        // PRIVATE_PROC(TRUE)

        if (ishuman(target) && Dm.truthy(wearing_rad_protected_clothing((mob_living_carbon_human)target)))
            return FALSE;

        Waiting.NoSleep(target.AddComponent(typeof(datum_component_irradiated)));
        return TRUE;
    }

    /// Returns whether or not the target can be irradiated by any means.
    /// Does not check for clothing.
    // tg: code/controllers/subsystem/radiation.dm:121
    public virtual object can_irradiate_basic(atom target = null)
    {
        if (!CAN_IRRADIATE(target))
            return FALSE;

        if (HAS_TRAIT(target, TRAIT_IRRADIATED) && !HAS_TRAIT(target, TRAIT_BYPASS_EARLY_IRRADIATED_CHECK))
            return FALSE;

        if (HAS_TRAIT(target, TRAIT_RADIMMUNE))
            return FALSE;

        return TRUE;
    }

    /// Returns whether or not the human is covered head to toe in rad-protected clothing.
    // tg: code/controllers/subsystem/radiation.dm:134
    public virtual object wearing_rad_protected_clothing(mob_living_carbon_human human = null)
    {
        foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(human.get_bodyparts()))
        {
            float @protected = FALSE;

            foreach (obj_item clothing in Dm.each_cast<obj_item>(human.get_clothing_on_part(limb)))
            {
                if (HAS_TRAIT(clothing, TRAIT_RADIATION_PROTECTED_CLOTHING))
                {
                    @protected = TRUE;
                    break;
                }
            }

            if (!Dm.truthy(@protected))
                return FALSE;
        }

        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/radiation.dm:1  SUBSYSTEM_DEF(radiation): GLOBAL_REAL(SSradiation, /datum/controller/subsystem/radiation)
    internal datum_controller_subsystem_radiation SSradiation;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_radiation SSradiation => ref TgState.Current.SSradiation;
}
