// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/effect_system/effects_reagents_explosion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5: reagent_explode and the chemistry's explosive reactions). The var block is generated.

//Reagent-based explosion effect
// tg: code/game/objects/effects/effect_system/effects_reagents_explosion.dm:5
public partial class datum_effect_system_reagents_explosion : datum_effect_system
{
    // tg: code/game/objects/effects/effect_system/effects_reagents_explosion.dm:15
    [DmParams("location", "amount", "flash_fact", "flame_fact", "message")]
    public override async DmTask New(params object[] __extra)
    {
        object location = Dm.arg<object>(__extra, 0);
        object amount = Dm.arg<object>(__extra, 1);
        object flash_fact = Dm.arg<object>(__extra, 2);
        object flame_fact = Dm.arg<object>(__extra, 3);
        object message = __extra != null && __extra.Length > 4 ? __extra[4] : TRUE;
        await base.New(__extra);
        this.amount = amount;
        explosion_message = Dm.num(message);
        if (!isturf(location))
            location = get_turf(location as atom); // tg assigns the parameter, not src.location (the parent New already set that)
        flashing_factor = flash_fact;
        flaming_factor = flame_fact;
    }

    /// Starts the explosion. The explosion_source is as part of logging and identifying the source of the explosion for logs.
    // tg: code/game/objects/effects/effect_system/effects_reagents_explosion.dm:25
    public override async DmTask<object> start(params object[] __extra)
    {
        atom explosion_source = Dm.arg<atom>(__extra, 0);
        if (explosion_source == null)
            stack_trace("Reagent explosion triggered without a source atom. This explosion may have incomplete logging.", "code/game/objects/effects/effect_system/effects_reagents_explosion.dm", 27);
        if (Dm.truthy(explosion_message))
            location.visible_message(span_danger("The solution violently explodes!"), span_hear("You hear an explosion!"));
        await dyn_explosion(location, amount, flash_range: flashing_factor, flame_range: flaming_factor == null ? null : Dm.num(flaming_factor), explosion_cause: explosion_source);
        return null;
    }
}
