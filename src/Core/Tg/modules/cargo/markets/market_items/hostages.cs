// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/markets/market_items/hostages.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file, the black market's hostages: a contractor's victim (`living.dm:2955` process_capture) is put on
// the market, bought back early through the LTSRBT or a supply pod, and comes home cuffed, with an electropack on a signaler the buyer
// gets, or in a souvenir tee. The var block is generated (D111).

///A special category for mobs captured by pirates, tots and contractors, should someone ever want to get them back in advance.
// tg: code/modules/cargo/markets/market_items/hostages.dm:2
public partial class datum_market_item_hostage : datum_market_item
{
    // tg: code/modules/cargo/markets/market_items/hostages.dm:12
    [DmParams("mob", "new_price")]
    public override async DmTask New(params object[] __extra)
    {
        var mob = Dm.arg(__extra, 0) as mob_living;
        object new_price = Dm.arg(__extra, 1);
        await base.New();
        set_item(mob);
        name = Dm.text("[]", mob.real_name);
        object specimen = Dm.initial(mob, nameof(mob.name));
        bool humie_mob = ishuman(mob);
        if (humie_mob)
        {
            var humie = (mob_living_carbon_human)mob;
            specimen = ((datum_species)humie.dna.species).name;
        }
        desc = pick(Dm.list(
            I18n.T("If you're looking for a recently stolen [], you've come to the right place.", specimen),
            I18n.T("We've recently acquired a fine [] from a station around here, eheh...", specimen),
            I18n.T("For a limited time, we're offering this [] for you to buy (back).", specimen)));
        desc = Dm.str(desc) + I18n.T(" DISCLAIMER: The offer will expire once the creature is returned to the station.");
        if (humie_mob)
            desc = Dm.str(desc) + I18n.T("[] may be delivered handcuffed, for safety of course.", mob.p_They());

        price = new_price;
        RegisterSignal(mob, COMSIG_LIVING_RETURN_FROM_CAPTURE, nameof(on_return_from_capture));
    }

    // tg: code/modules/cargo/markets/market_items/hostages.dm:33
    [SignalHandler]
    public virtual void on_return_from_capture(mob_living source = null, turf destination = null)
    {
        qdel(this); //as if we never existed, our mentions we'll be removed from the market.
    }

    // tg: code/modules/cargo/markets/market_items/hostages.dm:37
    public override object Destroy(float? force = null)
    {
        signaler = null;
        return base.Destroy(force);
    }

    // tg: code/modules/cargo/markets/market_items/hostages.dm:41
    public override async DmTask<object> buy(obj_item_market_uplink uplink = null, mob buyer = null, object shipping_method = null, object legal_status = null)
    {
        object dot = await base.buy(uplink, buyer, shipping_method, legal_status);
        if (!Dm.truthy(dot) || item is not mob_living || !prob(57)) // 3 in 7 chance of the electropack set not spawning...
            return dot;
        signaler = Dm.@new<obj_item_assembly_signaler>(uplink.drop_location());
        RegisterSignal(signaler, COMSIG_QDELETING, nameof(clear_signaler_ref));
        signaler.set_frequency(sanitize_frequency(rand(MIN_FREE_FREQ, MAX_FREE_FREQ)));
        signaler.code = rand(1, 100);
        await buyer.put_in_hands(signaler);
        to_chat(buyer, span_notice(I18n.T("A [] appears []!", signaler, Dm.truthy(buyer.is_holding(signaler)) ? I18n.T("in your hands") : I18n.T("at your feet"))));
        return dot;
    }

    // tg: code/modules/cargo/markets/market_items/hostages.dm:53
    [SignalHandler]
    public virtual void clear_signaler_ref(datum source = null)
    {
        signaler = null;
    }

    // tg: code/modules/cargo/markets/market_items/hostages.dm:57
    public override async DmTask<object> spawn_item(object loc = null, datum_market_purchase purchase = null)
    {
        var mob = (mob_living)item;
        UnregisterSignal(mob, COMSIG_LIVING_RETURN_FROM_CAPTURE);
        if (!IS_UNCONSCIOUS(mob))
            to_chat(mob, span_boldnicegreen(I18n.T("You have been <u>bought</u> back to the station. Be grateful to whoever got you out of the holding facility early.")));
        if (item is not mob_living_carbon_human humie)
            return await base.spawn_item(loc, purchase);
        if (signaler != null) //57% chance that the mob is equipped with electropack and cuffs
        {
            await humie.equip_to_slot_or_del(Dm.@new<obj_item_restraints_handcuffs>(), ITEM_SLOT_HANDCUFFED, indirect_action: TRUE);
            if (humie.back != null) //try to remove the neck item if possible before we attempt to install the collar bomb
                await humie.transferItemToLoc(humie.back, loc as atom);
            var pack = Dm.@new<obj_item_electropack>(loc);
            pack.set_frequency(signaler.frequency);
            pack.code = signaler.code;
            await humie.equip_to_slot_if_possible(pack, ITEM_SLOT_BACK, disable_warning: TRUE);
            UnregisterSignal(signaler, COMSIG_QDELETING);
            signaler = null;
        }
        else if (prob(66)) // 29% chance of just cuffs
            await humie.equip_to_slot_or_del(Dm.@new<obj_item_restraints_handcuffs>(), ITEM_SLOT_HANDCUFFED, indirect_action: TRUE);
        else // 14% chance of just a tee souvenir and pin, no cuffs and shit.
        {
            var souvenir = Dm.@new<obj_item_clothing_under_misc_syndicate_souvenir>(loc);
            await humie.equip_to_slot_if_possible(souvenir, ITEM_SLOT_ICLOTHING, indirect_action: TRUE);
            var pin = Dm.@new<obj_item_clothing_accessory_anti_sec_pin>(loc);
            await pin.try_attach(souvenir);
        }

        if (humie.w_uniform == null)
        {
            //FUCKING SLAVES, GET YOUR CLOTHES BACK ON!
            await humie.equip_to_slot_or_del(Dm.@new<obj_item_clothing_under_costume_jabroni>(humie), ITEM_SLOT_ICLOTHING, indirect_action: TRUE);
        }
        return await base.spawn_item(loc, purchase);
    }
}
