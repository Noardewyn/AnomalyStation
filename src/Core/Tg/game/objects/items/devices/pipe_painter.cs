// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/pipe_painter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The pipe painter: a colour picked from tg's pipe paint colours, then any pipe or machine of the
// atmospherics (or a loose pipe fitting) clicked is painted with it. The var block is generated (D111).

// tg: code/game/objects/items/devices/pipe_painter.dm:1
public partial class obj_item_pipe_painter : obj_item
{
    // tg: code/game/objects/items/devices/pipe_painter.dm:12
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with is obj_machinery_atmospherics target_pipe)
        {
            await target_pipe.paint(GLOB.pipe_paint_colors[paint_color]);
            playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
            balloon_alert(user, I18n.T("painted in [] color", paint_color));
            return ITEM_INTERACT_SUCCESS;
        }

        if (interacting_with is obj_item_pipe target_pipe__1)
        {
            object color__1 = GLOB.pipe_paint_colors[paint_color];
            target_pipe__1.pipe_color = color__1;
            await target_pipe__1.add_atom_colour(color__1, FIXED_COLOUR_PRIORITY);
            playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
            balloon_alert(user, I18n.T("painted in [] color", paint_color));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/items/devices/pipe_painter.dm:31
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        paint_color = await tgui_input_list(user, I18n.T("Which colour do you want to use?"), I18n.T("Pipe painter"), GLOB.pipe_paint_colors) as string;
        return null;
    }

    // tg: code/game/objects/items/devices/pipe_painter.dm:34
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It is set to [].", paint_color)));
        return dot;
    }
}
