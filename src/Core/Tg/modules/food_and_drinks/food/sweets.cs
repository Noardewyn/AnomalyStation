// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/sweets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the bubblegum (the nicotine and HP+ gums with it), the gumball and the lollipop came with the pinatas (syndicate
// N2b, D316), with the bubblegum gum that Bubblegum leaves after devouring a corpse; the spider and swirl lollipops' chewing with the
// restaurant. The other sweets keep the base. The var blocks are generated
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

// tg: code/modules/food_and_drinks/food/sweets.dm:233 (Bubblegum's gum, left by the megafauna's devouring)
public partial class obj_item_food_bubblegum_bubblegum : obj_item_food_bubblegum
{
    // tg: code/modules/food_and_drinks/food/sweets.dm:240
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (iscarbon(loc))
            Waiting.NoSleep(hallucinate(loc as mob_living_carbon));
        return null;
    }

    // tg: code/modules/food_and_drinks/food/sweets.dm:244
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("on_consume", Dm.@new<datum_callback>(this, nameof(OnConsume))));
    }

    // tg: code/modules/food_and_drinks/food/sweets.dm:248
    public virtual async DmTask OnConsume(mob_living eater = null, mob_living feeder = null)
    {
        if (iscarbon(eater))
            await hallucinate(eater as mob_living_carbon);
    }

    ///This proc has a 5% chance to have a bubblegum line appear, with an 85% chance for just text and 15% for a bubblegum hallucination and scarier text.
    // tg: code/modules/food_and_drinks/food/sweets.dm:253
    public virtual async DmTask hallucinate(mob_living_carbon victim = null)
    {
        if (prob(95)) //cursed by bubblegum
            return;
        if (prob(15))
            await victim.cause_hallucination(typeof(datum_hallucination_oh_yeah), "bubblegum bubblegum", Dm.named("haunt_them", TRUE));
        else
            to_chat(victim, span_warning(Dm.text("[]", pick(I18n.T("You hear faint whispers."), I18n.T("You smell ash."), I18n.T("You feel hot."), I18n.T("You hear a roar in the distance.")))));
    }

    // tg: code/modules/food_and_drinks/food/sweets.dm:261  (BUBBLEGUM_HALLUCINATION_LINES, __HELPERS/hallucinations.dm:296, expanded inline)
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        await user.say(Dm.text(";[]", pick("I AM IMMORTAL.", "I SHALL TAKE YOUR WORLD.", "I SEE YOU.", "YOU CANNOT ESCAPE ME FOREVER.", "NOTHING CAN HOLD ME.")), spans: Dm.list(SPAN_COLOSSUS));
        return await base.suicide_act(user);
    }
}

// tg: code/modules/food_and_drinks/food/sweets.dm:332
public partial class obj_item_food_spiderlollipop : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/sweets.dm:350
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_chewable)));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/sweets.dm:354
public partial class obj_item_food_swirl_lollipop : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/sweets.dm:371
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_chewable)));
        return dot;
    }
}
