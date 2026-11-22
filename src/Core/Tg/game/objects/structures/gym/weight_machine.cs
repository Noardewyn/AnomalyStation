// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/gym/weight_machine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST4 (D333): the file whole. The chest press and the incline bench press: buckled onto it, a mob gets the Work out action; a
// rep takes eight seconds of the machine's flick, its weights' overlay and the user bobbing, with tg's creak, the stamina drain of
// a rep count by fitness (heavy gravity, a spine implant and the strong trait change it), five nutrition burnt, the clumsy and the
// drunk hurt by the bang; wrenched loose and pried apart with the right button. The athletics skill (the clumsy chance and the rep
// count by fitness), the athletics experience and the exercised status effect a rep gives came with gaps SL1 (D333). The var block is
// generated (D111).

// tg: code/game/objects/structures/gym/weight_machine.dm:5
public partial class obj_structure_weightmachine : obj_structure
{
    // tg: code/game/objects/structures/gym/weight_machine.dm:1
    private const float WORKOUT_XP = 5;
    // tg: code/game/objects/structures/gym/weight_machine.dm:2
    private const float EXERCISE_STATUS_DURATION = 15 * SECONDS;
    // tg: code/game/objects/structures/gym/weight_machine.dm:3
    private const float SAFE_DRUNK_LEVEL = 39;
    /// roughly 8 seconds for 1 workout rep
    // tg: code/game/objects/structures/gym/weight_machine.dm:184
    private const float WORKOUT_LENGTH = 8;

    // tg: code/game/objects/structures/gym/weight_machine.dm:52
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        weight_action = Dm.@new<datum_action_push_weights>(this);
        weight_action.weightpress = this;

        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_tools), TgState.Current.obj_structure_weightmachine__Initialize__tool_behaviors));
        return dot;
    }

    // tg: code/game/objects/structures/gym/weight_machine.dm:71
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref weight_action);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/gym/weight_machine.dm:75
    public override void buckle_feedback(mob_living being_buckled = null, mob buckler = null)
    {
        if (HAS_TRAIT(being_buckled, TRAIT_RESTRAINED))
        {
            base.buckle_feedback(being_buckled, buckler);
            return;
        }

        if (being_buckled == buckler)
            being_buckled.visible_message(
                span_notice(I18n.T("[] lays down on [].", buckler, this)),
                span_notice(I18n.T("You lay down on [].", this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        else
            being_buckled.visible_message(
                span_notice(I18n.T("[] lays [] down on [].", buckler, being_buckled, this)),
                span_notice(I18n.T("[] lays you down on [].", buckler, this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
    }

    // tg: code/game/objects/structures/gym/weight_machine.dm:92
    public override void unbuckle_feedback(mob_living unbuckled_mob = null, mob unbuckler = null)
    {
        if (HAS_TRAIT(unbuckled_mob, TRAIT_RESTRAINED))
        {
            base.unbuckle_feedback(unbuckled_mob, unbuckler);
            return;
        }

        if (unbuckled_mob == unbuckler)
            unbuckled_mob.visible_message(
                span_notice(I18n.T("[] gets up from [].", unbuckler, this)),
                span_notice(I18n.T("You get up from [].", this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        else
            unbuckled_mob.visible_message(
                span_notice(I18n.T("[] pulls [] up from [].", unbuckler, unbuckled_mob, this)),
                span_notice(I18n.T("[] pulls you up from [].", unbuckler, this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
    }

    // tg: code/game/objects/structures/gym/weight_machine.dm:109
    public override async DmTask<object> buckle_mob(mob_living M = null, float? force = null, float? check_loc = null, float? buckle_mob_flags = null)
    {
        object dot = await base.buckle_mob(M, force, check_loc, buckle_mob_flags);
        await weight_action.Grant(M);
        return dot;
    }

    // tg: code/game/objects/structures/gym/weight_machine.dm:113
    public override async DmTask<object> unbuckle_mob(mob_living buckled_mob = null, float? force = null, float? can_fall = null)
    {
        object dot = await base.unbuckle_mob(buckled_mob, force, can_fall);
        await weight_action.Remove(buckled_mob);
        return dot;
    }

    // tg: code/game/objects/structures/gym/weight_machine.dm:117
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this);
        balloon_alert(user, Dm.truthy(anchored) ? I18n.T("unsecured") : I18n.T("secured"));
        anchored = Dm.truthy(anchored) ? FALSE : TRUE;
        return TRUE;
    }

    // tg: code/game/objects/structures/gym/weight_machine.dm:123
    public override async DmTask<object> crowbar_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("still secured!"));
            return FALSE;
        }
        tool.play_tool_sound(this);
        balloon_alert(user, I18n.T("deconstructing..."));
        if (!Dm.truthy(await do_after(user, 10 * SECONDS, target: this)))
            return FALSE;
        Dm.@new<obj_item_stack_sheet_iron>(get_turf(this), 5);
        Dm.@new<obj_item_stack_rods>(get_turf(this), 2);
        Dm.@new<obj_item_chair>(get_turf(this));
        qdel(this);
        return TRUE;
    }

    // tg: code/game/objects/structures/gym/weight_machine.dm:137
    public virtual async DmTask perform_workout(mob_living user = null)
    {
        if (user.nutrition <= NUTRITION_LEVEL_STARVING)
        {
            user.balloon_alert(user, I18n.T("too hungry to workout!"));
            return;
        }

        user.balloon_alert_to_viewers(Dm.text("[]", I18n.T(Dm.str(pick(TgState.Current.obj_structure_weightmachine__more_weight)))));
        START_PROCESSING(SSobj, this);

        if (Dm.truthy(await do_after(user, 8 * SECONDS, this)) && Dm.truthy(user.has_gravity()))
        {
            // with enough dedication, even clowns can overcome their handicaps
            float clumsy_chance = 30 - (Dm.num(user.mind.get_skill_level(typeof(datum_skill_athletics))) * 5);
            if (HAS_TRAIT(user, TRAIT_CLUMSY) && prob(clumsy_chance))
            {
                playsound(this, Dm.resource("sound/effects/bang.ogg"), 50, TRUE);
                to_chat(user, span_warning(I18n.T("Your hand slips, causing \\the [] to smash you!", this)));
                await user.take_bodypart_damage(Dm.num(rand(2, 5)));
                end_workout();
                return;
            }

            // awlways a chance for a person not to fail horribly when drunk
            float drunk = Dm.num(user.get_drunk_amount());
            if (drunk > SAFE_DRUNK_LEVEL && prob(Math.Min(drunk, 99)))
            {
                playsound(this, Dm.resource("sound/effects/bang.ogg"), 50, TRUE);
                to_chat(user, span_warning(drunk_message));
                await user.take_bodypart_damage(Dm.num(rand(5, 10)), wound_bonus: 10);
                end_workout();
                return;
            }

            if (issilicon(user))
                user.balloon_alert(user, I18n.T(Dm.str(pick(TgState.Current.obj_structure_weightmachine__finished_silicon_message))));
            else
                user.balloon_alert(user, I18n.T(Dm.str(pick(TgState.Current.obj_structure_weightmachine__finished_message))));

            await user.adjust_nutrition(-5); // feel the burn

            if (iscarbon(user))
            {
                float gravity_modifier = Dm.num(user.has_gravity()) > STANDARD_GRAVITY ? 2 : 1;
                // remember the real xp gain is from sleeping after working out
                user.mind.adjust_experience(typeof(datum_skill_athletics), WORKOUT_XP * gravity_modifier);
                await user.apply_status_effect(typeof(datum_status_effect_exercised), EXERCISE_STATUS_DURATION);
            }
        }

        end_workout();
    }

    // tg: code/game/objects/structures/gym/weight_machine.dm:178
    public virtual void end_workout()
    {
        playsound(this, Dm.resource("sound/machines/click.ogg"), 60, TRUE);
        STOP_PROCESSING(SSobj, this);
        icon_state = Dm.initial(this, nameof(icon_state)) as string;
    }

    // tg: code/game/objects/structures/gym/weight_machine.dm:186
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(has_buckled_mobs()))
        {
            end_workout();
            return FALSE;
        }
        mutable_appearance workout = mutable_appearance(icon, Dm.text("[]-o", base_icon_state), ABOVE_MOB_LAYER, this, appearance_flags: KEEP_APART) as mutable_appearance;
        workout = color_atom_overlay(workout) as mutable_appearance;
        flick_overlay_view(workout, 0.8f * SECONDS);
        flick(Dm.text("[]-u", base_icon_state), this);
        mob_living user = buckled_mobs[1] as mob_living;
        animate(user, Dm.named("pixel_z", pixel_shift_z), Dm.named("time", WORKOUT_LENGTH * 0.5f), Dm.named("flags", Dm.bitor(ANIMATION_PARALLEL, ANIMATION_RELATIVE)));
        animate(Dm.named("pixel_z", -pixel_shift_z), Dm.named("time", WORKOUT_LENGTH * 0.5f), Dm.named("flags", ANIMATION_PARALLEL));
        playsound(user, Dm.resource("sound/machines/creak.ogg"), 60, TRUE);

        if (!iscarbon(user) || user.mind == null)
            return TRUE;

        float affected_gravity = Dm.num(user.has_gravity());
        if (affected_gravity == 0)
            return TRUE; // No weight? I could do this all day
        float gravity_modifier = affected_gravity > STANDARD_GRAVITY ? 0.75f : 1;
        // the amount of workouts you can do before you hit stamcrit
        float workout_reps = Dm.num(TgState.Current.obj_structure_weightmachine__total_workout_reps[user.mind.get_skill_level(typeof(datum_skill_athletics))]) * gravity_modifier;
        // total stamina drain of 1 workout calculated based on the workout length
        float stamina_exhaustion = FLOOR(user.maxHealth / workout_reps / WORKOUT_LENGTH, 0.1f);

        if (user.get_organ_slot(ORGAN_SLOT_SPINE) is obj_item_organ_cyberimp_chest_spine potential_spine)
            stamina_exhaustion *= potential_spine.athletics_boost_multiplier;

        if (HAS_TRAIT(user, TRAIT_STRENGTH)) //The strong get reductions to stamina damage taken while exercising
            stamina_exhaustion *= 0.5f;

        Waiting.NoSleep(user.adjust_stamina_loss(stamina_exhaustion * Dm.num(seconds_per_tick)));

        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/gym/weight_machine.dm:27  var/static/list/total_workout_reps
    internal DmList obj_structure_weightmachine__total_workout_reps = Dm.list(3, 4, 4, 5, 6, 6, 7);

    // tg: code/game/objects/structures/gym/weight_machine.dm:30  var/static/list/more_weight
    internal DmList obj_structure_weightmachine__more_weight = Dm.list(
        "pushing it to the limit!",
        "going into overdrive!",
        "burning with determination!",
        "rising up to the challenge!",
        "getting strong now!",
        "getting ripped!");

    // tg: code/game/objects/structures/gym/weight_machine.dm:39  var/static/list/finished_message
    internal DmList obj_structure_weightmachine__finished_message = Dm.list(
        "You feel stronger!",
        "You feel like you can take on the world!",
        "You feel robust!",
        "You feel indestructible!");

    // tg: code/game/objects/structures/gym/weight_machine.dm:45  var/static/list/finished_silicon_message
    internal DmList obj_structure_weightmachine__finished_silicon_message = Dm.list(
        "You feel nothing!",
        "No pain, no gain!",
        "Chassis hardness rating... Unchanged.",
        "You feel the exact same. Nothing.");

    // tg: code/game/objects/structures/gym/weight_machine.dm:58  /obj/structure/weightmachine/Initialize's var/static/list/tool_behaviors
    internal DmList obj_structure_weightmachine__Initialize__tool_behaviors = Dm.list(
        Dm.kv(TOOL_CROWBAR, Dm.list(Dm.kv(SCREENTIP_CONTEXT_RMB, "Deconstruct"))),
        Dm.kv(TOOL_WRENCH, Dm.list(Dm.kv(SCREENTIP_CONTEXT_RMB, "Anchor"))));
}
