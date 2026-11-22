// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/pipecleaners.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 power helpers slice (D164): the whole file, which the rapid pipe cleaner layer (items/rcd/RCL.dm) lays. "This is the old
// cable code, but minus any actual powernet logic": a pipe cleaner is a decorative wire on the floor with no network. The var
// block is generated (D111). Left deferred (not-translated.md): the investigate log of pulling one up. Left in live-partial.txt:
// nothing. The cyborg coil's energy storage (/datum/robot_energy_storage/pipe_cleaner) is var-only (the cyborgs' storages: silicons SI2).

//This is the old cable code, but minus any actual powernet logic
//Wireart is fun

///////////////////////////////
//CABLE STRUCTURE
///////////////////////////////


////////////////////////////////
// Definitions
////////////////////////////////

/* Cable directions (d1 and d2)
 * 9   1   5
 * \ | /
 * 8 - 0 - 4
 * / | \
 * 10  2   6

If d1 = 0 and d2 = 0, there's no pipe_cleaner
If d1 = 0 and d2 = dir, it's a O-X pipe_cleaner, getting from the center of the tile to dir (knot pipe_cleaner)
If d1 = dir1 and d2 = dir2, it's a full X-X pipe_cleaner, getting from dir1 to dir2
By design, d1 is the smallest direction and d2 is the highest
*/

// tg: code/modules/power/pipecleaners.dm:26
public partial class obj_structure_pipe_cleaner : obj_structure
{
    // the power pipe_cleaner object
    // tg: code/modules/power/pipecleaners.dm:79
    [DmParams("mapload", "param_color")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object param_color = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, Dm.forward(__extra, param_color));

        // ensure d1 & d2 reflect the icon_state for entering and exiting pipe_cleaner
        float dash = findtext(icon_state, "-");
        d1 = Dm.num(text2num(copytext(icon_state, 1, dash)));
        d2 = Dm.num(text2num(copytext(icon_state, dash + length(copytext(icon_state, dash, dash + 1)))));

        if (Dm.truthy(d1))
            stored = Dm.@new<obj_item_stack_pipe_cleaner_coil>(null, 2, null, null, null);
        else
            stored = Dm.@new<obj_item_stack_pipe_cleaner_coil>(null, 1, null, null, null);

        if (!Dm.truthy(param_color))
            param_color = "white";
        color = GLOB.cable_colors[param_color];
        pipecleaner_color = (string)param_color;
        stored?.set_pipecleaner_color(pipecleaner_color);
        update_appearance();

        if (isturf(loc))
        {
            turf turf_loc = loc as turf;
            turf_loc.add_blueprints_preround(this);
        }
        return dot;
    }

    // tg: code/modules/power/pipecleaners.dm:103
    public override object Destroy(float? force = null) // called when a pipe_cleaner is deleted
    {
        //If we have a stored item at this point, lets just delete it, since that should be
        //handled by deconstruction
        if (stored != null)
            QDEL_NULL(ref stored);
        return base.Destroy(force); // then go ahead and delete the pipe_cleaner
    }

    // tg: code/modules/power/pipecleaners.dm:110
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        turf location = get_turf(loc);
        if (Dm.truthy(location))
        {
            await stored.forceMove(location);
            stored = null;
        }
        else
            qdel(stored);
    }

    ///////////////////////////////////
    // General procedures
    ///////////////////////////////////

    // tg: code/modules/power/pipecleaners.dm:122
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]-[]", d1, d2);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/power/pipecleaners.dm:126
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        await add_atom_colour(color, FIXED_COLOUR_PRIORITY);
        return dot;
    }

    // Items usable on a pipe_cleaner :
    //   - Wirecutters : cut it duh !
    //   - pipe cleaner coil : merge pipe cleaners
    //
    // tg: code/modules/power/pipecleaners.dm:134
    public virtual async DmTask<object> handlecable(obj_item tool = null, mob user = null, object @params = null)
    {
        add_fingerprint(user);
        if (!istype(tool, typeof(obj_item_stack_pipe_cleaner_coil)))
            return ITEM_INTERACT_BLOCKING;
        obj_item_stack_pipe_cleaner_coil coil = tool as obj_item_stack_pipe_cleaner_coil;
        if (Dm.num(coil.get_amount()) < 1)
        {
            to_chat(user, span_warning(I18n.T("Not enough pipe cleaner!")));
            return ITEM_INTERACT_BLOCKING;
        }
        await coil.pipe_cleaner_join(this, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/pipecleaners.dm:145
    public virtual async DmTask cut_pipe_cleaner(mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] pulls up the pipe cleaner.", user)), span_notice(I18n.T("You pull up the pipe cleaner.")));
        stored.add_fingerprint(user);
        investigate_log(Dm.text("was pulled up by [] in []", key_name(usr), AREACOORD(this)), INVESTIGATE_WIRES);
        await deconstruct();
    }

    // tg: code/modules/power/pipecleaners.dm:151
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        return await handlecable(tool, user, modifiers);
    }

    // tg: code/modules/power/pipecleaners.dm:154
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        await cut_pipe_cleaner(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/pipecleaners.dm:158
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await base.singularity_pull(singularity, current_size);
        if (Dm.num(current_size) >= STAGE_FIVE)
            await deconstruct();
        return null;
    }

    // tg: code/modules/power/pipecleaners.dm:163
    public virtual void update_stored(float? length = null, object colorC = null)
    {
        length ??= 1;
        colorC ??= COLOR_RED;
        stored.amount = length;
        stored.color = colorC;
        stored.update_appearance();
    }

    // tg: code/modules/power/pipecleaners.dm:168
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await cut_pipe_cleaner(user);
        return CLICK_ACTION_SUCCESS;
    }
}

///////////////////////////////////////////////
// The pipe cleaner coil object, used for laying pipe cleaner
///////////////////////////////////////////////

////////////////////////////////
// Definitions
////////////////////////////////

// tg: code/modules/power/pipecleaners.dm:180
[DmPath("/obj/item/stack/pipe_cleaner_coil")]
public partial class obj_item_stack_pipe_cleaner_coil : obj_item_stack
{
    // tg: code/modules/power/pipecleaners.dm:212
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_copper), 2));
    }

    //Sets the pipe cleaner coil's colour
    // tg: code/modules/power/pipecleaners.dm:243
    public virtual void set_pipecleaner_color(object new_color = null)
    {
        color = GLOB.cable_colors[new_color];
        pipecleaner_color = (string)new_color;
        update_appearance();
    }

    // tg: code/modules/power/pipecleaners.dm:248
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (Dm.truthy(locate_type(typeof(obj_structure_chair_stool), get_turf(user))))
            user.visible_message(span_suicide(I18n.T("[] is making a noose with []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        else
            user.visible_message(span_suicide(I18n.T("[] is strangling []self with []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        return OXYLOSS;
    }

    // tg: code/modules/power/pipecleaners.dm:255
    [DmParams("mapload", "new_amount", "mat_override", "mat_amt", "param_color")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_amount = Dm.arg(__extra, 0);
        DmList mat_override = Dm.arg<DmList>(__extra, 1);
        float? mat_amt = Dm.arg<float?>(__extra, 2);
        object param_color = Dm.arg(__extra, 3);
        mat_amt ??= 1;
        // `..()` passes the current arguments in order, so the stack's merge slot gets mat_override, its mat_override slot mat_amt
        // and its mat_amt slot param_color, as in tg (stack.dm:68)
        object dot = base.Initialize(mapload, Dm.forward(__extra, new_amount, mat_override, mat_amt, param_color));

        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        if (Dm.truthy(param_color))
            set_pipecleaner_color(param_color);
        if (!Dm.truthy(color))
        {
            DmList pipe_cleaner_colors = GLOB.cable_colors;
            object random_color = pick(pipe_cleaner_colors);
            set_pipecleaner_color(random_color);
        }

        pixel_x = base_pixel_x + rand(-2, 2);
        pixel_y = base_pixel_y + rand(-2, 2);
        update_appearance();
        return dot;
    }

    ///////////////////////////////////
    // General procedures
    ///////////////////////////////////

    // tg: code/modules/power/pipecleaners.dm:274
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        name = I18n.T("pipe cleaner []", (Dm.num(amount) < 3) ? I18n.T("piece") : I18n.T("coil"));
        return dot;
    }

    // tg: code/modules/power/pipecleaners.dm:278
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", Dm.initial(this, (atom a) => a.icon_state), Dm.num(amount) < 3 ? amount : "");
        inhand_icon_state = Dm.text("coil_[]", pipecleaner_color);
        return dot;
    }

    // tg: code/modules/power/pipecleaners.dm:283
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        await add_atom_colour(color, FIXED_COLOUR_PRIORITY);
        return dot;
    }

    // tg: code/modules/power/pipecleaners.dm:287
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(dot))
            return dot;
        obj_item_stack_pipe_cleaner_coil new_pipe_cleaner = dot as obj_item_stack_pipe_cleaner_coil;
        if (istype(new_pipe_cleaner, typeof(obj_item_stack_pipe_cleaner_coil)))
            new_pipe_cleaner.set_pipecleaner_color(pipecleaner_color);
        return dot;
    }

    //add pipe_cleaners to the stack
    // tg: code/modules/power/pipecleaners.dm:296
    public virtual void give(object extra = null)
    {
        if (Dm.num(amount) + Dm.num(extra) > max_amount)
            amount = max_amount;
        else
            amount = Dm.num(amount) + Dm.num(extra);
        update_appearance();
    }

    ///////////////////////////////////////////////
    // Cable laying procedures
    //////////////////////////////////////////////

    // tg: code/modules/power/pipecleaners.dm:307
    public virtual object get_new_pipe_cleaner(object location = null)
    {
        return Dm.@new<obj_structure_pipe_cleaner>(location, pipecleaner_color);
    }

    // called when pipe_cleaner_coil is clicked on a turf
    // tg: code/modules/power/pipecleaners.dm:311
    public virtual async DmTask<object> place_turf(turf T = null, mob user = null, object dirnew = null)
    {
        if (!isturf(user.loc))
            return null;

        if (!isturf(T) || !Dm.truthy(T.can_have_cabling()))
        {
            to_chat(user, span_warning(I18n.T("You can only lay pipe cleaners on a solid floor!")));
            return null;
        }

        if (Dm.num(get_amount()) < 1) // Out of pipe_cleaner
        {
            to_chat(user, span_warning(I18n.T("There is no pipe cleaner left!")));
            return null;
        }

        if (get_dist(T, user) > 1) // Too far
        {
            to_chat(user, span_warning(I18n.T("You can't lay pipe cleaner at a place that far away!")));
            return null;
        }

        object dirn;
        if (!Dm.truthy(dirnew)) //If we weren't given a direction, come up with one! (Called as null from catwalk.dm and floor.dm)
        {
            if (user.loc == T)
                dirn = user.dir; //If laying on the tile we're on, lay in the direction we're facing
            else
                dirn = get_dir(T, user);
        }
        else
            dirn = dirnew;

        foreach (obj_structure_pipe_cleaner LC in Dm.each<obj_structure_pipe_cleaner>(T))
        {
            if (Dm.eq(LC.d2, dirn) && LC.d1 == 0)
            {
                to_chat(user, span_warning(I18n.T("There's already a pipe cleaner at that position!")));
                return null;
            }
        }

        obj_structure_pipe_cleaner C = get_new_pipe_cleaner(T) as obj_structure_pipe_cleaner;

        //set up the new pipe_cleaner
        C.d1 = 0; //it's a O-X node pipe_cleaner
        C.d2 = Dm.num(dirn);
        C.add_fingerprint(user);
        C.update_appearance();

        await use(1);

        return C;
    }

    // called when pipe_cleaner_coil is click on an installed obj/pipe_cleaner
    // or click on a turf that already contains a "node" pipe_cleaner
    // tg: code/modules/power/pipecleaners.dm:355
    public virtual async DmTask<object> pipe_cleaner_join(obj_structure_pipe_cleaner C = null, mob user = null, float? showerror = null, object forceddir = null)
    {
        showerror ??= TRUE;
        turf U = user.loc as turf;
        if (!isturf(U))
            return null;

        turf T = C.loc as turf;

        if (!isturf(T)) // sanity check
            return null;

        if (get_dist(C, user) > 1) // make sure it's close enough
        {
            to_chat(user, span_warning(I18n.T("You can't lay pipe cleaner at a place that far away!")));
            return null;
        }


        if (U == T && !Dm.truthy(forceddir)) //if clicked on the turf we're standing on and a direction wasn't supplied, try to put a pipe_cleaner in the direction we're facing
        {
            await place_turf(T, user);
            return null;
        }

        object dirn = get_dir(C, user);
        if (Dm.truthy(forceddir))
            dirn = forceddir;

        // one end of the clicked pipe_cleaner is pointing towards us and no direction was supplied
        if ((Dm.eq(C.d1, dirn) || Dm.eq(C.d2, dirn)) && !Dm.truthy(forceddir))
        {
            if (!Dm.truthy(U.can_have_cabling())) //checking if it's a plating or catwalk
            {
                if (Dm.truthy(showerror))
                    to_chat(user, span_warning(I18n.T("You can only lay pipe cleaners on catwalks and plating!")));
                return null;
            }
            else
            {
                // pipe_cleaner is pointing at us, we're standing on an open tile
                // so create a stub pointing at the clicked pipe_cleaner on our tile

                float fdirn = REVERSE_DIR(Dm.num(dirn)); // the opposite direction

                foreach (obj_structure_pipe_cleaner LC in Dm.each<obj_structure_pipe_cleaner>(U)) // check to make sure there's not a pipe_cleaner there already
                {
                    if (LC.d1 == fdirn || LC.d2 == fdirn)
                    {
                        if (Dm.truthy(showerror))
                            to_chat(user, span_warning(I18n.T("There's already a pipe cleaner at that position!")));
                        return null;
                    }
                }

                obj_structure_pipe_cleaner NC = get_new_pipe_cleaner(U) as obj_structure_pipe_cleaner;

                NC.d1 = 0;
                NC.d2 = fdirn;
                NC.add_fingerprint(user);
                NC.update_appearance();

                await use(1);

                return null;
            }
        }

        // exisiting pipe_cleaner doesn't point at our position or we have a supplied direction, so see if it's a stub
        else if (C.d1 == 0)
        {
            // if so, make it a full pipe_cleaner pointing from its old direction to our dirn
            float nd1 = C.d2; // these will be the new directions
            float nd2 = Dm.num(dirn);


            if (nd1 > nd2) // swap directions to match icons/states
            {
                nd1 = Dm.num(dirn);
                nd2 = C.d2;
            }


            foreach (obj_structure_pipe_cleaner LC in Dm.each<obj_structure_pipe_cleaner>(T)) // check to make sure there's no matching pipe_cleaner
            {
                if (LC == C) // skip the pipe_cleaner we're interacting with
                    continue;
                if ((LC.d1 == nd1 && LC.d2 == nd2) || (LC.d1 == nd2 && LC.d2 == nd1)) // make sure no pipe_cleaner matches either direction
                {
                    if (Dm.truthy(showerror))
                        to_chat(user, span_warning(I18n.T("There's already a pipe cleaner at that position!")));

                    return null;
                }
            }


            C.update_appearance();

            C.d1 = nd1;
            C.d2 = nd2;

            //updates the stored pipe_cleaner coil
            C.update_stored(2, color);

            C.add_fingerprint(user);
            C.update_appearance();

            await use(1);

            return null;
        }
        return null;
    }
}

//////////////////////////////
// Misc.
/////////////////////////////

// tg: code/modules/power/pipecleaners.dm:215
public partial class obj_item_stack_pipe_cleaner_coil_cyborg : obj_item_stack_pipe_cleaner_coil
{
    // tg: code/modules/power/pipecleaners.dm:215
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        DmList pipe_cleaner_colors = GLOB.cable_colors;
        DmList possible_colors = Dm.list();
        foreach (object color in Dm.each(pipe_cleaner_colors))
        {
            image pipe_icon = Dm.image(icon: icon, icon_state: icon_state);
            pipe_icon.color = pipe_cleaner_colors[color];
            Dm.append(ref possible_colors, Dm.list(Dm.kv(Dm.text("[]", color), pipe_icon)));
        }

        object selected_color = await show_radial_menu(user, this, possible_colors, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), radius: 40, require_near: TRUE);
        if (!Dm.truthy(selected_color))
            return null;
        set_pipecleaner_color(selected_color);
        return null;
    }

    /**
     * Checks if we are allowed to interact with a radial menu
     *
     * Arguments:
     * * user The mob interacting with the menu
     */
    // tg: code/modules/power/pipecleaners.dm:234
    public virtual object check_menu(mob user = null)
    {
        if (!istype(user, typeof(mob)))
            return FALSE;
        if (!Dm.truthy(user.is_holding(this)))
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        return TRUE;
    }
}

// tg: code/modules/power/pipecleaners.dm:491
public partial class obj_item_stack_pipe_cleaner_coil_cut : obj_item_stack_pipe_cleaner_coil
{
    // tg: code/modules/power/pipecleaners.dm:494
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!Dm.truthy(amount))
            amount = rand(1, 2);
        object dot = base.Initialize(mapload, __extra);
        pixel_x = base_pixel_x + rand(-2, 2);
        pixel_y = base_pixel_y + rand(-2, 2);
        update_appearance();
        return dot;
    }
}
