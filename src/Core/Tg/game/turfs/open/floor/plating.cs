// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/floor/plating.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: /turf/open/floor/plating's states and make_plating (M3 P4, the floors subset), its item_interaction (M4b: the
// extinguisher sprays an adjacent tile; M5 slice 4: rods into an engine floor, tiles, the plating repair tool, plasteel into
// reinforced plating, plastitanium insulation) and welder_act (M5 slice 4: fixing dents); its examine is a generated
// live-partial stub (M9). Gaps G2 (D291): the metal foam and reinforced platings; the planetary ones come with their milestones
// (the engine floor is live, reinforced_floor.cs).

/**
 * PLATINGS
 *
 * Handle interaction with tiles and lets you put stuff on top of it.
 */
// tg: code/game/turfs/open/floor/plating.dm:7
public partial class turf_open_floor_plating : turf_open_floor
{
    // tg: code/game/turfs/open/floor/plating.dm:43  #define PLATE_REINFORCE_COST 2 (#undef at :151)
    private const int PLATE_REINFORCE_COST = 2;

    // tg: code/game/turfs/open/floor/plating.dm:25
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged4");
    }

    // tg: code/game/turfs/open/floor/plating.dm:28
    public override object burnt_states()
    {
        return Dm.list("floorscorched1", "floorscorched2");
    }

    // tg: code/game/turfs/open/floor/plating.dm:31
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(broken) || Dm.truthy(burnt))
        {
            Dm.append(ref dot, span_notice(I18n.T("It looks like the dents could be <i>welded</i> smooth.")));
            return dot;
        }
        if (Dm.truthy(attachment_holes))
            Dm.append(ref dot, span_notice(I18n.T("There are a few attachment holes for a new <i>tile</i> or reinforcement <i>rods</i>.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("You might be able to build ontop of it with some <i>tiles</i>...")));
        if (Dm.truthy(upgradable))
            Dm.append(ref dot, span_notice(I18n.T("You could probably make this plating more resilient with some plasteel.")));
        return dot;
    }

    // tg: code/game/turfs/open/floor/plating.dm:45  (M4b: an item used on plating, the extinguisher's spray at an adjacent
    // tile; every branch is construction, M5)
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(ITEM_INTERACT_ANY_BLOCKER, dot)))
            return dot;

        if (istype(tool, typeof(obj_item_stack_rods)) && Dm.truthy(attachment_holes))
        {
            if (Dm.truthy(broken) || Dm.truthy(burnt))
            {
                to_chat(user, span_warning(I18n.T("Repair the plating first! Use a welding tool[] to fix the damage.", iscyborg(user) ? " or a plating repair tool" : "")));
                return ITEM_INTERACT_BLOCKING;
            }

            obj_item_stack_rods material = tool as obj_item_stack_rods;
            if (Dm.num(material.get_amount()) < 2)
            {
                to_chat(user, span_warning(I18n.T("You need two rods to make a reinforced floor!")));
                return ITEM_INTERACT_BLOCKING;
            }

            to_chat(user, span_notice(I18n.T("You begin reinforcing the floor...")));
            if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;

            // TURF-IDENTITY: istype(src, /turf/open/floor/engine) asks what the turf here is now; a replaced src is no longer
            // this plating
            if (Dm.num(material.get_amount()) < 2 || QDELETED(this) || istype(this, typeof(turf_open_floor_engine)))
                return ITEM_INTERACT_BLOCKING;

            object placed = await place_on_top(typeof(turf_open_floor_engine), flags: CHANGETURF_INHERIT_AIR);
            playsound((placed ?? this) as atom, Dm.resource("sound/items/deconstruct.ogg"), 80, TRUE); // TURF-IDENTITY: src is the new floor
            await material.use(2);
            to_chat(user, span_notice(I18n.T("You reinforce the floor.")));
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_stack_tile)))
        {
            if (Dm.truthy(broken) || Dm.truthy(burnt))
            {
                balloon_alert(user, I18n.T("too damaged, use a welding[] tool!", iscyborg(user) ? "or plating repair " : ""));
                return ITEM_INTERACT_BLOCKING;
            }

            foreach (obj blocker in Dm.each<obj>(this))
            {
                foreach (mob sitter in Dm.each<mob>(blocker.buckled_mobs))
                {
                    to_chat(user, span_warning(I18n.T("Someone is buckled to \\the []! Unbuckle [] to move [] out of the way.", blocker, sitter, sitter.p_them())));
                    return ITEM_INTERACT_BLOCKING;
                }
            }

            obj_item_stack_tile tile = tool as obj_item_stack_tile;
            await tile.place_tile(this, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_cautery_prt))) //plating repair tool
        {
            if ((!Dm.truthy(broken) && !Dm.truthy(burnt)) || !Dm.truthy(await tool.use_tool(this, user, 0, volume: 80)))
                return ITEM_INTERACT_BLOCKING;

            to_chat(user, span_danger(I18n.T("You fix some dents on the broken plating.")));
            icon_state = (string)base_icon_state;
            burnt = FALSE;
            broken = FALSE;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_stack_sheet_plasteel)) && Dm.truthy(upgradable)) //Reinforcement!
        {
            if (Dm.truthy(broken) || Dm.truthy(burnt))
            {
                balloon_alert(user, I18n.T("too damaged, use a welding[] tool!", iscyborg(user) ? "or plating repair " : ""));
                return ITEM_INTERACT_BLOCKING;
            }

            obj_item_stack_sheet sheets = tool as obj_item_stack_sheet;
            if (Dm.num(sheets.get_amount()) < PLATE_REINFORCE_COST)
                return ITEM_INTERACT_BLOCKING;

            balloon_alert(user, I18n.T("reinforcing plating..."));
            if (!Dm.truthy(await do_after(user, 12 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;
            // TURF-IDENTITY: as above
            if (Dm.num(sheets.get_amount()) < PLATE_REINFORCE_COST || QDELETED(this) || istype(this, typeof(turf_open_floor_plating_reinforced)))
                return ITEM_INTERACT_BLOCKING;
            await sheets.use(PLATE_REINFORCE_COST);
            playsound(this, Dm.resource("sound/machines/creak.ogg"), 100, vary: TRUE);
            await place_on_top(typeof(turf_open_floor_plating_reinforced), CHANGETURF_INHERIT_AIR);
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_stack_sheet_mineral_plastitanium)) && Dm.truthy(attachment_holes))
        {
            if (Dm.truthy(broken) || Dm.truthy(burnt))
            {
                to_chat(user, span_warning(I18n.T("Repair the plating first! Use a welding tool[] to fix the damage.", iscyborg(user) ? " or a plating repair tool" : "")));
                return ITEM_INTERACT_BLOCKING;
            }

            obj_item_stack_sheet_mineral_plastitanium sheet = tool as obj_item_stack_sheet_mineral_plastitanium;
            if (Dm.num(sheet.get_amount()) < 1)
            {
                to_chat(user, span_warning(I18n.T("You are literally holding nothing."))); // finally a reasonable message
                return ITEM_INTERACT_BLOCKING;
            }

            balloon_alert(user, I18n.T("insulating flooring..."));
            if (!Dm.truthy(await do_after(user, 1.5f * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;

            // TURF-IDENTITY: as above
            if (Dm.num(sheet.get_amount()) < 1 || QDELETED(this) || istype(this, typeof(turf_open_floor_engine_insulation)))
                return ITEM_INTERACT_BLOCKING;

            object placed = await place_on_top(typeof(turf_open_floor_engine_insulation), flags: CHANGETURF_INHERIT_AIR);
            playsound((placed ?? this) as atom, Dm.resource("sound/items/deconstruct.ogg"), 80, TRUE); // TURF-IDENTITY: src is the new floor
            await sheet.use(1);
            to_chat(user, span_notice(I18n.T("You insulate the floor.")));
            balloon_alert(user, I18n.T("insulated!"));
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/game/turfs/open/floor/plating.dm:141
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if ((!Dm.truthy(broken) && !Dm.truthy(burnt)) || !Dm.truthy(await tool.use_tool(this, user, 0, volume: 80)))
            return NONE;
        to_chat(user, span_danger(I18n.T("You fix some dents on the broken plating.")));
        icon_state = (string)base_icon_state;
        burnt = FALSE;
        broken = FALSE;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/turfs/open/floor/plating.dm:155
    public override async DmTask<object> make_plating(float? force = null)
    {
        force ??= FALSE;
        return null;
    }
}

// Gaps G2 (D291): the metal foam plating and the reinforced plating (plating.dm:157-352) turn live.

// tg: code/game/turfs/open/floor/plating.dm:157
public partial class turf_open_floor_plating_foam : turf_open_floor_plating
{
    // tg: code/game/turfs/open/floor/plating.dm:165
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_WELDER, TOOL_ACT_PRIMARY));
        return dot;
    }

    // tg: code/game/turfs/open/floor/plating.dm:169
    public override object burn_tile()
    {
        return null; //jetfuel can't melt steel foam
    }

    // tg: code/game/turfs/open/floor/plating.dm:172
    public override object break_tile()
    {
        return null; //jetfuel can't break steel foam...
    }

    // tg: code/game/turfs/open/floor/plating.dm:175
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!ismetaltile(tool))
            return NONE;

        obj_item_stack_tile tiles = tool as obj_item_stack_tile;
        if (!Dm.truthy(await tiles.use(1)))
            return ITEM_INTERACT_BLOCKING;
        obj lattice = Dm.locate<obj_structure_lattice>(this);
        if (Dm.truthy(lattice))
            qdel(lattice);
        to_chat(user, span_notice(I18n.T("You reinforce the foamed plating with tiling.")));
        playsound(this, Dm.resource("sound/items/weapons/Genhit.ogg"), 50, TRUE);
        await ChangeTurf(typeof(turf_open_floor_plating), flags: CHANGETURF_INHERIT_AIR);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/turfs/open/floor/plating.dm:190
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        playsound(this, Dm.resource("sound/items/weapons/tap.ogg"), 100, TRUE); //The attack sound is muffled by the foam itself
        user.changeNext_move(CLICK_CD_MELEE);
        await user.do_attack_animation(this);
        if (prob(Dm.num(attacking_item.force) * 20 - 25))
        {
            user.visible_message(span_danger(Dm.text(I18n.T("[] smashes through []!"), user, this)),
                span_danger(Dm.text(I18n.T("You smash through [] with []!"), this, attacking_item)));
            await ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
        }
        else
            to_chat(user, span_danger(Dm.text(I18n.T("You hit [], to no effect!"), this)));
        return null;
    }

    // tg: code/game/turfs/open/floor/plating.dm:201
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_TURF) && Dm.eq(the_rcd.rcd_design_path, typeof(turf_open_floor_plating_rcd)))
            return Dm.list(Dm.kv("delay", 0), Dm.kv("cost", 1));
        return null;
    }

    // tg: code/game/turfs/open/floor/plating.dm:205
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        if (Dm.eq(rcd_data[RCD_DESIGN_MODE], RCD_TURF) && Dm.eq(rcd_data[RCD_DESIGN_PATH], typeof(turf_open_floor_plating_rcd)))
        {
            await ChangeTurf(typeof(turf_open_floor_plating), flags: CHANGETURF_INHERIT_AIR);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/turfs/open/floor/plating.dm:211  (set waitfor = FALSE, inherited from /atom/ex_act: the wrapper of
    // waiting-model.md 5.4; ScrapeAway may sleep)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(ex_act__body(severity, target, __extra, dot), dot);
    }

    private async DmTask<object> ex_act__body(object severity, object target, object[] __extra, DotBox<object> dot)
    {
        dot.Value = base.ex_act(severity, target, __extra);
        // TURF-IDENTITY: the base's floor ex_act may have replaced this turf already; tg's src is the turf at the cell
        turf src = locate(x, y, z) as turf;
        if (src != null)
            await src.ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
        return TRUE;
    }
}

//reinforced plating deconstruction states
// tg: code/game/turfs/open/floor/plating.dm:222
public partial class turf_open_floor_plating_reinforced : turf_open_floor_plating
{
    // tg: code/game/turfs/open/floor/plating.dm:218-220  #define PLATE_INTACT 0, PLATE_BOLTS_LOOSENED 1, PLATE_CUT 2 (#undef at :353)
    private const int PLATE_INTACT = 0, PLATE_BOLTS_LOOSENED = 1, PLATE_CUT = 2;

    // tg: code/game/turfs/open/floor/plating.dm:237
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        Dm.append(ref dot, await base.examine(user, __extra));
        Dm.append(ref dot, deconstruction_hints(user));
        return dot;
    }

    // tg: code/game/turfs/open/floor/plating.dm:241
    public virtual object deconstruction_hints(mob user = null)
    {
        switch (Dm.num(deconstruction_state))
        {
            case PLATE_INTACT:
                return span_notice(I18n.T("The plating reinforcements are securely <b>bolted</b> in place."));
            case PLATE_BOLTS_LOOSENED:
                return span_notice(I18n.T("The plating reinforcement is <i>unscrewed</i> but <b>welded</b> firmly to the plating."));
            case PLATE_CUT:
                return span_notice(I18n.T("The plating reinforcements have been <i>sliced through</i> but are still <b>loosely</b> held in place."));
        }
        return null;
    }

    // tg: code/game/turfs/open/floor/plating.dm:250
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("r_plate-[]", deconstruction_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/turfs/open/floor/plating.dm:254
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        user.changeNext_move(CLICK_CD_MELEE);
        if (!ISADVANCEDTOOLUSER(user))
        {
            to_chat(user, span_warning(I18n.T("You don't have the dexterity to do this!")));
            return ITEM_INTERACT_BLOCKING;
        }

        //get the user's location
        if (!isturf(user.loc))
            return ITEM_INTERACT_BLOCKING; //can't do this stuff whilst inside objects and such

        add_fingerprint(user);

        if (Dm.truthy(await deconstruct_steps(tool, user)))
            return ITEM_INTERACT_SUCCESS;

        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/game/turfs/open/floor/plating.dm:271
    public virtual async DmTask<object> deconstruct_steps(obj_item tool_used = null, mob user = null)
    {
        // TURF-IDENTITY: after each wait tg's istype(src) check reads the turf now at this cell
        float cell_x = x, cell_y = y, cell_z = z;
        switch (Dm.num(deconstruction_state))
        {
            case PLATE_INTACT:
                if (Dm.eq(tool_used.tool_behaviour, TOOL_WRENCH))
                {
                    balloon_alert(user, I18n.T("loosening bolts..."));
                    if (Dm.truthy(await tool_used.use_tool(this, user as mob_living, 10 * SECONDS, volume: 100)))
                    {
                        turf_open_floor_plating_reinforced src = locate(cell_x, cell_y, cell_z) as turf_open_floor_plating_reinforced;
                        if (src == null || !Dm.eq(src.deconstruction_state, PLATE_INTACT))
                            return TRUE;
                        src.deconstruction_state = PLATE_BOLTS_LOOSENED;
                        src.update_appearance(UPDATE_ICON);
                        src.drop_screws();
                        src.balloon_alert(user, I18n.T("removed bolts"));
                    }
                    return TRUE;
                }
                break;

            case PLATE_BOLTS_LOOSENED:
            {
                object sw__1 = tool_used.tool_behaviour;
                if (Dm.eq(sw__1, TOOL_WELDER))
                {
                    if (!Dm.truthy(tool_used.tool_start_check(user as mob_living, amount: 3)))
                        return null;
                    balloon_alert(user, I18n.T("slicing..."));
                    if (Dm.truthy(await tool_used.use_tool(this, user as mob_living, 15 * SECONDS, volume: 100)))
                    {
                        turf_open_floor_plating_reinforced src = locate(cell_x, cell_y, cell_z) as turf_open_floor_plating_reinforced;
                        if (src == null || !Dm.eq(src.deconstruction_state, PLATE_BOLTS_LOOSENED))
                            return TRUE;
                        src.deconstruction_state = PLATE_CUT;
                        src.update_appearance(UPDATE_ICON);
                        src.balloon_alert(user, I18n.T("sliced through"));
                    }
                    return TRUE;
                }
                if (Dm.eq(sw__1, TOOL_SCREWDRIVER))
                {
                    balloon_alert(user, I18n.T("securing bolts..."));
                    if (Dm.truthy(await tool_used.use_tool(this, user as mob_living, 15 * SECONDS, volume: 100)))
                    {
                        turf_open_floor_plating_reinforced src = locate(cell_x, cell_y, cell_z) as turf_open_floor_plating_reinforced;
                        if (src == null || !Dm.eq(src.deconstruction_state, PLATE_BOLTS_LOOSENED))
                            return TRUE;
                        src.deconstruction_state = PLATE_INTACT;
                        src.update_appearance(UPDATE_ICON);
                        src.balloon_alert(user, I18n.T("secured"));
                    }
                    return TRUE;
                }
                return FALSE;
            }

            case PLATE_CUT:
            {
                object sw__2 = tool_used.tool_behaviour;
                if (Dm.eq(sw__2, TOOL_CROWBAR))
                {
                    balloon_alert(user, I18n.T("prying off..."));
                    if (Dm.truthy(await tool_used.use_tool(this, user as mob_living, 20 * SECONDS, volume: 100)))
                    {
                        turf_open_floor_plating_reinforced src = locate(cell_x, cell_y, cell_z) as turf_open_floor_plating_reinforced;
                        if (src == null || !Dm.eq(src.deconstruction_state, PLATE_CUT))
                            return TRUE;
                        src.balloon_alert(user, I18n.T("pried off"));
                        Dm.@new<obj_item_stack_sheet_plasteel>(src, 2);
                        await src.ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
                    }
                    return TRUE;
                }
                if (Dm.eq(sw__2, TOOL_WELDER))
                {
                    if (!Dm.truthy(tool_used.tool_start_check(user as mob_living, amount: 3)))
                        return null;
                    balloon_alert(user, I18n.T("welding back on..."));
                    if (Dm.truthy(await tool_used.use_tool(this, user as mob_living, 15 * SECONDS, volume: 100)))
                    {
                        turf_open_floor_plating_reinforced src = locate(cell_x, cell_y, cell_z) as turf_open_floor_plating_reinforced;
                        if (src == null || !Dm.eq(src.deconstruction_state, PLATE_CUT))
                            return TRUE;
                        src.deconstruction_state = PLATE_BOLTS_LOOSENED;
                        src.update_appearance(UPDATE_ICON);
                        src.balloon_alert(user, I18n.T("welded back on"));
                    }
                    return TRUE;
                }
                return FALSE;
            }
        }
        return FALSE;
    }

    // tg: code/game/turfs/open/floor/plating.dm:336
    public virtual void drop_screws() //When you start dismantling R-Plates they'll drop their bolts on the Z-level below, a little visible warning.
    {
        turf below_turf = get_step_multiz(this, DOWN) as turf;
        while (istype(below_turf, typeof(turf_open_openspace)))
            below_turf = get_step_multiz(below_turf, DOWN) as turf;
        if (below_turf != null && !isspaceturf(below_turf))
        {
            Dm.@new<obj_effect_decal_cleanable_glass_plastitanium_screws>(below_turf);
            playsound(this, Dm.resource("sound/effects/structure_stress/pop3.ogg"), 100, vary: TRUE);
        }
    }
}
