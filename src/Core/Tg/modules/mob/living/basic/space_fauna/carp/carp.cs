// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/carp/carp.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but Cayenne's disk (the nuclear operator component, inert with the nuke, a row) (gaps package C4).

/**
 * # Space Carp
 *
 * Fast, hostile fish that nibble you to death and tear holes in the hull with their rifts.
 */
// tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:9
public partial class mob_living_basic_carp : mob_living_basic
{
    /// Carp want to eat raw meat
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:55
    private static DmList desired_food => TgState.Current.mob_living_basic_carp__desired_food ??= Dm.list(typeof(obj_item_food_meat_slab), typeof(obj_item_food_meat_rawcutlet));
    /// Carp want to eat delicious six pack rings
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:57
    private static DmList desired_trash => TgState.Current.mob_living_basic_carp__desired_trash ??= Dm.list(typeof(obj_item_storage_cans));
    /// Structures that AI carp are willing to attack. This prevents them from deciding to smash a table to death for no reason.
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:59
    private static DmList allowed_obstacle_targets => TgState.Current.mob_living_basic_carp__allowed_obstacle_targets ??= (DmList)typecacheof(Dm.list(
        typeof(obj_structure_closet),
        typeof(obj_machinery_door),
        typeof(obj_structure_door_assembly),
        typeof(obj_structure_filingcabinet),
        typeof(obj_structure_frame),
        typeof(obj_structure_grille),
        typeof(obj_structure_plasticflaps),
        typeof(obj_structure_rack),
        typeof(obj_structure_reagent_dispensers), // Carp can have a little welding fuel, as a treat
        typeof(obj_structure_table),
        typeof(obj_machinery_vending),
        typeof(obj_structure_window)));

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:86
    [DmParams("mapload", "tamer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob tamer = Dm.arg(__extra, 0) as mob;
        ADD_TRAIT(this, TRAIT_FREE_HYPERSPACE_MOVEMENT, INNATE_TRAIT); //Need to set before init cause if we init in hyperspace we get dragged before the trait can be added
        object dot = base.Initialize(mapload);
        apply_colour();
        add_traits(Dm.list(TRAIT_HEALS_FROM_CARP_RIFTS, TRAIT_SPACEWALK), INNATE_TRAIT);

        if (cell_line != null && Dm.truthy(cell_line))
            Waiting.NoSleep(AddElement(typeof(datum_element_swabable), cell_line, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));

        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        if (!Dm.truthy(cowardly))
            Waiting.NoSleep(AddElement(typeof(datum_element_ai_flee_while_injured)));
        setup_eating();

        Waiting.NoSleep(AddComponent(typeof(datum_component_speechmod), strings("crustacean_replacement.json", "crustacean")));
        Waiting.NoSleep(AddComponent(typeof(datum_component_aggro_emote), Dm.named("emote_list", string_list(Dm.list("gnashes")))));
        Waiting.NoSleep(AddComponent(typeof(datum_component_regenerator), Dm.named("outline_colour", regenerate_colour)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_profound_fisher)));
        if (tamer != null)
        {
            Waiting.NoSleep(tamed(tamer as mob_living, null, FALSE));
            Waiting.NoSleep(befriend(tamer as mob_living));
        }
        else
        {
            DmList food_types = TgState.Current.mob_living_basic_carp__food_types ??= Dm.list(typeof(obj_item_food_meat));
            Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), food_types, 10f, 5f));
        }

        teleport = Dm.@new<datum_action_cooldown_mob_cooldown_lesser_carp_rift>(this);
        Waiting.NoSleep(teleport.Grant(this));
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_CARP_RIFT, teleport);
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_OBSTACLE_TARGETING_WHITELIST, allowed_obstacle_targets);
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:120
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref teleport);
        return base.Destroy(force);
    }

    /// Tell the elements and the blackboard what food we want to eat
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:125
    public virtual object setup_eating()
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), null, null, null, null, null, null, desired_food));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), 0f, 10f, BRUTE, null, null, null, desired_trash)); // We are killing our planet
        DmList foods_list = Dm.list();
        Dm.combine(ref foods_list, desired_food);
        Dm.combine(ref foods_list, desired_trash);
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_BASIC_FOODS, typecacheof(foods_list));
        return null;
    }

    /// Set a random colour on the carp, override to do something else
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:132
    public virtual void apply_colour()
    {
        if (greyscale_config == null)
            return;
        Waiting.NoSleep(set_greyscale(colors: Dm.list(pick_weight(GLOB.carp_colors))));
    }

    /// Called when another mob has forged a bond of friendship with this one, passed the taming mob as 'tamer'
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:138
    public override async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        object feedback = Dm.arg(__extra, 0) ?? TRUE;
        await base.tamed(tamer, food);
        await AddElement(typeof(datum_element_ridable), ridable_data);
        await AddComponent(typeof(datum_component_obeys_commands), tamed_commands);
        if (!Dm.truthy(feedback))
            return;
        spin(spintime: 10f, speed: 1f);
        visible_message(Dm.text("[] spins in a circle as it seems to bond with [].", this, tamer));
    }

    /// Teleport when you right click away from you
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:147
    public override async DmTask<object> ranged_secondary_attack(atom atom_target = null, object modifiers = null)
    {
        await teleport.Trigger(null, null, atom_target);
        return null;
    }

    /// Gives the carp a list of weakrefs of destinations to try and travel between when it has nothing better to do
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:151
    public virtual void migrate_to(DmList migration_points = null)
    {
        datum_ai_controller controller = ai_controller as datum_ai_controller;
        controller.ai_traits = Dm.bitor(controller.ai_traits, RUN_WHILE_UNWATCHED);
        Waiting.NoSleep(controller.reset_ai_status()); // We need them to actually keep walking to the station
        DmList actual_points = Dm.list();
        foreach (datum_weakref point_ref in Dm.each_cast<datum_weakref>(migration_points))
        {
            turf point_resolved = point_ref.resolve() as turf;
            if (QDELETED(point_resolved))
                return; // invalid list, we can't migrate to this
            Dm.append(ref actual_points, point_resolved);
        }
        controller.set_blackboard_key(BB_CARP_MIGRATION_PATH, actual_points);
    }

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:162
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = await base.death(gibbed, __extra);
        REMOVE_TRAIT(this, TRAIT_FREE_HYPERSPACE_MOVEMENT, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:166
    public override async DmTask<object> revive(float? full_heal_flags = null, float? excess_healing = null, float? force_grab_ghost = null)
    {
        object dot = await base.revive(full_heal_flags, excess_healing, force_grab_ghost);
        ADD_TRAIT(this, TRAIT_FREE_HYPERSPACE_MOVEMENT, INNATE_TRAIT);
        return dot;
    }
}

/// Holodeck carp
// tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:174
public partial class mob_living_basic_carp_holographic : mob_living_basic_carp
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:182
    [DmParams("mapload", "tamer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_holographic_nature)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:186
    public override object setup_eating()
    {
        return FALSE;
    }
}

/// Pet carp, abstract carp which is tamer and speaks common
// tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:193
public partial class mob_living_basic_carp_pet : mob_living_basic_carp
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:200
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload);
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "bloop"));
        return dot;
    }
}

/// Cayenne, Nuclear Operatives' pet
// tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:219
public partial class mob_living_basic_carp_pet_cayenne : mob_living_basic_carp_pet
{
    private const int RARE_CAYENNE_CHANCE = 10; // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:216

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:229
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/mob/living/basic/space_fauna/carp/carp.dm:232", "Initialize: the nuclear bomb operator component and Cayenne's tacmap implant (the nuke and the disk stay inert)");
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:237
    public override void apply_colour()
    {
        if (prob(RARE_CAYENNE_CHANCE))
            Waiting.NoSleep(set_greyscale(colors: Dm.list(COLOR_CARP_SILVER)));
        else
            base.apply_colour();
    }

    /// She did it! Treats for Cayenne!!
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:244
    public virtual void got_disk(obj_item_disk_nuclear disky = null)
    {
        Dm.Deferred("code/modules/mob/living/basic/space_fauna/carp/carp.dm:244", "got_disk: the achievement of the disk (awards)");
    }

    /// Adds an extra overlay if we're carrying the disk
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:250
    public virtual void display_disk(DmList new_overlays = null)
    {
        Dm.Deferred("code/modules/mob/living/basic/space_fauna/carp/carp.dm:250", "display_disk: the disk overlay (the disk stays inert)");
    }
}

/// Ella, she was a carp
// tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:272
public partial class mob_living_basic_carp_ella : mob_living_basic_carp
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:277
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(death()); // It comes into the world dead when the disease is cured
        return dot;
    }
}

/// Fake carp that will not attack anyone, but will retaliate
// tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:282
public partial class mob_living_basic_carp_passive : mob_living_basic_carp
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:292
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ai_retaliate_advanced), Dm.@new<datum_callback>(this, nameof(on_attacked))));
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "bloop"));
        ADD_TRAIT(this, TRAIT_PACIFISM, INNATE_TRAIT);
        return dot;
    }

    /// Alert the other false carp of who hit us
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp.dm:298
    public virtual void on_attacked(mob_living attacker = null)
    {
        foreach (mob_living_basic_carp_passive schoolmate in Dm.each<mob_living_basic_carp_passive>(oview(this, 9)))
            (schoolmate.ai_controller as datum_ai_controller)?.set_blackboard_key_assoc_lazylist(BB_BASIC_MOB_RETALIATE_LIST, attacker, world.time);
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_carp__desired_food;
    internal DmList mob_living_basic_carp__desired_trash;
    internal DmList mob_living_basic_carp__allowed_obstacle_targets;
    internal DmList mob_living_basic_carp__food_types;
}
