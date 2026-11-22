// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/transit_tubes/transit_tube_construction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Transit tubes (the owner, 2026-10-26): the whole file. The unattached sections the RPD lays (turned by simple_rotation, flipped by the
// RPD's post_rotation) and wrenched into tubes; the unattached pod is dragged into a station (station.dm's mouse_drop_receive). The var
// blocks and the subtypes (only vars) are generated.

// transit tube construction

// normal transit tubes
// tg: code/game/objects/structures/transit_tubes/transit_tube_construction.dm:5
public partial class obj_structure_c_transit_tube : obj_structure
{
    // tg: code/game/objects/structures/transit_tubes/transit_tube_construction.dm:16
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        return dot;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_construction.dm:20
    public virtual object can_wrench_in_loc(mob user = null)
    {
        turf source_turf = get_turf(loc);
        float existing_tubes = 0;
        foreach (obj_structure_transit_tube tube in Dm.each<obj_structure_transit_tube>(source_turf))
        {
            existing_tubes += 1;
            if (existing_tubes >= 2)
            {
                to_chat(user, Dm.text("[] ", span_warning(I18n.T("You cannot wrench any more transit tubes!"))));
                return FALSE;
            }
        }
        return TRUE;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_construction.dm:30
    public virtual void post_rotation(mob user = null, object degrees = null)
    {
        if (flipped_build_type != null && Dm.eq(degrees, ROTATION_FLIP))
        {
            Waiting.NoSleep(setDir(turn(dir, Dm.num(degrees)))); //Turn back we don't actually flip
            flipped = Dm.not(flipped);
            float cur_flip = Dm.truthy(Dm.initial<obj_structure_c_transit_tube, float>(this, t => t.flipped)) ? Dm.not(flipped) : flipped;
            if (Dm.truthy(cur_flip))
                build_type = flipped_build_type;
            else
                build_type = Dm.initial<obj_structure_c_transit_tube, Type>(this, t => t.build_type);
            icon_state = Dm.text("[][]", base_icon_state, flipped);
        }
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_construction.dm:41
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item I = null)
    {
        await base.wrench_act(user, I);
        if (!Dm.truthy(can_wrench_in_loc(user)))
            return null;
        to_chat(user, span_notice(I18n.T("You start attaching \\the []...", this)));
        add_fingerprint(user);
        if (Dm.truthy(await I.use_tool(this, user, 2 * SECONDS, volume: 50, extra_checks: Dm.@new<datum_callback>(this, nameof(can_wrench_in_loc), user))))
        {
            to_chat(user, span_notice(I18n.T("You attach \\the [].", this)));
            obj_structure_transit_tube R = Dm.@new(build_type, loc, dir) as obj_structure_transit_tube;
            transfer_fingerprints_to(R);
            qdel(this);
        }
        return TRUE;
    }
}
