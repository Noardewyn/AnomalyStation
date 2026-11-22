// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/sabre.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps WP1 (D333): the file whole. The captain's sabre: a jousting charge, a quick butcher, blocks half the time (not shots,
// leaps or overwhelming blows), drawn and put away with tg's sheath sounds, and the weight of authority: a third more damage on a
// body whose liver has the maintenance metabolism (an assistant's), which screams; its suicide cuts off each limb in turn and dies
// when the last one falls. The parsnip sabre jousts and blocks the same way. The cuffable-item element (binding it to a wrist with
// handcuffs) waits with its status effect (not-translated.md section 46): Dm.Deferred here as in the other cuffable items. The
// var blocks are generated (D111).

// tg: code/game/objects/items/weaponry/melee/sabre.dm:1
public partial class obj_item_melee_sabre : obj_item_melee
{
    // tg: code/game/objects/items/weaponry/melee/sabre.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/objects/items/weaponry/melee/sabre.dm:28", "Initialize: AddElement(/datum/element/cuffable_item): cuffing the sabre to the wrist (the element and its status effect, not-translated.md section 46)"); //closed sword guard
        Waiting.NoSleep(AddComponent(typeof(datum_component_jousting)));
        //fast and effective, but as a sword, it might damage the results.
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering),
            Dm.named("speed", 3 * SECONDS),
            Dm.named("effectiveness", 95),
            Dm.named("bonus_modifier", 5)));
        // The weight of authority comes down on the tider's crimes.
        Waiting.NoSleep(AddComponent(typeof(datum_component_bane),
            Dm.named("damage_multiplier", 1.35f),
            Dm.named("should_bane_callback", Dm.@new<datum_callback>(this, nameof(bane_check))),
            Dm.named("on_bane_callback", Dm.@new<datum_callback>(this, nameof(bane_message))),
            Dm.named("label_text", "assistants")));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/sabre.dm:44
    public virtual object bane_check(mob_living target = null)
    {
        obj_item_organ_liver liver = target.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        return liver != null && HAS_TRAIT(liver, TRAIT_MAINTENANCE_METABOLISM);
    }

    // tg: code/game/objects/items/weaponry/melee/sabre.dm:48
    public virtual void bane_message(mob_living target = null, mob_living attacker = null)
    {
        target.visible_message(
            span_warning(I18n.T("[] tears through [] with unnatural ease!", this, target)),
            span_boldwarning(I18n.T("As [] tears into your body, you feel the weight of authority collapse into your wounds!", this)));
        Waiting.Detach(target.emote("scream"));
    }

    // tg: code/game/objects/items/weaponry/melee/sabre.dm:55
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

    // tg: code/game/objects/items/weaponry/melee/sabre.dm:60
    public override void on_exit_storage(datum_storage master_storage = null)
    {
        playsound(master_storage.parent, Dm.resource("sound/items/unsheath.ogg"), 25, TRUE);
    }

    // tg: code/game/objects/items/weaponry/melee/sabre.dm:63
    public override void on_enter_storage(datum_storage master_storage = null)
    {
        playsound(master_storage.parent, Dm.resource("sound/items/sheath.ogg"), 25, TRUE);
    }

    // tg: code/game/objects/items/weaponry/melee/sabre.dm:66
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is trying to cut off all [] limbs with []! it looks like [] trying to commit suicide!", user, user.p_their(), this, user.p_theyre())));
        float i = 0;
        ADD_TRAIT(this, TRAIT_NODROP, SABRE_SUICIDE_TRAIT);
        if (user is mob_living_carbon Cuser)
        {
            obj_item_bodypart holding_bodypart = Cuser.get_holding_bodypart_of_item(this) as obj_item_bodypart;
            DmList limbs_to_dismember;
            DmList arms = Dm.list();
            DmList legs = Dm.list();
            foreach (obj_item_bodypart bodypart in Dm.each<obj_item_bodypart>(Cuser.get_bodyparts()))
            {
                if (bodypart == holding_bodypart)
                    continue;
                if (Dm.truthy(Dm.bitand(bodypart.body_part, ARMS)))
                    Dm.append(arms, bodypart);
                else if (Dm.truthy(Dm.bitand(bodypart.body_part, LEGS)))
                    Dm.append(legs, bodypart);
            }
            limbs_to_dismember = Dm.add(arms, legs) as DmList;
            if (holding_bodypart != null)
                Dm.append(limbs_to_dismember, holding_bodypart);
            float speedbase = Math.Abs((4 * SECONDS) / limbs_to_dismember.len);
            foreach (obj_item_bodypart bodypart in Dm.each<obj_item_bodypart>(limbs_to_dismember))
            {
                i++;
                _addtimer(Dm.@new<datum_callback>(this, nameof(suicide_dismember), user, bodypart), speedbase * i, file: "code/game/objects/items/weaponry/melee/sabre.dm", line: 94);
            }
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(manual_suicide), user), (5 * SECONDS) * i, file: "code/game/objects/items/weaponry/melee/sabre.dm", line: 95);
        await DmTask.CompletedTask;
        return MANUAL_SUICIDE;
    }

    // tg: code/game/objects/items/weaponry/melee/sabre.dm:97
    public virtual async DmTask suicide_dismember(mob_living user = null, obj_item_bodypart affecting = null)
    {
        if (!QDELETED(affecting) && !Dm.truthy(Dm.bitand(affecting.bodypart_flags, BODYPART_UNREMOVABLE)) && affecting.owner == user && !QDELETED(user))
        {
            playsound(user, hitsound, 25, TRUE);
            await affecting.dismember(BRUTE);
            await user.adjust_brute_loss(force * 1.25f);
        }
    }

    // tg: code/game/objects/items/weaponry/melee/sabre.dm:103
    public virtual async DmTask manual_suicide(mob_living user = null, object originally_nodropped = null)
    {
        if (!QDELETED(user))
        {
            await user.adjust_brute_loss(200);
            await user.death(FALSE);
        }
        REMOVE_TRAIT(this, TRAIT_NODROP, SABRE_SUICIDE_TRAIT);
    }
}

// tg: code/game/objects/items/weaponry/melee/sabre.dm:110
public partial class obj_item_melee_parsnip_sabre : obj_item_melee
{
    // tg: code/game/objects/items/weaponry/melee/sabre.dm:134
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/objects/items/weaponry/melee/sabre.dm:136", "Initialize: AddElement(/datum/element/cuffable_item): cuffing the parsnip sabre to the wrist (the element and its status effect, not-translated.md section 46)"); //closed sword guard
        Waiting.NoSleep(AddComponent(typeof(datum_component_jousting)));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/sabre.dm:139
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

    // tg: code/game/objects/items/weaponry/melee/sabre.dm:144
    public override void on_exit_storage(datum_storage master_storage = null)
    {
        base.on_exit_storage(master_storage);
        playsound(master_storage.parent, Dm.resource("sound/items/unsheath.ogg"), 25, TRUE);
    }

    // tg: code/game/objects/items/weaponry/melee/sabre.dm:148
    public override void on_enter_storage(datum_storage master_storage = null)
    {
        base.on_enter_storage(master_storage);
        playsound(master_storage.parent, Dm.resource("sound/items/sheath.ogg"), 25, TRUE);
    }
}
