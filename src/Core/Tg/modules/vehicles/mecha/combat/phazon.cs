// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/combat/phazon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/mecha/combat/phazon.dm:1
public partial class obj_vehicle_sealed_mecha_phazon : obj_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/combat/phazon.dm:34
    public override void generate_actions()
    {
        base.generate_actions();
        initialize_passenger_action_type(typeof(datum_action_vehicle_sealed_mecha_mech_toggle_phasing));
        initialize_passenger_action_type(typeof(datum_action_vehicle_sealed_mecha_mech_switch_damtype));
    }

    // tg: code/modules/vehicles/mecha/combat/phazon.dm:39
    public override object CanPassThrough(atom blocker = null, object movement_dir = null, object blocker_opinion = null)
    {
        if (!Dm.truthy(phasing) || Dm.num(get_charge()) <= phasing_energy_drain || Dm.truthy(throwing))
            return base.CanPassThrough(blocker, movement_dir, blocker_opinion);
        if (Dm.truthy(phase_state))
            flick(phase_state, this);
        turf destination_turf = get_step(loc, Dm.num(movement_dir)) as turf;
        if (!Dm.truthy(check_teleport_valid(this, destination_turf)) || Dm.truthy(SSmapping.level_trait(destination_turf.z, ZTRAIT_NOPHASE)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/combat/phazon.dm:49
    public override async DmTask<object> vehicle_move(object direction = null, object forcerotate = null)
    {
        object dot = await base.vehicle_move(direction, forcerotate);
        if (Dm.truthy(dot) && Dm.truthy(phasing))
            use_energy(phasing_energy_drain);
        return dot;
    }

    // The tile crossing of a capsule-walked Phazon: the same drain as vehicle_move's, the walk does not go through it (DV358).
    public override void capsule_crossed()
    {
        if (Dm.truthy(phasing))
            use_energy(phasing_energy_drain);
    }

    // tg: code/modules/vehicles/mecha/combat/phazon.dm:54
    public override async DmTask try_bumpsmash(atom obstacle = null)
    {
        if (Dm.truthy(phasing)) // Theres only one cause for phasing canpass fails
        {
            to_chat(occupants, span_warning(I18n.T("A dull, universal force is preventing you from phasing here!")));
            await spark_system.start();
            return;
        }
        await base.try_bumpsmash(obstacle);
    }

    // tg: code/modules/vehicles/mecha/combat/phazon.dm:61
    public override void update_energy_drain()
    {
        base.update_energy_drain();
        if (capacitor != null)
            phasing_energy_drain = Dm.num(Dm.initial(this, nameof(phasing_energy_drain))) / Dm.num(capacitor.rating);
        else
            phasing_energy_drain = Dm.num(Dm.initial(this, nameof(phasing_energy_drain)));
    }

    // tg: code/modules/vehicles/mecha/combat/phazon.dm:68
    public override bool can_interact_with(atom target = null, mob user = null, DmList modifiers = null)
    {
        bool dot = base.can_interact_with(target, user, modifiers);
        if (!dot || !Dm.truthy(phasing))
            return dot;
        balloon_alert(user, I18n.T("not while phasing!"));
        return false;
    }
}

// tg: code/modules/vehicles/mecha/combat/phazon.dm:75
public partial class datum_action_vehicle_sealed_mecha_mech_switch_damtype : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/combat/phazon.dm:79
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (chassis == null || !Dm.@in(owner, chassis.occupants))
            return dot;
        string new_damtype = null;
        if (Dm.eq(chassis.damtype, TOX))
        {
            new_damtype = BRUTE;
            chassis.balloon_alert(owner, I18n.T("your punches will now deal brute damage"));
        }
        else if (Dm.eq(chassis.damtype, BRUTE))
        {
            new_damtype = BURN;
            chassis.balloon_alert(owner, I18n.T("your punches will now deal burn damage"));
        }
        else if (Dm.eq(chassis.damtype, BURN))
        {
            new_damtype = TOX;
            chassis.balloon_alert(owner, I18n.T("your punches will now deal toxin damage"));
        }
        chassis.damtype = new_damtype;
        button_icon_state = Dm.text("mech_damtype_[]", new_damtype);
        playsound(chassis, Dm.resource("sound/vehicles/mecha/mechmove01.ogg"), 50, TRUE);
        await build_all_button_icons();
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/combat/phazon.dm:101
public partial class datum_action_vehicle_sealed_mecha_mech_toggle_phasing : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/combat/phazon.dm:105
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (chassis == null || !Dm.@in(owner, chassis.occupants))
            return dot;
        obj_vehicle_sealed_mecha_phazon phazon = chassis as obj_vehicle_sealed_mecha_phazon;
        phazon.phasing = Dm.truthy(phazon.phasing) ? 0 : 1;
        button_icon_state = Dm.text("mech_phasing_[]", Dm.truthy(phazon.phasing) ? "on" : "off");
        phazon.balloon_alert(owner, I18n.T("[] phasing", Dm.truthy(phazon.phasing) ? "enabled" : "disabled"));
        await build_all_button_icons();
        return dot;
    }
}
