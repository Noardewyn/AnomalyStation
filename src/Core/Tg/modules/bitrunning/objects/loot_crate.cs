// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/objects/loot_crate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the domain's encrypted cache (it never unlocks) and the decrypted cache the byteforge prints,
// with the domain's completion loot and the ores by its reward points. The var blocks are generated (D111).

/// The virtual domain - side of the bitrunning crate. Deliver to the send location.
// tg: code/modules/bitrunning/objects/loot_crate.dm:19
public partial class obj_structure_closet_crate_secure_bitrunning_encrypted : obj_structure_closet_crate_secure_bitrunning
{
    // tg: code/modules/bitrunning/objects/loot_crate.dm:25
    public override object can_unlock(mob_living user = null, obj_item_card_id player_id = null, obj_item_card_id registered_id = null)
    {
        return FALSE;
    }
}

/// The bitrunner den - side of the bitrunning crate. Appears in the receive location.
// tg: code/modules/bitrunning/objects/loot_crate.dm:30
public partial class obj_structure_closet_crate_secure_bitrunning_decrypted : obj_structure_closet_crate_secure_bitrunning
{
    // tg: code/modules/bitrunning/objects/loot_crate.dm:1  (#undef at :89-97)
    private const float ORE_MULTIPLIER_IRON = 3;
    private const float ORE_MULTIPLIER_GLASS = 2;
    private const float ORE_MULTIPLIER_PLASMA = 1;
    private const float ORE_MULTIPLIER_SILVER = 0.7f;
    private const float ORE_MULTIPLIER_GOLD = 0.6f;
    private const float ORE_MULTIPLIER_TITANIUM = 0.5f;
    private const float ORE_MULTIPLIER_URANIUM = 0.4f;
    private const float ORE_MULTIPLIER_DIAMOND = 0.3f;
    private const float ORE_MULTIPLIER_BLUESPACE_CRYSTAL = 0.2f;

    // tg: code/modules/bitrunning/objects/loot_crate.dm:34
    [DmParams("mapload", "completed_domain", "rewards_multiplier")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_lazy_template_virtual_domain completed_domain = Dm.arg(__extra, 0) as datum_lazy_template_virtual_domain;
        object rewards_multiplier = Dm.arg(__extra, 1) ?? 1;
        object dot = base.Initialize(mapload, __extra);
        playsound(this, Dm.resource("sound/effects/magic/blink.ogg"), 50, TRUE);

        if (completed_domain == null)
            return dot;

        Waiting.NoSleep(PopulateContents(completed_domain.reward_points, completed_domain.completion_loot, rewards_multiplier));
        return dot;
    }

    // tg: code/modules/bitrunning/objects/loot_crate.dm:47
    [DmParams("reward_points", "completion_loot", "rewards_multiplier")]
    public override async DmTask PopulateContents(params object[] __extra)
    {
        object reward_points = Dm.arg(__extra, 0);
        DmList completion_loot = Dm.arg(__extra, 1) as DmList;
        object rewards_multiplier = Dm.arg(__extra, 2);
        await base.PopulateContents(__extra);
        await spawn_loot(completion_loot);

        await Dm.new_async<obj_item_stack_ore_iron>(this, calculate_loot(reward_points, rewards_multiplier, ORE_MULTIPLIER_IRON));
        await Dm.new_async<obj_item_stack_ore_glass>(this, calculate_loot(reward_points, rewards_multiplier, ORE_MULTIPLIER_GLASS));

        if (Dm.num(reward_points) > 1)
        {
            await Dm.new_async<obj_item_stack_ore_silver>(this, calculate_loot(reward_points, rewards_multiplier, ORE_MULTIPLIER_SILVER));
            await Dm.new_async<obj_item_stack_ore_titanium>(this, calculate_loot(reward_points, rewards_multiplier, ORE_MULTIPLIER_TITANIUM));
        }

        if (Dm.num(reward_points) > 2)
        {
            await Dm.new_async<obj_item_stack_ore_plasma>(this, calculate_loot(reward_points, rewards_multiplier, ORE_MULTIPLIER_PLASMA));
            await Dm.new_async<obj_item_stack_ore_gold>(this, calculate_loot(reward_points, rewards_multiplier, ORE_MULTIPLIER_GOLD));
            await Dm.new_async<obj_item_stack_ore_uranium>(this, calculate_loot(reward_points, rewards_multiplier, ORE_MULTIPLIER_URANIUM));
        }

        if (Dm.num(reward_points) > 3)
        {
            await Dm.new_async<obj_item_stack_ore_diamond>(this, calculate_loot(reward_points, rewards_multiplier, ORE_MULTIPLIER_DIAMOND));
            await Dm.new_async<obj_item_stack_ore_bluespace_crystal>(this, calculate_loot(reward_points, rewards_multiplier, ORE_MULTIPLIER_BLUESPACE_CRYSTAL));
        }
    }

    /// Handles generating random numbers & calculating loot totals
    // tg: code/modules/bitrunning/objects/loot_crate.dm:68
    [DmPrivate]
    public virtual object calculate_loot(object reward_points = null, object rewards_multiplier = null, object ore_multiplier = null)
    {
        // PRIVATE_PROC(TRUE)
        float @base = Dm.num(rewards_multiplier) + Dm.num(reward_points);
        float random_sum = (rand() + 0.5f) * @base;
        return Math.Max(ROUND_UP(random_sum * Dm.num(ore_multiplier)), 1);
    }

    /// Handles spawning completion loot. This tries to handle bad flat and assoc lists
    // tg: code/modules/bitrunning/objects/loot_crate.dm:76
    public virtual async DmTask<object> spawn_loot(DmList completion_loot = null)
    {
        foreach (object path in Dm.each(completion_loot))
        {
            if (!ispath(path))
                return FALSE;

            if (completion_loot[path] == null)
                return FALSE;

            for (float i = 1, i__end = Dm.num(completion_loot[path]); i <= i__end; i++)
                await Dm.new_async((Type)path, this);
        }

        return TRUE;
    }
}
