// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/lungs/_lungs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The lungs where M2 breathes translated (M2 P2, scope.md 2e "organs": _lungs.dm check_breath, handle_suffocation,
// on_life): the var block, Initialize, the insertion hooks, the gas reaction and relationship tables, the oxygen and
// carbon dioxide handlers, check_breath, breathe_gas_volume, handle_suffocation, handle_breath_temperature, on_life,
// get_availability and the received pressure procs. M4b slice 3 (fire and plasma): the handlers of the other gases
// (pluoxium, nitrogen breathers, plasma, bz, freon, halon, healium, helium, hypernoblium, miasma, n2o, nitrium, tritium,
// zauker, antinoblium). Gaps OG1 (D339, D341): the gases' reagents, the dilation's scanner line, the slime, surplus and ethereal lungs;
// the miasma's floor disease came with the diseases (gaps VI1). The breath particles are visuals (M10+).

// tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:1
[DmPath("/obj/item/organ/lungs")]
public partial class obj_item_organ_lungs : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:2-23
    public obj_item_organ_lungs()
    {
        name = "lungs";
        icon_state = "lungs";

        zone = BODY_ZONE_CHEST;
        slot = ORGAN_SLOT_LUNGS;
        gender = PLURAL;
        w_class = WEIGHT_CLASS_SMALL;

        healing_factor = STANDARD_ORGAN_HEALING;
        decay_factor = STANDARD_ORGAN_DECAY * 0.9f; // fails around 16.5 minutes, lungs are one of the last organs to die (of the ones we have)

        low_threshold_passed = span_warning("You feel short of breath.");
        high_threshold_passed = span_warning("You feel some sort of constriction around your chest as your breathing becomes shallow and rapid.");
        now_fixed = span_warning("Your lungs seem to once again be able to hold air.");
        low_threshold_cleared = span_info("You can breathe normally again.");
        high_threshold_cleared = span_info("The constriction around your chest loosens as your breathing calms down.");

        cell_line = CELL_LINE_ORGAN_LUNGS;
        cells_minimum = 1;
        cells_maximum = 2;

        visual = FALSE;
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:28-39  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        food_reagents = Dm.list(Dm.kv(typeof(datum_reagent_consumable_nutriment_organ_tissue), 5), Dm.kv(typeof(datum_reagent_medicine_salbutamol), 5));
        last_partial_pressures = Dm.alist();
        treat_as = Dm.list();
        breath_present = Dm.alist();
        breath_lost = Dm.alist();
        breathe_always = Dm.alist();
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:25
    public float failed = FALSE;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:26
    public float operated = FALSE; //whether we can still have our damages fixed through surgery

    /// Our previous breath's partial pressures, in the form gas id -> partial pressure
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:31
    public DmList last_partial_pressures;
    /// List of gas to treat as other gas, in the form list(inital_gas, treat_as, multiplier)
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:33
    public DmList treat_as;
    /// Alist of procs to run while a gas is present, in the form gas id -> proc_path
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:35
    public DmList breath_present;
    /// Alist of procs to run when a gas is immediately removed from the breath, in the form gas id -> proc_path
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:37
    public DmList breath_lost;
    /// Alist of procs to always run, in the form gas_id -> proc_path
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:39
    public DmList breathe_always;

    /// Gas mixture to breath out when we're done processing a breath
    /// Will get emptied out when it's all done
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:43
    public datum_gas_mixture_immutable breath_out;

    //Breath damage
    //These thresholds are checked against what amounts to total_mix_pressure * (gas_type_mols/total_mols)
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:47
    public float safe_oxygen_min = 16; // Minimum safe partial pressure of O2, in kPa
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:48
    public float safe_oxygen_max = 0;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:49
    public float safe_nitro_min = 0;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:50
    public float safe_co2_max = 10; // Yes it's an arbitrary value who cares?
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:51
    public float safe_plasma_min = 0;
    ///How much breath partial pressure is a safe amount of plasma. 0 means that we are immune to plasma.
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:53
    public float safe_plasma_max = 0.05f;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:54
    public float n2o_detect_min = 0.08f; //Minimum n2o for effects
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:55
    public float n2o_para_min = 1; //Sleeping agent
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:56
    public float n2o_sleep_min = 5; //Sleeping agent
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:57
    public float BZ_trip_balls_min = 1; //BZ gas
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:58
    public float BZ_brain_damage_min = 10; //Give people some room to play around without killing the station
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:59
    public float gas_stimulation_min = 0.002f; // For, Pluoxium, Nitrium and Freon
    ///Minimum amount of healium to make you unconscious for 4 seconds
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:61
    public float healium_para_min = 3;
    ///Minimum amount of healium to knock you down for good
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:63
    public float healium_sleep_min = 6;
    ///Minimum amount of helium to affect speech
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:65
    public float helium_speech_min = 5;
    ///Whether these lungs react negatively to miasma
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:67
    public float suffers_miasma = TRUE;
    // Vars for N2O/healium induced euphoria, stun, and sleep.
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:69
    public float n2o_euphoria = EUPHORIA_LAST_FLAG;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:70
    public float healium_euphoria = EUPHORIA_LAST_FLAG;

    /// All incoming breaths will have their pressure multiplied against this. Higher values allow more air to be breathed at once,
    /// while lower values can cause suffocation in low pressure environments.
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:74
    public float received_pressure_mult = 1;

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:76
    public float oxy_breath_dam_min = MIN_TOXIC_GAS_DAMAGE;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:77
    public float oxy_breath_dam_max = MAX_TOXIC_GAS_DAMAGE;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:78
    public string oxy_damage_type = OXY;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:79
    public float nitro_breath_dam_min = MIN_TOXIC_GAS_DAMAGE;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:80
    public float nitro_breath_dam_max = MAX_TOXIC_GAS_DAMAGE;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:81
    public string nitro_damage_type = OXY;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:82
    public float co2_breath_dam_min = MIN_TOXIC_GAS_DAMAGE;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:83
    public float co2_breath_dam_max = MAX_TOXIC_GAS_DAMAGE;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:84
    public string co2_damage_type = OXY;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:85
    public float plas_breath_dam_min = MIN_TOXIC_GAS_DAMAGE;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:86
    public float plas_breath_dam_max = MAX_TOXIC_GAS_DAMAGE;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:87
    public string plas_damage_type = TOX;

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:89
    public float tritium_irradiation_moles_min = 1;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:90
    public float tritium_irradiation_moles_max = 15;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:91
    public float tritium_irradiation_probability_min = 10;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:92
    public float tritium_irradiation_probability_max = 60;

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:94
    public float antinoblium_irradiation_moles_min = 0.5f;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:95
    public float antinoblium_irradiation_moles_max = 30;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:96
    public float antinoblium_irradiation_probability_min = 5;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:97
    public float antinoblium_irradiation_probability_max = 90;

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:99
    public string cold_message = "your face freezing and an icicle forming";
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:100
    public float cold_level_1_threshold = COLD_LEVEL_1_THRESHOLD;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:101
    public float cold_level_2_threshold = COLD_LEVEL_2_THRESHOLD;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:102
    public float cold_level_3_threshold = COLD_LEVEL_3_THRESHOLD;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:103
    public float cold_level_1_damage = COLD_GAS_DAMAGE_LEVEL_1; //Keep in mind with gas damage levels, you can set these to be negative, if you want someone to heal, instead.
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:104
    public float cold_level_2_damage = COLD_GAS_DAMAGE_LEVEL_2;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:105
    public float cold_level_3_damage = COLD_GAS_DAMAGE_LEVEL_3;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:106
    public string cold_damage_type = BURN;

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:108
    public string hot_message = "your face burning and a searing heat";
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:109
    public float heat_level_1_threshold = HEAT_LEVEL_1_THRESHOLD;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:110
    public float heat_level_2_threshold = HEAT_LEVEL_2_THRESHOLD;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:111
    public float heat_level_3_threshold = HEAT_LEVEL_3_THRESHOLD;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:112
    public float heat_level_1_damage = HEAT_GAS_DAMAGE_LEVEL_1;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:113
    public float heat_level_2_damage = HEAT_GAS_DAMAGE_LEVEL_2;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:114
    public float heat_level_3_damage = HEAT_GAS_DAMAGE_LEVEL_3;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:115
    public string heat_damage_type = BURN;

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:117
    public string breath_noise = "steady in- and exhalation";

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:946  #define SMOKER_ORGAN_HEALTH (STANDARD_ORGAN_THRESHOLD * 0.75)
    // (file-local, #undef at :1199; rules 11.4: internal on the file's main type, three lungs subtypes use it)
    internal const int SMOKER_ORGAN_HEALTH = 75;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:947  #define SMOKER_LUNG_HEALING (STANDARD_ORGAN_HEALING * 0.75)
    // (file-local, #undef at :1200)
    internal const float SMOKER_LUNG_HEALING = STANDARD_ORGAN_HEALING * 0.75f;

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:209  #define BREATH_RELATIONSHIP_INITIAL_GAS 1 (file-local, #undef at the end)
    private const int BREATH_RELATIONSHIP_INITIAL_GAS = 1;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:210  #define BREATH_RELATIONSHIP_CONVERT 2
    private const int BREATH_RELATIONSHIP_CONVERT = 2;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:211  #define BREATH_RELATIONSHIP_MULTIPLIER 3
    private const int BREATH_RELATIONSHIP_MULTIPLIER = 3;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:1128  #define LUNG_CAPACITY_ALERT_BUFFER 0.003 (file-local, #undef at :1148)
    private const float LUNG_CAPACITY_ALERT_BUFFER = 0.003f;
}

// tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:119 (interleaved types, rules 1.9)
public partial class obj_item_organ_lungs : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:119
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        breath_out = Dm.@new<datum_gas_mixture_immutable>(BREATH_VOLUME);

        // Sets up what gases we want to react to, and in what way
        // always is always processed, while_present is called when the gas is in the breath, and on_loss is called right after a gas is lost
        // The naming convention goes like this
        // always : breath_{gas_type}
        // safe/neutral while_present : consume_{gas_type}
        // safe/neutral on_loss : lose_{gas_type}
        // dangerous while_present : too_much_{gas_type}
        // dangerous on_loss : safe_{gas_type}
        // These are reccomendations, if something seems better feel free to ignore them. S a bit vibes based
        if (Dm.truthy(safe_oxygen_min))
            add_gas_reaction(typeof(datum_gas_oxygen), always: nameof(breathe_oxygen));
        if (Dm.truthy(safe_oxygen_max))
            add_gas_reaction(typeof(datum_gas_oxygen), while_present: nameof(too_much_oxygen), on_loss: nameof(safe_oxygen));
        add_gas_reaction(typeof(datum_gas_pluoxium), while_present: nameof(consume_pluoxium));
        // We treat a mole of ploux as 8 moles of oxygen
        add_gas_relationship(typeof(datum_gas_pluoxium), typeof(datum_gas_oxygen), 8);
        if (Dm.truthy(safe_nitro_min))
            add_gas_reaction(typeof(datum_gas_nitrogen), always: nameof(breathe_nitro));
        if (Dm.truthy(safe_co2_max))
            add_gas_reaction(typeof(datum_gas_carbon_dioxide), while_present: nameof(too_much_co2), on_loss: nameof(safe_co2));
        if (Dm.truthy(safe_plasma_min))
            add_gas_reaction(typeof(datum_gas_plasma), always: nameof(breathe_plasma));
        if (Dm.truthy(safe_plasma_max))
            add_gas_reaction(typeof(datum_gas_plasma), while_present: nameof(too_much_plasma), on_loss: nameof(safe_plasma));
        add_gas_reaction(typeof(datum_gas_bz), while_present: nameof(too_much_bz));
        add_gas_reaction(typeof(datum_gas_freon), while_present: nameof(too_much_freon));
        add_gas_reaction(typeof(datum_gas_halon), while_present: nameof(too_much_halon));
        add_gas_reaction(typeof(datum_gas_healium), while_present: nameof(consume_healium), on_loss: nameof(lose_healium));
        add_gas_reaction(typeof(datum_gas_helium), while_present: nameof(consume_helium), on_loss: nameof(lose_helium));
        add_gas_reaction(typeof(datum_gas_hypernoblium), while_present: nameof(consume_hypernoblium));
        if (Dm.truthy(suffers_miasma))
            add_gas_reaction(typeof(datum_gas_miasma), while_present: nameof(too_much_miasma), on_loss: nameof(safe_miasma));
        add_gas_reaction(typeof(datum_gas_nitrous_oxide), while_present: nameof(too_much_n2o), on_loss: nameof(safe_n2o));
        add_gas_reaction(typeof(datum_gas_nitrium), while_present: nameof(too_much_nitrium));
        add_gas_reaction(typeof(datum_gas_tritium), while_present: nameof(too_much_tritium));
        add_gas_reaction(typeof(datum_gas_zauker), while_present: nameof(too_much_zauker));
        add_gas_reaction(typeof(datum_gas_antinoblium), while_present: nameof(too_much_antinoblium));
        return dot;
    }

    ///Simply exists so that you don't keep any alerts from your previous lack of lungs.
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:162
    [DmParams("receiver", "special", "movement_flags")]
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon receiver = organ_owner;
        special ??= FALSE;
        object dot = null;
        dot = await base.on_mob_insert(receiver, special, movement_flags);

        receiver.clear_alert(ALERT_NOT_ENOUGH_OXYGEN);
        receiver.clear_alert(ALERT_NOT_ENOUGH_CO2);
        receiver.clear_alert(ALERT_NOT_ENOUGH_NITRO);
        receiver.clear_alert(ALERT_NOT_ENOUGH_PLASMA);
        receiver.clear_alert(ALERT_NOT_ENOUGH_N2O);
        update_bronchodilation_alerts();
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:172
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        // This is very "manual" I realize, but it's useful to ensure cleanup for gases we're removing happens
        // Avoids stuck alerts and such
        // var/static/datum/gas_mixture/immutable/dummy = new(BREATH_VOLUME): created at world start in TgState.InitStatics
        // (rules 10.6); the ??= only serves a test harness world that does not run InitStatics (never fires after a boot)
        on_mob_remove__dummy ??= Dm.@new<datum_gas_mixture_immutable>(BREATH_VOLUME);
        datum_gas_mixture_immutable dummy = on_mob_remove__dummy;
        foreach ((object gas_id, object partial_pressure) in Dm.pairs(last_partial_pressures))
        {
            object on_loss = breath_lost[gas_id];
            if (!Dm.truthy(on_loss))
                continue;

            await Dm.Call(this, on_loss as string, organ_owner, dummy, partial_pressure);
        }
        dummy.garbage_collect();
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:176  var/static/datum/gas_mixture/immutable/dummy (rules 10.6)
    private static ref datum_gas_mixture_immutable on_mob_remove__dummy => ref TgState.Current.obj_item_organ_lungs__on_mob_remove__dummy;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:648  var/static/datum/gas_mixture/immutable/empty_breath (rules 10.6)
    private static ref datum_gas_mixture_immutable check_breath__empty_breath => ref TgState.Current.obj_item_organ_lungs__check_breath__empty_breath;
}

public sealed partial class TgState
{
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:176  var/static/datum/gas_mixture/immutable/dummy = new(BREATH_VOLUME)
    internal datum_gas_mixture_immutable obj_item_organ_lungs__on_mob_remove__dummy;
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:648  var/static/datum/gas_mixture/immutable/empty_breath = new(BREATH_VOLUME)
    internal datum_gas_mixture_immutable obj_item_organ_lungs__check_breath__empty_breath;
}

// tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:197
public partial class obj_item_organ_lungs : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:197
    public virtual void add_gas_reaction(object gas_type = null, object while_present = null, object on_loss = null, object always = null)
    {
        if (Dm.truthy(always))
            breathe_always[gas_type] = always;

        if (while_present == null && on_loss == null)
            return;

        if (Dm.truthy(while_present))
            breath_present[gas_type] = while_present;
        if (Dm.truthy(on_loss))
            breath_lost[gas_type] = on_loss;
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:217
    public virtual void add_gas_relationship(object gas_type = null, object convert_to = null, object multiplier = null)
    {
        if (gas_type == null || convert_to == null || Dm.eq(multiplier, 0))
            return;

        DmList add = Dm.list_n(BREATH_RELATIONSHIP_MULTIPLIER);
        add[BREATH_RELATIONSHIP_INITIAL_GAS] = gas_type;
        add[BREATH_RELATIONSHIP_CONVERT] = convert_to;
        add[BREATH_RELATIONSHIP_MULTIPLIER] = multiplier;
        Dm.append(ref treat_as, Dm.list(add));
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:228
    public virtual void remove_gas_relationship(object gas_type = null, object convert_to = null, object multiplier = null)
    {
        if (gas_type == null || convert_to == null || Dm.eq(multiplier, 0))
            return;

        foreach (object packet in Dm.each(treat_as))
        {
            if (!Dm.eq(((DmList)packet)[BREATH_RELATIONSHIP_INITIAL_GAS], gas_type))
                continue;
            if (!Dm.eq(((DmList)packet)[BREATH_RELATIONSHIP_CONVERT], convert_to))
                continue;
            if (!Dm.eq(((DmList)packet)[BREATH_RELATIONSHIP_MULTIPLIER], multiplier))
                continue;
            Dm.remove(ref treat_as, packet);
            return;
        }
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:243
    public virtual async DmTask breathe_oxygen(mob_living_carbon breather = null, datum_gas_mixture breath = null, object o2_pp = null, object old_o2_pp = null)
    {
        if (Dm.num(o2_pp) < safe_oxygen_min && !HAS_TRAIT(breather, TRAIT_NO_BREATHLESS_DAMAGE))
        {
            // Not safe to check the old pp because of can_breath_vacuum
            breather.throw_alert(ALERT_NOT_ENOUGH_OXYGEN, typeof(atom_movable_screen_alert_not_enough_oxy));

            // rules 3.3 (settled at the M2 integration): handle_suffocation's slot types its first parameter /mob/living/carbon/human and tg passes the carbon
            // breather unchecked; the downcast is null for a non-human carbon (no M2 carbon but the human has species lungs).
            object gas_breathed = await handle_suffocation(breather as mob_living_carbon_human, Dm.num(o2_pp), safe_oxygen_min, Dm.num(breath.moles[typeof(datum_gas_oxygen)]));
            if (Dm.truthy(o2_pp))
                breathe_gas_volume(breath, typeof(datum_gas_oxygen), typeof(datum_gas_carbon_dioxide), volume: Dm.num(gas_breathed));
            return;
        }

        // If we used to not have enough, clear the alert
        // Note this can be redundant, because of the vacuum check. It is fail safe tho, so it's ok
        if (Dm.num(old_o2_pp) < safe_oxygen_min)
        {
            breather.failed_last_breath = FALSE;
            breather.clear_alert(ALERT_NOT_ENOUGH_OXYGEN);
        }

        breathe_gas_volume(breath, typeof(datum_gas_oxygen), typeof(datum_gas_carbon_dioxide));
        // Heal mob if not in crit.
        if (breather.stat == STABLE && Dm.truthy(breather.get_oxy_loss()))
            await breather.adjust_oxy_loss(-5);
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:265
    public virtual async DmTask<object> too_much_oxygen(mob_living_carbon breather = null, datum_gas_mixture breath = null, object o2_pp = null, object old_o2_pp = null)
    {
        // If too much Oxygen is poisonous.
        if (Dm.num(o2_pp) <= safe_oxygen_max)
        {
            if (Dm.num(old_o2_pp) > safe_oxygen_max)
                return BREATH_LOST;
            return null;
        }

        float ratio = (Dm.num(breath.moles[typeof(datum_gas_oxygen)]) / safe_oxygen_max) * 10;
        await breather.apply_damage(clamp(ratio, oxy_breath_dam_min, oxy_breath_dam_max), oxy_damage_type, spread_damage: TRUE);
        if (!HAS_TRAIT(breather, TRAIT_ANOSMIA))
        {
            breather.throw_alert(ALERT_TOO_MUCH_OXYGEN, typeof(atom_movable_screen_alert_too_much_oxy));
        }

        await breather.adjust_organ_loss(ORGAN_SLOT_LUNGS, clamp(Dm.num(o2_pp) * 0.5f, MIN_TOXIC_GAS_LUNG_DAMAGE, MAX_TOXIC_GAS_LUNG_DAMAGE));
        if (prob(Dm.num(o2_pp)))
            to_chat(breather, span_alert(I18n.T("You feel a burning sensation in your chest")));
        return null;
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:282
    public virtual void safe_oxygen(mob_living_carbon breather = null, datum_gas_mixture breath = null, object old_o2_pp = null)
    {
        breather.clear_alert(ALERT_TOO_MUCH_OXYGEN);
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:286
    public virtual async DmTask consume_pluoxium(mob_living_carbon breather = null, datum_gas_mixture breath = null, object pluoxium_pp = null, object old_pluoxium_pp = null)
    {
        breathe_gas_volume(breath, typeof(datum_gas_pluoxium));
        // Metabolize to reagent.
        if (Dm.num(pluoxium_pp) > gas_stimulation_min)
        {
            object existing = ((datum_reagents)breather.reagents).get_reagent_amount(typeof(datum_reagent_pluoxium)); // gaps OG1
            await ((datum_reagents)breather.reagents).add_reagent(typeof(datum_reagent_pluoxium), Math.Max(0, 1 - Dm.num(existing)));
        }
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:294
    public virtual async DmTask breathe_nitro(mob_living_carbon breather = null, datum_gas_mixture breath = null, object nitro_pp = null, object old_nitro_pp = null)
    {
        if (Dm.num(nitro_pp) < safe_nitro_min && !HAS_TRAIT(breather, TRAIT_NO_BREATHLESS_DAMAGE))
        {
            // Suffocation side-effects.
            // Not safe to check the old pp because of can_breath_vacuum
            if (!HAS_TRAIT(breather, TRAIT_ANOSMIA))
                breather.throw_alert(ALERT_NOT_ENOUGH_NITRO, typeof(atom_movable_screen_alert_not_enough_nitro));
            object gas_breathed = await handle_suffocation(breather as mob_living_carbon_human, Dm.num(nitro_pp), safe_nitro_min, Dm.num(breath.moles[typeof(datum_gas_nitrogen)]));
            if (Dm.truthy(nitro_pp))
                breathe_gas_volume(breath, typeof(datum_gas_nitrogen), typeof(datum_gas_carbon_dioxide), volume: Dm.num(gas_breathed));
            return;
        }

        if (Dm.num(old_nitro_pp) < safe_nitro_min)
        {
            breather.failed_last_breath = FALSE;
            breather.clear_alert(ALERT_NOT_ENOUGH_NITRO);
        }

        // Inhale N2, exhale equivalent amount of CO2. Look ma, sideways breathing!
        breathe_gas_volume(breath, typeof(datum_gas_nitrogen), typeof(datum_gas_carbon_dioxide));
        // Heal mob if not in crit.
        if (breather.health >= breather.crit_threshold && Dm.truthy(breather.oxyloss))
            await breather.adjust_oxy_loss(-5);
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:316
    public virtual async DmTask<object> too_much_co2(mob_living_carbon breather = null, datum_gas_mixture breath = null, object co2_pp = null, object old_co2_pp = null)
    {
        if (Dm.num(co2_pp) <= safe_co2_max)
        {
            if (Dm.num(old_co2_pp) > safe_co2_max)
                return BREATH_LOST;
            return null;
        }

        // If it's the first breath with too much CO2 in it, lets start a counter, then have them pass out after 12s or so.
        if (Dm.num(old_co2_pp) < safe_co2_max)
            breather.co2overloadtime = world.time;

        // CO2 side-effects.
        // Give the mob a chance to notice.
        if (prob(20))
        {
            await breather.emote("cough");
        }

        if ((world.time - Dm.num(breather.co2overloadtime)) > 12 * SECONDS)
        {
            if (!HAS_TRAIT(breather, TRAIT_ANOSMIA))
            {
                breather.throw_alert(ALERT_TOO_MUCH_CO2, typeof(atom_movable_screen_alert_too_much_co2));
            }
            await breather.Unconscious(6 * SECONDS);
            // Lets hurt em a little, let them know we mean business.
            await breather.apply_damage(3, co2_damage_type, spread_damage: TRUE);
            // They've been in here 30s now, start to kill them for their own good!
            if ((world.time - Dm.num(breather.co2overloadtime)) > 30 * SECONDS)
                await breather.apply_damage(8, co2_damage_type, spread_damage: TRUE);
        }
        return null;
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:342
    public virtual void safe_co2(mob_living_carbon breather = null, datum_gas_mixture breath = null, object old_co2_pp = null)
    {
        // Reset side-effects.
        breather.co2overloadtime = 0;
        breather.clear_alert(ALERT_TOO_MUCH_CO2);
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:348
    public virtual async DmTask breathe_plasma(mob_living_carbon breather = null, datum_gas_mixture breath = null, object plasma_pp = null, object old_plasma_pp = null)
    {
        // Suffocation side-effects.
        if (Dm.num(plasma_pp) < safe_plasma_min && !HAS_TRAIT(breather, TRAIT_NO_BREATHLESS_DAMAGE))
        {
            // Could check old_plasma_pp but vacuum breathing hates me
            if (!HAS_TRAIT(breather, TRAIT_ANOSMIA))
                breather.throw_alert(ALERT_NOT_ENOUGH_PLASMA, typeof(atom_movable_screen_alert_not_enough_plas));
            // Breathe insufficient amount of Plasma, exhale CO2.
            object gas_breathed = await handle_suffocation(breather as mob_living_carbon_human, Dm.num(plasma_pp), safe_plasma_min, Dm.num(breath.moles[typeof(datum_gas_plasma)]));
            if (Dm.truthy(plasma_pp))
                breathe_gas_volume(breath, typeof(datum_gas_plasma), typeof(datum_gas_carbon_dioxide), volume: Dm.num(gas_breathed));
            return;
        }

        if (Dm.num(old_plasma_pp) < safe_plasma_min)
        {
            breather.failed_last_breath = FALSE;
            breather.clear_alert(ALERT_NOT_ENOUGH_PLASMA);
        }
        // Inhale Plasma, exhale equivalent amount of CO2.
        breathe_gas_volume(breath, typeof(datum_gas_plasma), typeof(datum_gas_carbon_dioxide));
        // Heal mob if not in crit.
        if (breather.health >= breather.crit_threshold && Dm.truthy(breather.oxyloss))
            await breather.adjust_oxy_loss(-5);
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:370
    public virtual async DmTask<object> too_much_plasma(mob_living_carbon breather = null, datum_gas_mixture breath = null, object plasma_pp = null, object old_plasma_pp = null)
    {
        if (Dm.num(plasma_pp) <= safe_plasma_max)
        {
            if (Dm.num(old_plasma_pp) > safe_plasma_max)
                return BREATH_LOST;
            return null;
        }

        if (Dm.num(old_plasma_pp) < safe_plasma_max)
        {
            if (!HAS_TRAIT(breather, TRAIT_ANOSMIA))
                breather.throw_alert(ALERT_TOO_MUCH_PLASMA, typeof(atom_movable_screen_alert_too_much_plas));
        }

        float ratio = (Dm.num(breath.moles[typeof(datum_gas_plasma)]) / safe_plasma_max) * 10;
        await breather.apply_damage(clamp(ratio, plas_breath_dam_min, plas_breath_dam_max), plas_damage_type, spread_damage: TRUE);
        await breather.adjust_organ_loss(ORGAN_SLOT_LUNGS, clamp(Dm.num(plasma_pp) * 0.5f, MIN_TOXIC_GAS_LUNG_DAMAGE, MAX_TOXIC_GAS_LUNG_DAMAGE));
        if (prob(Dm.num(plasma_pp)))
            to_chat(breather, span_alert(I18n.T("You feel a burning sensation in your chest")));
        return null;
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:387
    public virtual void safe_plasma(mob_living_carbon breather = null, datum_gas_mixture breath = null, object old_plasma_pp = null)
    {
        breather.clear_alert(ALERT_TOO_MUCH_PLASMA);
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:391
    public virtual async DmTask too_much_bz(mob_living_carbon breather = null, datum_gas_mixture breath = null, object bz_pp = null, object old_bz_pp = null)
    {
        if (Dm.num(bz_pp) > BZ_trip_balls_min)
        {
            await ((datum_reagents)breather.reagents).add_reagent(typeof(datum_reagent_bz_metabolites), clamp(Dm.num(bz_pp), 1, 5)); // gaps OG1
        }
        if (Dm.num(bz_pp) > BZ_brain_damage_min && prob(33))
            await breather.adjust_organ_loss(ORGAN_SLOT_BRAIN, 3, 150, ORGAN_ORGANIC);
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:398
    public virtual async DmTask too_much_freon(mob_living_carbon breather = null, datum_gas_mixture breath = null, object freon_pp = null, object old_freon_pp = null)
    {
        // Inhale Freon. Exhale nothing.
        breathe_gas_volume(breath, typeof(datum_gas_freon));
        if (Dm.num(freon_pp) > gas_stimulation_min)
        {
            await ((datum_reagents)breather.reagents).add_reagent(typeof(datum_reagent_freon), 1); // gaps OG1
        }
        if (Dm.num(freon_pp) > 40)
        {
            await breather.emote("gasp");
            await breather.adjust_fire_loss(15);
            if (prob(Dm.num(freon_pp) / 2))
            {
                to_chat(breather, span_alert(I18n.T("Your throat closes up!")));
                await breather.set_timed_status_effect(6 * SECONDS, typeof(datum_status_effect_silenced), TRUE); // set_silence_if_lower (__DEFINES/status_effects.dm:179), inline
            }
        }
        else
            await breather.adjust_fire_loss(Dm.num(freon_pp) / 4);
        await breather.adjust_organ_loss(ORGAN_SLOT_LUNGS, clamp(Dm.num(freon_pp) * 0.5f, MIN_TOXIC_GAS_LUNG_DAMAGE, MAX_TOXIC_GAS_LUNG_DAMAGE));
        if (prob(Dm.num(freon_pp)))
            to_chat(breather, span_alert(I18n.T("You feel a burning sensation in your chest")));
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:416
    public virtual async DmTask too_much_halon(mob_living_carbon breather = null, datum_gas_mixture breath = null, object halon_pp = null, object old_halon_pp = null)
    {
        // Inhale Halon. Exhale nothing.
        breathe_gas_volume(breath, typeof(datum_gas_halon));
        // Metabolize to reagent.
        if (Dm.num(halon_pp) > gas_stimulation_min)
        {
            await breather.adjust_oxy_loss(5);
            await ((datum_reagents)breather.reagents).add_reagent(typeof(datum_reagent_halon), Math.Max(0, 1 - Dm.num(((datum_reagents)breather.reagents).get_reagent_amount(typeof(datum_reagent_halon))))); // gaps OG1
        }
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:425
    public virtual async DmTask consume_healium(mob_living_carbon breather = null, datum_gas_mixture breath = null, object healium_pp = null, object old_healium_pp = null)
    {
        breathe_gas_volume(breath, typeof(datum_gas_healium));
        // Euphoria side-effect.
        if (Dm.num(healium_pp) > gas_stimulation_min)
        {
            if (prob(15))
            {
                to_chat(breather, span_alert(I18n.T("Your head starts spinning and your lungs burn!")));
                healium_euphoria = EUPHORIA_ACTIVE;
                await breather.emote("gasp");
            }
        }
        else
            healium_euphoria = EUPHORIA_INACTIVE;
        // Stun/Sleep side-effects.
        if (Dm.num(healium_pp) > healium_para_min && !Dm.truthy(breather.IsSleeping()) && prob(30))
            await breather.Sleeping(rand(3 * SECONDS, 5 * SECONDS));
        // Metabolize to reagent when concentration is high enough.
        if (Dm.num(healium_pp) > healium_sleep_min)
        {
            await ((datum_reagents)breather.reagents).add_reagent(typeof(datum_reagent_healium), Math.Max(0, 1 - Dm.num(((datum_reagents)breather.reagents).get_reagent_amount(typeof(datum_reagent_healium))))); // gaps OG1
        }
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:443
    public virtual void lose_healium(mob_living_carbon breather = null, datum_gas_mixture breath = null, object old_healium_pp = null)
    {
        healium_euphoria = EUPHORIA_INACTIVE;
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:447
    public virtual void consume_helium(mob_living_carbon breather = null, datum_gas_mixture breath = null, object helium_pp = null, object old_helium_pp = null)
    {
        breathe_gas_volume(breath, typeof(datum_gas_helium));
        if (Dm.num(helium_pp) > helium_speech_min)
        {
            if (Dm.num(old_helium_pp) <= helium_speech_min)
                RegisterSignal(breather, COMSIG_MOB_SAY, nameof(handle_helium_speech));
        }
        else
        {
            if (Dm.num(old_helium_pp) > helium_speech_min)
                UnregisterSignal(breather, COMSIG_MOB_SAY);
        }
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:457
    public virtual void lose_helium(mob_living_carbon breather = null, datum_gas_mixture breath = null, object old_helium_pp = null)
    {
        UnregisterSignal(breather, COMSIG_MOB_SAY);
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:461
    public virtual void handle_helium_speech(mob_living_carbon breather = null, DmList speech_args = null)
    {
        // SIGNAL_HANDLER
        Dm.combine_at(speech_args, SPEECH_SPANS, SPAN_SMALL_VOICE);
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:466
    public virtual async DmTask consume_hypernoblium(mob_living_carbon breather = null, datum_gas_mixture breath = null, object hypernob_pp = null, object old_hypernob_pp = null)
    {
        breathe_gas_volume(breath, typeof(datum_gas_hypernoblium));
        if (Dm.num(hypernob_pp) > gas_stimulation_min)
        {
            object existing = ((datum_reagents)breather.reagents).get_reagent_amount(typeof(datum_reagent_hypernoblium)); // gaps OG1
            await ((datum_reagents)breather.reagents).add_reagent(typeof(datum_reagent_hypernoblium), Math.Max(0, 1 - Dm.num(existing)));
        }
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:473
    public virtual async DmTask too_much_miasma(mob_living_carbon breather = null, datum_gas_mixture breath = null, object miasma_pp = null, object old_miasma_pp = null)
    {
        // Inhale Miasma. Exhale nothing.
        breathe_gas_volume(breath, typeof(datum_gas_miasma));
        // Miasma side effects
        if (prob(0.5f * Dm.num(miasma_pp)))
        {
            foreach (datum_disease disease in Dm.each_cast<datum_disease>(breather.diseases)) // gaps VI1
            {
                if (Dm.truthy(is_type_in_list(disease, GLOB.floor_diseases)))
                    return;
            }
            Type random_disease = pick_weight(GLOB.floor_diseases) as Type;
            datum_disease inflicted_disease = (datum_disease)await Dm.new_async(random_disease);
            await breather.ForceContractDisease(inflicted_disease, del_on_fail: TRUE);
        }
        if (HAS_TRAIT(breather, TRAIT_ANOSMIA)) //Anosmia quirk holder cannot smell miasma, but can get diseases from it.
            return;
        float sw = Dm.num(miasma_pp);
        if (sw >= 0.25f && sw <= 5) // 0.25 to 5
        {
            // At lower pp, give out a little warning
            breather.clear_mood_event("smell");
            if (prob(5))
                to_chat(breather, span_notice(I18n.T("There is an unpleasant smell in the air.")));
        }
        else if (sw >= 5 && sw <= 15) // 5 to 15
        {
            //At somewhat higher pp, warning becomes more obvious
            if (prob(15))
            {
                to_chat(breather, span_warning(I18n.T("You smell something horribly decayed inside this room.")));
                await breather.add_mood_event("smell", typeof(datum_mood_event_disgust_bad_smell));
            }
        }
        else if (sw >= 15 && sw <= 30) // 15 to 30
        {
            //Small chance to vomit. By now, people have internals on anyway
            if (prob(5))
            {
                to_chat(breather, span_warning(I18n.T("The stench of rotting carcasses is unbearable!")));
                await breather.add_mood_event("smell", typeof(datum_mood_event_disgust_nauseating_stench));
                await breather.vomit(VOMIT_CATEGORY_DEFAULT);
            }
        }
        else if (sw >= 30) // 30 to INFINITY
        {
            //Higher chance to vomit. Let the horror start
            if (prob(15))
            {
                to_chat(breather, span_warning(I18n.T("The stench of rotting carcasses is unbearable!")));
                await breather.add_mood_event("smell", typeof(datum_mood_event_disgust_nauseating_stench));
                await breather.vomit(VOMIT_CATEGORY_DEFAULT);
            }
        }
        else
            breather.clear_mood_event("smell");
        // In a full miasma atmosphere with 101.34 pKa, about 10 disgust per breath, is pretty low compared to threshholds
        // Then again, this is a purely hypothetical scenario and hardly reachable
        breather.adjust_disgust(0.1f * Dm.num(miasma_pp));
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:516
    public virtual void safe_miasma(mob_living_carbon breather = null, datum_gas_mixture breath = null, object old_miasma_pp = null)
    {
        // Clear out moods when immune to miasma, or if there's no miasma at all.
        breather.clear_mood_event("smell");
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:521
    public virtual async DmTask<object> too_much_n2o(mob_living_carbon breather = null, datum_gas_mixture breath = null, object n2o_pp = null, object old_n2o_pp = null)
    {
        if (Dm.num(n2o_pp) < n2o_para_min)
        {
            // Small amount of N2O, small side-effects.
            if (Dm.num(n2o_pp) <= n2o_detect_min)
            {
                if (Dm.num(old_n2o_pp) > n2o_detect_min)
                    return BREATH_LOST;
                return null;
            }
            // No alert for small amounts, but the mob randomly feels euphoric.
            if (Dm.num(old_n2o_pp) >= n2o_para_min || Dm.num(old_n2o_pp) <= n2o_detect_min)
                breather.clear_alert(ALERT_TOO_MUCH_N2O);

            if (prob(20))
            {
                n2o_euphoria = EUPHORIA_ACTIVE;
                await breather.emote(pick("giggle", "laugh"));
                await breather.set_timed_status_effect(30 * SECONDS, typeof(datum_status_effect_drugginess)); // set_drugginess (__DEFINES/status_effects.dm:173), inline
            }
            else
                n2o_euphoria = EUPHORIA_INACTIVE;
            return null;
        }

        // More N2O, more severe side-effects. Causes stun/sleep.
        if (Dm.num(old_n2o_pp) < n2o_para_min)
        {
            if (!HAS_TRAIT(breather, TRAIT_ANOSMIA))
                breather.throw_alert(ALERT_TOO_MUCH_N2O, typeof(atom_movable_screen_alert_too_much_n2o));
        }
        n2o_euphoria = EUPHORIA_ACTIVE;

        // give them one second of grace to wake up and run away a bit!
        if (!HAS_TRAIT(breather, TRAIT_SLEEPIMMUNE))
            await breather.Unconscious(6 * SECONDS);
        // Enough to make the mob sleep.
        if (Dm.num(n2o_pp) > n2o_sleep_min)
            await breather.Sleeping(min(Dm.num(breather.AmountSleeping()) + 100, 200));
        return null;
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:554
    public virtual void safe_n2o(mob_living_carbon breather = null, datum_gas_mixture breath = null, object old_n2o_pp = null)
    {
        n2o_euphoria = EUPHORIA_INACTIVE;
        breather.clear_alert(ALERT_TOO_MUCH_N2O);
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:559
    public virtual async DmTask too_much_nitrium(mob_living_carbon breather = null, datum_gas_mixture breath = null, object nitrium_pp = null, object old_nitrium_pp = null)
    {
        breathe_gas_volume(breath, typeof(datum_gas_nitrium));

        if (prob(20))
        {
            await breather.emote("burp");
        }

        // Random chance to inflict side effects increases with pressure.
        if ((prob(Dm.num(nitrium_pp)) && (Dm.num(nitrium_pp) > 15)))
        {
            // Nitrium side-effect.
            await breather.adjust_organ_loss(ORGAN_SLOT_LUNGS, clamp(Dm.num(nitrium_pp) * 0.1f, MIN_TOXIC_GAS_LUNG_DAMAGE, MAX_TOXIC_GAS_LUNG_DAMAGE));
            to_chat(breather, span_alert(I18n.T("You feel a burning sensation in your chest")));
        }
        // Metabolize to reagents.
        if (Dm.num(nitrium_pp) > 5)
        {
            object existing = ((datum_reagents)breather.reagents).get_reagent_amount(typeof(datum_reagent_nitrium_low_metabolization)); // gaps OG1
            await ((datum_reagents)breather.reagents).add_reagent(typeof(datum_reagent_nitrium_low_metabolization), Math.Max(0, 2 - Dm.num(existing)));
        }
        if (Dm.num(nitrium_pp) > 10)
        {
            object existing = ((datum_reagents)breather.reagents).get_reagent_amount(typeof(datum_reagent_nitrium_high_metabolization));
            await ((datum_reagents)breather.reagents).add_reagent(typeof(datum_reagent_nitrium_high_metabolization), Math.Max(0, 2 - Dm.num(existing)));
        }
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:579
    public virtual async DmTask too_much_tritium(mob_living_carbon breather = null, datum_gas_mixture breath = null, object trit_pp = null, object old_trit_pp = null)
    {
        object gas_breathed = breathe_gas_volume(breath, typeof(datum_gas_tritium));
        float moles_visible = Dm.num(((DmList)GLOB.meta_gas_info[META_GAS_MOLES_VISIBLE])[typeof(datum_gas_tritium)]) * BREATH_PERCENTAGE;
        // Tritium side-effects.
        if (Dm.num(gas_breathed) > moles_visible)
        {
            float ratio = Dm.num(gas_breathed) * 15;
            await breather.adjust_tox_loss(clamp(ratio, MIN_TOXIC_GAS_DAMAGE, MAX_TOXIC_GAS_DAMAGE));
        }
        // If you're breathing in half an atmosphere of radioactive gas, you fucked up.
        // rules 10.6: tg's Master always creates SSradiation; a harness world without it irradiates nothing.
        if ((Dm.num(trit_pp) > tritium_irradiation_moles_min) && Dm.truthy(SSradiation?.can_irradiate_basic(breather)))
        {
            float lerp_scale = min(tritium_irradiation_moles_max, Dm.num(trit_pp) - tritium_irradiation_moles_min) / (tritium_irradiation_moles_max - tritium_irradiation_moles_min);
            float chance = LERP(tritium_irradiation_probability_min, tritium_irradiation_probability_max, lerp_scale);
            if (prob(chance))
                await breather.AddComponent(typeof(datum_component_irradiated));
        }
        await breather.adjust_organ_loss(ORGAN_SLOT_LUNGS, clamp(Dm.num(trit_pp) * 1, MIN_TOXIC_GAS_LUNG_DAMAGE, MAX_TOXIC_GAS_LUNG_DAMAGE));
        if (prob(Dm.num(trit_pp)))
            to_chat(breather, span_alert(I18n.T("You feel a burning sensation in your chest")));
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:597
    public virtual async DmTask too_much_antinoblium(mob_living_carbon breather = null, datum_gas_mixture breath = null, object antinoblium_pp = null, object old_antinoblium_pp = null)
    {
        breathe_gas_volume(breath, typeof(datum_gas_antinoblium));
        object gas_breathed = breathe_gas_volume(breath, typeof(datum_gas_antinoblium));
        float moles_visible = Dm.num(((DmList)GLOB.meta_gas_info[META_GAS_MOLES_VISIBLE])[typeof(datum_gas_antinoblium)]) * BREATH_PERCENTAGE;
        // more severe side effects than trit
        if (Dm.num(gas_breathed) > moles_visible)
        {
            float ratio = Dm.num(gas_breathed) * 30;
            await breather.adjust_tox_loss(clamp(ratio, MIN_TOXIC_GAS_DAMAGE, MAX_TOXIC_GAS_DAMAGE));
        }
        // rules 10.6: tg's Master always creates SSradiation; a harness world without it irradiates nothing.
        if ((Dm.num(antinoblium_pp) > antinoblium_irradiation_moles_min) && Dm.truthy(SSradiation?.can_irradiate_basic(breather)))
        {
            float lerp_scale = min(antinoblium_irradiation_moles_max, Dm.num(antinoblium_pp) - antinoblium_irradiation_moles_min) / (antinoblium_irradiation_moles_max - antinoblium_irradiation_moles_min);
            float chance = LERP(antinoblium_irradiation_probability_min, antinoblium_irradiation_probability_max, lerp_scale);
            if (prob(chance))
                await breather.AddComponent(typeof(datum_component_irradiated));
        }

        await breather.adjust_organ_loss(ORGAN_SLOT_LUNGS, clamp(Dm.num(antinoblium_pp) * 1, MIN_TOXIC_GAS_LUNG_DAMAGE, MAX_TOXIC_GAS_LUNG_DAMAGE));
        if (prob(Dm.num(antinoblium_pp)))
            to_chat(breather, span_alert(I18n.T("You feel a burning sensation in your chest")));
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:616
    public virtual async DmTask too_much_zauker(mob_living_carbon breather = null, datum_gas_mixture breath = null, object zauker_pp = null, object old_zauker_pp = null)
    {
        breathe_gas_volume(breath, typeof(datum_gas_zauker));
        // Metabolize to reagent.
        if (Dm.num(zauker_pp) > gas_stimulation_min)
        {
            object existing = ((datum_reagents)breather.reagents).get_reagent_amount(typeof(datum_reagent_zauker)); // gaps OG1
            await ((datum_reagents)breather.reagents).add_reagent(typeof(datum_reagent_zauker), Math.Max(0, 1 - Dm.num(existing)));
        }

        await breather.adjust_organ_loss(ORGAN_SLOT_LUNGS, clamp(Dm.num(zauker_pp) * 2, MIN_TOXIC_GAS_LUNG_DAMAGE, MAX_TOXIC_GAS_LUNG_DAMAGE));
        if (prob(Dm.num(zauker_pp)))
            to_chat(breather, span_alert(I18n.T("You feel a burning sensation in your chest")));
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:639
    public virtual async DmTask<object> check_breath(datum_gas_mixture breath = null, mob_living_carbon_human breather = null)
    {
        if (HAS_TRAIT(breather, TRAIT_GODMODE))
        {
            breather.failed_last_breath = FALSE;
            breather.clear_alert(ALERT_NOT_ENOUGH_OXYGEN);
            return FALSE;
        }

        if (HAS_TRAIT(breather, TRAIT_NOBREATH))
            return FALSE;

        // If the breath is falsy or "null", we can use the backup empty_breath.
        if (breath == null)
        {
            // var/static/datum/gas_mixture/immutable/empty_breath = new(BREATH_VOLUME): created at world start in
            // TgState.InitStatics (rules 10.6); the ??= only serves a test harness world without InitStatics
            check_breath__empty_breath ??= Dm.@new<datum_gas_mixture_immutable>(BREATH_VOLUME);
            datum_gas_mixture_immutable empty_breath = check_breath__empty_breath;
            breath = empty_breath;
        }

        // Indicates if there are moles of gas in the breath.
        bool has_moles = Dm.num(breath.total_moles()) != 0;

        // Check for moles of gas and handle partial pressures / special conditions.
        if (has_moles)
        {
            // Breath has more than 0 moles of gas.
            // Route gases through mask filter if breather is wearing one.
            obj_item_clothing_mask worn_mask = breather.get_item_by_slot(ITEM_SLOT_MASK) as obj_item_clothing_mask;
            if (worn_mask != null && Dm.truthy(worn_mask.has_filter))
                breath = worn_mask.consume_filter(breath) as datum_gas_mixture;
        }
        // Breath has 0 moles of gas, and we can breathe space
        else if (HAS_TRAIT(breather, TRAIT_NO_BREATHLESS_DAMAGE))
        {
            // The lungs can breathe anyways. What are you? Some bottom-feeding, scum-sucking algae eater?
            breather.failed_last_breath = FALSE;
            // Vacuum-adapted lungs regenerate oxyloss even when breathing nothing.
            if (breather.health >= breather.crit_threshold && Dm.truthy(breather.oxyloss))
                await breather.adjust_oxy_loss(-5);
        }
        else
        {
            // Can't breathe!
            breather.failed_last_breath = TRUE;
        }

        // The list of gases in the breath.
        DmList breath_moles = breath.moles;
        // Copy the breath's temperature into breath_out to avoid cooling the output breath down unfairly
        breath_out.temperature = breath.temperature;

        bool old_euphoria = (n2o_euphoria == EUPHORIA_ACTIVE || healium_euphoria == EUPHORIA_ACTIVE);

        // Cache for sonic speed
        DmList last_partial_pressures = this.last_partial_pressures;
        DmList breathe_always = this.breathe_always;
        DmList breath_present = this.breath_present;
        DmList breath_lost = this.breath_lost;

        // Build out our partial pressures, for use as we go
        DmList partial_pressures = Dm.alist();
        foreach ((object gas_id, object amount) in Dm.pairs(breath_moles))
            partial_pressures[gas_id] = breath.get_breath_partial_pressure(Dm.num(amount) * received_pressure_mult);

        // Treat gas as other types of gas
        foreach (DmList conversion_packet in Dm.each_cast<DmList>(treat_as))
        {
            object read_from = conversion_packet[BREATH_RELATIONSHIP_INITIAL_GAS];
            if (!Dm.truthy(partial_pressures[read_from]))
                continue;
            object convert_into = conversion_packet[BREATH_RELATIONSHIP_CONVERT];
            Dm.append_at(partial_pressures, convert_into, Dm.mul(partial_pressures[read_from], conversion_packet[BREATH_RELATIONSHIP_MULTIPLIER]));
            if (Dm.num(partial_pressures[convert_into]) <= 0)
                Dm.remove(ref partial_pressures, convert_into); // No negative values jeremy
        }

        // First, we breathe the stuff that always wants to be processed
        // This is typically things like o2, stuff the mob needs to live
        // Ensures the gas will always be instanciated, so people can interact with it safely
        foreach ((object breath_id, object inhale) in Dm.pairs(breathe_always))
        {
            object partial_pressure = (partial_pressures[breath_id] is var or__1 && Dm.truthy(or__1) ? or__1 : 0);
            object old_partial_pressure = (last_partial_pressures[breath_id] is var or__2 && Dm.truthy(or__2) ? or__2 : 0);
            await Dm.Call(this, inhale as string, breather, breath, partial_pressure, old_partial_pressure);
        }

        // Now we'll handle the callbacks that want to be run conditionally off our current breath
        foreach (object gas_id in Dm.each(breath_moles))
        {
            object when_present = breath_present[gas_id];
            if (!Dm.truthy(when_present))
                continue;

            object reaction = await Dm.Call(this, when_present as string, breather, breath, partial_pressures[gas_id], last_partial_pressures[gas_id]);
            if (Dm.eq(reaction, BREATH_LOST))
            {
                object on_lose = breath_lost[gas_id];
                if (Dm.truthy(on_lose))
                    await Dm.Call(this, on_lose as string, breather, breath, partial_pressures[gas_id], last_partial_pressures[gas_id]);
            }
        }

        // Finally, we'll run the callbacks that aren't in breath_gases, but WERE in our last breath
        foreach ((object gas_id, object partial_pressure) in Dm.pairs(last_partial_pressures))
        {
            // If we still have it, go away
            if (Dm.truthy(breath_moles[gas_id]))
                continue;
            object on_loss = breath_lost[gas_id];
            if (!Dm.truthy(on_loss))
                continue;

            await Dm.Call(this, on_loss as string, breather, breath, partial_pressure);
        }

        this.last_partial_pressures = partial_pressures;

        // Handle chemical euphoria mood event, caused by gases such as N2O or healium.
        bool new_euphoria = (n2o_euphoria == EUPHORIA_ACTIVE || healium_euphoria == EUPHORIA_ACTIVE);
        if (!old_euphoria && new_euphoria)
        {
            // M8x: the cryo loop's anesthetic N2O reaches this
            await breather.add_mood_event("chemical_euphoria", typeof(datum_mood_event_chemical_euphoria));
        }
        else if (old_euphoria && !new_euphoria)
            breather.clear_mood_event("chemical_euphoria");

        if (has_moles)
        {
            await handle_breath_temperature(breath, breather);
            // Merge breath_out into breath. They're kept seprerate before now to ensure stupid like, order of operations shit doesn't happen
            // But that time has passed
            breath.merge(breath_out);
            // Resets immutable gas_mixture to empty.
            breath_out.garbage_collect();
        }

        breath.garbage_collect();
        // Returning FALSE indicates the breath failed.
        if (!Dm.truthy(breather.failed_last_breath))
            return TRUE;
        return null;
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:758
    public virtual object breathe_gas_volume(datum_gas_mixture breath = null, object remove_id = null, object exchange_id = null, float? volume = null)
    {
        volume ??= INFINITY;
        DmList breath_moles = breath.moles;
        volume = min(Dm.num(volume), Dm.num(breath_moles[remove_id]));
        Dm.remove_at(breath_moles, remove_id, volume);
        if (Dm.truthy(exchange_id))
            Dm.append_at(breath_out.moles, exchange_id, volume);
        return volume;
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:768
    public virtual async DmTask<object> handle_suffocation(mob_living_carbon_human suffocator = null, float? breath_pp = null, float? safe_breath_min = null, float? mole_count = null)
    {
        breath_pp ??= 0;
        safe_breath_min ??= 0;
        mole_count ??= 0;
        object dot = null;
        dot = 0;
        // Can't suffocate without a Human, or without minimum breath pressure.
        if (suffocator == null || Dm.num(safe_breath_min) <= 0)
            return dot;
        // Mob is suffocating.
        suffocator.failed_last_breath = TRUE;
        // Give them a chance to notice something is wrong.
        if (prob(25))
        {
            await suffocator.emote("gasp");
        }

        // note: this is where crit damage is handled for mobs that breathe
        float oxy_damage_dealt = SUFFOCATION_OXYLOSS;
        if (Dm.num(breath_pp) > 0)
        {
            // we got a partial breath, scale the damage based on how much we got
            oxy_damage_dealt *= ((Dm.num(safe_breath_min) - Dm.num(breath_pp)) / Dm.num(safe_breath_min));
            dot = mole_count;
        }

        // in hard crit, suffocation damage is reduced significantly (or to zero if the relevant trait is present)
        if (suffocator.stat == SOFT_CRIT || suffocator.stat == HARD_CRIT)
            oxy_damage_dealt *= (HAS_TRAIT(suffocator, TRAIT_NOCRITDAMAGE) ? 0 : SUFFOCATION_OXYLOSS_CRIT_MODIFIER);

        if (oxy_damage_dealt > 0)
            await suffocator.apply_damage(oxy_damage_dealt, OXY);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:794
    public virtual async DmTask handle_breath_temperature(datum_gas_mixture breath = null, mob_living_carbon_human breather = null)
    {
        float breath_temperature = breath.temperature;
        breath.temperature = breather.bodytemperature; // The air you breathe out should match your body temperature

        if ((breath_temperature > cold_level_1_threshold) || (breath_temperature < heat_level_1_threshold))
            return; // safe air that isn't too hot or cold

        if (!HAS_TRAIT(breather, TRAIT_RESISTCOLD)) // COLD DAMAGE
        {
            float cold_modifier = ((datum_species)breather.dna.species).coldmod;
            float breath_effect_prob = 0;
            if (breath_temperature <= cold_level_3_threshold)
            {
                await breather.apply_damage(cold_level_3_damage * cold_modifier, cold_damage_type, spread_damage: TRUE);
                breath_effect_prob = 100;
                await breather.adjust_organ_loss(ORGAN_SLOT_LUNGS, TEMPERATURE_LUNG_DAMAGE);
            }
            else if (breath_temperature <= cold_level_2_threshold)
            {
                await breather.apply_damage(cold_level_2_damage * cold_modifier, cold_damage_type, spread_damage: TRUE);
                breath_effect_prob = 50;
            }
            else if (breath_temperature <= cold_level_1_threshold)
            {
                await breather.apply_damage(cold_level_1_damage * cold_modifier, cold_damage_type, spread_damage: TRUE);
                breath_effect_prob = 25;
            }

            if (breath_temperature <= cold_level_1_threshold)
            {
                if (prob(sqrt(breath_effect_prob) * 4))
                {
                    to_chat(breather, span_warning(I18n.T("You feel [] in your []!", cold_message, name)));
                    if (prob(50))
                    {
                        await breather.emote("shiver");
                    }
                }
                if (prob(breath_effect_prob))
                {
                    // Breathing into your mask, no particle. We can add fogged up glasses later
                    if (Dm.truthy(breather.is_mouth_covered()))
                        return;
                    // Even though breathing via internals TECHNICALLY exhales into the environment, we'll still block it
                    if (Dm.truthy(breather.@internal) || Dm.truthy(breather.external))
                        return;
                    // emit_breath_particle(breather, /particles/fog/breath): GAP(lead): /particles/fog/breath
                    // (effects/particles/smoke.dm) is not declared; the breath fog is a visual (emit_breath_particle below
                    // is a deferred leaf).
                    Dm.Deferred("code/modules/surgery/organs/internal/lungs/_lungs.dm:830", "handle_breath_temperature: emit_breath_particle(breather, /particles/fog/breath) (visuals, M4b)");
                }
            }
        }

        if (!HAS_TRAIT(breather, TRAIT_RESISTHEAT)) // HEAT DAMAGE
        {
            float heat_modifier = ((datum_species)breather.dna.species).heatmod;
            float heat_message_prob = 0;
            if (breath_temperature >= heat_level_3_threshold)
            {
                await breather.apply_damage(heat_level_3_damage * heat_modifier, heat_damage_type, spread_damage: TRUE);
                heat_message_prob = 100;
                await breather.adjust_organ_loss(ORGAN_SLOT_LUNGS, TEMPERATURE_LUNG_DAMAGE);
            }
            else if (breath_temperature >= heat_level_2_threshold)
            {
                await breather.apply_damage(heat_level_2_damage * heat_modifier, heat_damage_type, spread_damage: TRUE);
                heat_message_prob = 50;
            }
            else if (breath_temperature >= heat_level_1_threshold)
            {
                await breather.apply_damage(heat_level_1_damage * heat_modifier, heat_damage_type, spread_damage: TRUE);
                heat_message_prob = 25;
            }
            if (breath_temperature >= heat_level_1_threshold)
            {
                if (prob(sqrt(heat_message_prob) * 4))
                    to_chat(breather, span_warning(I18n.T("You feel [] in your []!", hot_message, name)));
            }
        }
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:847
    public virtual void emit_breath_particle(mob_living_carbon_human breather = null, object particle_type = null)
    {
        if (!(ispath(particle_type, typeof(particles))))
            throw Dm.CRASH(assertion_message("code/modules/surgery/organs/internal/lungs/_lungs.dm", 848, "ispath(particle_type, /particles)"), "code/modules/surgery/organs/internal/lungs/_lungs.dm", 848);

        // :850-879: a particle holder at the mouth with the breath particles turned by the breather's dir, deleted after the
        // particles' lifespan. The fog of breath is a visual (particles, presentation); nothing in M2 reads the holder.
        Dm.Deferred("code/modules/surgery/organs/internal/lungs/_lungs.dm:850", "emit_breath_particle: the breath fog particles (visuals, M4b)");
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:882
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (Dm.truthy(failed) && !Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
        {
            failed = FALSE;
            return;
        }
        if (Dm.num(damage) >= low_threshold)
        {
            bool do_i_cough = SPT_PROB((Dm.num(damage) < high_threshold) ? 2.5f : 5, seconds_per_tick); // between : past high
            if (do_i_cough)
            {
                await owner.emote("cough");
            }
        }
        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)) && !IS_UNCONSCIOUS_OR_CRIT(owner))
        {
            owner.visible_message(span_danger(I18n.T("[] grabs [] throat, struggling for breath!", owner, owner.p_their())), span_userdanger(I18n.T("You suddenly feel like you can't breathe!")));
            failed = TRUE;
        }
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:895
    public override object get_availability(datum_species owner_species = null, mob_living owner_mob = null)
    {
        return owner_species.mutantlungs;
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:943
    public virtual async DmTask<object> hear_breath_noise(mob_living hearer = null)
    {
        return span_notice(I18n.T("[] lungs emit [].", owner.p_Their(), breath_noise));
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:1119
    public virtual void adjust_received_pressure_mult(object adjustment = null)
    {
        received_pressure_mult = max(received_pressure_mult + Dm.num(adjustment), 0);
        update_bronchodilation_alerts();
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:1124
    public virtual void set_received_pressure_mult(object new_value = null)
    {
        received_pressure_mult = max(Dm.num(new_value), 0);
        update_bronchodilation_alerts();
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:1131
    public virtual void update_bronchodilation_alerts()
    {
        if (owner == null)
            return;

        float initial_value = Dm.initial(this, (obj_item_organ_lungs p) => p.received_pressure_mult);

        // you wont really notice if youre only breathing a bit more or a bit less
        bool dilated = (received_pressure_mult > (initial_value + LUNG_CAPACITY_ALERT_BUFFER));
        bool constricted = (received_pressure_mult < (initial_value - LUNG_CAPACITY_ALERT_BUFFER));

        if (dilated)
        {
            // owner.throw_alert(ALERT_BRONCHODILATION, /atom/movable/screen/alert/bronchodilated): GAP(P3): the alert type /atom/movable/screen/alert/bronchodilated (_onclick/hud/screen_objects/alert.dm) is not declared in
            // this branch; the integration restores the call when P3 declares it.
            Dm.Deferred("code/modules/surgery/organs/internal/lungs/_lungs.dm:1142", "update_bronchodilation_alerts: owner.throw_alert(ALERT_BRONCHODILATION, /atom/movable/screen/alert/bronchodilated) (screen alerts, M2 P3)");
        }
        else if (constricted)
        {
            // owner.throw_alert(ALERT_BRONCHODILATION, /atom/movable/screen/alert/bronchoconstricted): GAP(P3): the alert type /atom/movable/screen/alert/bronchoconstricted (_onclick/hud/screen_objects/alert.dm) is not declared in
            // this branch; the integration restores the call when P3 declares it.
            Dm.Deferred("code/modules/surgery/organs/internal/lungs/_lungs.dm:1144", "update_bronchodilation_alerts: owner.throw_alert(ALERT_BRONCHODILATION, /atom/movable/screen/alert/bronchoconstricted) (screen alerts, M2 P3)");
        }
        else
        {
            owner.clear_alert(ALERT_BRONCHODILATION);
        }
    }
}

// tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:981
[DmPath("/obj/item/organ/lungs/smoker_lungs")]
public partial class obj_item_organ_lungs_smoker_lungs : obj_item_organ_lungs
{
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:982-987
    public obj_item_organ_lungs_smoker_lungs()
    {
        name = "smoker lungs";
        desc = "A pair of lungs that look sickly, a result from smoking a lot.";
        icon_state = "lungs_smoker";
        breath_noise = "an unsteady, wheezing rhythm";
        maxHealth = SMOKER_ORGAN_HEALTH;
        healing_factor = SMOKER_LUNG_HEALING;
    }
}

// tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:898 (M6 slice 2: the self-check's organ lines)
public partial class obj_item_organ_lungs : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:898
    public override object feel_for_damage(object self_aware = null)
    {
        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
        {
            if (Dm.truthy(self_aware))
                return span_boldwarning(I18n.T("Your lungs hurt madly[]!", HAS_TRAIT(owner, TRAIT_NOBREATH) ? "" : I18n.T(", and you can't breathe")));
            return span_boldwarning(I18n.T("It hurts madly[]!", HAS_TRAIT(owner, TRAIT_NOBREATH) ? "" : I18n.T(", and you can't breathe")));
        }
        if (Dm.num(damage) < low_threshold)
            return "";
        if (Dm.num(damage) < high_threshold)
        {
            if (Dm.truthy(self_aware))
                return span_warning(I18n.T("Your lungs feel tight[].", HAS_TRAIT(owner, TRAIT_NOBREATH) ? "" : I18n.T(", and breathing is harder")));
            return span_warning(I18n.T("It feels tight[].", HAS_TRAIT(owner, TRAIT_NOBREATH) ? "" : I18n.T(", and breathing is harder")));
        }
        if (Dm.truthy(self_aware))
            return span_boldwarning(I18n.T("Your lungs feel extremely tight[].", HAS_TRAIT(owner, TRAIT_NOBREATH) ? "" : I18n.T(", and every breath is a struggle")));
        return span_boldwarning(I18n.T("It feels extremely tight[].", HAS_TRAIT(owner, TRAIT_NOBREATH) ? "" : I18n.T(", and every breath is a struggle")));
    }
}

// tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:989  (silicons SI3: the exosuit fabricator prints it)
public partial class obj_item_organ_lungs_cybernetic : obj_item_organ_lungs
{
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:1000
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        // COOLDOWN_FINISHED(src, severe_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(Dm.num(severe_cooldown) <= world.time)) //So we cant just spam emp to kill people.
        {
            owner.losebreath += 20 / Dm.num(severity);
            severe_cooldown = world.time + 30 * SECONDS; // COOLDOWN_START(src, severe_cooldown, 30 SECONDS)
        }
        if (prob(emp_vulnerability / Dm.num(severity))) //Chance of permanent effects
            organ_flags = Dm.bitor(organ_flags, ORGAN_EMP); //Starts organ faliure - gonna need replacing soon.
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:1057 (gaps P3b: the goliath infusion's lungs breathe lavaland's air too)
public partial class obj_item_organ_lungs_lavaland : obj_item_organ_lungs
{
    // Normal oxygen is 21 kPa partial pressure, but SS13 humans can tolerate down
    // to 16 kPa. So it follows that ashwalkers, as humanoids, follow the same rules.
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:1065  #define GAS_TOLERANCE 5 (file-local, #undef at :1116)
    private const float GAS_TOLERANCE = 5;

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:1067
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_gas_mixture volumed_mix = Dm.@new<datum_gas_mixture>(BREATH_VOLUME * 1000); // to be safe
        datum_gas_mixture_immutable_planetary mix = SSair.parse_gas_string(LAVALAND_DEFAULT_ATMOS, typeof(datum_gas_mixture_immutable_planetary)) as datum_gas_mixture_immutable_planetary;

        volumed_mix.copy_from(mix);

        // Take a "breath" of the air
        datum_gas_mixture breath = volumed_mix.remove(Dm.num(volumed_mix.total_moles()) * BREATH_PERCENTAGE) as datum_gas_mixture;

        DmList breath_moles = breath.moles;

        breath.assert_gases(
            typeof(datum_gas_oxygen),
            typeof(datum_gas_plasma),
            typeof(datum_gas_carbon_dioxide),
            typeof(datum_gas_nitrogen),
            typeof(datum_gas_bz),
            typeof(datum_gas_miasma));

        float oxygen_pp = Dm.num(breath.get_breath_partial_pressure(breath_moles[typeof(datum_gas_oxygen)]));
        float nitrogen_pp = Dm.num(breath.get_breath_partial_pressure(breath_moles[typeof(datum_gas_nitrogen)]));
        float plasma_pp = Dm.num(breath.get_breath_partial_pressure(breath_moles[typeof(datum_gas_plasma)]));
        float carbon_dioxide_pp = Dm.num(breath.get_breath_partial_pressure(breath_moles[typeof(datum_gas_carbon_dioxide)]));
        float bz_pp = Dm.num(breath.get_breath_partial_pressure(breath_moles[typeof(datum_gas_bz)]));
        float miasma_pp = Dm.num(breath.get_breath_partial_pressure(breath_moles[typeof(datum_gas_miasma)]));

        safe_oxygen_min = max(0, oxygen_pp - GAS_TOLERANCE);
        safe_nitro_min = max(0, nitrogen_pp - GAS_TOLERANCE);
        safe_plasma_min = max(0, plasma_pp - GAS_TOLERANCE);

        // Increase plasma tolerance based on amount in base air
        safe_plasma_max += plasma_pp;

        // CO2 is always a waste gas, so none is required, but ashwalkers
        // tolerate the base amount plus tolerance*2 (humans tolerate only 10 pp)

        safe_co2_max = carbon_dioxide_pp + GAS_TOLERANCE * 2;

        // The lung tolerance against BZ is also increased the amount of BZ in the base air
        BZ_trip_balls_min += bz_pp;
        BZ_brain_damage_min += bz_pp;

        // Lungs adapted to a high miasma atmosphere do not process it, and breathe it back out
        if (miasma_pp != 0)
            suffers_miasma = FALSE;
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:913 (gaps OG1: the lung dilation in a health scan)
public partial class obj_item_organ_lungs : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:913
    public override object get_status_appendix(object scanpower = null, object add_tooltips = null)
    {
        float initial_pressure_mult = Dm.initial(this, (obj_item_organ_lungs p) => p.received_pressure_mult);
        if (received_pressure_mult == initial_pressure_mult)
            return null;

        string tooltip = null;
        string dilation_text = null;
        string beginning_text = I18n.T("Lung Dilation: ");
        if (received_pressure_mult > initial_pressure_mult) // higher than usual
        {
            beginning_text = span_blue(Dm.text("<b>[]</b>", beginning_text));
            dilation_text = span_blue(Dm.text("[]%", (received_pressure_mult * 100) - 100));
            tooltip = I18n.T("Subject's lungs are dilated and breathing more air than usual. Increases the effectiveness of healium and other gases.");
        }
        else
        {
            beginning_text = span_alert(Dm.text("<b>[]</b>", beginning_text));
            if (received_pressure_mult <= 0) // lethal
            {
                dilation_text = span_alert(Dm.text("<b>[]%</b>", received_pressure_mult * 100));
                tooltip = I18n.T("Subject's lungs are completely shut. Subject is unable to breathe and requires emergency surgery. If asthmatic, perform asthmatic bypass surgery and adminster albuterol inhalant. Otherwise, replace lungs.");
            }
            else
            {
                dilation_text = span_alert(Dm.text("[]%", received_pressure_mult * 100));
                tooltip = I18n.T("Subject's lungs are partially shut. If unable to breathe, administer a high-pressure internals tank or replace lungs. If asthmatic, inhaled albuterol or bypass surgery will likely help.");
            }
        }

        return beginning_text + Dm.str(conditional_tooltip(dilation_text, tooltip, add_tooltips));
    }
}

// tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:969 (gaps OG1)
public partial class obj_item_organ_lungs_slime : obj_item_organ_lungs
{
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:975
    [DmParams("breath", "breather_slime")]
    public override async DmTask<object> check_breath(datum_gas_mixture breath = null, mob_living_carbon_human breather = null)
    {
        object dot = await base.check_breath(breath, breather);
        if (Dm.truthy(breath?.moles[typeof(datum_gas_plasma)]))
        {
            object plasma_pp = breath.get_breath_partial_pressure(Dm.num(breath.moles[typeof(datum_gas_plasma)]));
            breather.adjust_blood_volume(0.2f * Dm.num(plasma_pp)); // 10/s when breathing literally nothing but plasma, which will suffocate you.
        }
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:1033 (gaps OG1)
public partial class obj_item_organ_lungs_cybernetic_surplus : obj_item_organ_lungs_cybernetic
{
    //surplus organs are so awful that they explode when removed, unless failing
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:1043
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_dangerous_organ_removal), /*surgical = */ TRUE));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:1047
    public override async DmTask<object> hear_breath_noise(mob_living hearer = null)
    {
        await DmTask.CompletedTask;
        return span_danger(I18n.T("[] lungs emit [].", owner.p_Their(), breath_noise));
    }
}

// tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:1150 (gaps OG1)
public partial class obj_item_organ_lungs_ethereal : obj_item_organ_lungs
{
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:1167
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_gas_reaction(typeof(datum_gas_water_vapor), while_present: nameof(consume_water));
        return dot;
    }

    /// H2O electrolysis
    // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:1172
    public virtual void consume_water(mob_living_carbon breather = null, datum_gas_mixture breath = null, object h2o_pp = null, object old_h2o_pp = null)
    {
        object gas_breathed = breath.moles[typeof(datum_gas_water_vapor)];
        breath.adjust_gas(typeof(datum_gas_water_vapor), -Dm.num(gas_breathed));
        DmList new_gases = Dm.list(Dm.kv(typeof(datum_gas_oxygen), gas_breathed), Dm.kv(typeof(datum_gas_hydrogen), Dm.num(gas_breathed) * 2));
        breath_out.adjust_multiple_gases(new_gases);
    }
}
