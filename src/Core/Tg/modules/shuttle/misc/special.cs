// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/misc/special.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the bar table that kicks off whoever climbs it but the barstaff (a Lavaland ruin's and the special shuttles' bar; its turn
// with a docking shuttle is in shuttle_rotate_callbacks.cs). The special shuttles' machines and blockers come with their shuttles.
// The var blocks are generated.

public sealed partial class TgState
{
    // tg: code/modules/shuttle/misc/special.dm:181 (var/static/list/loc_connections)
    internal DmList obj_structure_table_wood_shuttle_bar__Initialize__loc_connections;
}

// Bar table, a wooden table that kicks you in a direction if you're not
// barstaff (defined as someone who was a roundstart bartender or someone
// with CENTCOM_BARSTAFF)
// tg: code/modules/shuttle/misc/special.dm:175
public partial class obj_structure_table_wood_shuttle_bar : obj_structure_table_wood
{
    // tg: code/modules/shuttle/misc/special.dm:179
    [DmParams("mapload", "frame_used", "stack_used")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList loc_connections = TgState.Current.obj_structure_table_wood_shuttle_bar__Initialize__loc_connections ??= Dm.list(
            Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_climbed)));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_SCREWDRIVER));
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_WRENCH));
        return dot;
    }

    // tg: code/modules/shuttle/misc/special.dm:188
    [SignalHandler]
    public virtual void on_climbed(datum source = null, atom_movable AM = null)
    {
        if (AM is mob_living M && !Dm.truthy(M.incorporeal_move) && !Dm.truthy(is_barstaff(M)))
        {
            // No climbing on the bar please
            object throwtarget = get_edge_target_turf(this, boot_dir);
            Waiting.Detach(M.Paralyze(40));
            Waiting.Detach(M.throw_at(throwtarget as atom, 5, 1));
            to_chat(M, span_notice(I18n.T("No climbing on the bar please.")));
        }
    }

    // tg: code/modules/shuttle/misc/special.dm:198
    public virtual object is_barstaff(mob_living user = null)
    {
        if (ishuman(user))
        {
            var human_user = (mob_living_carbon_human)user;
            if (human_user.mind?.assigned_role is datum_job_bartender) // is_bartender_job
                return TRUE;
        }

        if (user is mob_living_basic_drone_snowflake_bardrone)
            return TRUE;

        var ID = user.get_idcard(FALSE) as obj_item_card_id;
        if (ID != null && Dm.@in(ACCESS_CENT_BAR, ID.access))
            return TRUE;
        return FALSE;
    }
}
