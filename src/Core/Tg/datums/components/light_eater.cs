// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/light_eater.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3b (D349): the whole file: the light eater as a component, which also remembers the lights it ate. The var block is generated (D111).

/**
 * Makes anything it attaches to capable of removing something's ability to produce light until it is destroyed
 *
 * The permanent version of this is [/datum/element/light_eater]
 */
// tg: code/datums/components/light_eater.dm:7
public partial class datum_component_light_eater : datum_component
{
    // tg: code/datums/components/light_eater.dm:12
    [DmParams("_eaten")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList _eaten = Dm.arg(__extra, 0) as DmList;
        if (!isatom(parent) && !istype(parent, typeof(datum_reagent)))
            return COMPONENT_INCOMPATIBLE;

        object dot = await base.Initialize(__extra);
        if (length(_eaten) == 0)
            return dot;

        LAZYINITLIST(ref eaten_lights);
        DmList cached_eaten_lights = eaten_lights;
        foreach (object morsel in Dm.each(_eaten))
        {
            LAZYSET(ref cached_eaten_lights, morsel, TRUE);
            RegisterSignal((datum)morsel, COMSIG_QDELETING, nameof(deref_eaten_light));
        }
        return dot;
    }

    // tg: code/datums/components/light_eater.dm:26
    public override object Destroy(float? force = null)
    {
        foreach (object light in Dm.each(eaten_lights))
        {
            atom eaten_light = (atom)light;
            eaten_light.RemoveElement(typeof(datum_element_light_eaten));
            UnregisterSignal(eaten_light, COMSIG_QDELETING);
        }
        eaten_lights = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/light_eater.dm:34
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_LIGHT_EATER_DEVOUR, nameof(on_devour));
        await parent.AddElement(typeof(datum_element_light_eater));
    }

    // tg: code/datums/components/light_eater.dm:39
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        parent.RemoveElement(typeof(datum_element_light_eater));
        UnregisterSignal(parent, COMSIG_LIGHT_EATER_DEVOUR);
    }

    // tg: code/datums/components/light_eater.dm:44
    [DmParams("C", "i_am_original", "_eaten")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        DmList _eaten = Dm.arg(__extra, 0) as DmList;
        await base.InheritComponent(C, i_am_original, __extra);
        if (length(_eaten) == 0)
            return;

        LAZYINITLIST(ref eaten_lights);
        DmList cached_eaten_lights = eaten_lights;
        foreach (object morsel in Dm.each(_eaten))
        {
            RegisterSignal((datum)morsel, COMSIG_QDELETING, nameof(deref_eaten_light));
            LAZYSET(ref cached_eaten_lights, morsel, TRUE);
        }
    }

    /// Handles storing references to lights eaten by the light eater.
    // tg: code/datums/components/light_eater.dm:56
    [SignalHandler]
    public virtual object on_devour(datum source = null, atom morsel = null)
    {
        LAZYSET(ref eaten_lights, morsel, TRUE);
        RegisterSignal((datum)morsel, COMSIG_QDELETING, nameof(deref_eaten_light));
        return NONE;
    }

    /// Handles dereferencing deleted lights.
    // tg: code/datums/components/light_eater.dm:63
    [SignalHandler]
    public virtual object deref_eaten_light(atom eaten_light = null, object force = null)
    {
        UnregisterSignal(eaten_light, COMSIG_QDELETING);
        LAZYREMOVE(ref eaten_lights, eaten_light);
        return NONE;
    }
}
