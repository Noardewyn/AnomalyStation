// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/engineering/screwdriver.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the base screwdriver's Initialize (M4a P4a); suicide_act is a live-partial stub (M10); the abductor screwdriver comes after
// the core version; the hand drill is gaps J5's.

// tg: code/game/objects/items/tools/engineering/screwdriver.dm:1
public partial class obj_item_screwdriver : obj_item
{
    // tg: code/game/objects/items/tools/engineering/screwdriver.dm:41
    internal static ref DmList screwdriver_colors => ref TgState.Current.obj_item_screwdriver__screwdriver_colors;

    // tg: code/game/objects/items/tools/engineering/screwdriver.dm:55  suicide_act: live-partial (M10).

    // tg: code/game/objects/items/tools/engineering/screwdriver.dm:59
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(random_color))
        {
            // set_greyscale(colors = list(pick(screwdriver_colors))) (game/atom/atom_greyscale.dm:10)
            // GAP(lead): /atom/proc/set_greyscale is not declared (atom_greyscale.cs: "translated with clothing and dyes");
            // its body for a one-colour list and no new config, inline, until it is (request in p4a.json).
            DmList colors = Dm.list(pick(screwdriver_colors));
            object joined = colors.Join("");
            if (!Dm.eq(greyscale_colors, joined)) // If you want to disable greyscale stuff then give a blank string
                greyscale_colors = joined;
            Waiting.NoSleep(update_greyscale());
        }
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_eyestab)));
        Dm.Deferred("code/game/objects/items/tools/engineering/screwdriver.dm:64", "Initialize: AddElement(/datum/element/falling_hazard, ...), the screwdriver falling on a head (falls, M5)");
        return dot;
    }
}

// tg: code/game/objects/items/tools/engineering/screwdriver.dm:41  var/static/list/screwdriver_colors (rules 10.6)
public sealed partial class TgState
{
    internal DmList obj_item_screwdriver__screwdriver_colors = Dm.list(
        COLOR_TOOL_BLUE,
        COLOR_TOOL_RED,
        COLOR_TOOL_PINK,
        COLOR_TOOL_BROWN,
        COLOR_TOOL_GREEN,
        COLOR_TOOL_CYAN,
        COLOR_TOOL_YELLOW
    );
}

// tg: code/game/objects/items/tools/engineering/screwdriver.dm:86  (gaps J5, D298)
public partial class obj_item_screwdriver_power : obj_item_screwdriver
{
    // tg: code/game/objects/items/tools/engineering/screwdriver.dm:114
    public override object get_all_tool_behaviours() => Dm.list(TOOL_SCREWDRIVER, TOOL_WRENCH);

    // tg: code/game/objects/items/tools/engineering/screwdriver.dm:117
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_transforming),
            Dm.named("force_on", force),
            Dm.named("throwforce_on", throwforce),
            Dm.named("hitsound_on", hitsound),
            Dm.named("w_class_on", w_class),
            Dm.named("clumsy_check", FALSE),
            Dm.named("inhand_icon_change", FALSE)));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return dot;
    }

    /*
     * Signal proc for [COMSIG_TRANSFORMING_ON_TRANSFORM].
     *
     * Toggles between crowbar and wirecutters and gives feedback to the user.
     */
    // tg: code/game/objects/items/tools/engineering/screwdriver.dm:135
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        tool_behaviour = Dm.truthy(active) ? TOOL_WRENCH : TOOL_SCREWDRIVER;
        if (user != null)
            balloon_alert(user, I18n.T("attached []", Dm.truthy(active) ? "bolt bit" : "screw bit"));
        playsound(this, Dm.resource("sound/items/tools/change_drill.ogg"), 50, TRUE);
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }

    // tg: code/game/objects/items/tools/engineering/screwdriver.dm:144
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(I18n.T("It's fitted with a [] bit.", Dm.eq(tool_behaviour, TOOL_SCREWDRIVER) ? "screw" : "bolt"));
        return dot;
    }

    // tg: code/game/objects/items/tools/engineering/screwdriver.dm:148
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (Dm.eq(tool_behaviour, TOOL_SCREWDRIVER))
            user.visible_message(span_suicide(I18n.T("[] is putting [] to [] temple. It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        else
            user.visible_message(span_suicide(I18n.T("[] is pressing [] against [] head! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        playsound(loc, Dm.resource("sound/items/tools/drill_use.ogg"), 50, TRUE, -1);
        return BRUTELOSS;
    }
}
