// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/objects/gimmick_disks/dungeon_disk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file. The loadouts and their clothes are var blocks (generated, D111); the alchemist's bottles fill
// themselves. The wizard's Untie Digi-Shoes is a var-only child of the wizard's untie shoes spell (spells/pointed/tie_shoes.dm, not
// ported: the wizard, D333).

// tg: code/modules/bitrunning/objects/gimmick_disks/dungeon_disk.dm:30
public partial class obj_item_reagent_containers_cup_bottle_alchemist_basic : obj_item_reagent_containers_cup_bottle
{
    // tg: code/modules/bitrunning/objects/gimmick_disks/dungeon_disk.dm:33
    public override async DmTask add_initial_reagents()
    {
        /// List of possible reagents we may pick from
        DmList possible_reagents = Dm.list(
            typeof(datum_reagent_aluminium), // Basic chems
            typeof(datum_reagent_bromine),
            typeof(datum_reagent_carbon),
            typeof(datum_reagent_chlorine),
            typeof(datum_reagent_copper),
            typeof(datum_reagent_consumable_ethanol),
            typeof(datum_reagent_fluorine),
            typeof(datum_reagent_hydrogen),
            typeof(datum_reagent_iodine),
            typeof(datum_reagent_iron),
            typeof(datum_reagent_lithium),
            typeof(datum_reagent_mercury),
            typeof(datum_reagent_nitrogen),
            typeof(datum_reagent_oxygen),
            typeof(datum_reagent_phosphorus),
            typeof(datum_reagent_potassium),
            typeof(datum_reagent_uranium_radium),
            typeof(datum_reagent_silicon),
            typeof(datum_reagent_sodium),
            typeof(datum_reagent_stable_plasma),
            typeof(datum_reagent_consumable_sugar),
            typeof(datum_reagent_sulfur),
            typeof(datum_reagent_toxin_acid),
            typeof(datum_reagent_water),
            typeof(datum_reagent_fuel),
            typeof(datum_reagent_toxin_leadacetate), // Funny chems
            typeof(datum_reagent_consumable_liquidgibs),
            typeof(datum_reagent_consumable_nutriment),
            typeof(datum_reagent_consumable_coffee));

        object our_reagent = pick(possible_reagents);
        await ((datum_reagents)reagents).add_reagent(our_reagent, 50);
    }
}

// tg: code/modules/bitrunning/objects/gimmick_disks/dungeon_disk.dm:71
public partial class obj_item_reagent_containers_cup_bottle_alchemist_random : obj_item_reagent_containers_cup_bottle
{
    /// List of random adjectives this bottle may have
    // tg: code/modules/bitrunning/objects/gimmick_disks/dungeon_disk.dm:76  var/static/list/possible_adjectives (rules 10.6)
    private static DmList possible_adjectives => TgState.Current.obj_item_reagent_containers_cup_bottle_alchemist_random__possible_adjectives;

    // tg: code/modules/bitrunning/objects/gimmick_disks/dungeon_disk.dm:98
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = Dm.text("[] bottle", pick(possible_adjectives));
        return dot;
    }

    // tg: code/modules/bitrunning/objects/gimmick_disks/dungeon_disk.dm:102
    public override async DmTask add_initial_reagents()
    {
        object our_reagent = get_random_reagent_id();
        float our_amount = rand(20, 50);
        await ((datum_reagents)reagents).add_reagent(our_reagent, our_amount);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/bitrunning/objects/gimmick_disks/dungeon_disk.dm:76
    internal DmList obj_item_reagent_containers_cup_bottle_alchemist_random__possible_adjectives = Dm.list(
        "unlabeled",
        "skull-labeled", // Labels
        "heart-labeled",
        "explosion-labeled",
        "fish-labeled",
        "smiley-labeled",
        "frown-labeled",
        "interrobang-labeled",
        "plus-labeled",
        "d20-labeled",
        "unreadably-labeled",
        "black-labeled",
        "empty-labeled",
        "age-yellowed", // Qualities
        "blood-tinged",
        "ash-marred",
        "claw-scratched",
        "marker-marked",
        "cracked",
        "ominous"); // Abstract
}
