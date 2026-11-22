// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/on_fire.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file (the fake fire alert is in _onclick/hud/screen_objects/alert.cs and the rolling subtype in
// datums/status_effects/buffs/stop_drop_roll.cs). The var blocks are generated (D111).

// tg: code/modules/hallucination/on_fire.dm:1  #define RAISE_FIRE_COUNT 3, #define RAISE_FIRE_TIME 3 (file-local)
// tg: code/modules/hallucination/on_fire.dm:4
public partial class datum_hallucination_fire : datum_hallucination
{
    private const float RAISE_FIRE_COUNT = 3;
    private const float RAISE_FIRE_TIME = 3;

    // tg: code/modules/hallucination/on_fire.dm:33
    public virtual object make_overlay()
    {
        mutable_appearance real_overlay = hallucinator.get_fire_overlay(fake_firestacks) as mutable_appearance;
        if (real_overlay == null)
            return null;
        if (real_overlay.icon_state == fire_overlay?.icon_state)
            return fire_overlay;

        image new_overlay = image(real_overlay.icon, hallucinator, real_overlay.icon_state, real_overlay.layer);
        new_overlay.appearance_flags = real_overlay.appearance_flags;
        return new_overlay;
    }

    // tg: code/modules/hallucination/on_fire.dm:43
    public override async DmTask<object> start()
    {
        fake_firestacks = rand(5, 15);
        fire_overlay = make_overlay() as image;
        if (fire_overlay == null)
            return FALSE;

        if (hallucinator.client != null)
            Dm.combine(hallucinator.client.images, fire_overlay);
        to_chat(hallucinator, span_userdanger("You're set on fire!"));
        await hallucinator.add_mood_event("on_fire", typeof(datum_mood_event_on_fire));
        atom_movable_screen_alert_fire_fake alert = hallucinator.throw_alert(ALERT_FIRE, typeof(atom_movable_screen_alert_fire_fake), @override: TRUE) as atom_movable_screen_alert_fire_fake;
        alert.hallucination_weakref = WEAKREF(this) as datum_weakref;
        times_to_lower_stamina = rand(5, 10);
        _addtimer(Dm.@new<datum_callback>(this, nameof(start_expanding)), 2 * SECONDS, file: "code/modules/hallucination/on_fire.dm", line: 55);
        return TRUE;
    }

    // tg: code/modules/hallucination/on_fire.dm:58
    public override object Destroy(float? force = null)
    {
        hallucinator.clear_alert(ALERT_FIRE, clear_override: TRUE);
        hallucinator.clear_alert(ALERT_TEMPERATURE, clear_override: TRUE);
        if (!Dm.truthy(hallucinator.on_fire))
        {
            hallucinator.clear_mood_event("on_fire");
            Waiting.NoSleep(hallucinator.add_mood_event("fake_fire", typeof(datum_mood_event_fake_fire)));
        }
        if (fire_overlay != null)
        {
            if (hallucinator.client != null)
                Dm.remove(hallucinator.client.images, fire_overlay);
            fire_overlay = null;
        }

        STOP_PROCESSING(SSfastprocess, this);
        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/on_fire.dm:73
    public virtual void start_expanding()
    {
        if (QDELETED(this))
            return;

        START_PROCESSING(SSfastprocess, this);
    }

    // tg: code/modules/hallucination/on_fire.dm:79
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        float seconds = Dm.num(seconds_per_tick);
        if (QDELETED(this))
            return null;

        fake_firestacks -= (0.25f * seconds);
        if (fake_firestacks <= 0)
            clear_fire();
        else
        {
            object new_overlay = make_overlay();
            if (new_overlay != null && new_overlay != fire_overlay)
            {
                if (hallucinator.client != null)
                    Dm.remove(hallucinator.client.images, fire_overlay);
                fire_overlay = new_overlay as image;
                if (hallucinator.client != null)
                    Dm.combine(hallucinator.client.images, fire_overlay);
            }
        }

        time_spent += seconds;

        if (Dm.truthy(fire_clearing))
        {
            next_action -= seconds;
            if (next_action < 0)
            {
                stage -= 1;
                update_temp();
                next_action += 3;
            }
        }

        else if (Dm.truthy(increasing_stages))
        {
            float new_stage = min(round(time_spent / RAISE_FIRE_TIME), RAISE_FIRE_COUNT);
            if (stage != new_stage)
            {
                stage = new_stage;
                update_temp();

                if (stage == RAISE_FIRE_COUNT)
                    increasing_stages = FALSE;
            }
        }

        else if (Dm.truthy(times_to_lower_stamina))
        {
            next_action -= seconds;
            if (next_action < 0)
            {
                Waiting.NoSleep(hallucinator.adjust_stamina_loss(15));
                next_action += 2;
                times_to_lower_stamina = Dm.num(times_to_lower_stamina) - 1;
            }
        }

        else
            clear_fire();
        return null;
    }

    // tg: code/modules/hallucination/on_fire.dm:121
    public virtual void update_temp()
    {
        if (stage <= 0)
        {
            hallucinator.clear_alert(ALERT_TEMPERATURE, clear_override: TRUE);
            if (!Dm.truthy(active))
                qdel(this);
        }
        else
        {
            hallucinator.clear_alert(ALERT_TEMPERATURE, clear_override: TRUE);
            hallucinator.throw_alert(ALERT_TEMPERATURE, typeof(atom_movable_screen_alert_hot), stage, @override: TRUE);
        }
    }

    // tg: code/modules/hallucination/on_fire.dm:131
    public virtual void clear_fire()
    {
        if (!Dm.truthy(active))
            return;

        active = FALSE;
        hallucinator.clear_alert(ALERT_FIRE, clear_override: TRUE);
        if (hallucinator.client != null)
            Dm.remove(hallucinator.client.images, fire_overlay);
        fire_overlay = null;
        fire_clearing = TRUE;
        next_action = 0;
    }
}
