// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/masks/muzzle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice A and gaps CL1 (D333, the tape's moth snack): the whole file: the muzzle (the medical closets
// hold it) and the tape gags a roll of tape makes (stacks/tape.cs), whose wearer's speech is muffled. The var blocks are
// generated (D111).

// tg: code/modules/clothing/masks/muzzle.dm:1
public partial class obj_item_clothing_mask_muzzle : obj_item_clothing_mask
{
    // tg: code/modules/clothing/masks/muzzle.dm:12
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_muffles_speech)));
        return dot;
    }

    // tg: code/modules/clothing/masks/muzzle.dm:16
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        if (user.get_item_by_slot(ITEM_SLOT_MASK) == this)
        {
            to_chat(user, span_warning(I18n.T("You need help taking this off!")));
            return null;
        }
        return await base.attack_paw(user, modifiers);
    }
}

// tg: code/modules/clothing/masks/muzzle.dm:22
public partial class obj_item_clothing_mask_muzzle_tape : obj_item_clothing_mask_muzzle
{
    // tg: code/modules/clothing/masks/muzzle.dm:41
    public override object create_moth_snack()
    {
        return null;
    }

    // tg: code/modules/clothing/masks/muzzle.dm:43
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(dot, Dm.text("[]", span_notice(I18n.T("Use it on someone while not in combat mode to tape their mouth closed!"))));
        return dot;
    }

    // tg: code/modules/clothing/masks/muzzle.dm:47
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (user.get_item_by_slot(ITEM_SLOT_MASK) != this)
            return dot;
        playsound(user, Dm.resource("sound/items/duct_tape/duct_tape_rip.ogg"), 50, TRUE);
        if (Dm.truthy(harmful_strip))
        {
            await ((mob_living)user).apply_damage(stripping_damage, BRUTE, BODY_ZONE_HEAD);
            Waiting.Detach(user.emote("scream"));
            to_chat(user, span_userdanger(I18n.T("You feel a massive pain as hundreds of tiny spikes tear free from your face!")));
        }
        return dot;
    }

    // tg: code/modules/clothing/masks/muzzle.dm:58
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living victim = target_mob; // tg types them carbon; a typed var is no cast in DM, so any living mob passes
        mob_living attacker = user;
        if (Dm.truthy(attacker.combat_mode))
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (Dm.truthy(victim.is_mouth_covered(ITEM_SLOT_HEAD)))
        {
            to_chat(attacker, span_notice(I18n.T("[]'s mouth is covered.", victim)));
            return null;
        }
        if (!Dm.truthy(await mob_can_equip(victim, ITEM_SLOT_MASK)))
        {
            to_chat(attacker, span_notice(I18n.T("[] is already wearing somthing on their face.", victim)));
            return null;
        }
        balloon_alert(attacker, I18n.T("taping mouth..."));
        to_chat(victim, span_userdanger(I18n.T("[] is attempting to tape your mouth closed!", attacker)));
        if (!Dm.truthy(await do_after(attacker, equip_delay_other, target: victim)))
            return null;
        await victim.equip_to_slot_if_possible(this, ITEM_SLOT_MASK);
        update_appearance();
        return null;
    }
}
