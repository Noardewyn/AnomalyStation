// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/industrial.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the industrial extracts, which absorb the plasma injected into them a unit or two a tick and make their colour's
// item each time they have absorbed enough (its icon bubbles while it works). The var blocks are generated (D111).

/*
Industrial extracts:
	Slowly consume plasma, produce items with it.
*/
// tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:6
public partial class obj_item_slimecross_industrial : obj_item_slimecross
{
    // tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:15
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, I18n.T("It currently has [] units of plasma floating inside the outer shell, out of [] units.", plasmaabsorbed, plasmarequired));
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:19
    public virtual void do_after_spawn(obj_item spawned = null)
    {
    }

    // tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(100, Dm.bitor(INJECTABLE, DRAWABLE));
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:27
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:31
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        datum_reagents holder = (datum_reagents)reagents;
        bool IsWorking = false;
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_toxin_plasma), amount: 2)) && plasmarequired > 1) //Can absorb as much as 2
        {
            IsWorking = true;
            Waiting.NoSleep(holder.remove_reagent(typeof(datum_reagent_toxin_plasma), 2));
            plasmaabsorbed += 2;
        }
        else if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_toxin_plasma), amount: 1))) //Can absorb as little as 1
        {
            IsWorking = true;
            Waiting.NoSleep(holder.remove_reagent(typeof(datum_reagent_toxin_plasma), 1));
            plasmaabsorbed += 1;
        }

        if (plasmaabsorbed >= plasmarequired)
        {
            playsound(this, "sound/effects/blob/attackblob.ogg", 50, TRUE);
            plasmaabsorbed -= plasmarequired;
            for (int i = 1; i <= itemamount; i++)
                do_after_spawn(Dm.@new(itempath, get_turf(this)) as obj_item);
        }
        else if (IsWorking)
            playsound(this, "sound/effects/bubbles/bubbles.ogg", 5, TRUE);
        if (IsWorking)
            icon_state = "industrial";
        else
            icon_state = "industrial_still";
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:85
public partial class obj_item_slimecross_industrial_yellow : obj_item_slimecross_industrial
{
    // tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:90
    public override void do_after_spawn(obj_item spawned = null)
    {
        obj_item_stock_parts_power_store_cell_high C = spawned as obj_item_stock_parts_power_store_cell_high;
        if (C is obj_item_stock_parts_power_store_cell_high)
            C.charge = rand(0, Dm.num(C.maxcharge) / 2);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:102
public partial class obj_item_slimecross_industrial_darkblue : obj_item_slimecross_industrial
{
    // tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:107
    public override void do_after_spawn(obj_item spawned = null)
    {
        obj_item_slimepotion_fireproof potion = spawned as obj_item_slimepotion_fireproof;
        if (potion is obj_item_slimepotion_fireproof)
            potion.uses = 1;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:113
public partial class obj_item_slimecross_industrial_silver : obj_item_slimecross_industrial
{
    // tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:118
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        itempath = (Type)pick(Dm.list(get_random_food(), get_random_drink()));
        return base.process(seconds_per_tick, __extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:122
    public override void do_after_spawn(obj_item spawned = null)
    {
        ADD_TRAIT(spawned, TRAIT_FOOD_SILVER, INNATE_TRAIT);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:168
public partial class obj_item_slimecross_industrial_gold : obj_item_slimecross_industrial
{
    // tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:172
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        itempath = (Type)get_random_coin();
        return base.process(seconds_per_tick, __extra);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:201
public partial class obj_item_slimecross_industrial_rainbow : obj_item_slimecross_industrial
{
    // tg: code/modules/research/xenobiology/crossbreeding/industrial.dm:206
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        itempath = (Type)pick(subtypesof(typeof(obj_item_slime_extract)));
        return base.process(seconds_per_tick, __extra);
    }
}
