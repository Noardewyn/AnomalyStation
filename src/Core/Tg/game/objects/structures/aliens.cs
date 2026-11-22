// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/aliens.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: /obj/structure/alien and /obj/structure/alien/resin (gaps E4b, D327: the kudzu's flower bud is a resin
// structure) and (gaps X5, D333) the gelatinous mound, the resin walls and membranes, the weeds with their nodes (the spread from a
// node, the blue light) and the eggs (growing, grown, the opening flick, hatched; the hugger inside). The var blocks are generated
// (D111).

/* Alien shit!
 * Contains:
 * structure/alien
 * Resin
 * Weeds
 * Egg
 */

// tg: code/game/objects/structures/aliens.dm:10
public partial class obj_structure_alien : obj_structure
{
    // tg: code/game/objects/structures/aliens.dm:14
    public override async DmTask<object> run_atom_armor(object damage_amount = null, object damage_type = null, object damage_flag = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_flag ??= 0;
        if (Dm.eq(damage_flag, MELEE))
        {
            switch (Dm.str(damage_type))
            {
                case BRUTE:
                    damage_amount = Dm.num(damage_amount) * 0.25f;
                    break;
                case BURN:
                    damage_amount = Dm.num(damage_amount) * 2;
                    break;
            }
        }
        return await base.run_atom_armor(damage_amount, damage_type, damage_flag, attack_dir, armour_penetration);
    }

    // tg: code/game/objects/structures/aliens.dm:23
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        switch (damage_type)
        {
            case BRUTE:
                if (Dm.truthy(damage_amount))
                    playsound(loc, Dm.resource("sound/effects/blob/attackblob.ogg"), 100, TRUE);
                else
                    playsound(this, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
                break;
            case BURN:
                if (Dm.truthy(damage_amount))
                    playsound(loc, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
                break;
        }
        return null;
    }
}

// tg: code/game/objects/structures/aliens.dm:38
public partial class obj_structure_alien_gelpod : obj_structure_alien
{
    // tg: code/game/objects/structures/aliens.dm:44
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        Dm.@new<obj_effect_mob_spawn_corpse_human_damaged>(get_turf(this));
    }
}

/*
 * Resin
 */
// tg: code/game/objects/structures/aliens.dm:50
public partial class obj_structure_alien_resin : obj_structure_alien
{
    // tg: code/game/objects/structures/aliens.dm:67
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        air_update_turf(TRUE, TRUE);
        ADD_TRAIT(this, TRAIT_INVERTED_DEMOLITION, INNATE_TRAIT);
        return dot;
    }

    // tg: code/game/objects/structures/aliens.dm:72
    public override object Destroy(float? force = null)
    {
        air_update_turf(TRUE, FALSE);
        object dot = base.Destroy(force);
        return dot;
    }

    // tg: code/game/objects/structures/aliens.dm:76
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        turf T = loc as turf;
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        move_update_air(T);
        return dot;
    }

    // tg: code/game/objects/structures/aliens.dm:116
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }
}

// tg: code/game/objects/structures/aliens.dm:81
public partial class obj_structure_alien_resin_wall : obj_structure_alien_resin
{
    // tg: code/game/objects/structures/aliens.dm:91
    public override object block_superconductivity()
    {
        return 1;
    }
}

/*
 * Weeds
 */

// tg: code/game/objects/structures/aliens.dm:131
public partial class obj_structure_alien_weeds : obj_structure_alien
{
    // tg: code/game/objects/structures/aliens.dm:129  #define NODERANGE 3 (#undef at :317; the node_range default, generated)
    ///the list of turfs that the weeds will not be able to grow over
    // tg: code/game/objects/structures/aliens.dm:151  var/static/list/blacklisted_turfs
    public static ref DmList blacklisted_turfs => ref TgState.Current.obj_structure_alien_weeds__blacklisted_turfs;

    // tg: code/game/objects/structures/aliens.dm:159
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        //so the sprites line up right in the map editor
        pixel_x = -4;
        pixel_y = -4;

        object dot = base.Initialize(mapload, __extra);

        set_base_icon();

        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        return dot;
    }

    // tg: code/game/objects/structures/aliens.dm:170
    public override object Destroy(float? force = null)
    {
        if (parent_node != null)
        {
            UnregisterSignal(parent_node, COMSIG_QDELETING);
            parent_node = null;
        }
        return base.Destroy(force);
    }

    ///Randomizes the weeds' starting icon, gets redefined by children for them not to share the behavior.
    // tg: code/game/objects/structures/aliens.dm:177
    public virtual object set_base_icon()
    {
        object dot = base_icon_state;
        switch (rand(1, 3))
        {
            case 1:
                icon = Dm.resource("icons/obj/smooth_structures/alien/weeds1.dmi");
                base_icon_state = "weeds1";
                break;
            case 2:
                icon = Dm.resource("icons/obj/smooth_structures/alien/weeds2.dmi");
                base_icon_state = "weeds2";
                break;
            case 3:
                icon = Dm.resource("icons/obj/smooth_structures/alien/weeds3.dmi");
                base_icon_state = "weeds3";
                break;
        }
        set_smoothed_icon_state(smoothing_junction);
        return dot;
    }

    /**
     * Called when the node is trying to grow/expand
     */
    // tg: code/game/objects/structures/aliens.dm:194
    public virtual void try_expand()
    {
        //we cant grow without a parent node
        if (parent_node == null)
            return;
        //lets make sure we are still on a valid location
        turf src_turf = get_turf(this);
        if (Dm.truthy(is_type_in_list(src_turf, blacklisted_turfs)))
        {
            qdel(this);
            return;
        }
        //lets try to grow in a direction
        foreach (turf check_turf in Dm.each<turf>(src_turf.get_atmos_adjacent_turfs()))
        {
            //we cannot grow on blacklisted turfs
            if (Dm.truthy(is_type_in_list(check_turf, blacklisted_turfs)))
                continue;
            obj_structure_alien_weeds check_weed = Dm.locate<obj_structure_alien_weeds>(check_turf);
            //we cannot grow onto other weeds
            if (check_weed != null)
                continue;
            //spawn a new one in the turf
            check_weed = Dm.@new<obj_structure_alien_weeds>(check_turf);
            //set the new one's parent node to our parent node
            check_weed.parent_node = parent_node;
            check_weed.RegisterSignal(parent_node, COMSIG_QDELETING, nameof(after_parent_destroyed));
        }
    }

    /**
     * Called when the parent node is destroyed
     */
    // tg: code/game/objects/structures/aliens.dm:221
    public virtual void after_parent_destroyed()
    {
        if (!Dm.truthy(find_new_parent()))
        {
            float random_time = rand(2 * SECONDS, 8 * SECONDS);
            _addtimer(Dm.@new<datum_callback>(this, nameof(do_qdel)), random_time, file: "code/game/objects/structures/aliens.dm", line: 224);
        }
    }

    /**
     * Called when trying to find a new parent after our previous parent died
     * Will return false if it can't find a new_parent
     * Will return the new parent if it can find one
     */
    // tg: code/game/objects/structures/aliens.dm:231
    public virtual object find_new_parent()
    {
        object previous_node = parent_node;
        parent_node = null;
        foreach (obj_structure_alien_weeds_node new_parent in Dm.each<obj_structure_alien_weeds_node>(range(node_range, this)))
        {
            if (new_parent == previous_node)
                continue;
            parent_node = new_parent;
            RegisterSignal(parent_node, COMSIG_QDELETING, nameof(after_parent_destroyed));
            return parent_node;
        }
        return FALSE;
    }

    /**
     * Called to delete the weed
     */
    // tg: code/game/objects/structures/aliens.dm:245
    public virtual void do_qdel()
    {
        qdel(this);
    }

    // tg: code/game/objects/structures/aliens.dm:248
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(Dm.num(exposed_temperature) > 300);
    }

    // tg: code/game/objects/structures/aliens.dm:251
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        await take_damage(5, BURN, 0, 0);
        return null;
    }
}

// tg: code/game/objects/structures/aliens.dm:254
public partial class obj_structure_alien_weeds_node : obj_structure_alien_weeds
{
    // tg: code/game/objects/structures/aliens.dm:271
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        //give it light
        Waiting.NoSleep(set_light(lon_range));

        //we are the parent node
        parent_node = this;

        return INITIALIZE_HINT_LATELOAD;
    }

    // we do this in LateInitialize() because weeds on the same loc may not be done initializing yet (as in create_and_destroy)
    // tg: code/game/objects/structures/aliens.dm:283
    public override void LateInitialize(params object[] __extra)
    {
        //destroy any non-node weeds on turf
        obj_structure_alien_weeds check_weed = Dm.locate<obj_structure_alien_weeds>(loc);
        if (check_weed != null && check_weed != this)
            qdel(check_weed);

        //start the cooldown
        growtime = world.time + rand(minimum_growtime, maximum_growtime); // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline (rules 11.1 (a))

        //start processing
        START_PROCESSING(SSobj, this);
    }

    // tg: code/game/objects/structures/aliens.dm:295
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/aliens.dm:299
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        //we need to have a cooldown, so check and then add
        if (!(growtime <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return null;
        growtime = world.time + rand(minimum_growtime, maximum_growtime); // COOLDOWN_START
        //attempt to grow all weeds in range
        foreach (obj_structure_alien_weeds growing_weed in Dm.each<obj_structure_alien_weeds>(range(node_range, this)))
            growing_weed.try_expand();
        return null;
    }

    // tg: code/game/objects/structures/aliens.dm:308
    public override object set_base_icon()
    {
        return null; //No icon randomization at init. The node's icon is already well defined.
    }
}

/*
 * Egg
 */

// tg: code/game/objects/structures/aliens.dm:333
public partial class obj_structure_alien_egg : obj_structure_alien
{
    //for the status var
    // tg: code/game/objects/structures/aliens.dm:325  #define BURSTING "bursting" (#undef at :480)
    private const string BURSTING = "bursting";
    // tg: code/game/objects/structures/aliens.dm:326  #define BURST "burst" (#undef at :481)
    private const string BURST = "burst";
    // tg: code/game/objects/structures/aliens.dm:327  #define GROWING "growing" (#undef at :482)
    private const string GROWING = "growing";
    // tg: code/game/objects/structures/aliens.dm:328  #define GROWN "grown" (#undef at :483)
    private const string GROWN = "grown";
    // tg: code/game/objects/structures/aliens.dm:329  #define FAKE "fake" (#undef at :479)
    private const string FAKE = "fake";
    // tg: code/game/objects/structures/aliens.dm:330  #define MIN_GROWTH_TIME 900 (#undef at :484)
    private const int MIN_GROWTH_TIME = 900; //time it takes to grow a hugger
    // tg: code/game/objects/structures/aliens.dm:331  #define MAX_GROWTH_TIME 1500 (#undef at :485)
    private const int MAX_GROWTH_TIME = 1500;

    // tg: code/game/objects/structures/aliens.dm:349
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        if (status == GROWING || status == GROWN)
            child = Dm.@new<obj_item_clothing_mask_facehugger>(this);
        if (status == GROWING)
            _addtimer(Dm.@new<datum_callback>(this, nameof(Grow)), rand(MIN_GROWTH_TIME, MAX_GROWTH_TIME), file: "code/game/objects/structures/aliens.dm", line: 355);
        proximity_monitor = Dm.@new<datum_proximity_monitor>(this, status == GROWN ? 1 : 0);
        if (status == BURST)
            atom_integrity = integrity_failure * max_integrity;

        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        return dot;
    }

    // tg: code/game/objects/structures/aliens.dm:362
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref child);
        QDEL_NULL(ref proximity_monitor);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/aliens.dm:367
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        switch (status)
        {
            case GROWING:
                icon_state = Dm.text("[]_growing", base_icon_state);
                break;
            case GROWN:
                icon_state = Dm.text("[]", base_icon_state);
                break;
            case BURST:
                icon_state = Dm.text("[]_hatched", base_icon_state);
                break;
            case FAKE:
                icon_state = Dm.text("[]_growing", base_icon_state);
                break;
        }
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/aliens.dm:379
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/structures/aliens.dm:382
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/structures/aliens.dm:385
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (user.get_organ_by_type(typeof(obj_item_organ_alien_plasmavessel)) != null)
        {
            switch (status)
            {
                case BURSTING:
                    to_chat(user, span_notice(I18n.T("The child is hatching out.")));
                    return dot;
                case BURST:
                    to_chat(user, span_notice(I18n.T("You clear the hatched egg.")));
                    playsound(loc, Dm.resource("sound/effects/blob/attackblob.ogg"), 100, TRUE);
                    qdel(this);
                    return dot;
                case GROWING:
                    to_chat(user, span_notice(I18n.T("The child is not developed yet.")));
                    return dot;
                case GROWN:
                    to_chat(user, span_notice(I18n.T("You retrieve the child.")));
                    await Burst(kill: FALSE);
                    return dot;
            }
        }
        else
        {
            to_chat(user, span_notice(I18n.T("It feels slimy.")));
            user.changeNext_move(CLICK_CD_MELEE);
        }
        return dot;
    }


    // tg: code/game/objects/structures/aliens.dm:411
    public virtual async DmTask Grow()
    {
        status = GROWN;
        update_appearance();
        await proximity_monitor.set_range(1);
    }

    //drops and kills the hugger if any is remaining
    // tg: code/game/objects/structures/aliens.dm:417
    public virtual async DmTask Burst(float? kill = null)
    {
        kill ??= TRUE;
        if (status == GROWN || status == GROWING)
        {
            status = BURSTING;
            await proximity_monitor.set_range(0);
            flick("egg_opening", this);
            _addtimer(Dm.@new<datum_callback>(this, nameof(finish_bursting), kill), 1.5f * SECONDS, file: "code/game/objects/structures/aliens.dm", line: 422);
        }
    }

    // tg: code/game/objects/structures/aliens.dm:424
    public virtual async DmTask finish_bursting(float? kill = null)
    {
        kill ??= TRUE;
        status = BURST;
        update_appearance();
        if (child != null)
        {
            await child.forceMove(get_turf(this));
            // TECHNICALLY you could put non-facehuggers in the child var
            if (child is obj_item_clothing_mask_facehugger)
            {
                if (Dm.truthy(kill))
                    await child.die();
                else
                {
                    foreach (mob M in Dm.each<mob>(range(1, this)))
                    {
                        if (Dm.truthy(CanHug(M as mob_living)))
                        {
                            await child.leap_to(M as mob_living);
                            break;
                        }
                    }
                }
            }
        }
    }

    // tg: code/game/objects/structures/aliens.dm:439
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == child)
            child = null;
        return dot;
    }

    // tg: code/game/objects/structures/aliens.dm:444
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(Dm.num(exposed_temperature) > 500);
    }

    // tg: code/game/objects/structures/aliens.dm:447
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        await take_damage(5, BURN, 0, 0);
        return null;
    }

    // tg: code/game/objects/structures/aliens.dm:450
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (status != BURST)
            await Burst(kill: TRUE);
        return dot;
    }

    // tg: code/game/objects/structures/aliens.dm:455
    [DmParams("AM")]
    public override async DmTask<object> HasProximity(atom_movable proximity_check_mob = null)
    {
        atom_movable AM = proximity_check_mob;
        if (status == GROWN)
        {
            if (!Dm.truthy(CanHug(AM as mob_living)))
                return null;

            mob_living_carbon C = AM as mob_living_carbon;
            if (!IS_UNCONSCIOUS_OR_CRIT(AM) && C?.get_organ_by_type(typeof(obj_item_organ_body_egg_alien_embryo)) != null)
                return null;

            await Burst(kill: FALSE);
        }
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/aliens.dm:151
    internal DmList obj_structure_alien_weeds__blacklisted_turfs = Dm.list(
        typeof(turf_open_space),
        typeof(turf_open_chasm),
        typeof(turf_open_lava),
        typeof(turf_open_water),
        typeof(turf_open_openspace));
}
