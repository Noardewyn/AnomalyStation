// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/tesla/energy_ball.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 (D169): the whole file, the tesla engine's energy ball (its movement, zaps, mini-balls and dusting, orbiting with
// datums/components/orbiter.cs) and tesla_zap (the M7 zaps slice: the zap chain of the tesla coil, the powered grille and the
// supermatter's surroundings). The admin logs of the ball's creation and of its dustings are deferred (admin tooling, M10). The var
// block is generated.

/// The Tesla engine
// tg: code/modules/power/tesla/energy_ball.dm:14
public partial class obj_energy_ball : obj
{
    // tg: code/modules/power/tesla/energy_ball.dm:1  (file-local, rules 11.4)
    private const float TESLA_DEFAULT_ENERGY = (695.304f * MEGA * JOULES);
    // tg: code/modules/power/tesla/energy_ball.dm:2
    private const float TESLA_MINI_ENERGY = (347.652f * MEGA * JOULES); // Has a weird scaling thing so this is a lie for now (doesn't generate power anyways).

    // tg: code/modules/power/tesla/energy_ball.dm:41
    [DmParams("mapload", "starting_energy", "is_miniball")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object starting_energy = Dm.arg(__extra, 0) ?? 50;
        object is_miniball = Dm.arg(__extra, 1) ?? FALSE;
        object dot = base.Initialize(mapload, __extra);

        energy = starting_energy;
        miniball = Dm.num(is_miniball);
        START_PROCESSING(SSobj, this);

        if (!Dm.truthy(is_miniball))
        {
            Waiting.NoSleep(set_light(10, 7, "#5e5edd"));

            var spawned_turf = get_turf(this) as turf;
            message_admins(Dm.text("A tesla has been created at [].", ADMIN_VERBOSEJMP(spawned_turf)));
            investigate_log(Dm.text("was created at [].", AREACOORD(spawned_turf)), INVESTIGATE_ENGINE);
        }
        return dot;
    }

    // tg: code/modules/power/tesla/energy_ball.dm:55
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(orbiting) && istype(orbiting.parent, typeof(obj_energy_ball)))
        {
            obj_energy_ball parent_energy_ball = (obj_energy_ball)orbiting.parent;
            Dm.remove(ref parent_energy_ball.orbiting_balls, this);
        }

        QDEL_LIST(orbiting_balls);
        STOP_PROCESSING(SSobj, this);

        return base.Destroy(force);
    }

    // tg: code/modules/power/tesla/energy_ball.dm:65  (set waitfor = FALSE, inherited from /datum/process: the wrapper of
    // waiting-model.md 5.4; move and tesla_zap may sleep; the body never assigns ., so no box)
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null);
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        if (Dm.truthy(orbiting))
            energy = 0; // ensure we dont have miniballs of miniballs
        else
        {
            handle_energy();

            await move(4 + orbiting_balls.len * 1.5f);

            playsound(this.loc, Dm.resource("sound/effects/magic/lightningbolt.ogg"), 100, TRUE, extrarange: 30);

            pixel_x = 0;
            pixel_y = 0;
            shocked_things.Cut(1, shocked_things.len / 1.3f);
            DmList shocking_info = Dm.list();
            await tesla_zap(source: this, zap_range: 3, power: TESLA_DEFAULT_ENERGY, shocked_targets: shocking_info);

            pixel_x = -ICON_SIZE_X;
            pixel_y = -ICON_SIZE_Y;
            foreach (object ball in Dm.each(orbiting_balls))
            {
                float range = rand(1, clamp(orbiting_balls.len, 2, 3));
                DmList temp_shock = Dm.list();
                //We zap off the main ball instead of ourselves to make things looks proper
                await tesla_zap(source: this, zap_range: range, power: TESLA_MINI_ENERGY / 7 * range, shocked_targets: temp_shock);
                Dm.append(ref shocking_info, temp_shock);
            }
            Dm.append(ref shocked_things, shocking_info);
        }
        return null;
    }

    // tg: code/modules/power/tesla/energy_ball.dm:91
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (Dm.truthy(orbiting_balls.len))
            Dm.append(ref dot, I18n.T("There are [] mini-balls orbiting it.", orbiting_balls.len));
        return dot;
    }

    // tg: code/modules/power/tesla/energy_ball.dm:96
    public virtual async DmTask move(object move_amount = null)
    {
        DmList dirs = GLOB.alldirs.Copy();
        if (Dm.truthy(shocked_things.len))
        {
            for (float i = 1, i__end = 30; i <= i__end; i++)
            {
                atom real_thing = pick(shocked_things) as atom;
                Dm.append(ref dirs, get_dir(this, real_thing)); //Carry some momentum yeah? Just a bit tho
            }
        }
        for (float i = 0, i__end = Dm.num(move_amount); i <= i__end; i++)
        {
            float move_dir = Dm.num(pick(dirs)); //ensures teslas don't just sit around
            if (Dm.truthy(target) && prob(10))
                move_dir = get_dir(this, target);
            turf turf_to_move = get_step(this, move_dir);
            if (Dm.truthy(can_move(turf_to_move)))
            {
                await forceMove(turf_to_move);
                await setDir(move_dir);
                foreach (mob_living_carbon mob_to_dust in Dm.each<mob_living_carbon>(loc))
                    await dust_mobs(mob_to_dust);
            }
        }
    }

    // tg: code/modules/power/tesla/energy_ball.dm:113
    public virtual object can_move(turf to_move = null)
    {
        if (to_move == null)
            return FALSE;

        foreach (atom thing in Dm.each_cast<atom>(to_move))
        {
            if (Dm.truthy(Dm.bitand(!Dm.truthy(thing._listen_lookup?[COMSIG_ATOM_SINGULARITY_TRY_MOVE]) ? NONE : thing._SendSignal(COMSIG_ATOM_SINGULARITY_TRY_MOVE, Dm.list(thing)), SINGULARITY_TRY_MOVE_BLOCK)))
                return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/power/tesla/energy_ball.dm:124
    public virtual void handle_energy()
    {
        if (Dm.num(energy) >= energy_to_raise)
        {
            energy_to_lower = energy_to_raise - 20;
            energy_to_raise = energy_to_raise * 1.25f;

            playsound(this.loc, Dm.resource("sound/effects/magic/lightning_chargeup.ogg"), 100, TRUE, extrarange: 30);
            _addtimer(Dm.@new<datum_callback>(this, nameof(new_mini_ball)), 10 * SECONDS, file: "code/modules/power/tesla/energy_ball.dm", line: 130);
        }
        else if (Dm.num(energy) < energy_to_lower && Dm.truthy(orbiting_balls.len))
        {
            energy_to_raise = energy_to_raise / 1.25f;
            energy_to_lower = (energy_to_raise / 1.25f) - 20;

            object Orchiectomy_target = pick(orbiting_balls);
            qdel(Orchiectomy_target);
        }
    }

    // tg: code/modules/power/tesla/energy_ball.dm:138
    public virtual async DmTask new_mini_ball()
    {
        if (!Dm.truthy(loc))
            return;

        obj_energy_ball miniball = Dm.@new<obj_energy_ball>(
            loc,
            /* starting_energy = */ 0,
            /* is_miniball = */ TRUE
        );

        miniball.transform = Dm.mul(miniball.transform, pick(0.3f, 0.4f, 0.5f, 0.6f, 0.7f));
        // var/list/icon_dimensions = get_icon_dimensions(icon): the .dmi's width and height (IconSizes, __HELPERS/icons.cs)
        (int Width, int Height) icon_dimensions = IconSizes.Of(icon);

        float orbitsize = (icon_dimensions.Width + icon_dimensions.Height) * Dm.num(pick(0.4f, 0.5f, 0.6f, 0.7f, 0.8f));
        orbitsize -= (orbitsize / ICON_SIZE_ALL) * (ICON_SIZE_ALL * 0.25f);
        await miniball.orbit(this, orbitsize, Dm.num(pick(FALSE, TRUE)), rand(10, 25), Dm.num(pick(3, 4, 5, 6, 36)));
    }

    // tg: code/modules/power/tesla/energy_ball.dm:155
    public override async DmTask<object> Bump(atom bumped_atom = null)
    {
        await dust_mobs(bumped_atom);
        return null;
    }

    // tg: code/modules/power/tesla/energy_ball.dm:158
    public override object Bumped(atom_movable bumped_atom = null)
    {
        Waiting.NoSleep(dust_mobs(bumped_atom));
        return null;
    }

    // tg: code/modules/power/tesla/energy_ball.dm:161  (the /atom root, _onclick/telekinesis.dm, is not translated: the slot's first
    // declaration on this branch of the tree, as the supermatter crystal's, supermatter_hit_procs.cs)
    public override async DmTask<object> attack_tk(mob user = null)
    {
        if (!iscarbon(user))
            return null;
        mob_living_carbon jedi = (mob_living_carbon)user;
        to_chat(jedi, span_userdanger(I18n.T("That was a shockingly dumb idea.")));
        obj_item_organ_brain rip_u = Dm.locate<obj_item_organ_brain>(jedi.organs);
        jedi.ghostize(TRUE); // tg passes jedi itself as can_reenter_corpse, a true value
        if (Dm.truthy(rip_u))
            qdel(rip_u);
        jedi.investigate_log(Dm.text("had [] brain dusted by touching [] with telekinesis.", jedi.p_their(), this), INVESTIGATE_DEATHS);
        await jedi.death();
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/power/tesla/energy_ball.dm:174
    public override async DmTask<object> orbit(atom A = null, float? radius = null, float? clockwise = null, float? rotation_speed = null, float? rotation_segments = null, float? pre_rotation = null, params object[] __extra)
    {
        obj_energy_ball target = A as obj_energy_ball;
        object dot = null;
        if (target != null)
            Dm.append(ref target.orbiting_balls, this);
        dot = await base.orbit(A, radius, clockwise, rotation_speed, rotation_segments, pre_rotation, __extra);
        return dot;
    }

    // tg: code/modules/power/tesla/energy_ball.dm:179
    public override async DmTask stop_orbit(datum_component_orbiter orbits = null, float? refreshing = null)
    {
        refreshing ??= FALSE;
        if (Dm.truthy(refreshing))
        {
            await base.stop_orbit(orbits, refreshing);
            return;
        }
        if (Dm.truthy(orbiting) && istype(orbiting.parent, typeof(obj_energy_ball)))
        {
            obj_energy_ball orbitingball = (obj_energy_ball)orbiting.parent;
            Dm.remove(ref orbitingball.orbiting_balls, this);
        }
        await base.stop_orbit(orbits, refreshing);
        if (!QDELETED(this))
            qdel(this);
    }

    // tg: code/modules/power/tesla/energy_ball.dm:190
    public virtual async DmTask dust_mobs(atom A = null)
    {
        if (isliving(A))
        {
            mob_living living = (mob_living)A;
            if (Dm.truthy(living.incorporeal_move) || HAS_TRAIT(living, TRAIT_GODMODE))
                return;
        }
        if (!iscarbon(A))
            return;
        // DEVIATION(DV83): an inert carbon (D112: Meta's monkeys until C5, no DNA, organs or limbs) is not dusted
        if (InertSeam.IsInert(A))
            return;
        foreach (obj_machinery_power_energy_accumulator_grounding_rod GR in Dm.each<obj_machinery_power_energy_accumulator_grounding_rod>(orange(this, 2)))
        {
            if (Dm.truthy(GR.anchored))
                return;
        }
        mob_living_carbon C = (mob_living_carbon)A;
        C.investigate_log("has been dusted by an energy ball.", INVESTIGATE_DEATHS);
        await C.dust();
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/power/tesla/energy_ball.dm:4-11  the target priorities of tesla_zap (file-local, rules 11.4)
    // tg: code/modules/power/tesla/energy_ball.dm:11
    private const int STRUCTURE = (1);
    // tg: code/modules/power/tesla/energy_ball.dm:10
    private const int BLOB = (STRUCTURE + 1);
    // tg: code/modules/power/tesla/energy_ball.dm:9
    private const int MACHINERY = (BLOB + 1);
    // tg: code/modules/power/tesla/energy_ball.dm:8
    private const int LIVING = (MACHINERY + 1);
    // tg: code/modules/power/tesla/energy_ball.dm:7
    private const int RIDE = (LIVING + 1);
    // tg: code/modules/power/tesla/energy_ball.dm:6
    private const int ROD = (RIDE + 1);
    // tg: code/modules/power/tesla/energy_ball.dm:5
    private const int COIL = (ROD + 1);
    // tg: code/modules/power/tesla/energy_ball.dm:4
    private const int BIKE = (COIL + 1);

    // tg: code/modules/power/tesla/energy_ball.dm:217  var/static/list/things_to_shock (rules 10.6: a TgState field, below)
    private static ref DmList tesla_zap__things_to_shock => ref TgState.Current.GlobalProcs__tesla_zap__things_to_shock;

    // tg: code/modules/power/tesla/energy_ball.dm:204
    public static async DmTask<object> tesla_zap(atom source = null, float? zap_range = null, object power = null, float? cutoff = null, float? zap_flags = null, DmList shocked_targets = null)
    {
        zap_range ??= 3;
        cutoff ??= 4e5f;
        zap_flags ??= ZAP_DEFAULT_FLAGS;
        shocked_targets ??= Dm.list();
        object dot = null;
        if (QDELETED(source))
            return dot;
        if (!Dm.truthy(Dm.bitand(zap_flags.Value, ZAP_ALLOW_DUPLICATES)))
            LAZYSET(ref shocked_targets, source, TRUE); //I don't want no null refs in my list yeah?
        dot = source.dir;
        if (Dm.lt(power, cutoff.Value))
            return dot;

        /*
        THIS IS SO FUCKING UGLY AND I HATE IT, but I can't make it nice without making it slower, check*N rather then n. So we're stuck with it.
        */
        atom closest_atom = null;
        int closest_type = 0;
        // (a list, no datum: made on the first call, rules 10.6's ??= form; it moves no world id)
        tesla_zap__things_to_shock ??= zebra_typecacheof(Dm.list(
            // Things that we want to shock.
            Dm.kv(typeof(obj_machinery), TRUE),
            Dm.kv(typeof(mob_living), TRUE),
            Dm.kv(typeof(obj_structure), TRUE),
            Dm.kv(typeof(obj_vehicle_ridden), TRUE),

            // Things that we don't want to shock.
            Dm.kv(typeof(obj_machinery_atmospherics), FALSE),
            Dm.kv(typeof(obj_machinery_portable_atmospherics), FALSE),
            Dm.kv(typeof(obj_machinery_power_emitter), FALSE),
            Dm.kv(typeof(obj_machinery_field_generator), FALSE),
            Dm.kv(typeof(obj_machinery_field_containment), FALSE),
            Dm.kv(typeof(obj_machinery_camera), FALSE),
            Dm.kv(typeof(obj_machinery_gateway), FALSE),
            Dm.kv(typeof(mob_living_simple_animal), FALSE),
            Dm.kv(typeof(obj_structure_disposalpipe), FALSE),
            Dm.kv(typeof(obj_structure_disposaloutlet), FALSE),
            Dm.kv(typeof(obj_machinery_disposal_delivery_chute), FALSE),
            Dm.kv(typeof(obj_structure_sign), FALSE),
            Dm.kv(typeof(obj_structure_lattice), FALSE),
            Dm.kv(typeof(obj_structure_grille), FALSE),
            Dm.kv(typeof(obj_structure_frame_machine), FALSE)
        )) as DmList;

        //Ok so we are making an assumption here. We assume that view() still calculates from the center out.
        //This means that if we find an object we can assume it is the closest one of its type. This is somewhat of a speed increase.
        //This also means we have no need to track distance, as the doview() proc does it all for us.

        //Darkness fucks oview up hard. I've tried dview() but it doesn't seem to work
        //I hate existence
        foreach (atom A in Dm.each_cast<atom>(typecache_filter_list(oview(zap_range.Value + 2, source), tesla_zap__things_to_shock)))
        {
            if (!Dm.truthy(Dm.bitand(zap_flags.Value, ZAP_ALLOW_DUPLICATES)) && Dm.truthy(LAZYACCESS(shocked_targets, A)))
                continue;
            // NOTE: these type checks are safe because CURRENTLY the range family of procs returns turfs in least to greatest distance order
            // This is unspecified behavior tho, so if it ever starts acting up just remove these optimizations and include a distance check
            if (closest_type >= BIKE)
                break;

            else if (istype(A, typeof(obj_vehicle_ridden_bicycle)))//God's not on our side cause he hates idiots.
            {
                obj_vehicle_ridden_bicycle B = (obj_vehicle_ridden_bicycle)A;
                if (!HAS_TRAIT(B, TRAIT_BEING_SHOCKED) && Dm.truthy(B.can_buckle))//Gee goof thanks for the boolean
                {
                    //we use both of these to save on istype and typecasting overhead later on
                    //while still allowing common code to run before hand
                    closest_type = BIKE;
                    closest_atom = B;
                }
            }

            else if (closest_type >= COIL)
                continue; //no need checking these other things

            else if (istype(A, typeof(obj_machinery_power_energy_accumulator_tesla_coil)))
            {
                if (!HAS_TRAIT(A, TRAIT_BEING_SHOCKED))
                {
                    closest_type = COIL;
                    closest_atom = A;
                }
            }

            else if (closest_type >= ROD)
                continue;

            else if (istype(A, typeof(obj_machinery_power_energy_accumulator_grounding_rod)))
            {
                closest_type = ROD;
                closest_atom = A;
            }

            else if (closest_type >= RIDE)
                continue;

            else if (istype(A, typeof(obj_vehicle_ridden)))
            {
                obj_vehicle_ridden R = (obj_vehicle_ridden)A;
                if (Dm.truthy(R.can_buckle) && !HAS_TRAIT(R, TRAIT_BEING_SHOCKED))
                {
                    closest_type = RIDE;
                    closest_atom = A;
                }
            }

            else if (closest_type >= LIVING)
                continue;

            else if (isliving(A))
            {
                mob_living L = (mob_living)A;
                // DEVIATION(DV83): an inert living mob (D112: no DNA, organs or limbs, its Initialize not translated) is no target
                if (InertSeam.IsInert(L))
                    continue;
                if (L.stat != DEAD && !HAS_TRAIT(L, TRAIT_TESLA_SHOCKIMMUNE) && !HAS_TRAIT(L, TRAIT_BEING_SHOCKED))
                {
                    closest_type = LIVING;
                    closest_atom = A;
                }
            }

            else if (closest_type >= MACHINERY)
                continue;

            else if (ismachinery(A))
            {
                if (!HAS_TRAIT(A, TRAIT_BEING_SHOCKED))
                {
                    closest_type = MACHINERY;
                    closest_atom = A;
                }
            }

            else if (closest_type >= BLOB)
                continue;

            else if (istype(A, typeof(obj_structure_blob)))
            {
                if (!HAS_TRAIT(A, TRAIT_BEING_SHOCKED))
                {
                    closest_type = BLOB;
                    closest_atom = A;
                }
            }

            else if (closest_type >= STRUCTURE)
                continue;

            else if (isstructure(A))
            {
                if (!HAS_TRAIT(A, TRAIT_BEING_SHOCKED))
                {
                    closest_type = STRUCTURE;
                    closest_atom = A;
                }
            }
        }

        //Alright, we've done our loop, now lets see if was anything interesting in range
        if (!Dm.truthy(closest_atom))
            return dot;
        //common stuff
        source.Beam(closest_atom, icon_state: Dm.text("lightning[]", rand(1, 12)), time: 5);
        float zapdir = get_dir(source, closest_atom);
        if (Dm.truthy(zapdir))
            dot = zapdir;

        float next_range = 2;
        if (closest_type == COIL)
            next_range = 5;

        if (closest_type == LIVING)
        {
            mob_living closest_mob = (mob_living)closest_atom;
            ADD_TRAIT(closest_mob, TRAIT_BEING_SHOCKED, WAS_SHOCKED);
            _addtimer(TRAIT_CALLBACK_REMOVE(closest_mob, TRAIT_BEING_SHOCKED, WAS_SHOCKED), 1 * SECONDS, file: "code/modules/power/tesla/energy_ball.dm", line: 332);
            float shock_damage = Dm.truthy(Dm.bitand(zap_flags.Value, ZAP_MOB_DAMAGE)) ? (min(round(Dm.num(power) / 600), 90) + rand(-5, 5)) : 0;
            await closest_mob.electrocute_act(shock_damage, source, 1, Dm.bitor(SHOCK_TESLA, (Dm.truthy(Dm.bitand(zap_flags.Value, ZAP_MOB_STUN)) ? NONE : SHOCK_NOSTUN)));
            if (issilicon(closest_mob))
            {
                mob_living_silicon S = (mob_living_silicon)closest_mob;
                if (Dm.truthy(Dm.bitand(zap_flags.Value, ZAP_MOB_STUN)) && Dm.truthy(Dm.bitand(zap_flags.Value, ZAP_MOB_DAMAGE)))
                    await S.emp_act(EMP_LIGHT);
                next_range = 7; // metallic folks bounce it further
            }
            else
                next_range = 5;
            power = Dm.div(power, 1.5f);
        }

        else
            power = await closest_atom.zap_act(power, zap_flags);

        // Electrolysis.
        turf target_turf = get_turf(closest_atom);
        if (Dm.truthy(target_turf == null ? null : await target_turf.return_air()))
        {
            datum_gas_mixture air_mixture = await target_turf.return_air() as datum_gas_mixture;
            air_mixture.electrolyze(working_power: Dm.num(power) / 200);
            target_turf.air_update_turf();
        }

        if (prob(20))//I know I know
        {
            DmList shocked_copy = shocked_targets.Copy();
            await tesla_zap(source: closest_atom, zap_range: next_range, power: Dm.mul(power, 0.5f), cutoff: cutoff, zap_flags: zap_flags, shocked_targets: shocked_copy);
            await tesla_zap(source: closest_atom, zap_range: next_range, power: Dm.mul(power, 0.5f), cutoff: cutoff, zap_flags: zap_flags, shocked_targets: shocked_targets);
            Dm.append(ref shocked_targets, shocked_copy);
        }
        else
            await tesla_zap(source: closest_atom, zap_range: next_range, power: power, cutoff: cutoff, zap_flags: zap_flags, shocked_targets: shocked_targets);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/power/tesla/energy_ball.dm:217  /proc/tesla_zap var/static/list/things_to_shock
    internal DmList GlobalProcs__tesla_zap__things_to_shock;
}
