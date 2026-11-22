// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/eye/eye.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1c, D316: the camera eye of the infiltrator's navigation computer). The var block is generated (D111).

/// Eye mob, used by cameras and overminds such as blobs.
// tg: code/modules/mob/eye/eye.dm:2
public partial class mob_eye : mob
{
    // tg: code/modules/mob/eye/eye.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_GODMODE, INNATE_TRAIT);
        Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this));
        if (!Dm.truthy(move_on_shuttle))
            ADD_TRAIT(this, TRAIT_BLOCK_SHUTTLE_MOVEMENT, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/eye/eye.dm:23
    public override void experience_pressure_difference(object pressure_difference = null, object direction = null, float? pressure_resistance_prob_delta = null)
    {
    }

    // tg: code/modules/mob/eye/eye.dm:26
    public override object canUseStorage() => FALSE;

    // tg: code/modules/mob/eye/eye.dm:29
    public override async DmTask<object> up()
    {
        if (Dm.truthy(await zMove(UP, z_move_flags: ZMOVE_FEEDBACK)))
            to_chat(this, span_notice(I18n.T("You move upwards.")));
        return null;
    }

    // tg: code/modules/mob/eye/eye.dm:33
    public override async DmTask<object> down()
    {
        if (Dm.truthy(await zMove(DOWN, z_move_flags: ZMOVE_FEEDBACK)))
            to_chat(this, span_notice(I18n.T("You move down.")));
        return null;
    }

    // tg: code/modules/mob/eye/eye.dm:37
    public override object can_z_move(object direction = null, turf start = null, turf destination = null, float? z_move_flags = null, mob_living rider = null)
    {
        z_move_flags ??= NONE;
        z_move_flags = (float)((int)z_move_flags | ZMOVE_IGNORE_OBSTACLES); //cameras do not respect these FLOORS you speak so much of
        return base.can_z_move(direction, start, destination, z_move_flags, rider);
    }

    // tg: code/modules/mob/eye/eye.dm:41
    public override async DmTask<object> emote(object act = null, float? type_override = null, object message = null, float? intentional = null, float? force_silence = null, float? forced = null)
    {
        if (Dm.truthy(has_emotes))
            return await base.emote(act, type_override, message, intentional, force_silence, forced);
        return FALSE;
    }

    // tg: code/modules/mob/eye/eye.dm:46
    public override void update_sight()
    {
        lighting_color_cutoffs = Dm.list(lighting_cutoff_red, lighting_cutoff_green, lighting_cutoff_blue);
        base.update_sight();
    }
}
