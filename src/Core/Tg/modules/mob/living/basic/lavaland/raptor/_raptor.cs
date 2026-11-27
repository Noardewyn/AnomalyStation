// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/raptor/_raptor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


public partial class datum_controller_global_vars
{
    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:1  GLOBAL_LIST_INIT(raptor_inherit_traits, ...)
    public DmList raptor_inherit_traits;
    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:8  GLOBAL_LIST_EMPTY(raptor_population)
    public DmList raptor_population;

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:1  (GLOBAL_MANAGED(raptor_inherit_traits, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalraptor_inherit_traits()
    {
        raptor_inherit_traits = Dm.list(
            Dm.kv(BB_BASIC_DEPRESSED, "Depressed"),
            Dm.kv(BB_RAPTOR_MOTHERLY, "Motherly"),
            Dm.kv(BB_RAPTOR_PLAYFUL, "Playful"),
            Dm.kv(BB_RAPTOR_COWARD, "Coward"));
        Dm.append(ref gvars_datum_init_order, "raptor_inherit_traits");
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:8  (GLOBAL_MANAGED(raptor_population, list()))
    public virtual void InitGlobalraptor_population()
    {
        raptor_population = Dm.list();
        Dm.append(ref gvars_datum_init_order, "raptor_population");
    }
}

// tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:10
public partial class mob_living_basic_raptor : mob_living_basic
{
    /// Pet commands when we tame the raptor
    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:46 (var/static/list/pet_commands)
    public static DmList pet_commands => TgState.Current.mob_living_basic_raptor__pet_commands ??= Dm.list(
        typeof(datum_pet_command_breed),
        typeof(datum_pet_command_idle),
        typeof(datum_pet_command_move),
        typeof(datum_pet_command_free),
        typeof(datum_pet_command_attack),
        typeof(datum_pet_command_follow),
        typeof(datum_pet_command_fetch));

    /// Food types that we can consume
    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:66 (var/static/list/food_types)
    public static DmList food_types => TgState.Current.mob_living_basic_raptor__food_types ??= Dm.list(
        Dm.kv(typeof(obj_item_stack_ore), 0),
        Dm.kv(typeof(obj_item_food_meat), 15),
        Dm.kv(typeof(obj_item_food_meat_slab), 25),
        Dm.kv(typeof(obj_item_food_meat_slab_spider), -15), // Toxic meats
        Dm.kv(typeof(obj_item_food_meat_slab_xeno), -15),
        Dm.kv(typeof(obj_item_food_meat_steak), 50),
        Dm.kv(typeof(obj_item_food_grown_ash_flora), 10),
        Dm.kv(typeof(obj_item_fish), 25),
        Dm.kv(typeof(obj_item_organ), 25));

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:84
    [DmParams("mapload", "color_type", "passed_stats")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object color_type = Dm.arg(__extra, 0);
        datum_raptor_inheritance passed_stats = Dm.arg<datum_raptor_inheritance>(__extra, 1);
        object dot = base.Initialize(mapload, __extra);
        inherited_stats = passed_stats ?? Dm.@new<datum_raptor_inheritance>(this);
        // First thing as to go before tameable in change_growth_stage()
        Waiting.NoSleep(AddElement(typeof(datum_element_clever_girl)));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), Dm.named("food_types", food_types)));
        object color_key = Dm.truthy(color_type) ? color_type : Dm.truthy(raptor_color) ? raptor_color : pick(GLOB.raptor_colors);
        raptor_color = GLOB.raptor_colors[color_key];
        datum_raptor_color color = raptor_color as datum_raptor_color;
        color.setup_raptor(this);

        if (Dm.eq(growth_stage, RAPTOR_ADULT))
            color.setup_adult(this);
        else
            change_growth_stage(growth_stage, RAPTOR_ADULT);

        add_traits(Dm.list(TRAIT_ASHSTORM_IMMUNE, TRAIT_SNOWSTORM_IMMUNE, TRAIT_MINING_AOE_IMMUNE, TRAIT_NO_SLIP_ICE, TRAIT_NO_SLIP_SLIDE), INNATE_TRAIT);
        Waiting.NoSleep(AddElement(
            typeof(datum_element_crusher_loot),
            Dm.named("trophy_type", typeof(obj_item_crusher_trophy_raptor_feather)),
            Dm.named("drop_mod", 100),
            Dm.named("drop_immediately", FALSE)));

        if (!Dm.truthy(mapload))
            Dm.append(ref GLOB.raptor_population, REF(this));

        Waiting.NoSleep(AddComponent(typeof(datum_component_obeys_commands), pet_commands, Dm.list(-base_pixel_w, 0)));
        Waiting.NoSleep(AddElement(
            typeof(datum_element_change_force_on_death),
            Dm.named("move_resist", MOVE_RESIST_DEFAULT)));
        RegisterSignal(this, COMSIG_MOB_ATE, nameof(on_eat));
        RegisterSignal(this, COMSIG_MOB_PRE_EAT, nameof(on_pre_eat));

        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_flee_while_injured), Dm.named("stop_fleeing_at", 0.5f), Dm.named("start_fleeing_below", 0.2f)));

        if (Dm.truthy(can_breed))
            add_breeding_component();

        // Babies handle it in their change_growth_stage
        if (!Dm.eq(growth_stage, RAPTOR_BABY))
            update_blackboard();

        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), Dm.named("footstep_type", FOOTSTEP_MOB_CLAW)));
        RegisterSignal(this, COMSIG_LIVING_SCOOPED_UP, nameof(on_picked_up));
        add_happiness_component();
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:131
    public override object Destroy(float? force = null)
    {
        raptor_color = null;
        Dm.remove(ref GLOB.raptor_population, REF(this));
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:136
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = await base.death(gibbed, __extra);
        Dm.remove(ref GLOB.raptor_population, REF(this));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:140
    public override async DmTask<object> buckle_mob(mob_living target = null, float? force = null, float? check_loc = null, float? buckle_mob_flags = null)
    {
        force ??= FALSE;
        check_loc ??= TRUE;
        buckle_mob_flags ??= NONE;
        if (!iscarbon(target))
            return null;
        return await base.buckle_mob(target, force, check_loc, buckle_mob_flags);
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:145
    public override object get_hud_x_offset()
    {
        return -4;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:148
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (stat == DEAD)
            return dot;

        // switch (health / maxHealth): BYOND takes the first range that holds the value, both ends included
        float ratio = health / maxHealth;
        if (ratio >= 0 && ratio <= 0.2f)
            Dm.append(ref dot, span_italics(span_bolddanger(I18n.T("[] [] gruesomly wounded, barely staying up on [] feet!", p_They(), p_are(), p_their()))));
        else if (ratio >= 0.2f && ratio <= 0.4f)
            Dm.append(ref dot, span_danger(I18n.T("[] [] heavy injuries and open wounds all around [] body!", p_They(), p_have(), p_their())));
        else if (ratio >= 0.4f && ratio <= 0.6f)
            Dm.append(ref dot, span_warning(I18n.T("[] [] noticeably hurt, limping from [] cuts and bruises.", p_They(), p_are(), p_their())));
        else if (ratio >= 0.6f && ratio <= 0.8f)
            Dm.append(ref dot, span_warning(I18n.T("[] [] visibly injured, a few bruises and cuts showing between [] feathers.", p_They(), p_are(), p_their())));
        else if (ratio >= 0.8f && ratio <= 0.999f)
            Dm.append(ref dot, span_notice(I18n.T("[] [] a few minor bruises and scratches.", p_They(), p_have())));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:165
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.Life(seconds_per_tick, __extra);
        if (!Dm.eq(growth_stage, RAPTOR_BABY) || HAS_TRAIT(this, TRAIT_STASIS) || stat == DEAD)
            return dot;
        if (!SPT_PROB(Dm.num(growth_probability) * (1 + happiness_percentage * RAPTOR_GROWTH_HAPPINESS_MULTIPLIER), seconds_per_tick))
            return dot;
        growth_progress += rand(RAPTOR_BABY_GROWTH_LOWER, RAPTOR_BABY_GROWTH_UPPER);
        if (growth_progress >= RAPTOR_GROWTH_REQUIRED)
        {
            change_growth_stage(RAPTOR_YOUNG);
            growth_progress = 0;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:176
    public override async DmTask<object> early_melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.early_melee_attack(target, modifiers, ignore_cooldown);
        if (Dm.truthy(dot))
            return dot;
        if (!(target is obj_structure_ore_container_food_trough_raptor_trough))
            return BASIC_MOB_CONTINUE_ATTACK_CHAIN;
        obj ore_food = Dm.locate<obj_item_stack_ore>(target);
        if (ore_food == null)
            balloon_alert(this, "no food!");
        else
            await UnarmedAttack(ore_food, TRUE, modifiers);
        return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:189
    public override async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        if (!Dm.truthy(combat_mode) && target is mob_living_basic_raptor possible_baby)
        {
            if (Dm.eq(possible_baby.growth_stage, RAPTOR_BABY))
                return await target.attack_hand(this, Dm.list(Dm.kv(LEFT_CLICK, TRUE)));
        }
        return await base.melee_attack(target, modifiers, ignore_cooldown);
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:196
    public virtual void add_breeding_component()
    {
        DmList partner_types = TgState.Current.mob_living_basic_raptor__add_breeding_component__partner_types ??= typecacheof(Dm.list(typeof(mob_living_basic_raptor))) as DmList;
        DmList baby_types = TgState.Current.mob_living_basic_raptor__add_breeding_component__baby_types ??= Dm.list(Dm.kv(typeof(obj_item_food_egg_raptor_egg), 1));
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_breed),
            Dm.named("can_breed_with", partner_types),
            Dm.named("baby_paths", baby_types),
            Dm.named("breed_timer", 3 * MINUTES),
            Dm.named("post_birth", Dm.@new<datum_callback>(this, nameof(egg_inherit)))));
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:207
    public virtual void add_happiness_component()
    {
        DmList percentage_callbacks = TgState.Current.mob_living_basic_raptor__add_happiness_component__percentage_callbacks ??= Dm.list(0, 15, 25, 35, 50, 75, 90, 100);
        // Higher happiness cap so it decays slower, about 15 minutes from full to zero
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_happiness),
            Dm.named("maximum_happiness", 900),
            Dm.named("on_petted_change", 50),
            Dm.named("on_groom_change", 200),
            Dm.named("on_eat_change", 150),
            Dm.named("callback_percentages", percentage_callbacks),
            Dm.named("happiness_callback", Dm.@new<datum_callback>(this, nameof(happiness_change)))));
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:220
    public virtual void happiness_change(object percent_value = null)
    {
        float attack_boost = round((Dm.num(percent_value) - happiness_percentage) * RAPTOR_HAPPINESS_DAMAGE_BOOST, 1);
        melee_damage_lower += attack_boost;
        melee_damage_upper += attack_boost;
        happiness_percentage = Dm.num(percent_value);
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:226
    public override async DmTask<object> projectile_hit(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, object blocked = null)
    {
        // Most colors will redirect shots to their rider as to increase their own survivability, and only tank melee attacks
        if (Dm.truthy((raptor_color as datum_raptor_color).redirect_shots) && Dm.length(buckled_mobs) > 0)
            return await (buckled_mobs[1f] as mob_living).projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
        return await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
    }

    /// Pass our genetic data to the egg
    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:233
    public virtual void egg_inherit(obj_item_food_egg_raptor_egg baby_egg = null, mob_living_basic_raptor partner = null)
    {
        datum_raptor_inheritance child_genes = Dm.@new<datum_raptor_inheritance>();
        child_genes.set_parents(this, partner);
        baby_egg.inherited_stats = child_genes;
        baby_egg.child_color = get_child_color(partner);
        // Halve our food modifiers every time we breed
        foreach (object food_type in Dm.each(inherited_stats.foods_eaten))
        {
            DmList stat_mods = inherited_stats.foods_eaten[food_type] as DmList;
            stat_mods["amount"] = Dm.num(stat_mods["amount"]) / 2;
            stat_mods["attack"] = Dm.num(stat_mods["attack"]) / 2;
            stat_mods["health"] = Dm.num(stat_mods["health"]) / 2;
            stat_mods["speed"] = Dm.num(stat_mods["speed"]) / 2;
            stat_mods["ability"] = Dm.num(stat_mods["ability"]) / 2;
            stat_mods["growth"] = Dm.num(stat_mods["growth"]) / 2;
            DmList trait_list = stat_mods["traits"] as DmList;
            float to_remove = ceil(Dm.length(trait_list) / 2f);
            for (float i = 1; i <= to_remove; i++)
                Dm.remove(ref trait_list, pick(trait_list));

            DmList color_chances = stat_mods["color_chances"] as DmList;
            foreach (object color_type in Dm.each(color_chances))
            {
                color_chances[color_type] = floor(Dm.num(color_chances[color_type]) / 2);
                if (!Dm.truthy(color_chances[color_type]))
                    Dm.remove(ref color_chances, color_type);
            }
        }
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:257
    public virtual Type get_child_color(mob_living_basic_raptor partner = null)
    {
        datum_raptor_color our_color = raptor_color as datum_raptor_color;
        datum_raptor_color partner_color = partner.raptor_color as datum_raptor_color;
        if (our_color == partner_color)
            return our_color.type;

        if (Dm.truthy(our_color.guaranteed_crossbreeds[partner_color.type]))
            return our_color.guaranteed_crossbreeds[partner_color.type] as Type;

        // This should be redundant as they should be mirroring eachother, but just in case
        if (Dm.truthy(partner_color.guaranteed_crossbreeds[our_color.type]))
            return partner_color.guaranteed_crossbreeds[our_color.type] as Type;

        // We've got all the colors in our family tree and aren't rolling a guarantee, bingo
        if (Dm.length(Dm.bitor(Dm.bitor(Dm.bitor(inherited_stats.parent_colors, partner.inherited_stats.parent_colors), our_color.type), partner_color.type)) >= (Dm.length(GLOB.raptor_colors) - 1))
            return typeof(datum_raptor_color_black);

        DmList prob_list = Dm.list();
        foreach (object color_type in Dm.each(GLOB.raptor_colors))
            prob_list[color_type] = Dm.initial((Type)color_type, (datum_raptor_color c) => c.spawn_chance);

        float amount_eaten = 0;
        foreach (object food_type in Dm.each(inherited_stats.foods_eaten))
        {
            DmList stat_mods = inherited_stats.foods_eaten[food_type] as DmList;
            amount_eaten += Dm.num(stat_mods["amount"]);
        }

        foreach (object food_type in Dm.each(inherited_stats.foods_eaten))
        {
            DmList stat_mods = inherited_stats.foods_eaten[food_type] as DmList;
            DmList color_chances = stat_mods["color_chances"] as DmList;
            foreach (object color_type in Dm.each(color_chances))
                prob_list[color_type] = Dm.num(prob_list[color_type]) + floor(Dm.num(color_chances[color_type]) * Dm.num(stat_mods["amount"]));
        }

        return pick_weight(prob_list) as Type;
    }

    /// Updates the presence of the can_be_held element based on what we want from the raptor
    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:290
    public virtual void update_holdability(object @bool = null)
    {
        if (Dm.truthy(@bool) && !Dm.truthy(could_be_held))
        {
            Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
            could_be_held = TRUE;
        }

        if (!Dm.truthy(@bool) && Dm.truthy(could_be_held))
        {
            RemoveElement(typeof(datum_element_can_be_held));
            could_be_held = FALSE;
        }
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:299
    [SignalHandler]
    public virtual void on_picked_up(mob_living_basic_raptor source = null, mob_living user = null, obj_item_mob_holder holder = null)
    {
        // Our inventory code sucks so we have to do this
        holder.icon = Dm.resource("icons/mob/simple/lavaland/raptor_baby.dmi");
        holder.icon_state = icon_state;
        holder.alternate_worn_layer = HEAD_LAYER;
        holder.pixel_w = 0;
        holder.pixel_z = 0;
        holder.base_pixel_w = 0;
        holder.base_pixel_z = 0;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:310
    [SignalHandler]
    public virtual object on_pre_eat(datum source = null, obj_item potential_food = null, DmList effect_mult = null)
    {
        if (isorgan(potential_food))
        {
            obj_item_organ guts = potential_food as obj_item_organ;
            if (!Dm.truthy(Dm.bitand(guts.organ_flags, ORGAN_EDIBLE)) || !Dm.truthy(Dm.bitand(guts.organ_flags, ORGAN_ORGANIC)))
                return COMSIG_MOB_CANCEL_EAT;
        }

        if (Dm.truthy(happiness_percentage))
            Dm.append(ref effect_mult, happiness_percentage * RAPTOR_GROWTH_HAPPINESS_MULTIPLIER);
        return null;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:321
    [SignalHandler]
    public virtual void on_eat(datum source = null, atom food = null, mob_living feeder = null)
    {
        if (!(food is obj_item_food meal))
            return;

        bool is_flora = meal is obj_item_food_grown_ash_flora;
        bool is_meat = Dm.truthy(Dm.bitand(meal.foodtypes, Dm.bitor(MEAT, GORE)));
        // Babies cannot gain growth from eating meat, only plants, but they get some passively
        if ((!is_meat || Dm.eq(growth_stage, RAPTOR_BABY)) && !is_flora)
            return;

        if (Dm.eq(growth_stage, RAPTOR_ADULT))
            return;

        // Better meals make your raptor grow faster
        float growth_value = Dm.num(meal.crafting_complexity) * RAPTOR_MEAL_COMPLEXITY_GROWTH_FACTOR + (is_flora ? RAPTOR_GROWTH_BASE_PLANT : RAPTOR_GROWTH_BASE_MEAT);
        growth_progress += growth_value * (1 + Dm.num(inherited_stats.growth_modifier)) * (1 + happiness_percentage * RAPTOR_GROWTH_HAPPINESS_MULTIPLIER);
        if (growth_progress >= RAPTOR_GROWTH_REQUIRED)
        {
            change_growth_stage(Dm.eq(growth_stage, RAPTOR_BABY) ? RAPTOR_YOUNG : RAPTOR_ADULT);
            growth_progress = 0;
        }
    }

    /// Changes the raptor to a new growth stage. Only should be done forwards, or on raptor init as the first thing before everything else
    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:347
    public virtual object change_growth_stage(object new_stage = null, object prev_stage = null)
    {
        prev_stage ??= growth_stage;
        if (Dm.eq(new_stage, prev_stage))
            return FALSE;

        if (Dm.truthy(Dm.bitand(SEND_SIGNAL(COMSIG_RAPTOR_GROWTH_STAGE_CHANGE, Dm.list(this, new_stage, prev_stage)), COMPONENT_CANCEL_RAPTOR_GROWTH)))
            return FALSE;

        growth_stage = new_stage as string;
        datum_raptor_color color = raptor_color as datum_raptor_color;

        // Visuals
        switch (new_stage)
        {
            case RAPTOR_BABY:
                name = "baby raptor";
                desc = "Will this grow into something useful?";
                icon = Dm.resource("icons/mob/simple/lavaland/raptor_baby.dmi");
                base_icon_state = "baby";
                base_pixel_w = 0;
                mob_size = MOB_SIZE_TINY;
                break;
            case RAPTOR_YOUNG:
                name = "raptor youngling";
                desc = "A young raptor that can grow into a robust, trusty steed. Rather naive at such an age, it shouldn't be too hard to tame.";
                icon = Dm.resource("icons/mob/simple/lavaland/raptor_big.dmi");
                base_icon_state = "young";
                base_pixel_w = Dm.initial(this, (atom a) => a.base_pixel_w);
                mob_size = MOB_SIZE_HUMAN;
                break;
            case RAPTOR_ADULT:
                name = "raptor";
                desc = Dm.initial(this, (atom a) => a.desc);
                icon = Dm.resource("icons/mob/simple/lavaland/raptor_big.dmi");
                base_icon_state = "raptor";
                base_pixel_w = Dm.initial(this, (atom a) => a.base_pixel_w);
                mob_size = Dm.initial(this, (mob_living l) => l.mob_size);
                break;
        }

        density = Dm.initial(this, (atom a) => a.density);
        move_resist = Dm.initial(this, (atom_movable a) => a.move_resist);
        can_breed = Dm.initial(this, (mob_living_basic_raptor r) => r.can_breed);
        update_holdability(Dm.initial(this, (mob_living_basic_raptor r) => r.could_be_held));

        if (Dm.eq(new_stage, RAPTOR_ADULT))
        {
            // Adults need to be tamed with skill rather than snacks
            qdel(GetComponent(typeof(datum_component_tameable)));
        }
        else // Make us teeny-tiny
        {
            update_holdability(TRUE);
            density = FALSE;
            can_breed = FALSE;
            move_resist = MOVE_RESIST_DEFAULT;

            if (Dm.eq(prev_stage, RAPTOR_ADULT))
                Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), Dm.named("food_types", food_types), Dm.named("tame_chance", 25), Dm.named("bonus_tame_chance", 15), Dm.named("unique", TRUE)));
        }

        if (Dm.truthy(can_breed))
            add_breeding_component();
        else
            qdel(GetComponent(typeof(datum_component_breed)));

        obj_item_mob_holder holder = null;
        if (loc is obj_item_mob_holder held_in)
        {
            holder = held_in;
            if (!Dm.truthy(could_be_held))
            {
                Waiting.NoSleep(holder.release());
                holder = null;
            }
        }

        if (Dm.truthy(collar_state))
            RemoveElement(typeof(datum_element_wears_collar), Dm.named("collar_icon", Dm.resource("icons/mob/simple/lavaland/raptor_big.dmi")), Dm.named("collar_icon_state", Dm.text("[]_", collar_state)));

        if (Dm.eq(new_stage, RAPTOR_BABY))
        {
            collar_state = null;
            DmList friends = (ai_controller as datum_ai_controller)?.blackboard[BB_FRIENDS_LIST] as DmList;
            if (friends != null)
                friends = friends.Copy();
            QDEL_NULL(ref ai_controller);
            ai_controller = Dm.@new<datum_ai_controller_basic_controller_baby_raptor>(this);
            foreach (object old_friend in Dm.each(friends))
                (ai_controller as datum_ai_controller).insert_blackboard_key_lazylist(BB_FRIENDS_LIST, old_friend);
            update_blackboard();
            held_w_class = WEIGHT_CLASS_SMALL;
            worn_slot_flags = NONE;
            holder?.update_weight_class(held_w_class);
        }
        else
        {
            collar_state = base_icon_state as string;
            Waiting.NoSleep(AddElement(typeof(datum_element_wears_collar), Dm.named("collar_icon", Dm.resource("icons/mob/simple/lavaland/raptor_big.dmi")), Dm.named("collar_icon_state", Dm.text("[]_", collar_state))));
            if (Dm.eq(prev_stage, RAPTOR_BABY))
            {
                DmList friends = (ai_controller as datum_ai_controller)?.blackboard[BB_FRIENDS_LIST] as DmList;
                if (friends != null)
                    friends = friends.Copy();
                QDEL_NULL(ref ai_controller);
                ai_controller = Dm.@new(color.ai_controller, this);
                foreach (object old_friend in Dm.each(friends))
                    (ai_controller as datum_ai_controller).insert_blackboard_key_lazylist(BB_FRIENDS_LIST, old_friend);
                update_blackboard();
            }
            held_w_class = WEIGHT_CLASS_BULKY;
            worn_slot_flags = ITEM_SLOT_BACK;
            holder?.update_weight_class(held_w_class);
        }

        // And finish the setup on our color's side
        switch (new_stage)
        {
            case RAPTOR_BABY:
                color.setup_baby(this);
                break;
            case RAPTOR_YOUNG:
                color.setup_young(this);
                break;
            case RAPTOR_ADULT:
                color.setup_adult(this);
                break;
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:451
    public virtual void update_blackboard()
    {
        DmList display_emote = TgState.Current.mob_living_basic_raptor__update_blackboard__display_emote ??= Dm.list(
            Dm.kv(BB_EMOTE_SAY, Dm.list("Chirp chirp chirp!", "Kweh!", "Bwark!")),
            Dm.kv(BB_EMOTE_SEE, Dm.list("shakes its feathers!", "stretches!", "flaps its wings!", "pecks at the ground!")),
            Dm.kv(BB_EMOTE_SOUND, Dm.list(
                Dm.resource("sound/mobs/non-humanoids/raptor/raptor_1.ogg"),
                Dm.resource("sound/mobs/non-humanoids/raptor/raptor_2.ogg"),
                Dm.resource("sound/mobs/non-humanoids/raptor/raptor_3.ogg"),
                Dm.resource("sound/mobs/non-humanoids/raptor/raptor_4.ogg"),
                Dm.resource("sound/mobs/non-humanoids/raptor/raptor_5.ogg"))),
            Dm.kv(BB_SPEAK_CHANCE, 2));

        datum_ai_controller controller = ai_controller as datum_ai_controller;
        controller.set_blackboard_key(BB_BASIC_MOB_SPEAK_LINES, display_emote);

        DmList preferred_foods = TgState.Current.mob_living_basic_raptor__update_blackboard__preferred_foods ??= Dm.sub(
            typecacheof(Dm.list(typeof(obj_item_food_meat), typeof(obj_item_food_grown_ash_flora))),
            typecacheof(Dm.list( // Don't seek out toxic foods
                typeof(obj_item_food_meat_slab_spider),
                typeof(obj_item_food_meat_slab_xeno)))) as DmList;

        controller.set_blackboard_key(BB_BASIC_FOODS, preferred_foods);

        foreach (object trait in Dm.each(GLOB.raptor_inherit_traits))
        {
            bool should_inherit = Dm.@in(trait, inherited_stats.personality_traits);
            controller?.set_blackboard_key(trait, should_inherit ? TRUE : FALSE);
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:46 (var/static/list/pet_commands)
    internal DmList mob_living_basic_raptor__pet_commands;
    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:66 (var/static/list/food_types)
    internal DmList mob_living_basic_raptor__food_types;
    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:195 (var/static/list/partner_types)
    internal DmList mob_living_basic_raptor__add_breeding_component__partner_types;
    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:196 (var/static/list/baby_types)
    internal DmList mob_living_basic_raptor__add_breeding_component__baby_types;
    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:206 (var/static/list/percentage_callbacks)
    internal DmList mob_living_basic_raptor__add_happiness_component__percentage_callbacks;
    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:456 (var/static/list/display_emote)
    internal DmList mob_living_basic_raptor__update_blackboard__display_emote;
    // tg: code/modules/mob/living/basic/lavaland/raptor/_raptor.dm:471 (var/static/list/preferred_foods)
    internal DmList mob_living_basic_raptor__update_blackboard__preferred_foods;
}
