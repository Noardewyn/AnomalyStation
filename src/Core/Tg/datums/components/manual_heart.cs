// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/manual_heart.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps RG2, D339: corazargh, the inverse of ephedrine, makes its patient pump the heart by hand; the cursed heart
// uses it too). The var blocks are generated (D111).

/**
 * Manual heart pumping component. Requires the holder to pump their heart manually every
 * so often or die.
 *
 * Mainly used by the cursed heart.
 */
// tg: code/datums/components/manual_heart.dm:12
public partial class datum_component_manual_heart : datum_component
{
    /// If a beat is missed, how long to give before the next is missed
    // tg: code/datums/components/manual_heart.dm:2  #define MANUAL_HEART_GRACE_PERIOD 2 SECONDS (file-local, #undef at :186)
    private const float MANUAL_HEART_GRACE_PERIOD = 2 * SECONDS;

    // tg: code/datums/components/manual_heart.dm:30
    [DmParams("pump_delay", "blood_loss", "heal_brute", "heal_burn", "heal_oxy")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object pump_delay = Dm.arg(__extra, 0) ?? 3 * SECONDS;
        object blood_loss = Dm.arg(__extra, 1) ?? BLOOD_VOLUME_NORMAL * 0.2f;
        object heal_brute = Dm.arg(__extra, 2) ?? 0;
        object heal_burn = Dm.arg(__extra, 3) ?? 0;
        object heal_oxy = Dm.arg(__extra, 4) ?? 0;
        //Non-Carbon mobs can't have hearts, and should never receive this component.
        if (!iscarbon(parent))
        {
            stack_trace(Dm.text("Manual Heart component added to [] ([]) which is not a /mob/living/carbon subtype.", parent, parent?.type), "code/datums/components/manual_heart.dm", 33);
            return COMPONENT_INCOMPATIBLE;
        }

        this.pump_delay = Dm.num(pump_delay);
        this.blood_loss = Dm.num(blood_loss);
        this.heal_brute = Dm.num(heal_brute);
        this.heal_burn = Dm.num(heal_burn);
        this.heal_oxy = Dm.num(heal_oxy);

        pump_action = await Dm.new_async<datum_action_cooldown_manual_heart>(this);
        return null;
    }

    // tg: code/datums/components/manual_heart.dm:44
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref pump_action);
        return base.Destroy(force);
    }

    // tg: code/datums/components/manual_heart.dm:48
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_CARBON_LOSE_ORGAN, nameof(check_removed_organ));
        RegisterSignal(parent, COMSIG_CARBON_GAIN_ORGAN, nameof(check_added_organ));
        RegisterSignal(parent, COMSIG_HEART_MANUAL_PULSE, nameof(on_pump));
        RegisterSignal(parent, COMSIG_LIVING_UPDATE_BLOOD_STATUS, nameof(on_update_blood_status));

        pump_action.cooldown_time = pump_delay - (1 * SECONDS); //you can pump up to a second early
        await pump_action.Grant(parent as mob);

        mob_living_carbon carbon_parent = (mob_living_carbon)parent;
        obj_item_organ_heart parent_heart = carbon_parent.get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        if (parent_heart != null && Dm.truthy(CAN_HAVE_BLOOD(carbon_parent)) && carbon_parent.stat != DEAD)
        {
            START_PROCESSING(SSdcs, this);
            heart_timer = world.time + pump_delay; // COOLDOWN_START (__DEFINES/cooldowns.dm:110), inline
        }

        to_chat(parent, span_userdanger(I18n.T("Your heart no longer beats automatically! You have to pump it manually - otherwise you'll die!")));
    }

    // tg: code/datums/components/manual_heart.dm:65
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_CARBON_GAIN_ORGAN, COMSIG_CARBON_LOSE_ORGAN, COMSIG_HEART_MANUAL_PULSE, COMSIG_LIVING_REVIVE, COMSIG_LIVING_DEATH, COMSIG_LIVING_UPDATE_BLOOD_STATUS));

        to_chat(parent, span_userdanger(I18n.T("You feel your heart start beating normally again!")));
        mob_living_carbon carbon_parent = parent as mob_living_carbon;
        if (carbon_parent != null)
            carbon_parent.remove_client_colour(REF(this));
    }

    // tg: code/datums/components/manual_heart.dm:73
    [SignalHandler]
    public virtual void restart()
    {
        if (!Dm.truthy(check_valid()))
            return;
        heart_timer = world.time + pump_delay; // COOLDOWN_START, inline
        Waiting.NoSleep(pump_action.build_all_button_icons(UPDATE_BUTTON_STATUS)); //make sure the action button always shows as available when it is
        START_PROCESSING(SSdcs, this);
    }

    // tg: code/datums/components/manual_heart.dm:82
    [SignalHandler]
    public virtual void pause()
    {
        Waiting.NoSleep(pump_action.build_all_button_icons(UPDATE_BUTTON_STATUS));
        mob_living_carbon carbon_parent = parent as mob_living_carbon;
        if (carbon_parent != null)
            carbon_parent.remove_client_colour(REF(this)); //prevents red overlay from getting stuck
        STOP_PROCESSING(SSdcs, this);
    }

    /// Worker proc that checks logic for if a pump can happen, and applies effects from doing so
    // tg: code/datums/components/manual_heart.dm:91
    public virtual async DmTask on_pump(mob owner = null)
    {
        heart_timer = world.time + pump_delay; // COOLDOWN_START, inline
        playsound(owner, Dm.resource("sound/effects/singlebeat.ogg"), 40, TRUE);

        mob_living_carbon carbon_owner = owner as mob_living_carbon;

        if (!Dm.truthy(CAN_HAVE_BLOOD(carbon_owner)))
            return;
        carbon_owner.adjust_blood_volume(blood_loss * 0.5f);
        carbon_owner.remove_client_colour(REF(this));
        add_colour = TRUE;
        await carbon_owner.adjust_brute_loss(-heal_brute);
        await carbon_owner.adjust_fire_loss(-heal_burn);
        await carbon_owner.adjust_oxy_loss(-heal_oxy);
    }

    // tg: code/datums/components/manual_heart.dm:106
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        mob_living_carbon carbon_parent = (mob_living_carbon)parent;

        //If they aren't connected, don't kill them.
        if (carbon_parent.client == null)
        {
            heart_timer = world.time + pump_delay; // COOLDOWN_START, inline
            return null;
        }

        if (!(heart_timer <= world.time)) // COOLDOWN_FINISHED, inline
            return null;

        carbon_parent.adjust_blood_volume(-blood_loss);
        to_chat(carbon_parent, span_userdanger(I18n.T("You have to keep pumping your blood!")));
        heart_timer = world.time + MANUAL_HEART_GRACE_PERIOD; //give two full seconds before losing more blood
        if (Dm.truthy(add_colour))
        {
            carbon_parent.add_client_colour(typeof(datum_client_colour_manual_heart_blood), REF(this));
            add_colour = FALSE;
        }
        return null;
    }

    // tg: code/datums/components/manual_heart.dm:124
    [SignalHandler]
    public virtual void on_update_blood_status(datum source = null, object had_blood = null, object has_blood = null, object new_blood_volume = null, object old_blood_volume = null)
    {
        if (Dm.truthy(has_blood))
            restart();
        else
            pause();
    }

    ///If a new heart is added, start processing.
    // tg: code/datums/components/manual_heart.dm:133
    [SignalHandler]
    public virtual void check_added_organ(mob organ_owner = null, obj_item_organ new_organ = null)
    {
        obj_item_organ_heart new_heart = new_organ as obj_item_organ_heart;

        if (new_heart == null || !Dm.truthy(check_valid()))
            return;
        heart_timer = world.time + pump_delay; // COOLDOWN_START, inline
        Waiting.NoSleep(pump_action.build_all_button_icons(UPDATE_BUTTON_STATUS));
        mob_living_carbon carbon_parent = parent as mob_living_carbon;
        if (carbon_parent != null)
            carbon_parent.remove_client_colour(REF(this)); //prevents red overlay from getting stuck
        START_PROCESSING(SSdcs, this);
    }

    ///If the heart is removed, stop processing.
    // tg: code/datums/components/manual_heart.dm:148
    [SignalHandler]
    public virtual void check_removed_organ(mob organ_owner = null, obj_item_organ removed_organ = null)
    {
        obj_item_organ_heart removed_heart = removed_organ as obj_item_organ_heart;

        if (removed_heart != null)
        {
            Waiting.NoSleep(pump_action.build_all_button_icons(UPDATE_BUTTON_STATUS));
            STOP_PROCESSING(SSdcs, this);
        }
    }

    ///Helper proc to check if processing can be restarted.
    // tg: code/datums/components/manual_heart.dm:158
    public virtual object check_valid()
    {
        mob_living_carbon carbon_parent = (mob_living_carbon)parent;
        obj_item_organ_heart parent_heart = carbon_parent.get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        return Dm.num(parent_heart != null && Dm.truthy(CAN_HAVE_BLOOD(carbon_parent)) && carbon_parent.stat != DEAD);
    }
}

///Action to pump your heart. Cooldown will always be set to 1 second less than the pump delay.
// tg: code/datums/components/manual_heart.dm:165
public partial class datum_action_cooldown_manual_heart : datum_action_cooldown
{
    // tg: code/datums/components/manual_heart.dm:171
    public override async DmTask<object> Activate(atom target = null)
    {
        object dot = await base.Activate(target);

        owner.SEND_SIGNAL(COMSIG_HEART_MANUAL_PULSE, Dm.list(owner));
        return dot;
    }

    ///The action button is only available when you're a living carbon with blood and a heart.
    // tg: code/datums/components/manual_heart.dm:177
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        mob_living_carbon heart_haver = owner as mob_living_carbon;
        if (heart_haver == null || !Dm.truthy(CAN_HAVE_BLOOD(heart_haver)) || heart_haver.stat == DEAD)
            return FALSE;
        obj_item_organ_heart heart_havers_heart = heart_haver.get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        if (heart_havers_heart == null)
            return FALSE;
        return await base.IsAvailable(feedback);
    }
}
