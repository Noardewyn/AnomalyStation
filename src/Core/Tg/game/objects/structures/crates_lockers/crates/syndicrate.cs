// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/crates/syndicrate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4c, D316: the united surplus crate) but the key's screentip (a live-partial row, D264). The var blocks
// are generated (D111).

// tg: code/game/objects/structures/crates_lockers/crates/syndicrate.dm:1
public partial class obj_structure_closet_crate_secure_syndicrate : obj_structure_closet_crate_secure
{
    // tg: code/game/objects/structures/crates_lockers/crates/syndicrate.dm:23
    public override async DmTask<object> before_open(mob_living user = null, object force = null)
    {
        object dot = await base.before_open(user, force);
        if (!Dm.truthy(dot))
            return FALSE;

        if (!Dm.truthy(broken) && !Dm.truthy(force) && !Dm.truthy(created_items))
        {
            balloon_alert(user, I18n.T("locked!"));
            return FALSE;
        }

        return TRUE;
    }

    /// if the crate takes damage it will explode 25% of the time
    // tg: code/game/objects/structures/crates_lockers/crates/syndicrate.dm:34
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        if (Dm.truthy(created_items))
            return await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.num(damage_amount) < DAMAGE_PRECISION)
            return await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (prob(75))
            return await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        visible_message(span_danger(I18n.T("The syndicrate's anti-tamper system activates!")));
        await explosion(this, heavy_impact_range: 1, light_impact_range: 2, flash_range: 2);
        qdel(this);
        return null;
    }

    ///ensures that the syndicrate can only be unlocked by opening it with a syndicrate_key
    // tg: code/game/objects/structures/crates_lockers/crates/syndicrate.dm:46
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_syndicrate_key || Dm.truthy(created_items))
            return await base.item_interaction(user, tool, modifiers, __extra);
        created_items = TRUE;
        foreach (object item_path in Dm.each(unlock_contents))
            Dm.@new((Type)item_path, this);
        unlock_contents = Dm.list();
        qdel(tool);
        to_chat(user, span_notice(I18n.T("You twist the key into both locks at once, opening the crate.")));
        playsound(this, Dm.resource("sound/machines/airlock/boltsup.ogg"), 50, vary: FALSE);
        await togglelock(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/syndicrate.dm:59
    public override async DmTask<object> togglelock(mob_living user = null, object silent = null)
    {
        if (Dm.truthy(broken) || !Dm.truthy(created_items))
            return null;
        if (iscarbon(user))
            add_fingerprint(user);
        locked = Dm.truthy(locked) ? FALSE : TRUE;
        user.visible_message(
            span_notice(I18n.T("[] [] [].", user, Dm.truthy(locked) ? "locks" : "unlocks", this)),
            span_notice(I18n.T("You [] [].", Dm.truthy(locked) ? "locked" : "unlocked", this)));
        update_appearance();
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/syndicrate.dm:71
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        return ITEM_INTERACT_BLOCKING;
    }
}

// tg: code/game/objects/structures/crates_lockers/crates/syndicrate.dm:74
public partial class obj_item_syndicrate_key : obj_item
{
    // tg: code/game/objects/structures/crates_lockers/crates/syndicrate.dm:81
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_item_context();
        return dot;
    }
}
