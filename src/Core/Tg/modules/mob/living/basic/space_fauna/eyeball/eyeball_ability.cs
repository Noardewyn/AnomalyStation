// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/eyeball/eyeball_ability.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/eyeball/eyeball_ability.dm:1
public partial class datum_action_cooldown_spell_pointed_death_glare : datum_action_cooldown_spell_pointed
{
    // tg: code/modules/mob/living/basic/space_fauna/eyeball/eyeball_ability.dm:8
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        if (cast_on is not mob_living living_target)
        {
            to_chat(owner, span_warning("Only living things are affected by our glare!"));
            return FALSE;
        }
        if (Dm.truthy(living_target.has_movespeed_modifier(typeof(datum_movespeed_modifier_glare_slowdown))))
        {
            to_chat(owner, span_warning("This target is already affected by a glare!"));
            return FALSE;
        }
        if (!Dm.truthy(can_see(living_target, owner, 9)))
        {
            to_chat(owner, span_warning("This target cannot see our glare!"));
            return FALSE;
        }
        if (!Dm.eq(get_dir(living_target, owner), living_target.dir))
        {
            to_chat(owner, span_warning("This target is facing away from us!"));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/mob/living/basic/space_fauna/eyeball/eyeball_ability.dm:27
    public override async DmTask<object> cast(atom cast_on = null)
    {
        await base.cast(cast_on);
        cast_on.add_filter("glare", 2, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", glare_outline), Dm.kv("size", 1f)));
        await (cast_on as mob_living).add_movespeed_modifier(typeof(datum_movespeed_modifier_glare_slowdown));
        to_chat(cast_on, span_warning("You feel something watching you..."));
        _addtimer(Dm.@new<datum_callback>(this, nameof(remove_effect), cast_on), 5 * SECONDS, file: "code/modules/mob/living/basic/space_fauna/eyeball/eyeball_ability.dm", line: 32);
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/space_fauna/eyeball/eyeball_ability.dm:35
    public virtual void remove_effect(mob_living cast_on = null)
    {
        cast_on.remove_movespeed_modifier(typeof(datum_movespeed_modifier_glare_slowdown));
        cast_on.remove_filter("glare");
    }
}
