// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/singularity/dark_matter_singularity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// This type of singularity cannot grow as big, but it constantly hunts down living targets.
// tg: code/modules/power/singularity/dark_matter_singularity.dm:6
public partial class obj_singularity_dark_matter : obj_singularity
{
    // bonus probability to chase the target granted by eating a supermatter
    // tg: code/modules/power/singularity/dark_matter_singularity.dm:2
    private const int DARK_MATTER_SUPERMATTER_CHANCE_BONUS = 10;

    // tg: code/modules/power/singularity/dark_matter_singularity.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        initial_explosion_immunity = Dm.num(world.time) + 5 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline (rules 11.1 (a))
        datum_component_singularity resolved_singularity = singularity_component.resolve() as datum_component_singularity;
        resolved_singularity.chance_to_move_to_target = 100;
        _addtimer(Dm.@new<datum_callback>(this, nameof(normalize_tracking)), 20 * SECONDS, file: "code/modules/power/singularity/dark_matter_singularity.dm", line: 26);
        return dot;
    }

    // tg: code/modules/power/singularity/dark_matter_singularity.dm:28
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!(initial_explosion_immunity <= Dm.num(world.time))) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            Dm.append(ref dot, span_warning(I18n.T("Protected by dark matter, [] seems to be immune to explosions for [].", this, DisplayTimeText(initial_explosion_immunity - Dm.num(world.time)))));
        if (Dm.truthy(consumed_supermatter))
            Dm.append(ref dot, span_userdanger(I18n.T("IT HUNGERS")));
        else
        {
            Dm.append(ref dot, span_warning(I18n.T("<i>\"The most disturbing aspect of the singularity is its apparent attraction to living organisms. It seems to sense their presence and move towards them at a surprisingly fast speed. We have observed it consume several specimens of flora and fauna that we have collected from this sector. The singularity does not seem to care for other inanimate objects or machines, but will consume them all the same. We have tried to communicate with it using various methods, but received no response.\"</i><br>- Research Director Huey Knorr")));
        }
        return dot;
    }

    // tg: code/modules/power/singularity/dark_matter_singularity.dm:44
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (!(initial_explosion_immunity <= Dm.num(world.time))) // COOLDOWN_FINISHED, inline (rules 11.1 (a))
            return FALSE;
        return base.ex_act(severity, target, __extra);
    }

    // tg: code/modules/power/singularity/dark_matter_singularity.dm:49
    public override async DmTask supermatter_upgrade()
    {
        datum_component_singularity resolved_singularity = singularity_component.resolve() as datum_component_singularity;
        resolved_singularity.chance_to_move_to_target += DARK_MATTER_SUPERMATTER_CHANCE_BONUS;
        name = "Dark Lord Singuloth";
        desc = "You managed to make a singularity from dark matter, which makes no sense at all, and then you threw a supermatter into it? Are you fucking insane? Fuck it, praise Lord Singuloth.";
        consumed_supermatter = TRUE;
    }

    ///For 20 seconds, the singularity has buffed tracking to ensure it actually makes its way to the station, normalizes after 20 seconds
    // tg: code/modules/power/singularity/dark_matter_singularity.dm:57
    public virtual void normalize_tracking()
    {
        datum_component_singularity resolved_singularity = singularity_component.resolve() as datum_component_singularity;
        float initial_chance = Dm.initial(resolved_singularity, (datum_component_singularity p) => p.chance_to_move_to_target);
        resolved_singularity.chance_to_move_to_target = Dm.truthy(consumed_supermatter) ? initial_chance + DARK_MATTER_SUPERMATTER_CHANCE_BONUS : initial_chance;
    }
}
