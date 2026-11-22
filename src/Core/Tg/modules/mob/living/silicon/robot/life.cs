// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/robot/life.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326).

// tg: code/modules/mob/living/silicon/robot/life.dm:1
public partial class mob_living_silicon_robot : mob_living_silicon
{
    // tg: code/modules/mob/living/silicon/robot/life.dm:1
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        if (HAS_TRAIT(this, TRAIT_NO_TRANSFORM))
            return null;

        object dot = base.Life(seconds_per_tick, __extra);
        handle_robot_hud_updates();
        Waiting.NoSleep(handle_robot_cell(seconds_per_tick));
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/life.dm:9
    public virtual async DmTask handle_robot_cell(object seconds_per_tick = null)
    {
        if (stat == DEAD)
            return;

        if (Dm.truthy(low_power_mode))
        {
            if (Dm.truthy(cell__obj?.charge))
                low_power_mode = FALSE;
        }
        else if (!IS_UNCONSCIOUS_OR_CRIT(this))
            await use_energy(seconds_per_tick);
    }

    // tg: code/modules/mob/living/silicon/robot/life.dm:19
    public virtual async DmTask use_energy(object seconds_per_tick = null)
    {
        if (Dm.truthy(cell__obj?.charge))
        {
            if (Dm.num(cell__obj.charge) <= 0.01f * STANDARD_CELL_CHARGE)
                await drop_all_held_items();
            float energy_consumption = max(lamp_power_consumption * lamp_enabled * lamp_intensity * Dm.num(seconds_per_tick), BORG_MINIMUM_POWER_CONSUMPTION * Dm.num(seconds_per_tick)); //Lamp will use a max of 5 * [BORG_LAMP_POWER_CONSUMPTION], depending on brightness of lamp. If lamp is off, borg systems consume [BORG_MINIMUM_POWER_CONSUMPTION], or the rest of the cell if it's lower than that.
            await cell__obj.use(energy_consumption, Dm.named("force", TRUE));
        }
        else
        {
            await drop_all_held_items();
            low_power_mode = TRUE;
            await toggle_headlamp(TRUE);
        }
        diag_hud_set_borgcell();
    }

    // tg: code/modules/mob/living/silicon/robot/life.dm:31
    public virtual void handle_robot_hud_updates()
    {
        if (client == null)
            return;

        update_cell_hud_icon();
    }

    // tg: code/modules/mob/living/silicon/robot/life.dm:37
    public override object update_health_hud(object healthpercent = null)
    {
        if (client == null || hud_used == null)
            return null;

        atom_movable_screen_healths healths = hud_used.screen_objects[HUD_MOB_HEALTH] as atom_movable_screen_healths;
        if (healths == null)
            return null;

        if (stat == DEAD)
        {
            healths.icon_state = "health7";
            return null;
        }

        float health = Dm.num(this.health), maxHealth = Dm.num(this.maxHealth);
        if (health >= maxHealth)
            healths.icon_state = "health0";
        else if (health > maxHealth * 0.6f)
            healths.icon_state = "health2";
        else if (health > maxHealth * 0.2f)
            healths.icon_state = "health3";
        else if (health > -maxHealth * 0.2f)
            healths.icon_state = "health4";
        else if (health > -maxHealth * 0.6f)
            healths.icon_state = "health5";
        else
            healths.icon_state = "health6";
        return null;
    }

    // tg: code/modules/mob/living/silicon/robot/life.dm:62
    public virtual void update_cell_hud_icon()
    {
        if (cell != null)
        {
            float cellcharge = Dm.num(cell__obj.charge) / Dm.num(cell__obj.maxcharge);
            if (cellcharge >= 0.75f)
                clear_alert(ALERT_CHARGE);
            else if (cellcharge >= 0.5f)
                throw_alert(ALERT_CHARGE, typeof(atom_movable_screen_alert_lowcell), 1);
            else if (cellcharge >= 0.25f)
                throw_alert(ALERT_CHARGE, typeof(atom_movable_screen_alert_lowcell), 2);
            else if (cellcharge >= 0.01f)
                throw_alert(ALERT_CHARGE, typeof(atom_movable_screen_alert_lowcell), 3);
            else
                throw_alert(ALERT_CHARGE, typeof(atom_movable_screen_alert_emptycell));
        }
        else
            throw_alert(ALERT_CHARGE, typeof(atom_movable_screen_alert_nocell));
    }
}
