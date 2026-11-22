// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_healing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// tg: code/modules/surgery/operations/operation_healing.dm:8
public partial class datum_surgery_operation_basic_tend_wounds : datum_surgery_operation_basic
{
    /// Allow brute healing operation
    // tg: code/modules/surgery/operations/operation_healing.dm:2  #define BRUTE_SURGERY (1<<0)
    private const float BRUTE_SURGERY = 1 << 0;
    /// Allow burn healing operation
    // tg: code/modules/surgery/operations/operation_healing.dm:4  #define BURN_SURGERY (1<<1)
    private const float BURN_SURGERY = 1 << 1;
    /// Allow combo healing operation
    // tg: code/modules/surgery/operations/operation_healing.dm:6  #define COMBO_SURGERY (1<<2)
    private const float COMBO_SURGERY = 1 << 2;

    // tg: code/modules/surgery/operations/operation_healing.dm:35
    public override object all_required_strings()
    {
        return Dm.add(base.all_required_strings(), Dm.list(I18n.T("the patient must have brute or burn damage")));
    }

    // tg: code/modules/surgery/operations/operation_healing.dm:38
    public override object state_check(atom_movable operating_on = null)
    {
        mob_living patient = (mob_living)operating_on;
        return Dm.num(Dm.num(patient.get_brute_loss()) > 0 || Dm.num(patient.get_fire_loss()) > 0);
    }

    // tg: code/modules/surgery/operations/operation_healing.dm:41
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_storage_medkit));
    }

    // tg: code/modules/surgery/operations/operation_healing.dm:44
    public override async DmTask<object> get_radial_options(atom_movable operating_on = null, obj_item tool = null, object operating_zone = null)
    {
        mob_living patient = (mob_living)operating_on;
        DmList options = Dm.list();

        if (Dm.truthy(Dm.bitand(can_heal, COMBO_SURGERY)))
        {
            datum_radial_menu_choice all_healing = LAZYACCESS(cached_healing_options, Dm.text("[]", COMBO_SURGERY)) as datum_radial_menu_choice;
            if (all_healing == null)
            {
                all_healing = Dm.@new<datum_radial_menu_choice>();
                all_healing.image = image(typeof(obj_item_storage_medkit_advanced));
                all_healing.name = I18n.T("tend bruises and burns");
                all_healing.info = I18n.T("Heal a patient's superficial bruises, cuts, and burns.");
                LAZYSET(ref cached_healing_options, Dm.text("[]", COMBO_SURGERY), all_healing);
            }

            options[all_healing] = Dm.list(
                Dm.kv(Dm.text("[]", OPERATION_ACTION), "heal"),
                Dm.kv(Dm.text("[]", OPERATION_BRUTE_HEAL), healing_amount),
                Dm.kv(Dm.text("[]", OPERATION_BURN_HEAL), healing_amount),
                Dm.kv(Dm.text("[]", OPERATION_BRUTE_MULTIPLIER), healing_multiplier),
                Dm.kv(Dm.text("[]", OPERATION_BURN_MULTIPLIER), healing_multiplier));
        }

        if (Dm.truthy(Dm.bitand(can_heal, BRUTE_SURGERY)) && Dm.num(patient.get_brute_loss()) > 0)
        {
            datum_radial_menu_choice brute_healing = LAZYACCESS(cached_healing_options, Dm.text("[]", BRUTE_SURGERY)) as datum_radial_menu_choice;
            if (brute_healing == null)
            {
                brute_healing = Dm.@new<datum_radial_menu_choice>();
                brute_healing.image = image(typeof(obj_item_storage_medkit_brute));
                brute_healing.name = I18n.T("tend bruises");
                brute_healing.info = I18n.T("Heal a patient's superficial bruises and cuts.");
                LAZYSET(ref cached_healing_options, Dm.text("[]", BRUTE_SURGERY), brute_healing);
            }

            options[brute_healing] = Dm.list(
                Dm.kv(Dm.text("[]", OPERATION_ACTION), "heal"),
                Dm.kv(Dm.text("[]", OPERATION_BRUTE_HEAL), healing_amount),
                Dm.kv(Dm.text("[]", OPERATION_BRUTE_MULTIPLIER), healing_multiplier));
        }

        if (Dm.truthy(Dm.bitand(can_heal, BURN_SURGERY)) && Dm.num(patient.get_fire_loss()) > 0)
        {
            datum_radial_menu_choice burn_healing = LAZYACCESS(cached_healing_options, Dm.text("[]", BURN_SURGERY)) as datum_radial_menu_choice;
            if (burn_healing == null)
            {
                burn_healing = Dm.@new<datum_radial_menu_choice>();
                burn_healing.image = image(typeof(obj_item_storage_medkit_fire));
                burn_healing.name = I18n.T("tend burns");
                burn_healing.info = I18n.T("Heal a patient's superficial burns.");
                LAZYSET(ref cached_healing_options, Dm.text("[]", BURN_SURGERY), burn_healing);
            }

            options[burn_healing] = Dm.list(
                Dm.kv(Dm.text("[]", OPERATION_ACTION), "heal"),
                Dm.kv(Dm.text("[]", OPERATION_BURN_HEAL), healing_amount),
                Dm.kv(Dm.text("[]", OPERATION_BURN_MULTIPLIER), healing_multiplier));
        }

        return options;
    }

    // tg: code/modules/surgery/operations/operation_healing.dm:95
    public override object can_loop(mob_living patient = null, atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        object dot = base.can_loop(patient, operating_on, surgeon, tool, operation_args);
        if (!Dm.truthy(dot))
            return FALSE;
        bool brute_heal = Dm.num(operation_args[OPERATION_BRUTE_HEAL]) > 0;
        bool burn_heal = Dm.num(operation_args[OPERATION_BURN_HEAL]) > 0;
        if (brute_heal && burn_heal)
            return Dm.num(Dm.num(patient.get_brute_loss()) > 0 || Dm.num(patient.get_fire_loss()) > 0);
        else if (brute_heal)
            return Dm.num(Dm.num(patient.get_brute_loss()) > 0);
        else if (burn_heal)
            return Dm.num(Dm.num(patient.get_fire_loss()) > 0);
        return FALSE;
    }

    // tg: code/modules/surgery/operations/operation_healing.dm:109
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        mob_living patient = (mob_living)operating_on;
        string woundtype;
        bool brute_heal = Dm.num(operation_args[OPERATION_BRUTE_HEAL]) > 0;
        bool burn_heal = Dm.num(operation_args[OPERATION_BURN_HEAL]) > 0;
        if (brute_heal && burn_heal)
            woundtype = I18n.T("wounds");
        else if (brute_heal)
            woundtype = I18n.T("bruises");
        else //why are you trying to 0,0...?
            woundtype = I18n.T("burns");
        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("You attempt to patch some of []'s [].", patient, woundtype)),
            span_notice(I18n.T("[] attempts to patch some of []'s [].", surgeon, patient, woundtype)),
            span_notice(I18n.T("[] attempts to patch some of []'s [].", surgeon, patient, woundtype)));
        await display_pain(patient, I18n.T("Your [] sting like hell!", woundtype));
    }

    // tg: code/modules/surgery/operations/operation_healing.dm:128  #define CONDITIONAL_DAMAGE_MESSAGE(brute, burn, combo_msg, brute_msg, burn_msg) "[(brute > 0 && burn > 0) ? combo_msg : (brute > 0 ? brute_msg : burn_msg)]"
    private static string CONDITIONAL_DAMAGE_MESSAGE(object brute, object burn, string combo_msg, string brute_msg, string burn_msg)
        => Dm.text("[]", (Dm.num(brute) > 0 && Dm.num(burn) > 0) ? combo_msg : (Dm.num(brute) > 0 ? brute_msg : burn_msg));

    /// Returns a string letting the surgeon know roughly how much longer the surgery is estimated to take at the going rate
    // tg: code/modules/surgery/operations/operation_healing.dm:131
    public virtual object get_progress(mob_living surgeon = null, mob_living patient = null, object brute_healed = null, object burn_healed = null)
    {
        float estimated_remaining_steps = 0;
        if (Dm.num(brute_healed) > 0)
            estimated_remaining_steps = max(0, (Dm.num(patient.get_brute_loss()) / Dm.num(brute_healed)));
        if (Dm.num(burn_healed) > 0)
            estimated_remaining_steps = max(estimated_remaining_steps, (Dm.num(patient.get_fire_loss()) / Dm.num(burn_healed))); // whichever is higher between brute or burn steps

        object progress_text = null;

        if (Dm.truthy(show_stats(surgeon, patient)))
        {
            if (Dm.num(brute_healed) > 0 && Dm.num(patient.get_brute_loss()) > 0)
                progress_text = Dm.add(progress_text, I18n.T(". Remaining brute: <font color='#ff3333'>[]</font>", patient.get_brute_loss()));
            if (Dm.num(burn_healed) > 0 && Dm.num(patient.get_fire_loss()) > 0)
                progress_text = Dm.add(progress_text, I18n.T(". Remaining burn: <font color='#ff9933'>[]</font>", patient.get_fire_loss()));
            return progress_text;
        }

        float sw__1 = estimated_remaining_steps;
        if (sw__1 <= 1)
            return null;
        else if (sw__1 >= 1 && sw__1 <= 3)
            progress_text = Dm.add(progress_text, I18n.T(", finishing up the last few []", CONDITIONAL_DAMAGE_MESSAGE(brute_healed, burn_healed, I18n.T("signs of damage"), I18n.T("scrapes"), I18n.T("burn marks"))));
        else if (sw__1 >= 3 && sw__1 <= 6)
            progress_text = Dm.add(progress_text, I18n.T(", counting down the last few [] left to treat", CONDITIONAL_DAMAGE_MESSAGE(brute_healed, burn_healed, I18n.T("patches of trauma"), I18n.T("bruises"), I18n.T("blisters"))));
        else if (sw__1 >= 6 && sw__1 <= 9)
            progress_text = Dm.add(progress_text, I18n.T(", continuing to plug away at [] extensive []", patient.p_their(), CONDITIONAL_DAMAGE_MESSAGE(brute_healed, burn_healed, I18n.T("injuries"), I18n.T("rupturing"), I18n.T("roasting"))));
        else if (sw__1 >= 9 && sw__1 <= 12)
            progress_text = Dm.add(progress_text, I18n.T(", steadying yourself for the long surgery ahead"));
        else if (sw__1 >= 12 && sw__1 <= 15)
            progress_text = Dm.add(progress_text, I18n.T(", though [] still look[] more like [] than a person", patient.p_they(), patient.p_s(), CONDITIONAL_DAMAGE_MESSAGE(brute_healed, burn_healed, I18n.T("smooshed baby food"), I18n.T("ground beef"), I18n.T("burnt steak"))));
        else if (sw__1 >= 15)
            progress_text = Dm.add(progress_text, I18n.T(", though you feel like you're barely making a dent in treating [] [] body", patient.p_their(), CONDITIONAL_DAMAGE_MESSAGE(brute_healed, burn_healed, I18n.T("broken"), I18n.T("pulped"), I18n.T("charred"))));

        return progress_text;
    }

    /// Checks whether we show precise damage stats during the surgery
    // tg: code/modules/surgery/operations/operation_healing.dm:167
    public virtual object show_stats(mob_living surgeon = null, mob_living patient = null)
    {
        if (Dm.truthy(surgeon.is_holding_item_of_type(typeof(obj_item_healthanalyzer))))
            return TRUE;
        foreach (obj_machinery_vitals_reader vitals in Dm.each<obj_machinery_vitals_reader>(view(4, patient)))
        {
            if (vitals.patient == patient)
                return TRUE;
        }
        foreach (obj_machinery_computer_operating op_pc in Dm.each<obj_machinery_computer_operating>(range(1, patient)))
        {
            if (op_pc.table?.patient == patient)
                return TRUE;
        }
        foreach (obj_item_mod_control modsuit in Dm.each<obj_item_mod_control>(surgeon.get_equipped_items()))
        {
            if (Dm.truthy(modsuit.active) && istype(modsuit.selected_module, typeof(obj_item_mod_module_health_analyzer)))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/surgery/operations/operation_healing.dm:182
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        mob_living patient = (mob_living)operating_on;
        string user_msg = I18n.T("You succeed in fixing some of []'s wounds", patient); //no period, add initial space to "addons"
        string target_msg = I18n.T("[] fixes some of []'s wounds", surgeon, patient); //see above

        float brute_healed = Dm.num(operation_args[OPERATION_BRUTE_HEAL]);
        float burn_healed = Dm.num(operation_args[OPERATION_BURN_HEAL]);

        float dead_multiplier = Dm.eq(patient.stat, DEAD) ? 0.2f : 1.0f;
        float accessibility_modifier = 1.0f;
        if (!Dm.truthy(patient.is_location_accessible(BODY_ZONE_CHEST, IGNORED_OPERATION_CLOTHING_SLOTS)))
        {
            accessibility_modifier = 0.55f;
            user_msg += I18n.T(" as best as you can while [] [] clothing on", patient.p_they(), patient.p_have());
            target_msg += I18n.T(" as best as [] can while [] [] clothing on", surgeon.p_they(), patient.p_they(), patient.p_have());
        }

        float brute_multiplier = Dm.num(operation_args[OPERATION_BRUTE_MULTIPLIER]) * dead_multiplier * accessibility_modifier;
        float burn_multiplier = Dm.num(operation_args[OPERATION_BURN_MULTIPLIER]) * dead_multiplier * accessibility_modifier;

        brute_healed += Dm.num(round(Dm.num(patient.get_brute_loss()) * brute_multiplier, DAMAGE_PRECISION));
        burn_healed += Dm.num(round(Dm.num(patient.get_fire_loss()) * burn_multiplier, DAMAGE_PRECISION));

        await patient.heal_bodypart_damage(brute_healed, burn_healed);

        user_msg += Dm.text("[]", get_progress(surgeon, patient, brute_healed, burn_healed));

        if (HAS_MIND_TRAIT(surgeon, TRAIT_MORBID) && !Dm.eq(patient.stat, DEAD)) //Morbid folk don't care about tending the dead as much as tending the living
            await surgeon.add_mood_event("morbid_tend_wounds", typeof(datum_mood_event_morbid_tend_wounds));

        display_results(
            surgeon,
            patient,
            span_notice(Dm.text("[].", user_msg)),
            span_notice(Dm.text("[].", target_msg)),
            span_notice(Dm.text("[].", target_msg)));
        return null;
    }

    // tg: code/modules/surgery/operations/operation_healing.dm:215
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        mob_living patient = (mob_living)operating_on;
        display_results(
            surgeon,
            patient,
            span_warning(I18n.T("You screwed up!")),
            span_warning(I18n.T("[] screws up!", surgeon)),
            span_notice(I18n.T("[] fixes some of []'s wounds.", surgeon, patient)),
            target_detailed: TRUE);
        float brute_dealt = Dm.num(operation_args[OPERATION_BRUTE_HEAL]) * 0.8f;
        float burn_dealt = Dm.num(operation_args[OPERATION_BURN_HEAL]) * 0.8f;
        float brute_multiplier = Dm.num(operation_args[OPERATION_BRUTE_MULTIPLIER]) * 0.5f;
        float burn_multiplier = Dm.num(operation_args[OPERATION_BURN_MULTIPLIER]) * 0.5f;

        brute_dealt += Dm.num(round(Dm.num(patient.get_brute_loss()) * brute_multiplier, 0.1f));
        burn_dealt += Dm.num(round(Dm.num(patient.get_fire_loss()) * burn_multiplier, 0.1f));

        await patient.take_bodypart_damage(brute_dealt, burn_dealt, wound_bonus: CANT_WOUND);
    }
}
