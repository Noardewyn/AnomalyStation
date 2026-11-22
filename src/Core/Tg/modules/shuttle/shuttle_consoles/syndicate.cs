// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/shuttle_consoles/syndicate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1b, D316: the infiltrator's terminals and the assault pod's control; N1c: the navigation computer that picks
// a custom landing spot, camera_advanced/shuttle_docker/syndicate, whose procs are navigation_computer.cs's). The var blocks are generated
// (D111).

// tg: code/modules/shuttle/shuttle_consoles/syndicate.dm:3
public partial class obj_machinery_computer_shuttle_syndicate : obj_machinery_computer_shuttle
{
    // tg: code/modules/shuttle/shuttle_consoles/syndicate.dm:1  #define SYNDICATE_CHALLENGE_TIMER (20 MINUTES)
    private const float SYNDICATE_CHALLENGE_TIMER = 20 * MINUTES;

    // tg: code/modules/shuttle/shuttle_consoles/syndicate.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_SCREWDRIVER, TOOL_ACT_PRIMARY));
        return dot;
    }

    // tg: code/modules/shuttle/shuttle_consoles/syndicate.dm:20
    public override async DmTask<object> launch_check(mob user = null)
    {
        object dot = await base.launch_check(user);
        if (!Dm.truthy(dot))
            return FALSE;
        var board = circuit_value as obj_item_circuitboard_computer_syndicate_shuttle; // (the board instance: _machinery.cs circuit_value)
        if (board != null && Dm.truthy(board.challenge_start_time) && world.time < Dm.num(board.challenge_start_time) + SYNDICATE_CHALLENGE_TIMER)
        {
            to_chat(user, span_warning(I18n.T("You've issued a combat challenge to the station! You've got to give them at least [] more to allow them to prepare.",
                DisplayTimeText(Dm.num(board.challenge_start_time) + SYNDICATE_CHALLENGE_TIMER - world.time))));
            return FALSE;
        }
        board.moved = TRUE;
        return TRUE;
    }
}

// tg: code/modules/shuttle/shuttle_consoles/syndicate.dm:31
public partial class obj_machinery_computer_shuttle_syndicate_recall : obj_machinery_computer_shuttle_syndicate
{
}

// tg: code/modules/shuttle/shuttle_consoles/syndicate.dm:36
public partial class obj_machinery_computer_shuttle_syndicate_drop_pod : obj_machinery_computer_shuttle_syndicate
{
    // tg: code/modules/shuttle/shuttle_consoles/syndicate.dm:48
    public override async DmTask<object> launch_check(mob user = null)
    {
        object dot = await base.launch_check(user);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!Dm.truthy(is_reserved_level(z)))
        {
            to_chat(user, span_warning(I18n.T("Pods are one way!")));
            return FALSE;
        }
        return TRUE;
    }
}
