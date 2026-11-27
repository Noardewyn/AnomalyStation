// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:6
public partial class mob_living_basic_mining_gutlunch : mob_living_basic_mining
{
    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:1-4 (file-local defines, #undef at :174-177)
    internal const float MAX_ATTACK_DIFFERENCE = 3;
    internal const float MAX_LOWER_ATTACK = 15;
    internal const float MINIMUM_POSSIBLE_SPEED = 1;
    internal const float MAX_POSSIBLE_HEALTH = 100;

    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        GLOB.gutlunch_count++;
        if (greyscale_config != null)
            Waiting.NoSleep(set_greyscale(colors: Dm.list(pick(possible_colors))));
        if (Dm.truthy(can_breed))
            add_breeding_component();
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:42
    public override object Destroy(float? force = null)
    {
        GLOB.gutlunch_count--;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:46
    public override async DmTask<object> early_melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.early_melee_attack(target, modifiers, ignore_cooldown);
        if (Dm.truthy(dot))
            return dot;
        if (!(target is obj_structure_ore_container_food_trough_gutlunch_trough))
            return BASIC_MOB_CONTINUE_ATTACK_CHAIN;
        obj ore_food = Dm.locate<obj_item_stack_ore>(target);
        if (ore_food == null)
            balloon_alert(this, "no food!");
        else
            await UnarmedAttack(ore_food, TRUE, modifiers);
        return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
    }

    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:59
    public virtual async DmTask after_birth(mob_living_basic_mining_gutlunch_grub baby = null, mob_living partner = null)
    {
        object our_color = LAZYACCESS(atom_colours, FIXED_COLOUR_PRIORITY) ?? COLOR_GRAY;
        if (!Dm.truthy(our_color))
            our_color = COLOR_GRAY;
        object partner_color = LAZYACCESS(partner.atom_colours, FIXED_COLOUR_PRIORITY);
        if (!Dm.truthy(partner_color))
            partner_color = COLOR_GRAY;
        await baby.add_atom_colour(BlendRGB(our_color, partner_color, 1), FIXED_COLOUR_PRIORITY);
        atom male_parent = Dm.eq(gender, MALE) ? this : partner;
        baby.inherited_stats = Dm.@new<datum_gutlunch_inherited_stats>(male_parent);
    }

    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:66
    public virtual void roll_stats(object input_attack = null, object input_speed = null, object input_health = null)
    {
        melee_damage_lower = rand(Dm.num(input_attack), min(MAX_LOWER_ATTACK, Dm.num(input_attack) + MAX_ATTACK_DIFFERENCE));
        melee_damage_upper = melee_damage_lower + MAX_ATTACK_DIFFERENCE;
        speed = rand(MINIMUM_POSSIBLE_SPEED, Dm.num(input_speed));
        maxHealth = rand(Dm.num(input_health), MAX_POSSIBLE_HEALTH);
        health = maxHealth;
    }

    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:73
    public virtual void add_breeding_component()
    {
        DmList partner_paths = TgState.Current.mob_living_basic_mining_gutlunch__add_breeding_component__partner_paths ??= typecacheof(Dm.list(typeof(mob_living_basic_mining_gutlunch))) as DmList;
        DmList baby_paths = TgState.Current.mob_living_basic_mining_gutlunch__add_breeding_component__baby_paths ??= Dm.list(
            Dm.kv(typeof(mob_living_basic_mining_gutlunch_grub), 1));

        Waiting.NoSleep(AddComponent(
            typeof(datum_component_breed),
            Dm.named("can_breed_with", partner_paths),
            Dm.named("baby_paths", baby_paths),
            Dm.named("post_birth", Dm.@new<datum_callback>(this, nameof(after_birth))),
            Dm.named("breed_timer", 3 * MINUTES)));
    }
}

// tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:87
public partial class mob_living_basic_mining_gutlunch_milk : mob_living_basic_mining_gutlunch
{
    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:95
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_callback milking_callback = Dm.@new<datum_callback>(this, nameof(update_overlays));
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_udder),
            Dm.named("udder_type", typeof(obj_item_udder_gutlunch)),
            Dm.named("on_milk_callback", milking_callback),
            Dm.named("on_generate_callback", milking_callback)));
        full_udder = mutable_appearance(icon, "gl_full") as mutable_appearance;
        object udder_color = LAZYACCESS(atom_colours, FIXED_COLOUR_PRIORITY);
        full_udder.color = Dm.truthy(udder_color) ? udder_color : COLOR_GRAY;
        return dot;
    }

    // Kept as tg has it: the udder's callback calls update_overlays itself, whose list nobody applies, and a plain
    // update_appearance passes no volumes, so the full udder shows whenever the overlays are rebuilt.
    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:135
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object new_udder_volume = Dm.arg(__extra, 0);
        object max_udder_volume = Dm.arg(__extra, 1);
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.eq(new_udder_volume, max_udder_volume))
            return dot;
        Dm.append(ref dot, full_udder);
        return dot;
    }
}

// tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:107
public partial class mob_living_basic_mining_gutlunch_warrior : mob_living_basic_mining_gutlunch
{
    //pet commands when we tame the gutluncher
    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:118 (var/static/list/pet_commands)
    public static DmList pet_commands => TgState.Current.mob_living_basic_mining_gutlunch_warrior__pet_commands ??= Dm.list(
        typeof(datum_pet_command_idle),
        typeof(datum_pet_command_move),
        typeof(datum_pet_command_free),
        typeof(datum_pet_command_attack),
        typeof(datum_pet_command_breed_gutlunch),
        typeof(datum_pet_command_follow_start_active),
        typeof(datum_pet_command_fetch),
        typeof(datum_pet_command_mine_walls));

    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:129
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        roll_stats(melee_damage_lower, speed, maxHealth);
        Waiting.NoSleep(AddComponent(typeof(datum_component_obeys_commands), pet_commands));
        Waiting.NoSleep(AddElement(typeof(datum_element_wall_tearer), Dm.named("allow_reinforced", FALSE)));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:141
public partial class mob_living_basic_mining_gutlunch_grub : mob_living_basic_mining_gutlunch
{
    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:151
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_growth_and_differentiation),
            Dm.named("growth_time", 3 * MINUTES),
            Dm.named("growth_probability", 100),
            Dm.named("lower_growth_value", 0.5f),
            Dm.named("upper_growth_value", 1),
            Dm.named("signals_to_kill_on", Dm.list(COMSIG_MOB_CLIENT_LOGIN)),
            Dm.named("optional_checks", Dm.@new<datum_callback>(this, nameof(ready_to_grow))),
            Dm.named("optional_grow_behavior", Dm.@new<datum_callback>(this, nameof(determine_growth_path)))));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:164
    public virtual object ready_to_grow()
    {
        return !IS_UNCONSCIOUS_OR_CRIT(this) ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:167
    public virtual void determine_growth_path()
    {
        Type final_type = prob(50) ? typeof(mob_living_basic_mining_gutlunch_warrior) : typeof(mob_living_basic_mining_gutlunch_milk);
        mob_living_basic_mining_gutlunch grown_mob = Dm.@new(final_type, get_turf(this)) as mob_living_basic_mining_gutlunch;
        if (Dm.eq(grown_mob.gender, MALE) && inherited_stats != null)
            grown_mob.roll_stats(inherited_stats.attack, inherited_stats.speed, inherited_stats.health);
        qdel(this);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:74 (var/static/list/partner_paths)
    internal DmList mob_living_basic_mining_gutlunch__add_breeding_component__partner_paths;
    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:75 (var/static/list/baby_paths)
    internal DmList mob_living_basic_mining_gutlunch__add_breeding_component__baby_paths;
    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers.dm:118 (var/static/list/pet_commands)
    internal DmList mob_living_basic_mining_gutlunch_warrior__pet_commands;
}
