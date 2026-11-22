// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/singularity/singularity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5x slice 5 (D149, D150: a dev-spawned singularity eating the station, no containment): the gravitational singularity. Left in
// A stage six's glare and fire (vision_hurting, combust_mobs) came with the supermatter's delamination (M7 G6). The shuttle_event
// subtype stays inert.
// The var block is generated.

/// The gravitational singularity
// tg: code/modules/power/singularity/singularity.dm:2
[DmPath("/obj/singularity")]
public partial class obj_singularity : obj
{
    // tg: code/modules/power/singularity/singularity.dm:58
    [DmParams("mapload", "starting_energy")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object starting_energy = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);

        energy = Dm.truthy(starting_energy) ? Dm.num(starting_energy) : energy;

        START_PROCESSING(SSsinguloprocess, this);
        Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this)); // tg: :64 (M10x slice C)

        datum_component_singularity new_component = Waiting.NoSleep(AddComponent(
            singularity_component_type,
            Dm.named("consume_callback", Dm.@new<datum_callback>(this, nameof(consume))),
            Dm.named("roaming", (Dm.truthy(move_self) && current_size >= STAGE_TWO) ? TRUE : FALSE)
        )) as datum_component_singularity;

        singularity_component = WEAKREF(new_component) as datum_weakref;

        Waiting.NoSleep(check_energy());

        // (SSmachines?.: the port's rooms without the machines subsystem, as _machinery.cs:64 registers)
        foreach (obj_machinery_power_singularity_beacon singu_beacon in Dm.each_cast<obj_machinery_power_singularity_beacon>(SSmachines?.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_singularity_beacon))))
        {
            if (Dm.truthy(singu_beacon.active))
            {
                new_component.target = singu_beacon;
                break;
            }
        }

        if (!Dm.truthy(mapload))
        {
            notify_ghosts(
                ghost_notification_message,
                source: this,
                header: Dm.str(ghost_notification_message),
                ghost_sound: "sound/machines/warning-buzzer.ogg",
                notify_volume: 75);
        }
        return dot;
    }

    // tg: code/modules/power/singularity/singularity.dm:89
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSsinguloprocess, this);
        return base.Destroy(force);
    }

    // tg: code/modules/power/singularity/singularity.dm:146  (set waitfor = FALSE, inherited from /atom/ex_act)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                if (current_size <= STAGE_TWO)
                {
                    investigate_log("has been destroyed by a heavy explosion.", INVESTIGATE_ENGINE);
                    qdel(this);
                    return TRUE;
                }

                energy -= round(((energy + 1) / 2), 1);
                break;
            case EXPLODE_HEAVY:
                energy -= round(((energy + 1) / 3), 1);
                break;
            case EXPLODE_LIGHT:
                energy -= round(((energy + 1) / 4), 1);
                break;
        }

        return TRUE;
    }

    // tg: code/modules/power/singularity/singularity.dm:162  (set waitfor = FALSE, inherited from /datum/process: the wrapper of
    // waiting-model.md 5.4; event and check_energy may sleep)
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process__body(seconds_per_tick, dot), dot);
    }

    private async DmTask<object> process__body(object seconds_per_tick, DotBox<object> dot)
    {
        time_since_act += Dm.num(seconds_per_tick);
        if (time_since_act < 2)
            return null;
        time_since_act = 0;
        if (current_size >= STAGE_TWO)
        {
            if (prob(event_chance))
                await @event();
        }
        dissipate__proc(seconds_per_tick);
        await check_energy();
        return null;
    }

    // tg: code/modules/power/singularity/singularity.dm:173
    public virtual void dissipate__proc(object seconds_per_tick = null)
    {
        if (!Dm.truthy(dissipate))
            return;

        time_since_last_dissipiation += Dm.num(seconds_per_tick);

        // Uses a while in case of especially long delta times
        while (time_since_last_dissipiation >= dissipate_delay)
        {
            energy -= dissipate_strength;
            time_since_last_dissipiation -= dissipate_delay;
        }
    }

    // tg: code/modules/power/singularity/singularity.dm:184
    public virtual async DmTask<object> expand(object force_size = null)
    {
        float temp_allowed_size = allowed_size;

        if (Dm.truthy(force_size))
            temp_allowed_size = Dm.num(force_size);

        if (temp_allowed_size >= STAGE_SIX && !Dm.truthy(consumed_supermatter))
            temp_allowed_size = STAGE_FIVE;

        //cap it off if the singuloth has a maximum stage
        temp_allowed_size = min(temp_allowed_size, maximum_stage);

        if (temp_allowed_size == maximum_stage)
        {
            //It cant go smaller due to e loss
            dissipate = FALSE;
        }

        object new_grav_pull = null;
        object new_consume_range = null;

        switch (temp_allowed_size)
        {
            case STAGE_ONE:
                current_size = STAGE_ONE;
                icon = Dm.resource("icons/obj/machines/engine/singularity.dmi");
                icon_state = Dm.text("[]_s1", singularity_icon_variant);
                pixel_x = 0;
                pixel_y = 0;
                new_grav_pull = 4;
                new_consume_range = 0;
                dissipate_delay = 10;
                time_since_last_dissipiation = 0;
                dissipate_strength = 1;
                break;
            case STAGE_TWO:
                if (Dm.truthy(await check_cardinals_range(1, TRUE)))
                {
                    current_size = STAGE_TWO;
                    icon = Dm.resource("icons/effects/96x96.dmi");
                    icon_state = Dm.text("[]_s3", singularity_icon_variant);
                    pixel_x = -32;
                    pixel_y = -32;
                    new_grav_pull = 6;
                    new_consume_range = 1;
                    dissipate_delay = 5;
                    time_since_last_dissipiation = 0;
                    dissipate_strength = 5;
                }
                break;
            case STAGE_THREE:
                if (Dm.truthy(await check_cardinals_range(2, TRUE)))
                {
                    current_size = STAGE_THREE;
                    icon = Dm.resource("icons/effects/160x160.dmi");
                    icon_state = Dm.text("[]_s5", singularity_icon_variant);
                    pixel_x = -64;
                    pixel_y = -64;
                    new_grav_pull = 8;
                    new_consume_range = 2;
                    dissipate_delay = 4;
                    time_since_last_dissipiation = 0;
                    dissipate_strength = 20;
                }
                break;
            case STAGE_FOUR:
                if (Dm.truthy(await check_cardinals_range(3, TRUE)))
                {
                    current_size = STAGE_FOUR;
                    icon = Dm.resource("icons/effects/224x224.dmi");
                    icon_state = Dm.text("[]_s7", singularity_icon_variant);
                    pixel_x = -96;
                    pixel_y = -96;
                    new_grav_pull = 10;
                    new_consume_range = 3;
                    dissipate_delay = 10;
                    time_since_last_dissipiation = 0;
                    dissipate_strength = 10;
                }
                break;
            case STAGE_FIVE: //this one also lacks a check for gens because it eats everything
                current_size = STAGE_FIVE;
                icon = Dm.resource("icons/effects/288x288.dmi");
                icon_state = Dm.text("[]_s9", singularity_icon_variant);
                pixel_x = -128;
                pixel_y = -128;
                new_grav_pull = 10;
                new_consume_range = 4;
                dissipate = FALSE; //It cant go smaller due to e loss
                break;
            case STAGE_SIX: //This only happens if a stage 5 singulo consumes a supermatter shard.
                current_size = STAGE_SIX;
                icon = Dm.resource("icons/effects/352x352.dmi");
                icon_state = Dm.text("[]_s11", singularity_icon_variant);
                pixel_x = -160;
                pixel_y = -160;
                new_grav_pull = 15;
                new_consume_range = 5;
                dissipate = FALSE;
                break;
        }

        if (temp_allowed_size == STAGE_SIX)
        {
            Waiting.NoSleep(AddComponent(typeof(datum_component_vision_hurting)));
        }
        else
            qdel(GetComponent(typeof(datum_component_vision_hurting)));

        datum_component_singularity resolved_singularity = singularity_component?.resolve() as datum_component_singularity;
        if (resolved_singularity != null)
        {
            resolved_singularity.consume_range = new_consume_range;
            resolved_singularity.grav_pull = new_grav_pull;
            resolved_singularity.disregard_failed_movements = current_size >= STAGE_FIVE ? TRUE : FALSE;
            resolved_singularity.roaming = Dm.truthy(move_self) && current_size >= STAGE_TWO ? TRUE : FALSE;
            resolved_singularity.singularity_size = current_size;
        }

        if (current_size == allowed_size)
        {
            investigate_log(Dm.text("grew to size [].", current_size), INVESTIGATE_ENGINE);
            return TRUE;
        }
        else if (current_size < (--temp_allowed_size))
            return await expand(temp_allowed_size);
        else
            return FALSE;
    }

    // tg: code/modules/power/singularity/singularity.dm:291
    public virtual async DmTask<object> check_energy()
    {
        if (energy <= 0)
        {
            investigate_log("collapsed.", INVESTIGATE_ENGINE);
            qdel(this);
            return FALSE;
        }
        //Some of these numbers might need to be changed up later -Mport
        // switch(energy): DM's "if(a to b)" ranges are inclusive and tested in order
        if (energy >= STAGE_ONE_ENERGY_REQUIREMENT && energy <= STAGE_TWO_ENERGY_REQUIREMENT)
            allowed_size = STAGE_ONE;
        else if (energy >= STAGE_TWO_ENERGY_REQUIREMENT && energy <= STAGE_THREE_ENERGY_REQUIREMENT)
            allowed_size = STAGE_TWO;
        else if (energy >= STAGE_THREE_ENERGY_REQUIREMENT && energy <= STAGE_FOUR_ENERGY_REQUIREMENT)
            allowed_size = STAGE_THREE;
        else if (energy >= STAGE_FOUR_ENERGY_REQUIREMENT && energy <= STAGE_FIVE_ENERGY_REQUIREMENT)
            allowed_size = STAGE_FOUR;
        else if (energy >= STAGE_FIVE_ENERGY_REQUIREMENT && energy <= STAGE_SIX_ENERGY_REQUIREMENT)
            allowed_size = STAGE_FIVE;
        else if (energy >= STAGE_SIX_ENERGY_REQUIREMENT)
            allowed_size = Dm.truthy(consumed_supermatter) ? STAGE_SIX : STAGE_FIVE;

        if (current_size != allowed_size)
            await expand();
        return TRUE;
    }

    // tg: code/modules/power/singularity/singularity.dm:94
    public override async DmTask<object> attack_tk(mob user = null)
    {
        if (!iscarbon(user))
            return null;
        object dot = COMPONENT_CANCEL_ATTACK_CHAIN;
        mob_living_carbon jedi = user as mob_living_carbon;
        jedi.visible_message(
            span_danger(I18n.T("[]'s head begins to collapse in on itself!", jedi)),
            span_userdanger(I18n.T("Your head feels like it's collapsing in on itself! This was really not a good idea!")),
            span_hear(I18n.T("You hear something crack and explode in gore.")));
        await jedi.Stun(3 * SECONDS);
        Dm.@new<obj_effect_gibspawner_generic>(get_turf(jedi), jedi);
        await jedi.apply_damage(30, BRUTE, BODY_ZONE_HEAD);
        if (QDELETED(jedi))
            return dot; // damage was too much
        if (Dm.eq(jedi.stat, DEAD))
        {
            jedi.ghostize();
            obj_item_bodypart rip_u = jedi.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
            await rip_u.dismember(BURN); //nice try jedi
            qdel(rip_u);
            return dot;
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(carbon_tk_part_two), jedi), 0.1f * SECONDS, file: "code/modules/power/singularity/singularity.dm", line: 115);
        return dot;
    }

    // tg: code/modules/power/singularity/singularity.dm:117
    public virtual async DmTask carbon_tk_part_two(mob_living_carbon jedi = null)
    {
        if (QDELETED(jedi))
            return;
        Dm.@new<obj_effect_gibspawner_generic>(get_turf(jedi), jedi);
        await jedi.apply_damage(30, BRUTE, BODY_ZONE_HEAD);
        if (QDELETED(jedi))
            return; // damage was too much
        if (Dm.eq(jedi.stat, DEAD))
        {
            jedi.ghostize();
            obj_item_bodypart rip_u = jedi.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
            if (rip_u != null)
            {
                await rip_u.dismember(BURN);
                qdel(rip_u);
            }
            return;
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(carbon_tk_part_three), jedi), 0.1f * SECONDS, file: "code/modules/power/singularity/singularity.dm", line: 131);
    }

    // tg: code/modules/power/singularity/singularity.dm:133
    public virtual async DmTask carbon_tk_part_three(mob_living_carbon jedi = null)
    {
        if (QDELETED(jedi))
            return;
        Dm.@new<obj_effect_gibspawner_generic>(get_turf(jedi), jedi);
        await jedi.apply_damage(30, BRUTE, BODY_ZONE_HEAD);
        if (QDELETED(jedi))
            return; // damage was too much
        jedi.ghostize();
        obj_item_bodypart rip_u = jedi.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        if (rip_u != null)
        {
            await rip_u.dismember(BURN);
            qdel(rip_u);
        }
    }

    // tg: code/modules/power/singularity/singularity.dm:314
    public virtual async DmTask consume(atom thing = null)
    {
        if (istype(thing, typeof(obj_item_storage_backpack_holding)) && !Dm.truthy(consumed_supermatter) && !Dm.truthy(collapsing))
        {
            consume_boh(thing as obj);
            return;
        }

        // DEVIATION(DV27): the interaction guard (D132) on the type's own singularity_act: a type whose tg singularity_act is not
        // translated is left uneaten (and gives nothing), where the nearest translated ancestor's would eat it as another type.
        if (InertSeam.MissingOverride(thing, "singularity_act"))
            return;
        // The owner (2026-10-16): the dev godmode (TRAIT_GODMODE from the admin source, docs/ai/specs/apps.md 1.6a) keeps a mob from being
        // eaten, where tg's singularity_act gibs it whatever its godmode (living_defense.dm:652); the mob is still pulled.
        if (isliving(thing) && HAS_TRAIT(thing, TRAIT_GODMODE))
            return;
        // DEVIATION(DV83): an inert living mob (D112: Meta's monkeys until C5, no DNA, organs or limbs) is left uneaten; its gib reads them
        if (isliving(thing) && InertSeam.IsInert(thing))
            return;
        object gain = await thing.singularity_act();
        energy += Dm.num(gain);
        if (istype(thing, typeof(obj_machinery_power_supermatter_crystal)) && !Dm.truthy(consumed_supermatter))
            await supermatter_upgrade();
    }

    // tg: code/modules/power/singularity/singularity.dm:330
    public virtual void consume_boh(obj boh = null)
    {
        collapsing = TRUE;
        name = Dm.text("unstable []", Dm.initial<obj_singularity, string>(this, s => s.name));
        desc = Dm.text("[] It seems to be collapsing in on itself.", Dm.initial<obj_singularity, string>(this, s => s.desc));
        visible_message(
            message: span_danger(I18n.T("As [] consumes [], it begins to collapse in on itself!", this, boh)),
            blind_message: span_hear(I18n.T("You hear aggressive crackling!")),
            vision_distance: 15);
        playsound(loc as atom, "sound/effects/clockcult_gateway_disrupted.ogg", 200, vary: TRUE, extrarange: 3, falloff_exponent: 1, frequency: -1, pressure_affected: FALSE, ignore_walls: TRUE, falloff_distance: 7);
        _addtimer(Dm.@new<datum_callback>(this, nameof(consume_boh_sfx)), 4 * SECONDS, file: "code/modules/power/singularity/singularity.dm", line: 340);
        Dm.animate(this, Dm.named("time", 4 * SECONDS), Dm.named("transform", ((matrix)transform).Scale(0.25f)), Dm.named("flags", ANIMATION_PARALLEL), Dm.named("easing", ELASTIC_EASING));
        Dm.animate(Dm.named("time", 0.5f * SECONDS), Dm.named("alpha", 0));
        // QDEL_IN(src, 4.1 SECONDS) (__DEFINES/qdel.dm:60), expanded inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 4.1f * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), 4.1f * SECONDS, file: "code/modules/power/singularity/singularity.dm", line: 342);
        qdel(boh);
    }

    // tg: code/modules/power/singularity/singularity.dm:346
    public virtual void consume_boh_sfx()
    {
        playsound(loc as atom, "sound/effects/supermatter.ogg", 200, vary: TRUE, extrarange: 3, falloff_exponent: 1, frequency: 0.5f, pressure_affected: FALSE, ignore_walls: TRUE, falloff_distance: 7);
    }

    // tg: code/modules/power/singularity/singularity.dm:324
    public virtual async DmTask supermatter_upgrade()
    {
        name = Dm.text("supermatter-charged []", Dm.initial<obj_singularity, string>(this, s => s.name));
        desc = Dm.text("[] It glows fiercely with inner fire.", Dm.initial<obj_singularity, string>(this, s => s.desc));
        consumed_supermatter = TRUE;
        await set_light(10);
    }

    // tg: code/modules/power/singularity/singularity.dm:349
    public virtual async DmTask<object> check_cardinals_range(object steps = null, float? retry_with_move = null)
    {
        retry_with_move ??= FALSE;
        float dot = length(GLOB.cardinals); //Should be 4.
        foreach (object i in Dm.each(GLOB.cardinals))
            dot -= Dm.num(check_turfs_in(Dm.num(i), Dm.num(steps))); //-1 for each working direction
        if (Dm.truthy(dot) && Dm.truthy(retry_with_move)) //If there's still a positive value it means it didn't pass. Retry with move if applicable
        {
            foreach (object i in Dm.each(GLOB.cardinals))
            {
                if (Dm.truthy(await step(this, Dm.num(i)))) //Move in each direction.
                {
                    if (Dm.truthy(await check_cardinals_range(steps, FALSE))) //New location passes, return true.
                        return TRUE;
                }
            }
        }
        return !Dm.truthy(dot) ? TRUE : FALSE;
    }

    // tg: code/modules/power/singularity/singularity.dm:360
    public virtual object check_turfs_in(float? direction = null, float? step = null)
    {
        direction ??= 0;
        step ??= 0;
        if (!Dm.truthy(direction))
            return FALSE;
        float steps = 0;
        if (!Dm.truthy(step))
        {
            switch (current_size)
            {
                case STAGE_ONE: steps = 1; break;
                case STAGE_TWO: steps = 2; break;
                case STAGE_THREE: steps = 3; break;
                case STAGE_FOUR: steps = 4; break;
                case STAGE_FIVE: steps = 5; break;
            }
        }
        else
            steps = step.Value;
        DmList turfs = Dm.list();
        turf considered_turf = loc as turf;
        for (int i = 1; i <= steps; i++)
            considered_turf = get_step(considered_turf, direction.Value);
        if (!isturf(considered_turf))
            return FALSE;
        turfs.Add(considered_turf);
        float dir2 = 0;
        float dir3 = 0;
        switch (direction.Value)
        {
            case NORTH:
            case SOUTH:
                dir2 = 4;
                dir3 = 8;
                break;
            case EAST:
            case WEST:
                dir2 = 1;
                dir3 = 2;
                break;
        }
        turf other_turf = considered_turf;
        for (int j = 1; j <= steps - 1; j++)
        {
            other_turf = get_step(other_turf, dir2);
            if (!isturf(other_turf))
                return FALSE;
            turfs.Add(other_turf);
        }
        for (int k = 1; k <= steps - 1; k++)
        {
            considered_turf = get_step(considered_turf, dir3);
            if (!isturf(considered_turf))
                return FALSE;
            turfs.Add(considered_turf);
        }
        foreach (turf check_turf in Dm.each<turf>(turfs))
        {
            if (check_turf == null)
                continue;
            if (!Dm.truthy(can_move(check_turf)))
                return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/power/singularity/singularity.dm:411
    public virtual object can_move(turf considered_turf = null)
    {
        if (considered_turf == null)
            return FALSE;
        if (HAS_TRAIT(considered_turf, TRAIT_CONTAINMENT_FIELD))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/power/singularity/singularity.dm:418
    public virtual async DmTask<object> @event()
    {
        float numb = rand(1, 4);
        switch (numb)
        {
            case 1: //EMP
                await emp_area();
                break;
            case 2: //Stun mobs who lack optic scanners
                await mezzer();
                break;
            case 3:
            case 4: //Sets all nearby mobs on fire
                if (current_size < STAGE_SIX)
                    return FALSE;
                await combust_mobs();
                break;
            default:
                return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/power/singularity/singularity.dm:434
    public virtual async DmTask combust_mobs()
    {
        foreach (mob_living_carbon burned_mob in Dm.each<mob_living_carbon>(urange(20, this, 1)))
        {
            burned_mob.visible_message(
                span_warning(I18n.T("[]'s skin bursts into flame!", burned_mob)),
                span_userdanger(I18n.T("You feel an inner fire as your skin bursts into flames!"))
            );
            await burned_mob.adjust_fire_stacks(5);
            burned_mob.ignite_mob();
        }
        return;
    }

    // tg: code/modules/power/singularity/singularity.dm:442
    public virtual async DmTask mezzer()
    {
        foreach (mob_living_carbon stunned_mob in Dm.each<mob_living_carbon>(oviewers(8, this)))
        {
            if (stunned_mob.stat == DEAD || Dm.truthy(stunned_mob.is_blind()))
                continue;
            if (InertSeam.IsInert(stunned_mob)) // DEVIATION(DV83): an inert carbon's Stun reads the species it never got
                continue;

            if (!HAS_TRAIT(stunned_mob, TRAIT_MESON_VISION))
                await apply_stun(stunned_mob);
            else
                to_chat(stunned_mob, span_notice(I18n.T("You look directly into \\the [], good thing your vision is protected!", this)));
        }
    }

    // tg: code/modules/power/singularity/singularity.dm:452
    public virtual async DmTask apply_stun(mob_living_carbon stunned_mob = null)
    {
        await stunned_mob.apply_effect(60, EFFECT_STUN);
        stunned_mob.visible_message(
            span_danger(I18n.T("[] stares blankly at \\the []!", stunned_mob, this)),
            span_userdanger(I18n.T("You look directly into \\the [] and feel weak.", this))
        );
    }

    // tg: code/modules/power/singularity/singularity.dm:459
    public virtual async DmTask emp_area()
    {
        await empulse(this, 8, 10);
    }

    // tg: code/modules/power/singularity/singularity.dm:462
    public override async DmTask<object> singularity_act()
    {
        float gain = (energy / 2);
        float dist = max((current_size - 2), 1);
        investigate_log("has been destroyed by another singularity.", INVESTIGATE_ENGINE);
        await explosion(
            this,
            devastation_range: dist,
            heavy_impact_range: dist * 2,
            light_impact_range: dist * 4
        );
        qdel(this);
        return gain;
    }

    // tg: code/modules/power/singularity/singularity.dm:488
    public override async DmTask<object> deadchat_plays(float? mode = null, float? cooldown = null)
    {
        mode ??= DEMOCRACY_MODE;
        cooldown ??= 12 * SECONDS;
        object dot = await AddComponent(typeof(datum_component_deadchat_control_cardinal_movement), mode, Dm.list(), cooldown, Dm.@new<datum_callback>(this, nameof(stop_deadchat_plays)));

        if (Dm.eq(dot, COMPONENT_INCOMPATIBLE))
            return dot;

        move_self = FALSE;
        return dot;
    }

    // tg: code/modules/power/singularity/singularity.dm:483
    public virtual void stop_deadchat_plays()
    {
        move_self = TRUE;
    }
}

/// Special singularity spawned by being sucked into a black hole during emagged orion trail.
// tg: code/modules/power/singularity/singularity.dm:494
public partial class obj_singularity_orion : obj_singularity
{
    // tg: code/modules/power/singularity/singularity.dm:496
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_component_singularity singularity = singularity_component.resolve() as datum_component_singularity;
        if (singularity != null)
            singularity.grav_pull = 1;
        return dot;
    }

    // tg: code/modules/power/singularity/singularity.dm:501
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        if (SPT_PROB(0.5f, Dm.num(seconds_per_tick)))
            await mezzer();
        return null;
    }
}

// tg: code/modules/power/singularity/singularity.dm:488
public partial class obj_singularity_deadchat_controlled : obj_singularity
{
    // tg: code/modules/power/singularity/singularity.dm:488
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(deadchat_plays(mode: DEMOCRACY_MODE));
        return dot;
    }
}
