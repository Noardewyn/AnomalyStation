// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm
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
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:1  GLOBAL_LIST_INIT(raptor_colors, init_raptor_colors())
    public DmList raptor_colors;

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:1  (GLOBAL_MANAGED(raptor_colors, init_raptor_colors()))
    public virtual void InitGlobalraptor_colors()
    {
        raptor_colors = init_raptor_colors();
        Dm.append(ref gvars_datum_init_order, "raptor_colors");
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:3
    public static DmList init_raptor_colors()
    {
        DmList colors = Dm.list();
        foreach (Type color_type in Dm.each<Type>(subtypesof(typeof(datum_raptor_color))))
            colors[color_type] = Dm.@new(color_type);
        return colors;
    }
}

// tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:9
public partial class datum_raptor_color : datum
{
    /// Shared proc, only called once on raptor init for color-specific traits and properties
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:33
    public virtual void setup_raptor(mob_living_basic_raptor raptor = null)
    {
        if (raptor.ai_controller != null)
        {
            CRASH(Dm.text("setup_raptor called on a raptor ([]) with a present AI controller! This is most likely a result of a second call to setup_raptor.", raptor), "code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm", 35);
            return;
        }
        raptor.ai_controller = Dm.@new(ai_controller, raptor);
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:38
    public virtual void setup_adult(mob_living_basic_raptor raptor = null)
    {
        datum_raptor_inheritance stats = raptor.inherited_stats;
        float real_health = health + Dm.num(stats.health_modifier);
        // If we grow up while damaged, keep the damage percentage the same
        raptor.health *= real_health / raptor.maxHealth;
        raptor.maxHealth = real_health;
        // -0.33 ~ 0.33 gets rounded to 0 rather than +-0.5
        float speed_mod = clamp(round(abs(Dm.num(stats.speed_modifier) - 0.08f), 0.5f) * sign(Dm.num(stats.speed_modifier)), -0.5f, 0.5f);
        Waiting.NoSleep(raptor.set_varspeed(speed - speed_mod));
        raptor.melee_damage_lower = melee_damage_lower + Dm.num(stats.attack_modifier);
        raptor.melee_damage_upper = melee_damage_upper + Dm.num(stats.attack_modifier);
        if (rideable_component != null)
            Waiting.NoSleep(raptor.AddElement(typeof(datum_element_ridable), rideable_component));
        setup_appearance(raptor);
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:53
    public virtual void setup_young(mob_living_basic_raptor raptor = null)
    {
        datum_raptor_inheritance stats = raptor.inherited_stats;
        float real_health = health + Dm.num(stats.health_modifier);
        raptor.health *= real_health / 2 / raptor.maxHealth;
        raptor.maxHealth = real_health / 2;
        float speed_mod = clamp(round(abs(Dm.num(stats.speed_modifier) - 0.08f), 0.5f) * sign(Dm.num(stats.speed_modifier)), -0.5f, 0.5f);
        Waiting.NoSleep(raptor.set_varspeed(speed - speed_mod));
        raptor.melee_damage_lower = floor((melee_damage_lower + Dm.num(stats.attack_modifier)) / 2);
        raptor.melee_damage_upper = floor((melee_damage_upper + Dm.num(stats.attack_modifier)) / 2);
        setup_appearance(raptor);
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:64
    public virtual void setup_baby(mob_living_basic_raptor raptor = null)
    {
        datum_raptor_inheritance stats = raptor.inherited_stats;
        float real_health = health + Dm.num(stats.health_modifier);
        raptor.health *= real_health / 8 / raptor.maxHealth;
        raptor.maxHealth = real_health / 8;
        float speed_mod = clamp(round(abs(Dm.num(stats.speed_modifier) - 0.08f), 0.5f) * sign(Dm.num(stats.speed_modifier)), -0.5f, 0.5f);
        Waiting.NoSleep(raptor.set_varspeed(speed + 4.5f - speed_mod));
        raptor.melee_damage_lower = floor((melee_damage_lower + Dm.num(stats.attack_modifier)) / 3);
        raptor.melee_damage_upper = floor((melee_damage_upper + Dm.num(stats.attack_modifier)) / 3);
        setup_appearance(raptor);
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:75
    public virtual void setup_appearance(mob_living_basic_raptor raptor = null)
    {
        raptor.name = Dm.text("[] []", color, raptor.name);
        raptor.icon_state = Dm.text("[]_[]", raptor.base_icon_state, color);
        raptor.held_state = Dm.text("[]_[]", raptor.base_icon_state, color);
        raptor.icon_living = Dm.text("[]_[]", raptor.base_icon_state, color);
        raptor.icon_dead = Dm.text("[]_[]_dead", raptor.base_icon_state, color);
        raptor.update_appearance();
    }
}

// tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:86
public partial class datum_raptor_color_red : datum_raptor_color
{
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:99
    public override void setup_raptor(mob_living_basic_raptor raptor = null)
    {
        base.setup_raptor(raptor);
        ADD_TRAIT(raptor, TRAIT_MINING_AGGRO, INNATE_TRAIT);
    }
}

// tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:104
public partial class datum_raptor_color_purple : datum_raptor_color
{
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:113
    public override void setup_raptor(mob_living_basic_raptor raptor = null)
    {
        base.setup_raptor(raptor);
        RegisterSignal(raptor, COMSIG_MOVABLE_PREBUCKLE, nameof(on_pre_buckle));
        raptor.inhand_holder_type = typeof(obj_item_mob_holder_purple_raptor);
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:118
    [SignalHandler]
    public virtual object on_pre_buckle(mob_living_basic_raptor source = null, mob_living potential_rider = null, object force = null, object ride_check_flags = null)
    {
        if (!ishuman(potential_rider))
            return COMPONENT_BLOCK_BUCKLE;

        mob_living_carbon_human rider = potential_rider as mob_living_carbon_human;
        if (rider.mob_height > HUMAN_HEIGHT_SHORTEST)
        {
            to_chat(rider, span_warning(I18n.T("Your tall stature will crush [] were you attempt to ride []!", source, source.p_them())));
            return COMPONENT_BLOCK_BUCKLE;
        }
        return null;
    }

    // Purple raptors never "fully" grow up, and remain usable as backpacks
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:130
    public override void setup_adult(mob_living_basic_raptor raptor = null)
    {
        raptor.update_holdability(TRUE);
        raptor.density = FALSE;
        raptor.move_resist = MOVE_RESIST_DEFAULT;
        raptor.held_w_class = WEIGHT_CLASS_BULKY;
        base.setup_adult(raptor);
        // Non-shorties cannot ride these, so we gotta keep em tameable through food
        Waiting.NoSleep(raptor.AddComponent(typeof(datum_component_tameable), Dm.named("food_types", mob_living_basic_raptor.food_types), Dm.named("tame_chance", 25), Dm.named("bonus_tame_chance", 15), Dm.named("unique", TRUE)));
        if (raptor.atom_storage != null)
            return;
        // A bit bigger (23 vs 21) than a backpack at max size, a bit less by default
        float storage_volume = floor(19 * (1 + Dm.num(raptor.inherited_stats.ability_modifier)));
        Waiting.NoSleep(raptor.create_storage(
            max_total_storage: storage_volume,
            max_slots: storage_volume,
            storage_type: typeof(datum_storage_raptor_storage)));
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:148
    public override void setup_young(mob_living_basic_raptor raptor = null)
    {
        base.setup_young(raptor);
        if (raptor.atom_storage != null)
            return;
        float storage_volume = floor(19 * (1 + Dm.num(raptor.inherited_stats.ability_modifier)));
        Waiting.NoSleep(raptor.create_storage(
            max_total_storage: storage_volume,
            max_slots: storage_volume,
            storage_type: typeof(datum_storage_raptor_storage)));
    }
}

// tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:160
public partial class obj_item_mob_holder_purple_raptor : obj_item_mob_holder
{
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:170
    [DmParams("mapload", "held_mob", "worn_state", "head_icon", "lh_icon", "rh_icon", "worn_slot_flags")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob_living held_mob = Dm.arg<mob_living>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);

        mob_living_basic_raptor raptor = held_mob as mob_living_basic_raptor;
        if (Dm.eq(raptor.growth_stage, RAPTOR_BABY))
            return dot;

        // Create a mirror storage for our raptor when picked up to handle interactions
        datum_storage raptor_storage = Waiting.NoSleep(create_storage(
            max_total_storage: held_mob.atom_storage.max_total_storage,
            max_slots: held_mob.atom_storage.max_slots,
            storage_type: typeof(datum_storage_raptor_storage))) as datum_storage;
        Waiting.NoSleep(raptor_storage.set_real_location(held_mob));
        raptor_storage.insert_on_attack = TRUE;

        if (!Dm.eq(raptor.growth_stage, RAPTOR_ADULT))
            return dot;

        flight_action = Dm.@new<datum_action_innate_raptor_wings>(this);

        Waiting.NoSleep(AddComponent(
            typeof(datum_component_jetpack),
            TRUE,
            drift_force,
            COMSIG_RAPTOR_WINGS_OPENED,
            COMSIG_RAPTOR_WINGS_CLOSED,
            null,
            Dm.@new<datum_callback>(this, nameof(check_flight)),
            Dm.@new<datum_callback>(this, nameof(check_flight))));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:202
    public override object Destroy(float? force = null)
    {
        if (ishuman(loc) && Dm.truthy(wings_open))
            toggle_wings(loc as mob_living_carbon_human);
        QDEL_NULL(ref flight_action);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:208
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_BACK)) && ishuman(user) && flight_action != null)
        {
            await flight_action.Grant(held_mob);
            await flight_action.GiveAction(user);
            RegisterSignal(user, COMSIG_MOVABLE_CHASM_DROPPED, nameof(chasm_react));
        }
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:215
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        UnregisterSignal(user, COMSIG_MOVABLE_CHASM_DROPPED);
        if (Dm.truthy(wings_open))
            toggle_wings(user as mob_living_carbon_human);
        // Removed in Destroy()
        if (flight_action != null)
        {
            await flight_action.Remove(held_mob);
            flight_action.HideFrom(user);
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:225
    [SignalHandler]
    public virtual void on_weight_updated(mob_living_carbon_human source = null)
    {
        if (source.mob_height <= HUMAN_HEIGHT_SHORTEST && !HAS_TRAIT(source, TRAIT_FAT))
        {
            source.remove_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_raptor_slow));
            Waiting.NoSleep(source.add_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_raptor)));
        }
        else
        {
            source.remove_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_raptor));
            Waiting.NoSleep(source.add_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_raptor_slow)));
        }
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:235
    public virtual object can_fly(object silent = null)
    {
        silent ??= FALSE;
        mob_living_carbon_human user = loc as mob_living_carbon_human;
        if (user == null || IS_UNCONSCIOUS_OR_CRIT(user) || Dm.eq(user.body_position, LYING_DOWN) || user.client == null)
            return FALSE;

        turf location = get_turf(user) as turf;
        if (location == null)
            return FALSE;

        datum_gas_mixture environment = Waiting.NoSleep(location.return_air()) as datum_gas_mixture;
        if (environment != null && Dm.num(environment.return_pressure()) >= HAZARD_LOW_PRESSURE + 10)
            return TRUE;

        if (!Dm.truthy(silent))
            to_chat(user, span_warning(I18n.T("The atmosphere is too thin for you to fly!")));
        return FALSE;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:252
    public virtual object check_flight()
    {
        return can_fly(silent: TRUE);
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:255
    public virtual void toggle_wings(mob_living_carbon_human user = null)
    {
        // In case something goes wrong
        if (user == null)
        {
            wings_open = FALSE;
            worn_icon_state = icon_state;
            SEND_SIGNAL(COMSIG_RAPTOR_WINGS_CLOSED, Dm.list(this, user));
            STOP_PROCESSING(SSprocessing, this);
            return;
        }

        if (!Dm.truthy(wings_open) && !Dm.truthy(can_fly()))
            return;

        wings_open = Dm.truthy(wings_open) ? FALSE : TRUE;
        worn_icon_state = Dm.text("[][]", icon_state, Dm.truthy(wings_open) ? "_wings_out" : "");
        Waiting.NoSleep(user.update_worn_back());

        // Raptors won't have the best of times keeping up tall humans or fatties up in the air
        bool struggling = HAS_TRAIT(user, TRAIT_FAT) || user.mob_height > HUMAN_HEIGHT_SHORTEST;
        if (Dm.truthy(wings_open))
        {
            wings_underlay = mutable_appearance(worn_icon, "raptor_purple_wings", -BODY_BEHIND_LAYER, user) as mutable_appearance;
            user.apply_height(wings_underlay, UPPER_BODY);
            user.add_overlay(wings_underlay);
            MODIFY_PHYSIOLOGY(user, PHYS_COEFF_STUN, 2);
            user.add_traits(Dm.list(TRAIT_MOVE_FLOATING, TRAIT_IGNORING_GRAVITY, TRAIT_NOGRAV_ALWAYS_DRIFT), REF(this));
            if (struggling)
                Waiting.NoSleep(user.add_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_raptor_slow)));
            else
                Waiting.NoSleep(user.add_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_raptor)));
            Waiting.NoSleep(user.AddElement(typeof(datum_element_forced_gravity), 0));
            ADD_TRAIT(user, TRAIT_PASSTABLE, REF(this));
            to_chat(user, span_notice(I18n.T("You begin gently hovering above ground as [] on your back starts furiously flapping [] wings[]!", held_mob, held_mob.p_their(), struggling ? I18n.T(", struggling to keep you up in the air") : "")));
            Waiting.NoSleep(user.set_resting(FALSE, TRUE));
            Waiting.NoSleep(user.refresh_gravity());
            START_PROCESSING(SSprocessing, this);
            RegisterSignals(user, Dm.list(COMSIG_HUMAN_HEIGHT_UPDATED, SIGNAL_ADDTRAIT(TRAIT_FAT), SIGNAL_REMOVETRAIT(TRAIT_FAT)), nameof(on_weight_updated));
            SEND_SIGNAL(COMSIG_RAPTOR_WINGS_OPENED, Dm.list(this, user));
            return;
        }

        user.cut_overlay(wings_underlay);
        QDEL_NULL(ref wings_underlay);
        MODIFY_PHYSIOLOGY(user, PHYS_COEFF_STUN, 0.5f);
        user.remove_traits(Dm.list(TRAIT_MOVE_FLOATING, TRAIT_IGNORING_GRAVITY, TRAIT_NOGRAV_ALWAYS_DRIFT), REF(this));
        user.remove_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_raptor_slow));
        user.remove_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_raptor));
        user.RemoveElement(typeof(datum_element_forced_gravity), 0);
        REMOVE_TRAIT(user, TRAIT_PASSTABLE, REF(this));
        to_chat(user, span_notice(I18n.T("You settle gently back onto the ground[]...", struggling ? I18n.T(", [] on your back breathing out a sigh of releif", held_mob) : "")));
        Waiting.NoSleep(user.refresh_gravity());
        STOP_PROCESSING(SSprocessing, this);
        UnregisterSignal(user, Dm.list(COMSIG_HUMAN_HEIGHT_UPDATED, SIGNAL_ADDTRAIT(TRAIT_FAT), SIGNAL_REMOVETRAIT(TRAIT_FAT)));
        SEND_SIGNAL(COMSIG_RAPTOR_WINGS_CLOSED, Dm.list(this, user));
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:307
    [SignalHandler]
    public virtual object chasm_react(mob_living user = null, turf chasm = null)
    {
        if (Dm.truthy(wings_open) || !Dm.truthy(can_fly()))
            return null;

        toggle_wings(user as mob_living_carbon_human);
        if (Dm.truthy(wings_open))
            return COMPONENT_NO_CHASM_DROP;
        return null;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:317
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(can_fly(silent: TRUE)))
        {
            toggle_wings(loc as mob_living_carbon_human);
            return PROCESS_KILL;
        }
        return null;
    }
}

// tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:320
public partial class datum_storage_raptor_storage : datum_storage
{
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:326
    public override object on_mousedropped_onto(datum source = null, obj_item dropping = null, mob user = null)
    {
        return NONE;
    }
}

// tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:327
public partial class datum_action_innate_raptor_wings : datum_action_innate
{
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:337
    public override async DmTask<object> Activate()
    {
        obj_item_mob_holder_purple_raptor holder = target as obj_item_mob_holder_purple_raptor;
        mob_living_carbon_human user = holder.loc as mob_living_carbon_human;
        if (user == null || user.get_item_by_slot(ITEM_SLOT_BACK) != holder)
            return null;
        holder.toggle_wings(user);
        background_icon_state = Dm.text("bg_default[]", Dm.truthy(holder.wings_open) ? "_on" : "");
        await build_all_button_icons();
        return null;
    }
}

// tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:345
public partial class datum_raptor_color_green : datum_raptor_color
{
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:357
    public override void setup_adult(mob_living_basic_raptor raptor = null)
    {
        base.setup_adult(raptor);
        float ability_scale = 1 - INVERSE_LERP(RAPTOR_INHERIT_MIN_MODIFIER, RAPTOR_INHERIT_MAX_MODIFIER, Dm.num(raptor.inherited_stats.ability_modifier));
        float mining_mod = round(ability_scale * 0.1f, 0.025f);
        if (ability_scale >= 0.75f)
            mining_mod = 0;
        Waiting.NoSleep(raptor.AddComponent(typeof(datum_component_proficient_miner), mining_mod, TRUE));
    }
}

// tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:364
public partial class datum_raptor_color_white : datum_raptor_color
{
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:374
    public override void setup_young(mob_living_basic_raptor raptor = null)
    {
        base.setup_young(raptor);
        Waiting.NoSleep(raptor.AddComponent(
            typeof(datum_component_healing_touch),
            Dm.named("heal_brute", melee_damage_upper * 0.75f * (1 + Dm.num(raptor.inherited_stats.ability_modifier))),
            Dm.named("heal_burn", melee_damage_upper * 0.75f * (1 + Dm.num(raptor.inherited_stats.ability_modifier))),
            Dm.named("heal_time", 0),
            Dm.named("valid_targets_typecache", typecacheof(Dm.list(typeof(mob_living_basic_raptor))))));
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:384
    public override void setup_adult(mob_living_basic_raptor raptor = null)
    {
        base.setup_adult(raptor);
        qdel(raptor.GetComponent(typeof(datum_component_healing_touch)));
        Waiting.NoSleep(raptor.AddComponent(
            typeof(datum_component_healing_touch),
            Dm.named("heal_brute", melee_damage_upper * (1 + Dm.num(raptor.inherited_stats.ability_modifier))),
            Dm.named("heal_burn", melee_damage_upper * (1 + Dm.num(raptor.inherited_stats.ability_modifier))),
            Dm.named("heal_time", 0),
            Dm.named("valid_targets_typecache", typecacheof(Dm.list(typeof(mob_living_basic_raptor), typeof(mob_living_carbon_human)))),
            Dm.named("extra_checks", Dm.@new<datum_callback>(this, nameof(heal_checks))),
            Dm.named("healing_multiplier", Dm.@new<datum_callback>(this, nameof(heal_multiplier)))));
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:397
    public virtual object heal_checks(mob_living healer = null, mob_living target = null)
    {
        if (target is mob_living_basic_raptor)
            return TRUE;
        // Only heal raptors, or critted rider
        if (target.stat == STABLE || target.stat == DEAD)
            return FALSE;
        return target.buckled == healer ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:405
    public virtual object heal_multiplier(mob_living healer = null, mob_living target = null)
    {
        if (target is mob_living_basic_raptor)
            return 1;
        // The healing is slow so this is fine
        return 0.67f;
    }
}

// tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:420
public partial class datum_raptor_color_blue : datum_raptor_color
{
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:430
    public override void setup_raptor(mob_living_basic_raptor raptor = null)
    {
        base.setup_raptor(raptor);
        raptor.add_traits(Dm.list(TRAIT_LAVA_IMMUNE, TRAIT_NOFIRE_SPREAD), INNATE_TRAIT);
    }
}

// tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:434
public partial class datum_raptor_color_black : datum_raptor_color
{
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:446
    public override void setup_raptor(mob_living_basic_raptor raptor = null)
    {
        base.setup_raptor(raptor);
        raptor.add_traits(Dm.list(TRAIT_LAVA_IMMUNE, TRAIT_NOFIRE_SPREAD, TRAIT_MINING_AGGRO), INNATE_TRAIT);
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_color.dm:450
    public override void setup_adult(mob_living_basic_raptor raptor = null)
    {
        base.setup_adult(raptor);
        // Slightly worse than greens at this
        float ability_scale = 1 - INVERSE_LERP(RAPTOR_INHERIT_MIN_MODIFIER, RAPTOR_INHERIT_MAX_MODIFIER, Dm.num(raptor.inherited_stats.ability_modifier));
        float mining_mod = round(ability_scale * 0.2f, 0.025f);
        if (ability_scale >= 0.8f)
            mining_mod = 0;
        Waiting.NoSleep(raptor.AddComponent(typeof(datum_component_proficient_miner), mining_mod, TRUE));
    }
}
