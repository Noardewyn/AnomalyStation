// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/vampire.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3b (D349): the whole file: the vampire species (blood loss outside a coffin, healing inside it, the Chapel's fire, the blood
// HUD), its fangs with the Drain Victim action and the bloodbag stab, and its heart. The vampires' holiday-only clan events are the
// antagonist side of D333 and are not translated. The var blocks are generated (D111).

// tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:8
public partial class datum_species_human_vampire : datum_species_human
{
    ///how many vampires exist in each house
    public const int VAMPIRES_PER_HOUSE = 5;
    ///maximum a vampire will drain, they will drain less if they hit their cap
    public const int VAMP_DRAIN_AMOUNT = 50;

    // tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:28
    public override object check_roundstart_eligible()
    {
        if (Dm.truthy(check_holidays(HALLOWEEN)))
            return TRUE;
        return base.check_roundstart_eligible();
    }

    // tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:33
    public override async DmTask on_species_gain(mob_living_carbon_human new_vampire = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        await base.on_species_gain(new_vampire, old_species, pref_load, regenerate_icons, replace_missing);
        to_chat(new_vampire, Dm.text("[]", info_text));
        new_vampire.skin_tone = "albino";
        RegisterSignal(new_vampire, COMSIG_MOB_HUD_CREATED, nameof(on_hud_created));
        RegisterSignal(new_vampire, COMSIG_LIVING_LIFE, nameof(on_life));
        if (new_vampire.hud_used != null)
            on_hud_created(new_vampire);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:42
    public override async DmTask on_species_loss(mob_living_carbon_human old_vampire = null, datum_species new_species = null, object pref_load = null, params object[] __extra)
    {
        await base.on_species_loss(old_vampire, new_species, pref_load, __extra);
        UnregisterSignal(old_vampire, Dm.list(
            COMSIG_MOB_HUD_CREATED,
            COMSIG_LIVING_LIFE));
        if (old_vampire.hud_used != null)
            await old_vampire.hud_used.remove_screen_object(HUD_MOB_BLOOD_LEVEL);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:50
    [SignalHandler]
    public virtual void on_life(mob_living_carbon_human vampire = null, object seconds_per_tick = null, object times_fired = null)
    {
        float spt = Dm.num(seconds_per_tick);
        if (istype(vampire.loc, typeof(obj_structure_closet_crate_coffin)))
        {
            float need_mob_update = 0;
            need_mob_update += Dm.num(Waiting.NoSleep(vampire.heal_overall_damage(brute: 2 * spt, burn: 2 * spt, updating_health: FALSE, required_bodytype: BODYTYPE_ORGANIC)));
            need_mob_update += Dm.num(Waiting.NoSleep(vampire.adjust_tox_loss(-2 * spt, updating_health: FALSE)));
            need_mob_update += Dm.num(Waiting.NoSleep(vampire.adjust_oxy_loss(-2 * spt, updating_health: FALSE)));
            if (need_mob_update != 0)
                Waiting.NoSleep(vampire.updatehealth());
            return;
        }
        vampire.adjust_blood_volume(-0.125f * spt);
        if (Dm.num(vampire.get_blood_volume(apply_modifiers: TRUE)) <= BLOOD_VOLUME_SURVIVE)
        {
            to_chat(vampire, span_danger(I18n.T("You ran out of blood!")));
            vampire.investigate_log("has been dusted by a lack of blood (vampire).", INVESTIGATE_DEATHS);
            Waiting.NoSleep(vampire.dust());
        }
        area A = get_area(vampire) as area;
        if (istype(A, typeof(area_station_service_chapel)))
        {
            to_chat(vampire, span_warning(I18n.T("You don't belong here!")));
            Waiting.NoSleep(vampire.adjust_fire_loss(10 * spt));
            Waiting.NoSleep(vampire.adjust_fire_stacks(3 * spt));
            vampire.ignite_mob();
        }
    }

    ///Gives the blood HUD to the vampire so they always know how much blood they have.
    // tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:73
    [SignalHandler]
    public virtual void on_hud_created(mob source = null)
    {
        Waiting.NoSleep(source.hud_used.add_screen_object(typeof(atom_movable_screen_blood_level), HUD_MOB_BLOOD_LEVEL, HUD_GROUP_INFO, update_screen: TRUE));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:77
    public override object get_physical_attributes()
    {
        return I18n.T("Vampires are afflicted with the Thirst, needing to sate it by draining the blood out of another living creature. However, they do not need to breathe or eat normally. They will instantly turn into dust if they run out of blood or enter a holy area. However, coffins stabilize and heal them, and they can transform into bats!");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:81
    public override object get_species_description()
    {
        return I18n.T("A classy Vampire! They descend upon Space Station Thirteen Every year to spook the crew! \"Bleeg!!\"");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:84
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("Vampires are unholy beings blessed and cursed with The Thirst. The Thirst requires them to feast on blood to stay alive, and in return it gives them many bonuses. Because of this, Vampires have split into two clans, one that embraces their powers as a blessing and one that rejects it."));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:91
    public override object create_pref_unique_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "bed"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Coffin Brooding")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Vampires can delay The Thirst and heal by resting in a coffin. So THAT'S why they do that!"))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
                Dm.kv(SPECIES_PERK_ICON, "book-dead"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Vampire Clans")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Vampires belong to one of two clans - the Inoculated, and the Outcast. The Outcast don't follow many vampiric traditions, while the Inoculated are given unique names and flavor."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "cross"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Against God and Nature")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Almost all higher powers are disgusted by the existence of Vampires, and entering the Chapel is essentially suicide. Do not do it!")))));

        return to_add;
    }

    // Vampire blood is special, so it needs to be handled with its own entry.
    // tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:120
    public override object create_pref_blood_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, "tint"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("The Thirst")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("In place of eating, Vampires suffer from The Thirst. Thirst of what? Blood! Their fangs allows them to grab people and drink their blood, and they will die if they run out. As a note, it doesn't matter whose blood you drink, it will all be converted into your blood type when consumed.")))));

        return to_add;
    }

    // There isn't a "Minor Undead" biotype, so we have to explain it in an override (see: dullahans)
    // tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:137
    public override object create_pref_biotypes_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, "skull"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Minor Undead")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("[] are minor undead. Minor undead enjoy some of the perks of being dead, like not needing to breathe or eat, but do not get many of the environmental immunities involved with being fully undead.", name)))));

        return to_add;
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:153
public partial class obj_item_organ_fangs_vampire : obj_item_organ_fangs
{
    // tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:166
    public override async DmTask<object> on_mob_insert(mob_living_carbon receiver = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(receiver, special, movement_flags);
        RegisterSignal(receiver, COMSIG_ATOM_ITEM_INTERACTION, nameof(stab_bloodbag));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:170
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        UnregisterSignal(organ_owner as datum, COMSIG_ATOM_ITEM_INTERACTION);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:174
    [SignalHandler]
    public virtual object stab_bloodbag(mob_living source = null, mob_living user = null, obj_item used_item = null, DmList modifiers = null)
    {
        if (user != source)
            return NONE;
        if (!istype(used_item, typeof(obj_item_reagent_containers_blood)))
            return NONE;
        if (Dm.num((used_item.reagents as datum_reagents)?.total_volume) <= 0)
        {
            to_chat(user, span_warning(I18n.T("[] is empty!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(
            span_notice(I18n.T("[] stabs [] with [] sharp teeth and drains its contents!", user, used_item, user.p_their())),
            span_notice(I18n.T("You stab [] with your sharp teeth and drain its contents!", used_item)),
            span_hear(I18n.T("You hear a stabbing sound! ... Followed by slurping?")),
            COMBAT_MESSAGE_RANGE);
        Waiting.Detach(async_stab_bloodbag(user as mob_living_carbon, used_item as obj_item_reagent_containers_blood)); // INVOKE_ASYNC
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:194
    public virtual async DmTask async_stab_bloodbag(mob_living_carbon user = null, obj_item_reagent_containers_blood bloodbag = null, object time = null)
    {
        time ??= 0.5f * SECONDS;
        if (!Dm.truthy(await do_after(user, time, bloodbag)))
            return;

        to_chat(user, span_notice(I18n.T("You swallow a gulp of [].", this)));
        playsound(bloodbag, "sound/items/drink.ogg", 50, TRUE); //slurp
        await ((datum_reagents)bloodbag.reagents).trans_to(user, Dm.num(((datum_reagents)bloodbag.reagents).maximum_volume) * 0.05f, transferred_by: user, methods: INGEST);
        if (Dm.num(((datum_reagents)bloodbag.reagents).total_volume) > 0)
            await async_stab_bloodbag(user, bloodbag, 1 * SECONDS);
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:205
public partial class datum_action_item_action_organ_action_vampire : datum_action_item_action_organ_action
{
    // tg: code/modules/mob/living/carbon/human/species_types/vampire.dm:211
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        if (!iscarbon(owner))
            return FALSE;

        mob_living_carbon user = (mob_living_carbon)owner;
        obj_item_organ_fangs_vampire fang_drinker = (obj_item_organ_fangs_vampire)target;
        if (!(fang_drinker.drain_cooldown <= world.time)) // COOLDOWN_FINISHED
        {
            to_chat(user, span_warning(I18n.T("You just drained blood, wait a few seconds!")));
            return FALSE;
        }

        if (!iscarbon(user.pulling))
            return FALSE;

        mob_living_carbon victim = (mob_living_carbon)user.pulling;
        if (Dm.num(user.get_blood_volume()) >= BLOOD_VOLUME_MAXIMUM)
        {
            to_chat(user, span_warning(I18n.T("You're already full!")));
            return FALSE;
        }
        if (Dm.eq(victim.stat, DEAD))
        {
            to_chat(user, span_warning(I18n.T("You need a living victim!")));
            return FALSE;
        }
        object blood_name = LOWER_TEXT((user.get_bloodtype() as datum_blood_type)?.get_blood_name());
        if (!Dm.truthy(victim.get_blood_volume()) || !Dm.eq(victim.get_blood_reagent(), user.get_blood_reagent()))
        {
            if (Dm.truthy(blood_name))
                to_chat(user, span_warning(I18n.T("[] doesn't have []!", victim, blood_name)));
            else
                to_chat(user, span_warning(I18n.T("[] doesn't have anything inside of them you could stomach!", victim)));
            return FALSE;
        }
        fang_drinker.drain_cooldown = world.time + 3 * SECONDS; // COOLDOWN_START
        if (Dm.truthy(victim.can_block_magic(MAGIC_RESISTANCE_HOLY, charge_cost: 0)))
        {
            victim.show_message(span_warning(I18n.T("[] tries to bite you, but stops before touching you!", user)));
            to_chat(user, span_warning(I18n.T("[] is blessed! You stop just in time to avoid catching fire.", victim)));
            return FALSE;
        }
        if (Dm.truthy(victim.has_reagent(typeof(datum_reagent_consumable_garlic))))
        {
            victim.show_message(span_warning(I18n.T("[] tries to bite you, but recoils in disgust!", user)));
            to_chat(user, span_warning(I18n.T("[] reeks of garlic! you can't bring yourself to drain such tainted blood.", victim)));
            return FALSE;
        }
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: victim, cog_icon: null)))
            return FALSE;

        victim.show_message(span_danger(I18n.T("[] is draining your blood!", user)));
        to_chat(user, span_notice(I18n.T("You drain some blood!")));
        playsound(user, "sound/items/drink.ogg", 30, TRUE, -2);

        // Since we adjust the user first, we need to take the victim's blood volume into account.
        object amount_drained = Dm.min(datum_species_human_vampire.VAMP_DRAIN_AMOUNT, victim.get_blood_volume());

        // Takes into account how much blood the vampire can take.
        amount_drained = user.adjust_blood_volume(amount_drained);

        victim.adjust_blood_volume(-Dm.num(amount_drained));

        if (!Dm.truthy(victim.get_blood_volume()))
            to_chat(user, span_notice(I18n.T("You finish off []'s [] supply.", victim, blood_name)));
        return TRUE;
    }
}
