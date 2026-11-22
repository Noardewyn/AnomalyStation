// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/market_crash.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1b (D327): the whole file, the market crash (the station's vendors charge more while the inflation climbs, then settle). The
// var blocks are generated (D111).

/**
 * An event which decreases the station target temporarily, causing the inflation var to increase heavily.
 *
 * Done by decreasing the station_target by a high value per crew member, resulting in the station total being much higher than the target, and causing artificial inflation.
 */
// tg: code/modules/events/market_crash.dm:13
public partial class datum_round_event_market_crash : datum_round_event
{
    // tg: code/modules/events/market_crash.dm:17
    public override async DmTask<object> setup()
    {
        start_when = 1;
        end_when = rand(100, 50);
        announce_when = 2;
        return null;
    }

    // tg: code/modules/events/market_crash.dm:22
    public override async DmTask announce__proc(object fake = null)
    {
        DmList poss_reasons = Dm.list("the alignment of the moon and the sun",
            "some risky housing market outcomes",
            "the B.E.P.I.S. team's untimely downfall",
            "speculative Terragov grants backfiring",
            "greatly exaggerated reports of Nanotrasen accountancy personnel being \"laid off\"",
            "a \"great investment\" into \"non-fungible tokens\" by a \"moron\"",
            "a number of raids from Tiger Cooperative agents",
            "supply chain shortages",
            "the \"Nanotrasen+\" social media network's untimely downfall",
            "the \"Nanotrasen+\" social media network's unfortunate success",
            "uhh, bad luck, we guess");
        object reason = pick(poss_reasons);
        priority_announce(Dm.text("Due to [], prices for on-station vendors will be increased for a short period.", reason), "Nanotrasen Accounting Division");
    }

    // tg: code/modules/events/market_crash.dm:38
    public override async DmTask<object> start()
    {
        object dot = await base.start();
        SSeconomy.update_vending_prices();
        await SSeconomy.price_update();
        ADD_TRAIT(SSeconomy, TRAIT_MARKET_CRASHING, MARKET_CRASH_EVENT_TRAIT);
        return dot;
    }

    // tg: code/modules/events/market_crash.dm:44
    public override async DmTask end()
    {
        await base.end();
        REMOVE_TRAIT(SSeconomy, TRAIT_MARKET_CRASHING, MARKET_CRASH_EVENT_TRAIT);
        await SSeconomy.price_update();
        SSeconomy.update_vending_prices();
        priority_announce("Prices for on-station vendors have now stabilized.", "Nanotrasen Accounting Division");
    }

    // tg: code/modules/events/market_crash.dm:51
    public override async DmTask tick()
    {
        await base.tick();
        tick_counter++; // tg writes tick_counter = tick_counter++, which stores the old value back and never counts; its comment above wants
        // the vendors repriced every fifth tick, which the port does
        SSeconomy.inflation_value = 5.5f * Dm.log(activeFor + 1);
        if (tick_counter == 5)
        {
            tick_counter = 1;
            SSeconomy.update_vending_prices();
        }
    }
}
