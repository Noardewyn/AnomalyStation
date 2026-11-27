// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/vatgrowing/petri_dish.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated (D111).

///Holds a biological sample which can then be put into the growing vat
// tg: code/modules/research/xenobiology/vatgrowing/petri_dish.dm:2
public partial class obj_item_petri_dish : obj_item
{
    // tg: code/modules/research/xenobiology/vatgrowing/petri_dish.dm:12
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_NULL(ref sample);
        return dot;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/petri_dish.dm:16
    public override async DmTask<object> vv_edit_var(object vname = null, object vval = null)
    {
        object dot = await base.vv_edit_var(vname, vval);
        if (Dm.eq(vname, nameof(sample)))
            update_appearance();
        return dot;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/petri_dish.dm:21
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (sample == null)
            return dot;
        Dm.append(ref dot, span_notice(I18n.T("You can see the following micro-organisms:")));
        foreach (object i in Dm.each(sample.micro_organisms))
        {
            datum_micro_organism MO = i as datum_micro_organism;
            Dm.append(ref dot, MO.get_details());
        }
        return dot;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/petri_dish.dm:30
    public override async DmTask<object> wash(object clean_types = null, params object[] __extra)
    {
        object dot = await base.wash(clean_types, __extra);
        sample = null;
        update_appearance();
        return dot;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/petri_dish.dm:35
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (sample == null)
            return dot;
        string reagentcolor = sample.sample_color;
        mutable_appearance base_overlay = mutable_appearance(icon, "petri_dish_overlay", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
        base_overlay.color = reagentcolor;
        Dm.append(ref dot, base_overlay);
        object overlay2 = mutable_appearance(icon, "petri_dish_overlay2");
        Dm.append(ref dot, overlay2);
        return dot;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/petri_dish.dm:46
    public virtual void deposit_sample(object user = null, datum_biological_sample deposited_sample = null)
    {
        sample = deposited_sample;
        to_chat(user, span_notice(I18n.T("You deposit a sample into [].", this)));
        update_appearance();
    }
}

/// Petri dish with random sample already in it.
// tg: code/modules/research/xenobiology/vatgrowing/petri_dish.dm:59
public partial class obj_item_petri_dish_random : obj_item_petri_dish
{
    // tg: code/modules/research/xenobiology/vatgrowing/petri_dish.dm:60  var/static/list/possible_samples (rules 10.6)
    public static ref DmList possible_samples => ref TgState.Current.obj_item_petri_dish_random__possible_samples;

    // tg: code/modules/research/xenobiology/vatgrowing/petri_dish.dm:61
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList chosen = pick(possible_samples) as DmList;
        sample = Dm.@new<datum_biological_sample>();
        Waiting.NoSleep(sample.GenerateSample(chosen[1], chosen[2], chosen[3], chosen[4]));
        update_appearance();
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/research/xenobiology/vatgrowing/petri_dish.dm:60  /obj/item/petri_dish/random's var/static/list/possible_samples
    internal DmList obj_item_petri_dish_random__possible_samples = Dm.list(
        Dm.list(CELL_LINE_TABLE_CORGI, CELL_VIRUS_TABLE_GENERIC_MOB, 1, 5),
        Dm.list(CELL_LINE_TABLE_SNAKE, CELL_VIRUS_TABLE_GENERIC_MOB, 1, 5),
        Dm.list(CELL_LINE_TABLE_COCKROACH, CELL_VIRUS_TABLE_GENERIC_MOB, 1, 7),
        Dm.list(CELL_LINE_TABLE_BLOBBERNAUT, CELL_VIRUS_TABLE_GENERIC_MOB, 1, 5)
    );
}
