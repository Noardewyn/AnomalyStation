// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/slime/slime.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4): the extract crossbreeds and the slime's own HUD wait with xenobiology (rows).

/// Basically a big bag of nutrition that grows and splits. Slimes also have several life stages and a lot of potential targets.
// tg: code/modules/mob/living/basic/slime/slime.dm:7
public partial class mob_living_basic_slime : mob_living_basic
{
    private const int SLIME_EXTRA_SHOCK_COST = 3; // tg: code/modules/mob/living/basic/slime/slime.dm:1
    private const int SLIME_EXTRA_SHOCK_THRESHOLD = 8; // tg: code/modules/mob/living/basic/slime/slime.dm:2
    private const int SLIME_BASE_SHOCK_PERCENTAGE = 10; // tg: code/modules/mob/living/basic/slime/slime.dm:3
    private const int SLIME_SHOCK_PERCENTAGE_PER_LEVEL = 7; // tg: code/modules/mob/living/basic/slime/slime.dm:4

    /// Our list of slime types, generated only once
    // tg: code/modules/mob/living/basic/slime/slime.dm:80
    private static ref DmList possible_slime_types => ref TgState.Current.mob_living_basic_slime__possible_slime_types;
    // tg: code/modules/mob/living/basic/slime/slime.dm:91
    private static DmList pet_commands => TgState.Current.mob_living_basic_slime__pet_commands ??= Dm.list(typeof(datum_pet_command_idle), typeof(datum_pet_command_free), typeof(datum_pet_command_follow), typeof(datum_pet_command_attack_slime));

    // tg: code/modules/mob/living/basic/slime/slime.dm:108
    [DmParams("mapload", "new_type", "new_life_stage")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_type = Dm.arg(__extra, 0) ?? typeof(datum_slime_type_grey);
        object new_life_stage = Dm.arg(__extra, 1) ?? SLIME_LIFE_STAGE_BABY;
        object dot = base.Initialize(mapload);

        evolve_action = Dm.@new<datum_action_innate_slime_evolve>(this);
        Waiting.NoSleep(evolve_action.Grant(this));
        reproduce_action = Dm.@new<datum_action_innate_slime_reproduce>(this);
        Waiting.NoSleep(reproduce_action.Grant(this));

        if (possible_slime_types == null)
        {
            possible_slime_types = Dm.list();
            foreach (object slime_type_path in Dm.each(subtypesof(typeof(datum_slime_type))))
                possible_slime_types[slime_type_path] = Dm.@new((Type)slime_type_path);
        }

        set_life_stage(new_life_stage, TRUE);
        set_slime_type(new_type);
        Waiting.NoSleep(set_nutrition(SLIME_STARTING_NUTRITION));

        Waiting.NoSleep(AddComponent(typeof(datum_component_health_scaling_effects), Dm.named("min_health_slowdown", 2f)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_obeys_commands), pet_commands));

        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_health_examine), "It has some punctures in its flesh!", span_bold("It has severe punctures and tears in its flesh!"), 0.4f));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_SLIME));
        Waiting.NoSleep(AddElement(typeof(datum_element_soft_landing)));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_SLIME, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));

        add_traits(Dm.list(TRAIT_CANT_RIDE, TRAIT_CAN_MOUNT_HUMANS, TRAIT_VENTCRAWLER_ALWAYS), INNATE_TRAIT);

        RegisterSignal(this, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(on_slime_pre_attack));
        RegisterSignal(this, COMSIG_ATOM_ATTACK_HAND, nameof(on_attack_hand));

        (ai_controller as datum_ai_controller).set_blackboard_key(BB_SLIME_EVOLVE, evolve_action);
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_SLIME_REPRODUCE, reproduce_action);
        return dot;
    }

    // tg: code/modules/mob/living/basic/slime/slime.dm:150
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref evolve_action);
        QDEL_NULL(ref reproduce_action);
        slime_type = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/slime/slime.dm:228
    public override async DmTask adjust_nutrition(object change = null, float? forced = null)
    {
        await base.adjust_nutrition(change, forced);
        nutrition = Dm.min(Dm.num(nutrition), SLIME_MAX_NUTRITION);
    }

    // tg: code/modules/mob/living/basic/slime/slime.dm:232
    public override async DmTask set_nutrition(object set_to = null, float? forced = null)
    {
        await base.set_nutrition(set_to, forced);
        nutrition = Dm.min(Dm.num(nutrition), SLIME_MAX_NUTRITION);
    }

    // tg: code/modules/mob/living/basic/slime/slime.dm:236
    public override object get_fullness(object only_consumable = null)
    {
        return Dm.round((Dm.num(nutrition) / SLIME_MAX_NUTRITION) * NUTRITION_LEVEL_FAT);
    }

    // tg: code/modules/mob/living/basic/slime/slime.dm:241
    public override object update_name(float? updates = null)
    {
        regex slime_name_regex = TgState.Current.mob_living_basic_slime__slime_name_regex ??= Dm.@new<regex>("\\w+ (baby|adult) slime \\(\\d+\\)");
        if (Dm.truthy(slime_name_regex.Find(name)))
        {
            object slime_id = rand(1, 1000);
            name = Dm.text("[] [] slime ([])", slime_type.colour, life_stage, slime_id);
            real_name = name;
        }
        return base.update_name(updates);
    }

    // tg: code/modules/mob/living/basic/slime/slime.dm:250
    public override async DmTask<object> regenerate_icons()
    {
        cut_overlays();
        if (Dm.truthy(slime_type.transparent))
            alpha = SLIME_TRANSPARENCY_ALPHA;
        icon_living = Dm.text("[]-[]", slime_type.colour, life_stage);
        icon_dead = !Dm.truthy(cores) ? Dm.text("[]-cut", slime_type.colour) : Dm.text("[]-[]-dead", slime_type.colour, life_stage);
        if (stat != DEAD)
        {
            icon_state = Dm.text("[]", icon_living);
            if (Dm.truthy(current_mood) && !Dm.eq(current_mood, SLIME_MOOD_NONE) && stat == 0)
                add_overlay(Dm.text("aslime-[]", current_mood));
        }
        else
            icon_state = Dm.text("[]", icon_dead);
        return await base.regenerate_icons();
    }

    // tg: code/modules/mob/living/basic/slime/slime.dm:264
    public override async DmTask<object> mouse_drop_dragged(atom target_atom = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        if (isliving(target_atom) && target_atom != this && user == this)
        {
            mob_living food = target_atom as mob_living;
            if (Dm.truthy(can_feed_on(food)))
                await start_feeding(food);
        }
        return null;
    }

    // tg: code/modules/mob/living/basic/slime/slime.dm:270
    public override async DmTask resist_buckle()
    {
        if (isliving(buckled))
            await buckled.unbuckle_mob(this, force: TRUE);
    }

    // tg: code/modules/mob/living/basic/slime/slime.dm:274
    public override async DmTask<object> start_pulling(atom_movable pulled_atom = null, object state = null, object force = null, float? supress_message = null)
    {
        return null;
    }

    // tg: code/modules/mob/living/basic/slime/slime.dm:277
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        // DM's switch ranges run in order: a value on a shared bound takes the first range.
        if (powerlevel >= SLIME_MIN_POWER && powerlevel <= SLIME_EXTRA_SHOCK_COST)
            Dm.append(ref dot, "It is flickering gently with harmless levels of electrical activity.");
        else if (powerlevel >= SLIME_EXTRA_SHOCK_COST && powerlevel <= SLIME_MEDIUM_POWER)
            Dm.append(ref dot, "It is glowing brightly with medium levels electrical activity.");
        else if (powerlevel >= SLIME_MEDIUM_POWER && powerlevel <= SLIME_MAX_POWER)
            Dm.append(ref dot, "It is glowing alarmingly with high levels of electrical activity.");
        else if (powerlevel == SLIME_MAX_POWER)
            Dm.append(ref dot, span_boldwarning("It is radiating with massive levels of electrical activity!"));

        if (Dm.truthy(overcrowded))
            Dm.append(ref dot, span_warning("It seems too overcroweded to properly reproduce!"));
        return dot;
    }

    ///Changes the slime's current life state
    // tg: code/modules/mob/living/basic/slime/slime.dm:299
    public virtual void set_life_stage(object new_life_stage = null, object initial_ = null)
    {
        life_stage = (new_life_stage ?? SLIME_LIFE_STAGE_BABY) as string;
        if (Dm.eq(life_stage, SLIME_LIFE_STAGE_ADULT))
        {
            health = Dm.num(health) / 0.75f;
            maxHealth = Dm.num(maxHealth) / 0.75f;
            melee_damage_lower = Dm.num(melee_damage_lower) * 2;
            melee_damage_upper = Dm.num(melee_damage_upper) * 2;
            obj_damage = 15;
            wound_bonus = -90;
        }
        else if (!Dm.truthy(initial_))
        {
            health = Dm.num(health) * 0.75f;
            maxHealth = Dm.num(maxHealth) * 0.75f;
            melee_damage_lower = Dm.num(melee_damage_lower) * 0.5f;
            melee_damage_upper = Dm.num(melee_damage_upper) * 0.5f;
            obj_damage = Dm.num(initial(this, "obj_damage"));
            wound_bonus = Dm.num(initial(this, "wound_bonus"));
        }

        (ai_controller as datum_ai_controller).set_blackboard_key(BB_SLIME_LIFE_STAGE, life_stage);
        Waiting.NoSleep(update_mob_action_buttons());
    }

    ///Sets the slime's type, name and its icons
    // tg: code/modules/mob/living/basic/slime/slime.dm:323
    public virtual void set_slime_type(object new_type = null)
    {
        new_type ??= SLIME_TYPE_RANDOM;
        if (Dm.eq(new_type, SLIME_TYPE_RANDOM))
            new_type = pick(subtypesof(typeof(datum_slime_type)));

        slime_type = possible_slime_types[new_type] as datum_slime_type;
        update_name();
        Waiting.NoSleep(regenerate_icons());
    }

    ///randomizes the colour of a slime
    // tg: code/modules/mob/living/basic/slime/slime.dm:333
    [SignalHandler]
    public virtual object on_slime_pre_attack(mob_living_basic_slime our_slime = null, atom target = null, object proximity = null, object modifiers = null)
    {
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)) && isliving(target) && target != this)
        {
            if (Dm.truthy(our_slime.can_feed_on(target as mob_living)))
                Waiting.Detach(our_slime.start_feeding(target as mob_living));
            return COMPONENT_HOSTILE_NO_ATTACK;
        }

        if (isAI(target)) //The aI is not tasty!
        {
            target.balloon_alert(our_slime, "not tasty!");
            return COMPONENT_HOSTILE_NO_ATTACK;
        }

        if (our_slime.buckled == target) //If you try to attack the creature you are latched on, you instead cancel feeding
        {
            our_slime.stop_feeding();
            return COMPONENT_HOSTILE_NO_ATTACK;
        }

        if (iscyborg(target))
        {
            mob_living_silicon_robot borg_target = target as mob_living_silicon_robot;
            Waiting.NoSleep(borg_target.flash_act());
            do_sparks(5, TRUE, borg_target);
            float stunprob = our_slime.powerlevel * SLIME_SHOCK_PERCENTAGE_PER_LEVEL + SLIME_BASE_SHOCK_PERCENTAGE;
            if (prob(stunprob) && our_slime.powerlevel >= SLIME_EXTRA_SHOCK_COST)
            {
                our_slime.adjust_power_level(-SLIME_EXTRA_SHOCK_COST);
                Waiting.NoSleep(borg_target.apply_damage(our_slime.powerlevel * rand(6, 10), BRUTE, spread_damage: TRUE, wound_bonus: CANT_WOUND));
                borg_target.visible_message(span_danger(Dm.text("\\The [] shocks []!", our_slime, borg_target)), span_userdanger(Dm.text("\\The [] shocks you!", our_slime)));
            }
            else
                borg_target.visible_message(span_danger(Dm.text("\\The [] fails to hurt []!", our_slime, borg_target)), span_userdanger(Dm.text("\\The [] failed to hurt you!", our_slime)));

            return COMPONENT_HOSTILE_NO_ATTACK;
        }

        if (iscarbon(target) && our_slime.powerlevel > SLIME_MIN_POWER)
        {
            mob_living_carbon carbon_target = target as mob_living_carbon;
            float stunprob = our_slime.powerlevel * SLIME_SHOCK_PERCENTAGE_PER_LEVEL + SLIME_BASE_SHOCK_PERCENTAGE;  // 17 at level 1, 80 at level 10
            if (!prob(stunprob))
                return NONE; // normal attack

            carbon_target.visible_message(span_danger(Dm.text("\\The [] shocks []!", our_slime, carbon_target)), span_userdanger(Dm.text("\\The [] shocks you!", our_slime)));

            do_sparks(5, TRUE, carbon_target);
            float power = our_slime.powerlevel + rand(0, 3);
            Waiting.NoSleep(carbon_target.Paralyze(2 * SECONDS));
            Waiting.NoSleep(carbon_target.Knockdown(power * 2 * SECONDS));
            Waiting.NoSleep(carbon_target.set_stutter_if_lower(power * 2 * SECONDS));
            if (prob(stunprob) && our_slime.powerlevel >= SLIME_EXTRA_SHOCK_COST)
            {
                adjust_power_level(-SLIME_EXTRA_SHOCK_COST);
                Waiting.NoSleep(carbon_target.apply_damage(our_slime.powerlevel * rand(6, 10), BURN, spread_damage: TRUE, wound_bonus: CANT_WOUND));
            }
        }

        if (isslime(target))
        {
            if (target == our_slime)
                return COMPONENT_HOSTILE_NO_ATTACK;

            mob_living_basic_slime target_slime = target as mob_living_basic_slime;

            if (target_slime.buckled != null)
            {
                target_slime.stop_feeding(TRUE);
                visible_message(span_danger(Dm.text("[] pulls [] off!", our_slime, target_slime)),
                    span_danger(Dm.text("You pull [] off!", target_slime)));
                return NONE; // normal attack
            }

            bool is_adult_slime = Dm.eq(our_slime.life_stage, SLIME_LIFE_STAGE_ADULT);
            if (Dm.num(target_slime.nutrition) >= 100) //steal some nutrition. negval handled in life()
            {
                float stolen_nutrition = Dm.min(is_adult_slime ? 90 : 50, Dm.num(target_slime.nutrition));
                Waiting.NoSleep(target_slime.adjust_nutrition(-stolen_nutrition));
                Waiting.NoSleep(our_slime.adjust_nutrition(stolen_nutrition));
            }
            if (Dm.num(target_slime.health) > 0)
                Waiting.NoSleep(our_slime.adjust_brute_loss(is_adult_slime ? -20 : -10));
        }
        return null;
    }

    ///Spawns a crossed slimecore item
    // tg: code/modules/mob/living/basic/slime/slime.dm:414
    public virtual void spawn_corecross()
    {
        DmList crossbreeds = TgState.Current.mob_living_basic_slime__crossbreeds ??= (DmList)subtypesof(typeof(obj_item_slimecross));
        visible_message(span_danger(Dm.text("[] shudders, its mutated core consuming the rest of its body!", this)));
        playsound(this, Dm.resource("sound/effects/magic/smoke.ogg"), 50, TRUE);
        object selected_crossbreed_path = null;
        foreach (object crossbreed_path in Dm.each(crossbreeds))
        {
            if (Dm.eq(initial(crossbreed_path, "colour"), slime_type.colour) && Dm.eq(initial(crossbreed_path, "effect"), crossbreed_modification))
            {
                selected_crossbreed_path = crossbreed_path;
                break;
            }
        }
        if (selected_crossbreed_path != null)
            Dm.@new((Type)selected_crossbreed_path, loc);
        else
            visible_message(span_warning("The mutated core shudders, and collapses into a puddle, unable to maintain its form."));
        qdel(this);
    }

    ///Handles slime attacking restrictions, and any extra effects that would trigger
    // tg: code/modules/mob/living/basic/slime/slime.dm:431
    public virtual object try_extract_cores(object count = null)
    {
        float how_many = Dm.num(count ?? 1f);
        if (stat != DEAD)
            return FALSE;
        if (how_many <= 0 || cores < how_many)
            return FALSE;
        float core_count = Dm.min(how_many, cores);
        for (int i = 1; i <= core_count; i++)
        {
            Dm.@new(slime_type.core_type, loc);
            cores--;
        }
        Waiting.NoSleep(regenerate_icons());
        return TRUE;
    }

    ///Makes the slime peaceful and content
    // tg: code/modules/mob/living/basic/slime/slime.dm:443
    public virtual void set_pacified_behaviour()
    {
        hunger_disabled = TRUE;
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_SLIME_RABID, FALSE);
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_SLIME_HUNGER_DISABLED, TRUE);
        Waiting.NoSleep(set_nutrition(SLIME_STARTING_NUTRITION));
    }

    ///Makes the slime angry and hungry
    // tg: code/modules/mob/living/basic/slime/slime.dm:450
    public virtual void set_enraged_behaviour()
    {
        hunger_disabled = FALSE;
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_SLIME_HUNGER_DISABLED, FALSE);
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_SLIME_RABID, TRUE);
    }

    ///Makes the slime behave normally
    // tg: code/modules/mob/living/basic/slime/slime.dm:456
    public virtual void set_default_behaviour()
    {
        hunger_disabled = FALSE;
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_SLIME_HUNGER_DISABLED, FALSE);
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_SLIME_RABID, FALSE);
    }
}

// tg: code/modules/mob/living/basic/slime/slime.dm:160
public partial class mob_living_basic_slime_random : mob_living_basic_slime
{
    // tg: code/modules/mob/living/basic/slime/slime.dm:162
    [DmParams("mapload", "new_colour", "new_life_stage")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        // Random slime subtype
        return base.Initialize(mapload, SLIME_TYPE_RANDOM, prob(50) ? SLIME_LIFE_STAGE_ADULT : SLIME_LIFE_STAGE_BABY);
    }
}

// tg: code/modules/mob/living/basic/slime/slime.dm:165
public partial class mob_living_basic_slime_pet : mob_living_basic_slime
{
    // tg: code/modules/mob/living/basic/slime/slime.dm:169
    [DmParams("mapload", "new_colour", "new_life_stage")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_pacified_behaviour();
        return dot;
    }
}

// tg: code/modules/mob/living/basic/slime/slime.dm:174
public partial class mob_living_basic_slime_hilbert : mob_living_basic_slime
{
    // tg: code/modules/mob/living/basic/slime/slime.dm:177
    [DmParams("mapload", "new_colour", "new_life_stage")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, typeof(datum_slime_type_bluespace));
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_SLIME_RABID, TRUE);
        return dot;
    }
}

// tg: code/modules/mob/living/basic/slime/slime_type.dm:1
public partial class datum_slime_type : datum
{
    // tg: code/modules/mob/living/basic/slime/slime_type.dm:13
    public override object Destroy(float? force = null)
    {
        if (!Dm.truthy(force))
        {
            stack_trace("Something tried to delete a \"/datum/slime_type\", this should never happen as could lead to slime colors being broken!", "code/modules/mob/living/basic/slime/slime_type.dm", 15);
            return QDEL_HINT_LETMELIVE;
        }
        return base.Destroy(force);
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_slime__possible_slime_types;
    internal DmList mob_living_basic_slime__pet_commands;
    internal DmList mob_living_basic_slime__crossbreeds;
    internal regex mob_living_basic_slime__slime_name_regex;
}
