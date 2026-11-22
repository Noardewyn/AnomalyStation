// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/gulag_item_reclaimer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Gaps LC1 (lavaland.md L3's labour camp): the whole file. The equipment reclaimer on the brig's wall keeps what the labour camp
// teleporter took from each prisoner and gives it back to the prisoner whose ID met its goal, or to the brig's officers (an emag
// lets anyone); a prisoner gone ejects their things. Its window is GulagItemReclaimer. The var block is generated.

using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/machinery/gulag_item_reclaimer.dm:1
public partial class obj_machinery_gulag_item_reclaimer : obj_machinery
{
    // tg: code/game/machinery/gulag_item_reclaimer.dm:14
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        foreach (object person in Dm.each((stored_items as DmList)?.Copy()))
        {
            if (stored_items[person] is DmList items)
            {
                Dm.remove(ref items, gone);
                stored_items[person] = items;
            }
        }
        return dot;
    }

    // tg: code/game/machinery/gulag_item_reclaimer.dm:19
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
            return dot;

        Dm.append(ref dot, mutable_appearance(icon, screen_icon));
        // DEVIATION(DV12): . += emissive_appearance(icon, screen_icon, src), the screen's glow on the emissive plane, is not created
        return dot;
    }

    // tg: code/game/machinery/gulag_item_reclaimer.dm:27
    public override object Destroy(float? force = null)
    {
        foreach (obj_item I in Dm.each<obj_item>((contents as DmList)?.Copy()))
            Waiting.NoSleep(I.forceMove(get_turf(this)));
        if (linked_teleporter != null)
            linked_teleporter.linked_reclaimer = null;
        linked_teleporter = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/gulag_item_reclaimer.dm:36
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED))) // emagging lets anyone reclaim all the items
            return FALSE;
        req_access = Dm.list();
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        screen_icon = "emagged_general";
        update_appearance();
        balloon_alert(user, I18n.T("id checker scrambled"));
        return TRUE;
    }

    // tg: code/game/machinery/gulag_item_reclaimer.dm:46
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "GulagItemReclaimer", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/gulag_item_reclaimer.dm:52
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        bool can_reclaim = false;

        if (Dm.truthy(await allowed__proc(user)))
            can_reclaim = true;

        obj_item_card_id I = null;
        if (user is mob_living L)
            I = L.get_idcard(TRUE) as obj_item_card_id;
        if (I is obj_item_card_id_advanced_prisoner P)
        {
            if (P.points >= Dm.num(P.goal))
                can_reclaim = true;
        }

        DmList mobs = Dm.list();
        foreach (object i in Dm.each((stored_items as DmList)?.Copy()))
        {
            var thismob = i as mob;
            if (QDELETED(thismob))
            {
                await say(I18n.T("Alert! Unable to locate vital signals of a previously processed prisoner. Ejecting equipment!"));
                await drop_items(thismob);
                continue;
            }
            DmList mob_info = Dm.list();
            mob_info["name"] = thismob.real_name;
            mob_info["mob"] = Dm.text("[]", REF(thismob));
            Dm.append(ref mobs, Dm.list(mob_info));
        }

        data["mobs"] = mobs;
        data["can_reclaim"] = can_reclaim ? TRUE : FALSE;

        return data;
    }

    // tg: code/game/machinery/gulag_item_reclaimer.dm:83
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (Dm.str(action))
        {
            case "release_items":
            {
                // locate(params["mobref"]) in stored_items
                mob_living_carbon_human H = Dm.locate_ref(@params["mobref"]) is mob_living_carbon_human found && Dm.@in(found, stored_items) ? found : null;
                if (!ReferenceEquals(H, usr) && !Dm.truthy(await allowed__proc(usr)))
                {
                    to_chat(usr, span_warning(I18n.T("Access denied.")));
                    return dot;
                }
                await drop_items(H);
                dot = TRUE;
                break;
            }
        }
        return dot;
    }

    // tg: code/game/machinery/gulag_item_reclaimer.dm:97
    public virtual async DmTask drop_items(mob user)
    {
        if (user == null || !Dm.truthy(stored_items[user]))
            return;
        if (!Dm.truthy(await use_energy(active_power_usage, force: FALSE)))
        {
            balloon_alert(user, I18n.T("not enough energy!"));
            return;
        }
        var drop_location = this.drop_location() as atom;
        foreach (obj_item W in Dm.each<obj_item>((stored_items[user] as DmList)?.Copy()))
        {
            DmList items = (DmList)stored_items[user];
            Dm.remove(ref items, W);
            stored_items[user] = items;
            await W.forceMove(drop_location);
        }
        Dm.remove(ref stored_items, user);
        user.log_message("has reclaimed their items from the gulag item reclaimer.", LOG_GAME);
    }
}
