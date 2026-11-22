// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/screwy_health_doll.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The health doll is the tg screen object of the hallucinator (D107), so the fake severity reaches only his
// screen. The var block is generated (D111).

///Causes the target to see incorrect health damages on the healthdoll
// tg: code/modules/hallucination/screwy_health_doll.dm:2
public partial class datum_hallucination_fake_health_doll : datum_hallucination
{
    // tg: code/modules/hallucination/screwy_health_doll.dm:14
    [DmParams("hallucinator", "duration")]
    public override async DmTask New(params object[] __extra)
    {
        object duration = Dm.arg(__extra, 1) ?? (50 * SECONDS);
        this.duration = duration;
        await base.New(__extra);
    }

    // So that the associated addition proc cleans it up correctly
    // tg: code/modules/hallucination/screwy_health_doll.dm:19
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(del_timer_id))
            deltimer(del_timer_id);

        foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(bodyparts.Copy()))
            remove_bodypart(limb);

        hallucinator.update_health_hud();
        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/screwy_health_doll.dm:29
    public override async DmTask<object> start()
    {
        if (!ishuman(hallucinator))
            return FALSE;

        add_fake_limb();
        del_timer_id = QDEL_IN_HALLUCINATION(this, Dm.num(duration), "code/modules/hallucination/screwy_health_doll.dm", 34, stoppable: true);
        return TRUE;
    }

    /// Increments the severity of the damage seen on all the limbs we are already tracking.
    // tg: code/modules/hallucination/screwy_health_doll.dm:37
    public virtual void increment_fake_damage()
    {
        foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(bodyparts))
            bodyparts[limb] = clamp(Dm.num(bodyparts[limb]) + 1, 1, 5);

        hallucinator.update_health_hud();
    }

    /**
     * Adds a fake limb to the effect.
     *
     * specific_limb - optional, the specific limb to apply the effect to. If not passed, picks a random limb
     * seveirty - optional, the specific severity level to apply the effect. Clamped from 1 to 5. If not passed, picks a random number.
     */
    // tg: code/modules/hallucination/screwy_health_doll.dm:50
    public virtual void add_fake_limb(obj_item_bodypart specific_limb = null, object severity = null)
    {
        mob_living_carbon_human human_mob = (mob_living_carbon_human)hallucinator;

        obj_item_bodypart picked = specific_limb ?? (pick(human_mob.get_bodyparts()) as obj_item_bodypart);
        if (!Dm.@in(picked, bodyparts))
        {
            RegisterSignals(picked, Dm.list(COMSIG_QDELETING, COMSIG_BODYPART_REMOVED), nameof(remove_bodypart));
            RegisterSignal(picked, COMSIG_BODYPART_UPDATING_HEALTH_HUD, nameof(on_bodypart_hud_update));
            RegisterSignal(picked, COMSIG_BODYPART_CHECKED_FOR_INJURY, nameof(on_bodypart_checked));
        }

        bodyparts[picked] = clamp(Dm.truthy(severity) ? Dm.num(severity) : rand(1, 5), 1, 5);
        hallucinator.update_health_hud();
    }

    /// Remove a bodypart from our list, unregistering all associated signals and handling the reference
    // tg: code/modules/hallucination/screwy_health_doll.dm:62
    [SignalHandler]
    public virtual void remove_bodypart(obj_item_bodypart source = null)
    {
        UnregisterSignal(source, Dm.list(COMSIG_QDELETING, COMSIG_BODYPART_REMOVED, COMSIG_BODYPART_UPDATING_HEALTH_HUD, COMSIG_BODYPART_CHECKED_FOR_INJURY));
        Dm.remove(bodyparts, source);
    }

    /// Whenever a bodypart we're tracking has their health hud updated, override it with our fake overlay
    // tg: code/modules/hallucination/screwy_health_doll.dm:69
    [SignalHandler]
    public virtual object on_bodypart_hud_update(obj_item_bodypart source = null, mob_living_carbon_human owner = null, DmList overridable_key = null)
    {
        overridable_key[1] = bodyparts[source];
        return OVERRIDE_BODYPART_HEALTH_HUD;
    }

    /// Signal proc for [COMSIG_BODYPART_CHECKED_FOR_INJURY]. Our bodyparts look a lot more wounded than they actually are.
    // tg: code/modules/hallucination/screwy_health_doll.dm:76
    [SignalHandler]
    public virtual void on_bodypart_checked(obj_item_bodypart source = null, mob_living_carbon examiner = null, DmList check_list = null, DmList limb_damage = null)
    {
        limb_damage[BRUTE] = Dm.num(bodyparts[source]) * 0.2f * source.max_damage;
    }
}
