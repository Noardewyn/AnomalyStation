// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grafts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file. `plant_dna` is hand-declared (rule V3 types it Type; tg stores the graft's seed in it). The
// screentip element is left out (D264).

/**
 *A new subsystem for hydroponics, as a way to share specific traits into plants, as a way to phase out the DNA manipulator.
 */
// tg: code/modules/hydroponics/grafts.dm:4
public partial class obj_item_graft : obj_item
{
    /// Our internal seed used to modify stats or genes by grafting or passed as our seed when propagating vegetatively,
    // tg: code/modules/hydroponics/grafts.dm:14  (hand-declared: generated as a path, rule V3)
    public object plant_dna = typeof(obj_item_seeds_apple);

    // tg: code/modules/hydroponics/grafts.dm:5  (the generated constructor, kept with the hand-declared var: types-converter.md 3.2)
    public obj_item_graft()
    {
        name = "plant graft";
        desc = "A carefully cut graft off of a freshly grown plant. Can be grafted onto a plant in order to share unique plant traits onto a plant.";
        w_class = 1;
        icon = Dm.resource("icons/obj/service/hydroponics/equipment.dmi");
        icon_state = "graft_plant";
        worn_icon_state = "graft";
    }

    // tg: code/modules/hydroponics/grafts.dm:11
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        attack_verb_continuous = Dm.list("plants", "vegitizes", "crops", "reaps", "farms"); // tg: code/modules/hydroponics/grafts.dm:11
        attack_verb_simple = Dm.list("plant", "vegitize", "crop", "reap", "farm"); // tg: code/modules/hydroponics/grafts.dm:12
    }

    // tg: code/modules/hydroponics/grafts.dm:16
    [DmParams("mapload", "mother_plant")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object mother_plant = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        //create our "plant dna" internal seed from the plant the cuttings are taken from.
        if (mother_plant != null)
        {
            if (ispath(mother_plant))
            {
                plant_dna = Dm.@new((Type)mother_plant);
                if (plant_dna is not obj_item_seeds)
                    throw Dm.CRASH("Tried to create a graft using a non-seed path.", "code/modules/hydroponics/grafts.dm", 23);
            }
            else if (mother_plant is obj_item_seeds mother_seed)
            {
                plant_dna = mother_seed.Copy();
            }
            else
            {
                throw Dm.CRASH("Tried to create a graft using a non-seed reference.", "code/modules/hydroponics/grafts.dm", 27);
            }
        }
        icon_state = pick_weight(Dm.list(Dm.kv("graft_plant", 10), Dm.kv("graft_flower", 5), Dm.kv("graft_mushroom", 4), Dm.kv("graft_doom", 1))) as string;

        // a graft made without a mother plant keeps the apple seed's path, and tg reads plantname of that path: a runtime in tg
        if (plant_dna is obj_item_seeds dna_seed)
            name += Dm.text(" ([])", dna_seed.plantname);

        // var/static/list/hovering_item_typechecks; AddElement(/datum/element/contextual_screentip_item_typechecks, ...): the context tips (D264)
        return dot;
    }
}
