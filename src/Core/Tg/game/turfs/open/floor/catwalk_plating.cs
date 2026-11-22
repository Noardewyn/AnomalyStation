// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/floor/catwalk_plating.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps G2 (D291): the whole file (the reskins below the base hold only vars).

/**
 * ## catwalk flooring
 *
 * They show what's underneath their catwalk flooring (pipes and the like)
 * you can screwdriver it to interact with the underneath stuff without destroying the tile...
 * unless you want to!
 */
// tg: code/game/turfs/open/floor/catwalk_plating.dm:8
public partial class turf_open_floor_catwalk_floor : turf_open_floor
{
    // tg: code/game/turfs/open/floor/catwalk_plating.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(underlays, mutable_appearance(icon, Dm.text("[]_below", catwalk_type), LOW_FLOOR_LAYER, this, FLOOR_PLANE));
        update_appearance();
        return dot;
    }

    // tg: code/game/turfs/open/floor/catwalk_plating.dm:28
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (Dm.truthy(covered))
            Dm.append(ref dot, span_notice(I18n.T("You can <b>unscrew</b> it to reveal the contents beneath.")));
        else
        {
            Dm.append(ref dot, span_notice(I18n.T("You can <b>screw</b> it to hide the contents beneath.")));
            Dm.append(ref dot, span_notice(I18n.T("There's a <b>small crack</b> on the edge of it.")));
        }
        return dot;
    }

    // tg: code/game/turfs/open/floor/catwalk_plating.dm:37
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.screwdriver_act(user, tool);
        covered = Dm.truthy(covered) ? FALSE : TRUE;
        if (!Dm.truthy(covered))
        {
            underfloor_accessibility = UNDERFLOOR_INTERACTABLE;
            layer = LOW_FLOOR_LAYER;
            icon_state = Dm.text("[]_below", catwalk_type);
            ADD_TRAIT(this, TRAIT_UNCOVERED_TURF, INNATE_TRAIT);
        }
        else
        {
            underfloor_accessibility = UNDERFLOOR_VISIBLE;
            icon_state = Dm.text("[]_above", catwalk_type);
            layer = Dm.num(Dm.initial(this, nameof(layer)));
            REMOVE_TRAIT(this, TRAIT_UNCOVERED_TURF, INNATE_TRAIT);
        }

        levelupdate();
        user.balloon_alert(user, !Dm.truthy(covered) ? I18n.T("cover removed") : I18n.T("cover added"));
        tool.play_tool_sound(this);
        update_appearance();
        return dot;
    }

    // tg: code/game/turfs/open/floor/catwalk_plating.dm:56
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(covered))
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_overlay", catwalk_type), CATWALK_LAYER, this, FLOOR_PLANE, appearance_flags: KEEP_APART));
        return dot;
    }

    // tg: code/game/turfs/open/floor/catwalk_plating.dm:61
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(covered))
        {
            user.balloon_alert(user, I18n.T("remove cover first!"));
            return FALSE;
        }
        return await base.crowbar_act(user, tool);
    }
}
