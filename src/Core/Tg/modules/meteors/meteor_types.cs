// Translated from tgstation (snapshot 1d99bd7f): code/modules/meteors/meteor_types.dm, meteor_spawning.dm (the debris locations), meteor_deflection.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D299: tg's meteors, for the shuttle's flight events. SSaugury (the doom watchers) and the achievements are not ported, nor the station's meteor waves and shields,
// which a later package brings; the changeling and dark matter meteors wait for their antagonists and stay inert.

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/_globalvars/lists/objects.dm:61
    public DmList meteor_list;

    public virtual void InitGlobalmeteor_list()
    {
        meteor_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "meteor_list");
    }

    // tg: code/__DEFINES/is_helpers.dm:108
    public DmList turfs_pass_meteor;

    public virtual void InitGlobalturfs_pass_meteor()
    {
        turfs_pass_meteor = typecacheof(Dm.list(
            typeof(turf_closed_mineral),
            typeof(turf_open_misc_asteroid),
            typeof(turf_open_openspace),
            typeof(turf_open_space))) as DmList;
        Dm.append(ref gvars_datum_init_order, "turfs_pass_meteor");
    }
}

// tg: code/modules/meteors/meteor_types.dm:3
public partial class obj_effect_meteor : obj_effect
{
    // tg: code/modules/meteors/meteor_types.dm:38
    [DmParams("mapload", "target")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var target = Dm.arg<atom>(__extra, 0);
        z_original = z;
        Dm.append(ref GLOB.meteor_list, this);
        SpinAnimation();
        chase_target(target);
        setup_extra_drops();
        Waiting.NoSleep(AddComponent(typeof(datum_component_meteor_combat), Dm.@new<datum_callback>(this, nameof(redirect)), Dm.@new<datum_callback>(this, nameof(make_debris))));
        return dot;
    }

    // tg: code/modules/meteors/meteor_types.dm:53
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.meteor_list, this);
        if (GLOB.move_manager.processing_on(this, SSmovement) is datum_move_loop moveloop)
            UnregisterSignal(moveloop, COMSIG_MOVELOOP_STOP);
        return base.Destroy(force);
    }

    // tg: code/modules/meteors/meteor_types.dm:60
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (QDELETED(this))
            return dot;

        if (old_loc != loc) //If did move, ram the turf we get in
        {
            turf T = get_turf(loc);
            ram_turf(T);

            if (prob(10) && !Dm.truthy(is_type_in_typecache(T, GLOB.turfs_pass_meteor))) //randomly takes a 'hit' from ramming
                await get_hit();
        }

        if (!QDELETED(this) && (!Dm.eq(z, z_original) || loc == get_turf(dest)))
            moved_off_z();
        return dot;
    }

    // tg: code/modules/meteors/meteor_types.dm:75
    public override async DmTask<object> Process_Spacemove(float? movement_dir = null, float? continuous_move = null) => TRUE; //Keeps us from drifting for no reason

    // tg: code/modules/meteors/meteor_types.dm:78
    public override async DmTask<object> Bump(atom A = null)
    {
        object dot = await base.Bump(A); //What could go wrong
        if (A != null)
        {
            ram_turf(get_turf(A));
            playsound(loc, meteorsound, 40, TRUE);
            await get_hit();
        }
        return dot;
    }

    // tg: code/modules/meteors/meteor_types.dm:85
    public virtual void chase_target(atom chasing = null, object delay = null, object home = null)
    {
        if (chasing == null)
            return;
        if (GLOB.move_manager.move_towards(this, chasing, delay, home, lifetime) is datum_move_loop new_loop)
            RegisterSignal(new_loop, COMSIG_MOVELOOP_STOP, nameof(on_loop_stopped));
    }

    // tg: code/modules/meteors/meteor_types.dm:92
    public virtual void setup_extra_drops()
    {
    }

    // tg: code/modules/meteors/meteor_types.dm:95
    public virtual void on_loop_stopped(datum source = null)
    {
        if (move_packet == null || !Dm.truthy(length(move_packet.existing_loops)))
            qdel(this);
    }

    ///Deals with what happens when we stop moving, IE we die
    // tg: code/modules/meteors/meteor_types.dm:101
    public virtual void moved_off_z()
    {
        qdel(this);
    }

    // tg: code/modules/meteors/meteor_types.dm:104
    public virtual void ram_turf(turf T = null)
    {
        //first yell at mobs about them dying horribly
        foreach (mob_living thing in Dm.each<mob_living>(T))
            thing.visible_message(span_warning(Dm.text("[] slams into [].", this, thing)), span_userdanger(Dm.text("[] slams into you!.", this)));

        //then, ram the turf
        if (hitpwr == EXPLODE_DEVASTATE)
            Dm.append(ref SSexplosions.highturf, T);
        else if (hitpwr == EXPLODE_HEAVY)
            Dm.append(ref SSexplosions.medturf, T);
        else if (hitpwr == EXPLODE_LIGHT)
            Dm.append(ref SSexplosions.lowturf, T);
    }

    //process getting 'hit' by colliding with a dense object
    //or randomly when ramming turfs
    // tg: code/modules/meteors/meteor_types.dm:120
    public virtual async DmTask get_hit()
    {
        hits--;
        if (hits <= 0)
        {
            make_debris();
            await meteor_effect();
            qdel(this);
        }
    }

    // tg: code/modules/meteors/meteor_types.dm:127  (the athletics line: gaps SL1; the award is not ported)
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (Dm.ge(user.mind?.get_skill_level(typeof(datum_skill_athletics)), SKILL_LEVEL_LEGENDARY))
            Dm.append(ref dot, span_notice(I18n.T("On second thought, it doesn't look too tough.")));
        check_examine_award(user);
        return dot;
    }

    ///Called by component/meteor_combat to send us moving to the edge of the map away from whoever punched us
    // tg: code/modules/meteors/meteor_types.dm:135
    public virtual void redirect(mob athlete = null)
    {
        // get_cardinal_dir (__HELPERS/atoms.dm:82)
        float dx = Math.Abs(x - athlete.x), dy = Math.Abs(y - athlete.y);
        dest = spaceDebrisStartLoc(Dm.bitand(get_dir(athlete, this), rand() * (dx + dy) < dy ? 3 : 12), z);
        chase_target(dest);
    }

    // tg: code/modules/meteors/meteor_types.dm:139
    public virtual void make_debris()
    {
        for (float throws = dropamt; throws > 0; throws--)
            Dm.@new((Type)pick(meteordrop), get_turf(this));
    }

    // tg: code/modules/meteors/meteor_types.dm:144
    public virtual async DmTask meteor_effect()
    {
        if (!Dm.truthy(heavy))
            return;
        object random_frequency = get_rand_frequency();

        foreach (mob M in Dm.each<mob>(GLOB.player_list))
        {
            turf T = get_turf(M);
            if (T == null || T.z != z)
                continue;
            float dist = Dm.num(get_dist(M.loc, loc));
            shake_camera(M, dist > 20 ? 2 : 4, dist > 20 ? 1 : 3);
            M.playsound_local(loc, null, 50, 1, random_frequency, 10, sound_to_use: sound(meteorsound));
        }
    }

    // tg: code/modules/meteors/meteor_types.dm:169  (the award is not ported)
    public virtual void check_examine_award(mob user = null)
    {
    }

    // tg: code/modules/meteors/meteor_types.dm:184
    public virtual object shield_defense(atom defender = null) => TRUE;


    internal const float MAP_EDGE_PAD = 5;
}

// tg: code/modules/meteors/meteor_types.dm:192
public partial class obj_effect_meteor_sand : obj_effect_meteor
{
    // tg: code/modules/meteors/meteor_types.dm:200
    public override void make_debris()
    {
        //We drop NOTHING
    }

    // tg: code/modules/meteors/meteor_types.dm:203
    public override void ram_turf(turf turf_to_ram = null)
    {
        if (turf_to_ram is turf_closed_wall wall_to_ram && wall_to_ram.hardness <= 25) //sand is too weak to affect rwalls or walls with similar durability.
            return;

        var area_to_check = (area)get_area(turf_to_ram);
        if (Dm.truthy(Dm.bitand(area_to_check.area_flags, EVENT_PROTECTED))) //This event absolutely destroys arrivals, and putting latejoiners into firelock hell is cringe
            return;

        base.ram_turf(turf_to_ram);
    }

    // tg: code/modules/meteors/meteor_types.dm:215
    public override void check_examine_award(mob user = null)
    {
    }
}

// tg: code/modules/meteors/meteor_types.dm:230
public partial class obj_effect_meteor_medium : obj_effect_meteor
{
    // tg: code/modules/meteors/meteor_types.dm:235
    public override async DmTask meteor_effect()
    {
        await base.meteor_effect();
        await explosion(this, heavy_impact_range: 1, light_impact_range: 2, flash_range: 3, adminlog: FALSE);
    }
}

// tg: code/modules/meteors/meteor_types.dm:240
public partial class obj_effect_meteor_big : obj_effect_meteor
{
    // tg: code/modules/meteors/meteor_types.dm:248
    public override async DmTask meteor_effect()
    {
        await base.meteor_effect();
        await explosion(this, devastation_range: 1, heavy_impact_range: 2, light_impact_range: 3, flash_range: 4, adminlog: FALSE);
    }
}

// tg: code/modules/meteors/meteor_types.dm:253
public partial class obj_effect_meteor_flaming : obj_effect_meteor
{
    // tg: code/modules/meteors/meteor_types.dm:264
    public override async DmTask meteor_effect()
    {
        await base.meteor_effect();
        await explosion(this, devastation_range: 1, heavy_impact_range: 2, light_impact_range: 3, flame_range: 5, flash_range: 4, adminlog: FALSE);
    }
}

// tg: code/modules/meteors/meteor_types.dm:269
public partial class obj_effect_meteor_irradiated : obj_effect_meteor
{
    // tg: code/modules/meteors/meteor_types.dm:279
    public override async DmTask meteor_effect()
    {
        await base.meteor_effect();
        await explosion(this, heavy_impact_range: 1, light_impact_range: 3, flash_range: 6, adminlog: FALSE);
        foreach (turf_open_floor surviving_ground in Dm.each<turf_open_floor>(range(2, get_turf(this))))
        {
            if (prob(70))
                Dm.@new<obj_effect_decal_cleanable_greenglow_radioactive>(get_turf(surviving_ground));
        }
    }
}

// tg: code/modules/meteors/meteor_types.dm:287
public partial class obj_effect_meteor_cluster : obj_effect_meteor
{
    // tg: code/modules/meteors/meteor_types.dm:299
    public override async DmTask meteor_effect()
    {
        await base.meteor_effect();

        turf start_turf = get_turf(this);

        while (cluster_count > 0)
        {
            object startSide = pick(GLOB.cardinals);
            turf destination = spaceDebrisStartLoc(startSide, z);
            Dm.@new<obj_effect_meteor_cluster_fragment>(start_turf, destination);
            cluster_count--;
        }

        await explosion(this, heavy_impact_range: 2, light_impact_range: 3, flash_range: 4, adminlog: FALSE);
    }
}

// tg: code/modules/meteors/meteor_types.dm:318
public partial class obj_effect_meteor_carp : obj_effect_meteor
{
    // tg: code/modules/meteors/meteor_types.dm:329
    [DmParams("mapload", "target")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (prob(2))
            meteordrop = Dm.list(typeof(mob_living_basic_carp_mega)); //hehe
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/meteors/meteor_types.dm:335
public partial class obj_effect_meteor_bluespace : obj_effect_meteor
{
    // tg: code/modules/meteors/meteor_types.dm:345
    public override async DmTask<object> Bump(atom bumped_atom = null)
    {
        await base.Bump(bumped_atom);
        if (!QDELETED(this) && prob(35))
            await do_teleport(this, get_turf(this), 6, asoundin: Dm.resource("sound/effects/phasein.ogg"), channel: TELEPORT_CHANNEL_BLUESPACE);
        return null;
    }
}

// tg: code/modules/meteors/meteor_types.dm:350
public partial class obj_effect_meteor_banana : obj_effect_meteor
{
    // tg: code/modules/meteors/meteor_types.dm:362
    public override async DmTask meteor_effect()
    {
        await base.meteor_effect();
        playsound(this, Dm.resource("sound/items/airhorn/AirHorn.ogg"), 100, TRUE, -1);
        turf here = get_turf(this);
        foreach (atom_movable @object in Dm.each<atom_movable>(view(4, here)))
        {
            var throwtarget = (atom)get_edge_target_turf(here, get_dir(here, get_step_away(@object, here)));
            await @object.safe_throw_at(throwtarget, 5, 1, force: MOVE_FORCE_STRONG);
        }
    }

    // tg: code/modules/meteors/meteor_types.dm:369
    public override void ram_turf(turf bumped = null)
    {
        foreach (mob_living slipped in Dm.each<mob_living>(get_turf(bumped)))
        {
            Waiting.Detach(slipped.slip(100, slipped.loc, Dm.bitor(-GALOSHES_DONT_HELP, SLIDE)));
            slipped.visible_message(span_warning(Dm.text("[] honks [] to the floor!", this, slipped)), span_userdanger(Dm.text("[] harmlessly passes through you, knocking you over.", this)));
        }
        Waiting.Detach(get_hit());
    }
}

// tg: code/modules/meteors/meteor_types.dm:375
public partial class obj_effect_meteor_emp : obj_effect_meteor
{
    // tg: code/modules/meteors/meteor_types.dm:383
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = await base.Move(newloc, direction, glide_size_override, update_dir);
        if (Dm.truthy(dot))
            Dm.@new<obj_effect_temp_visual_impact_effect_ion>(get_turf(this));
        return dot;
    }

    // tg: code/modules/meteors/meteor_types.dm:388
    public override async DmTask meteor_effect()
    {
        await base.meteor_effect();
        playsound(this, Dm.resource("sound/items/weapons/zapbang.ogg"), 100, TRUE, -1);
        await empulse(this, 3, 8, emp_source: this);
    }
}

// tg: code/modules/meteors/meteor_types.dm:394
public partial class obj_effect_meteor_meaty : obj_effect_meteor
{
    // tg: code/modules/meteors/meteor_types.dm:406
    public override void setup_extra_drops()
    {
        Dm.append(ref meteordrop, pick(subtypesof(typeof(obj_item_food_meat_slab_human_mutant))));
        Dm.append(ref meteordrop, pick(typesof(typeof(obj_item_organ_tongue))));
    }

    // tg: code/modules/meteors/meteor_types.dm:410
    public override void make_debris()
    {
        base.make_debris();
        Dm.@new(meteorgibs, get_turf(this));
    }

    // tg: code/modules/meteors/meteor_types.dm:414
    public override void ram_turf(turf T = null)
    {
        if (!isspaceturf(T))
            Dm.@new<obj_effect_decal_cleanable_blood>(T);
    }

    // tg: code/modules/meteors/meteor_types.dm:418
    public override async DmTask<object> Bump(atom A = null)
    {
        await EX_ACT(A, hitpwr);
        await get_hit();
        return null;
    }
}

// tg: code/modules/meteors/meteor_types.dm:423
public partial class obj_effect_meteor_meaty_xeno : obj_effect_meteor_meaty
{
    // tg: code/modules/meteors/meteor_types.dm:429
    public override void setup_extra_drops()
    {
        Dm.append(ref meteordrop, subtypesof(typeof(obj_item_organ_alien)));
    }

    // tg: code/modules/meteors/meteor_types.dm:432
    public override void ram_turf(turf T = null)
    {
        if (!isspaceturf(T))
            Dm.@new<obj_effect_decal_cleanable_blood_xeno>(T);
    }
}

// tg: code/modules/meteors/meteor_types.dm:437
public partial class obj_effect_meteor_tunguska : obj_effect_meteor
{
    // tg: code/modules/meteors/meteor_types.dm:449
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = await base.Move(newloc, direction, glide_size_override, update_dir);
        if (Dm.truthy(dot))
            Dm.@new<obj_effect_temp_visual_revenant>(get_turf(this));
        return dot;
    }

    // tg: code/modules/meteors/meteor_types.dm:454
    public override async DmTask meteor_effect()
    {
        await base.meteor_effect();
        await explosion(this, devastation_range: 5, heavy_impact_range: 10, light_impact_range: 15, flash_range: 20, adminlog: FALSE);
    }

    // tg: code/modules/meteors/meteor_types.dm:458
    public override async DmTask<object> Bump(atom A = null)
    {
        await base.Bump(A);
        if (prob(20))
            await explosion(this, devastation_range: 2, heavy_impact_range: 4, light_impact_range: 6, flash_range: 8, adminlog: FALSE);
        return null;
    }
}

// tg: code/modules/meteors/meteor_types.dm:463
public partial class obj_effect_meteor_pumpkin : obj_effect_meteor
{
    // tg: code/modules/meteors/meteor_types.dm:474
    [DmParams("mapload", "target")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        meteorsound = SFX_HALLUCINATION_I_M_HERE;
        return dot;
    }
}

/**
 * Component for meteors and meteor imitators that handles punching them, mining them, & examining them
 */
// tg: code/modules/meteors/meteor_deflection.dm:5
public partial class datum_component_meteor_combat : datum_component
{
    // tg: code/modules/meteors/meteor_deflection.dm:13
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        redirection_proc = Dm.arg<datum_callback>(__extra, 0);
        destruction_proc = Dm.arg<datum_callback>(__extra, 1);
        achievement_enabled = FALSE; // the awards are not ported
        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(on_attacked));
        RegisterSignal(parent, COMSIG_ATOM_ATTACK_HAND, nameof(on_punched));
        return null;
    }

    // tg: code/modules/meteors/meteor_deflection.dm:20
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_ATOM_ATTACKBY);
        UnregisterSignal(parent, COMSIG_ATOM_ATTACK_HAND);
    }

    // tg: code/modules/meteors/meteor_deflection.dm:24  (the bat's athletics and the power fist's gas: gaps SL1)
    [SignalHandler]
    public virtual object on_attacked(atom owner = null, obj_item attacking_item = null, mob user = null, DmList modifiers = null)
    {
        if (Dm.eq(attacking_item.tool_behaviour, TOOL_MINING))
        {
            if (destruction_proc != null)
                Waiting.Detach(destruction_proc.Invoke());
            playsound((atom)parent, Dm.resource("sound/effects/pickaxe/picaxe1.ogg"), 50, TRUE);
            qdel(parent);
            return TRUE;
        }

        if (attacking_item is obj_item_melee_baseball_bat)
        {
            if (Dm.lt(user.mind?.get_skill_level(typeof(datum_skill_athletics)), SKILL_LEVEL_EXPERT))
            {
                to_chat(user, span_warning(I18n.T("\\The [] is too heavy for you!", parent)));
                return FALSE;
            }
            playsound((atom)parent, Dm.resource("sound/items/baseballhit.ogg"), 100, TRUE);
            Waiting.NoSleep(redirection_proc.Invoke(user));
            return TRUE;
        }

        if (attacking_item is obj_item_melee_powerfist)
        {
            obj_item_melee_powerfist fist = (obj_item_melee_powerfist)attacking_item;
            if (fist.tank == null)
            {
                to_chat(user, span_warning(I18n.T("\\The [] has no gas tank!", fist)));
                return FALSE;
            }
            datum_gas_mixture gas_used = fist.tank.remove_air(fist.gas_per_fist * 3) as datum_gas_mixture; // 3 is HIGH_PRESSURE setting on powerfist.
            if (gas_used == null || !Dm.truthy(molar_cmp_equals(gas_used.total_moles(), fist.gas_per_fist * 3)))
            {
                to_chat(user, span_warning(I18n.T("\\The [] didn't have enough gas to budge \\the []!", fist, parent)));
                return FALSE;
            }
            playsound((atom)parent, Dm.resource("sound/items/weapons/resonator_blast.ogg"), 50, TRUE);
            Waiting.NoSleep(redirection_proc.Invoke(user));
            return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/meteors/meteor_deflection.dm:58  (gaps SL1: a legendary athlete punches a meteor away)
    [SignalHandler]
    public virtual object on_punched(atom owner = null, mob user = null, DmList modifiers = null)
    {
        if (!isliving(user))
            return FALSE;
        mob_living livinguser = (mob_living)user;

        if (Dm.truthy(livinguser.combat_mode) && Dm.ge(livinguser.mind?.get_skill_level(typeof(datum_skill_athletics)), SKILL_LEVEL_LEGENDARY))
        {
            check_punch_award(livinguser);
            playsound((atom)parent, SFX_PUNCH, 50, TRUE);
            Waiting.NoSleep(redirection_proc.Invoke(livinguser));
            return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/meteors/meteor_deflection.dm:69  (the awards are not ported)
    public virtual void check_punch_award(mob user = null)
    {
    }
}

// tg: code/modules/shuttle/shuttle_events/meteors.dm:1
public partial class datum_shuttle_event_simple_spawner_meteor : datum_shuttle_event_simple_spawner
{
    // tg: code/modules/shuttle/shuttle_events/meteors.dm:4
    public override void post_spawn(atom_movable spawnee = null)
    {
        base.post_spawn(spawnee);
        ADD_TRAIT(spawnee, TRAIT_FREE_HYPERSPACE_MOVEMENT, INNATE_TRAIT);
    }

    // tg: code/modules/shuttle/shuttle_events/meteors.dm:8
    public override void spawn_movable(object spawn_type = null)
    {
        turf spawn_turf = get_spawn_turf();
        //invert the dir cause we shoot in the opposite direction we're flying
        if (typeof(obj_effect_meteor).IsAssignableFrom((Type)spawn_type))
            post_spawn((atom_movable)Dm.@new((Type)spawn_type, spawn_turf, get_edge_target_turf(spawn_turf, angle2dir(Dm.num(dir2angle(port.preferred_direction)) - 180))));
        else //if you want to spawn some random garbage inbetween, go wild
            post_spawn((atom_movable)Dm.@new((Type)spawn_type, get_spawn_turf()));
    }
}

// tg: code/modules/shuttle/shuttle_events/meteors.dm:17
public partial class datum_shuttle_event_simple_spawner_meteor_dust : datum_shuttle_event_simple_spawner_meteor
{
    // tg: code/modules/shuttle/shuttle_events/meteors.dm:29
    public override turf get_spawn_turf() => (turf)(prob(hit_the_shuttle_chance) ? pick(spawning_turfs_hit) : pick(spawning_turfs_miss));
}

// tg: code/modules/meteors/meteor_spawning.dm:34 (a global proc; the meteors' file holds it, the black market launches from it too)
public static partial class GlobalProcs
{
    // tg: code/modules/meteors/meteor_spawning.dm:34
    public static turf spaceDebrisStartLoc(object start_side = null, object Z = null, float distance_from_edge = 0)
    {
        float pad = TRANSITIONEDGE + obj_effect_meteor.MAP_EDGE_PAD;
        float side = Dm.num(start_side), maxx = Dm.world.maxx, maxy = Dm.world.maxy;
        if (side == NORTH)
            return locate(rand(pad + distance_from_edge, maxx - (pad + distance_from_edge)), maxy - pad - distance_from_edge, Dm.num(Z));
        if (side == EAST)
            return locate(maxx - pad - distance_from_edge, rand(pad + distance_from_edge, maxy - (pad + distance_from_edge)), Dm.num(Z));
        if (side == SOUTH)
            return locate(rand(pad + distance_from_edge, maxx - (pad + distance_from_edge)), pad + distance_from_edge, Dm.num(Z));
        return locate(pad + distance_from_edge, rand(pad + distance_from_edge, maxy - (pad + distance_from_edge)), Dm.num(Z));
    }
}

