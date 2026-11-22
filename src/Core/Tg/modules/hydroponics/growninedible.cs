// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/growninedible.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the inedible produce base, for the banana peel. Its seed is hand-declared (rule V3 types it Type).

// tg: code/modules/hydroponics/growninedible.dm:6
public partial class obj_item_grown : obj_item
{
    // tg: code/modules/hydroponics/growninedible.dm:11
    public object seed; // type path, gets converted to item on New(). It's safe to assume it's always a seed item.

    // tg: code/modules/hydroponics/growninedible.dm:6 (the hand-declared seed makes the class's var block hand-written: its overrides move here)
    public obj_item_grown()
    {
        name = "grown_weapon";
        icon = Dm.resource("icons/obj/service/hydroponics/harvest.dmi");
        worn_icon = Dm.resource("icons/mob/clothing/head/hydroponics.dmi");
        abstract_type = typeof(obj_item_grown);
        resistance_flags = 4;
    }

    // This may look like it's doing nothing but it's necessary, we do this to have kwargs work in New (for passing into Initialize)
    // tg: code/modules/hydroponics/growninedible.dm:18
    [DmParams("loc", "new_seed")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
    }

    // tg: code/modules/hydroponics/growninedible.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item_seeds new_seed = Dm.arg(__extra, 0) as obj_item_seeds;
        object dot = base.Initialize(mapload, __extra);
        create_reagents(100);

        if (new_seed != null)
        {
            seed = new_seed.Copy();
        }
        else if (ispath(seed))
        {
            // This is for adminspawn or map-placed growns. They get the default stats of their seed type.
            seed = Dm.@new((Type)seed);
            ((obj_item_seeds)seed).adjust_potency(50 - Dm.num(((obj_item_seeds)seed).potency));
        }

        if (Dm.truthy(offset_at_init))
        {
            pixel_x = base_pixel_x + rand(-5, 5);
            pixel_y = base_pixel_y + rand(-5, 5);
        }

        obj_item_seeds our_seed = seed as obj_item_seeds;
        if (our_seed != null)
        {
            // Go through all traits in their genes and call on_new_plant from them.
            foreach (datum_plant_gene_trait trait in Dm.each<datum_plant_gene_trait>(our_seed.genes))
                Waiting.NoSleep(trait.on_new_plant(this, loc));

            if (Dm.istype(this, our_seed.product)) // no adding reagents if it is just a trash item
                Waiting.NoSleep(our_seed.prepare_result(this));
            transform = Dm.mul(transform, Dm.num(Dm.pow(Dm.sin((90 * Dm.num(our_seed.potency)) / 100), 2)) + 0.5f);
            add_juice();
        }
        ADD_TRAIT(this, TRAIT_VALID_DNA_INFUSION, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/hydroponics/growninedible.dm:48
    public override object Destroy(float? force = null)
    {
        if (isatom(seed))
        {
            qdel(seed);
            seed = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/hydroponics/growninedible.dm:53
    public virtual object add_juice()
    {
        return reagents != null ? TRUE : FALSE;
    }
}
