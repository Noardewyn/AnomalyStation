// Translated from tgstation (snapshot 1d99bd7f): code/modules/photography/photos/frame.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Picture frames. Keeping the framed photos between rounds (SSpersistence's photo frames database and its queue of mapped frames) is
// owner-deferred (D332): those calls are marked leaves, so a frame works for the round and the showroom and bar frames start empty.

// Picture frames

// tg: code/modules/photography/photos/frame.dm:3
public partial class obj_item_wallframe_picture : obj_item_wallframe
{
    // tg: code/modules/photography/photos/frame.dm:15
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_photo)))
            return NONE;
        if (displayed != null)
        {
            to_chat(user, span_warning(I18n.T("\\The [] already contains a photo.", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;
        displayed = tool as obj_item_photo;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/photography/photos/frame.dm:28
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (user.get_inactive_held_item() != this)
        {
            await base.attack_hand(user, modifiers, __extra);
            return null;
        }
        if (Dm.truthy(length(contents)))
        {
            obj_item I = pick(contents) as obj_item;
            await user.put_in_hands(I);
            to_chat(user, span_notice(I18n.T("You carefully remove the photo from \\the [].", this)));
            displayed = null;
            update_appearance();
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/photography/photos/frame.dm:40
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        user.examinate(this);
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/photography/photos/frame.dm:43
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(user.is_holding(this)) && displayed != null)
        {
            displayed.show(user);
            return Dm.list();
        }
        else
            return await base.examine(user, __extra);
    }

    // tg: code/modules/photography/photos/frame.dm:50
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (displayed != null)
            Dm.append(ref dot, displayed);
        return dot;
    }

    // tg: code/modules/photography/photos/frame.dm:55
    public override void after_attach(obj attached_to = null)
    {
        base.after_attach(attached_to);
        obj_structure_sign_picture_frame PF = attached_to as obj_structure_sign_picture_frame;
        PF.copy_overlays(this);
        if (displayed != null)
            PF.set_and_save_framed(displayed);
        if (Dm.truthy(length(contents)))
        {
            obj_item I = pick(contents) as obj_item;
            Waiting.NoSleep(I.forceMove(PF));
        }
    }
}

// tg: code/modules/photography/photos/frame.dm:65
public partial class obj_structure_sign_picture_frame : obj_structure_sign
{
    // tg: code/modules/photography/photos/frame.dm:77
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_art), art_value));
        Dm.Deferred("code/modules/photography/photos/frame.dm:80", "Initialize: LAZYADD(SSpersistence.queued_photo_frames, src), the framed photo loaded from the last round (persistence is owner-deferred, D332)");
        return dot;
    }

    // tg: code/modules/photography/photos/frame.dm:83
    public override object Destroy(float? force = null)
    {
        // LAZYREMOVE(SSpersistence.queued_photo_frames, src): the queue is not kept (D332)
        return base.Destroy(force);
    }

    // tg: code/modules/photography/photos/frame.dm:87
    public virtual object get_photo_id()
    {
        if (istype(framed, typeof(obj_item_photo)) && istype(framed.picture, typeof(datum_picture)))
            return framed.picture.id;
        return null;
    }

    //Manual loading, DO NOT USE FOR HARDCODED/MAPPED IN ALBUMS. This is for if an album needs to be loaded mid-round from an ID.
    // tg: code/modules/photography/photos/frame.dm:92
    public virtual void persistence_load()
    {
        Dm.Deferred("code/modules/photography/photos/frame.dm:93", "persistence_load: SSpersistence.photo_frames_database, the photo kept between rounds (persistence is owner-deferred, D332)");
    }

    // tg: code/modules/photography/photos/frame.dm:97
    public virtual void load_from_id(object id = null)
    {
        Dm.Deferred("code/modules/photography/photos/frame.dm:98", "load_from_id: load_photo_from_disk, the photo kept between rounds (persistence is owner-deferred, D332)");
    }

    /// Given a photo (or null), will change the contained picture, and queue a persistent save.
    // tg: code/modules/photography/photos/frame.dm:107
    public virtual void set_and_save_framed(obj_item_photo photo = null)
    {
        framed = photo;

        if (persistence_id == null)
            return;

        Dm.Deferred("code/modules/photography/photos/frame.dm:113", "set_and_save_framed: SSpersistence.photo_frames_database.set_key, the photo kept for the next round (persistence is owner-deferred, D332)");
    }

    // tg: code/modules/photography/photos/frame.dm:115
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(in_range(this, user)))
            framed?.show(user);
        return dot;
    }

    /// Internal proc
    // tg: code/modules/photography/photos/frame.dm:121
    public virtual async DmTask<object> try_deconstruct(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(can_decon))
            return FALSE;
        to_chat(user, span_notice(I18n.T("You start unsecuring []...", name)));
        if (Dm.truthy(await tool.use_tool(this, user, 3 * SECONDS, volume: 50)))
        {
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            to_chat(user, span_notice(I18n.T("You unsecure [].", name)));
            await deconstruct();
        }
        return TRUE;
    }

    // tg: code/modules/photography/photos/frame.dm:132
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return await try_deconstruct(user, tool);
    }

    // tg: code/modules/photography/photos/frame.dm:135
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        return await try_deconstruct(user, tool);
    }

    // tg: code/modules/photography/photos/frame.dm:138
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (framed == null)
            return FALSE;
        tool.play_tool_sound(this);
        await framed.forceMove(drop_location() as atom);
        user.visible_message(span_warning(I18n.T("[] cuts away [] from []!", user, framed, this)));
        set_and_save_framed(null);
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/photography/photos/frame.dm:149
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_photo)))
            return NONE;
        if (framed != null)
        {
            to_chat(user, span_warning(I18n.T("\\The [] already contains a photo.", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;
        set_and_save_framed(tool as obj_item_photo);
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/photography/photos/frame.dm:161
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (framed != null)
            framed.show(user);
        return dot;
    }

    // tg: code/modules/photography/photos/frame.dm:168
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (framed != null)
            Dm.append(ref dot, framed);
        return dot;
    }

    // tg: code/modules/photography/photos/frame.dm:173
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        obj_item_wallframe_picture showcase = Dm.@new<obj_item_wallframe_picture>(loc);
        if (framed != null)
        {
            showcase.displayed = framed;
            set_and_save_framed(null);
        }
        if (Dm.truthy(length(contents)))
        {
            obj_item I = pick(contents) as obj_item;
            await I.forceMove(showcase);
        }
        showcase.update_appearance();
    }
}

/// This used to be a plaque portrait of a monkey. Now it's been revamped into something more.
// tg: code/modules/photography/photos/frame.dm:189
public partial class obj_structure_sign_picture_frame_portrait : obj_structure_sign_picture_frame
{
    // tg: code/modules/photography/photos/frame.dm:198
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object sw__1 = rand(1, 4);
        if (Dm.eq(sw__1, 1)) // Deempisi
        {
            name = Dm.text("\\improper Mr. Deempisi portrait");
            icon_state = "frame-monkey";
            desc = "Under the portrait a plaque reads: 'While the meat grinder may not have spared you, fear not. Not one part of you has gone to waste... You were delicious.'";
        }
        else if (Dm.eq(sw__1, 2)) // A fruit
        {
            name = "picture of a fruit";
            icon_state = "frame-fruit";
            desc = "<i>Ceci n'est pas une orange.</i>";
        }
        else if (Dm.eq(sw__1, 3)) // Rat
        {
            name = Dm.text("\\improper Tom portrait");
            desc = "Jerry the cat is still not amused.";
            icon_state = "frame-rat";
        }
        else if (Dm.eq(sw__1, 4)) // Ratvar
        {
            name = "portrait of the imprisoned god";
            desc = "Under the portrait a plaque reads: 'In loving memory of Ratvar, ancient powerful entity and rival of Nar'Sie, ultimately struck down by NT bluespace artillery at the hands of Outpost 17 crew. Rust in peace.'"; // common core lore.
            icon_state = "frame-ratvar";
        }
        portrait_name = name;
        portrait_state = icon_state;
        portrait_desc = desc;
        return dot;
    }

    // tg: code/modules/photography/photos/frame.dm:224
    public override object update_name(float? updates = null)
    {
        if (framed != null)
            name = Dm.initial(this, (obj_structure_sign_picture_frame_portrait p) => p.name);
        else
            name = portrait_name as string;
        return base.update_name(updates);
    }

    // tg: code/modules/photography/photos/frame.dm:231
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (framed != null)
            icon_state = "frame-overlay";
        else
            icon_state = portrait_state as string;
        return dot;
    }

    // tg: code/modules/photography/photos/frame.dm:238
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        if (framed != null)
            desc = "Every time you look it makes you laugh.";
        else
            desc = portrait_desc as string;
        return dot;
    }

    // tg: code/modules/photography/photos/frame.dm:245
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        if (framed == null)
            Dm.append(ref dot, span_notice(I18n.T("The frame and the picture are glued together, but you guess you could slip a photo between the two.")));
        return dot;
    }
}

///Generates a persistence id unique to the current map. Every bar should feel a little bit different after all.
// tg: code/modules/photography/photos/frame.dm:274
public partial class obj_structure_sign_picture_frame_portrait_bar : obj_structure_sign_picture_frame_portrait
{
    // tg: code/modules/photography/photos/frame.dm:278
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        // SSmapping?.current_map?: a test world loads no map config (rules 10.6)
        if (SSmapping?.current_map != null && !Dm.eq(SSmapping.current_map.map_path, CUSTOM_MAP_PATH)) //skip adminloaded custom maps.
            persistence_id = Dm.text("frame_bar_[]", SSmapping.current_map.map_name);
        return base.Initialize(mapload, __extra);
    }
}
