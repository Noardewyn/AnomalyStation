// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/explosive.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M5x slice 4, D149): the explosive wires and C4's. Every wire is a detonation wire (the duds explode too); cutting
// or pulsing one sets the charge off. The wires window that cuts and pulses them is the hacking interface (M7, D150). The chem
// grenade's wires since gaps F1 (its trigger assembly); the pizza bomb's stay with their (inert) holder. The var blocks are generated.

// tg: code/datums/wires/explosive.dm:1
[DmPath("/datum/wires/explosive")]
public partial class datum_wires_explosive : datum_wires
{
    // tg: code/datums/wires/explosive.dm:6
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        add_duds(duds_number); // Duds also explode here.
        await base.New(__extra);
    }

    // tg: code/datums/wires/explosive.dm:10
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        await explode();
    }

    // tg: code/datums/wires/explosive.dm:13
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        mend ??= FALSE;
        if (source != null)
            log_combat(source, holder, "cut the detonation wire for");
        await explode();
    }

    // tg: code/datums/wires/explosive.dm:18
    public virtual async DmTask explode()
    {
        return;
    }
}

// tg: code/datums/wires/explosive.dm:100
[DmPath("/datum/wires/explosive/c4")]
public partial class datum_wires_explosive_c4 : datum_wires_explosive
{
    // tg: code/datums/wires/explosive.dm:104
    public override async DmTask explode()
    {
        obj_item_grenade_c4 bomb = (obj_item_grenade_c4)holder;
        await bomb.detonate();
    }
}

// tg: code/datums/wires/explosive.dm:22
public partial class datum_wires_explosive_chem_grenade : datum_wires_explosive
{
    // tg: code/datums/wires/explosive.dm:26
    public override bool interactable(mob user)
    {
        if (!base.interactable(user))
            return false;
        obj_item_grenade_chem_grenade G = holder as obj_item_grenade_chem_grenade;
        if (G.stage == GRENADE_WIRED)
            return true;
        return false;
    }

    // tg: code/datums/wires/explosive.dm:33
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_item_grenade_chem_grenade grenade = holder as obj_item_grenade_chem_grenade;
        if (grenade.stage != GRENADE_READY)
            return;
        await base.on_pulse(wire, user);
    }

    // tg: code/datums/wires/explosive.dm:39
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        obj_item_grenade_chem_grenade grenade = holder as obj_item_grenade_chem_grenade;
        if (grenade.stage != GRENADE_READY)
            return;
        await base.on_cut(wire, mend, source);
    }

    // tg: code/datums/wires/explosive.dm:45
    public override async DmTask<object> attach_assembly(object color = null, obj_item_assembly assembly = null)
    {
        fingerprint = assembly.fingerprintslast;
        obj_item_grenade_chem_grenade grenade = holder as obj_item_grenade_chem_grenade;
        if (!Dm.truthy(assembly.secured))
            await assembly.toggle_secure();

        if (istype(assembly, typeof(obj_item_assembly_timer)))
        {
            obj_item_assembly_timer timer = assembly as obj_item_assembly_timer;
            grenade.det_time = timer.saved_time * SECONDS;
            return await base.attach_assembly(color, assembly);
        }

        if (istype(assembly, typeof(obj_item_assembly_prox_sensor)))
        {
            obj_item_assembly_prox_sensor sensor = assembly as obj_item_assembly_prox_sensor;
            grenade.landminemode = sensor;
            await sensor.proximity_monitor.set_ignore_if_not_on_turf(FALSE);
            sensor.time = grenade.det_time * 0.1f;
            return await base.attach_assembly(color, assembly);
        }

        if (!istype(assembly, typeof(obj_item_assembly_health)))
            return await base.attach_assembly(color, assembly);

        obj_item_assembly_health sensor__1 = (obj_item_assembly_health)assembly; // tg's `sensor`, renamed beside the prox branch's (rules 2.6)
        if (!Dm.truthy(sensor__1.scanning))
            sensor__1.toggle_scan();
        return await base.attach_assembly(color, assembly);
    }

    // tg: code/datums/wires/explosive.dm:71
    public override async DmTask explode()
    {
        obj_item_grenade_chem_grenade grenade = holder as obj_item_grenade_chem_grenade;
        obj_item_assembly pulser = get_attached(get_wire(1)) as obj_item_assembly;
        string message = Dm.text("\\An [] has pulsed [] ([]), which was installed by []", pulser, grenade, grenade.type, fingerprint);
        if (pulser is obj_item_assembly_voice spoken_trigger)
            message += Dm.text(" with the following activation message: \"[]\"", spoken_trigger.recorded);
        if (!Dm.truthy(grenade.dud_flags))
            message_admins(message);
        log_game(message);
        grenade.log_grenade(get_mob_by_ckey(fingerprint) as mob); //Used in arm_grenade() too but this one conveys where the mob who triggered the bomb is
        if (Dm.truthy(grenade.landminemode))
            await grenade.detonate(); ///already armed
        else
            await grenade.arm_grenade(); //The one here conveys where the bomb was when it went boom
    }

    // tg: code/datums/wires/explosive.dm:88
    public override async DmTask<object> detach_assembly(object color = null)
    {
        obj_item_assembly assembly = get_attached(color) as obj_item_assembly;
        if (!istype(assembly, typeof(obj_item_assembly)))
            return null;

        obj_item_grenade_chem_grenade grenade = holder as obj_item_grenade_chem_grenade;
        LAZYREMOVE(ref assemblies, color);
        assembly.connected = null;
        assembly.holder = null;
        await assembly.forceMove(holder.drop_location() as atom);
        grenade.landminemode = null;
        return assembly;
    }
}

// The syndicate campaign G2 (D316): the pizza bomb's wires.
// tg: code/datums/wires/explosive.dm:107
public partial class datum_wires_explosive_pizza : datum_wires_explosive
{
    // tg: code/datums/wires/explosive.dm:111
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        wires = Dm.list(
            WIRE_DISARM);
        add_duds(3); // Duds also explode here.
        await base.New(__extra);
    }

    // tg: code/datums/wires/explosive.dm:118
    public override bool interactable(mob user)
    {
        if (!base.interactable(user))
            return false;
        obj_item_pizzabox pizza_bomb = (obj_item_pizzabox)holder;
        if (Dm.truthy(pizza_bomb.open) && pizza_bomb.bomb != null)
            return true;
        return false;
    }

    // tg: code/datums/wires/explosive.dm:125
    public override DmList get_status()
    {
        obj_item_pizzabox pizza_bomb = (obj_item_pizzabox)holder;
        DmList status = Dm.list();
        status.Add(I18n.T("The red light is [].", Dm.truthy(pizza_bomb.bomb_active) ? I18n.T("on") : I18n.T("off")));
        status.Add(I18n.T("The green light is [].", Dm.truthy(pizza_bomb.bomb_defused) ? I18n.T("on") : I18n.T("off")));
        return status;
    }

    // tg: code/datums/wires/explosive.dm:132
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_item_pizzabox pizza_bomb = (obj_item_pizzabox)holder;
        if (Dm.eq(wire, WIRE_DISARM)) // Pulse to toggle
            pizza_bomb.bomb_defused = Dm.truthy(pizza_bomb.bomb_defused) ? FALSE : TRUE;
        else // Boom
            await explode();
    }

    // tg: code/datums/wires/explosive.dm:139
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        if (Dm.truthy(mend))
            return;

        obj_item_pizzabox pizza_bomb = (obj_item_pizzabox)holder;
        if (Dm.eq(wire, WIRE_DISARM)) // Disarm and untrap the box.
        {
            pizza_bomb.bomb_defused = TRUE;
            return;
        }

        if (!Dm.truthy(pizza_bomb.bomb_defused))
        {
            if (source != null)
                log_combat(source, holder as atom, "cut the detonation wire for");
            await explode();
        }
    }

    // tg: code/datums/wires/explosive.dm:153
    public override async DmTask explode()
    {
        obj_item_pizzabox pizza_bomb = (obj_item_pizzabox)holder;
        await pizza_bomb.bomb.detonate();
    }
}
