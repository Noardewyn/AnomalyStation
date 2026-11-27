// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/sources/subtypes/mining_ruins.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the autowiki's tables (live-partial rows): the cursed spring, the chasms with their detritus, the lava
// and plasma vents that want a lava-proof line, the oil wells. The var blocks are generated (D111).

// tg: code/modules/fishing/sources/subtypes/mining_ruins.dm:19
public partial class datum_fish_source_chasm : datum_fish_source
{
    // tg: code/modules/fishing/sources/subtypes/mining_ruins.dm:32
    public override void on_start_fishing(obj_item_fishing_rod rod = null, mob fisherman = null, atom parent = null)
    {
        base.on_start_fishing(rod, fisherman, parent);
        if (istype(rod.hook_value, typeof(obj_item_fishing_hook_rescue)))
        {
            to_chat(fisherman, span_notice(I18n.T("The rescue hook falls straight down the chasm! Hopefully it catches a corpse.")));
            return;
        }
        to_chat(fisherman, span_danger(I18n.T("Your fishing hook makes a soft 'thud' noise as it gets stuck on the wall of the chasm. It doesn't look like it's going to catch much of anything, except maybe some detritus.")));
    }

    // tg: code/modules/fishing/sources/subtypes/mining_ruins.dm:39
    public override async DmTask<object> roll_reward(obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null)
    {
        object rolled_reward = await base.roll_reward(rod, fisherman, location);

        if (rod.hook_value == null || !ispath(rolled_reward, typeof(datum_chasm_detritus)))
            return rolled_reward;

        return rod.hook_value.chasm_detritus_type;
    }
}

// tg: code/modules/fishing/sources/subtypes/mining_ruins.dm:47
public partial class datum_fish_source_lavaland : datum_fish_source
{
    // tg: code/modules/fishing/sources/subtypes/mining_ruins.dm:79
    public override object reason_we_cant_fish(obj_item_fishing_rod rod = null, mob fisherman = null, atom parent = null)
    {
        object dot = base.reason_we_cant_fish(rod, fisherman, parent);
        if (!HAS_TRAIT(rod, TRAIT_ROD_LAVA_USABLE))
            return I18n.T("You'll need reinforced fishing line to fish in there.");
        return dot;
    }
}
