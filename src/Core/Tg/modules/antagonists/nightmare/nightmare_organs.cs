// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/nightmare/nightmare_organs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3b (D349): the whole file but the terrorize spell the brain grants to a nightmare ANTAGONIST (the antagonist datum and its spell
// are not translated, D333; live-partial.txt): the nightmare's brain (Shadow Walk, dodging bullets in the dark), the status effect and the
// heart of darkness with its armblade and its revival in the dark. The var blocks are generated (D111).

public static partial class GlobalProcs
{
    /// How many life ticks are required for the nightmare's heart to revive the nightmare.
    public const float HEART_RESPAWN_THRESHHOLD = 80 * SECONDS;
    /// A special flag value used to make a nightmare heart not grant a light eater. Appears to be unused.
    public const int HEART_SPECIAL_SHADOWIFY = 2;
}

// tg: code/modules/antagonists/nightmare/nightmare_organs.dm:7
public partial class obj_item_organ_brain_shadow_nightmare : obj_item_organ_brain_shadow
{
    // tg: code/modules/antagonists/nightmare/nightmare_organs.dm:19
    public override async DmTask<object> on_mob_insert(mob_living_carbon brain_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(brain_owner, special, movement_flags);

        if (!Dm.eq(((datum_species)brain_owner.dna.species).id, SPECIES_NIGHTMARE))
        {
            brain_owner.set_species(typeof(datum_species_shadow_nightmare), null, null, FALSE);
            visible_message(span_warning(I18n.T("[] thrashes as [] takes root in [] body!", brain_owner, this, brain_owner.p_their())));
        }

        our_jaunt = Dm.@new<datum_action_cooldown_spell_jaunt_shadow_walk>(brain_owner) as datum_action_cooldown_spell_jaunt_shadow_walk;
        await our_jaunt.Grant(brain_owner);

        // The terrorize spell of an antagonist nightmare (datum_antagonist_nightmare) is the antagonist side and stays out (D333).
        return dot;
    }

    // tg: code/modules/antagonists/nightmare/nightmare_organs.dm:33
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        QDEL_NULL(ref our_jaunt);
        QDEL_NULL(ref terrorize_spell);
    }

    // tg: code/modules/antagonists/nightmare/nightmare_organs.dm:38
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);

        turf owner_turf = owner.loc as turf;
        if (owner_turf == null)
            return;
        if (Dm.truthy(owner_turf.check_lumcount_below(SHADOW_SPECIES_LIGHT_THRESHOLD))) //dodge in the dark
            await owner.apply_status_effect(typeof(datum_status_effect_shadow_nightmare));
    }
}

// tg: code/modules/antagonists/nightmare/nightmare_organs.dm:48
public partial class datum_status_effect_shadow_nightmare : datum_status_effect_shadow
{
    // tg: code/modules/antagonists/nightmare/nightmare_organs.dm:51
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return FALSE;
        RegisterSignal(owner, COMSIG_ATOM_PRE_BULLET_ACT, nameof(dodge_bullets));
        return TRUE;
    }

    // tg: code/modules/antagonists/nightmare/nightmare_organs.dm:58
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_ATOM_PRE_BULLET_ACT);
        return await base.on_remove();
    }

    // tg: code/modules/antagonists/nightmare/nightmare_organs.dm:62
    [SignalHandler]
    public virtual object dodge_bullets(mob_living_carbon_human source = null, obj_projectile hitting_projectile = null, object def_zone = null)
    {
        source.visible_message(
            span_danger(I18n.T("[] dances in the shadows, evading []!", source, hitting_projectile)),
            span_danger(I18n.T("You evade [] with the cover of darkness!", hitting_projectile)));
        playsound(source, SFX_BULLET_MISS, 75, TRUE);
        return COMPONENT_BULLET_PIERCED;
    }
}

// tg: code/modules/antagonists/nightmare/nightmare_organs.dm:77
public partial class obj_item_organ_heart_nightmare : obj_item_organ_heart
{
    /// The armblade granted to the host of this heart. The generated var `blade` is a /type (the types tool read the typepath of tg's `var/obj/item/light_eater/blade`), so the item is `blade_item` here.
    public obj_item_light_eater blade_item;

    // tg: code/modules/antagonists/nightmare/nightmare_organs.dm:95
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref blade_item);
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/nightmare/nightmare_organs.dm:99
    public override async DmTask<object> attack__proc(mob_living M = null, mob_living user_living = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living_carbon user = user_living as mob_living_carbon;
        if (M != user)
            return await base.attack__proc(M, user_living, modifiers, attack_modifiers, __extra);
        user.visible_message(
            span_warning(I18n.T("[] raises [] to [] mouth and tears into it with [] teeth!", user, this, user.p_their(), user.p_their())),
            span_danger(I18n.T("[] feels unnaturally cold in your hands. You raise [] to your mouth and devour it!", this, this)));
        playsound(user, Dm.resource("sound/effects/magic/demon_consume.ogg"), 50, TRUE);

        user.visible_message(
            span_warning(I18n.T("Blood erupts from []'s arm as it reforms into a weapon!", user)),
            span_userdanger(I18n.T("Icy blood pumps through your veins as your arm reforms itself!")));
        await user.temporarilyRemoveItemFromInventory(this, TRUE);
        await Insert(user);
        return null;
    }

    // tg: code/modules/antagonists/nightmare/nightmare_organs.dm:115
    public override async DmTask<object> on_mob_insert(mob_living_carbon heart_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(heart_owner, special, movement_flags);
        if (Dm.eq(special, HEART_SPECIAL_SHADOWIFY))
            return dot;
        blade_item = Dm.@new<obj_item_light_eater>() as obj_item_light_eater;
        await heart_owner.put_in_hands(blade_item);
        RegisterSignal(blade_item, COMSIG_QDELETING, nameof(on_blade_deleted));
        return dot;
    }

    // tg: code/modules/antagonists/nightmare/nightmare_organs.dm:123
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon heart_owner = organ_owner as mob_living_carbon;
        await base.on_mob_remove(organ_owner, special, movement_flags);
        respawn_progress = 0;
        if (blade_item != null && !Dm.eq(special, HEART_SPECIAL_SHADOWIFY))
        {
            heart_owner.visible_message(span_warning(I18n.T("\\The [] disintegrates!", blade_item)));
            QDEL_NULL(ref blade_item);
        }
    }

    // tg: code/modules/antagonists/nightmare/nightmare_organs.dm:130
    public override object Stop()
    {
        return FALSE;
    }

    // Happens if the blade_item was deleted before we were during mob destruction
    // tg: code/modules/antagonists/nightmare/nightmare_organs.dm:134
    [SignalHandler]
    public virtual void on_blade_deleted(datum source = null)
    {
        blade_item = null;
    }

    // tg: code/modules/antagonists/nightmare/nightmare_organs.dm:138
    public override async DmTask on_death(object seconds_per_tick = null)
    {
        if (owner == null)
            return;

        turf owner_turf = get_turf(owner);
        if (owner_turf != null)
        {
            if (Dm.truthy(owner_turf.check_lumcount_below(SHADOW_SPECIES_LIGHT_THRESHOLD)))
            {
                respawn_progress += Dm.num(seconds_per_tick) * SECONDS;
                playsound(owner, Dm.resource("sound/effects/singlebeat.ogg"), 40, TRUE);
            }
        }

        if (respawn_progress < HEART_RESPAWN_THRESHHOLD)
            return;

        await owner.revive(HEAL_ALL & ~HEAL_REFRESH_ORGANS);
        string species_id = Dm.str(((datum_species)owner.dna.species).id);
        if (!(species_id == SPECIES_SHADOW || species_id == SPECIES_NIGHTMARE))
        {
            mob_living_carbon old_owner = owner;
            await Remove(owner, HEART_SPECIAL_SHADOWIFY);
            old_owner.set_species(typeof(datum_species_shadow));
            await Insert(old_owner, HEART_SPECIAL_SHADOWIFY);
            to_chat(owner, span_userdanger(I18n.T("You feel the shadows invade your skin, leaping into the center of your chest! You're alive!")));
            Dm.output_to(owner, sound(Dm.resource("sound/effects/ghost.ogg"))); // SEND_SOUND
        }
        owner.visible_message(span_warning(I18n.T("[] staggers to [] feet!", owner, owner.p_their())));
        playsound(owner, Dm.resource("sound/effects/hallucinations/far_noise.ogg"), 50, TRUE);
        respawn_progress = 0;
    }

    // tg: code/modules/antagonists/nightmare/nightmare_organs.dm:163
    public override object get_availability(datum_species owner_species = null, mob_living owner_mob = null)
    {
        if (HAS_TRAIT(owner_mob, TRAIT_NIGHTMARISH))
            return TRUE;
        return base.get_availability(owner_species, owner_mob);
    }
}
