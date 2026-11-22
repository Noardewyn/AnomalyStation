// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/cash.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3b: the briefcase full of cash): space cash and its denominations. The var blocks are generated (D111).

// tg: code/game/objects/items/stacks/cash.dm:1
public partial class obj_item_stack_spacecash : obj_item_stack //Don't use base space cash stacks. Any other space cash stack can merge with them, and could cause potential money duping exploits.
{
    // tg: code/game/objects/items/stacks/cash.dm:17
    [DmParams("mapload", "new_amount", "merge", "mat_override", "mat_amt")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_FISHING_BAIT, TRAIT_BAIT_ALLOW_FISHING_DUD), INNATE_TRAIT);
        update_desc();
        return dot;
    }

    // tg: code/game/objects/items/stacks/cash.dm:22
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_cellulose), 10));
    }

    // tg: code/game/objects/items/stacks/cash.dm:25
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        object total_worth = get_item_credit_value();
        // MONEY_NAME_AUTOPURAL(total_worth) (__DEFINES/economy.dm:99), inline (rules 11.1)
        desc = I18n.T("It's worth [] [] in total.", total_worth, Dm.text("credit[]", Dm.eq(total_worth, 1) ? "" : "s"));
        return dot;
    }

    // tg: code/game/objects/items/stacks/cash.dm:30
    public override object get_item_credit_value()
    {
        return Dm.num(amount) * Dm.num(value);
    }

    // tg: code/game/objects/items/stacks/cash.dm:33
    public override async DmTask<object> merge(obj_item_stack target_stack = null, object limit = null)
    {
        object dot = await base.merge(target_stack, limit);
        update_desc();
        return dot;
    }

    // tg: code/game/objects/items/stacks/cash.dm:37
    [DmParams("used", "transfer", "check")]
    public override async DmTask<object> use(object used = null, params object[] __extra)
    {
        object dot = await base.use(used, __extra);
        update_desc();
        return dot;
    }

    // tg: code/game/objects/items/stacks/cash.dm:41
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        float n = Dm.num(amount);
        object initial_state = Dm.initial(this, (obj_item_stack_spacecash c) => c.icon_state);
        if (n == 1)
            icon_state = initial_state as string;
        else if (n >= 2 && n <= 9)
            icon_state = Dm.text("[]_2", initial_state);
        else if (n >= 10 && n <= 24)
            icon_state = Dm.text("[]_3", initial_state);
        else if (n >= 25)
            icon_state = Dm.text("[]_4", initial_state);
        return dot;
    }
}

public static partial class GlobalProcs
{
    /**
     * Takes an amount of credit and returns a list of space cash typepaths that are equivalent to that amount.
     * For example, if you passed 450 credits, it would return a list with a c200, another c200, and a c50 space cash stack typepath.
     * You can then use the list to new() all the types provided if you wanted to give a player that amount in space cash.
     *
     * * amount - the amount of credits to convert to space cash.
     * If you provide a decimal, rounds down to the nearest whole number.
     */
    // tg: code/game/objects/items/stacks/cash.dm:62
    public static DmList credits_to_spacecash(object amount = null)
    {
        float left = Dm.floor(Dm.num(amount));
        // ordered by value so we can always provide the highest denominations possible
        DmList spacecash_types_by_value = Dm.list(
            typeof(obj_item_stack_spacecash_c10000),
            typeof(obj_item_stack_spacecash_c1000),
            typeof(obj_item_stack_spacecash_c500),
            typeof(obj_item_stack_spacecash_c200),
            typeof(obj_item_stack_spacecash_c100),
            typeof(obj_item_stack_spacecash_c50),
            typeof(obj_item_stack_spacecash_c20),
            typeof(obj_item_stack_spacecash_c10),
            typeof(obj_item_stack_spacecash_c1));
        DmList spacecash_list = Dm.list();
        while (left > 0)
        {
            Type found_cashtype = null;
            foreach (Type cashtype in Dm.each<Type>(spacecash_types_by_value))
            {
                if (Dm.num(Dm.initial(cashtype, (obj_item_stack_spacecash c) => c.value)) <= left)
                {
                    found_cashtype = cashtype;
                    break;
                }
            }

            if (found_cashtype == null)
            {
                stack_trace(Dm.text("credits_to_spacecash failed to find a cashtype for amount: []", left), "code/game/objects/items/stacks/cash.dm", 84);
                break;
            }

            Dm.append(ref spacecash_list, found_cashtype);
            left -= Dm.num(Dm.initial(found_cashtype, (obj_item_stack_spacecash c) => c.value));
        }

        if (left > 0)
            stack_trace(Dm.text("credits_to_spacecash failed to convert [] credits into space cash. Remaining amount: []", left, left), "code/game/objects/items/stacks/cash.dm", 91);

        return spacecash_list;
    }
}
