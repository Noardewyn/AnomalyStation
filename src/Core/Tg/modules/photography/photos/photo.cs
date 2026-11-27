// Translated from tgstation (snapshot 1d99bd7f): code/modules/photography/photos/photo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the persistence (gaps J3, D298); the shown picture is the photographer's client's render (DV685). The var blocks are generated.

/*
 * Photo
 */
// tg: code/modules/photography/photos/photo.dm:5
public partial class obj_item_photo : obj_item
{
    // tg: code/modules/photography/photos/photo.dm:17
    public override async DmTask<object> grind_results()
    {
        return Dm.truthy(custom_materials?[typeof(datum_material_hauntium)]) ? Dm.list(Dm.kv(typeof(datum_reagent_hauntium), 20)) : Dm.list(Dm.kv(typeof(datum_reagent_iodine), 4));
    }

    // tg: code/modules/photography/photos/photo.dm:23
    [DmParams("mapload", "P", "datum_name", "datum_desc", "override_name")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_picture P = Dm.arg(__extra, 0) as datum_picture;
        object datum_name = Dm.arg(__extra, 1) ?? TRUE;
        object datum_desc = Dm.arg(__extra, 2) ?? TRUE;
        object override_name = Dm.arg(__extra, 3) ?? TRUE;
        Waiting.NoSleep(set_picture(P, datum_name, datum_desc, Dm.num(override_name)));
        //Photos are quite rarer than papers, so they're more likely to be added to the queue to make things even.
        // if(!mapload && prob(MESSAGE_BOTTLE_CHANCE * 5) && picture?.id) LAZYADD(SSpersistence.queued_message_bottles, src): picture.id is set only by the pictures' log (no image to log)
        object dot = base.Initialize(mapload);
        Waiting.NoSleep(AddElement(typeof(datum_element_burn_on_item_ignition)));
        return dot;
    }

    // tg: code/modules/photography/photos/photo.dm:31
    public override object Destroy(float? force = null)
    {
        // LAZYREMOVE(SSpersistence.queued_message_bottles, src): never queued (Initialize)
        return base.Destroy(force);
    }

    // tg: code/modules/photography/photos/photo.dm:35
    public virtual async DmTask set_picture(datum_picture P = null, object setname = null, object setdesc = null, float? name_override = null)
    {
        name_override ??= FALSE;
        if (P == null)
            return;
        picture = P;
        update_appearance();
        if (Dm.truthy(P.caption))
            scribble = P.caption;
        if (Dm.truthy(setname) && Dm.truthy(P.picture_name))
        {
            if (Dm.truthy(name_override))
                name = P.picture_name;
            else
                name = Dm.text("photo - []", P.picture_name);
        }
        if (Dm.truthy(setdesc) && Dm.truthy(P.picture_desc))
            desc = P.picture_desc;

        if (!Dm.truthy(P.see_ghosts)) ///Dont bother with this last bit if we can't see ghosts
            return;
        foreach (datum_weakref seen_ref in Dm.each<datum_weakref>(P.mobs_seen)) //Any ghosts in the pic? its a haunted photo ooooo~
        {
            mob seen = seen_ref.resolve() as mob;
            if (seen == null)
            {
                P.mobs_seen.Remove(seen_ref);
                continue;
            }
            if (!HAS_TRAIT(seen, TRAIT_GHOSTLY_MOB))
                continue;
            await set_custom_materials(Dm.list(Dm.kv(typeof(datum_material_hauntium), SHEET_MATERIAL_AMOUNT)));
            break;
        }
    }

    // tg: code/modules/photography/photos/photo.dm:63
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (picture == null || picture.picture_image == null)
            return await base.update_icon_state(__extra);
        icon I = picture.get_small_icon(Dm.initial(this, (obj_item_photo p) => p.icon_state)) as icon;
        if (Dm.truthy(I))
            icon = I;
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/photography/photos/photo.dm:71
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is taking one last look at \\the []! It looks like [] giving in to death!", user, this, user.p_theyre()))); //when you wanna look at photo of waifu one last time before you die...
        await user.emote("laugh", intentional: FALSE, forced: TRUE); // EVERY TIME I DO IT MAKES ME LAUGH
        return OXYLOSS;
    }

    // tg: code/modules/photography/photos/photo.dm:76
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        user.examinate(this);
        return null;
    }

    // tg: code/modules/photography/photos/photo.dm:79
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.eq((await tool.get_writing_implement_details() as DmList)?["interaction_mode"], MODE_WRITING)) // IS_WRITING_UTENSIL (__DEFINES/paper.dm:19), inline
            return NONE;
        if (!Dm.truthy(await user.can_write(tool)))
            return ITEM_INTERACT_BLOCKING;
        object txt = await tgui_input_text(user, I18n.T("What would you like to write on the back?"), I18n.T("Photo Writing"), max_length: 128);
        if (!Dm.truthy(txt) || !Dm.truthy(await user.can_perform_action(this)))
            return ITEM_INTERACT_BLOCKING;
        playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);
        scribble = txt;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/photography/photos/photo.dm:91
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;

        if (in_range(this, user) || isobserver(user))
            show(user);
        else
            dot.Add(span_warning(I18n.T("You need to get closer to get a good look at this photo!")));
        return dot;
    }

    // tg: code/modules/photography/photos/photo.dm:99
    public virtual void show(mob user = null)
    {
        if (picture == null || picture.picture_image == null)
        {
            to_chat(user, span_warning(I18n.T("[] seems to be blank...", this)));
            return;
        }
        string width_height = "width";
        if (picture.psize_y > picture.psize_x)
        {
            // if we're a tall picture, swap our focus to height to stay in frame
            width_height = "height";
        }
        ClientSeam.BrowseRsc(user, picture.picture_image, "tmp_photo.png"); // user << browse_rsc(picture.picture_image, "tmp_photo.png")
        ClientSeam.Browse(user, Dm.text("<html><head><meta http-equiv='Content-Type' content='text/html; charset=UTF-8'><title>[]</title></head>", name)
            + "<body style='overflow:hidden;margin:0;text-align:center'>"
            + Dm.text("<img src='tmp_photo.png' []='480' style='image-rendering:pixelated' />", width_height)
            + (Dm.truthy(scribble) ? Dm.text("<br>Written on the back:<br><i>[]</i>", scribble) : "")
            + "</body></html>", Dm.text("window=photo_showing;size=[]", Dm.truthy(scribble) ? "480x580" : "480x480"));
        // onclose(user, "[name]"): the window's close callback (browse()'s onclose, not ported: the Browser window closes by itself)
    }
}
