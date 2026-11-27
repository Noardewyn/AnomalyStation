// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/step_triggers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the step trigger's base (it fires when something walks onto its tile), the message trigger, the sound trigger and the
// outfitter. The thrower, the stopper and the teleporters wait for their maps. The var blocks are generated.

public sealed partial class TgState
{
    // tg: code/game/objects/effects/step_triggers.dm:12 (var/static/list/loc_connections)
    internal DmList obj_effect_step_trigger__Initialize__loc_connections;
}

/* Simple object type, calls a proc when "stepped" on by something */
// tg: code/game/objects/effects/step_triggers.dm:3
public partial class obj_effect_step_trigger : obj_effect
{
    // tg: code/game/objects/effects/step_triggers.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList loc_connections = TgState.Current.obj_effect_step_trigger__Initialize__loc_connections ??= Dm.list(
            Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_entered)));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
        return dot;
    }

    // tg: code/game/objects/effects/step_triggers.dm:17
    public virtual async DmTask<object> Trigger(atom_movable A = null)
    {
        await DmTask.CompletedTask;
        return 0;
    }

    // tg: code/game/objects/effects/step_triggers.dm:20
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable entering = null)
    {
        if (entering == null || entering == this || Dm.num(entering.invisibility) >= INVISIBILITY_ABSTRACT || entering is atom_movable_mirage_holder) //dont teleport ourselves, abstract objects, and mirage holders due to init shenanigans
            return;
        if (isobserver(entering) && !Dm.truthy(affect_ghosts))
            return;
        if (!ismob(entering) && Dm.truthy(mobs_only))
            return;
        Waiting.Detach(Trigger(entering));
    }

    // tg: code/game/objects/effects/step_triggers.dm:31
    public override DmTask<object> singularity_act()
    {
        return default;
    }

    // tg: code/game/objects/effects/step_triggers.dm:34
    public override DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return default;
    }
}

/* Sends a message to mob when triggered*/
// tg: code/game/objects/effects/step_triggers.dm:39
public partial class obj_effect_step_trigger_message : obj_effect_step_trigger
{
    // tg: code/game/objects/effects/step_triggers.dm:44
    [DmParams("M")]
    public override async DmTask<object> Trigger(atom_movable A = null)
    {
        await DmTask.CompletedTask;
        var M = (mob)A;
        if (M.client != null)
        {
            to_chat(M, span_info(Dm.text("[]", message)));
            if (Dm.truthy(once))
                qdel(this);
        }
        return null;
    }
}

/* Simple sound player, Mapper friendly! */
// tg: code/game/objects/effects/step_triggers.dm:194
public partial class obj_effect_step_trigger_sound_effect : obj_effect_step_trigger
{
    // tg: code/game/objects/effects/step_triggers.dm:203
    public override async DmTask<object> Trigger(atom_movable A = null)
    {
        await DmTask.CompletedTask;
        turf T = get_turf(A);

        if (T == null)
            return null;

        if (Dm.truthy(triggerer_only) && ismob(A))
        {
            var B = (mob)A;
            B.playsound_local(T, this.sound, volume, freq_vary);
        }
        else
            playsound(T, this.sound, volume, freq_vary, extra_range);

        if (Dm.truthy(happens_once))
            qdel(this);
        return null;
    }
}

/// Forces a given outfit onto any carbon which crosses it, for event maps
// tg: code/game/objects/effects/step_triggers.dm:225
public partial class obj_effect_step_trigger_outfitter : obj_effect_step_trigger
{
    // tg: code/game/objects/effects/step_triggers.dm:231
    public override async DmTask<object> Trigger(atom_movable A = null)
    {
        if (!ishuman(A))
            return null;

        var fellow = (mob_living_carbon_human)A;
        await fellow.delete_equipment();
        await fellow.equipOutfit(outfit_to_equip, FALSE);

        if (Dm.truthy(happens_once))
            qdel(this);
        return null;
    }
}
