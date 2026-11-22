// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/engineering/painter/airlock_painter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but suicide_act (the suicide verb) and examine (the ink level, D264), both live-partial.txt E14 (engineering E14).
// The paint jobs themselves are the airlock's change_paintjob (airlock.dm:1497) and the closet's (closets.dm:699). The var block is
// generated.

// tg: code/game/objects/items/tools/engineering/painter/airlock_painter.dm:1
public partial class obj_item_airlock_painter : obj_item
{
    // tg: code/game/objects/items/tools/engineering/painter/airlock_painter.dm:42
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ink = Dm.@new(initial_ink_type, this) as obj_item_toner;
        return dot;
    }

    // tg: code/game/objects/items/tools/engineering/painter/airlock_painter.dm:46
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref ink);
        return base.Destroy(force);
    }

    //This proc doesn't just check if the painter can be used, but also uses it.
    //Only call this if you are certain that the painter will be used right after this check!
    // tg: code/game/objects/items/tools/engineering/painter/airlock_painter.dm:52
    public virtual object use_paint(mob user = null)
    {
        if (Dm.truthy(can_use(user)))
        {
            ink.charges--;
            playsound(loc as atom, Dm.resource("sound/effects/spray2.ogg"), 50, TRUE);
            return TRUE;
        }
        else
            return FALSE;
    }

    //This proc only checks if the painter can be used.
    //Call this if you don't want the painter to be used right after this check, for example
    //because you're expecting user input.
    // tg: code/game/objects/items/tools/engineering/painter/airlock_painter.dm:63
    public virtual object can_use(mob user = null)
    {
        if (ink == null)
        {
            balloon_alert(user, I18n.T("no cartridge!"));
            return FALSE;
        }
        else if (ink.charges < 1)
        {
            balloon_alert(user, I18n.T("out of ink!"));
            return FALSE;
        }
        else
            return TRUE;
    }

    // tg: code/game/objects/items/tools/engineering/painter/airlock_painter.dm:73  suicide_act: live-partial (the suicide verb).
    // tg: code/game/objects/items/tools/engineering/painter/airlock_painter.dm:120
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (ink == null)
        {
            Dm.append(ref dot, span_notice(I18n.T("It doesn't have a toner cartridge installed.")));
            return dot;
        }
        string ink_level = I18n.T("high");
        if (ink.charges < 1)
            ink_level = I18n.T("empty");
        else if ((ink.charges / ink.max_charges) <= 0.25f) //25%
            ink_level = I18n.T("low");
        else if ((ink.charges / ink.max_charges) > 1) //Over 100% (admin var edit)
            ink_level = I18n.T("dangerously high");
        Dm.append(ref dot, span_notice(I18n.T("Its ink levels look [].", ink_level)));
        return dot;
    }

    // tg: code/game/objects/items/tools/engineering/painter/airlock_painter.dm:134
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_toner)))
            return NONE;

        if (ink != null)
        {
            to_chat(user, span_warning(I18n.T("[] already contains \\a []!", this, ink)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You install [] into [].", tool, this)));
        ink = tool as obj_item_toner;
        playsound(loc as atom, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/tools/engineering/painter/airlock_painter.dm:150
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (ink == null)
            return CLICK_ACTION_BLOCKING;

        playsound(loc as atom, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        await ink.forceMove(user.drop_location() as atom);
        await user.put_in_hands(ink);
        to_chat(user, span_notice(I18n.T("You remove [] from [].", ink, this)));
        ink = null;
        return CLICK_ACTION_SUCCESS;
    }
}
