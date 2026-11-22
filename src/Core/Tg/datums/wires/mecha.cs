// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/mecha.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/datums/wires/mecha.dm:1
public partial class datum_wires_mecha : datum_wires
{
    // tg: code/datums/wires/mecha.dm:5
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        wires = Dm.list(WIRE_IDSCAN, WIRE_DISARM, WIRE_ZAP, WIRE_OVERCLOCK, WIRE_LAUNCH);
        obj_vehicle_sealed_mecha mecha = __extra[0] as obj_vehicle_sealed_mecha;
        if (Dm.truthy(Dm.bitand(mecha.mecha_flags, HAS_LIGHTS)))
            wires.Add(WIRE_LIGHT);
        add_duds(3);
        await base.New(__extra);
    }

    // tg: code/datums/wires/mecha.dm:13
    public override bool interactable(mob user)
    {
        if (!base.interactable(user))
            return false;
        obj_vehicle_sealed_mecha mecha = holder as obj_vehicle_sealed_mecha;
        return Dm.truthy(Dm.bitand(mecha.mecha_flags, PANEL_OPEN));
    }

    // tg: code/datums/wires/mecha.dm:19
    public override DmList get_status()
    {
        obj_vehicle_sealed_mecha mecha = holder as obj_vehicle_sealed_mecha;
        DmList status = Dm.list();
        status.Add(I18n.T("The orange light is [].", Dm.truthy(Dm.bitand(mecha.internal_damage, MECHA_INT_SHORT_CIRCUIT)) ? "on" : "off"));
        status.Add(I18n.T("The red light is [].", Dm.truthy(mecha.overclock_mode) ? "blinking" : "off"));
        status.Add(I18n.T("The green light is [].", Dm.truthy(Dm.bitand(mecha.mecha_flags, ID_LOCK_ON)) || Dm.truthy(mecha.dna_lock) ? "on" : "off"));
        if (Dm.truthy(Dm.bitand(mecha.mecha_flags, HAS_LIGHTS)))
            status.Add(I18n.T("The yellow light is [].", Dm.truthy(mecha.light_on) ? "on" : "off"));
        status.Add(I18n.T("The blue light is [].", Dm.truthy(mecha.equipment_disabled) ? "on" : "off"));
        return status;
    }

    // tg: code/datums/wires/mecha.dm:30
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_vehicle_sealed_mecha mecha = holder as obj_vehicle_sealed_mecha;
        if (Dm.eq(wire, WIRE_IDSCAN))
        {
            mecha.mecha_flags = Dm.bitxor(mecha.mecha_flags, ID_LOCK_ON);
            mecha.dna_lock = null;
        }
        else if (Dm.eq(wire, WIRE_DISARM))
        {
            mecha.equipment_disabled = TRUE;
            mecha.set_mouse_pointer();
        }
        else if (Dm.eq(wire, WIRE_ZAP))
            mecha.internal_damage = Dm.bitxor(mecha.internal_damage, MECHA_INT_SHORT_CIRCUIT);
        else if (Dm.eq(wire, WIRE_LIGHT))
            await mecha.set_light_on(Dm.truthy(mecha.light_on) ? FALSE : TRUE);
        else if (Dm.eq(wire, WIRE_OVERCLOCK))
            await mecha.toggle_overclock();
        else if (Dm.eq(wire, WIRE_LAUNCH))
            await try_attack(user);
    }

    // tg: code/datums/wires/mecha.dm:46
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        obj_vehicle_sealed_mecha mecha = holder as obj_vehicle_sealed_mecha;
        if (Dm.eq(wire, WIRE_IDSCAN))
        {
            if (!Dm.truthy(mend))
            {
                Dm.mask(ref mecha.mecha_flags, Dm.bitnot(ID_LOCK_ON));
                mecha.dna_lock = null;
            }
        }
        else if (Dm.eq(wire, WIRE_DISARM))
        {
            mecha.equipment_disabled = Dm.truthy(mend) ? 0 : 1;
            mecha.set_mouse_pointer();
        }
        else if (Dm.eq(wire, WIRE_ZAP))
        {
            if (Dm.truthy(mend))
                Dm.mask(ref mecha.internal_damage, Dm.bitnot(MECHA_INT_SHORT_CIRCUIT));
            else
            {
                Dm.combine(ref mecha.internal_damage, MECHA_INT_SHORT_CIRCUIT);
                if (isliving(source))
                    await mecha.shock__proc(source, 50);
            }
        }
        else if (Dm.eq(wire, WIRE_LIGHT))
            await mecha.set_light_on(Dm.truthy(mend) ? FALSE : TRUE);
        else if (Dm.eq(wire, WIRE_OVERCLOCK))
        {
            if (!Dm.truthy(mend))
                await mecha.toggle_overclock(FALSE);
        }
        else if (Dm.eq(wire, WIRE_LAUNCH))
        {
            if (!Dm.truthy(mend))
                await try_attack(source);
        }
    }

    // tg: code/datums/wires/mecha.dm:72
    public virtual async DmTask try_attack(mob_living target = null)
    {
        obj_vehicle_sealed_mecha mecha = holder as obj_vehicle_sealed_mecha;
        if (Dm.truthy(mecha.occupant_amount())) //no powergamers sorry
            return;
        DmList armaments = Dm.list();
        if (mecha.equip_by_category[MECHA_R_ARM] != null)
            armaments.Add(mecha.equip_by_category[MECHA_R_ARM]);
        if (mecha.equip_by_category[MECHA_L_ARM] != null)
            armaments.Add(mecha.equip_by_category[MECHA_L_ARM]);
        obj_item_mecha_parts_mecha_equipment armament = Dm.truthy(length(armaments)) ? pick(armaments) as obj_item_mecha_parts_mecha_equipment : null; //null makes a melee attack
        if (target == null)
        {
            target = Dm.locate<mob_living>(view(Dm.truthy(length(armaments)) ? 5 : 1, mecha));
            if (target == null) // still no target
                return;
        }

        float disabled = mecha.equipment_disabled;
        if (armament != null && Dm.truthy(Dm.bitand(armament.range, MECHA_RANGED)))
        {
            mecha.equipment_disabled = FALSE; // honestly just avoid this wire
            Waiting.Detach(armament.action(mecha as object as mob, target));
            mecha.equipment_disabled = disabled;
            return;
        }
        if (Dm.truthy(await mecha.Adjacent(target)) && !Dm.truthy(TIMER_COOLDOWN_RUNNING(mecha, COOLDOWN_MECHA_MELEE_ATTACK)) && Dm.truthy(await target.mech_melee_attack(mecha)))
            mecha.timer_cooldown_start(COOLDOWN_MECHA_MELEE_ATTACK, mecha.melee_cooldown);
    }

    // tg: code/datums/wires/mecha.dm:100
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        mob user = ui.user;
        obj_vehicle_sealed_mecha mecha = holder as obj_vehicle_sealed_mecha;
        if (!Dm.truthy(HAS_SILICON_ACCESS(user)) && Dm.truthy(Dm.bitand(mecha.internal_damage, MECHA_INT_SHORT_CIRCUIT)) && Dm.truthy(await mecha.shock__proc(usr as mob_living)))
            return FALSE;
        return dot;
    }

    // tg: code/datums/wires/mecha.dm:109
    public override bool can_reveal_wires(mob user)
    {
        if (HAS_TRAIT(user, TRAIT_KNOW_ROBO_WIRES))
            return true;
        return base.can_reveal_wires(user);
    }
}
