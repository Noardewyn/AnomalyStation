// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/botlaunchpad.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole, with the bot pad controller (game/objects/items/botpad_remote.cs). The bot pad opens with a
// screwdriver ("botpad-open"), comes apart with a crowbar, saves itself to a multitool's buffer while open, launches the one bot on
// it in a see-through transport pod that drops it at the controller's user, and recalls it (the bot's summon). The botpod's var
// block is generated (D111).

// tg: code/game/machinery/botlaunchpad.dm:1
public partial class obj_machinery_botpad : obj_machinery
{
    // tg: code/game/machinery/botlaunchpad.dm:13
    public override object Destroy(float? force = null)
    {
        if (connected_remote != null)
        {
            connected_remote.connected_botpad = null;
            connected_remote = null;
        }
        launched_bot = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/botlaunchpad.dm:20
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]-open", base_icon_state) : (string)base_icon_state;
        return dot;
    }

    // tg: code/game/machinery/botlaunchpad.dm:24
    public override DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return DmTask<object>.FromResult(default_deconstruction_screwdriver(user, tool));
    }

    // tg: code/game/machinery/botlaunchpad.dm:27
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/botlaunchpad.dm:30
    public override DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open))
            return DmTask<object>.FromResult(NONE);
        obj_item_multitool multitool = tool as obj_item_multitool;
        multitool.set_buffer(this);
        balloon_alert(user, I18n.T("saved to multitool buffer"));
        return DmTask<object>.FromResult(ITEM_INTERACT_SUCCESS);
    }

    // Checks the turf for a bot and launches it if it's the only mob on the pad.
    // tg: code/game/machinery/botlaunchpad.dm:39
    public virtual async DmTask launch(mob_living user = null)
    {
        turf reverse_turf = get_turf(user) as turf;
        atom possible_bot = null;
        foreach (mob_living robot in Dm.each<mob_living>(get_turf(this)))
        {
            if (!isbot(robot))
            {
                user.balloon_alert(user, I18n.T("unidentified life form on the pad!"));
                return;
            }
            if (possible_bot != null)
            {
                user.balloon_alert(user, I18n.T("too many bots on the pad!"));
                return;
            }
            possible_bot = robot; // We don't change the launched_bot var here because we are not sure if there is another bot on the pad.
        }

        if (!Dm.truthy(await use_energy(active_power_usage, force: FALSE)))
        {
            balloon_alert(user, I18n.T("not enough energy!"));
            return;
        }
        launched_bot = WEAKREF(possible_bot) as datum_weakref;
        await podspawn(Dm.list(
            Dm.kv("target", get_turf(this)),
            Dm.kv("path", typeof(obj_structure_closet_supplypod_transport_botpod)),
            Dm.kv("style", typeof(datum_pod_style_seethrough)),
            Dm.kv("reverse_dropoff_coords", Dm.list(reverse_turf.x, reverse_turf.y, reverse_turf.z))
        ));
    }

    // tg: code/game/machinery/botlaunchpad.dm:62
    public virtual async DmTask recall(mob_living user = null)
    {
        atom our_bot = launched_bot?.resolve() as atom;
        if (our_bot == null)
        {
            user.balloon_alert(user, I18n.T("no bots sent from the pad!"));
            return;
        }
        user.balloon_alert(user, I18n.T("bot sent back to pad"));
        mob_living_basic_bot basic_bot = our_bot as mob_living_basic_bot;
        await basic_bot.summon_bot(this);
    }
}
