// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/life.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/mob/living/life.dm:12
public partial class mob_living : mob
{
    /// This divisor controls how fast body temperature changes to match the environment
    // tg: code/modules/mob/living/life.dm:2  #define BODYTEMP_DIVISOR 16 (file-local, #undef at :150)
    private const int BODYTEMP_DIVISOR = 16;

    /**
     * Handles the biological and general over-time processes of the mob.
     *
     *
     * Arguments:
     * - seconds_per_tick: The amount of time that has elapsed since this last fired.
     * - times_fired: The number of times SSmobs has fired
     */
    // tg: code/modules/mob/living/life.dm:12  set waitfor = FALSE, SHOULD_NOT_SLEEP(TRUE): the lint proves the body never
    // sleeps, so it is a plain synchronous method (waiting-model.md 4.4, 5.4) and DmTask callees go through Waiting.NoSleep.
    [ShouldNotSleep]
    public virtual object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();

        object signal_result = (!Dm.truthy(_listen_lookup?[COMSIG_LIVING_PRE_LIFE]) ? NONE : _SendSignal(COMSIG_LIVING_PRE_LIFE, Dm.list(this, seconds_per_tick)));

        if (Dm.truthy(Dm.bitand(signal_result, COMPONENT_LIVING_CANCEL_LIFE_PROCESSING))) // mmm less work
            return null;

        if (Dm.truthy(client))
        {
            turf T = get_turf(this);
            if (!Dm.truthy(T))
            {
                Waiting.NoSleep(move_to_error_room());
                string msg = " was found to have no .loc with an attached client, if the cause is unknown it would be wise to ask how this was accomplished.";
                message_admins(ADMIN_LOOKUPFLW(this) + msg);
                send2tgs_adminless_only("Mob", key_name_and_tag(this) + msg, R_ADMIN);
                log_message("was found to have no .loc with an attached client.", LOG_GAME);
            }

            // This is a temporary error tracker to make sure we've caught everything
            else if (!Dm.eq(registered_z, T.z))
            {
                // tg: #ifdef TESTING ... #endif not translated (inactive in production)
                log_game(Dm.text("Z-TRACKING: [] has somehow ended up in Z-level [] despite being registered in Z-level [].", this, T.z, registered_z));
                Waiting.NoSleep(update_z(T.z));
            }
        }
        else if (Dm.truthy(registered_z))
        {
            log_game(Dm.text("Z-TRACKING: [] of type [] has a Z-registration despite not having a client.", this, this.type));
            Waiting.NoSleep(update_z(null));
        }

        if (loc == null || HAS_TRAIT(this, TRAIT_NO_TRANSFORM))
            return null;

        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_LIFE]))
            _SendSignal(COMSIG_LIVING_LIFE, Dm.list(this, seconds_per_tick));
        if (QDELETED(this)) // signal handlers such as diseases could delete the mob
            return null;

        if (!HAS_TRAIT(this, TRAIT_STASIS))
        {
            if (stat != DEAD)
            {
                //Breathing, if applicable
                Waiting.NoSleep(handle_breathing(seconds_per_tick));
                if (loc == null) // Mice can die and become items from breathing
                    return null;
            }

            // Handle temperature/pressure differences between body and environment
            datum_gas_mixture environment = Waiting.NoSleep(loc.return_air()) as datum_gas_mixture;
            if (Dm.truthy(environment))
                Waiting.NoSleep(handle_environment(environment, seconds_per_tick));

            Waiting.NoSleep(handle_gravity(seconds_per_tick));
        }

        if (Dm.truthy(Dm.bitand(living_flags, QUEUE_NUTRITION_UPDATE)))
        {
            Waiting.NoSleep(update_nutrition());
            Dm.mask(ref living_flags, Dm.bitnot(QUEUE_NUTRITION_UPDATE));
        }

        if (Dm.truthy(Dm.bitand(living_flags, BLOOD_UPDATE_QUEUED)))
        {
            update_blood_effects();
            Dm.mask(ref living_flags, Dm.bitnot(BLOOD_UPDATE_QUEUED));
        }

        if (stat != DEAD)
            return TRUE;
        return null;
    }

    // tg: code/modules/mob/living/life.dm:73
    public virtual async DmTask handle_breathing(object seconds_per_tick = null)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_HANDLE_BREATHING]))
            _SendSignal(COMSIG_LIVING_HANDLE_BREATHING, Dm.list(this, seconds_per_tick));
    }

    // Base mob environment handler for body temperature
    // tg: code/modules/mob/living/life.dm:77
    public virtual async DmTask handle_environment(datum_gas_mixture environment = null, object seconds_per_tick = null, params object[] __extra)
    {
        object loc_temp = await get_temperature(environment);
        object temp_delta = Dm.sub(loc_temp, bodytemperature);

        if (ismovable(loc))
        {
            atom_movable occupied_space = loc as atom_movable;
            temp_delta = Dm.mul(temp_delta, 1 - occupied_space.contents_thermal_insulation);
        }

        if (Dm.lt(temp_delta, 0)) // it is cold here
        {
            if (!Dm.truthy(on_fire)) // do not reduce body temp when on fire
                adjust_bodytemperature(max(Dm.mul(max(Dm.div(temp_delta, BODYTEMP_DIVISOR), BODYTEMP_COOLING_MAX), seconds_per_tick), temp_delta));
        }
        else // this is a hot place
            adjust_bodytemperature(min(Dm.mul(min(Dm.div(temp_delta, BODYTEMP_DIVISOR), BODYTEMP_HEATING_MAX), seconds_per_tick), temp_delta));
    }

    /**
     * Get the fullness of the mob
     *
     * Fullness is a representation of how much nutrition the mob has,
     * including the nutrition of stuff yet to be digested (reagents in blood / stomach)
     *
     * * only_consumable - if TRUE, only consumable reagents are counted.
     * Otherwise, all reagents contribute to fullness, despite not adding nutrition as they process.
     *
     * Returns a number representing fullness, scaled similarly to nutrition.
     */
    // tg: code/modules/mob/living/life.dm:102
    public virtual object get_fullness(object only_consumable = null)
    {
        float fullness = nutrition;
        // we add the nutrition value of what we're currently digesting
        foreach (datum_reagent_consumable bits in Dm.each<datum_reagent_consumable>(((datum_reagents)reagents).reagent_list))
            fullness += Dm.num(bits.get_nutriment_factor(this as mob_living_carbon)) * Dm.num(bits.volume) / bits.metabolization_rate;
        return fullness;
    }

    /**
     * Check if the mob contains this reagent.
     *
     * This will validate the the reagent holder for the mob and any sub holders contain the requested reagent.
     * Vars:
     * * reagent (typepath) takes a PATH to a reagent.
     * * amount (int) checks for having a specific amount of that chemical.
     * * needs_metabolizing (bool) takes into consideration if the chemical is matabolizing when it's checked.
     */
    // tg: code/modules/mob/living/life.dm:118
    public virtual object has_reagent(object reagent = null, float? amount = null, float? needs_metabolizing = null)
    {
        amount ??= -1;
        needs_metabolizing ??= FALSE;
        return (reagents as datum_reagents)?.has_reagent(reagent, amount, needs_metabolizing);
    }

    // tg: code/modules/mob/living/life.dm:121
    public virtual void update_damage_hud()
    {
        return;
    }

    // tg: code/modules/mob/living/life.dm:124
    public virtual async DmTask handle_gravity(object seconds_per_tick = null)
    {
        if (Dm.gt(gravity_state, STANDARD_GRAVITY))
            await handle_high_gravity(gravity_state, seconds_per_tick);
    }

    // tg: code/modules/mob/living/life.dm:128
    public virtual void gravity_animate()
    {
        // M6 slice 7 (D155): translated. The client draws no motion blur and no filter animation (protocol.md section 34.1), so the
        // high gravity shows through tg's alert (veryhighgravity / highgravity on the HUD); not-translated.md keeps the row.
        if (!Dm.truthy(get_filter("gravity")))
            add_filter("gravity", 1, Dm.list(Dm.kv("type", "motion_blur"), Dm.kv("x", 0), Dm.kv("y", 0)));
        Dm.animate(get_filter("gravity"), Dm.named("y", 1), Dm.named("time", 10), Dm.named("loop", -1));
        Dm.animate(Dm.named("y", 0), Dm.named("time", 10));
    }

    // tg: code/modules/mob/living/life.dm:134
    public virtual async DmTask handle_high_gravity(object gravity = null, object seconds_per_tick = null)
    {
        if (Dm.lt(gravity, GRAVITY_DAMAGE_THRESHOLD)) //Aka gravity values of 3 or more
            return;

        object grav_strength = Dm.sub(gravity, GRAVITY_DAMAGE_THRESHOLD);
        await adjust_brute_loss(Dm.mul(min(Dm.mul(GRAVITY_DAMAGE_SCALING, grav_strength), GRAVITY_DAMAGE_MAXIMUM), seconds_per_tick));
    }

    /// Proc used for custom metabolization of reagents, if any
    // tg: code/modules/mob/living/life.dm:142
    [ShouldCallParent]
    public virtual async DmTask<object> reagent_tick(datum_reagent chem = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        return (!Dm.truthy(_listen_lookup?[COMSIG_MOB_REAGENT_TICK]) ? NONE : _SendSignal(COMSIG_MOB_REAGENT_TICK, Dm.list(this, chem, seconds_per_tick, metabolization_ratio)));
    }

    /// Proc used for custom reagent exposure effects, if any
    // tg: code/modules/mob/living/life.dm:147
    public virtual async DmTask<object> reagent_expose(datum_reagent chem = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        return null;
    }
}
