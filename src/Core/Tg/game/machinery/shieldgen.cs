// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/shieldgen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering package E10 (D287): the whole file but the cult's barriers. The emergency energy shield and its regenerating
// variant, the anti-breach shielding projector (it fills the space tiles around it with shields, locks with an engineering ID,
// breaks and is mended with cable), the shield wall generator and its xenobiology variant (anchored on a cable it draws its power
// from the grid in SSmachines' early queue and raises a wall to another active generator within eight tiles; the wall blocks
// mobs, lets a glass-passing mover through one time in five and a projectile one in ten, and a blow on it drains both
// generators). The emag came with it (G1a, D316); the logs are deferred
// leaves. The cult's barriers (/obj/structure/emergency_shield/cult and its subtypes) stay inert with the cult (antagonists,
// docs/ai/specs/not-translated.md). The var blocks are generated (D111).

// tg: code/game/machinery/shieldgen.dm:1
public partial class obj_structure_emergency_shield : obj_structure
{
    // tg: code/game/machinery/shieldgen.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(setDir(pick(GLOB.cardinals)));
        air_update_turf(TRUE, TRUE);
        return dot;
    }

    // tg: code/game/machinery/shieldgen.dm:20
    public override object Destroy(float? force = null)
    {
        air_update_turf(TRUE, FALSE);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/shieldgen.dm:24
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        turf T = loc as turf;
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        move_update_air(T);
        return dot;
    }

    // tg: code/game/machinery/shieldgen.dm:29
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        object sw__1 = severity;
        if (Dm.eq(sw__1, 1))
            qdel(this);
        else if (Dm.eq(sw__1, 2))
            await take_damage(50, BRUTE, ENERGY, 0);
        return dot;
    }

    // tg: code/game/machinery/shieldgen.dm:39
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        if (Dm.eq(damage_type, BURN))
            playsound(loc, Dm.resource("sound/effects/empulse.ogg"), 75, TRUE);
        else if (Dm.eq(damage_type, BRUTE))
            playsound(loc, Dm.resource("sound/effects/empulse.ogg"), 75, TRUE);
        return null;
    }

    // tg: code/game/machinery/shieldgen.dm:46
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        sound_effect ??= 1;
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.truthy(dot)) //damage was dealt
            Dm.@new<obj_effect_temp_visual_impact_effect_ion>(loc);
        return dot;
    }
}

/// Subtype of shields that repair over time after sustaining integrity damage
// tg: code/game/machinery/shieldgen.dm:52
public partial class obj_structure_emergency_shield_regenerating : obj_structure_emergency_shield
{
    // tg: code/game/machinery/shieldgen.dm:59
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), Dm.bitor(EMP_PROTECT_SELF, EMP_NO_EXAMINE)));
        return dot;
    }

    // tg: code/game/machinery/shieldgen.dm:63
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/shieldgen.dm:67
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.truthy(dot))
            // We took some damage so we'll start processing to heal said damage.
            START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/game/machinery/shieldgen.dm:73
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object repaired_amount = Waiting.NoSleep(repair_damage(heal_rate_per_second * Dm.num(seconds_per_tick)));
        if (Dm.num(repaired_amount) <= 0)
            // 0 damage repaired means we're at the max integrity, so don't need to process anymore
            STOP_PROCESSING(SSobj, this);
        return null;
    }
}

// tg: code/game/machinery/shieldgen.dm:142
public partial class obj_machinery_shieldgen : obj_machinery
{
    // tg: code/game/machinery/shieldgen.dm:157
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        deployed_shields = Dm.list();
        if (Dm.truthy(mapload) && Dm.truthy(active) && Dm.truthy(anchored))
            shields_up();
        return dot;
    }

    // tg: code/game/machinery/shieldgen.dm:163
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(deployed_shields);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/shieldgen.dm:168
    public virtual void shields_up()
    {
        active = TRUE;
        update_appearance();
        move_resist = INFINITY;

        foreach (turf target_tile in Dm.each<turf>(RANGE_TURFS(shield_range, this)))
        {
            if (isspaceturf(target_tile) && !Dm.truthy(Dm.locate<obj_structure_emergency_shield>(target_tile)))
            {
                if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || prob(33))
                    Dm.append(ref deployed_shields, Dm.@new<obj_structure_emergency_shield>(target_tile));
            }
        }
    }

    // tg: code/game/machinery/shieldgen.dm:178
    public virtual void shields_down()
    {
        active = FALSE;
        move_resist = Dm.initial(this, (atom_movable a) => a.move_resist);
        update_appearance();
        QDEL_LIST(deployed_shields);
    }

    // tg: code/game/machinery/shieldgen.dm:184
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)) && Dm.truthy(active))
        {
            if (Dm.truthy(length(deployed_shields)) && SPT_PROB(2.5f, seconds_per_tick))
                qdel(pick(deployed_shields));
        }
        return null;
    }

    // tg: code/game/machinery/shieldgen.dm:189
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(locked) && !HAS_SILICON_ACCESS(user))
        {
            to_chat(user, span_warning(I18n.T("The machine is locked, you are unable to use it!")));
            return dot;
        }
        if (Dm.truthy(panel_open))
        {
            to_chat(user, span_warning(I18n.T("The panel must be closed before operating this machine!")));
            return dot;
        }

        if (Dm.truthy(active))
        {
            user.visible_message(span_notice(I18n.T("[] deactivated \\the [].", user, this)),
                span_notice(I18n.T("You deactivate \\the [].", this)),
                span_hear(I18n.T("You hear heavy droning fade out.")));
            shields_down();
        }
        else
        {
            if (Dm.truthy(anchored))
            {
                user.visible_message(span_notice(I18n.T("[] activated \\the [].", user, this)),
                    span_notice(I18n.T("You activate \\the [].", this)),
                    span_hear(I18n.T("You hear heavy droning.")));
                shields_up();
            }
            else
                to_chat(user, span_warning(I18n.T("The device must first be secured to the floor!")));
        }
        return dot;
    }

    // tg: code/game/machinery/shieldgen.dm:215
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this, 100);
        toggle_panel_open();
        if (Dm.truthy(panel_open))
            to_chat(user, span_notice(I18n.T("You open the panel and expose the wiring.")));
        else
            to_chat(user, span_notice(I18n.T("You close the panel.")));
        return TRUE;
    }

    // tg: code/game/machinery/shieldgen.dm:224
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = TRUE;
        if (Dm.truthy(locked))
        {
            to_chat(user, span_warning(I18n.T("The bolts are covered! Unlocking this would retract the covers.")));
            return dot;
        }
        if (!Dm.truthy(anchored) && !Dm.truthy(isinspace()))
        {
            tool.play_tool_sound(this, 100);
            balloon_alert(user, I18n.T("secured"));
            await set_anchored(TRUE);
        }
        else if (Dm.truthy(anchored))
        {
            tool.play_tool_sound(this, 100);
            balloon_alert(user, I18n.T("unsecured"));
            if (Dm.truthy(active))
            {
                to_chat(user, span_notice(I18n.T("\\The [] shuts off!", this)));
                shields_down();
            }
            await set_anchored(FALSE);
        }
        return dot;
    }

    // tg: code/game/machinery/shieldgen.dm:242
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_stack_cable_coil && Dm.truthy(Dm.bitand(machine_stat, BROKEN)) && Dm.truthy(panel_open))
        {
            obj_item_stack_cable_coil coil = (obj_item_stack_cable_coil)tool;
            if (Dm.num(coil.get_amount()) < 1)
            {
                to_chat(user, span_warning(I18n.T("You need one length of cable to repair []!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            to_chat(user, span_notice(I18n.T("You begin to replace the wires...")));
            if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;
            if (Dm.num(coil.get_amount()) < 1)
                return ITEM_INTERACT_BLOCKING;
            await coil.use(1);
            atom_integrity = max_integrity;
            await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(BROKEN)));
            to_chat(user, span_notice(I18n.T("You repair \\the [].", this)));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(tool.GetID()))
        {
            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            {
                to_chat(user, span_danger(I18n.T("Error, access controller damaged!")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await allowed__proc(user)))
            {
                to_chat(user, span_danger(I18n.T("Access denied.")));
                return ITEM_INTERACT_BLOCKING;
            }
            locked = Dm.truthy(locked) ? FALSE : TRUE;
            to_chat(user, span_notice(I18n.T("You [] the controls.", Dm.truthy(locked) ? "lock" : "unlock")));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/machinery/shieldgen.dm:273
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            to_chat(user, span_warning(I18n.T("The access controller is damaged!")));
            return FALSE;
        }
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        locked = FALSE;
        playsound(this, SFX_SPARKS, 100, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        balloon_alert(user, "access controller shorted");
        return TRUE;
    }

    // tg: code/game/machinery/shieldgen.dm:283
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("shield[][]", Dm.truthy(active) ? "on" : "off", Dm.truthy(Dm.bitand(machine_stat, BROKEN)) ? "br" : null);
        return await base.update_icon_state(__extra);
    }
}

// tg: code/game/machinery/shieldgen.dm:290
public partial class obj_machinery_power_shieldwallgen : obj_machinery_power
{
    // tg: code/game/machinery/shieldgen.dm:287  #define ACTIVE_SETUPFIELDS 1 (file-local, #undef at the end)
    private const float ACTIVE_SETUPFIELDS = 1;

    // tg: code/game/machinery/shieldgen.dm:288  #define ACTIVE_HASFIELDS 2 (file-local, #undef at the end)
    private const float ACTIVE_HASFIELDS = 2;

    // tg: code/game/machinery/shieldgen.dm:331
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //Add to the early process queue to prioritize power draw
        // rules 10.6: tg's Master creates SSmachines before the map; a test harness world may lack it.
        if (SSmachines != null)
            Dm.append(ref SSmachines.processing_early, this);
        if (Dm.truthy(anchored))
            connect_to_network();
        RegisterSignal(this, COMSIG_ATOM_SINGULARITY_TRY_MOVE, nameof(block_singularity_if_active));
        set_wires(Dm.@new<datum_wires_shieldwallgen>(this));
        return dot;
    }

    // tg: code/game/machinery/shieldgen.dm:340
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        Waiting.NoSleep(set_light(l_on: Dm.truthy(active) ? TRUE : FALSE));
        return dot;
    }

    // tg: code/game/machinery/shieldgen.dm:344
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(active) ? "_on" : "");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/shieldgen.dm:348
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(panel_open))
            return dot;
        Dm.append(ref dot, "shieldgen_wires");
        return dot;
    }

    // tg: code/game/machinery/shieldgen.dm:354
    public override object Destroy(float? force = null)
    {
        foreach (object d in Dm.each(GLOB.cardinals))
            cleanup_field(d);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/shieldgen.dm:359
    public override object should_have_node()
    {
        return anchored;
    }

    // tg: code/game/machinery/shieldgen.dm:362
    public override object connect_to_network()
    {
        object dot = null;
        if (!Dm.truthy(anchored))
            return FALSE;
        dot = base.connect_to_network();
        return dot;
    }

    // tg: code/game/machinery/shieldgen.dm:367
    public override object process_early(params object[] __extra)
    {
        if (Dm.truthy(active))
        {
            if (Dm.eq(active, ACTIVE_SETUPFIELDS))
            {
                float fields = 0;
                foreach (object d in Dm.each(GLOB.cardinals))
                {
                    if (Dm.truthy(setup_field(d)))
                        fields++;
                }
                if (fields != 0)
                    active = ACTIVE_HASFIELDS;
                update_appearance();
            }
            if (!Dm.truthy(active_power_usage) || Dm.num(surplus()) >= active_power_usage)
                add_load(active_power_usage);
            else
            {
                visible_message(span_danger(I18n.T("[] shuts down due to lack of power!", this)),
                    I18n.T("If this message is ever seen, something is wrong."),
                    span_hear(I18n.T("You hear heavy droning fade out.")));
                deactivate();
                log_game(Dm.text("[] deactivated due to lack of power at []", this, AREACOORD(this)));
            }
        }
        else
        {
            update_appearance();
            foreach (object d in Dm.each(GLOB.cardinals))
                cleanup_field(d);
        }
        return null;
    }

    /// Constructs the actual field walls in the specified direction, cleans up old/stuck shields before doing so
    // tg: code/game/machinery/shieldgen.dm:391
    public virtual object setup_field(object direction = null)
    {
        if (!Dm.truthy(direction))
            return null;

        turf T = loc as turf;
        obj_machinery_power_shieldwallgen G = null;
        float steps = 0;
        float opposite_direction = REVERSE_DIR(Dm.num(direction));

        for (float i = 1, i__end = shield_range; i <= i__end; i++) //checks out to 8 tiles away for another generator
        {
            T = get_step(T, Dm.num(direction));
            G = Dm.locate<obj_machinery_power_shieldwallgen>(T);
            if (Dm.truthy(G))
            {
                if (!Dm.truthy(G.active))
                    return null;
                G.cleanup_field(opposite_direction);
                break;
            }
            else
                steps++;
        }

        if (!Dm.truthy(G) || steps == 0) //no shield gen or no tiles between us and the gen
            return null;

        for (float i = 1, i__end = steps; i <= i__end; i++) //creates each field tile
        {
            T = get_step(T, opposite_direction);
            Dm.@new<obj_machinery_shieldwall>(T, this, G);
        }
        return TRUE;
    }

    /// cleans up fields in the specified direction if they belong to this generator
    // tg: code/game/machinery/shieldgen.dm:420
    public virtual void cleanup_field(object direction = null)
    {
        obj_machinery_shieldwall F = null;
        obj_machinery_power_shieldwallgen G = null;
        turf T = loc as turf;

        for (float i = 1, i__end = shield_range; i <= i__end; i++)
        {
            T = get_step(T, Dm.num(direction));

            G = Dm.locate<obj_machinery_power_shieldwallgen>(T);
            if (Dm.truthy(G) && !Dm.truthy(G.active))
                break;

            F = Dm.locate<obj_machinery_shieldwall>(T);
            if (Dm.truthy(F) && (F.gen_primary == this || F.gen_secondary == this)) //it's ours, kill it.
                qdel(F);
        }
    }

    // tg: code/game/machinery/shieldgen.dm:436
    [SignalHandler]
    public virtual object block_singularity_if_active()
    {
        if (Dm.truthy(active))
            return SINGULARITY_TRY_MOVE_BLOCK;
        return null;
    }

    // tg: code/game/machinery/shieldgen.dm:442
    public override object can_be_unfasten_wrench(mob user = null, object silent = null)
    {
        if (Dm.truthy(active))
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(I18n.T("Turn off the shield generator first!")));
            return FAILED_UNFASTEN;
        }
        return base.can_be_unfasten_wrench(user, silent);
    }

    // tg: code/game/machinery/shieldgen.dm:450
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object unfasten_result = await default_unfasten_wrench(user, tool, time: 0);
        update_cable_icons_on_turf(get_turf(this));
        if (Dm.eq(unfasten_result, SUCCESSFUL_UNFASTEN) && Dm.truthy(anchored))
            connect_to_network();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/shieldgen.dm:457
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open) && Dm.truthy(locked))
        {
            balloon_alert(user, I18n.T("unlock first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/shieldgen.dm:464
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(active))
            return ITEM_INTERACT_BLOCKING;

        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/shieldgen.dm:470
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(tool.GetID()))
        {
            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            {
                balloon_alert(user, I18n.T("malfunctioning!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await allowed__proc(user)))
            {
                balloon_alert(user, I18n.T("no access!"));
                return ITEM_INTERACT_BLOCKING;
            }
            locked = Dm.truthy(locked) ? FALSE : TRUE;
            balloon_alert(user, Dm.truthy(locked) ? I18n.T("locked!") : I18n.T("unlocked"));
            return ITEM_INTERACT_SUCCESS;
        }

        add_fingerprint(user);
        if (Dm.truthy(is_wire_tool(tool)) && Dm.truthy(panel_open))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/machinery/shieldgen.dm:490
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("not secured!"));
            return dot;
        }
        if (Dm.truthy(locked) && !HAS_SILICON_ACCESS(user))
        {
            balloon_alert(user, I18n.T("locked!"));
            return dot;
        }
        if (!Dm.truthy(powernet))
        {
            balloon_alert(user, I18n.T("needs to be powered by wire!"));
            return dot;
        }
        if (Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("panel open!"));
            return dot;
        }

        if (Dm.truthy(active))
        {
            user.visible_message(span_notice(I18n.T("[] turned \\the [] off.", user, this)),
                span_notice(I18n.T("You turn off \\the [].", this)),
                span_hear(I18n.T("You hear heavy droning fade out.")));
            deactivate();
            user.log_message(Dm.text("deactivated [].", this), LOG_GAME);
        }
        else
        {
            user.visible_message(span_notice(I18n.T("[] turned \\the [] on.", user, this)),
                span_notice(I18n.T("You turn on \\the [].", this)),
                span_hear(I18n.T("You hear heavy droning.")));
            await activate();
            user.log_message(Dm.text("activated [].", this), LOG_GAME);
        }
        add_fingerprint(user);
        return dot;
    }

    // tg: code/game/machinery/shieldgen.dm:521
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            to_chat(user, span_warning(I18n.T("The access controller is damaged!")));
            return FALSE;
        }
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        locked = FALSE;
        playsound(this, SFX_SPARKS, 100, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        balloon_alert(user, "access controller shorted");
        return TRUE;
    }

    /// Turn the machine on with side effects
    // tg: code/game/machinery/shieldgen.dm:532
    public virtual async DmTask activate()
    {
        active = ACTIVE_SETUPFIELDS;
        await AddElement(typeof(datum_element_give_turf_traits), string_list(Dm.list(TRAIT_CONTAINMENT_FIELD)));
    }

    /// Turn the machine off with side effects
    // tg: code/game/machinery/shieldgen.dm:537
    public virtual void deactivate()
    {
        active = FALSE;
        foreach (object d in Dm.each(GLOB.cardinals))
            cleanup_field(d);
        update_appearance();
        RemoveElement(typeof(datum_element_give_turf_traits), string_list(Dm.list(TRAIT_CONTAINMENT_FIELD)));
    }
}

//////////////Containment Field START
// tg: code/game/machinery/shieldgen.dm:545
public partial class obj_machinery_shieldwall : obj_machinery
{
    // tg: code/game/machinery/shieldgen.dm:560
    [DmParams("mapload", "first_gen", "second_gen")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_machinery_power_shieldwallgen first_gen = Dm.arg<obj_machinery_power_shieldwallgen>(__extra, 0);
        obj_machinery_power_shieldwallgen second_gen = Dm.arg<obj_machinery_power_shieldwallgen>(__extra, 1);
        object dot = base.Initialize(mapload, Dm.forward(__extra, first_gen, second_gen));
        gen_primary = first_gen;
        gen_secondary = second_gen;
        if (Dm.truthy(gen_primary) && Dm.truthy(gen_secondary))
        {
            needs_power = TRUE;
            Waiting.NoSleep(setDir(get_dir(gen_primary, gen_secondary)));
        }
        foreach (mob_living L in Dm.each<mob_living>(get_turf(this)))
        {
            visible_message(span_danger(I18n.T("\\The [] is suddenly occupying the same space as \\the []!", this, L)));
            L.investigate_log(Dm.text("has been gibbed by [].", this), INVESTIGATE_DEATHS);
            Waiting.NoSleep(L.gib(DROP_ALL_REMAINS));
        }
        RegisterSignal(this, COMSIG_ATOM_SINGULARITY_TRY_MOVE, nameof(block_singularity));
        Waiting.NoSleep(AddElement(typeof(datum_element_give_turf_traits), string_list(Dm.list(TRAIT_CONTAINMENT_FIELD))));
        return dot;
    }

    // tg: code/game/machinery/shieldgen.dm:574
    public override object Destroy(float? force = null)
    {
        gen_primary = null;
        gen_secondary = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/shieldgen.dm:579
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // DEVIATION(DV12): . += emissive_appearance(icon, icon_state, src, alpha = 200), the glow on the emissive plane, is not created
        return dot;
    }

    // tg: code/game/machinery/shieldgen.dm:583
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(needs_power))
        {
            if (!Dm.truthy(gen_primary) || !Dm.truthy(gen_primary.active) || !Dm.truthy(gen_secondary) || !Dm.truthy(gen_secondary.active))
            {
                qdel(this);
                return null;
            }

            drain_power(10);
        }
        return null;
    }

    // tg: code/game/machinery/shieldgen.dm:591
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        if (Dm.eq(damage_type, BURN))
            playsound(loc, Dm.resource("sound/effects/empulse.ogg"), 75, TRUE);
        else if (Dm.eq(damage_type, BRUTE))
            playsound(loc, Dm.resource("sound/effects/empulse.ogg"), 75, TRUE);
        return null;
    }

    //the shield wall is immune to damage but it drains the stored power of the generators.
    // tg: code/game/machinery/shieldgen.dm:599
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        sound_effect ??= 1;
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.eq(damage_type, BRUTE) || Dm.eq(damage_type, BURN))
            drain_power(damage_amount);
        return dot;
    }

    /// succs power from the connected shield wall generator
    // tg: code/game/machinery/shieldgen.dm:605
    public virtual void drain_power(object drain_amount = null)
    {
        if (Dm.truthy(needs_power) && Dm.truthy(gen_primary))
        {
            gen_primary.add_load(Dm.num(drain_amount) * 0.5f);
            if (Dm.truthy(gen_secondary)) //using power may cause us to be destroyed
                gen_secondary.add_load(Dm.num(drain_amount) * 0.5f);
        }
    }

    // tg: code/game/machinery/shieldgen.dm:611
    [SignalHandler]
    public virtual object block_singularity()
    {
        return SINGULARITY_TRY_MOVE_BLOCK;
    }

    // tg: code/game/machinery/shieldgen.dm:616
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (mover is atom_movable && Dm.truthy(Dm.bitand(mover.pass_flags, PASSGLASS)))
            return prob(20) ? TRUE : FALSE;
        else
        {
            if (isprojectile(mover))
                return prob(10) ? TRUE : FALSE;
        }
        return dot;
    }
}
