// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/solar.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice G1 (Meta's solars, D163): the whole file but the paper guide (a var-only type) and, in the control computer, the history chart's
// extras. The var blocks are generated (D111). The panel's 2.5D look is tg's: two vis_contents overlays (the glass and its edge) that
// animate() turns toward the sun (get_panel_transform); the replicator flattens them into the panel's overlays with their transform and
// pixel_z, and the client draws the panel turned by that transform (docs/ai/specs/models.md, "azimuth"). The emissive blocker of the overlays
// (blocks_emissive) is DEVIATION(DV12). The control computer's investigate logs do not exist in tg's file.

// tg: code/modules/power/solar.dm:1
public partial class obj_machinery_power_solar : obj_machinery_power
{
    // tg: code/modules/power/solar.dm:1  #define SOLAR_GEN_RATE 2500, PANEL_Z_OFFSET 13, PANEL_EDGE_Z_OFFSET (PANEL_Z_OFFSET - 2): file-local, #undef'd at :646
    internal const float SOLAR_GEN_RATE = 2500;
    private const float PANEL_Z_OFFSET = 13;
    private const float PANEL_EDGE_Z_OFFSET = PANEL_Z_OFFSET - 2;

    // tg: code/modules/power/solar.dm:40
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        obj_item_solar_assembly S = Dm.arg(__extra, 0) as obj_item_solar_assembly;

        panel_edge = add_panel_overlay("solar_panel_glass_edge", PANEL_EDGE_Z_OFFSET);
        panel = add_panel_overlay("solar_panel_glass", PANEL_Z_OFFSET);

        Make(S);
        // DEVIATION(DV197): a panel starts turned to a bearing of its own (tg's starts at 0), anomaly_start_azimuth of its id, which the
        // client also knows (its own tilt until something turns it, anomaly_start_tilt)
        float start = anomaly_start_azimuth(dm_id);
        panel.transform = get_panel_transform(start);
        panel_edge.transform = get_panel_transform(start);
        azimuth_current = start;
        azimuth_target = start;
        connect_to_network();
        RegisterSignal(SSsun, COMSIG_SUN_MOVED, nameof(queue_update_solar_exposure));
        return dot;
    }

    /// <summary>DV197: the bearing (degrees, hundredths) a new panel with this id starts turned to; a hash of the id, the same on the server and the client.</summary>
    public static float anomaly_start_azimuth(long id) => (float)(anomaly_start_hash(id) % 36000) / 100f;

    /// <summary>DV197: the tilt (degrees from flat, 0 to 75: its face never lower than 15 degrees over the horizon) the client gives a panel still at its start bearing.</summary>
    public static float anomaly_start_tilt(long id) => (float)((anomaly_start_hash(id) >> 32) % 7501) / 100f;

    // SplitMix64's finalizer
    private static ulong anomaly_start_hash(long id)
    {
        ulong z = (ulong)id + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    // tg: code/modules/power/solar.dm:51
    public override object Destroy(float? force = null)
    {
        unset_control(); //remove from control computer
        // QDEL_NULL(panel), QDEL_NULL(panel_edge) (__DEFINES/qdel.dm)
        qdel(panel);
        panel = null;
        qdel(panel_edge);
        panel_edge = null;
        return base.Destroy(force);
    }

    // tg: code/modules/power/solar.dm:57
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (Dm.truthy(same_z_layer))
            return;
        SET_PLANE(panel_edge, Dm.num(PLANE_TO_TRUE(panel_edge.plane)), new_turf);
        SET_PLANE(panel, Dm.num(PLANE_TO_TRUE(panel.plane)), new_turf);
    }

    // tg: code/modules/power/solar.dm:69
    public virtual obj_effect_overlay_solar_panel add_panel_overlay(string icon_state, float z_offset)
    {
        obj_effect_overlay_solar_panel overlay = Dm.@new<obj_effect_overlay_solar_panel>(this);
        overlay.icon_state = icon_state;
        SET_PLANE_EXPLICIT(overlay, ABOVE_GAME_PLANE, this);
        overlay.pixel_z = z_offset;
        Dm.append(vis_contents, overlay);
        return overlay;
    }

    // tg: code/modules/power/solar.dm:77
    public override object should_have_node()
    {
        return TRUE;
    }

    //set the control of the panel to a given computer
    // tg: code/modules/power/solar.dm:81
    public virtual void set_control(obj_machinery_power_solar_control SC = null)
    {
        unset_control();
        control = SC;
        Dm.append(ref SC.connected_panels, this);
        SC.total_capacity = Dm.add(SC.total_capacity, SOLAR_GEN_RATE * power_tier);
        queue_turn(SC.azimuth_target);
    }

    //set the control of the panel to null and removes it from the control list of the previous control computer if needed
    // tg: code/modules/power/solar.dm:89
    public virtual void unset_control()
    {
        if (control != null)
        {
            Dm.remove(ref control.connected_panels, this);
            control.total_capacity = Dm.sub(control.total_capacity, SOLAR_GEN_RATE * power_tier);
            control = null;
        }
    }

    // tg: code/modules/power/solar.dm:96
    public virtual void Make(obj_item_solar_assembly assembly = null)
    {
        if (assembly == null)
        {
            assembly = Dm.@new<obj_item_solar_assembly>(this);
            assembly.glass_type = typeof(obj_item_stack_sheet_glass);
            Waiting.NoSleep(assembly.set_anchored(TRUE));
        }
        else
            Waiting.NoSleep(assembly.forceMove(this));
    }

    // tg: code/modules/power/solar.dm:104
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await tool.use_tool(this, user, 0)))
        {
            playsound(this.loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            user.visible_message(span_notice(I18n.T("[] takes the glass off [].", user, this)), span_notice(I18n.T("You take the glass off [].", this)));
            await deconstruct(TRUE);
        }
        return TRUE;
    }

    // tg: code/modules/power/solar.dm:111
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        object sw__1 = damage_type;
        if (Dm.eq(sw__1, BRUTE))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
                playsound(loc, Dm.resource("sound/effects/hit_on_shattered_glass.ogg"), 60, TRUE);
            else
                playsound(loc, Dm.resource("sound/effects/glass/glasshit.ogg"), 90, TRUE);
        }
        else if (Dm.eq(sw__1, BURN))
            playsound(loc, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        return null;
    }

    // tg: code/modules/power/solar.dm:122
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = null;
        dot = await base.atom_break(damage_flag);
        if (Dm.truthy(dot))
        {
            playsound(loc, Dm.resource("sound/effects/glass/glassbr3.ogg"), 100, TRUE);
            unset_control();
            // Make sure user can see it's broken
            float new_angle = rand(160, 200);
            visually_turn(new_angle);
            azimuth_current = new_angle;
        }
        return dot;
    }

    // tg: code/modules/power/solar.dm:132
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (Dm.truthy(disassembled))
        {
            obj_item_solar_assembly assembly = Dm.locate<obj_item_solar_assembly>(this);
            if (assembly != null)
            {
                await assembly.forceMove(loc);
                if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
                {
                    Dm.@new(Dm.initial(material_type, (datum_material m) => m.shard_type), get_turf(this));
                    Dm.@new(Dm.initial(material_type, (datum_material m) => m.shard_type), get_turf(this));
                }
                else
                {
                    Dm.@new(Dm.initial(material_type, (datum_material m) => m.sheet_type), get_turf(this));
                    Dm.@new(Dm.initial(material_type, (datum_material m) => m.sheet_type), get_turf(this));
                }
            }
        }
        else
        {
            //When smashed to bits
            playsound(this, SFX_SHATTER, 70, TRUE);

            Dm.@new(Dm.initial(material_type, (datum_material m) => m.shard_type), get_turf(this));
            Dm.@new(Dm.initial(material_type, (datum_material m) => m.shard_type), get_turf(this));
        }
        return null;
    }

    // tg: code/modules/power/solar.dm:153
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        string material_name = (string)Dm.initial(material_type, (datum_material m) => m.name);
        panel.icon_state = Dm.text("solar_panel_[][]", material_name, Dm.truthy(Dm.bitand(machine_stat, BROKEN)) ? "-b" : null);
        panel_edge.icon_state = Dm.text("solar_panel_[][]", material_name, Dm.truthy(Dm.bitand(machine_stat, BROKEN)) ? "-b" : "_edge");
        return dot;
    }

    // tg: code/modules/power/solar.dm:159
    public virtual void queue_turn(float azimuth = 0)
    {
        needs_to_turn = TRUE;
        azimuth_target = azimuth;
    }

    // tg: code/modules/power/solar.dm:163
    [SignalHandler]
    public virtual void queue_update_solar_exposure(datum source = null, object azimuth = null)
    {
        needs_to_update_solar_exposure = TRUE; //updating right away would be wasteful if we're also turning later
    }

    /**
     * Get the 2.5D transform for the panel, given an angle
     * Arguments:
     * * angle - the angle the panel is facing
     */
    // tg: code/modules/power/solar.dm:174
    public virtual matrix get_panel_transform(float angle = 0)
    {
        // 2.5D solar panel works by using a magic combination of transforms
        matrix turner = Dm.matrix();
        // Rotate towards sun
        turner.Turn(angle);
        // "Tilt" the panel in 3D towards East and West
        turner.Shear(0, -0.6f * sin(angle));
        // Make it skinny when facing north (away), fat south
        turner.Scale(1, 0.85f * (cos(angle) * -0.5f + 0.5f) + 0.15f);

        return turner;
    }

    // tg: code/modules/power/solar.dm:186
    public virtual void visually_turn_part(obj_effect_overlay part = null, float angle = 0)
    {
        float mid_azimuth = (azimuth_current + angle) / 2;

        // actually flip to other direction?
        if (abs(angle - azimuth_current) > 180)
            mid_azimuth = SIMPLIFY_DEGREES(mid_azimuth + 180); // REVERSE_ANGLE(mid_azimuth) (__DEFINES/maths.dm:140), expanded inline

        // Split into 2 parts so it doesn't distort on large changes
        animate(part,
            Dm.named("transform", get_panel_transform(mid_azimuth)),
            Dm.named("time", 2.5f * SECONDS), Dm.named("easing", Dm.bitor(CUBIC_EASING, EASE_IN)));
        animate(
            Dm.named("transform", get_panel_transform(angle)),
            Dm.named("time", 2.5f * SECONDS), Dm.named("easing", Dm.bitor(CUBIC_EASING, EASE_OUT)));
    }

    // tg: code/modules/power/solar.dm:204
    public virtual void visually_turn(float angle = 0)
    {
        visually_turn_part(panel, angle);
        visually_turn_part(panel_edge, angle);
    }

    // tg: code/modules/power/solar.dm:208
    public virtual void update_turn()
    {
        needs_to_turn = FALSE;
        if (azimuth_current != azimuth_target)
        {
            visually_turn(azimuth_target);
            azimuth_current = azimuth_target;
            turf sun_turf = get_turf(this);
            obscured = sun_turf == null ? TRUE : Dm.num(sun_turf.is_sunlight_blocked());
            needs_to_update_solar_exposure = TRUE;
        }
    }

    ///calculates the fraction of the sunlight that the panel receives
    // tg: code/modules/power/solar.dm:218
    public virtual object update_solar_exposure()
    {
        object dot = null;
        needs_to_update_solar_exposure = FALSE;
        sunfrac = 0;
        if (Dm.truthy(obscured))
            return 0;

        float sun_azimuth = SSsun.azimuth;
        if (azimuth_current == sun_azimuth) //just a quick optimization for the most frequent case
            dot = 1;
        else
        {
            //dot product of sun and panel -- Lambert's Cosine Law
            dot = cos(azimuth_current - sun_azimuth);
            dot = clamp(round(Dm.num(dot), 0.01f), 0, 1);
        }
        sunfrac = Dm.num(dot);
        return dot;
    }

    // tg: code/modules/power/solar.dm:233
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        // If the turf is sun blocked directly we have no hope of being able to see the sun so don't bother processing
        // On the other hand if the station is blocking the sun we might we might be able to see it later, so check for that (in update_turn)
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || loc == null || HAS_TRAIT(loc, TRAIT_TURF_SUN_BLOCKED))
            return null;

        if (control != null && (powernet == null || control.powernet != powernet))
            unset_control();
        if (Dm.truthy(needs_to_turn))
            update_turn();
        if (Dm.truthy(needs_to_update_solar_exposure))
            update_solar_exposure();
        if (sunfrac <= 0)
            return null;

        float sgen = SOLAR_GEN_RATE * sunfrac * power_tier;
        add_avail(power_to_energy(sgen));
        if (control != null)
            control.gen += sgen;
        return null;
    }
}

//Bit of a hack but this whole type is a hack
// tg: code/modules/power/solar.dm:248
public partial class obj_machinery_power_solar_fake : obj_machinery_power_solar
{
    // tg: code/modules/power/solar.dm:249
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        UnregisterSignal(SSsun, COMSIG_SUN_MOVED);
        return dot;
    }

    // tg: code/modules/power/solar.dm:253
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return PROCESS_KILL;
    }
}

//
// Solar Assembly - For construction of solar arrays.
//

// tg: code/modules/power/solar.dm:260
public partial class obj_item_solar_assembly : obj_item
{
    // tg: code/modules/power/solar.dm:274
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(anchored) && !Dm.truthy(pixel_x) && !Dm.truthy(pixel_y))
            randomise_offset(random_offset);
        return dot;
    }

    // tg: code/modules/power/solar.dm:279
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(tracker) ? "tracker_base" : "sp_base";
        return dot;
    }

    // tg: code/modules/power/solar.dm:283
    public virtual void randomise_offset(float amount = 0)
    {
        pixel_x = base_pixel_x + rand(-amount, amount);
        pixel_y = base_pixel_y + rand(-amount, amount);
    }

    // tg: code/modules/power/solar.dm:287
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = null;
        dot = await base.set_anchored(anchorvalue);
        if (dot == null)
            return dot;
        randomise_offset(Dm.truthy(anchored) ? 0 : random_offset);
        return dot;
    }

    // tg: code/modules/power/solar.dm:293
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        turf solarturf = get_turf(this);

        if (Dm.truthy(tracker))
        {
            //prevent construction if something dense's on our tile
            if (Dm.truthy(await solarturf.is_blocked_turf(TRUE, source_atom: this)))
            {
                balloon_alert(user, I18n.T("something in the way!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!istype(tool, typeof(obj_item_stack_sheet_glass)))
            {
                to_chat(user, span_warning(I18n.T("The tracker only accepts standard, un-reinforced glass.")));
                return ITEM_INTERACT_BLOCKING;
            }
            obj_item_stack_sheet my_sheet = tool as obj_item_stack_sheet;
            if (!Dm.truthy(await my_sheet.use(2)))
            {
                to_chat(user, span_warning(I18n.T("You don't have enough glass to complete the tracker.")));
                return ITEM_INTERACT_BLOCKING;
            }
            playsound(this.loc, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
            user.visible_message(span_notice(I18n.T("[] places the glass on the solar assembly.", user)), span_notice(I18n.T("You place the glass on the solar assembly.")));
            Dm.@new<obj_machinery_power_tracker>(get_turf(this), this);
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_electronics_tracker)))
        {
            if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                return ITEM_INTERACT_BLOCKING;
            tracker = TRUE;
            update_appearance();
            qdel(tool);
            user.visible_message(span_notice(I18n.T("[] inserts the electronics into the solar assembly.", user)), span_notice(I18n.T("You insert the electronics into the solar assembly.")));
            return ITEM_INTERACT_SUCCESS;
        }

        //make a list of all the glass: the typecache of these four types (typecacheof adds their subtypes, istype takes them)
        if (!(istype(tool, typeof(obj_item_stack_sheet_glass)) || istype(tool, typeof(obj_item_stack_sheet_plasmaglass))
            || istype(tool, typeof(obj_item_stack_sheet_titaniumglass)) || istype(tool, typeof(obj_item_stack_sheet_plastitaniumglass))))
        {
            //items that arent used above, or arent usable glass will make it here.
            //so we check if its reinfocred glass, or some other item
            if (istype(tool, typeof(obj_item_stack_sheet_rglass)) || istype(tool, typeof(obj_item_stack_sheet_plasmarglass)))
                to_chat(user, span_warning(I18n.T("The solar array will only accept glass or glass alloys that have not been reinforced.")));
            //an else statement can be put here if you want something to happen to all the misc items that make it this far
            return ITEM_INTERACT_BLOCKING;
        }

        //prevent construction if something dense's on our tile
        if (Dm.truthy(await solarturf.is_blocked_turf(TRUE, source_atom: this)))
        {
            balloon_alert(user, I18n.T("something in the way!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (is_glass_sheet(tool))
        {
            if (!Dm.truthy(anchored))
            {
                to_chat(user, span_warning(I18n.T("You need to secure the assembly before you can add glass.")));
                return ITEM_INTERACT_BLOCKING;
            }

            DmList glass_material_to_tier = Dm.list(
                Dm.kv(typeof(datum_material_glass), 1),
                Dm.kv(typeof(datum_material_alloy_titaniumglass), 2),
                Dm.kv(typeof(datum_material_alloy_plasmaglass), 3),
                Dm.kv(typeof(datum_material_alloy_plastitaniumglass), 4));

            obj_item_stack_sheet my_sheet = tool as obj_item_stack_sheet;
            if (!Dm.truthy(await my_sheet.use(2)))
            {
                to_chat(user, span_warning(I18n.T("You need at least two sheets of glass to complete a solar panel!")));
                return ITEM_INTERACT_BLOCKING;
            }

            Type glass_material = my_sheet.material_type;
            playsound(this.loc, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
            user.visible_message(span_notice(I18n.T("[] places the glass on the solar assembly.", user)), span_notice(I18n.T("You place the glass on the solar assembly.")));
            obj_machinery_power_solar mySolar = Dm.@new<obj_machinery_power_solar>(get_turf(this), this);
            mySolar.power_tier = Dm.num(glass_material_to_tier[glass_material]);
            mySolar.material_type = glass_material;
            string glass_name = (string)Dm.initial(glass_material, (datum_material m) => m.name);
            mySolar.panel.icon_state = Dm.text("solar_panel_[]", glass_name);
            mySolar.panel_edge.icon_state = Dm.text("solar_panel_[]_edge", glass_name);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/power/solar.dm:380
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        turf solarturf = get_turf(this);
        if (!isturf(loc))
            return NONE;
        if (!Dm.truthy(solarturf.can_have_cabling())) //allows catwalks
        {
            balloon_alert(user, I18n.T("can't secure in space!"));
            return ITEM_INTERACT_BLOCKING;
        }
        foreach (obj stuff_in_the_way in Dm.each<obj>(solarturf)) //prevent anchoring on other machinery or solar assemblies
        {
            if (stuff_in_the_way == this)
                continue;
            if (stuff_in_the_way is obj_item_solar_assembly && Dm.truthy(stuff_in_the_way.anchored))
            {
                balloon_alert(user, I18n.T("secured assembly in the way!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(stuff_in_the_way.density) && !Dm.truthy(Dm.bitand(stuff_in_the_way.flags_1, ON_BORDER_1)))
            {
                balloon_alert(user, I18n.T("something in the way!"));
                return ITEM_INTERACT_BLOCKING;
            }
        }
        await set_anchored(Dm.truthy(anchored) ? FALSE : TRUE);
        user.visible_message(
            span_notice(I18n.T("[] []wrenches the solar assembly[].", user, Dm.truthy(anchored) ? null : I18n.T("un"), Dm.truthy(anchored) ? I18n.T(" into place") : null)),
            span_notice(I18n.T("You []wrench the solar assembly[].", Dm.truthy(anchored) ? null : I18n.T("un"), Dm.truthy(anchored) ? I18n.T(" into place") : null)));
        tool.play_tool_sound(this, 75);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/solar.dm:403
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(tracker))
            return NONE;
        Dm.@new<obj_item_electronics_tracker>(this.loc);
        tracker = FALSE;
        update_appearance();
        user.visible_message(span_notice(I18n.T("[] takes out the electronics from the solar assembly.", user)),
                            span_notice(I18n.T("You take out the electronics from the solar assembly.")));
        return ITEM_INTERACT_SUCCESS;
    }
}

//
// Solar Control Computer
//

// tg: code/modules/power/solar.dm:411
public partial class obj_machinery_power_solar_control : obj_machinery_power
{
    // tg: code/modules/power/solar.dm:444
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        RegisterSignal(SSsun, COMSIG_SUN_MOVED, nameof(timed_track));
        connect_to_network();
        if (powernet != null)
            set_panels(azimuth_target);
        azimuth_rate = SSsun.base_rotation;
        record_interval = SSsun.wait;
        history["supply"] = Dm.list();
        history["capacity"] = Dm.list();
        return dot;
    }

    // tg: code/modules/power/solar.dm:456
    public override object Destroy(float? force = null)
    {
        foreach (obj_machinery_power_solar M in Dm.each<obj_machinery_power_solar>(connected_panels))
            M.unset_control();
        if (connected_tracker != null)
            connected_tracker.unset_control();
        return base.Destroy(force);
    }

    //search for unconnected panels and trackers in the computer powernet and connect them
    // tg: code/modules/power/solar.dm:464
    public virtual void search_for_connected()
    {
        if (powernet != null)
        {
            foreach (obj_machinery_power M in Dm.each<obj_machinery_power>(((datum_powernet)powernet).nodes))
            {
                if (istype(M, typeof(obj_machinery_power_solar)))
                {
                    // space vines block out sunlight
                    obj_structure_spacevine vine = Dm.locate<obj_structure_spacevine>(loc);
                    if (vine != null && !Dm.@in(typeof(datum_spacevine_mutation_transparency), vine.mutations))
                        continue;

                    obj_machinery_power_solar S = M as obj_machinery_power_solar;
                    if (S.control == null) //i.e unconnected
                        S.set_control(this);
                }
                else if (istype(M, typeof(obj_machinery_power_tracker)))
                {
                    if (connected_tracker == null) //if there's already a tracker connected to the computer don't add another
                    {
                        obj_machinery_power_tracker T = M as obj_machinery_power_tracker;
                        if (T.control == null) //i.e unconnected
                            T.set_control(this);
                    }
                }
            }
        }
    }

    ///Record the generated power supply and capacity for history
    // tg: code/modules/power/solar.dm:484
    public virtual void record()
    {
        if (record_size == 0)
            record_size = 1 + ROUND_UP(Dm.div(360, azimuth_rate * abs(SSsun.azimuth_mod))); //History contains full sun cycle

        if (Dm.world.time >= next_record)
        {
            next_record = Dm.world.time + record_interval;

            DmList supply = history["supply"] as DmList;
            if (powernet != null)
                Dm.append(ref supply, round(lastgen));
            if (supply.len > record_size)
                supply.Cut(1, 2);

            DmList capacity = history["capacity"] as DmList;
            if (powernet != null)
                Dm.append(ref capacity, total_capacity);
            if (capacity.len > record_size)
                capacity.Cut(1, 2);
        }
    }

    // tg: code/modules/power/solar.dm:502
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_off", icon_keyboard)));
            return dot;
        }

        Dm.append(ref dot, mutable_appearance(icon, icon_keyboard));
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_broken", icon_state)));
            return dot;
        }
        Dm.append(ref dot, mutable_appearance(icon, icon_screen));
        return dot;
    }

    // tg: code/modules/power/solar.dm:514
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "SolarControl", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/power/solar.dm:520
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["supply"] = round(lastgen);
        data["capacity"] = total_capacity;
        data["azimuth_current"] = azimuth_target;
        data["azimuth_rate"] = azimuth_rate;
        data["max_rotation_rate"] = SSsun.base_rotation * 2;
        data["tracking_state"] = track;
        data["connected_panels"] = connected_panels.len;
        data["connected_tracker"] = (connected_tracker != null ? TRUE : FALSE);
        data["history"] = history;
        return data;
    }

    // tg: code/modules/power/solar.dm:532
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.eq(action, "azimuth"))
        {
            float? adjust = text2num(@params["adjust"]);
            float? value = text2num(@params["value"]);
            if (Dm.truthy(adjust))
                value = azimuth_target + adjust;
            if (value != null)
            {
                set_panels(value.Value);
                return TRUE;
            }
            return FALSE;
        }
        if (Dm.eq(action, "azimuth_rate"))
        {
            float? adjust = text2num(@params["adjust"]);
            float? value = text2num(@params["value"]);
            if (Dm.truthy(adjust))
                value = azimuth_rate + adjust;
            if (value != null)
            {
                azimuth_rate = round(clamp(value.Value, -2 * SSsun.base_rotation, 2 * SSsun.base_rotation), 0.01f);
                return TRUE;
            }
            return FALSE;
        }
        if (Dm.eq(action, "tracking"))
        {
            float? mode = text2num(@params["mode"]);
            track = Dm.num(mode);
            if (mode == SOLAR_TRACK_AUTO)
            {
                if (connected_tracker != null)
                    connected_tracker.sun_update(SSsun, SSsun.azimuth);
                else
                    track = SOLAR_TRACK_OFF;
            }
            return TRUE;
        }
        if (Dm.eq(action, "refresh"))
        {
            search_for_connected();
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/power/solar.dm:572
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(tool.item_flags, NOBLUDGEON)))
            return await base.item_interaction(user, tool, modifiers, __extra);
        await attack_hand(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/solar.dm:578
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await tool.use_tool(this, user, 20, volume: 50)))
            return ITEM_INTERACT_BLOCKING;
        obj_structure_frame_computer new_computer = Dm.@new<obj_structure_frame_computer>(this.loc);
        if (Dm.truthy(Dm.bitand(this.machine_stat, BROKEN)))
        {
            to_chat(user, span_notice(I18n.T("The broken glass falls out.")));
            Dm.@new<obj_item_shard>(this.loc);
            new_computer.state = 3;
            new_computer.icon_state = "3";
        }
        else
        {
            to_chat(user, span_notice(I18n.T("You disconnect the monitor.")));
            new_computer.state = 4;
            new_computer.icon_state = "4";
        }
        foreach (obj within in Dm.each<obj>(this))
            await within.forceMove(drop_location() as atom);
        // new_computer.circuit = new /obj/item/circuitboard/computer/solar_control(new_computer): the board instance is circuit_value (GAP(types), as the machine's)
        new_computer.circuit_value = Dm.@new<obj_item_circuitboard_computer_solar_control>(new_computer);
        new_computer.circuit = new_computer.circuit_value.GetType();
        await new_computer.set_anchored(TRUE);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/solar.dm:597
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        object sw__1 = damage_type;
        if (Dm.eq(sw__1, BRUTE))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
                playsound(this.loc, Dm.resource("sound/effects/hit_on_shattered_glass.ogg"), 70, TRUE);
            else
                playsound(this.loc, Dm.resource("sound/effects/glass/glasshit.ogg"), 75, TRUE);
        }
        else if (Dm.eq(sw__1, BURN))
            playsound(this.loc, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        return null;
    }

    // tg: code/modules/power/solar.dm:607
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = null;
        dot = await base.atom_break(damage_flag);
        if (Dm.truthy(dot))
            playsound(loc, Dm.resource("sound/effects/glass/glassbr3.ogg"), 100, TRUE);
        return dot;
    }

    // tg: code/modules/power/solar.dm:612
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        lastgen = gen;
        gen = 0;
        if (connected_tracker != null && (powernet == null || connected_tracker.powernet != powernet))
            connected_tracker.unset_control();
        record();
        return null;
    }

    ///Ran every time the sun updates.
    // tg: code/modules/power/solar.dm:620
    [SignalHandler]
    public virtual void timed_track(datum source = null, object azimuth = null)
    {
        if (track == SOLAR_TRACK_TIMED)
        {
            azimuth_target += azimuth_rate;
            set_panels(azimuth_target);
        }
    }

    ///Rotates the panel to the passed angles
    // tg: code/modules/power/solar.dm:627
    public virtual void set_panels(float azimuth = 0)
    {
        azimuth = clamp(round(azimuth, 0.01f), -360, 719.99f);
        if (azimuth >= 360)
            azimuth -= 360;
        if (azimuth < 0)
            azimuth += 360;
        azimuth_target = azimuth;

        foreach (obj_machinery_power_solar S in Dm.each<obj_machinery_power_solar>(connected_panels))
            S.queue_turn(azimuth);
    }
}
