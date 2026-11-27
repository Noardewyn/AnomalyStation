// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/dcs_components/component_boris_circuit_container.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the mark that lets an AI click a shell holding a B.O.R.I.S. module (or anything carrying one) to connect to
// its circuit. The var block is generated.

// tg: code/modules/wiremod/dcs_components/component_boris_circuit_container.dm:1
public partial class datum_component_boris_circuit_container : datum_component
{
    // tg: code/modules/wiremod/dcs_components/component_boris_circuit_container.dm:6
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object dot = await base.Initialize(__extra);
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;
        return dot;
    }

    // tg: code/modules/wiremod/dcs_components/component_boris_circuit_container.dm:11
    public override object Destroy(float? force = null)
    {
        mmi_components = null;
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/dcs_components/component_boris_circuit_container.dm:15
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_ATOM_ATTACK_AI, nameof(on_ai_click));
        atom parent_atom = parent as atom;
        image indicator_image = image(Dm.resource("icons/mob/huds/hud.dmi"), parent_atom, "hudtracking", pixel_x: 8);
        SET_PLANE_EXPLICIT(indicator_image, ABOVE_LIGHTING_PLANE, parent_atom);
        indicator_weakref = WEAKREF(parent_atom.add_alt_appearance(
            typeof(datum_atom_hud_alternate_appearance_basic_ais),
            Dm.text("boris_circuit_container_[]", REF(this)),
            indicator_image,
            NONE
        ) as datum) as datum_weakref;
    }

    // tg: code/modules/wiremod/dcs_components/component_boris_circuit_container.dm:27
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        QDEL_NULL(ref indicator_weakref);
        await base.UnregisterFromParent(__extra);
    }

    // tg: code/modules/wiremod/dcs_components/component_boris_circuit_container.dm:31
    public override async DmTask<object> on_source_add(object source = null, params object[] __extra)
    {
        object dot = await base.on_source_add(source, __extra);
        obj_item_circuit_component_mmi source_comp = Dm.locate_ref(source) as obj_item_circuit_component_mmi;
        if (!(source_comp is obj_item_circuit_component_mmi))
            return COMPONENT_INCOMPATIBLE;
        LAZYADD(ref mmi_components, source_comp);
        return dot;
    }

    // tg: code/modules/wiremod/dcs_components/component_boris_circuit_container.dm:38
    public override void on_source_remove(object source = null)
    {
        obj_item_circuit_component_mmi source_comp = Dm.locate_ref(source) as obj_item_circuit_component_mmi;
        if (source_comp != null)
            LAZYREMOVE(ref mmi_components, source_comp);
        base.on_source_remove(source);
    }

    // tg: code/modules/wiremod/dcs_components/component_boris_circuit_container.dm:44
    [SignalHandler]
    public virtual object on_ai_click(atom_movable source = null, mob user = null)
    {
        if (!Dm.truthy(length(mmi_components)))
            return null;
        if (HAS_TRAIT(user, TRAIT_CONNECTED_TO_CIRCUIT))
            return null;
        if (length(mmi_components) == 1)
        {
            obj_item_circuit_component_mmi mmi_comp = mmi_components[1] as obj_item_circuit_component_mmi;
            if (mmi_comp.parent.shell == source)
            {
                Waiting.Detach(mmi_comp.confirm_ai_connect(user as mob_living_silicon_ai, source));
                return COMPONENT_CANCEL_ATTACK_CHAIN;
            }
        }
        Waiting.Detach(select_circuit(source, user));
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/wiremod/dcs_components/component_boris_circuit_container.dm:59
    public virtual async DmTask select_circuit(atom_movable source = null, mob user = null)
    {
        DmList choices = Dm.list();
        DmList choice_map = Dm.list();
        float source_in_choices = FALSE;
        foreach (object mmi_component__it in Dm.each(mmi_components))
        {
            obj_item_circuit_component_mmi mmi_component = (obj_item_circuit_component_mmi)mmi_component__it;
            if (mmi_component.occupant != null)
                continue;
            atom_movable shell = mmi_component.parent.shell;
            choices[shell] = shell;
            choice_map[shell] = mmi_component;
            if (shell == source)
                source_in_choices = TRUE;
        }
        if (!Dm.truthy(source_in_choices))
            choices[source] = source;
        atom_movable choice = await show_radial_menu(user, source, choices, user_space: TRUE) as atom_movable;
        if (QDELETED(choice) || QDELETED(user) || !Dm.truthy(await user.can_interact_with(choice)))
            return;
        if (choice == source && !Dm.truthy(source_in_choices))
        {
            await choice.attack_ai(user);
            return;
        }
        obj_item_circuit_component_mmi choice_comp = choice_map[choice] as obj_item_circuit_component_mmi;
        if (QDELETED(choice_comp) || choice_comp.parent?.shell != choice || choice_comp.boris == null)
            return;
        choice_comp.do_ai_connect(user as mob_living_silicon_ai, choice);
    }
}
