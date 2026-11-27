// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/fugitive/hunters/hunter_gear.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the bouncy castle (gaps ST5, D333: the structures' leftovers): named after the mob it was made from, it leaves blood
// as it moves and spreads it to whoever touches it, and it squelches when hit; and the EVA crate with its three suits, helmets,
// masks and oxygen tanks (the bitrunning psyker shuffle's crates). The fugitive hunters' gear (the bounty locator, the
// psychic's and the bounty hunters' items) waits for the fugitive hunters (antagonists, not-translated.md 213). The var blocks are
// generated (D111).

// tg: code/modules/antagonists/fugitive/hunters/hunter_gear.dm:129
public partial class obj_structure_bouncy_castle : obj_structure
{
    // tg: code/modules/antagonists/fugitive/hunters/hunter_gear.dm:138
    [DmParams("mapload", "gored")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob gored = Dm.arg<mob>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (gored != null)
            name = Dm.str(gored.real_name);

        Waiting.NoSleep(AddComponent(
            typeof(datum_component_blood_walk),
            Dm.named("blood_type", typeof(obj_effect_decal_cleanable_blood)),
            Dm.named("blood_spawn_chance", 66.6f),
            Dm.named("max_blood", INFINITY)));

        Waiting.NoSleep(AddComponent(typeof(datum_component_bloody_spreader)));
        return dot;
    }

    // tg: code/modules/antagonists/fugitive/hunters/hunter_gear.dm:152
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        switch (damage_type)
        {
            case BRUTE:
                if (Dm.truthy(damage_amount))
                    playsound(this, Dm.resource("sound/effects/blob/attackblob.ogg"), 50, TRUE);
                else
                    playsound(this, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
                break;
            case BURN:
                playsound(this, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
                break;
        }
        return null;
    }
}

// tg: code/modules/antagonists/fugitive/hunters/hunter_gear.dm:51
public partial class obj_structure_closet_crate_eva : obj_structure_closet_crate
{
    // tg: code/modules/antagonists/fugitive/hunters/hunter_gear.dm:56
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        for (float i = 1; i <= 3; i++)
            Dm.@new<obj_item_clothing_suit_space_eva>(this);
        for (float i = 1; i <= 3; i++)
            Dm.@new<obj_item_clothing_head_helmet_space_eva>(this);
        for (float i = 1; i <= 3; i++)
            Dm.@new<obj_item_clothing_mask_breath>(this);
        for (float i = 1; i <= 3; i++)
            Dm.@new<obj_item_tank_internals_oxygen>(this);
    }
}
