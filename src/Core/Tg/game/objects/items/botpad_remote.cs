// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/botpad_remote.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole. The bot pad controller links to a bot pad from a multitool's buffer, launches the bot standing
// alone on the pad (a left click) and recalls the launched bot (a right click), with the terminal's typing sound and balloon
// alerts. The bot pad itself (game/machinery/botlaunchpad.cs) is translated with it. The var block is generated (D111).

// tg: code/game/objects/items/botpad_remote.dm:1
public partial class obj_item_botpad_remote : obj_item
{
    // tg: code/game/objects/items/botpad_remote.dm:13
    public override object Destroy(float? force = null)
    {
        if (connected_botpad != null)
        {
            connected_botpad.connected_remote = null;
            connected_botpad = null;
        }
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/botpad_remote.dm:19
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        playsound(this, SFX_TERMINAL_TYPE, 25, FALSE);
        await try_launch(user as mob_living);
        return null;
    }

    // tg: code/game/objects/items/botpad_remote.dm:24
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        playsound(this, SFX_TERMINAL_TYPE, 25, FALSE);
        if (connected_botpad != null)
        {
            await connected_botpad.recall(user as mob_living);
            return null;
        }
        user?.balloon_alert(user, I18n.T("no connected pad!"));
        return null;
    }

    // tg: code/game/objects/items/botpad_remote.dm:32
    public override DmTask<object> multitool_act(mob_living user = null, obj_item multitool = null)
    {
        object dot = NONE;
        if (!(((obj_item_multitool)multitool).buffer is obj_machinery_botpad))
            return DmTask<object>.FromResult(dot);

        obj_machinery_botpad buffered_remote = ((obj_item_multitool)multitool).buffer as obj_machinery_botpad;
        if (buffered_remote == connected_botpad)
        {
            to_chat(user, span_warning(I18n.T("Controller cannot connect to its own botpad!")));
            return DmTask<object>.FromResult(ITEM_INTERACT_BLOCKING);
        }

        if (connected_botpad == null && buffered_remote is obj_machinery_botpad)
        {
            connected_botpad = buffered_remote;
            connected_botpad.connected_remote = this;
            connected_botpad.id = id;
            ((obj_item_multitool)multitool).set_buffer(null);
            to_chat(user, span_notice(I18n.T("You connect the controller to the pad with data from \\the []'s buffer.", multitool)));
            return DmTask<object>.FromResult(ITEM_INTERACT_SUCCESS);
        }
        return DmTask<object>.FromResult(dot);
    }

    // tg: code/game/objects/items/botpad_remote.dm:50
    public virtual async DmTask try_launch(mob_living user = null)
    {
        if (connected_botpad == null)
        {
            user?.balloon_alert(user, I18n.T("no connected pad!"));
            return;
        }
        if (Dm.truthy(connected_botpad.panel_open))
        {
            user?.balloon_alert(user, I18n.T("close the panel!"));
            return;
        }
        if (!Dm.truthy(Dm.locate_type(typeof(mob_living), get_turf(connected_botpad))))
        {
            user?.balloon_alert(user, I18n.T("no bots detected on the pad!"));
            return;
        }
        await connected_botpad.launch(user);
    }
}
