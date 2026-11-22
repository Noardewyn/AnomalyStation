// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/construction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file; rcd_result_with_memory, the RCD's, since E13.

public static partial class GlobalProcs
{
    /// Makes sure only integer values are used when consuming, removing & checking for mats
    // tg: code/__HELPERS/construction.dm:2  #define OPTIMAL_COST(cost)(max(1, floor(cost))) (a member, rules 11.1)
    public static float OPTIMAL_COST(object cost) => max(1, floor(Dm.num(cost)));

    // Wrapper to convert material name into its source name
    // tg: code/__HELPERS/construction.dm:5  #define MATERIAL_SOURCE(mat) "[mat.name]_material"
    public static string MATERIAL_SOURCE(datum_material mat) => Dm.text("[]_material", mat.name);

    /**
     * Produces a new RCD result from the given one if it can be calculated that
     * the RCD should speed up with the remembered form.
     *
     */
    // tg: code/__HELPERS/construction.dm:13
    public static object rcd_result_with_memory(DmList defaults = null, turf place = null, object expected_memory = null)
    {
        if (Dm.eq(place?.rcd_memory, expected_memory))
            return Dm.add(defaults, Dm.list(
                Dm.kv("cost", Dm.num(defaults["cost"]) / RCD_MEMORY_COST_BUFF),
                Dm.kv("delay", Dm.num(defaults["delay"]) / RCD_MEMORY_SPEED_BUFF),
                Dm.kv(RCD_RESULT_BYPASS_FREQUENT_USE_COOLDOWN, TRUE)));
        else
            return defaults;
    }

    /**
     * Turns material amount into the number of sheets, returning FALSE if the number is less than SHEET_MATERIAL_AMOUNT
     *
     * Arguments:
     * - amt: amount to convert
     */
    // tg: code/__HELPERS/construction.dm:29
    public static object amount2sheet(object amt = null)
    {
        if (Dm.num(amt) >= SHEET_MATERIAL_AMOUNT)
            return floor(Dm.num(amt) / SHEET_MATERIAL_AMOUNT);
        return 0;
    }

    /**
     * Turns number of sheets into material amount, returning FALSE if the number is <= 0
     *
     * Arguments:
     * - amt: amount to convert
     */
    // tg: code/__HELPERS/construction.dm:40
    public static object sheet2amount(object sheet_amt = null)
    {
        if (Dm.num(sheet_amt) > 0)
            return Dm.num(sheet_amt) * SHEET_MATERIAL_AMOUNT;
        return 0;
    }

    /**
     * Splits a stack. we don't use /obj/item/stack/proc/split_stack because Byond complains that should only be called asynchronously.
     * This proc is also more faster because it doesn't deal with mobs, copying evidences or refreshing atom storages
     * Has special internal uses for e.g. by the material container & RPED
     *
     * Arguments:
     * - [target][obj/item/stack]: the stack to split
     * - [amount]: amount to split by
     */
    // tg: code/__HELPERS/construction.dm:54
    public static object fast_split_stack(obj_item_stack target = null, object amount = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(target.use(amount, TRUE, FALSE))))
            return null;

        return Dm.@new(target.type, target.drop_location(), amount, FALSE, target.mats_per_unit);
    }
}
