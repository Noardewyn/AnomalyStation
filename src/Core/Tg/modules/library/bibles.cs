// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/bibles.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the bible: healing by the holy role, the skin choice, the water's blessing, the bullet it can stop; the sect's own
// blessing and corpse blessing since the chapel's sects. The syndicate tome (syndicate G3b); its rune clearing
// (the effect_remover component) is not live, so the AddComponent does nothing (not-translated.md 136). Gaps H1: the curse on a heathen who
// burns it (the omen) and the smiting of the twice cursed.

// tg: code/modules/library/bibles.dm:1
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/library/bibles.dm  GLOBAL_LIST_INIT(biblenames, list(...))
    public DmList biblenames;

    // tg: code/modules/library/bibles.dm  (GLOBAL_MANAGED(biblenames, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbiblenames()
    {
        biblenames = Dm.list("Bible", "Quran", "Scrapbook", "Burning Bible", "Clown Bible", "Banana Bible", "Creeper Bible", "White Bible", "Holy Light", "The God Delusion", "Tome", "The King in Yellow", "Ithaqua", "Scientology", "Melted Bible", "Necronomicon", "Insulationism", "Guru Granth Sahib", "Kojiki");
        Dm.append(ref gvars_datum_init_order, "biblenames");
    }

    // tg: code/modules/library/bibles.dm  GLOBAL_LIST_INIT(biblestates, list(...))
    public DmList biblestates;

    // tg: code/modules/library/bibles.dm  (GLOBAL_MANAGED(biblestates, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbiblestates()
    {
        biblestates = Dm.list("bible", "koran", "scrapbook", "burning", "honk1", "honk2", "creeper", "white", "holylight", "atheist", "tome", "kingyellow", "ithaqua", "scientology", "melted", "necronomicon", "insuls", "gurugranthsahib", "kojiki");
        Dm.append(ref gvars_datum_init_order, "biblestates");
    }

    // tg: code/modules/library/bibles.dm  GLOBAL_LIST_INIT(bibleitemstates, list(...))
    public DmList bibleitemstates;

    // tg: code/modules/library/bibles.dm  (GLOBAL_MANAGED(bibleitemstates, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbibleitemstates()
    {
        bibleitemstates = Dm.list("bible", "koran", "scrapbook", "burning", "honk1", "honk2", "creeper", "white", "holylight", "atheist", "tome", "kingyellow", "ithaqua", "scientology", "melted", "necronomicon", "kingyellow", "gurugranthsahib", "kojiki");
        Dm.append(ref gvars_datum_init_order, "bibleitemstates");
    }
}

// tg: code/modules/library/bibles.dm:68
public partial class obj_item_book_bible : obj_item_book
{
    // tg: code/modules/library/bibles.dm:83
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_anti_magic), MAGIC_RESISTANCE_HOLY));
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_bullet_intercepting),
            Dm.named("active_slots", ITEM_SLOT_SUITSTORE),
            Dm.named("on_intercepted", Dm.@new<datum_callback>(this, nameof(on_intercepted_bullet))),
            Dm.named("block_charges", 1)));
        RegisterSignal(this, COMSIG_ATOM_IGNITED_BY_ITEM, nameof(curse_heathen));
        return dot;
    }

    /// Destroy the bible when it's shot by a bullet
    // tg: code/modules/library/bibles.dm:95
    public virtual async DmTask on_intercepted_bullet(mob_living victim = null, obj_projectile bullet = null)
    {
        await victim.add_mood_event("blessing", typeof(datum_mood_event_blessing));
        playsound(victim, Dm.resource("sound/effects/magic/magic_block_holy.ogg"), 50, TRUE);
        victim.visible_message(span_warning(I18n.T("[] takes [] in []'s place!", this, bullet, victim)));
        obj_structure_fluff_paper_stack pages = Dm.@new<obj_structure_fluff_paper_stack>(get_turf(this));
        await pages.setDir(pick(GLOB.alldirs));
        name = "punctured bible";
        desc = "A memento of good luck, or perhaps divine intervention?";
        icon_state = "shot";
        if (!Dm.truthy(GLOB.bible_icon_state))
            GLOB.bible_icon_state = "shot"; // New symbol of your religion if you hadn't picked one
        if (atom_storage != null)
            await atom_storage.remove_all(get_turf(this));
        QDEL_NULL(ref atom_storage);
    }

    // tg: code/modules/library/bibles.dm:109
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(deity_name))
            Dm.append(ref dot, span_notice(I18n.T("This bible has been approved by [].", deity_name)));
        if (Dm.truthy(user.mind?.holy_role))
        {
            if (Dm.truthy(GLOB.chaplain_altars.len))
                Dm.append(ref dot, span_notice(I18n.T("[] has an expansion pack to replace any broken Altar.", this)));
            else
                Dm.append(ref dot, span_notice(I18n.T("[] can be unpacked by hitting the floor of a holy area with it.", this)));
        }
        return dot;
    }

    // tg: code/modules/library/bibles.dm:119
    public override object get_attack_self_context(mob_living user = null)
    {
        if (Dm.truthy(can_set_bible_skin(user)))
            return "Select bible skin";
        return null;
    }

    // tg: code/modules/library/bibles.dm:123
    [SignalHandler]
    public virtual void curse_heathen(datum source = null, mob_living user = null, obj_item burning_tool = null)
    {
        // no deity to cast a curse upon thee
        if (!Dm.truthy(deity_name))
            return;
        var existing_omen = user.GetComponent(typeof(datum_component_omen)) as datum_component_omen;
        //DOUBLE CURSED?! Just straight up gib the guy.
        if (existing_omen != null)
        {
            to_chat(user, span_userdanger(I18n.T("[] <b>SMITE</b> thee!", deity_name)));
            add_memory_in_range(user, 7, typeof(datum_memory_witnessed_gods_wrath), Dm.named("protagonist", user), Dm.named("deuteragonist", this), Dm.named("antagonist", deity_name));
            if (user.client != null)
                Dm.Deferred("code/modules/library/bibles.dm:134", "user.client?.give_award(/datum/award/achievement/misc/gods_wrath, user): the achievements (D332)");
            Waiting.Detach(user.gib(DROP_ALL_REMAINS));
        }
        else
        {
            to_chat(user, span_userdanger(I18n.T("[] cast a curse upon thee!", deity_name)));
            Waiting.NoSleep(user.AddComponent(typeof(datum_component_omen_bible)));
        }
    }

    // tg: code/modules/library/bibles.dm:140
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is offering []self to []! It looks like [] trying to commit suicide!", user, user.p_them(), deity_name, user.p_theyre())));
        return BRUTELOSS;
    }

    // tg: code/modules/library/bibles.dm:144
    public virtual object can_set_bible_skin(mob_living user = null)
    {
        if (Dm.truthy(GLOB.bible_icon_state))
            return FALSE;
        if (!Dm.eq(user?.mind?.holy_role, HOLY_ROLE_HIGHPRIEST))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/library/bibles.dm:151
    public override async DmTask<object> attack_self(mob user_mob = null, object modifiers = null, params object[] __extra)
    {
        mob_living_carbon_human user = user_mob as mob_living_carbon_human;
        if (!Dm.truthy(can_set_bible_skin(user)))
            return FALSE;

        DmList skins = Dm.list();
        for (float i = 1; i <= GLOB.biblestates.len; i++)
        {
            image bible_image = image(icon: Dm.resource("icons/obj/storage/book.dmi"), icon_state: GLOB.biblestates[i]);
            Dm.append(ref skins, Dm.list(Dm.kv(Dm.text("[]", GLOB.biblenames[i]), bible_image)));
        }

        object choice = await show_radial_menu(user, this, skins, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), radius: 40, require_near: TRUE);
        if (!Dm.truthy(choice))
            return FALSE;
        float bible_index = GLOB.biblenames.Find(choice);
        if (!Dm.truthy(bible_index))
            return FALSE;
        icon_state = Dm.str(GLOB.biblestates[bible_index]);
        inhand_icon_state = Dm.str(GLOB.bibleitemstates[bible_index]);

        switch (icon_state)
        {
            case "honk1":
                await user.dna.add_mutation(typeof(datum_mutation_clumsy), MUTATION_SOURCE_CLOWN_CLUMSINESS);
                await user.equip_to_slot_or_del(Dm.@new<obj_item_clothing_mask_gas_clown_hat>(user), ITEM_SLOT_MASK);
                break;
            case "honk2":
                await user.dna.add_mutation(typeof(datum_mutation_clumsy), MUTATION_SOURCE_CLOWN_CLUMSINESS);
                await user.equip_to_slot_or_del(Dm.@new<obj_item_clothing_mask_gas_clown_hat>(user), ITEM_SLOT_MASK);
                break;
            case "insuls":
                obj_item_clothing_gloves_color_fyellow insuls = Dm.@new<obj_item_clothing_gloves_color_fyellow>();
                insuls.name = "insuls";
                insuls.desc = "A mere copy of the true insuls.";
                insuls.siemens_coefficient = 0.99999f;
                await user.equip_to_slot(insuls, ITEM_SLOT_GLOVES);
                break;
        }
        GLOB.bible_icon_state = icon_state;
        GLOB.bible_inhand_icon_state = inhand_icon_state;
        SSblackbox.record_feedback("text", "religion_book", 1, Dm.text("[]", choice));
        return null;
    }

    /**
     * Checks if we are allowed to interact with a radial menu
     *
     * Arguments:
     * * user The mob interacting with the menu
     */
    // tg: code/modules/library/bibles.dm:192
    public virtual object check_menu(mob_living_carbon_human user = null)
    {
        if (user == null || !Dm.truthy(user.is_holding(this)))
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        if (!Dm.truthy(can_set_bible_skin(user)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/library/bibles.dm:201
    public virtual async DmTask make_new_altar(atom bible_smacked = null, mob user = null)
    {
        turf new_altar_area = get_turf(bible_smacked);

        balloon_alert(user, I18n.T("unpacking bible..."));
        if (!Dm.truthy(await do_after(user, 15 * SECONDS, new_altar_area)))
            return;
        Dm.@new<obj_structure_altar_of_gods>(new_altar_area);
        qdel(this);
    }

    // tg: code/modules/library/bibles.dm:210
    public virtual async DmTask<object> bless(mob_living blessed = null, mob_living user = null)
    {
        if (GLOB.religious_sect != null)
            return await GLOB.religious_sect.sect_bless(blessed, user);

        if (!ishuman(blessed))
            return BLESSING_FAILED;

        mob_living_carbon_human built_in_his_image = blessed as mob_living_carbon_human;
        foreach (obj_item_bodypart bodypart in Dm.each<obj_item_bodypart>(built_in_his_image.get_bodyparts()))
        {
            if (!Dm.truthy(IS_ORGANIC_LIMB(bodypart)))
            {
                balloon_alert(user, I18n.T("can't heal inorganic!"));
                return BLESSING_IGNORED;
            }
        }

        float heal_amt = 10;
        DmList hurt_limbs = built_in_his_image.get_damaged_bodyparts(1, 1, BODYTYPE_ORGANIC) as DmList;
        if (!Dm.truthy(length(hurt_limbs)))
            return BLESSING_IGNORED;

        foreach (obj_item_bodypart affecting in Dm.each<obj_item_bodypart>(hurt_limbs))
        {
            if (Dm.truthy(await affecting.heal_damage(heal_amt, heal_amt, required_bodytype: BODYTYPE_ORGANIC)))
                await built_in_his_image.update_damage_overlays();
        }

        built_in_his_image.visible_message(span_notice(I18n.T("[] heals [] with the power of []!", user, built_in_his_image, deity_name)));
        to_chat(built_in_his_image, span_boldnotice(I18n.T("May the power of [] compel you to be healed!", deity_name)));
        playsound(built_in_his_image, SFX_PUNCH, 25, TRUE, -1);
        await built_in_his_image.add_mood_event("blessing", typeof(datum_mood_event_blessing));
        return BLESSING_SUCCESS;
    }

    // tg: code/modules/library/bibles.dm:238
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user_living = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object heal_mode = Dm.arg(__extra, 0) ?? TRUE;
        mob_living_carbon_human user = user_living as mob_living_carbon_human;
        if (!ISADVANCEDTOOLUSER(user_living))
        {
            balloon_alert(user_living, I18n.T("not dextrous enough!"));
            return null;
        }

        if (HAS_TRAIT(user, TRAIT_CLUMSY) && prob(50))
        {
            to_chat(user, span_danger(I18n.T("[] slips out of your hand and hits your head.", this)));
            await user.take_bodypart_damage(10);
            await user.Unconscious(40 * SECONDS);
            return null;
        }

        if (!Dm.truthy(user.mind?.holy_role))
        {
            to_chat(user, span_danger(I18n.T("The book sizzles in your hands.")));
            await user.take_bodypart_damage(burn: 10);
            return null;
        }

        if (!Dm.truthy(heal_mode))
            return await base.attack__proc(target_mob, user_living, modifiers, attack_modifiers, __extra);

        if (Dm.eq(target_mob.stat, DEAD))
        {
            if (GLOB.religious_sect != null && Dm.eq(await GLOB.religious_sect.sect_dead_bless(target_mob, user), BLESSING_FAILED))
            {
                target_mob.visible_message(span_danger(I18n.T("[] smacks []'s lifeless corpse with [].", user, target_mob, this)));
                playsound(target_mob, SFX_PUNCH, 25, TRUE, -1);
            }
            return null;
        }

        if (user == target_mob)
        {
            balloon_alert(user, I18n.T("can't heal yourself!"));
            return null;
        }

        float smack_chance = DEFAULT_SMACK_CHANCE;
        if (GLOB.religious_sect != null)
            smack_chance = GLOB.religious_sect.smack_chance;

        if (!prob(smack_chance))
        {
            object bless_result = await bless(target_mob, user);
            if (!Dm.eq(bless_result, BLESSING_FAILED))
            {
                if (Dm.truthy(target_mob?._listen_lookup?[COMSIG_LIVING_BLESSED]))
                    target_mob._SendSignal(COMSIG_LIVING_BLESSED, Dm.list(target_mob, user, this, bless_result));
                return null;
            }
        }

        if (ishuman(target_mob))
        {
            mob_living_carbon_human human_target = target_mob as mob_living_carbon_human;
            if (human_target.head is not obj_item_clothing_head_helmet)
            {
                await human_target.adjust_organ_loss(ORGAN_SLOT_BRAIN, 5, 60);
                human_target.balloon_alert(human_target, I18n.T("you feel dumber!"));
            }
        }
        target_mob.visible_message(span_danger(I18n.T("[] beats [] over the head with []!", user, target_mob, this)),
                span_userdanger(I18n.T("[] beats [] over the head with []!", user, target_mob, this)));
        playsound(target_mob, SFX_PUNCH, 25, TRUE, -1);
        log_combat(user, target_mob, "attacked", this);
        return null;
    }

    // tg: code/modules/library/bibles.dm:287
    public override async DmTask<object> interact_with_atom(atom bible_smacked = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(user.mind?.holy_role))
            return null;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(bible_smacked._listen_lookup?[COMSIG_BIBLE_SMACKED]) ? (object)NONE : bible_smacked._SendSignal(COMSIG_BIBLE_SMACKED, Dm.list(bible_smacked, user))), COMSIG_END_BIBLE_CHAIN)))
            return ITEM_INTERACT_SUCCESS;
        if (isfloorturf(bible_smacked))
        {
            area current_area = get_area(bible_smacked) as area;
            if (!Dm.truthy(GLOB.chaplain_altars.len) && current_area is area_station_service_chapel)
            {
                await make_new_altar(bible_smacked, user);
                return ITEM_INTERACT_SUCCESS;
            }
            foreach (obj_effect_rune nearby_runes in Dm.each<obj_effect_rune>(range(2, user)))
                nearby_runes.SetInvisibility(INVISIBILITY_NONE, id: type, priority: INVISIBILITY_PRIORITY_BASIC_ANTI_INVISIBILITY);
            bible_smacked.balloon_alert(user, I18n.T("floor smacked!"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy((bible_smacked.reagents as datum_reagents)?.has_reagent(typeof(datum_reagent_water)))) // blesses all the water in the holder
        {
            bible_smacked.balloon_alert(user, I18n.T("blessed"));
            float water2holy = Dm.num(((datum_reagents)bible_smacked.reagents).get_reagent_amount(typeof(datum_reagent_water)));
            await ((datum_reagents)bible_smacked.reagents).del_reagent(typeof(datum_reagent_water));
            await ((datum_reagents)bible_smacked.reagents).add_reagent(typeof(datum_reagent_water_holywater), water2holy);
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.truthy((bible_smacked.reagents as datum_reagents)?.has_reagent(typeof(datum_reagent_fuel_unholywater)))) // yeah yeah, copy pasted code - sue me
        {
            bible_smacked.balloon_alert(user, I18n.T("purified"));
            float unholy2holy = Dm.num(((datum_reagents)bible_smacked.reagents).get_reagent_amount(typeof(datum_reagent_fuel_unholywater)));
            await ((datum_reagents)bible_smacked.reagents).del_reagent(typeof(datum_reagent_fuel_unholywater));
            await ((datum_reagents)bible_smacked.reagents).add_reagent(typeof(datum_reagent_water_holywater), unholy2holy);
            return ITEM_INTERACT_SUCCESS;
        }
        if (bible_smacked is obj_item_book_bible && bible_smacked is not obj_item_book_bible_syndicate)
        {
            bible_smacked.balloon_alert(user, I18n.T("converted"));
            obj_item_book_bible other_bible = bible_smacked as obj_item_book_bible;
            other_bible.name = name;
            other_bible.icon_state = icon_state;
            other_bible.inhand_icon_state = inhand_icon_state;
            other_bible.deity_name = deity_name;
            return ITEM_INTERACT_SUCCESS;
        }

        if (bible_smacked is obj_item_melee_cultblade_haunted && !(Dm.truthy(user?.mind?.has_antag_datum(typeof(datum_antagonist_cult))) || HAS_TRAIT(user, TRAIT_ACT_AS_CULTIST)))
        {
            Dm.Deferred("code/modules/library/bibles.dm:325", "interact_with_atom: exorcising a bound haunted blade (the cult, not ported)");
            return ITEM_INTERACT_BLOCKING;
        }
        return NONE;
    }
}

// tg: code/modules/library/bibles.dm:341
public partial class obj_item_book_bible_booze : obj_item_book_bible
{
    // tg: code/modules/library/bibles.dm:343
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(carve_out());
        Dm.@new<obj_item_reagent_containers_cup_glass_bottle_whiskey>(this);
        return dot;
    }
}

// tg: code/modules/library/bibles.dm:348
public partial class obj_item_book_bible_syndicate : obj_item_book_bible
{
    // tg: code/modules/library/bibles.dm:365
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_anti_magic), Dm.bitor(MAGIC_RESISTANCE, MAGIC_RESISTANCE_HOLY)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_bane), Dm.named("affected_biotypes", MOB_SPIRIT), Dm.named("added_damage", 25)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_effect_remover),
            Dm.named("success_feedback", "You disrupt the magic of %THEEFFECT with %THEWEAPON."),
            Dm.named("success_forcesay", "BEGONE FOUL MAGIKS!!"),
            Dm.named("tip_text", "Clear rune"),
            Dm.named("effects_we_clear", Dm.list(typeof(obj_effect_rune), typeof(obj_effect_heretic_rune), typeof(obj_effect_cosmic_rune)))));
        return dot;
    }

    // tg: code/modules/library/bibles.dm:376
    public override async DmTask<object> attack_self(mob user_mob = null, object modifiers = null, params object[] __extra)
    {
        mob_living_carbon_human user = user_mob as mob_living_carbon_human;
        if (!Dm.truthy(uses) || user == null)
            return null;
        user.mind.set_holy_role(HOLY_ROLE_PRIEST);
        uses -= 1;
        to_chat(user, span_userdanger(I18n.T("You try to open the book AND IT BITES YOU!")));
        playsound(loc, Dm.resource("sound/effects/snap.ogg"), 50, TRUE);
        await user.apply_damage(5, BRUTE, user.get_active_hand(), attacking_item: this);
        to_chat(user, span_notice(I18n.T("Your name appears on the inside cover, in blood.")));
        owner_name = user.real_name;
        return null;
    }

    // tg: code/modules/library/bibles.dm:387
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(owner_name))
            dot.Add(span_warning(I18n.T("The name [] is written in blood inside the cover.", owner_name)));
        return dot;
    }

    // tg: code/modules/library/bibles.dm:392
    public override object get_attack_self_context(mob_living user = null)
    {
        if (Dm.truthy(uses))
            return "Read";
        return null;
    }

    // tg: code/modules/library/bibles.dm:396
    [DmParams("target_mob", "user", "modifiers", "attack_modifiers", "heal_mode")]
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user_living = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(user_living.combat_mode))
            return await base.attack__proc(target_mob, user_living, modifiers, attack_modifiers, __extra);
        return await base.attack__proc(target_mob, user_living, modifiers, attack_modifiers, FALSE);
    }
}
