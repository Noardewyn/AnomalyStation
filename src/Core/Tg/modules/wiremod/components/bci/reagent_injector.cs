// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/bci/reagent_injector.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * # Reagent Injector Component
 *
 * Injects reagents into the user.
 * Requires a shell that can be inserted into a mob that reagents can be injected into.
 */
// tg: code/modules/wiremod/components/bci/reagent_injector.dm:8
public partial class obj_item_circuit_component_reagent_injector : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/bci/reagent_injector.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(15, OPENCONTAINER); //This is mostly used in the case of the shell still having reagents in it when the component is removed.
        return dot;
    }

    // tg: code/modules/wiremod/components/bci/reagent_injector.dm:25
    public override async DmTask populate_ports()
    {
        await base.populate_ports();
        inject = add_input_port("Inject", PORT_TYPE_SIGNAL, trigger: nameof(trigger_inject)) as datum_port_input;
        injected = add_output_port("Injected", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/bci/reagent_injector.dm:30
    public virtual object get_mob()
    {
        if (istype(shell_item, typeof(obj_item_organ_cyberimp_bci)))
        {
            obj_item_organ_cyberimp_bci shell_bci = shell_item as obj_item_organ_cyberimp_bci;
            return shell_bci.owner;
        }
        if (istype(shell_item, typeof(obj_item_implant)))
        {
            obj_item_implant shell_implant = shell_item as obj_item_implant;
            return shell_implant.imp_in;
        }
        return null;
    }

    // tg: code/modules/wiremod/components/bci/reagent_injector.dm:38
    [ShouldNotSleep]
    public virtual void trigger_inject()
    {
        mob_living mob_to_inject = get_mob() as mob_living;
        if (!(mob_to_inject is mob_living))
            return;
        if (mob_to_inject.reagents == null)
            return;
        if (((datum_reagents)mob_to_inject.reagents).total_volume + ((datum_reagents)shell_item.reagents).total_volume > ((datum_reagents)mob_to_inject.reagents).maximum_volume)
            return;
        object contained = ((datum_reagents)shell_item.reagents).get_reagent_log_string();
        object units = Waiting.NoSleep(((datum_reagents)shell_item.reagents).trans_to(mob_to_inject.reagents, ((datum_reagents)shell_item.reagents).total_volume, methods: INJECT));
        if (Dm.truthy(units))
        {
            injected.set_output(COMPONENT_SIGNAL);
            log_combat(mob_to_inject, mob_to_inject, "injected", shell_item, Dm.text("which had []", contained));
        }
    }

    // tg: code/modules/wiremod/components/bci/reagent_injector.dm:53
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        if (Dm.truthy(is_type_in_list(shell, required_shells as DmList)))
        {
            shell_item = shell as obj_item;
            shell_item.create_reagents(15, OPENCONTAINER);
            if (Dm.truthy(((datum_reagents)reagents).total_volume))
                await ((datum_reagents)reagents).trans_to(shell_item, ((datum_reagents)reagents).total_volume);
        }
    }

    // tg: code/modules/wiremod/components/bci/reagent_injector.dm:61
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        await base.unregister_shell(shell);
        if (shell_item?.reagents != null)
        {
            if (Dm.truthy(((datum_reagents)shell_item.reagents).total_volume))
                await ((datum_reagents)shell_item.reagents).trans_to(this, ((datum_reagents)shell_item.reagents).total_volume);
            QDEL_NULL(ref shell_item.reagents);
        }
        shell_item = null;
    }
}
