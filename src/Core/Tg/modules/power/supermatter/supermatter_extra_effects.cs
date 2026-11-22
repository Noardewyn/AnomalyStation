// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/supermatter/supermatter_extra_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 supermatter slice (G5, D163): the whole file: the radiation pulses, the hum and accents, the psychologist's soothing
// (psy_coeff, the hallucination pulse: M8 slice G), the high-energy zaps, the anomalies and the pull.

// tg: code/modules/power/supermatter/supermatter_extra_effects.dm:20
public partial class obj_machinery_power_supermatter_crystal : obj_machinery_power
{
    // Any power past this number will be clamped down
    // tg: code/modules/power/supermatter/supermatter_extra_effects.dm:2  (file-local, rules 11.4)
    private const float MAX_ACCEPTED_POWER_OUTPUT = 5000;

    // At the highest power output, assuming no integrity changes, the threshold will be 0.
    // tg: code/modules/power/supermatter/supermatter_extra_effects.dm:5
    private const float THRESHOLD_EQUATION_SLOPE = (-1 / MAX_ACCEPTED_POWER_OUTPUT);

    // The higher this number, the faster low integrity will drop threshold
    // I would've named this "power", but y'know. :P
    // tg: code/modules/power/supermatter/supermatter_extra_effects.dm:9
    private const float INTEGRITY_EXPONENTIAL_DEGREE = 2;

    // At INTEGRITY_MIN_NUDGABLE_AMOUNT, the power will be treated as, at most, INTEGRITY_MAX_POWER_NUDGE.
    // Any lower integrity will result in INTEGRITY_MAX_POWER_NUDGE.
    // tg: code/modules/power/supermatter/supermatter_extra_effects.dm:13-14
    private const float INTEGRITY_MAX_POWER_NUDGE = 1500;
    private const float INTEGRITY_MIN_NUDGABLE_AMOUNT = 0.7f;

    // tg: code/modules/power/supermatter/supermatter_extra_effects.dm:16-18
    private const float RADIATION_CHANCE_AT_FULL_INTEGRITY = 0.03f;
    private const float RADIATION_CHANCE_AT_ZERO_INTEGRITY = 0.4f;
    private const float CHANCE_EQUATION_SLOPE = (RADIATION_CHANCE_AT_ZERO_INTEGRITY - RADIATION_CHANCE_AT_FULL_INTEGRITY);

    // tg: code/modules/power/supermatter/supermatter_extra_effects.dm:20
    public virtual void emit_radiation()
    {
        // As power goes up, rads go up.
        // A standard N2 SM seems to produce a value of around 1,500.
        float power_factor = min(internal_energy, MAX_ACCEPTED_POWER_OUTPUT);

        float integrity = 1 - CLAMP01(Dm.num(damage) / explosion_point);

        // When integrity goes down, the threshold (from an observable point of view, rads) go up.
        // However, the power factor must go up as well, otherwise turning off the emitters
        // on a delaminating SM would stop radiation from escaping.
        // To fix this, lower integrities raise the power factor to a minimum.
        float integrity_power_nudge = LERP(INTEGRITY_MAX_POWER_NUDGE, 0, CLAMP01((integrity - INTEGRITY_MIN_NUDGABLE_AMOUNT) / (1 - INTEGRITY_MIN_NUDGABLE_AMOUNT)));

        power_factor = max(power_factor, integrity_power_nudge);

        // At the "normal" N2 power output (with max integrity), this is 0.7, which is enough to be stopped
        // by the walls or the radation shutters.
        // As integrity does down, rads go up.
        float threshold = 0;
        switch (integrity)
        {
            case 0:
                threshold = Dm.truthy(power_factor) ? 0 : 1;
                break;
            case 1:
                threshold = (THRESHOLD_EQUATION_SLOPE * power_factor + 1);
                break;
            default:
                threshold = Dm.pow((THRESHOLD_EQUATION_SLOPE * power_factor + 1), Dm.pow((1 / integrity), INTEGRITY_EXPONENTIAL_DEGREE));
                break;
        }

        // Calculating chance is done entirely on integrity, so that actively delaminating SMs feel more dangerous
        float chance = (CHANCE_EQUATION_SLOPE * (1 - integrity)) + RADIATION_CHANCE_AT_FULL_INTEGRITY;

        radiation_pulse(
            this,
            max_range: 8,
            threshold: threshold,
            chance: chance * 100
        );
    }

    // tg: code/modules/power/supermatter/supermatter_extra_effects.dm:57
    public virtual void processing_sound()
    {
        if (Dm.truthy(internal_energy))
            soundloop.volume = clamp((50 + (internal_energy / 50)), 50, 100);
        if (Dm.num(damage) >= 300)
            soundloop.mid_sounds = Dm.list(Dm.kv(Dm.resource("sound/machines/sm/loops/delamming.ogg"), 1));
        else
            soundloop.mid_sounds = Dm.list(Dm.kv(Dm.resource("sound/machines/sm/loops/calm.ogg"), 1));

        //We play delam/neutral sounds at a rate determined by power and damage
        if (last_accent_sound >= world.time || !prob(20))
            return;
        float aggression = min(((Dm.num(damage) / 800) * (internal_energy / 2500)), 1.0f) * 100;
        if (Dm.num(damage) >= 300)
            playsound(this, SFX_SM_DELAM, max(50, aggression), FALSE, 40, 30, falloff_distance: 10);
        else
            playsound(this, SFX_SM_CALM, max(50, aggression), FALSE, 25, 25, falloff_distance: 10);
        float next_sound = round((100 - aggression) * 5);
        last_accent_sound = world.time + max(SUPERMATTER_ACCENT_SOUND_MIN_COOLDOWN, next_sound);
    }

    // tg: code/modules/power/supermatter/supermatter_extra_effects.dm:76
    public virtual async DmTask psychological_examination()
    {
        // Defaults to a value less than 1. Over time the psy_coeff goes to 0 if
        // no supermatter soothers are nearby.
        float psy_coeff_diff = -0.05f;
        foreach (mob_living_carbon_human seen_by_sm in Dm.each<mob_living_carbon_human>(view(this, SM_HALLUCINATION_RANGE(internal_energy))))
        {
            // Someone (generally a Psychologist), when looking at the SM within hallucination range makes it easier to manage.
            if (HAS_MIND_TRAIT(seen_by_sm, TRAIT_SUPERMATTER_SOOTHER))
                psy_coeff_diff = 0.05f;
        }
        await visible_hallucination_pulse(
            center: this,
            radius: SM_HALLUCINATION_RANGE(internal_energy),
            hallucination_duration: internal_energy * hallucination_power,
            hallucination_max_duration: 400 * SECONDS);
        psy_coeff = clamp(psy_coeff + psy_coeff_diff, 0, 1);
    }

    // tg: code/modules/power/supermatter/supermatter_extra_effects.dm:92
    public virtual async DmTask handle_high_power()
    {
        if (internal_energy <= POWER_PENALTY_THRESHOLD && Dm.num(damage) <= danger_point) //If the power is above 5000 or if the damage is above 550
        {
            last_high_energy_accumulation_perspective_machines = SSmachines.times_fired; //Prevent oddly high initial zap due to high energy zaps not getting triggered via too low energy.
            return;
        }
        float range = 4;
        zap_cutoff = 1500;
        float total_moles = Dm.num(absorbed_gasmix.total_moles());
        float pressure = Dm.num(absorbed_gasmix.return_pressure());
        float temp = absorbed_gasmix.temperature;
        if (pressure > 0 && temp > 0)
        {
            //You may be able to freeze the zapstate of the engine with good planning, we'll see
            zap_cutoff = clamp(1.2e6f - (internal_energy * total_moles * 40) / temp, 1.4e5f, 1.2e6f);//If the core is cold, it's easier to jump, ditto if there are a lot of mols
            //We should always be able to zap our way out of the default enclosure
            //See supermatter_zap() for more details
            range = clamp(internal_energy / pressure * 10, 2, 7);
        }
        float flags = ZAP_SUPERMATTER_FLAGS;
        float zap_count = 0;
        //Deal with power zaps
        float sw__1 = internal_energy;
        if (sw__1 >= POWER_PENALTY_THRESHOLD && sw__1 <= SEVERE_POWER_PENALTY_THRESHOLD)
        {
            zap_icon = DEFAULT_ZAP_ICON_STATE;
            zap_count = 2;
        }
        else if (sw__1 >= SEVERE_POWER_PENALTY_THRESHOLD && sw__1 <= CRITICAL_POWER_PENALTY_THRESHOLD)
        {
            zap_icon = SLIGHTLY_CHARGED_ZAP_ICON_STATE;
            //Uncaps the zap damage, it's maxed by the input power
            //Objects take damage now
            Dm.combine(ref flags, (Dm.bitor(ZAP_MOB_DAMAGE, ZAP_OBJ_DAMAGE)));
            zap_count = 3;
        }
        else if (sw__1 >= CRITICAL_POWER_PENALTY_THRESHOLD && sw__1 <= INFINITY)
        {
            zap_icon = OVER_9000_ZAP_ICON_STATE;
            //It'll stun more now, and damage will hit harder, gloves are no garentee.
            //Machines go boom
            Dm.combine(ref flags, (Dm.bitor(Dm.bitor(Dm.bitor(ZAP_MOB_STUN, ZAP_MACHINE_EXPLOSIVE), ZAP_MOB_DAMAGE), ZAP_OBJ_DAMAGE)));
            zap_count = 4;
        }
        //Now we deal with damage shit
        if (Dm.num(damage) > danger_point && prob(20))
            zap_count += 1;

        if (zap_count >= 1)
        {
            playsound(loc as atom, Dm.resource("sound/items/weapons/emitter2.ogg"), 100, TRUE, extrarange: 10);
            float delta_time = (SSmachines.times_fired - last_high_energy_accumulation_perspective_machines) * SSmachines.wait / (1 * SECONDS);
            object accumulated_energy = accumulate_energy(ZAP_ENERGY_ACCUMULATION_HIGH_ENERGY, energy: clamp(internal_energy * 3200, 6.4e6f, 3.2e7f) * delta_time);
            if (Dm.truthy(accumulated_energy))
            {
                for (float i = 1, i__end = zap_count; i <= i__end; i++)
                {
                    object discharged_energy = discharge_energy(ZAP_ENERGY_ACCUMULATION_HIGH_ENERGY, portion: 1 - Dm.pow((1 - ZAP_ENERGY_DISCHARGE_PORTION), INVERSE(zap_count)));
                    await supermatter_zap(this, range: range, zap_str: Dm.num(discharged_energy), zap_flags: flags, zap_cutoff: this.zap_cutoff, power_level: internal_energy, zap_icon: this.zap_icon);
                }
            }
            last_high_energy_accumulation_perspective_machines = SSmachines.times_fired;
        }
        if (prob(5))
            supermatter_anomaly_gen(get_ranged_target_turf(this, pick(GLOB.cardinals), rand(5, 10)) as turf, FLUX_ANOMALY, 3);
        if (prob(5))
            supermatter_anomaly_gen(get_ranged_target_turf(this, pick(GLOB.cardinals), rand(5, 10)) as turf, HALLUCINATION_ANOMALY, 3);
        if (internal_energy > SEVERE_POWER_PENALTY_THRESHOLD && prob(5) || prob(1))
            supermatter_anomaly_gen(get_ranged_target_turf(this, pick(GLOB.cardinals), rand(5, 10)) as turf, GRAVITATIONAL_ANOMALY, 3);
        if ((internal_energy > SEVERE_POWER_PENALTY_THRESHOLD && prob(2)) || (prob(0.3f) && internal_energy > POWER_PENALTY_THRESHOLD))
            supermatter_anomaly_gen(get_ranged_target_turf(this, pick(GLOB.cardinals), rand(5, 10)) as turf, PYRO_ANOMALY, 3);
    }

    // tg: code/modules/power/supermatter/supermatter_extra_effects.dm:148
    public virtual async DmTask supermatter_pull(turf center = null, float? pull_range = null)
    {
        pull_range ??= 3;
        // SHIM-GAP: world.view ("15x15" in tg's world.dm) reads in BYOND as the number 7 (dm_view_read)
        playsound(center, Dm.resource("sound/items/weapons/marauder.ogg"), 100, TRUE, extrarange: Dm.num(pull_range) - Dm.num(dm_view_read(world.view)));
        foreach (atom_movable movable_atom in Dm.each<atom_movable>(orange(pull_range, center)))
        {
            if ((Dm.truthy(movable_atom.anchored) || movable_atom.move_resist >= MOVE_FORCE_EXTREMELY_STRONG)) //move resist memes.
            {
                if (movable_atom is obj_structure_closet)
                {
                    obj_structure_closet closet = (obj_structure_closet)movable_atom;
                    await closet.open(force: TRUE);
                }
                continue;
            }
            if (ismob(movable_atom))
            {
                mob pulled_mob = (mob)movable_atom;
                if (Dm.truthy(pulled_mob.mob_negates_gravity()))
                    continue; //You can't pull someone nailed to the deck
            }
            await LocomotionSeam.PullOrStep(movable_atom, center); // DEVIATION(DV357)
        }
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/power/supermatter/supermatter_extra_effects.dm:162  (anomalycenter is object, not tg's turf: the delamination's
    // effect_anomaly passes the event spawn landmarks, delamination_effects.dm:68, :76; rules 3.5's "-" cell, Appendix A)
    public static void supermatter_anomaly_gen(object anomalycenter = null, string type = null, float? anomalyrange = null, float? has_changed_lifespan = null)
    {
        type ??= FLUX_ANOMALY;
        anomalyrange ??= 5;
        has_changed_lifespan ??= TRUE;
        turf local_turf = pick(RANGE_TURFS(anomalyrange.Value, (atom)anomalycenter)) as turf;
        if (!Dm.truthy(local_turf))
            return;
        switch (type)
        {
            case FLUX_ANOMALY:
            {
                float explosive = Dm.truthy(has_changed_lifespan) ? FLUX_NO_EMP : FLUX_LIGHT_EMP;
                Dm.@new<obj_effect_anomaly_flux>(local_turf, Dm.truthy(has_changed_lifespan) ? rand(25 * SECONDS, 35 * SECONDS) : null, FALSE, explosive);
                break;
            }
            case GRAVITATIONAL_ANOMALY:
                Dm.@new<obj_effect_anomaly_grav>(local_turf, Dm.truthy(has_changed_lifespan) ? rand(20 * SECONDS, 30 * SECONDS) : null, FALSE);
                break;
            case PYRO_ANOMALY:
                Dm.@new<obj_effect_anomaly_pyro>(local_turf, Dm.truthy(has_changed_lifespan) ? rand(15 * SECONDS, 25 * SECONDS) : null, FALSE);
                break;
            case HALLUCINATION_ANOMALY:
                Dm.@new<obj_effect_anomaly_hallucination_supermatter>(local_turf, Dm.truthy(has_changed_lifespan) ? rand(15 * SECONDS, 25 * SECONDS) : null, FALSE);
                break;
            case VORTEX_ANOMALY:
                Dm.@new<obj_effect_anomaly_bhole>(local_turf, 2 * SECONDS, FALSE);
                break;
            case BIOSCRAMBLER_ANOMALY:
                Dm.@new<obj_effect_anomaly_bioscrambler_docile>(local_turf, null, FALSE);
                break;
            case DIMENSIONAL_ANOMALY:
                Dm.@new<obj_effect_anomaly_dimensional>(local_turf, null, FALSE);
                break;
        }
    }
}
