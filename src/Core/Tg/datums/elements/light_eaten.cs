// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/light_eaten.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3b (D349): the whole file: what a light eater leaves behind, a thing that can never light up again. The var block is generated (D111).

/**
 * Makes anything that it attaches to incapable of producing light
 */
// tg: code/datums/elements/light_eaten.dm:5
public partial class datum_element_light_eaten : datum_element
{
    // tg: code/datums/elements/light_eaten.dm:7
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        object dot = await base.Attach(target, __extra);
        atom atom_target = (atom)target;
        RegisterSignal(atom_target, COMSIG_ATOM_SET_LIGHT_POWER, nameof(block_light_power));
        RegisterSignal(atom_target, COMSIG_ATOM_SET_LIGHT_RANGE, nameof(block_light_range));
        RegisterSignal(atom_target, COMSIG_ATOM_SET_LIGHT_ON, nameof(block_light_on));
        RegisterSignal(atom_target, COMSIG_ATOM_EXAMINE, nameof(on_examine));

        /// Because the lighting system does not like overlay lights getting set_light() called.
        if (Dm.eq(atom_target.light_system, COMPLEX_LIGHT))
        {
            await atom_target.set_light(0, 0, null, l_on: FALSE);
        }
        else
        {
            atom_target.set_light_power(0);
            atom_target.set_light_range(0);
            await atom_target.set_light_on(FALSE);
            await atom_target.update_icon();
        }
        return dot;
    }

    // tg: code/datums/elements/light_eaten.dm:30
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, Dm.list(
            COMSIG_ATOM_SET_LIGHT_POWER,
            COMSIG_ATOM_SET_LIGHT_RANGE,
            COMSIG_ATOM_SET_LIGHT_ON,
            COMSIG_ATOM_EXAMINE));
        return base.Detach(source, __extra);
    }

    /// Prevents the light power of the target atom from exceeding 0 or increasing.
    // tg: code/datums/elements/light_eaten.dm:39
    [SignalHandler]
    public virtual object block_light_power(atom eaten_light = null, object new_power = null)
    {
        if (Dm.num(new_power) > 0)
            return COMPONENT_BLOCK_LIGHT_UPDATE;
        if (Dm.num(new_power) > eaten_light.light_power)
            return COMPONENT_BLOCK_LIGHT_UPDATE;
        return NONE;
    }

    /// Prevents the light range of the target atom from exceeding 0 while the light power is greater than 0.
    // tg: code/datums/elements/light_eaten.dm:47
    [SignalHandler]
    public virtual object block_light_range(atom eaten_light = null, object new_range = null)
    {
        if (Dm.num(new_range) <= 0)
            return NONE;
        if (eaten_light.light_power <= 0)
            return NONE;
        return COMPONENT_BLOCK_LIGHT_UPDATE;
    }

    /// Prevents the light from turning on while the light power is greater than 0.
    // tg: code/datums/elements/light_eaten.dm:55
    [SignalHandler]
    public virtual object block_light_on(atom eaten_light = null, object new_on = null)
    {
        if (Dm.truthy(new_on))
            return COMPONENT_BLOCK_LIGHT_UPDATE;
        return NONE;
    }

    /// Signal handler for light eater flavortext
    // tg: code/datums/elements/light_eaten.dm:62
    [SignalHandler]
    public virtual object on_examine(atom eaten_light = null, mob examiner = null, DmList examine_text = null)
    {
        Dm.append(ref examine_text, span_warning(I18n.T("It's dark and empty...")));
        if (isliving(examiner) && prob(20))
        {
            mob_living target = (mob_living)examiner;
            Dm.append(ref examine_text, span_danger(I18n.T("You can feel something in [] gnash at your eyes!", eaten_light.p_them())));
            Waiting.NoSleep(target.adjust_temp_blindness(10 * SECONDS));
            Waiting.NoSleep(target.set_eye_blur_if_lower(20 * SECONDS));
        }
        return NONE;
    }
}
