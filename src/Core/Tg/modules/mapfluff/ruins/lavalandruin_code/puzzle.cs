// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the sliding puzzle (the ancient puzzle ruin's pillars that slide back into their picture, its necropolis chest or
// Bubblegum), the admin's puzzle and the prison cube. The pictures are tg's icons cut and scaled as tg cuts them: the icon operations
// go to the client (DmShim/icon.cs). The var blocks are generated, but the element's source and the prison's prisoner, which the
// generator types as paths.

// tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:2
public partial class obj_effect_sliding_puzzle : obj_effect
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:94
    internal const float COLLAPSE_DURATION = 7;

    //Gets the turf where the tile with given id should be
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:16
    public virtual turf get_turf_for_id(object id = null)
    {
        turf center = get_turf(this);
        switch (Dm.num(id))
        {
            case 1:
                return get_step(center, NORTHWEST);
            case 2:
                return get_step(center, NORTH);
            case 3:
                return get_step(center, NORTHEAST);
            case 4:
                return get_step(center, WEST);
            case 5:
                return center;
            case 6:
                return get_step(center, EAST);
            case 7:
                return get_step(center, SOUTHWEST);
            case 8:
                return get_step(center, SOUTH);
            case 9:
                return get_step(center, SOUTHEAST);
        }
        return null;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:42
    public override void LateInitialize(params object[] __extra)
    {
        if (Dm.truthy(auto_setup))
            setup();
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:46
    public virtual object check_setup_location()
    {
        for (float id = 1; id <= 9; id++)
        {
            turf T = get_turf_for_id(id);
            if (T == null)
                return FALSE;
            if (T is turf_closed_indestructible)
                return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:56
    public virtual void validate()
    {
        if (Dm.truthy(finished))
            return;

        if (elements.len < 8) //Someone broke it
            qdel(this);

        //Check if everything is in place
        for (float id = 1; id <= 9; id++)
        {
            turf target_turf = get_turf_for_id(id);
            var E = Dm.locate<obj_structure_puzzle_element>(target_turf);
            if (Dm.eq(id, empty_tile_id) && E == null) // This location should be empty.
                continue;
            if (E == null || !Dm.eq(E.id, id)) //wrong tile or no tile at all
                return;
        }
        //Ding ding
        finish();
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:76
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(LAZYLEN(elements)))
        {
            foreach (obj_structure_puzzle_element E in Dm.each<obj_structure_puzzle_element>(elements))
                E.source = null;
            elements.Cut();
        }
        return base.Destroy(force);
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:85
    public virtual void finish()
    {
        finished = TRUE;
        foreach (mob M in Dm.each<mob>(range(7, this)))
            shake_camera(M, COLLAPSE_DURATION, 1);
        foreach (obj_structure_puzzle_element E in Dm.each<obj_structure_puzzle_element>(elements))
            E.collapse();

        dispense_reward();
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:94
    public virtual void dispense_reward()
    {
        Dm.@new(reward_type, get_turf(this));
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:97
    public virtual object is_solvable()
    {
        DmList current_ordering = Dm.list();
        foreach (obj_structure_puzzle_element E in Dm.each<obj_structure_puzzle_element>(elements_in_order()))
            Dm.append(ref current_ordering, E.id);

        float swap_tally = 0;
        for (float i = 1; i <= current_ordering.len; i++)
        {
            float checked_value = Dm.num(current_ordering[i]);
            for (float j = i; j <= current_ordering.len; j++)
            {
                if (Dm.num(current_ordering[j]) < checked_value)
                    swap_tally++;
            }
        }

        return Dm.mod(swap_tally, 2) == 0 ? TRUE : FALSE;
    }

    //swap two tiles in same row
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:112
    public virtual void make_solvable()
    {
        float first_tile_id = 1;
        float other_tile_id = 2;
        if (Dm.eq(empty_tile_id, 1) || Dm.eq(empty_tile_id, 2)) //Can't swap with empty one so just grab some in second row
        {
            first_tile_id = 4;
            other_tile_id = 5;
        }

        turf T1 = get_turf_for_id(first_tile_id);
        turf T2 = get_turf_for_id(other_tile_id);

        var E1 = Dm.locate<obj_structure_puzzle_element>(T1);
        var E2 = Dm.locate<obj_structure_puzzle_element>(T2);

        Waiting.NoSleep(E1.forceMove(T2));
        Waiting.NoSleep(E2.forceMove(T1));
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:141
    public virtual DmList elements_in_order()
    {
        return sortTim(elements, cmp: "/proc/" + nameof(cmp_xy_desc)) as DmList;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:144
    public virtual icon get_base_icon()
    {
        // var/icon/I = new('icons/obj/fluff/puzzle.dmi'); var/list/puzzles = icon_states(I): the server reads no .dmi (D104), so the
        // states come from the committed icon state table, as icon_states_fast's
        DmList puzzles = icon_states_fast(Dm.resource("icons/obj/fluff/puzzle.dmi"));
        object puzzle_state = pick(puzzles);
        icon P = Dm.icon(Dm.resource("icons/obj/fluff/puzzle.dmi"), puzzle_state);
        return P;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:151
    public virtual void setup()
    {
        //First we slice the 96x96 icon into 32x32 pieces
        DmList puzzle_pieces = Dm.list(); //id -> icon list

        float width = 3;
        float height = 3;
        DmList left_ids = Dm.list();
        float tile_count = width * height;

        //Generate per tile icons
        icon base_icon = get_base_icon();

        for (float id = 1; id <= tile_count; id++)
        {
            float y = width - Dm.round((id - 1) / width);
            float x = Dm.mod(id - 1, width) + 1;

            float x_start = 1 + (x - 1) * ICON_SIZE_X;
            float x_end = x_start + ICON_SIZE_X - 1;
            float y_start = 1 + ((y - 1) * ICON_SIZE_Y);
            float y_end = y_start + ICON_SIZE_Y - 1;

            icon T = Dm.icon(base_icon);
            T.Crop(x_start, y_start, x_end, y_end);
            puzzle_pieces[Dm.text("[]", id)] = T;
            Dm.append(ref left_ids, id);
        }

        //Setup random empty tile
        empty_tile_id = pick_n_take(left_ids);
        turf empty_tile_turf = get_turf_for_id(empty_tile_id);
        Waiting.NoSleep(empty_tile_turf.place_on_top(floor_type, CHANGETURF_INHERIT_AIR));
        empty_tile_turf = (turf)turf.dm_identity(empty_tile_turf); // TURF-IDENTITY: tg's turf is the tile, now the vault floor
        var MA = Dm.@new<mutable_appearance>(puzzle_pieces[Dm.text("[]", empty_tile_id)]);
        MA.layer = empty_tile_turf.layer + 0.1f;
        empty_tile_turf.add_overlay(MA);

        elements = Dm.list();
        DmList empty_spots = left_ids.Copy();
        foreach (object spot_id in Dm.each(empty_spots))
        {
            turf T = get_turf_for_id(spot_id);
            T = Waiting.NoSleep(T.place_on_top(floor_type, CHANGETURF_INHERIT_AIR)) as turf;
            var E = (obj_structure_puzzle_element)Dm.@new(element_type, T);
            Dm.append(ref elements, E);
            object chosen_id = pick_n_take(left_ids);
            E.puzzle_icon = puzzle_pieces[Dm.text("[]", chosen_id)] as icon;
            E.source = this;
            E.id = Dm.num(chosen_id);
            E.set_puzzle_icon();
        }

        if (!Dm.truthy(is_solvable()))
            make_solvable();
    }
}

// tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:198
public partial class obj_structure_puzzle_element : obj_structure
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:205 (the generator types the var as a path)
    public obj_effect_sliding_puzzle source;

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:198 (the var block, hand-written with the source's var)
    public obj_structure_puzzle_element()
    {
        name = "mysterious pillar";
        desc = "Puzzling...";
        icon = Dm.resource("icons/obj/mining_zones/artefacts.dmi");
        icon_state = "puzzle_pillar";
        anchored = FALSE;
        density = TRUE;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:208
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        if (!isturf(newloc) || Dm.truthy(moving_diagonally) || get_dist(get_step(this, Dm.num(direct)), get_turf(source)) > 1)
            return 0;
        return await base.Move(newloc, direct, glide_size_override, update_dir);
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:214
    public virtual void set_puzzle_icon()
    {
        cut_overlays();
        if (puzzle_icon != null)
        {
            //Need to scale it down a bit to fit the static border
            icon C = Dm.icon(puzzle_icon);
            C.Scale(19, 19);
            var puzzle_small = Dm.@new<mutable_appearance>(C);
            puzzle_small.layer = layer + 0.1f;
            puzzle_small.pixel_w = 7;
            puzzle_small.pixel_z = 7;
            add_overlay(puzzle_small);
        }
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:227
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra) // to prevent update_appearance calls from cutting the overlays and not adding them back
    {
        updates ??= ALL;
        object dot = await base.update_icon(updates, __extra);
        set_puzzle_icon();
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:231
    public override object Destroy(float? force = null)
    {
        if (source != null)
        {
            Dm.remove(ref source.elements, this);
            source.validate();
        }
        return base.Destroy(force);
    }

    //Set the full image on the turf and delete yourself
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:238
    public virtual void collapse()
    {
        turf T = get_turf(this);
        var MA = Dm.@new<mutable_appearance>(puzzle_icon);
        MA.layer = T.layer + 0.1f;
        T.add_overlay(MA);
        //Some basic shaking animation
        for (float i = 1; i <= obj_effect_sliding_puzzle.COLLAPSE_DURATION; i++)
            animate(this, Dm.named("pixel_x", Dm.rand(-5, 5)), Dm.named("pixel_y", Dm.rand(-2, 2)), Dm.named("time", 1));
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this), obj_effect_sliding_puzzle.COLLAPSE_DURATION, file: "code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm", line: 246); // QDEL_IN(src, COLLAPSE_DURATION)
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:248
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        source?.validate();
        return dot;
    }
}

//Admin abuse version so you can pick the icon before it sets up
// tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:254
public partial class obj_effect_sliding_puzzle_admin : obj_effect_sliding_puzzle
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:259
    public override icon get_base_icon()
    {
        icon I = Dm.icon(puzzle_icon, puzzle_state);
        return I;
    }
}

//Ruin version
// tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:264
public partial class obj_effect_sliding_puzzle_lavaland : obj_effect_sliding_puzzle
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:267
    public override void dispense_reward()
    {
        if (prob(25))
        {
            //If it's not roaming somewhere else already.
            var B = Dm.locate<mob_living_simple_animal_hostile_megafauna_bubblegum>(GLOB.mob_list);
            if (B == null)
                reward_type = typeof(mob_living_simple_animal_hostile_megafauna_bubblegum);
        }
        base.dispense_reward();
    }
}

//Prison cube version
// tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:276
public partial class obj_effect_sliding_puzzle_prison : obj_effect_sliding_puzzle
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:277 (the generator types the var as a path)
    public mob_living prisoner;

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:276 (the var block, hand-written with the prisoner's var)
    public obj_effect_sliding_puzzle_prison()
    {
        auto_setup = FALSE; //This will be done by cube proc
        element_type = typeof(obj_structure_puzzle_element_prison);
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:281
    public override icon get_base_icon()
    {
        if (prisoner == null)
            throw Dm.CRASH("Prison cube without prisoner", "code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm", 283);
        Waiting.NoSleep(prisoner.setDir(SOUTH));
        // DEVIATION(DV637): tg's getFlatIcon(prisoner), the prisoner's whole look flattened into one picture on the server; the server
        // draws no icons (D104), so the cube's picture is the prisoner's own icon and state, facing south (as DV500's showpieces)
        icon I = Dm.icon(prisoner.icon, prisoner.icon_state, SOUTH);
        I.Scale(96, 96);
        return I;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:289
    public override object Destroy(float? force = null)
    {
        if (prisoner != null)
        {
            to_chat(prisoner, span_userdanger(I18n.T("With the cube broken by force, you can feel your body falling apart.")));
            prisoner.investigate_log("has died from their prison puzzle being destroyed.", INVESTIGATE_DEATHS);
            Waiting.NoSleep(prisoner.death());
            qdel(prisoner);
        }
        return base.Destroy(force);
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:297
    public override void dispense_reward()
    {
        Waiting.NoSleep(prisoner.forceMove(get_turf(this)));
        REMOVE_TRAIT(prisoner, TRAIT_NO_TRANSFORM, element_type);
        prisoner = null;
    }
}

// tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:303
public partial class obj_structure_puzzle_element_prison : obj_structure_puzzle_element
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:315
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:318
public partial class obj_item_prisoncube : obj_item
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:324
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return NONE;

        var carbon_victim = interacting_with as mob_living_carbon;
        //Handcuffed or unconscious
        if (carbon_victim != null && (Dm.truthy(carbon_victim.handcuffed) || IS_UNCONSCIOUS_OR_CRIT(carbon_victim)))
        {
            await user.do_attack_animation(carbon_victim);
            if (!Dm.truthy(await puzzle_imprison(carbon_victim)))
            {
                to_chat(user, span_warning(I18n.T("[] does nothing.", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            to_chat(user, span_warning(I18n.T("You trap [] in the prison cube!", carbon_victim)));
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }

        to_chat(user, span_notice(I18n.T("[] only accepts restrained or unconscious prisoners.", this)));
        return ITEM_INTERACT_BLOCKING;
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:128
    public static object cmp_xy_desc(atom_movable A = null, atom_movable B = null)
    {
        if (A.y > B.y)
            return -1;
        if (A.y < B.y)
            return 1;
        if (A.x > B.x)
            return 1;
        if (A.x < B.x)
            return -1;
        return 0;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm:339
    public static async DmTask<object> puzzle_imprison(mob_living prisoner = null)
    {
        turf T = get_turf(prisoner);
        var cube = Dm.@new<obj_effect_sliding_puzzle_prison>(T);
        if (!Dm.truthy(cube.check_setup_location()))
        {
            qdel(cube);
            return FALSE;
        }

        //First grab the prisoner and move them temporarily into the generator so they won't get thrown around.
        ADD_TRAIT(prisoner, TRAIT_NO_TRANSFORM, cube.element_type);
        await prisoner.forceMove(cube);
        to_chat(prisoner, span_userdanger(I18n.T("You're trapped by the prison cube! You will remain trapped until someone solves it.")));

        //Clear the area from objects (and cube user)
        DmList things_to_throw = Dm.list();
        foreach (atom_movable AM in Dm.each<atom_movable>(range(1, T)))
        {
            if (!Dm.truthy(AM.anchored))
                Dm.append(ref things_to_throw, AM);
        }

        foreach (atom_movable AM in Dm.each<atom_movable>(things_to_throw))
        {
            turf throwtarget = get_edge_target_turf(T, get_dir(T, get_step_away(AM, T))) as turf;
            Waiting.NoSleep(AM.throw_at(throwtarget, 2, 3));
        }

        //Create puzzle itself
        cube.prisoner = prisoner;
        cube.setup();

        //Move them into random block
        var E = (obj_structure_puzzle_element)pick(cube.elements);
        await prisoner.forceMove(E);
        return TRUE;
    }
}
