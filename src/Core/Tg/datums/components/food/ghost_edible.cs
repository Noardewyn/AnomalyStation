// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/food/ghost_edible.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3b (D349): the whole file: ghosts orbiting a food eat it a bite at a time. The var block is generated (D111).

/**
 * Allows ghosts to eat this by orbiting it
 * They do this by consuming the reagents in the object, so if it doesn't have any then it won't work
 */
// tg: code/datums/components/food/ghost_edible.dm:7
public partial class datum_component_ghost_edible : datum_component
{
    // tg: code/datums/components/food/ghost_edible.dm:15
    [DmParams("bite_consumption", "bite_chance", "minimum_scale")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object bite_consumption = Dm.arg(__extra, 0) ?? 3;
        object bite_chance = Dm.arg(__extra, 1) ?? 20;
        object minimum_scale = Dm.arg(__extra, 2) ?? 0.6f;
        object dot = await base.Initialize(__extra);
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;
        atom atom_parent = (atom)parent;
        datum_reagents parent_reagents = atom_parent.reagents as datum_reagents;
        if (parent_reagents == null || Dm.num(parent_reagents.total_volume) == 0)
            return COMPONENT_INCOMPATIBLE;
        this.bite_consumption = bite_consumption;
        this.bite_chance = bite_chance;
        this.minimum_scale = minimum_scale;
        initial_reagent_volume = Dm.num(parent_reagents.total_volume);

        string parent_name = Dm.text("[]", parent);
        if (ismob(parent))
        {
            mob mob_parent = (mob)parent;
            parent_name = Dm.text("[]", mob_parent.real_name);
        }
        notify_ghosts(
            I18n.T("[] is edible by ghosts!", parent_name),
            source: atom_parent,
            header: I18n.T("Something Tasty!"),
            notify_flags: NOTIFY_CATEGORY_NOFLASH);
        return dot;
    }

    // tg: code/datums/components/food/ghost_edible.dm:38
    public override async DmTask RegisterWithParent()
    {
        START_PROCESSING(SSdcs, this);
    }

    // tg: code/datums/components/food/ghost_edible.dm:41
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        STOP_PROCESSING(SSdcs, this);
    }

    // tg: code/datums/components/food/ghost_edible.dm:44
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSdcs, this);
        return base.Destroy(force);
    }

    // tg: code/datums/components/food/ghost_edible.dm:48
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        atom atom_parent = (atom)parent;
        // Ghosts can eat this burger
        float munch_chance = 0;
        foreach (mob_dead_observer ghost in Dm.each<mob_dead_observer>(atom_parent.orbiters?.orbiter_list))
        {
            munch_chance += Dm.num(bite_chance);
            if (munch_chance >= 100)
                break;
        }
        if (!prob(munch_chance))
            return null;
        playsound(atom_parent.loc as atom, Dm.resource("sound/items/eatfood.ogg"), rand(10, 50), TRUE);
        datum_reagents parent_reagents = (datum_reagents)atom_parent.reagents;
        Waiting.NoSleep(parent_reagents.remove_all(bite_consumption));
        if (Dm.num(parent_reagents.total_volume) <= 0)
        {
            atom_parent.visible_message(span_notice(I18n.T("[] disappears completely!", atom_parent)));
            Dm.@new<obj_item_ectoplasm>(atom_parent.loc);
            qdel(parent);
            return null;
        }

        float final_scale = LERP(Dm.num(minimum_scale), 1, Dm.num(parent_reagents.total_volume) / initial_reagent_volume);
        matrix final_transform = new matrix().Scale(final_scale);
        matrix animate_transform = Dm.matrix(final_transform).Scale(0.8f);
        Dm.animate(parent, Dm.named("transform", animate_transform), Dm.named("time", 0.1f * SECONDS));
        Dm.animate(Dm.named("transform", final_transform), Dm.named("time", 0.1f * SECONDS));
        return null;
    }
}
