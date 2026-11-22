// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/life_link.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file (the holoparasite's link to its summoner's health). The var block is generated.

/**
 * A mob with this component passes all damage (and healing) it takes to another mob, passed as a parameter
 * Essentially we use another mob's health bar as our health bar
 */
// tg: code/datums/components/life_link.dm:5
public partial class datum_component_life_link : datum_component
{
    // the standard damage types' signals: COMSIG_LIVING_ADJUST_STANDARD_DAMAGE_TYPES (signals_mob_living.dm:95), a list literal
    private static DmList standard_damage_signals() => Dm.list(COMSIG_LIVING_ADJUST_BRUTE_DAMAGE, COMSIG_LIVING_ADJUST_BURN_DAMAGE, COMSIG_LIVING_ADJUST_OXY_DAMAGE, COMSIG_LIVING_ADJUST_TOX_DAMAGE);

    // tg: code/datums/components/life_link.dm:14
    [DmParams("host", "on_passed_damage", "on_linked_death")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object host = Dm.arg(__extra, 0);
        datum_callback on_passed_damage = Dm.arg<datum_callback>(__extra, 1);
        datum_callback on_linked_death = Dm.arg<datum_callback>(__extra, 2);
        await base.Initialize(__extra);
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        if (host is not mob_living living_host)
            throw Dm.CRASH(Dm.text("Life link created on [] and attempted to link to invalid type [].", parent.type, (host as datum)?.type), "code/datums/components/life_link.dm", 19);
        await register_host(living_host);
        this.on_passed_damage = on_passed_damage;
        this.on_linked_death = on_linked_death;
        return null;
    }

    // tg: code/datums/components/life_link.dm:24
    public override async DmTask RegisterWithParent()
    {
        if (!iscarbon(parent))
            RegisterSignals(parent, standard_damage_signals(), nameof(on_damage_adjusted));
        else
        {
            RegisterSignal(parent, COMSIG_CARBON_LIMB_DAMAGED, nameof(on_limb_damage)); //carbon mobs handle brute and burn damage differently
            RegisterSignals(parent, Dm.list(COMSIG_LIVING_ADJUST_OXY_DAMAGE, COMSIG_LIVING_ADJUST_TOX_DAMAGE), nameof(on_damage_adjusted));
        }
        RegisterSignal(parent, COMSIG_LIVING_HEALTH_UPDATE, nameof(on_health_updated));
        if (host != null)
        {
            mob_living living_parent = (mob_living)parent;
            await living_parent.updatehealth();
        }
    }

    // tg: code/datums/components/life_link.dm:35
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        unregister_host();
        UnregisterSignal(parent, Dm.add(Dm.list(COMSIG_CARBON_LIMB_DAMAGED, COMSIG_LIVING_HEALTH_UPDATE), standard_damage_signals()));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/life_link.dm:39
    [DmParams("new_comp", "i_am_original", "host", "on_passed_damage", "on_linked_death")]
    public override async DmTask InheritComponent(datum_component new_comp = null, object i_am_original = null, params object[] __extra)
    {
        mob_living host = Dm.arg(__extra, 0) as mob_living;
        await register_host(host);
    }

    /// Set someone up as our new host
    // tg: code/datums/components/life_link.dm:43
    public virtual async DmTask register_host(mob_living new_host = null)
    {
        unregister_host();
        if (new_host == null)
            return;
        host = new_host;
        RegisterSignal(host, COMSIG_LIVING_DEATH, nameof(on_host_died));
        RegisterSignal(host, COMSIG_LIVING_HEALTH_UPDATE, nameof(on_health_updated));
        RegisterSignal(host, COMSIG_LIVING_REVIVE, nameof(on_host_revived));
        RegisterSignal(host, COMSIG_QDELETING, nameof(on_host_deleted));
        mob_living living_parent = (mob_living)parent;
        await living_parent.updatehealth();
    }

    /// Drop someone from being our host
    // tg: code/datums/components/life_link.dm:56
    public virtual void unregister_host()
    {
        if (host == null)
            return;
        UnregisterSignal(host, Dm.list(COMSIG_LIVING_DEATH, COMSIG_LIVING_HEALTH_UPDATE, COMSIG_LIVING_REVIVE, COMSIG_QDELETING));
        host = null;
    }

    /// Called when your damage goes up or down
    // tg: code/datums/components/life_link.dm:63
    [SignalHandler]
    public virtual object on_damage_adjusted(mob_living our_mob = null, object type = null, object amount = null, object forced = null)
    {
        if (Dm.truthy(forced))
            return null;
        amount = Dm.mul(amount, our_mob.get_incoming_damage_modifier(Dm.num(amount), Dm.str(type)));
        Waiting.NoSleep(host.apply_damage(Dm.num(amount), Dm.str(type), spread_damage: TRUE));

        if (on_passed_damage != null)
            Waiting.NoSleep(on_passed_damage.Invoke(our_mob, host, amount));
        return COMPONENT_IGNORE_CHANGE;
    }

    /// Called when someone hurts one of our limbs, bypassing normal damage adjustment
    // tg: code/datums/components/life_link.dm:74
    [SignalHandler]
    public virtual object on_limb_damage(mob_living our_mob = null, obj_item_bodypart part = null, object brute = null, object burn = null)
    {
        if (Dm.num(brute) != 0)
            Waiting.NoSleep(host.apply_damage(Dm.num(brute), BRUTE, part.body_zone));
        if (Dm.num(burn) != 0)
            Waiting.NoSleep(host.apply_damage(Dm.num(burn), BURN, part.body_zone));
        if (on_passed_damage != null)
            Waiting.NoSleep(on_passed_damage.Invoke(our_mob, host, Dm.num(brute) + Dm.num(burn)));
        return COMPONENT_PREVENT_LIMB_DAMAGE;
    }

    /// Called when either the host or parent's health tries to update, update our displayed health
    // tg: code/datums/components/life_link.dm:84
    [SignalHandler]
    public virtual void on_health_updated()
    {
        update_health_hud(parent as mob_living);
        update_med_hud_health(parent as mob_living);
        update_med_hud_status(parent as mob_living);
    }

    /// Update our parent's health display based on how harmed our host is
    // tg: code/datums/components/life_link.dm:91
    public virtual void update_health_hud(mob_living mob_parent = null)
    {
        float severity = 0;
        float healthpercent = health_percentage(host);
        if (healthpercent >= 100)
            severity = 0;
        else if (healthpercent >= 85)
            severity = 1;
        else if (healthpercent >= 70)
            severity = 2;
        else if (healthpercent >= 55)
            severity = 3;
        else if (healthpercent >= 40)
            severity = 4;
        else if (healthpercent >= 25)
            severity = 5;
        else
            severity = 6;
        if (severity > 0)
            mob_parent.overlay_fullscreen("brute", typeof(atom_movable_screen_fullscreen_brute), severity);
        else
            mob_parent.clear_fullscreen("brute");

        atom_movable_screen health_display = mob_parent.hud_used?.screen_objects?[HUD_MOB_HEALTH] as atom_movable_screen;
        if (health_display != null)
            health_display.maptext = MAPTEXT(Dm.text("<div align='center' valign='middle' style='position:relative; top:0px; left:6px'><font color='#efeeef'>[]%</font></div>", round(healthpercent, 0.5f)));
    }

    /// Update our health on the medical hud
    // tg: code/datums/components/life_link.dm:118
    public virtual void update_med_hud_health(mob_living mob_parent = null)
    {
        mob_parent.set_hud_image_state(HEALTH_HUD, Dm.text("hud[]", RoundHealth(host)));
    }

    /// Update our vital status on the medical hud
    // tg: code/datums/components/life_link.dm:122
    public virtual void update_med_hud_status(mob_living mob_parent = null)
    {
        if (IS_DEAD_OR_FAKING(host))
            mob_parent.set_hud_image_state(STATUS_HUD, "huddead");
        else
            mob_parent.set_hud_image_state(STATUS_HUD, "hudhealthy");
    }

    /// Called when our host dies, we should die too
    // tg: code/datums/components/life_link.dm:129
    [SignalHandler]
    public virtual void on_host_died(mob_living source = null, object gibbed = null)
    {
        if (on_linked_death != null)
            Waiting.NoSleep(on_linked_death.Invoke(parent, host, gibbed));
        mob_living living_parent = (mob_living)parent;
        Waiting.NoSleep(living_parent.death(gibbed));
    }

    /// Called when our host undies, we should undie too
    // tg: code/datums/components/life_link.dm:136
    [SignalHandler]
    public virtual void on_host_revived(mob_living source = null, object full_heal_flags = null)
    {
        mob_living living_parent = (mob_living)parent;
        Waiting.NoSleep(living_parent.revive(full_heal_flags == null ? null : Dm.num(full_heal_flags)));
    }

    /// Called when
    // tg: code/datums/components/life_link.dm:142
    [SignalHandler]
    public virtual void on_host_deleted()
    {
        qdel(this);
    }
}
