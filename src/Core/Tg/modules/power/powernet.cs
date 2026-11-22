// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/powernet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice 1 (the power network core). The var block is generated (D111). M7 slice 2: the SMES's restore in reset (:87) is
// translated. M7 slice 3 (the APC): propagate_light_flicker's lights.
//
// GAP(lead): VARSET_CALLBACK (__HELPERS/varset_callback.dm:3) needs the global proc ___callbackvarset, which is not declared
// (vars is not translated, rules 10.7); the flicker's timer calls flickering__varset below, which does for this datum what
// ___callbackvarset does (as doorcontrol.cs's cooldown__varset).

////////////////////////////////////////////
// POWERNET DATUM
// each contiguous network of cables & nodes
/////////////////////////////////////
// tg: code/modules/power/powernet.dm:5
public partial class datum_powernet : datum
{
    // tg: code/modules/power/powernet.dm:18
    public override async DmTask New(params object[] __extra)
    {
        // (SSmachines?.: the port's rooms without the machines subsystem, as _machinery.cs:64 registers)
        if (SSmachines != null)
            Dm.append(ref SSmachines.powernets, this);
    }

    // tg: code/modules/power/powernet.dm:21
    public override object Destroy(float? force = null)
    {
        //Go away references, you suck!
        foreach (obj_structure_cable C in Dm.each<obj_structure_cable>(cables))
        {
            Dm.remove(ref cables, C);
            C.powernet = null;
        }
        foreach (obj_machinery_power M in Dm.each<obj_machinery_power>(nodes))
        {
            Dm.remove(ref nodes, M);
            M.powernet = null;
        }

        if (SSmachines != null)
            Dm.remove(ref SSmachines.powernets, this);
        return base.Destroy(force);
    }

    // tg: code/modules/power/powernet.dm:33
    public virtual object is_empty()
    {
        return Dm.num(!Dm.truthy(cables.len) && !Dm.truthy(nodes.len));
    }

    //remove a cable from the current powernet
    //if the powernet is then empty, delete it
    //Warning : this proc DON'T check if the cable exists
    // tg: code/modules/power/powernet.dm:39
    public virtual void remove_cable(obj_structure_cable C = null)
    {
        if (Dm.truthy(C._listen_lookup?[COMSIG_CABLE_REMOVED_FROM_POWERNET]))
            C._SendSignal(COMSIG_CABLE_REMOVED_FROM_POWERNET, Dm.list(C));
        Dm.remove(ref cables, C);
        C.powernet = null;
        if (Dm.truthy(is_empty()))//the powernet is now empty...
            qdel(this);///... delete it
    }

    //add a cable to the current powernet
    //Warning : this proc DON'T check if the cable exists
    // tg: code/modules/power/powernet.dm:48
    public virtual void add_cable(obj_structure_cable C = null)
    {
        if (Dm.truthy(C.powernet))// if C already has a powernet...
        {
            if (C.powernet == this)
                return;
            else
                C.powernet.remove_cable(C); //..remove it
        }
        C.powernet = this;
        Dm.append(ref cables, C);
        if (Dm.truthy(C._listen_lookup?[COMSIG_CABLE_ADDED_TO_POWERNET]))
            C._SendSignal(COMSIG_CABLE_ADDED_TO_POWERNET, Dm.list(C));
    }

    //remove a power machine from the current powernet
    //if the powernet is then empty, delete it
    //Warning : this proc DON'T check if the machine exists
    // tg: code/modules/power/powernet.dm:61
    public virtual void remove_machine(obj_machinery_power M = null)
    {
        Dm.remove(ref nodes, M);
        M.powernet = null;
        if (Dm.truthy(is_empty()))//the powernet is now empty...
            qdel(this);///... delete it
    }


    //add a power machine to the current powernet
    //Warning : this proc DON'T check if the machine exists
    // tg: code/modules/power/powernet.dm:70
    public virtual void add_machine(obj_machinery_power M = null)
    {
        if (Dm.truthy(M.powernet))// if M already has a powernet...
        {
            if (Dm.eq(M.powernet, this))
                return;
            else
                M.disconnect_from_network();//..remove it
        }
        M.powernet = this;
        nodes[M] = M;
    }

    //handles the power changes in the powernet
    //called every ticks by the powernet controller
    // tg: code/modules/power/powernet.dm:81
    public virtual async DmTask reset()
    {
        //see if there's a surplus of power remaining in the powernet and stores unused power in the SMES
        netexcess = avail - Dm.num(load);

        if (netexcess > 100 && Dm.truthy(length(nodes))) // if there was excess power last cycle
        {
            foreach (obj_machinery_power_smes S in Dm.each<obj_machinery_power_smes>(nodes)) // find the SMESes in the network
                await S.restore(); // and restore some of the power that was used
        }

        // reset the powernet
        load = delayedload;
        delayedload = 0;
        avail = newavail;
        newavail = 0;
    }

    // tg: code/modules/power/powernet.dm:95
    public virtual object get_electrocute_damage()
    {
        return ELECTROCUTE_DAMAGE(Dm.num(energy_to_power(avail))); // Assuming 1 second of contact.
    }

    // Mostly just a wrapper for sending the COMSIG_POWERNET_CIRCUIT_TRANSMISSION signal, but could be retooled in the future to give it other uses
    // tg: code/modules/power/powernet.dm:99
    public virtual void data_transmission(DmList data = null, object encryption_key = null, datum_weakref port = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_POWERNET_CIRCUIT_TRANSMISSION]))
            this._SendSignal(COMSIG_POWERNET_CIRCUIT_TRANSMISSION, Dm.list(this, Dm.list(Dm.kv("data", data), Dm.kv("enc_key", encryption_key), Dm.kv("port", port))));
    }

    /**
     * Triggers lights connected to this powernet to flicker a few times
     *
     * * flicker_source - The center of the flicker. If null the whole powernet will flicker
     * * falloff_distance - Only relevant if you passed a source. Areas beyond this distance will be less and less likely to flicker.
     */
    // tg: code/modules/power/powernet.dm:108
    public virtual void propagate_light_flicker(atom flicker_source = null, float? falloff_distance = null)
    {
        falloff_distance ??= 32;
        if (Dm.truthy(flickering) || !Dm.truthy(length(nodes)))
            return;

        flickering = TRUE;
        foreach (obj_machinery_power_terminal terminal in Dm.each<obj_machinery_power_terminal>(nodes))
        {
            if (!(terminal.master is obj_machinery_power_apc))
                continue;

            float flicker_prob = 85;
            if (flicker_source != null)
                flicker_prob = 85 + min(3 * (Dm.num(falloff_distance) - get_dist(flicker_source, terminal)), 0);

            if (!prob(flicker_prob))
                continue;

            obj_machinery_power_apc apc = terminal.master as obj_machinery_power_apc;
            foreach (obj_machinery_light light in Dm.each_cast<obj_machinery_light>(apc.get_lights()))
            {
                light.flicker(amount: 1);
                CHECK_TICK();
            }
        }

        // don't let another flicker propagation until our slowest area is done (with some added leeway)
        _addtimer(Dm.@new<datum_callback>(this, nameof(flickering__varset), FALSE), 9 * SECONDS, file: "code/modules/power/powernet.dm", line: 130);
    }

    /// VARSET_CALLBACK(src, flickering, FALSE)'s target (see the GAP(lead) in the header): ___callbackvarset's datum branch
    /// (varset_callback.dm:8-23) for this var.
    public virtual void flickering__varset(object var_value = null)
    {
        flickering = Dm.num(var_value);
    }
}
