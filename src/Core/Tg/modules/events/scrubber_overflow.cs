// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/scrubber_overflow.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1c, D316: the round's scrubber overflows, and the beer nuke's every-vent flood) but its admin option, the
// admins' choice of a reagent (/datum/event_admin_setup/listed_options/scrubber_overflow: the admin tools' Trigger Event panel, D275).
// The var blocks are generated (D111).
//
// GAP(types): `var/datum/reagent/forced_reagent_type` (:21) holds a reagent's typepath (the beer nuke's flood_reagent, :73); the
// generator types it as the reagent datum, so it is declared here as the typepath it is.

// tg: code/modules/events/scrubber_overflow.dm:1
public partial class datum_round_event_control_scrubber_overflow : datum_round_event_control
{
    // tg: code/modules/events/scrubber_overflow.dm:93
    public override object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        object dot = base.can_spawn_event(players_amt, allow_magic);
        if (!Dm.truthy(dot))
            return dot;
        foreach (obj_machinery_atmospherics_components_unary_vent_scrubber temp_vent in Dm.each<obj_machinery_atmospherics_components_unary_vent_scrubber>(
            SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_atmospherics_components_unary_vent_scrubber))))
        {
            var scrubber_turf = get_turf(temp_vent) as turf;
            if (scrubber_turf == null)
                continue;
            if (!Dm.truthy(is_station_level(scrubber_turf.z)))
                continue;
            if (Dm.truthy(temp_vent.welded))
                continue;
            return TRUE; //there's at least one. we'll let the codergods handle the rest with prob() i guess.
        }
        return FALSE;
    }
}

// tg: code/modules/events/scrubber_overflow.dm:11
public partial class datum_round_event_scrubber_overflow : datum_round_event
{
    /// Specific reagent to force all scrubbers to use, null for random reagent choice
    // tg: code/modules/events/scrubber_overflow.dm:21  (hand-written: see the GAP(types) above)
    public Type forced_reagent_type;

    // tg: code/modules/events/scrubber_overflow.dm:11  (the type's overrides, hand-written with the var above)
    public datum_round_event_scrubber_overflow()
    {
        announce_when = 1;
        start_when = 5;
    }

    // tg: code/modules/events/scrubber_overflow.dm:62
    public override void announce_deadchat(object random = null, object cause = null)
    {
        if (forced_reagent_type == null)
        {
            //nothing out of the ordinary, so default announcement
            base.announce_deadchat(random, cause);
            return;
        }
        deadchat_broadcast(Dm.text(" has just been[] triggered[]!", Dm.truthy(random) ? " randomly" : "", Dm.truthy(cause) ? Dm.text(" by []", cause) : ""),
            Dm.text("<b>Scrubber Overflow: []</b>", Dm.initial(forced_reagent_type, "name")), message_type: DEADCHAT_ANNOUNCEMENT);
    }

    // tg: code/modules/events/scrubber_overflow.dm:68
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce("The scrubbers network is experiencing a backpressure surge. Some ejection of contents may occur.", Dm.text("[] Engineering Division", command_name()));
    }

    // tg: code/modules/events/scrubber_overflow.dm:71
    public override async DmTask<object> setup()
    {
        foreach (obj_machinery_atmospherics_components_unary_vent_scrubber temp_vent in Dm.each<obj_machinery_atmospherics_components_unary_vent_scrubber>(
            SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_atmospherics_components_unary_vent_scrubber))))
        {
            var scrubber_turf = get_turf(temp_vent) as turf;
            if (scrubber_turf == null)
                continue;
            if (!Dm.truthy(is_station_level(scrubber_turf.z)))
                continue;
            if (Dm.truthy(temp_vent.welded))
                continue;
            if (!prob(overflow_probability))
                continue;
            Dm.append(ref scrubbers, temp_vent);
        }

        if (!Dm.truthy(scrubbers.len))
        {
            kill();
            return null;
        }
        return null;
    }

    /// proc that will run the prob check of the event and return a safe or dangerous reagent based off of that.
    // tg: code/modules/events/scrubber_overflow.dm:107
    public virtual object get_overflowing_reagent(object dangerous = null)
    {
        return Dm.truthy(dangerous) ? get_random_reagent_id() : pick(safer_chems);
    }

    // tg: code/modules/events/scrubber_overflow.dm:110
    public override async DmTask<object> start()
    {
        foreach (obj_machinery_atmospherics_components_unary_vent_scrubber vent in Dm.each<obj_machinery_atmospherics_components_unary_vent_scrubber>(scrubbers))
        {
            if (vent.loc == null)
                throw Dm.CRASH(Dm.text("SCRUBBER SURGE: [] has no loc somehow?", vent), "code/modules/events/scrubber_overflow.dm", 113);

            var dispensed_reagent = Dm.@new<datum_reagents>(reagents_amount);
            dispensed_reagent.my_atom = vent;
            if (forced_reagent_type != null)
                await dispensed_reagent.add_reagent(forced_reagent_type, reagents_amount);
            else if (prob(danger_chance))
            {
                await dispensed_reagent.add_reagent(get_overflowing_reagent(dangerous: TRUE), reagents_amount);
                Dm.@new<mob_living_basic_cockroach>(get_turf(vent));
                Dm.@new<mob_living_basic_cockroach_bloodroach>(get_turf(vent));
            }
            else
                await dispensed_reagent.add_reagent(get_overflowing_reagent(dangerous: FALSE), reagents_amount);

            await dispensed_reagent.create_foam(typeof(datum_effect_system_fluid_spread_foam_short), reagents_amount);

            CHECK_TICK();
        }
        return null;
    }
}
