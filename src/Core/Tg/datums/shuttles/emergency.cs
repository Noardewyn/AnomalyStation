// Translated from tgstation (snapshot 1d99bd7f): code/datums/shuttles/emergency.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CM1 (D333): the whole file, the emergency shuttle catalogue the communications console sells (the var blocks are generated,
// D111): the Shuttle Firesale's discounts and the occupancy check at New, the flight events of a loaded shuttle (post_load), the
// unlocks of the prerequisite shuttles and the Arena shuttle's arena level. The templates the host mounts are the maps the converter
// writes (DV188: tg's emergency catalogue but the Scrapheap Challenge, whose modular map roots are not ported, and the Arena, whose
// arena level is not converted; not-translated.md 200).

// tg: code/datums/shuttles/emergency.dm:3
public partial class datum_map_template_shuttle_emergency : datum_map_template_shuttle
{
    // tg: code/datums/shuttles/emergency.dm:16
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!Dm.truthy(occupancy_limit) && who_can_purchase != null)
            throw Dm.CRASH(Dm.text("The [] needs an occupancy limit!", name), "code/datums/shuttles/emergency.dm", 19);
        if (HAS_TRAIT(SSstation, STATION_TRAIT_SHUTTLE_SALE) && credit_cost > 0 && prob(15))
        {
            float discount_amount = round(rand(25, 80), 5);
            name += I18n.T(" ([]% Discount!)", discount_amount);
            float discount_multiplier = 100 - discount_amount;
            credit_cost = ((credit_cost * discount_multiplier) / 100);
        }
    }

    ///on post_load use our variables to change shuttle events
    // tg: code/datums/shuttles/emergency.dm:27
    [DmParams("mobile")]
    public override async DmTask post_load(params object[] __extra)
    {
        var mobile = Dm.arg<obj_docking_port_mobile>(__extra, 0);
        await base.post_load(__extra);
        if (events == null)
            return;
        if (Dm.truthy(events_override))
            mobile.event_list.Cut();
        if (Dm.truthy(use_all_events))
        {
            foreach (object path in Dm.each(events.Copy())) // DM's for-in reads the list it started with
            {
                mobile.add_shuttle_event(path as Type);
                events.Remove(path);
            }
        }
        else
        {
            for (float i = 1; i <= event_amount; i++)
            {
                object path = pick_weight(events);
                events.Remove(path);
                mobile.add_shuttle_event(path as Type);
            }
        }
    }
}

// tg: code/datums/shuttles/emergency.dm:147
public partial class datum_map_template_shuttle_emergency_medisim : datum_map_template_shuttle_emergency
{
    // tg: code/datums/shuttles/emergency.dm:156
    public override object prerequisites_met()
    {
        return SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_MEDISIM];
    }
}

// tg: code/datums/shuttles/emergency.dm:168
public partial class datum_map_template_shuttle_emergency_arena : datum_map_template_shuttle_emergency
{
    // tg: code/datums/shuttles/emergency.dm:179
    public override object prerequisites_met()
    {
        return SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_BUBBLEGUM];
    }

    // tg: code/datums/shuttles/emergency.dm:182
    [DmParams("M")]
    public override async DmTask post_load(params object[] __extra)
    {
        await base.post_load(__extra);
        if (!Dm.truthy(arena_loaded))
        {
            arena_loaded = TRUE;
            var arena_template = await Dm.new_async<datum_map_template_arena>();
            await arena_template.load_new_z();
        }
    }
}

// tg: code/datums/shuttles/emergency.dm:287
public partial class datum_map_template_shuttle_emergency_scrapheap : datum_map_template_shuttle_emergency
{
    // tg: code/datums/shuttles/emergency.dm:297
    public override object prerequisites_met()
    {
        return SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_SCRAPHEAP];
    }
}

// tg: code/datums/shuttles/emergency.dm:303
public partial class datum_map_template_shuttle_emergency_narnar : datum_map_template_shuttle_emergency
{
    // tg: code/datums/shuttles/emergency.dm:313
    public override object prerequisites_met()
    {
        return SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_NARNAR];
    }
}

// tg: code/datums/shuttles/emergency.dm:386
public partial class datum_map_template_shuttle_emergency_wabbajack : datum_map_template_shuttle_emergency
{
    // tg: code/datums/shuttles/emergency.dm:397
    public override object prerequisites_met()
    {
        return SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_WABBAJACK];
    }
}

// tg: code/datums/shuttles/emergency.dm:513
public partial class datum_map_template_shuttle_emergency_zeta : datum_map_template_shuttle_emergency
{
    // tg: code/datums/shuttles/emergency.dm:523
    public override object prerequisites_met()
    {
        return SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_ALIENTECH];
    }
}

// tg: code/datums/shuttles/emergency.dm:526
public partial class datum_map_template_shuttle_emergency_tombstone : datum_map_template_shuttle_emergency
{
    // tg: code/datums/shuttles/emergency.dm:535
    public override object prerequisites_met()
    {
        return SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_TOMBSTONE];
    }
}

// tg: code/datums/shuttles/emergency.dm:538
public partial class datum_map_template_shuttle_emergency_ark : datum_map_template_shuttle_emergency
{
    // tg: code/datums/shuttles/emergency.dm:547
    public override object prerequisites_met()
    {
        return SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_ARK];
    }
}
