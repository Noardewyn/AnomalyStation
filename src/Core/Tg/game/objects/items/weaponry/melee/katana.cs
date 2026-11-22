// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/katana.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps WP1 (D333): the file whole. The katana: 40 force, a stab as its alternative edge (15 less force), blocks half the time but
// for an overwhelming blow, unsheathed with tg's sound when picked up; its suicide is seppuku. The cursed kind (a wizard event's) only
// changes a var. The var blocks are generated (D111).

// tg: code/game/objects/items/weaponry/melee/katana.dm:1
public partial class obj_item_katana : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/katana.dm:27
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Waiting.NoSleep(AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple, -15));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/katana.dm:33
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (attack_type == OVERWHELMING_ATTACK)
            final_block_chance = 0; //Not a high freuqnecy blade, sorry pal
        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }

    // tg: code/game/objects/items/weaponry/melee/katana.dm:42
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is slitting [] stomach open with []! It looks like [] trying to commit seppuku!", user, user.p_their(), this, user.p_theyre())));
        await DmTask.CompletedTask;
        return BRUTELOSS;
    }
}
