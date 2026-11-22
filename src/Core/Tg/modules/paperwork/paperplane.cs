// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/paperplane.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298). The var blocks are generated.

// tg: code/modules/paperwork/paperplane.dm:1
public partial class obj_item_paperplane : obj_item
{
    // tg: code/modules/paperwork/paperplane.dm:24
    [DmParams("mapload", "paper_made_of")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item_paper paper_made_of = Dm.arg(__extra, 0) as obj_item_paper;
        object dot = base.Initialize(mapload, __extra);
        pixel_x = base_pixel_x + rand(-9, 9);
        pixel_y = base_pixel_y + rand(-8, 8);
        if (paper_made_of != null)
        {
            internal_paper = paper_made_of;
            flags_1 = paper_made_of.flags_1;
            color = paper_made_of.color;
            Waiting.NoSleep(paper_made_of.forceMove(this));
        }
        else
            internal_paper = Dm.@new<obj_item_paper>(this);
        if (istype(internal_paper, typeof(obj_item_paper_carbon_copy)))
            icon_state = Dm.text("[]_carbon", base_icon_state);
        update_appearance(UPDATE_ICON);
        Waiting.NoSleep(AddElement(typeof(datum_element_burn_on_item_ignition)));
        return dot;
    }

    // tg: code/modules/paperwork/paperplane.dm:40
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (internal_paper == gone)
        {
            internal_paper = null;
            if (!QDELETED(this))
                qdel(this);
        }
        return dot;
    }

    // tg: code/modules/paperwork/paperplane.dm:47
    public override object Destroy(float? force = null)
    {
        internal_paper = null;
        return base.Destroy(force);
    }

    // tg: code/modules/paperwork/paperplane.dm:51
    public override object custom_fire_overlay__proc()
    {
        if (!Dm.truthy(custom_fire_overlay))
            custom_fire_overlay = mutable_appearance(Dm.resource("icons/obj/service/bureaucracy.dmi"), "paperplane_onfire", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART));
        return custom_fire_overlay;
    }

    // tg: code/modules/paperwork/paperplane.dm:56
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        obj_item_organ_eyes eyes = user.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        await user.Stun(20 * SECONDS);
        user.visible_message(span_suicide(I18n.T("[] jams [] in [] nose. It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        await user.adjust_eye_blur(12 * SECONDS);
        if (eyes != null)
            await eyes.apply_organ_damage(rand(6, 8));
        await sleep(1 * SECONDS);
        return BRUTELOSS;
    }

    // tg: code/modules/paperwork/paperplane.dm:66
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        foreach (object stamp in Dm.each(internal_paper?.stamp_cache))
            Dm.append(ref dot, Dm.text("[]_[]", base_icon_state, stamp));
        return dot;
    }

    // tg: code/modules/paperwork/paperplane.dm:71
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        balloon_alert(user, I18n.T("unfolded"));

        atom location = drop_location() as atom;
        // Need to keep a reference to the internal paper
        // when we move it out of the plane, our ref gets set to null
        obj_item_paper released_paper = internal_paper;
        await released_paper.forceMove(location);
        // This will as a side effect, qdel the paper plane, making the user's hands empty

        await user.put_in_hands(released_paper);
        return null;
    }

    // tg: code/modules/paperwork/paperplane.dm:83
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (IS_WRITING_UTENSIL(tool))
        {
            to_chat(user, span_warning(I18n.T("You should unfold [] before changing it!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (istype(tool, typeof(obj_item_stamp))) //we don't randomize stamps on a paperplane
        {
            await internal_paper.item_interaction(user, tool); //spoofed attack to update internal paper.
            update_appearance();
            add_fingerprint(user);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/modules/paperwork/paperplane.dm:96
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        if (iscarbon(hit_atom) && HAS_TRAIT(hit_atom, TRAIT_PAPER_MASTER))
        {
            mob_living_carbon hit_carbon = (mob_living_carbon)hit_atom;
            if (Dm.truthy(hit_carbon.can_catch_item(skip_throw_mode_check: TRUE))) // tg passes src too: can_catch_item(skip_throw_mode_check, try_offhand) ignores it
                hit_carbon.throw_mode_on(THROW_MODE_TOGGLE);
        }

        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (Dm.truthy(dot) || !ishuman(hit_atom)) //if the plane is caught or it hits a nonhuman
            return dot;
        mob_living_carbon_human hit_human = (mob_living_carbon_human)hit_atom;
        obj_item_organ_eyes eyes = hit_human.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (!prob(hit_probability))
            return dot;
        if (Dm.truthy(hit_human.is_eyes_covered()))
            return dot;
        visible_message(span_danger(I18n.T("\\The [] hits [] in the eye[]!", this, hit_human, eyes != null ? "" : I18n.T(" socket"))));
        Waiting.NoSleep(hit_human.adjust_eye_blur(12 * SECONDS));
        if (eyes != null)
            Waiting.NoSleep(eyes.apply_organ_damage(rand(6, 8)));
        Waiting.NoSleep(hit_human.Paralyze(4 * SECONDS));
        Waiting.NoSleep(hit_human.emote("scream"));
        return dot;
    }

    // tg: code/modules/paperwork/paperplane.dm:117
    public override async DmTask<object> throw_at(atom target = null, object range = null, object speed = null, atom thrower = null, float? spin = null, float? diagonals_first = null, datum_callback callback = null, float? force = null, float? gentle = null, object quickstart = null, Type throw_datum_typepath = null, params object[] __extra)
    {
        diagonals_first ??= FALSE;
        quickstart ??= TRUE;
        return await base.throw_at(target, range, speed, thrower, FALSE, diagonals_first, callback, quickstart: quickstart);
    }
}
