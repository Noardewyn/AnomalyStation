// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/paper_biscuit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the screentips (add_context: live-partial rows, D264), gaps S3 (D298). The var blocks are generated.

// tg: code/modules/paperwork/paper_biscuit.dm:1
public partial class obj_item_folder_biscuit : obj_item_folder
{
    // tg: code/modules/paperwork/paper_biscuit.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (contained_slip is Type slip_path && ispath(slip_path, typeof(obj_item_paper_paperslip)))
            contained_slip = Dm.@new(slip_path, this);
        return dot;
    }

    // tg: code/modules/paperwork/paper_biscuit.dm:24
    public override object Destroy(float? force = null)
    {
        if (isdatum(contained_slip))
        {
            object slip = contained_slip;
            QDEL_NULL(ref slip);
            contained_slip = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/paperwork/paper_biscuit.dm:29
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (contained_slip == gone)
            contained_slip = null;
        return dot;
    }

    // tg: code/modules/paperwork/paper_biscuit.dm:34
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (contained_slip == null && istype(arrived, typeof(obj_item_paper_paperslip)))
            contained_slip = arrived;
        return dot;
    }

    // tg: code/modules/paperwork/paper_biscuit.dm:39
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] tries to eat []! [] trying to commit suicide!", user, this, user.p_theyre())));
        playsound(get_turf(user), Dm.resource("sound/effects/wounds/crackandbleed.ogg"), 40, TRUE); //Don't eat plastic cards kids, they get really sharp if you chew on them.
        return BRUTELOSS;
    }

    // tg: code/modules/paperwork/paper_biscuit.dm:44
    public override object get_paper_overlay()
    {
        if (!Dm.truthy(cracked))
            return null;
        return base.get_paper_overlay();
    }

    ///Checks if the biscuit has been already cracked.
    // tg: code/modules/paperwork/paper_biscuit.dm:50
    public virtual object crack_check(mob user = null)
    {
        if (Dm.truthy(cracked))
            return TRUE;
        balloon_alert(user, I18n.T("open first!"));
        return FALSE;
    }

    // tg: code/modules/paperwork/paper_biscuit.dm:56
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(cracked))
            Dm.append(ref dot, span_notice(I18n.T("It's been cracked open.")));
        else
        {
            Dm.append(ref dot, span_notice(I18n.T("You'll need to crack it open to access its contents.")));
            if (contained_slip is obj_item_paper_paperslip slip)
                Dm.append(ref dot, I18n.T("This one contains [].", slip.name));
        }
        return dot;
    }

    //The next few checks are done to prevent you from reaching the contents or putting anything inside when it's not cracked open
    // tg: code/modules/paperwork/paper_biscuit.dm:72
    public override async DmTask remove_item(obj_item Item = null, mob user = null)
    {
        if (!Dm.truthy(crack_check(user)))
            return;

        await base.remove_item(Item, user);
    }

    // tg: code/modules/paperwork/paper_biscuit.dm:78
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)) && !Dm.truthy(crack_check(user)))
            return null;

        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/paperwork/paper_biscuit.dm:84
    public override async DmTask<object> interact_with_insertables(obj_item interacting_with = null, mob_living user = null)
    {
        if (!Dm.truthy(crack_check(user)))
            return ITEM_INTERACT_BLOCKING;
        return await base.interact_with_insertables(interacting_with, user);
    }

    // tg: code/modules/paperwork/paper_biscuit.dm:89
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);
        if (!Dm.truthy(cracked))
        {
            if (!Dm.eq(await tgui_alert(user, I18n.T("Do you want to crack it open?"), I18n.T("Biscuit Cracking"), Dm.list("Yes", "No")), "Yes"))
                return null;
            cracked = TRUE;
            contents_hidden = FALSE;
            playsound(get_turf(user), Dm.resource("sound/effects/wounds/crack1.ogg"), 60);
            icon_state = Dm.text("[]_cracked", icon_state);
            update_appearance();
        }

        await ui_interact(user);
        return null;
    }
}

// tg: code/modules/paperwork/paper_biscuit.dm:118
public partial class obj_item_folder_biscuit_unsealed : obj_item_folder_biscuit
{
    // tg: code/modules/paperwork/paper_biscuit.dm:131
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(has_been_sealed))
            Dm.append(ref dot, span_notice(I18n.T("This one could be sealed <b>in hand</b>. Once sealed, the contents are inaccessible until cracked open again - but once opened this is irreversible.")));
        return dot;
    }

    //Asks if you want to seal the biscuit, after you do that it behaves like a normal paper biscuit.
    // tg: code/modules/paperwork/paper_biscuit.dm:143
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);
        if (!Dm.truthy(cracked))
            return await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(has_been_sealed))
            return null;
        if (!Dm.eq(await tgui_alert(user, I18n.T("Do you want to seal it? This can only be done once."), I18n.T("Biscuit Sealing"), Dm.list("Yes", "No")), "Yes"))
            return null;
        cracked = FALSE;
        has_been_sealed = TRUE;
        contents_hidden = TRUE;
        playsound(get_turf(user), Dm.resource("sound/items/duct_tape/duct_tape_snap.ogg"), 60);
        icon_state = Dm.text("[]", sealed_icon);
        update_appearance();
        return null;
    }
}
