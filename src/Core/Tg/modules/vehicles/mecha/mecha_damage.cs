// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/mecha_damage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*!
 * # Mecha defence explanation
 * Mechs focus is on a more heavy-but-slower damage approach
 * For this they have the following mechanics
 *
 * ## Backstab
 * Basically the tldr is that mechs are less flexible so we encourage good positioning, pretty simple
 * ## Armor modules
 * Pretty simple, adds armor, you can choose against what
 * ## Internal damage
 * When taking damage will force you to take some time to repair, encourages improvising in a fight
 * Targeting different def zones will damage them to encurage a more strategic approach to fights
 * where they target the "dangerous" modules
 */

// tg: code/modules/vehicles/mecha/mecha_damage.dm:17
public partial class obj_vehicle_sealed_mecha : obj_vehicle_sealed
{
    /// returns a number for the damage multiplier for this relative angle/dir
    // tg: code/modules/vehicles/mecha/mecha_damage.dm:17
    public virtual object get_armour_facing(float relative_dir = 0)
    {
        if (relative_dir == 180) // BACKSTAB!
            return facing_modifiers[MECHA_BACK_ARMOUR];
        if (relative_dir == 0 || relative_dir == 45) // direct or 45 degrees off
            return facing_modifiers[MECHA_FRONT_ARMOUR];
        return facing_modifiers[MECHA_SIDE_ARMOUR]; //if its not a front hit or back hit then assume its from the side
    }

    ///tries to deal internal damaget depending on the damage amount
    // tg: code/modules/vehicles/mecha/mecha_damage.dm:26
    public virtual void try_deal_internal_damage(object damage = null)
    {
        float internal_damage_threshold = Dm.num(max_integrity) / 10; /// Mech internal damage can only occur if the resulting hit exceeds a tenth of the mecha's maximum integrity in a single blow.
        if (Dm.num(damage) < internal_damage_threshold)
            return;
        if (!prob(internal_damage_probability))
            return;
        float internal_damage_to_deal = possible_int_damage;
        Dm.mask(ref internal_damage_to_deal, Dm.bitnot(internal_damage));
        if (internal_damage_to_deal != 0)
            set_internal_damage(pick(bitfield_to_list(internal_damage_to_deal)));
    }

    /// tries to repair any internal damage and plays fluff for it
    // tg: code/modules/vehicles/mecha/mecha_damage.dm:38
    public virtual async DmTask try_repair_int_damage(mob user = null, object flag_to_heal = null)
    {
        balloon_alert(user, get_int_repair_fluff_start(flag_to_heal));
        log_message(Dm.text("[] starting internal damage repair for flag []", key_name(user), flag_to_heal), LOG_MECHA);
        if (!Dm.truthy(await do_after(user, 10 * SECONDS, this)))
        {
            balloon_alert(user, get_int_repair_fluff_fail(flag_to_heal));
            log_message(Dm.text("Internal damage repair for flag [] failed.", flag_to_heal), LOG_MECHA, color: "red");
            return;
        }
        clear_internal_damage(flag_to_heal);
        balloon_alert(user, get_int_repair_fluff_end(flag_to_heal));
        log_message(Dm.text("Finished internal damage repair for flag []", flag_to_heal), LOG_MECHA);
    }

    ///gets the starting balloon alert flufftext
    // tg: code/modules/vehicles/mecha/mecha_damage.dm:50
    public virtual string get_int_repair_fluff_start(object flag = null)
    {
        float f = Dm.num(flag);
        if (f == MECHA_INT_FIRE)
            return I18n.T("activating internal fire supression...");
        if (f == MECHA_INT_TEMP_CONTROL)
            return I18n.T("resetting temperature module...");
        if (f == MECHA_CABIN_AIR_BREACH)
            return I18n.T("activating cabin breach sealant...");
        if (f == MECHA_INT_CONTROL_LOST)
            return I18n.T("recalibrating coordination system...");
        if (f == MECHA_INT_SHORT_CIRCUIT)
            return I18n.T("flushing internal capacitor...");
        return null;
    }

    ///gets the successful finish balloon alert flufftext
    // tg: code/modules/vehicles/mecha/mecha_damage.dm:64
    public virtual string get_int_repair_fluff_end(object flag = null)
    {
        float f = Dm.num(flag);
        if (f == MECHA_INT_FIRE)
            return I18n.T("internal fire supressed");
        if (f == MECHA_INT_TEMP_CONTROL)
            return I18n.T("temperature chip reactivated");
        if (f == MECHA_CABIN_AIR_BREACH)
            return I18n.T("cabin breach sealed");
        if (f == MECHA_INT_CONTROL_LOST)
            return I18n.T("coordination re-established");
        if (f == MECHA_INT_SHORT_CIRCUIT)
            return I18n.T("internal capacitor reset");
        return null;
    }

    ///gets the on-fail balloon alert flufftext
    // tg: code/modules/vehicles/mecha/mecha_damage.dm:78
    public virtual string get_int_repair_fluff_fail(object flag = null)
    {
        float f = Dm.num(flag);
        if (f == MECHA_INT_FIRE)
            return I18n.T("fire supression canceled");
        if (f == MECHA_INT_TEMP_CONTROL)
            return I18n.T("reset aborted");
        if (f == MECHA_CABIN_AIR_BREACH)
            return I18n.T("sealant deactivated");
        if (f == MECHA_INT_CONTROL_LOST)
            return I18n.T("recalibration failed");
        if (f == MECHA_INT_SHORT_CIRCUIT)
            return I18n.T("capacitor flush failure");
        return null;
    }

    // tg: code/modules/vehicles/mecha/mecha_damage.dm:91
    public virtual void set_internal_damage(object int_dam_flag = null)
    {
        Dm.combine(ref internal_damage, int_dam_flag);
        log_message(Dm.text("Internal damage of type [].", int_dam_flag), LOG_MECHA);
        foreach (mob occupant in Dm.each<mob>(occupants))
            Dm.output_to(occupant, sound(Dm.resource("sound/machines/warning-buzzer.ogg"), wait: 0)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        diag_hud_set_mechstat();
    }

    // tg: code/modules/vehicles/mecha/mecha_damage.dm:97
    public virtual void clear_internal_damage(object int_dam_flag = null)
    {
        if (Dm.truthy(Dm.bitand(internal_damage, int_dam_flag)))
        {
            float f = Dm.num(int_dam_flag);
            if (f == MECHA_INT_TEMP_CONTROL)
                to_chat(occupants, span_boldnotice(I18n.T("Life support system reactivated.")));
            else if (f == MECHA_INT_FIRE)
                to_chat(occupants, span_boldnotice(I18n.T("Internal fire extinguished.")));
            else if (f == MECHA_CABIN_AIR_BREACH)
                to_chat(occupants, span_boldnotice(I18n.T("Cabin breach has been sealed.")));
            else if (f == MECHA_INT_CONTROL_LOST)
                to_chat(occupants, span_boldnotice(I18n.T("Control module reactivated.")));
            else if (f == MECHA_INT_SHORT_CIRCUIT)
                to_chat(occupants, span_boldnotice(I18n.T("Internal capacitor has been reset successfully.")));
        }
        Dm.mask(ref internal_damage, Dm.bitnot(int_dam_flag));
        diag_hud_set_mechstat();
    }
}
