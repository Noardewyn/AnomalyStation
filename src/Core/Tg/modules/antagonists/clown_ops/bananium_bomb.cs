// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/clown_ops/bananium_bomb.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N3b, D316: the clown operatives' bananium nuke clowns the station). The var block is generated (D111).

// tg: code/modules/antagonists/clown_ops/bananium_bomb.dm:1
public partial class obj_machinery_nuclearbomb_syndicate_bananium : obj_machinery_nuclearbomb_syndicate
{
    // tg: code/modules/antagonists/clown_ops/bananium_bomb.dm:7
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (deconstruction_state != NUKESTATE_INTACT)
        {
            icon_state = Dm.text("[]_base", base_icon_state);
            return dot;
        }

        var sw__1 = get_nuke_state();
        if (Dm.eq(sw__1, NUKE_OFF_LOCKED) || Dm.eq(sw__1, NUKE_OFF_UNLOCKED))
            icon_state = Dm.text("[]_base", base_icon_state);
        else if (Dm.eq(sw__1, NUKE_ON_TIMING))
            icon_state = Dm.text("[]_timing", base_icon_state);
        else if (Dm.eq(sw__1, NUKE_ON_EXPLODING))
            icon_state = Dm.text("[]_exploding", base_icon_state);
        return dot;
    }

    // tg: code/modules/antagonists/clown_ops/bananium_bomb.dm:21
    public override Type get_cinematic_type(object detonation_status = null)
    {
        var sw__1 = detonation_status;
        if (Dm.eq(sw__1, DETONATION_HIT_STATION))
            return typeof(datum_cinematic_nuke_clown);
        if (Dm.eq(sw__1, DETONATION_NEAR_MISSED_STATION))
            return typeof(datum_cinematic_nuke_ops_miss);
        if (Dm.eq(sw__1, DETONATION_HIT_SYNDIE_BASE) || Dm.eq(sw__1, DETONATION_MISSED_STATION))
            return typeof(datum_cinematic_nuke_fake); //it is farther away, so just a bikehorn instead of an airhorn

        stack_trace(Dm.text("[] - get_cinematic_type got a detonation_status it was not expecting. (Got: [])", type, detonation_status),
            file: "code/modules/antagonists/clown_ops/bananium_bomb.dm", line: 30);
        return typeof(datum_cinematic_nuke_fake);
    }

    // tg: code/modules/antagonists/clown_ops/bananium_bomb.dm:33
    public override void nuke_effects(DmList affected_z_levels = null)
    {
        Waiting.Detach(callback_on_everyone_on_z(affected_z_levels, Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.make_into_clown)), this));
    }
}

public static partial class GlobalProcs
{
    /**
     * Helper proc that handles making someone into a clown after a bananium nuke goes off.
     */
    // tg: code/modules/antagonists/clown_ops/bananium_bomb.dm:39
    // tg types the parameter /mob/living/carbon/human and istype-checks it: callback_on_everyone_on_z hands it every living mob, so the C#
    // parameter is mob_living (a narrower one would fail the binding, not the check)
    [DmParams("clowned_on")]
    public static async DmTask make_into_clown(mob_living mob_clowned_on = null)
    {
        if (mob_clowned_on is not mob_living_carbon_human clowned_on)
            return;

        await clowned_on.Stun(1 * SECONDS);
        if (clowned_on.w_uniform == null || Dm.truthy(await clowned_on.dropItemToGround(clowned_on.w_uniform)))
        {
            obj_item_clothing clown_shirt = Dm.@new<obj_item_clothing_under_rank_civilian_clown>(clowned_on);
            ADD_TRAIT(clown_shirt, TRAIT_NODROP, CLOWN_NUKE_TRAIT);
            await clowned_on.equip_to_slot_or_del(clown_shirt, ITEM_SLOT_ICLOTHING);
        }

        if (clowned_on.shoes == null || Dm.truthy(await clowned_on.dropItemToGround(clowned_on.shoes)))
        {
            obj_item_clothing clown_shoes = Dm.@new<obj_item_clothing_shoes_clown_shoes>(clowned_on);
            ADD_TRAIT(clown_shoes, TRAIT_NODROP, CLOWN_NUKE_TRAIT);
            await clowned_on.equip_to_slot_or_del(clown_shoes, ITEM_SLOT_FEET);
        }

        if (clowned_on.wear_mask == null || Dm.truthy(await clowned_on.dropItemToGround(clowned_on.wear_mask)))
        {
            obj_item_clothing clown_mask = Dm.@new<obj_item_clothing_mask_gas_clown_hat>(clowned_on);
            ADD_TRAIT(clown_mask, TRAIT_NODROP, CLOWN_NUKE_TRAIT);
            await clowned_on.equip_to_slot_or_del(clown_mask, ITEM_SLOT_MASK);
        }

        await clowned_on.dna.add_mutation(typeof(datum_mutation_clumsy), MUTATION_SOURCE_CLOWN_CLUMSINESS);
        await clowned_on.gain_trauma(typeof(datum_brain_trauma_mild_phobia_clowns), TRAUMA_RESILIENCE_LOBOTOMY); //MWA HA HA
    }
}
