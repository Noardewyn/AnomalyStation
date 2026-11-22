// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states/default.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 3 (D148): the whole file except the pAI's override (/pai :49: with the pAI, silicons SI6); the cyborg's (:28) came with
// the cyborgs (silicons SI2, D326), the AI's (:39) with the AI (silicons SI4).

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/tgui/states/default.dm:13  GLOBAL_DATUM_INIT(default_state, ...) (GLOBAL_RAW, __DEFINES/__globals.dm:32)
    public datum_ui_state_default default_state;

    // tg: code/modules/tgui/states/default.dm:13  (GLOBAL_MANAGED(default_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldefault_state()
    {
        default_state = Dm.@new<datum_ui_state_default>();
        Dm.append(ref gvars_datum_init_order, "default_state");
    }
}

// tg: code/modules/tgui/states/default.dm:15
[DmPath("/datum/ui_state/default")]
public partial class datum_ui_state_default : datum_ui_state
{
    // tg: code/modules/tgui/states/default.dm:15
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        return user.default_can_use_topic(src_object); // Call the individual mob-overridden procs.
    }
}

public partial class mob
{
    // tg: code/modules/tgui/states/default.dm:18
    public virtual object default_can_use_topic(object src_object = null)
    {
        return UI_CLOSE; // Don't allow interaction by default.
    }
}

public partial class mob_living
{
    // tg: code/modules/tgui/states/default.dm:21
    public override object default_can_use_topic(object src_object = null)
    {
        object dot = shared_ui_interaction(src_object);
        if (Dm.num(dot) > UI_CLOSE && Dm.truthy(loc)) //must not be in nullspace.
            dot = Dm.min(dot, shared_living_ui_distance(src_object as atom_movable)); // Check the distance...
        if (Dm.eq(dot, UI_INTERACTIVE) && !ISADVANCEDTOOLUSER(this)) // unhandy living mobs can only look, not touch.
            return UI_UPDATE;
        return dot;
    }
}

// tg: code/modules/tgui/states/default.dm:28
public partial class mob_living_silicon_robot : mob_living_silicon
{
    // tg: code/modules/tgui/states/default.dm:28
    public override object default_can_use_topic(object src_object = null)
    {
        object dot = shared_ui_interaction(src_object);
        if (Dm.num(dot) <= UI_DISABLED)
            return dot;

        // Robots can interact with anything they can see.
        DmList clientviewlist = getviewsize(client.view) as DmList;
        if (get_dist(this, src_object) <= min(Dm.num(clientviewlist[1]), Dm.num(clientviewlist[2])))
            return UI_INTERACTIVE;
        return UI_DISABLED; // Otherwise they can keep the UI open.
    }
}

// tg: code/modules/tgui/states/default.dm:39  (silicons SI4)
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // tg: code/modules/tgui/states/default.dm:39
    public override object default_can_use_topic(object src_object = null)
    {
        object dot = shared_ui_interaction(src_object);
        if (Dm.num(dot) < UI_INTERACTIVE)
            return dot;

        // The AI can interact with anything it can see nearby, or with cameras while wireless control is enabled.
        if (!Dm.truthy(control_disabled) && Dm.truthy(can_see(src_object as atom)))
            return UI_INTERACTIVE;
        return UI_CLOSE;
    }
}

// tg: code/modules/tgui/states/default.dm:49  (silicons SI6)
public partial class mob_living_silicon_pai : mob_living_silicon
{
    // tg: code/modules/tgui/states/default.dm:49
    public override object default_can_use_topic(object src_object = null)
    {
        // pAIs can only use themselves and the owner's radio.
        if ((src_object == this || src_object == radio) && !Dm.truthy(stat))
            return UI_INTERACTIVE;
        else
            return min(Dm.num(base.default_can_use_topic(src_object)), UI_UPDATE);
    }
}
