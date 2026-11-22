// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/nukeop/equipment/nuclear_authentication_disk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1a, D316: the Captain's disk on Meta; N1c: its weight on the lone operative event) but the minimap blip, the
// minimap's (tg's minimap HUD, not ported). The var block is generated (D111).

// DAT FUKKEN DISK.
// tg: code/modules/antagonists/nukeop/equipment/nuclear_authentication_disk.dm:2
public partial class obj_item_disk_nuclear : obj_item_disk
{
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_authentication_disk.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_bed_tuckable), mapload, 6, -6, 0));

        if (!Dm.truthy(fake))
        {
            Waiting.NoSleep(AddComponent(typeof(datum_component_stationloving), Dm.num(!Dm.truthy(fake))));
            Waiting.NoSleep(AddComponent(typeof(datum_component_keep_me_secure), Dm.@new<datum_callback>(this, nameof(secured_process)), Dm.@new<datum_callback>(this, nameof(unsecured_process)), 10));
            Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this));
            // add_minimap_blip(src, MINIMAP_NUKEDISK_BLIP, "green_disk_off", 'icons/ui_icons/minimap/map_blips_large.dmi', TRUE): the minimap
            // (modules/minimap) is not ported
        }
        else
        {
            // Ensure fake disks still have examine text, but dont actually do anything
            Waiting.NoSleep(AddComponent(typeof(datum_component_keep_me_secure)));
        }
        return dot;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_authentication_disk.dm:33
    public override DmTask setup_reskins()
    {
        return DmTask.CompletedTask;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_authentication_disk.dm:36
    public virtual void secured_process(object last_move = null)
    {
        var new_turf = get_turf(this) as turf;
        var loneop = Dm.locate<datum_round_event_control_operative>(SSevents?.control);
        if (loneop != null && loneop.occurrences < loneop.max_occurrences && prob(loneop.weight))
        {
            loneop.weight = max(loneop.weight - 1, 0);
            if (loneop.weight % 5 == 0 && SSticker.totalPlayers > 1)
                message_admins(Dm.text("[] is secured (currently in []). The weight of Lone Operative is now [].", this, AREACOORD(new_turf), loneop.weight));
            log_game(Dm.text("[] being secured has reduced the weight of the Lone Operative event to [].", this, loneop.weight));
        }
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_authentication_disk.dm:45
    public virtual void unsecured_process(object last_move = null)
    {
        var new_turf = get_turf(this) as turf;
        /// How comfy is our disk?
        int disk_comfort_level = 0;

        //Go through and check for items that make disk comfy
        foreach (obj comfort_item in Dm.each<obj>((loc as atom)?.contents))
        {
            if (comfort_item is obj_item_bedsheet || comfort_item is obj_structure_bed)
                disk_comfort_level++;
        }

        if (Dm.num(last_move) < world.time - 500 * SECONDS && prob((world.time - 500 * SECONDS - Dm.num(last_move)) * 0.0001f))
        {
            var loneop = Dm.locate<datum_round_event_control_operative>(SSevents?.control);
            if (loneop != null && loneop.occurrences < loneop.max_occurrences)
            {
                loneop.weight += 1;
                if (loneop.weight % 5 == 0 && SSticker.totalPlayers > 1)
                {
                    if (disk_comfort_level >= 2)
                        visible_message(span_notice(I18n.T("[] sleeps soundly. Sleep tight, disky.", this)));
                    message_admins(Dm.text("[] is unsecured in []. The weight of Lone Operative is now [].", this, AREACOORD(new_turf), loneop.weight));
                }
                log_game(Dm.text("[] was left unsecured in []. Weight of the Lone Operative event increased to [].", this, loc_name(new_turf), loneop.weight));
            }
        }
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_authentication_disk.dm:65
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(fake))
            return dot;

        if (isobserver(user) || HAS_MIND_TRAIT(user, TRAIT_DISK_VERIFIER))
            dot.Add(span_warning(I18n.T("The serial numbers on [] are incorrect.", this)));
        return dot;
    }

    /*
     * You can't accidentally eat the nuke disk, bro
     */
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_authentication_disk.dm:76
    public override async DmTask<object> on_accidental_consumption(mob_living_carbon M = null, mob_living_carbon user = null, obj_item source_item = null, float? discover_after = null)
    {
        discover_after ??= TRUE;
        M.visible_message(span_warning(I18n.T("[] looks like [] just bitten into something important.", M, M.p_theyve())),
            span_warning(I18n.T("Wait, is this the nuke disk?")));

        return discover_after;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_authentication_disk.dm:82
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_claymore_highlander claymore || Dm.truthy(fake))
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (claymore.nuke_disk != null)
        {
            to_chat(user, span_notice(I18n.T("Wait... what?")));
            qdel(claymore.nuke_disk);
            claymore.nuke_disk = null;
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(
            span_warning(I18n.T("[] captures []!", user, this)),
            span_userdanger(I18n.T("You've got the disk! Defend it with your life!")));
        await forceMove(claymore);
        claymore.nuke_disk = this;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_authentication_disk.dm:101
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is going delta! It looks like [] trying to commit suicide!", user, user.p_theyre())));
        playsound(this, "sound/announcer/alarm/nuke_alarm.ogg", 50, -1, TRUE);
        for (int i = 1; i <= 100; i++)
            _addtimer(Dm.@new<datum_callback>(user, nameof(atom.add_atom_colour), i % 2 != 0 ? COLOR_VIBRANT_LIME : COLOR_RED, ADMIN_COLOUR_PRIORITY), i,
                file: "code/modules/antagonists/nukeop/equipment/nuclear_authentication_disk.dm", line: 105);
        _addtimer(Dm.@new<datum_callback>(this, nameof(manual_suicide), user), 101,
            file: "code/modules/antagonists/nukeop/equipment/nuclear_authentication_disk.dm", line: 106);
        return MANUAL_SUICIDE;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_authentication_disk.dm:109
    public virtual async DmTask manual_suicide(mob_living user = null)
    {
        await user.remove_atom_colour(ADMIN_COLOUR_PRIORITY);
        user.visible_message(span_suicide(I18n.T("[] is destroyed by the nuclear blast!", user)));
        await user.adjust_oxy_loss(200);
        await user.death(FALSE);
    }
}
