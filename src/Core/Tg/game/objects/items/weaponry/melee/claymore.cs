// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/claymore.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the claymore itself (:1-52) translated, by gaps DE1 (D333) for religion.dm's weak and ceremonial claymores: its alt
// sharpness (a stab), its butchering and its block that a projectile, a leap or an overwhelming attack passes; so the carrot, the
// shortsword and the gladius, which only set vars, are live too. The cutlass and the highlander claymore, which have procs of their
// own, stay inert and come with the melee leftovers (PORT-CHECKLIST section 8). The var block is generated (D111).

// tg: code/game/objects/items/weaponry/melee/claymore.dm:1
public partial class obj_item_claymore : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/claymore.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Waiting.NoSleep(make_stabby());
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering),
            Dm.named("speed", 4 * SECONDS),
            Dm.named("effectiveness", 105)));
        return dot;
    }

    // Applies alt sharpness component, for overrides
    // tg: code/game/objects/items/weaponry/melee/claymore.dm:42
    public virtual async DmTask make_stabby()
    {
        await AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple, -15);
    }

    // tg: code/game/objects/items/weaponry/melee/claymore.dm:45
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is falling on []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/weaponry/melee/claymore.dm:49
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (attack_type == PROJECTILE_ATTACK || attack_type == LEAP_ATTACK || attack_type == OVERWHELMING_ATTACK)
            final_block_chance = 0; //Don't bring a sword to a gunfight, and also you aren't going to really block someone full body tackling you with a sword. Or a road roller, if one happened to hit you.
        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }
}
