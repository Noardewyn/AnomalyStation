// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/pinatas.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2b, D316: the uplink's weapons grade pinata, the corgi and Donk pinatas with it). The var blocks and the
// subtypes are generated (D111).

///A pinata that has a chance to drop candy items when struck with a melee weapon that deals at least 10 damage
// tg: code/game/objects/structures/pinatas.dm:2
public partial class obj_structure_pinata : obj_structure
{
    // tg: code/game/objects/structures/pinatas.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_pinata), Dm.named("candy", candy_options), Dm.named("death_drop", destruction_loot)));
        return dot;
    }

    // tg: code/game/objects/structures/pinatas.dm:27
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.num(get_integrity()) < Dm.num(max_integrity) / 2)
            icon_state = Dm.text("[]_damaged", base_icon_state);
        if (Dm.num(damage_amount) >= 10) // Swing means minimum damage threshold for dropping candy is met.
            flick(Dm.text("[]_swing", icon_state), this);
        return dot;
    }

    // tg: code/game/objects/structures/pinatas.dm:34
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        var sw__1 = damage_type;
        if (Dm.eq(sw__1, BRUTE))
        {
            if (Dm.truthy(damage_amount))
                playsound(this, Dm.resource("sound/items/weapons/slash.ogg"), 50, TRUE);
            else
                playsound(this, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
        }
        else if (Dm.eq(sw__1, BURN))
            playsound(this, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        return null;
    }

    // tg: code/game/objects/structures/pinatas.dm:44
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new(debris, get_turf(this));
    }
}

///An item that when used inhand spawns an immovable pinata
// tg: code/game/objects/structures/pinatas.dm:48
public partial class obj_item_pinata : obj_item
{
    // tg: code/game/objects/structures/pinatas.dm:56
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        turf player_turf = get_turf(user);
        if (player_turf != null && Dm.truthy(await player_turf.is_blocked_turf(TRUE)))
            return FALSE;
        balloon_alert_to_viewers("setting up pinata...");
        if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: get_turf(user))))
            balloon_alert(user, "cancelled!");
        Dm.@new(pinata_type, get_turf(user));
        balloon_alert(user, "pinata setup");
        qdel(this);
        return null;
    }
}
