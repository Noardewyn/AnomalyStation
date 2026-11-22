// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/mulebot/mulebot_delivery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:1
public partial class mob_living_basic_bot_mulebot : mob_living_basic_bot
{
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_delivery.dm:1
    public override async DmTask execute_resist()
    {
        await base.execute_resist();
        if (load != null)
            await unload();
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_delivery.dm:6
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == load)
            await unload();
        if (gone == cell)
        {
            await turn_off();
            assign_cell();
            cell = null;
            set_cell_hud();
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_delivery.dm:16
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (arrived is obj_item_stock_parts_power_store_cell && cell == null)
            assign_cell(arrived);
        return dot;
    }

    // mousedrop a crate to load the bot
    // can load anything if hacked
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_delivery.dm:23
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        if (!isliving(user))
            return null;

        if (dropped is not atom_movable atom_to_load || isdead(atom_to_load) || iseyemob(atom_to_load) || atom_to_load is obj_effect_dummy_phased_mob)
            return null;

        await load__proc(atom_to_load);
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_delivery.dm:32
    public override async DmTask post_unbuckle_mob(mob_living unbuckled_mob = null)
    {
        load = null;
        await base.post_unbuckle_mob(unbuckled_mob);
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_delivery.dm:36
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (Dm.truthy(user.incapacitated))
            return null;
        if (load == user)
            await unload();
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_delivery.dm:42
    public override object remove_air(object amount = null) //To prevent riders suffocating
    {
        return loc?.remove_air(amount);
    }

    /// Called to load an atom on the mulebot, which is usually a crate, unless if hacked
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_delivery.dm:46
    public virtual async DmTask load__proc(atom_movable atom_to_load = null)
    {
        if (load != null || Dm.truthy(atom_to_load.anchored))
            return;

        atom prev_loc = atom_to_load.loc;
        if (!isturf(prev_loc)) //To prevent the loading from stuff from someone's inventory or screen icons.
            return;

        obj_structure_closet_crate crate = atom_to_load as obj_structure_closet_crate;
        if (crate == null)
        {
            if (!Dm.truthy((wires as datum_wires).is_cut(WIRE_LOADCHECK)))
            {
                buzz(MULEBOT_MOOD_SIGH);
                return; // if not hacked, only allow crates to be loaded
            }
        }

        if (crate != null || isobj(atom_to_load))
        {
            obj object_to_load = (obj)atom_to_load;
            if (Dm.truthy(object_to_load.has_buckled_mobs()) || Dm.locate<mob>(atom_to_load) != null) //can't load non crates objects with mobs buckled to it or inside it.
            {
                buzz(MULEBOT_MOOD_SIGH);
                return;
            }

            if (crate != null)
                await crate.close();  //make sure the crate is closed
            await object_to_load.forceMove(this);
            playsound(this, SFX_HEAVY_DROP, 60, TRUE);
        }
        else if (isliving(atom_to_load))
        {
            if (!Dm.truthy(await load_mob(atom_to_load as mob_living))) //forceMove() is handled in buckling
                return;
        }

        load = atom_to_load;
        float stored_alpha = load.alpha;
        do_loading_animation(load, prev_loc as turf);
        load.alpha = stored_alpha; // Restoring previous alpha following the animation's completion.
        load.transform = ((matrix)load.transform).Scale(10f / 7);
        update_appearance();
    }

    ///resolves the name to display for the loaded mob. primarily needed for the paranormal subtype since we don't want to show the name of ghosts riding it.
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_delivery.dm:82
    public virtual object get_load_name()
    {
        return load != null ? load.name : null;
    }

    ///Loads a mob onto the mulebot
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_delivery.dm:86
    public virtual async DmTask<object> load_mob(mob_living mob_to_load = null)
    {
        can_buckle = TRUE;
        if (Dm.truthy(await buckle_mob(mob_to_load)))
        {
            passenger = mob_to_load;
            load = mob_to_load;
            can_buckle = FALSE;
            return TRUE;
        }
        return null;
    }

    /**
     * called to unload the bot
     * argument is optional direction to unload
     * if zero or null, unload at bot's location
     */
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_delivery.dm:99
    public virtual async DmTask unload(object dirn = null)
    {
        if (QDELETED(load))
        {
            if (load != null) //if our thing was qdel'd, there's likely a leftover reference. just clear it and remove the overlay. we'll let the bot keep moving around to prevent it abruptly stopping somewhere.
            {
                load = null;
                update_appearance();
            }
            return;
        }

        update_bot_mode(new_mode: BOT_IDLE);

        atom_movable cached_load = load; //cache the load since unbuckling mobs clears the var.
        unbuckle_all_mobs();
        if (load != null) //don't have to do any of this for mobs.
        {
            await cached_load.forceMove(loc);
            cached_load.pixel_y = Dm.initial(cached_load, (atom p) => p.pixel_y);
            cached_load.layer = Dm.initial(cached_load, (atom p) => p.layer);
            SET_PLANE_EXPLICIT(cached_load, Dm.initial(cached_load, (atom p) => p.plane), this);
            load = null;
        }

        if (Dm.truthy(dirn)) //move the thing to the delivery point.
            await cached_load.Move(get_step(loc, Dm.num(dirn)) as atom, dirn);
        playsound(this, SFX_HEAVY_DROP, 50, TRUE);
        update_appearance();
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_delivery.dm:124
    public virtual void do_loading_animation(atom_movable animated_load = null, turf old_loc = null)
    {
        if (animated_load == null || old_loc == null)
            return;
        animate(this, Dm.named("time", 0.1f * SECONDS), Dm.named("pixel_y", -2));

        image pickup_animation = image(icon: animated_load);
        SET_PLANE(pickup_animation, GAME_PLANE, old_loc);
        pickup_animation.appearance_flags = APPEARANCE_UI_IGNORE_ALPHA;

        float direction = Dm.num(get_dir(old_loc, this));
        float to_x = base_pixel_x + base_pixel_w;
        float to_y = base_pixel_y + base_pixel_z;

        if (Dm.truthy(Dm.bitand(direction, NORTH)))
            to_y += 32;
        else if (Dm.truthy(Dm.bitand(direction, SOUTH)))
            to_y -= 32;
        if (Dm.truthy(Dm.bitand(direction, EAST)))
            to_x += 32;
        else if (Dm.truthy(Dm.bitand(direction, WEST)))
            to_x -= 32;
        if (!Dm.truthy(direction))
        {
            to_y += 10;
            pickup_animation.pixel_x = Dm.num(pickup_animation.pixel_x) + 6 * (prob(50) ? 1 : -1); //6 to the right or left, helps break up the straight upward move
        }

        atom_movable_flick_visual pickup = old_loc.flick_overlay_view(pickup_animation, 0.4f * SECONDS) as atom_movable_flick_visual;
        matrix animation_matrix = Dm.@new<matrix>(pickup.transform);
        animation_matrix.Turn(Dm.num(pick(-30, 30)));
        animation_matrix.Scale(1.1f);

        animate(pickup, Dm.named("alpha", 175), Dm.named("pixel_x", to_x), Dm.named("pixel_y", to_y), Dm.named("time", 0.3f * SECONDS), Dm.named("transform", animation_matrix), Dm.named("easing", CUBIC_EASING));
        animate(animated_load, Dm.named("alpha", 0), Dm.named("transform", Dm.@new<matrix>().Scale(0.7f)), Dm.named("time", 0.1f * SECONDS));
        animate(this, Dm.named("time", 0.1f * SECONDS), Dm.named("pixel_y", 0), Dm.named("delay", 0.1f * SECONDS), Dm.named("easing", QUAD_EASING));
    }
}
