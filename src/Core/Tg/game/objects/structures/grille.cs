// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/grille.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P4: /obj/structure/grille is live. Its init, its icon states, its passage (projectiles may pass) and the atmos hooks
// are translated; M5 slice 1: the heat exposure (atmos_sensitive), the damage (take_damage, play_attack_sound with the grille
// hit sound), breaking (atom_break: the broken grille and its dropped rod) and atom_deconstruct (its rods); M5 slice 5: the
// hand, paw, animal and alien hits, the wirecutters and the screwdriver, rods on a broken grille (repair_grille), glass on a
// grille (the window placing, clear_tile) and a thrown object's hit. The M7 shocks slice: shock, is_shocked and the powered
// grille's lightning sound and load in hitby; the M7 zaps slice: its tesla_zap; E13: rcd_vals and rcd_act (the RCD); gaps ST5:
// the hulk's punch (hulk_damage, attack_hulk). The context tips (add_context) stay a live-partial row (D291). The var block is
// generated.

// tg: code/game/objects/structures/grille.dm:4
public partial class obj_structure_grille : obj_structure
{
    // tg: code/game/objects/structures/grille.dm:377
    public override async DmTask<object> hitby(atom_movable hitting_atom = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        atom_movable AM = hitting_atom;
        if (isobj(AM))
        {
            if (prob(50) && Dm.truthy(anchored) && !Dm.truthy(broken))
            {
                obj O = (obj)AM;
                if (!Dm.eq(O.throwforce, 0)) //don't want to let people spam tesla bolts, this way it will break after time
                {
                    turf T = get_turf(this);
                    if (Dm.truthy(T.overfloor_placed))
                        return FALSE;
                    obj_structure_cable C = T.get_cable_node() as obj_structure_cable;
                    if (Dm.truthy(C))
                    {
                        playsound(this, Dm.resource("sound/effects/magic/lightningshock.ogg"), 100, TRUE, extrarange: 5);
                        await tesla_zap(source: this, zap_range: 3, power: Dm.num(C.newavail()) * 0.01f, cutoff: 1e3f, zap_flags: Dm.bitor(Dm.bitor(Dm.bitor(Dm.bitor(ZAP_MOB_DAMAGE, ZAP_OBJ_DAMAGE), ZAP_MOB_STUN), ZAP_LOW_POWER_GEN), ZAP_ALLOW_DUPLICATES)); //Zap for 1/100 of the amount of power. At a million watts in the grid, it will be as powerful as a tesla revolver shot.
                        C.add_delayedload(Dm.num(C.newavail()) * 0.0375f); // you can gain up to 3.5 via the 4x upgrades power is halved by the pole so thats 2x then 1X then .5X for 3.5x the 3 bounces shock. // What do you mean by this?
                    }
                }
            }
        }
        return await base.hitby(hitting_atom, skipcatch, hitpush, blocked, throwingdatum);
    }

    // tg: code/game/objects/structures/grille.dm:392
    public override object get_dumping_location()
    {
        return null;
    }

    // tg: code/game/objects/structures/grille.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        Dm.Deferred("code/game/objects/structures/grille.dm:34", "Initialize: register_context(): screentip contexts (M10+)");
        return dot;
    }

    // tg: code/game/objects/structures/grille.dm:36
    public override object Destroy(float? force = null)
    {
        update_cable_icons_on_turf(get_turf(this));
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/grille.dm:40
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_flag ??= 0;
        sound_effect ??= 1;
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/grille.dm:44
    public override object update_appearance(float? updates = null)
    {
        object dot = null;
        if (QDELETED(this))
            return dot;
        dot = base.update_appearance(updates);
        if (Dm.truthy(Dm.bitand(updates, UPDATE_SMOOTHING)) && Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
            QUEUE_SMOOTH(this);
        return dot;
    }

    // tg: code/game/objects/structures/grille.dm:51
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(broken))
            icon_state = Dm.text("broken[]", base_icon_state);
        else
            icon_state = Dm.text("[][]", base_icon_state, (Dm.le(Dm.div(atom_integrity, max_integrity), 0.5f) ? (object)Dm.text("50_[]", rand(0, 3)) : null));
        return await base.update_icon_state(__extra);
    }

    /// Max number of unanchored items that will be moved from a tile when attempting to add a window to a grille.
    // tg: code/game/objects/structures/grille.dm:2  #define CLEAR_TILE_MOVE_LIMIT 20 (file-local)
    private const int CLEAR_TILE_MOVE_LIMIT = 20;

    // tg: code/game/objects/structures/grille.dm:58
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
            return dot;
        if (Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("It's secured in place with []. The rods look like they could be [] through.", EXAMINE_HINT(I18n.T("screws")), EXAMINE_HINT(I18n.T("cut")))));
        else
            Dm.append(ref dot, span_notice(I18n.T("The anchoring screws are []. The rods look like they could be [] through.", EXAMINE_HINT(I18n.T("unscrewed")), EXAMINE_HINT(I18n.T("cut")))));
        return dot;
    }

    // tg: code/game/objects/structures/grille.dm:136
    public virtual async DmTask<object> clear_tile(mob user = null)
    {
        turf at_users_feet = get_turf(user);

        float unanchored_items_on_tile = 0;
        obj_item last_item_moved = null;
        foreach (obj_item item_to_move in Dm.each<obj_item>(loc.contents))
        {
            if (!Dm.truthy(item_to_move.anchored))
            {
                if (unanchored_items_on_tile <= CLEAR_TILE_MOVE_LIMIT)
                {
                    await item_to_move.forceMove(at_users_feet);
                    last_item_moved = item_to_move;
                }
                unanchored_items_on_tile++;
            }
        }

        if (!Dm.truthy(unanchored_items_on_tile))
            return TRUE;

        to_chat(user, span_notice(I18n.T("You move [] out of the way.", unanchored_items_on_tile == 1 ? (object)Dm.text("[]", last_item_moved) : "some things")));

        if (unanchored_items_on_tile - CLEAR_TILE_MOVE_LIMIT > 0)
        {
            to_chat(user, span_warning(I18n.T("There's still too much stuff in the way!")));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/game/objects/structures/grille.dm:159
    [DmParams("AM")]
    public override object Bumped(atom_movable AM = null)
    {
        if (!ismob(AM))
            return null;
        mob M = AM as mob;
        Waiting.NoSleep(shock__proc(M as mob_living, 70));
        return null;
    }

    // tg: code/game/objects/structures/grille.dm:165
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_animal(user, modifiers);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(await shock__proc(user as mob_living, 70)) && !QDELETED(this)) //Last hit still shocks but shouldn't deal damage to the grille
            await take_damage(rand(5, 10), BRUTE, MELEE, 1);
        return dot;
    }

    // tg: code/game/objects/structures/grille.dm:172
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/structures/grille.dm:183
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        user.changeNext_move(CLICK_CD_MELEE);
        await user.do_attack_animation(this, ATTACK_EFFECT_KICK);
        user.visible_message(span_warning(I18n.T("[] hits [].", user, this)), null, null, COMBAT_MESSAGE_RANGE);
        log_combat(user, this, "hit");
        if (!Dm.truthy(await shock__proc(user as mob_living, 70)))
            await take_damage(rand(5, 10), BRUTE, MELEE, 1);
        return dot;
    }

    // tg: code/game/objects/structures/grille.dm:194
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        await user.do_attack_animation(this);
        user.changeNext_move(CLICK_CD_MELEE);
        user.visible_message(span_warning(I18n.T("[] mangles [].", user, this)), null, null, COMBAT_MESSAGE_RANGE);
        if (!Dm.truthy(await shock__proc(user, 70)))
            await take_damage(20, BRUTE, MELEE, 1);
        return null;
    }

    // tg: code/game/objects/structures/grille.dm:201
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = null;
        dot = await base.CanAllowThrough(mover, border_dir);
        if (!Dm.truthy(dot) && isprojectile(mover))
            return Dm.num(prob(30));
        return dot;
    }

    // tg: code/game/objects/structures/grille.dm:213
    public override async DmTask<object> wirecutter_act_secondary(mob_living user = null, obj_item tool = null)
    {
        // tg: code/game/objects/structures/grille.dm:214
        add_fingerprint(user);
        if (Dm.truthy(await shock__proc(user, 100)))
            return null;
        tool.play_tool_sound(this, 100);
        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/grille.dm:221
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!isturf(loc))
            return FALSE;
        // tg: code/game/objects/structures/grille.dm:224
        add_fingerprint(user);
        if (Dm.truthy(await shock__proc(user, 90)))
            return FALSE;
        if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 100)))
            return FALSE;
        await set_anchored(Dm.not(anchored));
        user.visible_message(span_notice(I18n.T("[] [] [].", user, Dm.truthy(anchored) ? "fastens" : "unfastens", this)),
            span_notice(Dm.truthy(anchored) ? I18n.T("You fasten [] to the floor.", this) : I18n.T("You unfasten [] from the floor.", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/grille.dm:234
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_stack_rods)) && Dm.truthy(broken))
        {
            if (!Dm.truthy(await do_after(user, 1 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;
            if (Dm.truthy(await shock__proc(user, 90)))
                return ITEM_INTERACT_BLOCKING;
            obj_item_stack_rods grille_to_be = (obj_item_stack_rods)tool;
            user.visible_message(span_notice(I18n.T("[] rebuilds the broken grille.", user)),
                span_notice(I18n.T("You rebuild the broken grille.")));
            repair_grille();
            await grille_to_be.use(1);
            return ITEM_INTERACT_SUCCESS;
        }

        //window placing begin
        if (!Dm.truthy(broken) && (is_glass_sheet(tool) || istype(tool, typeof(obj_item_stack_sheet_bronze))))
        {
            obj_item_stack to_spend = (obj_item_stack)tool;
            if (Dm.lt(to_spend.get_amount(), 2))
            {
                to_chat(user, span_warning(I18n.T("You need at least two sheets of glass for that!")));
                return ITEM_INTERACT_BLOCKING;
            }

            float dir_to_set = SOUTHWEST;
            if (!Dm.truthy(anchored))
            {
                to_chat(user, span_warning(I18n.T("[] needs to be fastened to the floor first!", this)));
                return ITEM_INTERACT_BLOCKING;
            }

            foreach (obj_structure_window competitor in Dm.each<obj_structure_window>(loc))
            {
                to_chat(user, span_warning(I18n.T("There is already a window there!")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await clear_tile(user)))
                return ITEM_INTERACT_BLOCKING;

            to_chat(user, span_notice(I18n.T("You start placing the window...")));
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;

            if (this.loc == null || !Dm.truthy(anchored)) //Grille broken or unanchored while waiting
                return ITEM_INTERACT_BLOCKING;

            foreach (obj_structure_window competitor in Dm.each<obj_structure_window>(loc)) //Another window already installed on grille
                return ITEM_INTERACT_BLOCKING;

            if (!Dm.truthy(await clear_tile(user)))
                return ITEM_INTERACT_BLOCKING;

            obj_structure_window building_window;
            if (istype(tool, typeof(obj_item_stack_sheet_plasmarglass)))
                building_window = Dm.@new<obj_structure_window_reinforced_plasma_fulltile>(drop_location()); //reinforced plasma window
            else if (istype(tool, typeof(obj_item_stack_sheet_plasmaglass)))
                building_window = Dm.@new<obj_structure_window_plasma_fulltile>(drop_location()); //plasma window
            else if (istype(tool, typeof(obj_item_stack_sheet_rglass)))
                building_window = Dm.@new<obj_structure_window_reinforced_fulltile>(drop_location()); //reinforced window
            else if (istype(tool, typeof(obj_item_stack_sheet_titaniumglass)))
                building_window = Dm.@new<obj_structure_window_reinforced_shuttle>(drop_location());
            else if (istype(tool, typeof(obj_item_stack_wall_filling_plastitaniumglass_basic)))
                building_window = Dm.@new<obj_structure_window_reinforced_plasma_plastitanium_windowfill>(drop_location());
            else if (istype(tool, typeof(obj_item_stack_wall_filling_plastitaniumglass_pod)))
                building_window = Dm.@new<obj_structure_window_reinforced_shuttle_survival_pod>(drop_location());
            else if (istype(tool, typeof(obj_item_stack_sheet_plastitaniumglass)))
                building_window = Dm.@new<obj_structure_window_reinforced_plasma_plastitanium>(drop_location());
            else if (istype(tool, typeof(obj_item_stack_sheet_bronze)))
                building_window = Dm.@new<obj_structure_window_bronze_fulltile>(drop_location());
            else if (istype(tool, typeof(obj_item_stack_sheet_runed_metal)))
                building_window = Dm.@new<obj_structure_window_cult_fulltile>(drop_location());
            else
                building_window = Dm.@new<obj_structure_window_fulltile>(drop_location()); //normal window
            await building_window.setDir(dir_to_set);
            await building_window.set_anchored(FALSE);
            building_window.state = 0;
            if (istype(tool, typeof(obj_item_stack_wall_filling)))
                await to_spend.use(1);
            else
                await to_spend.use(2);
            to_chat(user, span_notice(I18n.T("You place [] on [].", to_spend, this)));
            return ITEM_INTERACT_SUCCESS;
        }
        //window placing end

        if (Dm.truthy(Dm.bitand(tool.obj_flags, CONDUCTS_ELECTRICITY)) && Dm.truthy(await shock__proc(user, 70)))
            return ITEM_INTERACT_BLOCKING;

        return NONE;
    }

    // tg: code/game/objects/structures/grille.dm:316
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
        => grille_play_attack_sound(this, damage_amount, damage_type);

    // The body of the grille's play_attack_sound with its src a parameter: tg's wall support calls it on itself
    // (call(src, /obj/structure/grille::play_attack_sound())(arglist(args)), wall_support.dm:70).
    internal static object grille_play_attack_sound(atom src, object damage_amount, string damage_type)
    {
        damage_type ??= BRUTE;
        object sw__1 = damage_type;
        if (Dm.eq(sw__1, BRUTE))
        {
            if (Dm.truthy(damage_amount))
                playsound(src, Dm.resource("sound/effects/grillehit.ogg"), 80, TRUE);
            else
                playsound(src, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
        }
        else if (Dm.eq(sw__1, BURN))
            playsound(src, Dm.resource("sound/items/tools/welder.ogg"), 80, TRUE);
        return null;
    }

    // tg: code/game/objects/structures/grille.dm:327
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        atom rods = Dm.@new(rods_type, drop_location(), rods_amount) as atom; // var/obj/rods
        // tg: code/game/objects/structures/grille.dm:329
        transfer_fingerprints_to(rods);
    }

    // tg: code/game/objects/structures/grille.dm:331
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (Dm.truthy(broken))
            return dot;
        set_density(FALSE);
        atom_integrity = 20;
        broken = TRUE;
        rods_amount = 1;
        atom dropped_rods = Dm.@new(rods_type, drop_location(), rods_amount) as atom; // var/obj/item/dropped_rods
        // tg: code/game/objects/structures/grille.dm:340
        transfer_fingerprints_to(dropped_rods);
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/grille.dm:343
    public virtual object repair_grille()
    {
        if (!Dm.truthy(broken))
            return FALSE;

        set_density(TRUE);
        atom_integrity = max_integrity;
        broken = FALSE;
        rods_amount = 2;
        update_appearance();
        return TRUE;
    }

    // tg: code/game/objects/structures/grille.dm:354  (the M7 shocks slice)
    public override async DmTask<object> shock__proc(mob_living shocking = null, float? chance = null, object shock_source = null, float? siemens_coeff = null)
    {
        chance ??= 100;
        siemens_coeff ??= 1;
        if (!Dm.truthy(anchored) || Dm.truthy(broken)) // anchored/broken grilles are never connected
            return FALSE;
        turf grill_loc = get_turf(this);
        if (Dm.truthy(grill_loc.overfloor_placed))//cant be a floor in the way!
            return FALSE;
        obj_structure_cable grill_cable = grill_loc.get_cable_node() as obj_structure_cable;
        if (grill_cable == null)
            return FALSE;
        shock_source = grill_cable;
        if (!Dm.truthy(await base.shock__proc(shocking, chance, shock_source, siemens_coeff)))
            return FALSE;
        // Shocking hurts the grille (to weaken monkey powersinks)
        if (prob(50))
            await take_damage(1, BURN, FIRE, sound_effect: FALSE);
        return TRUE;
    }

    // tg: code/game/objects/structures/grille.dm:371
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(Dm.gt(exposed_temperature, T0C + 1500) && !Dm.truthy(broken));
    }

    // tg: code/game/objects/structures/grille.dm:374
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        await take_damage(1, BURN, 0, 0);
        return null;
    }

    // tg: code/game/objects/structures/grille.dm:395
    public virtual void temporary_shatter(float? time_to_go = null, float? time_to_return = null)
    {
        Dm.NotTranslated("code/game/objects/structures/grille.dm:395", "temporary_shatter: the dramatic shattering of an antagonist ability (M10+)");
    }

    /// Do some very specific checks to see if we *would* get shocked. Returns TRUE if it's shocked
    // tg: code/game/objects/structures/grille.dm:409
    public virtual object is_shocked()
    {
        turf turf = get_turf(this);
        obj_structure_cable cable = turf.get_cable_node() as obj_structure_cable;
        DmList powernet_info = get_powernet_info_from_source(cable) as DmList;

        if (!Dm.truthy(powernet_info))
            return FALSE;

        datum_powernet powernet = powernet_info["powernet"] as datum_powernet;
        return Dm.not(Dm.not(powernet.get_electrocute_damage()));
    }
}

// tg: code/game/objects/structures/grille.dm:420
public partial class obj_structure_grille_broken : obj_structure_grille
{
    // tg: code/game/objects/structures/grille.dm:426
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(take_damage(max_integrity * 0.6f));
        return dot;
    }
}

// tg: code/game/objects/structures/grille.dm:77 (E13: the RCD)
public partial class obj_structure_grille : obj_structure
{
    // tg: code/game/objects/structures/grille.dm:77
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        object sw__1 = the_rcd.mode;
        if (Dm.eq(sw__1, RCD_DECONSTRUCT))
            return Dm.list(Dm.kv("delay", 2 * SECONDS), Dm.kv("cost", 5));
        else if (Dm.eq(sw__1, RCD_WINDOWGRILLE))
        {
            float cost = 0;
            float delay = 0;

            if (the_rcd.rcd_design_path == typeof(obj_structure_window))
            {
                cost = 4;
                delay = 2 * SECONDS;
            }
            else if (the_rcd.rcd_design_path == typeof(obj_structure_window_reinforced))
            {
                cost = 6;
                delay = 2.5f * SECONDS;
            }
            else if (the_rcd.rcd_design_path == typeof(obj_structure_window_fulltile))
            {
                cost = 8;
                delay = 3 * SECONDS;
            }
            else if (the_rcd.rcd_design_path == typeof(obj_structure_window_reinforced_fulltile))
            {
                cost = 12;
                delay = 4 * SECONDS;
            }
            if (!Dm.truthy(cost))
                return FALSE;

            return rcd_result_with_memory(
                Dm.list(Dm.kv("delay", delay), Dm.kv("cost", cost)),
                get_turf(this), RCD_MEMORY_WINDOWGRILLE);
        }
        return FALSE;
    }

    // tg: code/game/objects/structures/grille.dm:106
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        object sw__1 = rcd_data[RCD_DESIGN_MODE];
        if (Dm.eq(sw__1, RCD_DECONSTRUCT))
        {
            qdel(this);
            return TRUE;
        }
        else if (Dm.eq(sw__1, RCD_WINDOWGRILLE))
        {
            if (!isturf(loc))
                return FALSE;
            turf T = loc as turf;

            if (Dm.truthy(repair_grille()))
                balloon_alert(user, I18n.T("grille rebuilt"));
            if (!Dm.truthy(await clear_tile(user)))
                return FALSE;

            Type window_path = rcd_data[RCD_DESIGN_PATH] as Type;
            if (!Dm.ispath(window_path))
                throw Dm.CRASH(Dm.text("Invalid window path type in RCD: []", window_path), "code/game/objects/structures/grille.dm", 123);

            //checks if its a valid build direction
            if (!Dm.truthy(Dm.initial(window_path, (obj_structure_window w) => w.fulltile)))
            {
                if (!Dm.truthy(valid_build_direction(loc as turf, user.dir, is_fulltile: FALSE)))
                {
                    balloon_alert(user, I18n.T("window already here!"));
                    return FALSE;
                }
            }

            obj_structure_window WD = await Dm.new_async(window_path, T, user.dir) as obj_structure_window;
            await WD.set_anchored(TRUE);
            return TRUE;
        }
        return FALSE;
    }
}

// Gaps package C1 (the AI's pathfinding and movement).
public partial class obj_structure_grille : obj_structure
{
    // tg: code/game/objects/structures/grille.dm:206
    public override object CanAStarPass(object to_dir = null, datum_can_pass_info pass_info = null)
    {
        if (!Dm.truthy(density))
            return TRUE;
        if (Dm.truthy(Dm.bitand(pass_info.pass_flags, PASSGRILLE)))
            return TRUE;
        return FALSE;
    }
}

// Gaps ST5 (D333): the hulk's punch.
public partial class obj_structure_grille : obj_structure
{
    // tg: code/game/objects/structures/grille.dm:175
    public override object hulk_damage()
    {
        return 60;
    }

    // tg: code/game/objects/structures/grille.dm:178
    public override async DmTask<object> attack_hulk(mob_living_carbon_human user = null, params object[] __extra)
    {
        if (Dm.truthy(await shock__proc(user, 70)))
            return null;
        return await base.attack_hulk(user, __extra);
    }
}
