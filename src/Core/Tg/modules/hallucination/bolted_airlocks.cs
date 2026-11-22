// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/bolted_airlocks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The fake bolt lights are images on the hallucinator's client over the airlock. The var blocks are generated (D111).

// tg: code/modules/hallucination/bolted_airlocks.dm:1
public partial class datum_hallucination_bolts : datum_hallucination
{
    // tg: code/modules/hallucination/bolted_airlocks.dm:13
    public override async DmTask<object> start()
    {
        float door_number = rand(0, 4); //if 0, we bolt all visible doors
        feedback_details += Dm.text("Door amount: []", door_number);

        foreach (obj_machinery_door_airlock nearby_airlock in Dm.each<obj_machinery_door_airlock>(view(hallucinator)))
        {
            if (LAZYLEN(airlocks_to_hit) > door_number && door_number > 0)
                break;
            if (!Dm.truthy(nearby_airlock.density))
                continue;
            LAZYADD(ref airlocks_to_hit, WEAKREF(nearby_airlock));
        }

        if (LAZYLEN(airlocks_to_hit) == 0) // Not an airlock in sight
            return FALSE;

        START_PROCESSING(SSfastprocess, this);
        return TRUE;
    }

    // tg: code/modules/hallucination/bolted_airlocks.dm:31
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (QDELETED(this))
            return null;

        next_action -= (Dm.num(seconds_per_tick) * 10);
        if (next_action > 0)
            return null;

        if (Dm.truthy(locking))
        {
            datum_weakref next_airlock = pop(airlocks_to_hit) as datum_weakref;
            obj_machinery_door_airlock to_lock = next_airlock?.resolve() as obj_machinery_door_airlock;
            if (to_lock != null)
            {
                obj_effect_client_image_holder_hallucination_fake_door_lock lock_ = Dm.@new<obj_effect_client_image_holder_hallucination_fake_door_lock>(to_lock.loc, hallucinator, this, to_lock);
                LAZYADD(ref locks, WEAKREF(lock_));
            }

            if (LAZYLEN(airlocks_to_hit) == 0)
            {
                locking = FALSE;
                next_action = 100;
                return null;
            }
        }

        else
        {
            datum_weakref next_unlock = popleft(locks) as datum_weakref;
            obj_effect_client_image_holder_hallucination_fake_door_lock to_unlock = next_unlock?.resolve() as obj_effect_client_image_holder_hallucination_fake_door_lock;
            if (to_unlock != null)
                to_unlock.unlock();

            else
            {
                // All unlocked, qdel time
                qdel(this);
                return null;
            }
        }

        next_action = rand(4, 12);
        return null;
    }

    // tg: code/modules/hallucination/bolted_airlocks.dm:70
    public override object Destroy(float? force = null)
    {
        // Clean up any locks we happen to have remaining on qdel.
        // Hypothetically this shouldn't delete anything. But just in case.
        foreach (datum_weakref leftover_lock_ref in Dm.each_cast<datum_weakref>(locks))
        {
            obj_effect_client_image_holder_hallucination_fake_door_lock leftover_lock = leftover_lock_ref.resolve() as obj_effect_client_image_holder_hallucination_fake_door_lock;
            if (!QDELETED(leftover_lock))
                qdel(leftover_lock);
        }

        STOP_PROCESSING(SSfastprocess, this);
        return base.Destroy(force);
    }
}

// tg: code/modules/hallucination/bolted_airlocks.dm:82
public partial class obj_effect_client_image_holder_hallucination_fake_door_lock : obj_effect_client_image_holder_hallucination
{
    // tg: code/modules/hallucination/bolted_airlocks.dm:90
    [DmParams("mapload", "mobs_which_see_us", "parent", "airlock")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_machinery_door_airlock airlock = Dm.arg<obj_machinery_door_airlock>(__extra, 2);
        if (airlock == null)
        {
            stack_trace(Dm.text("[] was created somewhere without an associated airlock.", type), "code/modules/hallucination/bolted_airlocks.dm", 92);
            return INITIALIZE_HINT_QDEL;
        }

        this.airlock = airlock;
        RegisterSignal(airlock, COMSIG_QDELETING, nameof(on_airlock_deleted));
        // We need to grab these for our image before we run our parent's parent initialize
        this.image_icon = airlock.overlays_file;
        this.image_state = Dm.text("lights_[]", AIRLOCK_LIGHT_BOLTS);

        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/hallucination/bolted_airlocks.dm:101
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(airlock, COMSIG_QDELETING);
        airlock = null;
        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/bolted_airlocks.dm:106
    public override object generate_image()
    {
        image created = base.generate_image() as image;
        created.layer = airlock.layer + 0.1f;
        return created;
    }

    // tg: code/modules/hallucination/bolted_airlocks.dm:111
    public override void show_image_to(mob show_to = null)
    {
        base.show_image_to(show_to);
        show_to.playsound_local(get_turf(this), Dm.resource("sound/machines/airlock/boltsdown.ogg"), 30, FALSE, 3);
    }

    // tg: code/modules/hallucination/bolted_airlocks.dm:115
    public override void hide_image_from(mob show_to = null)
    {
        base.hide_image_from(show_to);
        show_to.playsound_local(get_turf(this), Dm.resource("sound/machines/airlock/boltsup.ogg"), 30, FALSE, 3);
    }

    // tg: code/modules/hallucination/bolted_airlocks.dm:119
    [SignalHandler]
    public virtual void on_airlock_deleted(datum source = null)
    {
        qdel(this);
    }

    // tg: code/modules/hallucination/bolted_airlocks.dm:124
    public virtual void unlock()
    {
        foreach (mob seer in Dm.each_cast<mob>(who_sees_us.Copy()))
            hide_image_from(seer);

        shown_image = null;
        qdel(this);
    }

    // tg: code/modules/hallucination/bolted_airlocks.dm:131
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        if (Dm.@in(mover, who_sees_us) && Dm.truthy(airlock.density))
            return FALSE;

        return await base.CanAllowThrough(mover, border_dir);
    }
}
