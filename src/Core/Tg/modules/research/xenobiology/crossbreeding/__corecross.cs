// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/__corecross.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the crossbred extract's base (its name and colour from its effect and slime colour) and the result extracts that
// hold a crossbreed's chemicals, the autoinjecting ones among them. The var blocks are generated (D111).

// tg: code/modules/research/xenobiology/crossbreeding/__corecross.dm:28
public partial class obj_item_slimecross : obj_item
{
    // tg: code/modules/research/xenobiology/crossbreeding/__corecross.dm:41
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(effect_desc))
            Dm.append(ref dot, span_notice(I18n.T(effect_desc)));
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/__corecross.dm:46
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = effect + " " + colour + " extract";
        string itemcolor = COLOR_WHITE;
        switch (colour)
        {
            case SLIME_TYPE_ORANGE:
                itemcolor = "#FFA500";
                break;
            case SLIME_TYPE_PURPLE:
                itemcolor = "#B19CD9";
                break;
            case SLIME_TYPE_BLUE:
                itemcolor = "#ADD8E6";
                break;
            case SLIME_TYPE_METAL:
                itemcolor = "#7E7E7E";
                break;
            case SLIME_TYPE_YELLOW:
                itemcolor = COLOR_YELLOW;
                break;
            case SLIME_TYPE_DARK_PURPLE:
                itemcolor = COLOR_DARK_PURPLE;
                break;
            case SLIME_TYPE_DARK_BLUE:
                itemcolor = COLOR_BLUE;
                break;
            case SLIME_TYPE_SILVER:
                itemcolor = "#D3D3D3";
                break;
            case SLIME_TYPE_BLUESPACE:
                itemcolor = COLOR_LIME;
                break;
            case SLIME_TYPE_SEPIA:
                itemcolor = "#704214";
                break;
            case SLIME_TYPE_CERULEAN:
                itemcolor = "#2956B2";
                break;
            case SLIME_TYPE_PYRITE:
                itemcolor = "#FAFAD2";
                break;
            case SLIME_TYPE_RED:
                itemcolor = COLOR_RED;
                break;
            case SLIME_TYPE_GREEN:
                itemcolor = COLOR_VIBRANT_LIME;
                break;
            case SLIME_TYPE_PINK:
                itemcolor = "#FF69B4";
                break;
            case SLIME_TYPE_GOLD:
                itemcolor = COLOR_GOLD;
                break;
            case SLIME_TYPE_OIL:
                itemcolor = "#505050";
                break;
            case SLIME_TYPE_BLACK:
                itemcolor = COLOR_BLACK;
                break;
            case SLIME_TYPE_LIGHT_PINK:
                itemcolor = "#FFB6C1";
                break;
            case SLIME_TYPE_ADAMANTINE:
                itemcolor = "#008B8B";
                break;
        }
        Waiting.NoSleep(add_atom_colour(itemcolor, FIXED_COLOUR_PRIORITY));
        return dot;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/__corecross.dm:94
public partial class obj_item_slimecrossbeaker : obj_item
{
    // tg: code/modules/research/xenobiology/crossbreeding/__corecross.dm:101
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(50, Dm.bitor(Dm.bitor(INJECTABLE, DRAWABLE), SEALED_CONTAINER));
        if (Dm.truthy(list_reagents))
        {
            foreach (object reagent in Dm.each(list_reagents))
                Waiting.NoSleep(((datum_reagents)reagents).add_reagent(reagent, list_reagents[reagent]));
        }
        if (Dm.truthy(del_on_empty))
            START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/__corecross.dm:110
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/__corecross.dm:114
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(((datum_reagents)reagents).total_volume))
        {
            visible_message(span_notice(I18n.T("[] has been drained completely, and melts away.", this)));
            qdel(this);
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/__corecross.dm:138
public partial class obj_item_slimecrossbeaker_autoinjector : obj_item_slimecrossbeaker
{
    // tg: code/modules/research/xenobiology/crossbreeding/__corecross.dm:141
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ((datum_reagents)reagents).flags = DRAWABLE; // Cannot be refilled, since it's basically an autoinjector!
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/__corecross.dm:145
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!iscarbon(interacting_with))
            return NONE;
        mob_living_carbon injecting_mob = (mob_living_carbon)interacting_with;
        datum_reagents holder = (datum_reagents)reagents;
        if (!Dm.truthy(holder.total_volume))
        {
            to_chat(user, span_warning(I18n.T("[] is empty!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(self_use_only) && injecting_mob != user)
        {
            to_chat(user, span_warning(I18n.T("This can only be used on yourself.")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(holder.total_volume) && (Dm.truthy(ignore_flags) || Dm.truthy(injecting_mob.try_inject(user, injection_flags: INJECT_TRY_SHOW_ERROR_MESSAGE))))
        {
            await holder.trans_to(injecting_mob, Dm.num(holder.total_volume), transferred_by: user);
            if (user != injecting_mob)
            {
                to_chat(injecting_mob, span_warning(I18n.T("[] presses [] against you!", user, this)));
                to_chat(user, span_notice(I18n.T("You press [] against [], injecting [].", this, injecting_mob, injecting_mob.p_them())));
            }
            else
                to_chat(user, span_notice(I18n.T("You press [] against yourself, and it flattens against you!", this)));
            return ITEM_INTERACT_SUCCESS;
        }
        else
        {
            to_chat(user, span_warning(I18n.T("There's no place to stick []!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/__corecross.dm:183
public partial class obj_item_slimecrossbeaker_autoinjector_peaceandlove : obj_item_slimecrossbeaker_autoinjector
{
    // tg: code/modules/research/xenobiology/crossbreeding/__corecross.dm:188
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ((datum_reagents)reagents).flags = NONE; // It won't be *that* easy to get your hands on pax.
        return dot;
    }
}
