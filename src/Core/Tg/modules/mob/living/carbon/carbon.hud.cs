// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/carbon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the HUD procs of carbon.dm (update_damage_hud :479, the crit, critvision, oxy and brute fullscreens, and
// update_health_hud :574, the health indicator), split off for M2 P3 (D107; the M2 P2 task, "the HUD procs update_health_hud
// and its fullscreens are P3's partial"). The rest of carbon.dm is carbon.cs (P2).

// tg: code/modules/mob/living/carbon/carbon.dm:480
public partial class mob_living_carbon : mob_living
{
    //this handles hud updates
    // tg: code/modules/mob/living/carbon/carbon.dm:480
    public override void update_damage_hud()
    {

        if (!Dm.truthy(client))
            return;

        if (health <= crit_threshold && !HAS_TRAIT(this, TRAIT_NOCRITOVERLAY))
        {
            float severity = 0;
            float sw__1 = health;
            if (sw__1 >= -20 && sw__1 <= -10)
                severity = 1;
            else if (sw__1 >= -30 && sw__1 <= -20)
                severity = 2;
            else if (sw__1 >= -40 && sw__1 <= -30)
                severity = 3;
            else if (sw__1 >= -50 && sw__1 <= -40)
                severity = 4;
            else if (sw__1 >= -50 && sw__1 <= -40) // tg's duplicate range: severity 5 is unreachable (kept as written)
                severity = 5;
            else if (sw__1 >= -60 && sw__1 <= -50)
                severity = 6;
            else if (sw__1 >= -70 && sw__1 <= -60)
                severity = 7;
            else if (sw__1 >= -90 && sw__1 <= -70)
                severity = 8;
            else if (sw__1 >= -95 && sw__1 <= -90)
                severity = 9;
            else if (sw__1 >= -INFINITY && sw__1 <= -95)
                severity = 10;
            if (stat != HARD_CRIT)
            {
                float visionseverity = 4;
                float sw__2 = health;
                if (sw__2 >= -8 && sw__2 <= -4)
                    visionseverity = 5;
                else if (sw__2 >= -12 && sw__2 <= -8)
                    visionseverity = 6;
                else if (sw__2 >= -16 && sw__2 <= -12)
                    visionseverity = 7;
                else if (sw__2 >= -20 && sw__2 <= -16)
                    visionseverity = 8;
                else if (sw__2 >= -24 && sw__2 <= -20)
                    visionseverity = 9;
                else if (sw__2 >= -INFINITY && sw__2 <= -24)
                    visionseverity = 10;
                overlay_fullscreen("critvision", typeof(atom_movable_screen_fullscreen_crit_vision), visionseverity);
            }
            else
                clear_fullscreen("critvision");
            overlay_fullscreen("crit", typeof(atom_movable_screen_fullscreen_crit), severity);
        }
        else
        {
            clear_fullscreen("crit");
            clear_fullscreen("critvision");
        }

        //Oxygen damage overlay
        if (Dm.truthy(oxyloss))
        {
            float severity = 0;
            float sw__3 = oxyloss;
            if (sw__3 >= 10 && sw__3 <= 20)
                severity = 1;
            else if (sw__3 >= 20 && sw__3 <= 25)
                severity = 2;
            else if (sw__3 >= 25 && sw__3 <= 30)
                severity = 3;
            else if (sw__3 >= 30 && sw__3 <= 35)
                severity = 4;
            else if (sw__3 >= 35 && sw__3 <= 40)
                severity = 5;
            else if (sw__3 >= 40 && sw__3 <= 45)
                severity = 6;
            else if (sw__3 >= 45 && sw__3 <= INFINITY)
                severity = 7;
            overlay_fullscreen("oxy", typeof(atom_movable_screen_fullscreen_oxy), severity);
        }
        else
            clear_fullscreen("oxy");

        //Fire and Brute damage overlay (BSSR)
        float hurtdamage = Dm.num(get_brute_loss()) + Dm.num(get_fire_loss()) + damageoverlaytemp;
        if (Dm.truthy(hurtdamage) && !HAS_TRAIT(this, TRAIT_NO_DAMAGE_OVERLAY))
        {
            float severity = 0;
            float sw__4 = hurtdamage;
            if (sw__4 >= 5 && sw__4 <= 15)
                severity = 1;
            else if (sw__4 >= 15 && sw__4 <= 30)
                severity = 2;
            else if (sw__4 >= 30 && sw__4 <= 45)
                severity = 3;
            else if (sw__4 >= 45 && sw__4 <= 70)
                severity = 4;
            else if (sw__4 >= 70 && sw__4 <= 85)
                severity = 5;
            else if (sw__4 >= 85 && sw__4 <= INFINITY)
                severity = 6;
            overlay_fullscreen("brute", typeof(atom_movable_screen_fullscreen_brute), severity);
        }
        else
            clear_fullscreen("brute");
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:574
    [DmParams("shown_health_amount")]
    public override object update_health_hud(object shown_health_amount = null)
    {
        if (!Dm.truthy(client) || !Dm.truthy(hud_used))
            return null;

        atom_movable_screen_healths health_hud = hud_used.screen_objects[HUD_MOB_HEALTH] as atom_movable_screen_healths;

        if (!Dm.truthy(health_hud))
            return null;

        if (stat == DEAD)
        {
            health_hud.icon_state = "health7";
            return null;
        }

        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_CARBON_UPDATING_HEALTH_HUD]) ? NONE : this._SendSignal(COMSIG_CARBON_UPDATING_HEALTH_HUD, Dm.list(this, shown_health_amount))), COMPONENT_OVERRIDE_HEALTH_HUD)))
            return null;

        if (shown_health_amount == null)
            shown_health_amount = health;

        if (Dm.num(shown_health_amount) >= maxHealth)
            health_hud.icon_state = "health0";
        else if (Dm.num(shown_health_amount) > 0 && maxHealth > 0)
            health_hud.icon_state = Dm.text("health[]", 6 - ceil(Dm.num(shown_health_amount) / (maxHealth * 0.2f)));
        else
            health_hud.icon_state = "health6";
        return null;
    }
}
