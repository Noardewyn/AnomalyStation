// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/singularity/field_generator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E7 (singularity containment): the whole file. The field generator is wrenched and welded to the floor, turned on
// by hand (15 seconds of warming up, its +a1..+a3 overlays), takes its power from the shots that hit it (an emitter's beam), links
// to another active generator up to eight tiles away in each cardinal direction and makes the containment fields between them
// (containment_field.cs), with the shield floor inside a closed loop; it draws its power every machines cycle, shares it with the
// generators it links to and shuts down, its fields gone, when it runs out (the +p1..+p6 power overlays, +on while it has fields).
// It blocks the singularity while active and ignores EMPs (empprotection). The anchored, starts_on and magic subtypes too. The var
// blocks are generated (D111).

/*
field_generator power level display
The icon used for the field_generator need to have 6 icon states
named 'Field_Gen +p[num]' where 'num' ranges from 1 to 6

The power level is displayed using overlays. The current displayed power level is stored in 'powerlevel'.
The overlay in use and the powerlevel variable must be kept in sync.  A powerlevel equal to 0 means that
no power level overlay is currently in the overlays list.
-Aygar
*/

public static partial class GlobalProcs
{
    // tg: code/modules/power/singularity/field_generator.dm:15  #define field_generator_max_power 250 (a global #define outside
    // code/__DEFINES, which the defines converter does not emit: rules 11.4)
    public const int field_generator_max_power = 250;
}

// tg: code/modules/power/singularity/field_generator.dm:26
public partial class obj_machinery_field_generator : obj_machinery_field
{
    // tg: code/modules/power/singularity/field_generator.dm:17  #define FG_OFFLINE 0 (file-local, #undef at :452)
    private const int FG_OFFLINE = 0;
    // tg: code/modules/power/singularity/field_generator.dm:18  #define FG_CHARGING 1 (file-local, #undef at :453)
    private const int FG_CHARGING = 1;
    // tg: code/modules/power/singularity/field_generator.dm:19  #define FG_ONLINE 2 (file-local, #undef at :454)
    private const int FG_ONLINE = 2;

    //field generator construction defines
    // tg: code/modules/power/singularity/field_generator.dm:22  #define FG_UNSECURED 0 (file-local, #undef at :448)
    private const int FG_UNSECURED = 0;
    // tg: code/modules/power/singularity/field_generator.dm:23  #define FG_SECURED 1 (file-local, #undef at :449)
    private const int FG_SECURED = 1;
    // tg: code/modules/power/singularity/field_generator.dm:24  #define FG_WELDED 2 (file-local, #undef at :450)
    private const int FG_WELDED = 2;

    // tg: code/modules/power/singularity/field_generator.dm:66
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (Dm.truthy(warming_up))
            Dm.append(ref dot, Dm.text("+a[]", warming_up));
        if (Dm.truthy(LAZYLEN(fields)))
            Dm.append(ref dot, "+on");
        if (Dm.truthy(power_level))
            Dm.append(ref dot, Dm.text("+p[]", power_level));
        return dot;
    }

    // tg: code/modules/power/singularity/field_generator.dm:76
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        Waiting.NoSleep(AddElement(typeof(datum_element_blocks_explosives)));
        dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), Dm.bitor(EMP_PROTECT_SELF, EMP_PROTECT_WIRES)));
        RegisterSignal(this, COMSIG_ATOM_SINGULARITY_TRY_MOVE, nameof(block_singularity_if_active));
        return dot;
    }

    // tg: code/modules/power/singularity/field_generator.dm:86
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (active == FG_ONLINE)
            calc_power();
        return null;
    }

    // tg: code/modules/power/singularity/field_generator.dm:90
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (state != FG_WELDED)
        {
            to_chat(user, span_warning(I18n.T("[] needs to be firmly secured to the floor first!", this)));
            return null;
        }
        if (get_dist(this, user) > 1)//Need to actually touch the thing to turn it on
            return null;
        if (Dm.num(active) >= FG_CHARGING)
        {
            to_chat(user, span_warning(I18n.T("You are unable to turn off [] once it is online!", this)));
            return TRUE;
        }

        user.visible_message(
            span_notice(I18n.T("[] turns on [].", user, this)),
            span_notice(I18n.T("You turn on [].", this)),
            span_hear(I18n.T("You hear heavy droning.")));
        await turn_on();
        investigate_log(Dm.text("activated by [].", key_name(user)), INVESTIGATE_ENGINE);

        add_fingerprint(user);
        return null;
    }

    // tg: code/modules/power/singularity/field_generator.dm:109
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = null;
        dot = await base.set_anchored(anchorvalue);
        if (dot == null)
            return dot;
        if (Dm.truthy(active))
            turn_off();
        state = Dm.truthy(anchorvalue) ? FG_SECURED : FG_UNSECURED;
        return dot;
    }

    // tg: code/modules/power/singularity/field_generator.dm:117
    public override object can_be_unfasten_wrench(mob user = null, object silent = null)
    {
        if (Dm.truthy(active))
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(I18n.T("Turn \\the [] off first!", this)));
            return FAILED_UNFASTEN;
        }

        else if (state == FG_WELDED)
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(I18n.T("[] is welded to the floor!", this)));
            return FAILED_UNFASTEN;
        }

        return base.can_be_unfasten_wrench(user, silent);
    }

    // tg: code/modules/power/singularity/field_generator.dm:130
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = null;
        dot = await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/singularity/field_generator.dm:135
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        obj_item welder = tool;
        await base.welder_act(user, welder);
        if (Dm.truthy(active))
        {
            to_chat(user, span_warning(I18n.T("[] needs to be off!", this)));
            return TRUE;
        }

        if (state == FG_UNSECURED)
            to_chat(user, span_warning(I18n.T("[] needs to be wrenched to the floor!", this)));

        else if (state == FG_SECURED)
        {
            if (!Dm.truthy(welder.tool_start_check(user, amount: 1)))
                return TRUE;
            user.visible_message(
                span_notice(I18n.T("[] starts to weld [] to the floor.", user, this)),
                span_notice(I18n.T("You start to weld \\the [] to the floor...", this)),
                span_hear(I18n.T("You hear welding.")));
            if (Dm.truthy(await welder.use_tool(this, user, 20, volume: 50)) && state == FG_SECURED)
            {
                state = FG_WELDED;
                to_chat(user, span_notice(I18n.T("You weld the field generator to the floor.")));
            }
        }

        else if (state == FG_WELDED)
        {
            if (!Dm.truthy(welder.tool_start_check(user, amount: 1)))
                return TRUE;
            user.visible_message(
                span_notice(I18n.T("[] starts to cut [] free from the floor.", user, this)),
                span_notice(I18n.T("You start to cut \\the [] free from the floor...", this)),
                span_hear(I18n.T("You hear welding.")));
            if (Dm.truthy(await welder.use_tool(this, user, 20, volume: 50)) && state == FG_WELDED)
            {
                state = FG_SECURED;
                to_chat(user, span_notice(I18n.T("You cut \\the [] free from the floor.", this)));
            }
        }

        return TRUE;
    }

    // tg: code/modules/power/singularity/field_generator.dm:170
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        if (((mob_living_simple_animal)user).environment_smash == ENVIRONMENT_SMASH_RWALLS && active == FG_OFFLINE && state != FG_UNSECURED)
        {
            await set_anchored(FALSE);
            user.visible_message(span_warning(I18n.T("[] rips [] free from its moorings!", user, this)));
        }
        else
            await base.attack_animal(user, modifiers);
        if (!Dm.truthy(anchored))
            await step(this, get_dir(user, this));
        return null;
    }

    // tg: code/modules/power/singularity/field_generator.dm:185
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        obj_projectile considered_bullet = hitting_projectile;
        object dot = null;
        if (!Dm.eq(considered_bullet.armor_flag, BULLET))
        {
            power = min(Dm.num(power) + Dm.num(considered_bullet.damage), field_generator_max_power);
            check_power_level();
        }
        dot = await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
        return dot;
    }

    // tg: code/modules/power/singularity/field_generator.dm:192
    public override object Destroy(float? force = null)
    {
        cleanup();
        return base.Destroy(force);
    }

    /**
     *The power level is displayed using overlays. The current displayed power level is stored in 'powerlevel'.
     *The overlay in use and the powerlevel variable must be kept in sync.  A powerlevel equal to 0 means that
     *no power level overlay is currently in the overlays list.
     */
    // tg: code/modules/power/singularity/field_generator.dm:201
    public virtual void check_power_level()
    {
        float new_level = round(6 * Dm.num(power) / field_generator_max_power);
        if (new_level != power_level)
        {
            power_level = new_level;
            update_appearance();
        }
    }

    // tg: code/modules/power/singularity/field_generator.dm:207
    public virtual void turn_off()
    {
        active = FG_OFFLINE;
        can_atmos_pass = ATMOS_PASS_YES;
        air_update_turf(TRUE, FALSE);
        Waiting.DetachSync(() => cleanup()); // INVOKE_ASYNC(src, PROC_REF(cleanup))
        RemoveElement(typeof(datum_element_give_turf_traits), string_list(Dm.list(TRAIT_CONTAINMENT_FIELD)));
        if (Dm.truthy(instantenous))
        {
            warming_up = 0;
            return;
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(cool_down)), 5 * SECONDS, file: "code/modules/power/singularity/field_generator.dm", line: 216);
    }

    // tg: code/modules/power/singularity/field_generator.dm:218
    public virtual void cool_down()
    {
        if (Dm.truthy(active) || warming_up <= 0)
            return;
        warming_up--;
        update_appearance();
        if (warming_up > 0)
            _addtimer(Dm.@new<datum_callback>(this, nameof(cool_down)), 5 * SECONDS, file: "code/modules/power/singularity/field_generator.dm", line: 224);
    }

    // tg: code/modules/power/singularity/field_generator.dm:226
    public virtual async DmTask turn_on()
    {
        await AddElement(typeof(datum_element_give_turf_traits), string_list(Dm.list(TRAIT_CONTAINMENT_FIELD)));
        if (Dm.truthy(instantenous))
        {
            active = FG_ONLINE;
            warming_up = 3;
            start_fields();
            return;
        }
        active = FG_CHARGING;
        _addtimer(Dm.@new<datum_callback>(this, nameof(warm_up)), 5 * SECONDS, file: "code/modules/power/singularity/field_generator.dm", line: 234);
    }

    // tg: code/modules/power/singularity/field_generator.dm:236
    public virtual void warm_up()
    {
        if (!Dm.truthy(active))
            return;
        warming_up++;
        update_appearance();
        if (warming_up >= 3)
            start_fields();
        else
            _addtimer(Dm.@new<datum_callback>(this, nameof(warm_up)), 5 * SECONDS, file: "code/modules/power/singularity/field_generator.dm", line: 244);
    }

    // tg: code/modules/power/singularity/field_generator.dm:246
    public virtual object calc_power(object set_power_draw = null)
    {
        float power_draw = 2 + fields.len;
        if (Dm.truthy(set_power_draw))
            power_draw = Dm.num(set_power_draw);

        if (Dm.truthy(draw_power(Dm.round(power_draw * 0.5f, 1))))
        {
            check_power_level();
            return TRUE;
        }
        else
        {
            visible_message(span_danger(I18n.T("\\The [] shuts down!", this)), span_hear(I18n.T("You hear something shutting down.")));
            turn_off();
            investigate_log("ran out of power and DEACTIVATED.", INVESTIGATE_ENGINE);
            power = 0;
            check_power_level();
            return FALSE;
        }
    }

    //This could likely be better, it tends to start loopin if you have a complex generator loop setup.  Still works well enough to run the engine fields will likely recode the field gens and fields sometime -Mport
    // tg: code/modules/power/singularity/field_generator.dm:263
    public virtual object draw_power(float? draw = null, float? failsafe = null, obj_machinery_field_generator other_generator = null, obj_machinery_field_generator last = null)
    {
        draw ??= 0;
        failsafe ??= FALSE;
        if ((other_generator != null && (other_generator == this)) || (failsafe >= 8))//Loopin, set fail
            return FALSE;
        else
            failsafe++;

        if (Dm.num(power) >= draw)//We have enough power
        {
            power -= draw;
            return TRUE;
        }

        //Need more power
        draw -= Dm.num(power);
        power = 0;
        foreach (object connected_generator in Dm.each(connected_gens))
        {
            obj_machinery_field_generator considered_generator = (obj_machinery_field_generator)connected_generator;
            if (considered_generator == last)//We just asked you
                continue;
            if (other_generator != null)//Another gen is askin for power and we dont have it
            {
                if (Dm.truthy(considered_generator.draw_power(draw, failsafe, other_generator, this)))//Can you take the load
                    return TRUE;
                return FALSE;
            }
            //We are askin another for power
            if (Dm.truthy(considered_generator.draw_power(draw, failsafe, this, this)))
                return TRUE;
            return FALSE;
        }
        return null;
    }

    // tg: code/modules/power/singularity/field_generator.dm:290
    public virtual void start_fields()
    {
        if (state != FG_WELDED || !Dm.truthy(anchored))
        {
            turn_off();
            return;
        }
        move_resist = INFINITY;
        set_explosion_block(INFINITY);
        can_atmos_pass = ATMOS_PASS_NO;
        air_update_turf(TRUE, TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(setup_field), 1), 0.1f * SECONDS, file: "code/modules/power/singularity/field_generator.dm", line: 298);
        _addtimer(Dm.@new<datum_callback>(this, nameof(setup_field), 2), 0.2f * SECONDS, file: "code/modules/power/singularity/field_generator.dm", line: 299);
        _addtimer(Dm.@new<datum_callback>(this, nameof(setup_field), 4), 0.3f * SECONDS, file: "code/modules/power/singularity/field_generator.dm", line: 300);
        _addtimer(Dm.@new<datum_callback>(this, nameof(setup_field), 8), 0.4f * SECONDS, file: "code/modules/power/singularity/field_generator.dm", line: 301);
        // VARSET_CALLBACK(src, active, FG_ONLINE): ___callbackvarset is not declared (the GAP(lead) of doorcontrol.cs); its datum
        // branch for this var is active__varset below
        _addtimer(Dm.@new<datum_callback>(this, nameof(active__varset), FG_ONLINE), 0.5f * SECONDS, file: "code/modules/power/singularity/field_generator.dm", line: 302);
    }

    /// VARSET_CALLBACK(src, active, value)'s target: ___callbackvarset's datum branch (varset_callback.dm:8-23) for this var.
    public virtual void active__varset(object var_value = null)
    {
        active = Dm.num_or_null(var_value);
    }

    // tg: code/modules/power/singularity/field_generator.dm:304
    public virtual object setup_field(object NSEW = null)
    {
        turf current_turf = loc as turf;
        if (!istype(loc, typeof(turf)))
            return FALSE;

        obj_machinery_field_generator found_generator = null;
        float steps = 0;
        if (!Dm.truthy(NSEW))//Make sure its ran right
            return FALSE;
        for (float dist = 0, dist__end = 7; dist <= dist__end; dist++) // checks out to 8 tiles away for another generator
        {
            current_turf = get_step(current_turf, Dm.num(NSEW));
            if (Dm.truthy(current_turf.density))//We cant shoot a field though this
                return FALSE;

            found_generator = Dm.locate<obj_machinery_field_generator>(current_turf);
            if (found_generator != null)
            {
                steps -= 1;
                if (!Dm.truthy(found_generator.active))
                    return FALSE;
                break;
            }

            foreach (object turf_content in Dm.each(current_turf.contents))
            {
                atom found_atom = (atom)turf_content;
                if (ismob(found_atom))
                    continue;
                if (Dm.truthy(found_atom.density))
                    return FALSE;
            }

            steps++;
        }

        if (found_generator == null)
            return FALSE;

        current_turf = loc as turf;
        for (float dist = 0, dist__end = steps; dist <= dist__end; dist++) // creates each field tile
        {
            float field_dir = get_dir(current_turf, get_step(found_generator.loc, Dm.num(NSEW)));
            current_turf = get_step(current_turf, Dm.num(NSEW));
            if (!Dm.truthy(Dm.locate<obj_machinery_field_containment>(current_turf)))
            {
                obj_machinery_field_containment created_field = Dm.@new<obj_machinery_field_containment>(current_turf);
                created_field.set_master(this, found_generator);
                Waiting.NoSleep(created_field.setDir(field_dir));
                Dm.append(ref fields, created_field);
                Dm.append(ref found_generator.fields, created_field);
                foreach (mob_living shocked_mob in Dm.each<mob_living>(current_turf))
                    created_field.on_entered(this, shocked_mob);
            }
        }

        Dm.combine(ref connected_gens, found_generator);
        Dm.combine(ref found_generator.connected_gens, this);
        shield_floor(TRUE);
        update_appearance();
        return null;
    }

    // tg: code/modules/power/singularity/field_generator.dm:356
    public virtual void cleanup()
    {
        clean_up = TRUE;
        foreach (object field in Dm.each(fields))
            qdel(field);

        shield_floor(FALSE);

        foreach (object connected_generator in Dm.each(connected_gens))
        {
            obj_machinery_field_generator considered_generator = (obj_machinery_field_generator)connected_generator;
            Dm.remove(ref considered_generator.connected_gens, this);
            if (!Dm.truthy(considered_generator.clean_up))//Makes the other gens clean up as well
                considered_generator.cleanup();
            Dm.remove(ref connected_gens, considered_generator);
        }
        clean_up = FALSE;
        update_appearance();

        move_resist = Dm.initial(this, (atom_movable p) => p.move_resist);
        set_explosion_block(0);
    }

    // tg: code/modules/power/singularity/field_generator.dm:375
    public virtual void shield_floor(object create = null)
    {
        if (connected_gens.len < 2)
            return;
        float connected_gen_counter;
        for (connected_gen_counter = 1; connected_gen_counter < connected_gens.len; connected_gen_counter++)
        {

            DmList connected_gen_list = (DmList)Dm.bitxor(Dm.bitand(((obj_machinery_field_generator)connected_gens[connected_gen_counter]).connected_gens, ((obj_machinery_field_generator)connected_gens[connected_gen_counter + 1]).connected_gens), this);
            if (!Dm.truthy(connected_gen_list.len))
                return;
            obj_machinery_field_generator considered_generator = (obj_machinery_field_generator)connected_gen_list[1];

            float x_step;
            float y_step;
            if (considered_generator.x > x && considered_generator.y > y)
            {
                for (x_step = x; x_step <= considered_generator.x; x_step++)
                    for (y_step = y; y_step <= considered_generator.y; y_step++)
                        place_floor(locate(x_step, y_step, z), create);
            }
            else if (considered_generator.x > x && considered_generator.y < y)
            {
                for (x_step = x; x_step <= considered_generator.x; x_step++)
                    for (y_step = y; y_step >= considered_generator.y; y_step--)
                        place_floor(locate(x_step, y_step, z), create);
            }
            else if (considered_generator.x < x && considered_generator.y > y)
            {
                for (x_step = x; x_step >= considered_generator.x; x_step--)
                    for (y_step = y; y_step <= considered_generator.y; y_step++)
                        place_floor(locate(x_step, y_step, z), create);
            }
            else
            {
                for (x_step = x; x_step >= considered_generator.x; x_step--)
                    for (y_step = y; y_step >= considered_generator.y; y_step--)
                        place_floor(locate(x_step, y_step, z), create);
            }
        }
    }

    // tg: code/modules/power/singularity/field_generator.dm:406
    public virtual void place_floor(object Location = null, object create = null)
    {
        if (Dm.truthy(create) && !Dm.truthy(Dm.locate<obj_effect_shield>(Location)))
            Dm.@new<obj_effect_shield>(Location);
        else if (!Dm.truthy(create))
        {
            obj_effect_shield created_shield = Dm.locate<obj_effect_shield>(Location);
            if (created_shield != null)
                qdel(created_shield);
        }
    }

    // tg: code/modules/power/singularity/field_generator.dm:414
    [SignalHandler]
    public virtual object block_singularity_if_active()
    {
        if (Dm.truthy(active))
            return SINGULARITY_TRY_MOVE_BLOCK;
        return null;
    }

    // tg: code/modules/power/singularity/field_generator.dm:420
    public override async DmTask<object> yeet_shock(mob_living user = null)
    {
        if (Dm.truthy(fields.len))
            await base.yeet_shock(user);
        return null;
    }

    // tg: code/modules/power/singularity/field_generator.dm:424
    public override async DmTask<object> bump_field(atom_movable considered_atom = null)
    {
        atom_movable AM = considered_atom;
        if (Dm.truthy(fields.len))
            await base.bump_field(AM);
        return null;
    }

    // tg: code/modules/power/singularity/field_generator.dm:179
    public override async DmTask<object> blob_act(obj_structure_blob attacking_blob = null)
    {
        if (Dm.truthy(active))
            return FALSE;
        return await base.blob_act(attacking_blob);
    }
}

// tg: code/modules/power/singularity/field_generator.dm:82
public partial class obj_machinery_field_generator_anchored : obj_machinery_field_generator
{
    // tg: code/modules/power/singularity/field_generator.dm:82
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(set_anchored(TRUE));
        return dot;
    }
}

// tg: code/modules/power/singularity/field_generator.dm:428
public partial class obj_machinery_field_generator_starts_on : obj_machinery_field_generator
{
    // tg: code/modules/power/singularity/field_generator.dm:432
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/modules/power/singularity/field_generator.dm:436
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        await turn_on();
    }
}

// tg: code/modules/power/singularity/field_generator.dm:440
public partial class obj_machinery_field_generator_starts_on_magic : obj_machinery_field_generator_starts_on
{
    // tg: code/modules/power/singularity/field_generator.dm:444
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return PROCESS_KILL; // this is the only place calc_power is called, and doing it here avoids one unnecessary proc call
    }
}
