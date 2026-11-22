// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/packs/_packs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D299: the supply packs.

// tg: code/modules/cargo/packs/_packs.dm:1
public partial class datum_supply_pack : datum
{
    // tg: code/modules/cargo/packs/_packs.dm:35
    public override async DmTask New(params object[] __extra)
    {
        id = GetType();
    }

    /// Returns data used for cargo purchasing UI
    // tg: code/modules/cargo/packs/_packs.dm:39
    public virtual DmList get_contents_ui_data()
    {
        DmList data = Dm.list();
        foreach (object item in Dm.each(contains))
        {
            bool greyscale = Dm.truthy(Dm.initial(item, "greyscale_config"));
            DmList item_data = Dm.list(
                Dm.kv("name", Dm.initial(item, "name")),
                Dm.kv("icon", greyscale ? null : Dm.text("[]", Dm.initial(item, "icon"))),
                Dm.kv("icon_state", greyscale ? null : Dm.initial(item, "icon_state")),
                Dm.kv("amount", contains[item]));
            Dm.append(ref data, Dm.list(item_data)); // UNTYPED_LIST_ADD
        }
        return data;
    }

    /**
     * Proc that takes a given supply_pack, and attempts to create a crate containing the pack's contents as determined by fill()
     *
     * @ atom/A: The location or turf that the pack is being generated onto. Cargo shuttle provides an empty turf, other generate()s call this either null or otherwise.
     * @ datum/bank_account/paying_account: The account to associate the supply pack with when going and generating the crate. Only the paying account can open said secure crate/case.
     * @ crate_override: If defined, we will fill our supply pack with this object. This is used for when we need to spawn a random crate but the contents are goodies or we don't need a full crate.
     */
    // tg: code/modules/cargo/packs/_packs.dm:59
    public virtual async DmTask<object> generate(atom A = null, datum_bank_account paying_account = null, Type crate_override = null)
    {
        if (crate_override != null)
        {
            var unique_container = (obj)Dm.@new(crate_override, A);
            await fill(unique_container);
            return unique_container;
        }

        if (crate_type == null)
            throw Dm.CRASH("tried to generate a supply pack without a valid crate type", "code/modules/cargo/packs/_packs.dm", 68);

        var C = (obj_structure_closet)Dm.@new(crate_type, A);
        C.name = Dm.text("[][]", Dm.truthy(crate_name) ? crate_name : C.name, paying_account != null ? I18n.T(" - Purchased by []", paying_account.account_holder) : "");
        if (paying_account != null) // adds component for locking the crate so only the buyer (or their department) can open it
            await C.AddComponent(typeof(datum_component_locked_to_account), paying_account);

        if (Dm.truthy(access))
            C.req_access = Dm.list(access);
        if (Dm.truthy(access_any))
            C.req_one_access = Dm.list(access_any);

        await fill(C);
        return C;
    }

    // tg: code/modules/cargo/packs/_packs.dm:83
    public virtual float get_cost() => cost * SSeconomy.pack_price_modifier;

    /**
     * Takes a provided container, iterates, and spawns the full quantity of items within it, and applies and necessary status effects when the crate is populated.
     * @ container: The container holding the contents of the supply pack. Most typically a obj/structure/closet/crate, but can technically be anything.
     */
    // tg: code/modules/cargo/packs/_packs.dm:91
    public virtual async DmTask<object> fill(obj container = null)
    {
        foreach (object item in Dm.each(contains))
        {
            if (!Dm.truthy(contains[item]))
                contains[item] = 1f;
            float amount = Dm.num(contains[item]);
            for (float iteration = 1; iteration <= amount; iteration++)
            {
                var A = (atom)Dm.@new((Type)item, container);
                if (!Dm.truthy(Dm.bitand(order_flags, ORDER_ADMIN_SPAWNED)))
                    continue;
                A.flags_1 = Dm.num(Dm.bitor(A.flags_1, ADMIN_SPAWNED_1));
            }
        }
        return null;
    }

    /// For generating supply packs at runtime. Returns a list of supply packs to use instead of this one.
    // tg: code/modules/cargo/packs/_packs.dm:103
    public virtual DmList generate_supply_packs() => null;

    /// A random pick of the pack's contents: tg's many `for(i in 1 to N) new pick(contains)(container)` fills.
    protected async DmTask fill_random(obj container, int count)
    {
        for (int i = 1; i <= count; i++)
            Dm.@new((Type)pick(contains), container);
    }
}

public static partial class GlobalProcs
{
    ///Easily send a supplypod to an area
    // tg: code/modules/cargo/packs/_packs.dm:107  (gaps P4d: the forced fax's pod)
    public static bool send_supply_pod_to_area(object contents = null, object area_type = null, Type pod_type = null)
    {
        pod_type ??= typeof(obj_structure_closet_supplypod);
        DmList areas = get_areas(area_type);
        if (!Dm.truthy(LAZYLEN(areas)))
            return false;
        DmList open_turfs = Dm.list();
        foreach (turf_open_floor found_turf in Dm.each<turf_open_floor>(get_area_turfs(pick(areas), subtypes: TRUE) as DmList))
            Dm.append(ref open_turfs, found_turf);

        if (!Dm.truthy(length(open_turfs)))
            return false;

        Dm.@new<obj_effect_pod_landingzone>(pick(open_turfs), Dm.@new(pod_type), contents);
        return true;
    }
}

/**
 * Custom supply pack
 * The contents are given on New rather than being static
 * This is for adding custom orders to the Cargo console (like order consoles)
 */
// tg: code/modules/cargo/packs/_packs.dm:126
public partial class datum_supply_pack_custom : datum_supply_pack
{
    // tg: code/modules/cargo/packs/_packs.dm:133
    public override async DmTask New(params object[] __extra)
    {
        object purchaser = __extra.Length > 0 ? __extra[0] : null;
        object cost = __extra.Length > 1 ? __extra[1] : null;
        object contains = __extra.Length > 2 ? __extra[2] : null;
        await base.New();
        name = I18n.T("[]'s Mining Order", purchaser);
        this.cost = Dm.num(cost);
        this.contains = contains as DmList;
    }
}

// tg: code/modules/cargo/packs/_packs.dm:140
public partial class datum_supply_pack_custom_minerals : datum_supply_pack_custom
{
    // tg: code/modules/cargo/packs/_packs.dm:146
    public override async DmTask New(params object[] __extra)
    {
        object purchaser = __extra.Length > 0 ? __extra[0] : null;
        await base.New(__extra);
        name = I18n.T("[]'s Materials Order", purchaser);
    }

    ///Alters material amrkey & adjust order quantities if they exceed whats on the market
    // tg: code/modules/cargo/packs/_packs.dm:152
    public virtual DmList adjust_market()
    {
        DmList result = Dm.list();
        foreach (object possible_stack in Dm.each(contains.Copy()))
        {
            Type material_type = Dm.initial((Type)possible_stack, (obj_item_stack p) => p.material_type);
            //in case we ordered more than what's in the market at the time due to market fluctuations
            //we find the min of what was ordered & what's actually available in the market at this point of time
            float market_quantity = Dm.num(SSstock_market.materials_quantity[material_type]);
            float available_quantity = Dm.num(contains[possible_stack]);
            object singular_name = Dm.initial((Type)possible_stack, (obj_item_stack p) => p.singular_name);
            if (available_quantity > market_quantity)
            {
                string message = I18n.T("[]: requested=[] sheets, available=[] sheets, adjusted=[] sheets.", singular_name, available_quantity, market_quantity, market_quantity - available_quantity);
                available_quantity = market_quantity;
                if (!Dm.truthy(available_quantity))
                {
                    Dm.append(ref result, I18n.T("[]: order cancelled due to insufficient sheets in the market.", singular_name));
                    Dm.remove(ref contains, possible_stack);
                    continue;
                }
                Dm.append(ref result, message);
            }

            //adjust the order based ont the available quantity
            contains[possible_stack] = available_quantity;

            //Prices go up as material quantity becomes scarce
            float fraction = available_quantity;
            if (market_quantity != available_quantity) //to avoid division by zero error
                fraction /= market_quantity - available_quantity;
            SSstock_market.adjust_material_price(material_type, Dm.num(SSstock_market.materials_prices[material_type]) * fraction);

            //We decrease the quantity only after adjusting our prices for accurate values
            SSstock_market.adjust_material_quantity(material_type, -available_quantity);
        }
        return result;
    }

    // tg: code/modules/cargo/packs/_packs.dm:181
    public override async DmTask<object> fill(obj container = null)
    {
        foreach (object possible_stack in Dm.each(contains))
        {
            //spawn the ordered stack inside the crate
            float sheets_to_spawn = Dm.num(contains[possible_stack]);
            while (sheets_to_spawn > 0)
            {
                float spawn_quantity = Math.Min(sheets_to_spawn, MAX_STACK_SIZE);
                var ordered_stack = (obj_item_stack_sheet)Dm.@new((Type)possible_stack, container, spawn_quantity);
                if (Dm.truthy(Dm.bitand(order_flags, ORDER_ADMIN_SPAWNED)))
                    ordered_stack.flags_1 = Dm.num(Dm.bitor(ordered_stack.flags_1, ADMIN_SPAWNED_1));
                sheets_to_spawn -= spawn_quantity;
            }
        }
        return null;
    }
}

// The packs' fills. tg's other overrides wait for what they spawn: the gift crate (costumes_toys.dm:294), the pizza box's anomalies
// (organic.dm:209), the livestock's extra mobs and stasis (livestock.dm): shortcut: those packs ship their plain contents, translate them with
// their contents. The uplink's surplus (general.dm:224) came with the stray syndicate cargo pod (gaps E3, D327).

// tg: code/modules/cargo/packs/costumes_toys.dm:36
public partial class datum_supply_pack_costumes_toys_randomised : datum_supply_pack_costumes_toys
{
    public override async DmTask<object> fill(obj container = null)
    {
        DmList L = contains.Copy();
        for (float i = 1; i <= num_contained; i++)
            Dm.@new((Type)pick_n_take(L), container);
        return null;
    }
}

// tg: code/modules/cargo/packs/medical.dm:121
public partial class datum_supply_pack_medical_supplies : datum_supply_pack_medical
{
    public override async DmTask<object> fill(obj container = null)
    {
        await fill_random(container, 10);
        return null;
    }
}

// tg: code/modules/cargo/packs/organic.dm:39
public partial class datum_supply_pack_organic_randomized : datum_supply_pack_organic
{
    public override async DmTask<object> fill(obj container = null)
    {
        await fill_random(container, 15);
        return null;
    }
}

// tg: code/modules/cargo/packs/imports.dm:244
public partial class datum_supply_pack_imports_russian : datum_supply_pack_imports
{
    public override async DmTask<object> fill(obj container = null)
    {
        await fill_random(container, 10);
        return null;
    }
}

// tg: code/modules/cargo/packs/service.dm:233
public partial class datum_supply_pack_service_randomized_donkpockets : datum_supply_pack_service_randomized
{
    public override async DmTask<object> fill(obj container = null)
    {
        await fill_random(container, 3);
        return null;
    }
}

// tg: code/modules/cargo/packs/service.dm:255
public partial class datum_supply_pack_service_randomized_ready_donk : datum_supply_pack_service_randomized
{
    public override async DmTask<object> fill(obj container = null)
    {
        await fill_random(container, 3);
        return null;
    }
}

// tg: code/modules/cargo/packs/general.dm:162
public partial class datum_supply_pack_misc_empty : datum_supply_pack_misc
{
    public override async DmTask<object> generate(atom A = null, datum_bank_account paying_account = null, Type crate_override = null) => null;
}

///Special supply crate that generates random syndicate gear up to a determined TC value
// tg: code/modules/cargo/packs/general.dm:210
public partial class datum_supply_pack_misc_syndicate : datum_supply_pack_misc
{
    ///Generate assorted uplink items, taking into account the same surplus modifiers used for surplus crates
    // tg: code/modules/cargo/packs/general.dm:224
    public override async DmTask<object> fill(obj container = null)
    {
        DmList uplink_items = Dm.list();
        foreach (datum_uplink_item item in Dm.each_cast<datum_uplink_item>(SStraitor.uplink_items))
        {
            if (Dm.truthy(Dm.bitand(item.purchasable_from, contents_uplink_type)) && item.item != null)
                Dm.append(ref uplink_items, item);
        }

        while (Dm.truthy(crate_value))
        {
            datum_uplink_item uplink_item = (datum_uplink_item)pick(uplink_items);
            if (!Dm.truthy(uplink_item.surplus) || prob(100 - uplink_item.surplus))
                continue;
            if (Dm.truthy(length(uplink_item.restricted_roles)) || Dm.truthy(length(uplink_item.restricted_species)))
                continue;
            if (crate_value < uplink_item.cost)
                continue;
            crate_value -= uplink_item.cost;
            await Dm.new_async(uplink_item.item, container);
        }
        return null;
    }
}

///Syndicate supply crate that can have its contents value changed by admins, uses a seperate datum to avoid having admins touch the original one.
// tg: code/modules/cargo/packs/general.dm:241
public partial class datum_supply_pack_misc_syndicate_custom_value : datum_supply_pack_misc_syndicate
{
    // tg: code/modules/cargo/packs/general.dm:244
    public virtual void setup_contents(object value = null, object uplink = null)
    {
        crate_value = Dm.num(value);
        contents_uplink_type = Dm.num(uplink);
    }
}

// tg: code/modules/cargo/packs/materials.dm:80
public partial class datum_supply_pack_materials_gas_canisters : datum_supply_pack_materials
{
    public override DmList generate_supply_packs()
    {
        DmList canister_packs = Dm.list();

        // This is the amount of moles in a default canister
        var fakeCanister = typeof(obj_machinery_portable_atmospherics_canister);
        float moleCount = (Dm.num(Dm.initial(fakeCanister, "maximum_pressure")) * Dm.num(Dm.initial(fakeCanister, "filled"))) * Dm.num(Dm.initial(fakeCanister, "volume")) / (R_IDEAL_GAS_EQUATION * T20C);

        foreach (Type gas in Dm.each<Type>(GLOB.meta_gas_info[META_GAS_ID]))
        {
            if (!Dm.truthy(Dm.bitand(Dm.initial(gas, "cargo_flags"), GAS_PURCHASABLE)))
                continue;
            var pack = Dm.@new<datum_supply_pack_materials>();
            object canname = Dm.initial(gas, "name");
            pack.name = Dm.text("[] Canister", canname);
            pack.desc = Dm.text("Contains a canister of [].", canname);
            if (Dm.truthy(Dm.bitand(Dm.initial(gas, "cargo_flags"), GAS_DANGEROUS)))
            {
                pack.access = ACCESS_ATMOSPHERICS;
                pack.access_view = ACCESS_ATMOSPHERICS;
            }
            pack.crate_name = Dm.text("[] canister crate", canname);
            pack.id = Dm.text("[]([])", GetType(), canname);

            pack.cost = cost + moleCount * Dm.num(Dm.initial(gas, "base_value")) * 1.6f;
            pack.cost = (float)Math.Ceiling(pack.cost / 10) * 10; // CEILING(pack.cost, 10)

            pack.test_ignored = FALSE;

            pack.contains = Dm.list(GLOB.gas_id_to_canister[Dm.initial(gas, "id")]);

            pack.crate_type = crate_type;

            Dm.append(ref canister_packs, pack);
        }
        return canister_packs;
    }
}
