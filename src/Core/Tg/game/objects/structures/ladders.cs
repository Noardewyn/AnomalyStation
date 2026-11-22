// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/ladders.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D325 (multi-z): the whole file. The station map's blips are not ported (the minimap, as the mechs' blips); the athletics skill
// came with gaps SL1 (D333). The ladder hole's filters and render target are tg's picture of the
// hole in the floor above; the 3D client draws the hole from the turf's transparency.

// tg: code/game/objects/structures/ladders.dm:2
public partial class obj_structure_ladder : obj_structure
{
    // tg: code/game/objects/structures/ladders.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_structure_ladder up = Dm.arg<obj_structure_ladder>(__extra, 0);
        obj_structure_ladder down = Dm.arg<obj_structure_ladder>(__extra, 1);
        base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.ladders, this);
        if (up != null)
            link_up(up);
        if (down != null)
            link_down(down);

        register_context();
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/objects/structures/ladders.dm:31
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (up != null)
            context[SCREENTIP_CONTEXT_LMB] = "Climb up";
        if (down != null)
            context[SCREENTIP_CONTEXT_RMB] = "Climb down";
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/objects/structures/ladders.dm:38
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("[] it to start moving up; [] to start moving down.", EXAMINE_HINT(I18n.T("Left-click")), EXAMINE_HINT(I18n.T("Right-click")))));
        return dot;
    }

    // tg: code/game/objects/structures/ladders.dm:42
    public override object Destroy(float? force = null)
    {
        Dm.remove(GLOB.ladders, this);
        disconnect();
        return base.Destroy(force);
    }

    /// Trait source applied by ladder holes
    // tg: code/game/objects/structures/ladders.dm:48  #define SOURCE_LADDER(ladder) "ladder_[REF(ladder)]" (file-local, rules 11)
    internal static string SOURCE_LADDER(object ladder) => Dm.text("ladder_[]", REF(ladder));

    /// Makes the base of the ladder transparent
    // tg: code/game/objects/structures/ladders.dm:132
    public virtual void make_base_transparent()
    {
        if (!Dm.truthy(SSmapping.level_trait(z, ZTRAIT_DOWN))) // Ladders which are actually teleporting you to another z level
            return;
        base_pixel_z = Dm.num(Dm.initial(this, nameof(base_pixel_z))) + 12;
        pixel_z = base_pixel_z;
        Dm.@new<obj_effect_abstract_ladder_hole>(loc, this);
    }

    /// Clears any ladder holes created by this ladder
    // tg: code/game/objects/structures/ladders.dm:140
    public virtual void clear_base_transparency()
    {
        base_pixel_z = Dm.num(Dm.initial(this, nameof(base_pixel_z)));
        pixel_z = base_pixel_z;
        foreach (obj_effect_abstract_ladder_hole hole in Dm.each<obj_effect_abstract_ladder_hole>(loc))
        {
            if (hole.ladder == this)
                qdel(hole);
        }
    }

    /// Links this ladder to passed ladder (which should generally be below it)
    // tg: code/game/objects/structures/ladders.dm:150
    public virtual void link_down(obj_structure_ladder down_ladder = null)
    {
        if (down != null)
            return;

        down = down_ladder;
        down_ladder.up = this;
        down_ladder.update_appearance(UPDATE_ICON_STATE);
        update_appearance(UPDATE_ICON_STATE);
        make_base_transparent();
    }

    /// Unlinks this ladder from the ladder below it.
    // tg: code/game/objects/structures/ladders.dm:161
    public virtual void unlink_down()
    {
        if (down == null)
            return;

        down.up = null;
        down.update_appearance(UPDATE_ICON_STATE);
        down.update_minimap_blip();
        down = null;
        update_appearance(UPDATE_ICON_STATE);
        clear_base_transparency();
    }

    /// Links this ladder to passed ladder (which should generally be above it)
    // tg: code/game/objects/structures/ladders.dm:173
    public virtual void link_up(obj_structure_ladder up_ladder = null)
    {
        if (up != null)
            return;

        up = up_ladder;
        up_ladder.down = this;
        up_ladder.make_base_transparent();
        up_ladder.update_appearance(UPDATE_ICON_STATE);
        update_appearance(UPDATE_ICON_STATE);
    }

    /// Unlinks this ladder from the ladder above it.
    // tg: code/game/objects/structures/ladders.dm:184
    public virtual void unlink_up()
    {
        if (up == null)
            return;

        up.down = null;
        up.clear_base_transparency();
        up.update_appearance(UPDATE_ICON_STATE);
        up.update_minimap_blip();
        up = null;
        update_appearance(UPDATE_ICON_STATE);
    }

    /// Helper to unlink everything
    // tg: code/game/objects/structures/ladders.dm:196
    public virtual void disconnect()
    {
        unlink_down();
        unlink_up();
    }

    // tg: code/game/objects/structures/ladders.dm:200
    public virtual void update_minimap_blip()
    {
        // remove_minimap_blip(MINIMAP_LADDER_BLIP, src); if(up || down) add_minimap_blip(src, MINIMAP_LADDER_BLIP, "ladder"):
        // the station map's blip, the minimap is not ported
    }

    // tg: code/game/objects/structures/ladders.dm:205
    public override void LateInitialize(params object[] __extra)
    {
        // By default, discover ladders above and below us vertically
        turf @base = get_turf(this);

        if (down == null)
        {
            obj_structure_ladder new_down = Dm.locate<obj_structure_ladder>(GET_TURF_BELOW(@base));
            if (new_down != null && crafted == new_down.crafted)
                link_down(new_down);
        }

        if (up == null)
        {
            obj_structure_ladder new_up = Dm.locate<obj_structure_ladder>(GET_TURF_ABOVE(@base));
            if (new_up != null && crafted == new_up.crafted)
                link_up(new_up);
        }

        // Linking updates our icon, so if we failed both links we need a manual update
        if (down == null && up == null)
            update_appearance(UPDATE_ICON_STATE);
        update_minimap_blip();
    }

    // tg: code/game/objects/structures/ladders.dm:224
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][][]", base_icon_state, up != null ? 1 : 0, down != null ? 1 : 0);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/ladders.dm:228
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        if (!Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
        {
            visible_message(span_danger(I18n.T("[] is torn to pieces by the gravitational pull!", this)));
            qdel(this);
        }
        return null;
    }

    // tg: code/game/objects/structures/ladders.dm:233
    public virtual async DmTask use(mob user = null, float? going_up = null)
    {
        going_up ??= TRUE;
        if (!in_range(this, user) || Dm.truthy(DOING_INTERACTION(user, DOAFTER_SOURCE_CLIMBING_LADDER)))
            return;

        if (up == null && down == null)
        {
            balloon_alert(user, I18n.T("doesn't lead anywhere!"));
            return;
        }
        if (Dm.truthy(going_up) ? up == null : down == null)
        {
            balloon_alert(user, I18n.T("can't go any further []", Dm.truthy(going_up) ? "up" : "down"));
            return;
        }
        if (user.buckled != null && Dm.truthy(user.buckled.anchored))
        {
            balloon_alert(user, I18n.T("buckled to something anchored!"));
            return;
        }
        if (Dm.truthy(travel_time))
            Waiting.Detach(start_travelling(user, going_up));
        else
            await travel(user, going_up);
        add_fingerprint(user);
    }

    // tg: code/game/objects/structures/ladders.dm:251
    public virtual async DmTask start_travelling(mob user = null, object going_up = null)
    {
        show_initial_fluff_message(user, going_up);

        // Our climbers athletics ability
        object fitness_level = user.mind?.get_skill_level(typeof(datum_skill_athletics));

        // Misc bonuses to the climb speed.
        float misc_multiplier = 1;

        if (user.get_organ_slot(ORGAN_SLOT_SPINE) is obj_item_organ_cyberimp_chest_spine potential_spine)
            misc_multiplier *= potential_spine.athletics_boost_multiplier;

        float final_travel_time = Dm.num(Dm.sub(travel_time, fitness_level)) * misc_multiplier;

        // DEVIATION(DV371): the climb is announced to the clients, which draw the climber going up or down the ladder over the do_after
        if ((Dm.truthy(going_up) ? up : down) != null)
            LevelRides.Start(user, Dm.truthy(going_up) ? 1 : -1, final_travel_time);
        bool climbed = Dm.truthy(await do_after(user, final_travel_time, target: this, interaction_key: DOAFTER_SOURCE_CLIMBING_LADDER));
        LevelRides.End(user);
        if (climbed)
            await travel(user, Dm.num(going_up), grant_exp: TRUE);
    }

    /// The message shown when the player starts climbing the ladder
    // tg: code/game/objects/structures/ladders.dm:270
    public virtual void show_initial_fluff_message(mob user = null, object going_up = null)
    {
        string up_down = Dm.truthy(going_up) ? "up" : "down";
        user.balloon_alert_to_viewers(I18n.T("climbing []...", up_down));
    }

    // tg: code/game/objects/structures/ladders.dm:274
    public virtual async DmTask travel(mob user = null, float? going_up = null, float? is_ghost = null, float? grant_exp = null)
    {
        going_up ??= TRUE;
        is_ghost ??= FALSE;
        grant_exp ??= FALSE;
        obj_structure_ladder ladder = Dm.truthy(going_up) ? up : down;
        if (ladder == null)
        {
            balloon_alert(user, I18n.T("there's nothing that way!"));
            return;
        }
        object response = user.SEND_SIGNAL(COMSIG_LADDER_TRAVEL, Dm.list(user, this, ladder, going_up));
        if (Dm.truthy(Dm.bitand(response, LADDER_TRAVEL_BLOCK)))
            return;

        turf target = get_turf(ladder);
        await user.zMove(target: target, z_move_flags: ZMOVE_CHECK_PULLEDBY | ZMOVE_ALLOW_BUCKLED | ZMOVE_INCLUDE_PULLED);

        if (Dm.truthy(grant_exp))
        {
            object fitness_level = user.mind?.get_skill_level(typeof(datum_skill_athletics));
            user.mind?.adjust_experience(typeof(datum_skill_athletics), round(Dm.num(Dm.div(ATHLETICS_SKILL_MISC_EXP, (fitness_level is var or__1 && Dm.truthy(or__1) ? or__1 : 1))), 1)); //get a little experience for our trouble
        }

        if (!Dm.truthy(is_ghost))
            show_final_fluff_message(user, ladder, going_up);

        // to avoid having players hunt for the pixels of a ladder that goes through several stories and is
        // partially covered by the sprites of their mobs, a radial menu will be displayed over them.
        // this way players can keep climbing up or down with ease until they reach an end.
        // DEVIATION(DV396): no menu after the climb: in 3D the ladder stands before the climber at the arrival (DV397), who climbs on with a left
        // (up) or right (down) click on it (the owner, 2026-11-11). ghost_use's menu for a ghost's click stays.
        // if (ladder.up != null && ladder.down != null) await ladder.show_options(user, is_ghost);
    }

    /// The messages shown after the player has finished climbing. Players can see this happen from either src or the destination so we've 2 POVs here
    // tg: code/game/objects/structures/ladders.dm:301
    public virtual void show_final_fluff_message(mob user = null, obj_structure_ladder destination = null, object going_up = null)
    {
        string up_down = Dm.truthy(going_up) ? "up" : "down";

        //POV of players around the source
        visible_message(span_notice(I18n.T("[] climbs [] [].", user, up_down, this)));
        //POV of players around the destination
        user.balloon_alert_to_viewers(I18n.T("climbed []", up_down));
    }

    /// Shows a radial menu that players can use to climb up and down a stair.
    // tg: code/game/objects/structures/ladders.dm:310
    public virtual async DmTask show_options(mob user = null, float? is_ghost = null)
    {
        is_ghost ??= FALSE;
        DmList tool_list = Dm.list();
        tool_list["Up"] = image(Dm.resource("icons/testing/turf_analysis.dmi"), icon_state: "red_arrow", dir: NORTH);
        tool_list["Down"] = image(Dm.resource("icons/testing/turf_analysis.dmi"), icon_state: "red_arrow", dir: SOUTH);

        datum_callback check_menu = null;
        if (!Dm.truthy(is_ghost))
            check_menu = Dm.@new<datum_callback>(this, nameof(obj_structure_ladder.check_menu), user);
        object result = await show_radial_menu(user, this, tool_list, custom_check: check_menu, require_near: Dm.num(!Dm.truthy(is_ghost)), tooltips: TRUE);

        float going_up;
        switch (result)
        {
            case "Up":
                going_up = TRUE;
                break;
            case "Down":
                going_up = FALSE;
                break;
            default:
                return;
        }

        if (Dm.truthy(is_ghost) || !Dm.truthy(travel_time))
            await travel(user, going_up, is_ghost);
        else
            Waiting.Detach(start_travelling(user, going_up));
    }

    // tg: code/game/objects/structures/ladders.dm:335
    public virtual async DmTask<object> check_menu(mob user = null, object is_ghost = null)
    {
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(await user.Adjacent(this)))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/structures/ladders.dm:340
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        await use(user);
        return dot;
    }

    // tg: code/game/objects/structures/ladders.dm:346
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.num(dot) == SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN)
            return dot;
        await use(user, going_up: FALSE);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    //Not be called when right clicking as a monkey. attack_hand_secondary() handles that.
    // tg: code/game/objects/structures/ladders.dm:354
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        await use(user);
        return TRUE;
    }

    // tg: code/game/objects/structures/ladders.dm:358
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        await use(user);
        return TRUE;
    }

    // tg: code/game/objects/structures/ladders.dm:362
    public override async DmTask<object> attack_alien_secondary(mob_living_carbon_alien user = null, DmList modifiers = null)
    {
        object dot = await base.attack_alien_secondary(user, modifiers);
        if (Dm.num(dot) == SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN)
            return dot;
        await use(user, going_up: FALSE);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/structures/ladders.dm:369
    public override async DmTask<object> attack_larva(mob user = null, DmList modifiers = null)
    {
        await use(user);
        return TRUE;
    }

    // tg: code/game/objects/structures/ladders.dm:373
    public override async DmTask<object> attack_larva_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_larva_secondary(user, modifiers);
        if (Dm.num(dot) == SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN)
            return dot;
        await use(user, going_up: FALSE);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/structures/ladders.dm:380
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        await use(user);
        return TRUE;
    }

    // tg: code/game/objects/structures/ladders.dm:384
    public override async DmTask<object> attack_animal_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_animal_secondary(user, modifiers);
        if (Dm.num(dot) == SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN)
            return dot;
        await use(user, going_up: FALSE);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/structures/ladders.dm:391
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;
        await use(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/ladders.dm:397
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;
        await use(user, going_up: FALSE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/ladders.dm:403
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        if (Dm.truthy(await user.Adjacent(this)))
            await use(user);
        return TRUE;
    }

    // tg: code/game/objects/structures/ladders.dm:408
    public override async DmTask<object> attack_robot_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_robot_secondary(user, modifiers);
        if (Dm.num(dot) == SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN || !Dm.truthy(await user.Adjacent(this)))
            return dot;
        await use(user, going_up: FALSE);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/structures/ladders.dm:415
    public override async DmTask<object> attack_pai(mob user = null, DmList modifiers = null)
    {
        await use(user);
        return TRUE;
    }

    // tg: code/game/objects/structures/ladders.dm:419
    public override async DmTask<object> attack_pai_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_pai_secondary(user, modifiers);
        if (Dm.num(dot) == SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN)
            return dot;
        await use(user, going_up: FALSE);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    //ATTACK GHOST IGNORING PARENT RETURN VALUE
    // tg: code/game/objects/structures/ladders.dm:427
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        await ghost_use(user);
        return await base.attack_ghost(user);
    }

    ///Ghosts use the byond default popup menu function on right click, so this is going to work a little differently for them.
    // tg: code/game/objects/structures/ladders.dm:432
    public virtual async DmTask ghost_use(mob user = null)
    {
        if (up == null && down == null)
        {
            balloon_alert(user, I18n.T("doesn't lead anywhere!"));
            return;
        }
        if (up == null) //only goes down
            await travel(user, going_up: FALSE, is_ghost: TRUE);
        else if (down == null) //only goes up
            await travel(user, going_up: TRUE, is_ghost: TRUE);
        else //goes both ways
            await show_options(user, is_ghost: TRUE);
    }
}

/// Abstract object used to represent a hole in the floor created by a ladder
// tg: code/game/objects/structures/ladders.dm:51
public partial class obj_effect_abstract_ladder_hole : obj_effect_abstract
{
    // tg: code/game/objects/structures/ladders.dm:59
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_structure_ladder ladder = Dm.arg<obj_structure_ladder>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (ladder == null || !isopenturf(loc) || isopenspaceturf(loc))
            return INITIALIZE_HINT_QDEL;
        foreach (obj_effect_abstract_ladder_hole hole in Dm.each<obj_effect_abstract_ladder_hole>(loc))
        {
            if (hole.ladder == ladder)
                return INITIALIZE_HINT_QDEL;
        }

        this.ladder = ladder;
        RegisterSignal(ladder, COMSIG_QDELETING, nameof(cleanup));

        icon = ladder.icon;
        icon_state = Dm.text("[]_hole", ladder.base_icon_state);
        render_target = Dm.text("*[]", obj_structure_ladder.SOURCE_LADDER(ladder));

        ADD_KEEP_TOGETHER(loc, obj_structure_ladder.SOURCE_LADDER(ladder));
        ADD_TURF_TRANSPARENCY((turf)loc, obj_structure_ladder.SOURCE_LADDER(ladder));
        RegisterSignal(loc, COMSIG_TURF_CHANGE, nameof(turf_changing));
        RegisterSignal(loc, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(add_ladder_rim));
        loc.add_filter(obj_structure_ladder.SOURCE_LADDER(ladder), 1, (DmList)alpha_mask_filter(
            x: Dm.num(ladder.pixel_x) + Dm.num(ladder.pixel_w),
            y: Dm.num(ladder.pixel_y) + Dm.num(ladder.pixel_z),
            render_source: Dm.text("*[]", obj_structure_ladder.SOURCE_LADDER(ladder)),
            flags: MASK_INVERSE));
        loc.update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/objects/structures/ladders.dm:87
    public override object Destroy(float? force = null)
    {
        if (ladder == null)
            return base.Destroy(force);

        if (isopenturf(loc))
        {
            UnregisterSignal(loc, Dm.list(COMSIG_TURF_CHANGE, COMSIG_ATOM_UPDATE_OVERLAYS));
            REMOVE_KEEP_TOGETHER(loc, obj_structure_ladder.SOURCE_LADDER(ladder));
            REMOVE_TURF_TRANSPARENCY((turf)loc, obj_structure_ladder.SOURCE_LADDER(ladder));
            loc.remove_filter(obj_structure_ladder.SOURCE_LADDER(ladder));
            loc.update_appearance(UPDATE_OVERLAYS);
        }

        UnregisterSignal(ladder, COMSIG_QDELETING);
        ladder = null;

        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/ladders.dm:104
    [SignalHandler]
    public virtual void cleanup()
    {
        // the ladder will qdel us in its Destroy regardless, when it unlinks
        // this is just an extra layer of safety, in case the ladder gets moved or something
        qdel(this);
    }

    // tg: code/game/objects/structures/ladders.dm:111
    [SignalHandler]
    public virtual void turf_changing(datum source = null, object path = null, object new_baseturfs = null, object flags = null, DmList post_change_callbacks = null)
    {
        Dm.append(ref post_change_callbacks, Dm.@new<datum_callback>(ladder, nameof(obj_structure_ladder.make_base_transparent)));
        qdel(this);
    }

    // tg: code/game/objects/structures/ladders.dm:117
    [SignalHandler]
    public virtual void add_ladder_rim(turf source = null, DmList overlays = null)
    {
        var rim = (mutable_appearance)mutable_appearance(
            icon: ladder.icon,
            icon_state: Dm.text("[]_rim", ladder.base_icon_state),
            layer: TURF_DECAL_LAYER,
            plane: FLOOR_PLANE,
            offset_spokesman: source);
        rim.pixel_w = ladder.pixel_w;
        rim.pixel_x = ladder.pixel_x;
        rim.pixel_y = ladder.pixel_y;
        rim.pixel_z = ladder.pixel_z;

        Dm.append(ref overlays, rim);
    }
}

// Indestructible away mission ladders which link based on a mapped ID and height value rather than X/Y/Z.
// tg: code/game/objects/structures/ladders.dm:445
public partial class obj_structure_ladder_unbreakable : obj_structure_ladder
{
    // tg: code/game/objects/structures/ladders.dm:451
    public override void LateInitialize(params object[] __extra)
    {
        // Override the parent to find ladders based on being height-linked
        if (id == null || (up != null && down != null))
        {
            update_appearance();
            return;
        }

        foreach (obj_structure_ladder_unbreakable unbreakable_ladder in Dm.each<obj_structure_ladder_unbreakable>(GLOB.ladders))
        {
            if (unbreakable_ladder.id != id)
                continue; // not one of our pals
            if (down == null && unbreakable_ladder.height == height - 1)
            {
                down = unbreakable_ladder;
                unbreakable_ladder.up = this;
                unbreakable_ladder.update_appearance();
                if (up != null)
                    break; // break if both our connections are filled
            }
            else if (up == null && unbreakable_ladder.height == height + 1)
            {
                up = unbreakable_ladder;
                unbreakable_ladder.down = this;
                unbreakable_ladder.update_appearance();
                if (down != null)
                    break; // break if both our connections are filled
            }
        }

        update_appearance();
    }
}
