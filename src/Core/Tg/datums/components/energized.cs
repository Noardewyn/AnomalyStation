// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/energized.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The tram port (2026-11-12): the whole file (the tram's energized induction plates).

// tg: code/datums/components/energized.dm:4
public partial class datum_component_energized : datum_component
{
    // tg: code/datums/components/energized.dm:1
    private const float NORMAL_TOAST_PROB = 3;
    // tg: code/datums/components/energized.dm:2
    private const float BROKEN_TOAST_PROB = 33;

    // tg: code/datums/components/energized.dm:19
    [DmParams("plate_inbound", "plate_outbound", "plate_transport_id")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object plate_inbound = Dm.arg<object>(__extra, 0);
        object plate_outbound = Dm.arg<object>(__extra, 1);
        object plate_transport_id = Dm.arg<object>(__extra, 2);
        object dot = await base.Initialize();

        if (plate_inbound == null)
            return dot;

        inbound = plate_inbound;
        if (plate_outbound == null)
            return dot;

        outbound = plate_outbound;
        if (plate_transport_id == null)
            return dot;

        specific_transport_id = Dm.text("[]", plate_transport_id);
        find_tram();
        return dot;
    }

    // tg: code/datums/components/energized.dm:36
    public virtual void find_tram()
    {
        foreach (datum_transport_controller_linear transport in Dm.each<datum_transport_controller_linear>(SStransport.transports_by_type[TRANSPORT_TYPE_TRAM]))
        {
            if (Dm.eq(transport.specific_transport_id, specific_transport_id))
            {
                transport_ref = WEAKREF(transport) as datum_weakref;
                break;
            }
        }
    }

    // tg: code/datums/components/energized.dm:42
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_ATOM_ENTERED, nameof(toast));
    }

    // tg: code/datums/components/energized.dm:46
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_ATOM_ENTERED);
        await base.UnregisterFromParent(__extra);
    }

    // tg: code/datums/components/energized.dm:51
    public virtual object toast(turf_open_floor source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (!isliving(arrived))
            return null;

        mob_living future_tram_victim = (mob_living)arrived;
        datum_transport_controller_linear_tram tram = transport_ref?.resolve() as datum_transport_controller_linear_tram;

        // Check for stopped states.
        if (tram == null || !Dm.truthy(tram.controller_operational) || !Dm.truthy(tram.controller_active) || !Dm.truthy(inbound) || !Dm.truthy(outbound))
            return FALSE;

        obj_structure_transport_linear_tram tram_part = tram.return_closest_platform_to(parent as atom) as obj_structure_transport_linear_tram;

        if (QDELETED(tram_part))
            return FALSE;

        if (source == null)
            return FALSE;

        float toast_prob = NORMAL_TOAST_PROB;
        if (Dm.truthy(source.broken) || Dm.truthy(source.burnt) || HAS_TRAIT(future_tram_victim, TRAIT_CURSED))
            toast_prob = BROKEN_TOAST_PROB;

        if (prob(100 - toast_prob))
        {
            if (prob(25))
            {
                do_sparks(1, FALSE, source);
                playsound(parent as atom, SFX_SPARKS, 40, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
                source.audible_message(span_danger(I18n.T("[] makes an electric crackle...", parent)));
            }
            return FALSE;
        }

        // Everything will be based on position and travel direction
        float plate_pos;
        float tram_pos;
        float tram_velocity_sign; // 1 for positive axis movement, -1 for negative
        // Try to be agnostic about N-S vs E-W movement
        if (Dm.truthy(Dm.bitand(tram.travel_direction, Dm.bitor(NORTH, SOUTH))))
        {
            plate_pos = source.y;
            tram_pos = tram_part.y;
            tram_velocity_sign = Dm.truthy(Dm.bitand(tram.travel_direction, NORTH)) ? 1 : -1;
        }
        else
        {
            plate_pos = source.x;
            tram_pos = tram_part.x;
            tram_velocity_sign = Dm.truthy(Dm.bitand(tram.travel_direction, EAST)) ? 1 : -1;
        }

        // How far away are we? negative if already passed.
        float approach_distance = tram_velocity_sign * (plate_pos - (tram_pos + DEFAULT_TRAM_MIDPOINT));

        // Check if our victim is in the active path of the tram.
        if (!Dm.truthy(tram.controller_active))
            return FALSE;
        if (approach_distance < 0)
            return FALSE;
        if (Dm.truthy(Dm.bitand(tram.travel_direction, WEST)) && Dm.lt(inbound, tram.destination_platform.platform_code))
            return FALSE;
        if (Dm.truthy(Dm.bitand(tram.travel_direction, EAST)) && Dm.gt(outbound, tram.destination_platform.platform_code))
            return FALSE;
        if (approach_distance >= XING_THRESHOLD_AMBER)
            return FALSE;

        // Finally the interesting part where they ACTUALLY get hit!
        do_sparks(4, FALSE, source);
        playsound(parent as atom, SFX_SPARKS, 75, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        source.audible_message(span_danger(I18n.T("[] makes a loud electric crackle!", parent)));
        to_chat(future_tram_victim, span_userdanger(I18n.T("You hear a loud electric crackle!")));
        Waiting.Detach(future_tram_victim.electrocute_act(15, parent, 1));
        return TRUE;
    }
}
