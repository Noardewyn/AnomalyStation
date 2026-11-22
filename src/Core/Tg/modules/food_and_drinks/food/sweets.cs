// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/sweets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate N2b, D316: the pinatas drop them): the bubblegum (the nicotine and HP+ gums with it), the gumball and the
// lollipop. The bubblegum gum (it needs the bubblegum hallucination) and the other sweets keep the base. The var blocks are generated
// (D111), except the lollipop's head below.

// tg: code/modules/food_and_drinks/food/sweets.dm:195
public partial class obj_item_food_bubblegum : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/sweets.dm:207
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] swallows []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        qdel(this);
        return TOXLOSS;
    }

    // tg: code/modules/food_and_drinks/food/sweets.dm:212
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_chewable)));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/sweets.dm:264
public partial class obj_item_food_gumball : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/sweets.dm:278
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        color = rgb(rand(0, 255), rand(0, 255), rand(0, 255));
        Waiting.NoSleep(AddElement(typeof(datum_element_chewable)));
        return dot;
    }
}

// Lollipop
// tg: code/modules/food_and_drinks/food/sweets.dm:285
public partial class obj_item_food_lollipop : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/sweets.dm:303  GAP(types): the generator types it by name, and other heads are paths, but this
    // one is the candy head's mutable appearance (:308), so the var is hand-written
    public mutable_appearance head;

    // tg: code/modules/food_and_drinks/food/sweets.dm:306
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        head = mutable_appearance(Dm.resource("icons/obj/food/lollipop.dmi"), "lollipop_head") as mutable_appearance;
        change_head_color(rgb(rand(0, 255), rand(0, 255), rand(0, 255)));
        Waiting.NoSleep(AddElement(typeof(datum_element_chewable)));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/sweets.dm:312
    public virtual void change_head_color(object C = null)
    {
        head_color = C as string;
        cut_overlay(head);
        head.color = C;
        add_overlay(head);
    }

    // tg: code/modules/food_and_drinks/food/sweets.dm:318
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        base.throw_impact(hit_atom);
        throw_speed = 1;
        throwforce = 0;
        return null;
    }
}
