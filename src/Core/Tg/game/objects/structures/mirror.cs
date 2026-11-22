// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/mirror.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H1: the whole file (the mirror: its reflection, the radial menu of hair and beard styles, breaking it and the seven incidents
// of bad luck (the omen), welding it whole, detaching it; the broken mirror; the magic mirrors with the race, sex, name, eyes and hair
// colour changes; the lesser and heretic mirrors; the badmin and pride mirrors; the wall frames). A round-start mirror's revenant
// prison waits for the revenant (antagonists, D333; not-translated.md section 190). The var blocks are generated (D111).

// tg: code/game/objects/structures/mirror.dm:21
public partial class obj_structure_mirror : obj_structure
{
    // tg: code/game/objects/structures/mirror.dm:3-15 (the file's #defines, #undef'd at its end)
    internal const string CHANGE_HAIR = "Change Hair";
    internal const string CHANGE_BEARD = "Change Beard";
    internal const string CHANGE_RACE = "Change Race";
    internal const string CHANGE_SEX = "Change Sex";
    internal const string CHANGE_NAME = "Change Name";
    internal const string CHANGE_EYES = "Change Eyes";
    // Chance for the mirror to be haunted at creation
    private const float ROUNDSTART_CURSED_CHANCE = 0.2f;

    private static ref DmList Initialize__reflection_filter => ref TgState.Current.obj_structure_mirror__Initialize__reflection_filter;
    private static ref matrix Initialize__reflection_matrix => ref TgState.Current.obj_structure_mirror__Initialize__reflection_matrix;

    // tg: code/game/objects/structures/mirror.dm:42
    public virtual void update_choices()
    {
        foreach (object i in Dm.each<object>(mirror_options.Copy()))
            mirror_options[i] = icon(Dm.resource("icons/hud/radial.dmi"), i);

        if (Dm.@in(CHANGE_RACE, mirror_options))
        {
            selectable_races = Dm.list();
            foreach (Type species_type in Dm.each<Type>(get_species_pool()))
            {
                if (!Dm.truthy(race_flags) || Dm.truthy(Dm.bitand(Dm.initial(species_type, (datum_species s) => s.changesource_flags), race_flags)))
                    selectable_races[Dm.initial(species_type, (datum_species s) => s.name)] = species_type;
            }
            selectable_races = sort_list(selectable_races) as DmList;
        }
        else
            selectable_races = null;
    }

    // tg: code/game/objects/structures/mirror.dm:56
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Initialize__reflection_filter ??= alpha_mask_filter(icon: icon(Dm.resource("icons/obj/watercloset.dmi"), "mirror_mask")) as DmList;
        Initialize__reflection_matrix ??= matrix(0.75f, 0, 0, 0, 0.75f, 0);
        Waiting.NoSleep(AddComponent(typeof(datum_component_reflection),
            Dm.named("reflection_filter", Initialize__reflection_filter),
            Dm.named("reflection_matrix", Initialize__reflection_matrix),
            Dm.named("can_reflect", Dm.@new<datum_callback>(this, nameof(can_reflect))),
            Dm.named("update_signals", Dm.list(COMSIG_ATOM_BREAK)),
            Dm.named("check_reflect_signals", Dm.list(SIGNAL_ADDTRAIT(TRAIT_NO_MIRROR_REFLECTION), SIGNAL_REMOVETRAIT(TRAIT_NO_MIRROR_REFLECTION)))));
        if (Dm.truthy(cursable))
        {
            if (Dm.truthy(mapload) && prob(ROUNDSTART_CURSED_CHANCE))
                Dm.Deferred("code/game/objects/structures/mirror.dm:68", "AddComponent(/datum/component/revenant_prison, create_on_release = TRUE): the revenant (antagonists, D333)");
            else
                ADD_TRAIT(this, TRAIT_COZY_REVENANT_HOME, INNATE_TRAIT);
        }
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        update_choices();
        register_context();
        return dot;
    }

    // tg: code/game/objects/structures/mirror.dm:77
    public virtual object can_reflect(atom_movable target = null)
    {
        // I'm doing it this way too, because the signal is sent before the broken variable is set to TRUE.
        if (Dm.num(atom_integrity) <= integrity_failure * Dm.num(max_integrity) || Dm.truthy(broken))
            return FALSE;
        if (!isliving(target) || HAS_TRAIT(target, TRAIT_NO_MIRROR_REFLECTION))
            return FALSE;
        return TRUE;
    }

    /// Returns a list species typepaths to use for the mirror. This pool is then filtered by flags.
    // tg: code/game/objects/structures/mirror.dm:86
    public virtual object get_species_pool()
    {
        return subtypesof(typeof(datum_species));
    }

    // tg: code/game/objects/structures/mirror.dm:102
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot) || !ishuman(user) || Dm.truthy(broken))
            return TRUE;

        await display_radial_menu((mob_living_carbon_human)user);
        return TRUE;
    }

    // tg: code/game/objects/structures/mirror.dm:110
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(deconstructable))
        {
            balloon_alert(user, "magic prevents detaching!");
            return NONE;
        }
        user.visible_message(span_notice(I18n.T("[] starts detaching []...", user, this)), span_notice(I18n.T("You start detaching []...", this)));
        tool.play_tool_sound(this);
        if (Dm.truthy(await tool.use_tool(this, user, 3 * SECONDS)))
        {
            user.visible_message(span_notice(I18n.T("[] detaches []!", user, this)), span_notice(I18n.T("You detach [] from the wall.", this)));
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            await deconstruct();
            return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/structures/mirror.dm:123
    public virtual async DmTask display_radial_menu(mob_living_carbon_human user = null)
    {
        while (true)
        {
            if (!Dm.truthy(await can_use_mirror(user)))
                return;

            object pick = await show_radial_menu(user, this, mirror_options, user, radius: 36, require_near: TRUE, tooltips: TRUE);
            if (!Dm.truthy(pick) || !Dm.truthy(await can_use_mirror(user)) || !Dm.truthy(await pre_change(user, pick)))
                return;

            switch (Dm.str(pick))
            {
                case CHANGE_HAIR:
                    await change_hair(user);
                    break;
                case CHANGE_BEARD:
                    await change_beard(user);
                    break;
                case CHANGE_RACE:
                    await change_race(user);
                    break;
                case CHANGE_SEX: // sex: yes
                    await change_sex(user);
                    break;
                case CHANGE_NAME:
                    await change_name(user);
                    break;
                case CHANGE_EYES:
                    await change_eyes(user);
                    break;
            }

            // tg calls display_radial_menu(user) again here; the loop is that recursion
        }
    }

    /// Ran before we dive into any changes, can be used to block changes by returning FALSE
    // tg: code/game/objects/structures/mirror.dm:148
    public virtual async DmTask<object> pre_change(mob_living_carbon_human user = null, object picked = null)
    {
        await DmTask.CompletedTask;
        return TRUE;
    }

    /// Checks if the mob can continue to use the mirror
    // tg: code/game/objects/structures/mirror.dm:152
    public virtual async DmTask<object> can_use_mirror(mob_living_carbon_human user = null)
    {
        return !QDELETED(this) && !QDELETED(user) && !Dm.truthy(broken) && Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH));
    }

    // tg: code/game/objects/structures/mirror.dm:155
    public virtual async DmTask change_beard(mob_living_carbon_human beard_dresser = null)
    {
        if (Dm.eq(beard_dresser.physique, FEMALE))
        {
            if (Dm.eq(beard_dresser.facial_hairstyle, "Shaved"))
            {
                balloon_alert(beard_dresser, "nothing to shave!");
                return;
            }
            object shave_beard = await tgui_alert(beard_dresser, I18n.T("Shave your beard?"), I18n.T("Grooming"), Dm.list("Yes", "No"));
            if (Dm.eq(shave_beard, "Yes") && Dm.truthy(await can_use_mirror(beard_dresser)))
                beard_dresser.set_facial_hairstyle("Shaved", update: TRUE);
            return;
        }

        object new_style = await tgui_input_list(beard_dresser, I18n.T("Select a facial hairstyle"), I18n.T("Grooming"), SSaccessories.facial_hairstyles_list);

        if (new_style == null || !Dm.truthy(await can_use_mirror(beard_dresser)))
            return;

        if (HAS_TRAIT(beard_dresser, TRAIT_SHAVED))
        {
            to_chat(beard_dresser, span_notice(I18n.T("If only growing back facial hair were that easy for you... The reminder makes you feel terrible.")));
            await beard_dresser.add_mood_event("bald_hair_day", typeof(datum_mood_event_bald_reminder));
            return;
        }

        beard_dresser.set_facial_hairstyle(new_style, update: TRUE);
    }

    // tg: code/game/objects/structures/mirror.dm:177
    public virtual async DmTask change_hair(mob_living_carbon_human hairdresser = null)
    {
        object new_style = await tgui_input_list(hairdresser, I18n.T("Select a hairstyle"), I18n.T("Grooming"), SSaccessories.hairstyles_list);
        if (new_style == null || !Dm.truthy(await can_use_mirror(hairdresser)))
            return;
        if (HAS_TRAIT(hairdresser, TRAIT_BALD))
        {
            to_chat(hairdresser, span_notice(I18n.T("If only growing back hair were that easy for you... The reminder makes you feel terrible.")));
            await hairdresser.add_mood_event("bald_hair_day", typeof(datum_mood_event_bald_reminder));
            return;
        }

        hairdresser.set_hairstyle(new_style, update: TRUE);
    }

    // tg: code/game/objects/structures/mirror.dm:188
    public virtual async DmTask change_name(mob_living_carbon_human user = null)
    {
        object newname = await sanitize_name(await tgui_input_text(user, I18n.T("Who are we again?"), I18n.T("Name change"), user.name, MAX_NAME_LEN), allow_numbers: TRUE); //It's magic so whatever.
        if (!Dm.truthy(newname) || !Dm.truthy(await can_use_mirror(user)))
            return;
        user.real_name = Dm.str(newname);
        user.name = Dm.str(newname);
        if (user.dna != null)
            user.dna.real_name = Dm.str(newname);
        if (user.mind != null)
            user.mind.name = newname;
    }

    // Erm ackshually the proper term is species. Get it right??
    // tg: code/game/objects/structures/mirror.dm:200
    public virtual async DmTask change_race(mob_living_carbon_human race_changer = null)
    {
        while (true)
        {
            object racechoice = await tgui_input_list(race_changer, I18n.T("What are we again?"), I18n.T("Race change"), selectable_races);
            if (racechoice == null || !Dm.truthy(await can_use_mirror(race_changer)))
                return;

            var new_race_path = selectable_races[racechoice] as Type;
            if (new_race_path == null || !typeof(datum_species).IsAssignableFrom(new_race_path))
                return;

            var newrace = GLOB.species_prototypes[new_race_path] as datum_species;
            object attributes_desc = newrace.get_physical_attributes();

            object answer = await tgui_alert(race_changer, attributes_desc, I18n.T("Become a []?", newrace), Dm.list("Yes", "No"));
            if (!Dm.truthy(answer) || !Dm.truthy(await can_use_mirror(race_changer)))
                return;
            if (!Dm.eq(answer, "Yes"))
                continue; // try again (tg's change_race(race_changer))

            await on_species_change(race_changer, newrace);
            race_changer.set_species(new_race_path, icon_update: FALSE);
            if (HAS_TRAIT(race_changer, TRAIT_USES_SKINTONES))
            {
                object new_s_tone = await tgui_input_list(race_changer, I18n.T("Choose your skin tone"), I18n.T("Race change"), GLOB.skin_tones);
                if (Dm.truthy(new_s_tone) && Dm.truthy(await can_use_mirror(race_changer)))
                {
                    race_changer.skin_tone = Dm.str(new_s_tone);
                    race_changer.dna.update_ui_block(typeof(datum_dna_block_identity_skin_tone));
                }
            }
            else if (HAS_TRAIT(race_changer, TRAIT_MUTANT_COLORS) && !HAS_TRAIT(race_changer, TRAIT_FIXED_MUTANT_COLORS))
            {
                object new_mutantcolor = await tgui_color_picker(race_changer, I18n.T("Choose your skin color:"), I18n.T("Race change"), race_changer.dna.features[FEATURE_MUTANT_COLOR]);
                if (Dm.truthy(new_mutantcolor) && Dm.truthy(await can_use_mirror(race_changer)))
                {
                    DmList mutant_hsv = rgb2hsv(new_mutantcolor);
                    if (Dm.num(mutant_hsv[3]) >= 50) // mutantcolors must be bright
                    {
                        race_changer.dna.features[FEATURE_MUTANT_COLOR] = sanitize_hexcolor(new_mutantcolor);
                        race_changer.dna.update_uf_block(typeof(datum_dna_block_feature_mutant_color));
                    }
                    else
                        to_chat(race_changer, span_notice(I18n.T("Invalid color. Your color is not bright enough.")));
                }
            }

            await race_changer.update_body(Dm.named("is_creating", TRUE));
            return;
        }
    }

    /// Hook for mirrors to do stuff on species change
    // tg: code/game/objects/structures/mirror.dm:239
    public virtual async DmTask on_species_change(mob_living_carbon_human race_changer = null, datum_species newrace = null)
    {
        await DmTask.CompletedTask;
    }

    // possible Genders: MALE, FEMALE, PLURAL, NEUTER
    // possible Physique: MALE, FEMALE
    // saved you a click (many)
    // tg: code/game/objects/structures/mirror.dm:245
    public virtual async DmTask change_sex(mob_living_carbon_human sexy = null)
    {
        object chosen_sex = await tgui_input_list(sexy, I18n.T("Become a.."), I18n.T("Confirmation"), Dm.list("Warlock", "Witch", "Wizard", "Itzard")); // YOU try coming up with the 'it' version of wizard
        if (!Dm.truthy(chosen_sex) || !Dm.truthy(await can_use_mirror(sexy)))
            return;

        switch (Dm.str(chosen_sex))
        {
            case "Warlock":
                sexy.gender = MALE;
                to_chat(sexy, span_notice(I18n.T("Man, you feel like a man!")));
                break;
            case "Witch":
                sexy.gender = FEMALE;
                to_chat(sexy, span_notice(I18n.T("Man, you feel like a woman!")));
                break;
            case "Wizard":
                sexy.gender = PLURAL;
                to_chat(sexy, span_notice(I18n.T("Woah dude, you feel like a dude!")));
                break;
            case "Itzard":
                sexy.gender = NEUTER;
                to_chat(sexy, span_notice(I18n.T("Woah dude, you feel like something else!")));
                break;
        }

        object chosen_physique = await tgui_input_list(sexy, I18n.T("Alter your physique as well?"), I18n.T("Confirmation"), Dm.list("Warlock Physique", "Witch Physique", "Wizards Don't Need Gender"));

        if (Dm.truthy(chosen_physique) && !Dm.eq(chosen_physique, "Wizards Don't Need Gender") && Dm.truthy(await can_use_mirror(sexy)))
            sexy.physique = Dm.eq(chosen_physique, "Warlock Physique") ? MALE : FEMALE;

        sexy.dna.update_ui_block(typeof(datum_dna_block_identity_gender));
        await sexy.update_body(Dm.named("is_creating", TRUE)); // or else physique won't change properly
        await sexy.update_clothing(ITEM_SLOT_ICLOTHING); // update gender shaped clothing
    }

    // tg: code/game/objects/structures/mirror.dm:274
    public virtual async DmTask change_eyes(mob_living_carbon_human user = null)
    {
        object new_eye_color = await tgui_color_picker(user, I18n.T("Choose your eye color"), I18n.T("Eye Color"), user.eye_color_left);
        if (new_eye_color == null || !Dm.truthy(await can_use_mirror(user)))
            return;
        await user.set_eye_color(sanitize_hexcolor(new_eye_color));
        user.dna.update_ui_block(typeof(datum_dna_block_identity_eye_colors));
        await user.update_eyes();
        to_chat(user, span_notice(I18n.T("You gaze at your new eyes with your new eyes. Perfect!")));
    }

    // tg: code/game/objects/structures/mirror.dm:283
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(deconstructable))
            Dm.append(ref dot, span_notice(I18n.T("It's mounted to the wall with a couple of <b>bolts</b>.")));
        return dot;
    }

    // tg: code/game/objects/structures/mirror.dm:288
    public override object examine_status(mob user = null)
    {
        if (Dm.truthy(broken))
            return Dm.list(); // no message spam
        return base.examine_status(user);
    }

    // tg: code/game/objects/structures/mirror.dm:293
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (held_item == null)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Open Customize Radial";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH) && Dm.truthy(deconstructable))
        {
            context[SCREENTIP_CONTEXT_RMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/objects/structures/mirror.dm:303
    public override async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (Dm.truthy(broken))
            return await base.attacked_by(attacking_item, user, modifiers, attack_modifiers);
        object dot = await base.attacked_by(attacking_item, user, modifiers, attack_modifiers);
        if (!Dm.truthy(broken) || Dm.num(dot) <= 0) // breaking a mirror truly gets you bad luck!
            return dot;
        to_chat(user, span_warning(I18n.T("A chill runs down your spine as [] shatters...", this)));
        await user.AddComponent(typeof(datum_component_omen), Dm.named("incidents_left", 7));
        return dot;
    }

    // tg: code/game/objects/structures/mirror.dm:312
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        obj_projectile proj = hitting_projectile;
        if (Dm.truthy(broken) || !isliving(proj.firer) || !Dm.truthy(proj.damage))
            return await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);

        object dot = await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
        if (Dm.truthy(broken)) // breaking a mirror truly gets you bad luck!
        {
            var unlucky_dude = (mob_living)proj.firer;
            to_chat(unlucky_dude, span_warning(I18n.T("A chill runs down your spine as [] shatters...", this)));
            await unlucky_dude.AddComponent(typeof(datum_component_omen), Dm.named("incidents_left", 7));
        }
        return dot;
    }

    // tg: code/game/objects/structures/mirror.dm:322
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (Dm.truthy(broken))
            return dot;
        icon_state = "mirror_broke";
        playsound(this, SFX_SHATTER, 70, TRUE);
        if (Dm.eq(desc, Dm.initial(GetType(), (obj_structure_mirror m) => m.desc)))
            desc = "Oh no, seven years of bad luck!";
        broken = TRUE;
        return dot;
    }

    // tg: code/game/objects/structures/mirror.dm:332
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (!Dm.truthy(disassembled))
            Dm.@new<obj_item_shard>(loc);
        else if (Dm.truthy(broken))
            Dm.@new<obj_item_wallframe_mirror_broken>(loc);
        else
            Dm.@new<obj_item_wallframe_mirror>(loc);
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/mirror.dm:340
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        await base.welder_act(user, tool);
        if (Dm.truthy(user.combat_mode))
            return FALSE;

        if (!Dm.truthy(broken))
            return TRUE;

        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return TRUE;

        balloon_alert(user, "repairing...");
        if (Dm.truthy(await tool.use_tool(this, user, 10, volume: 50)))
        {
            balloon_alert(user, "repaired");
            broken = FALSE;
            icon_state = Dm.initial(GetType(), (obj_structure_mirror m) => m.icon_state);
            desc = Dm.initial(GetType(), (obj_structure_mirror m) => m.desc);
        }

        return TRUE;
    }

    // tg: code/game/objects/structures/mirror.dm:360
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        switch (damage_type)
        {
            case BRUTE:
                playsound(this, Dm.resource("sound/effects/hit_on_shattered_glass.ogg"), 70, TRUE);
                break;
            case BURN:
                playsound(this, Dm.resource("sound/effects/hit_on_shattered_glass.ogg"), 70, TRUE);
                break;
        }
        return null;
    }
}

// tg: code/game/objects/structures/mirror.dm:91
public partial class obj_structure_mirror_broken : obj_structure_mirror
{
    // tg: code/game/objects/structures/mirror.dm:97
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra);
}

// tg: code/game/objects/structures/mirror.dm:389
public partial class obj_structure_mirror_magic : obj_structure_mirror
{
    // magical mirrors do nothing but give you the damn beard
    // tg: code/game/objects/structures/mirror.dm:397
    public override async DmTask change_beard(mob_living_carbon_human beard_dresser = null)
    {
        object new_style = await tgui_input_list(beard_dresser, I18n.T("Select a facial hairstyle"), I18n.T("Grooming"), SSaccessories.facial_hairstyles_list);
        if (new_style == null || !Dm.truthy(await can_use_mirror(beard_dresser)))
            return;

        beard_dresser.set_facial_hairstyle(new_style, update: TRUE);
    }

    //Magic mirrors can change hair color as well
    // tg: code/game/objects/structures/mirror.dm:405
    public override async DmTask change_hair(mob_living_carbon_human user = null)
    {
        object hairchoice = await tgui_alert(user, I18n.T("Hairstyle or hair color?"), I18n.T("Change Hair"), Dm.list("Style", "Color"));
        if (!Dm.truthy(await can_use_mirror(user)))
            return;
        if (Dm.eq(hairchoice, "Style")) //So you just want to use a mirror then?
        {
            await base.change_hair(user);
            return;
        }

        object new_hair_color = await tgui_color_picker(user, I18n.T("Choose your hair color"), I18n.T("Hair Color"), user.hair_color);
        if (!Dm.truthy(new_hair_color) || !Dm.truthy(await can_use_mirror(user)))
            return;
        if (Dm.truthy(new_hair_color))
        {
            user.set_haircolor(sanitize_hexcolor(new_hair_color));
            user.dna.update_ui_block(typeof(datum_dna_block_identity_hair_color));
        }
        if (Dm.eq(user.physique, MALE))
        {
            object new_face_color = await tgui_color_picker(user, I18n.T("Choose your facial hair color"), I18n.T("Hair Color"), user.facial_hair_color);
            if (Dm.truthy(new_face_color) && Dm.truthy(await can_use_mirror(user)))
            {
                user.set_facial_haircolor(sanitize_hexcolor(new_face_color));
                user.dna.update_ui_block(typeof(datum_dna_block_identity_facial_color));
            }
        }
    }

    /// Hook for mirrors to do stuff on species change
    // tg: code/game/objects/structures/mirror.dm:425
    public override async DmTask on_species_change(mob_living_carbon_human race_changer = null, datum_species newrace = null)
    {
        await DmTask.CompletedTask;
        if (HAS_TRAIT(race_changer, TRAIT_ADVANCEDTOOLUSER) && HAS_TRAIT(race_changer, TRAIT_LITERATE))
            return;

        to_chat(race_changer, span_alert(I18n.T("You feel quite intelligent.")));
        // Prevents wizards from being soft locked out of everything
        // If this stays after the species was changed once more, well, the magic mirror did it. It's magic i aint gotta explain shit
        race_changer.add_traits(Dm.list(TRAIT_LITERATE, TRAIT_ADVANCEDTOOLUSER), SPECIES_TRAIT);
    }
}

// tg: code/game/objects/structures/mirror.dm:434
public partial class obj_structure_mirror_magic_lesser : obj_structure_mirror_magic
{
    // tg: code/game/objects/structures/mirror.dm:437
    public override object get_species_pool()
    {
        DmList race_list = Dm.list();
        foreach (object species_id in Dm.each<object>(get_selectable_species()))
            Dm.append(ref race_list, GLOB.species_list[species_id]);
        return race_list;
    }
}

// tg: code/game/objects/structures/mirror.dm:443
public partial class obj_structure_mirror_magic_lesser_heretic : obj_structure_mirror_magic_lesser
{
    // tg: code/game/objects/structures/mirror.dm:448
    public override async DmTask on_species_change(mob_living_carbon_human race_changer = null, datum_species newrace = null)
    {
        await base.on_species_change(race_changer, newrace);
        if ((race_changer.dna?.species as datum_species)?.GetType() == newrace.GetType())
            return;
        await atom_break(null);
    }

    // tg: code/game/objects/structures/mirror.dm:454
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_shard>(loc); // never gives the frame back
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/mirror.dm:457
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        to_chat(user, span_alert(I18n.T("Trying to find where to start fixing [] proves nigh impossible.", this)));
        await DmTask.CompletedTask;
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/structures/mirror.dm:461
    public override async DmTask<object> pre_change(mob_living_carbon_human user = null, object picked = null)
    {
        // IS_HERETIC(user) (__DEFINES/antagonists.dm:293), expanded inline (rules 11.1)
        if (Dm.truthy(user.mind?.has_antag_datum(typeof(datum_antagonist_heretic))) || HAS_TRAIT(user, TRAIT_ACT_AS_HERETIC))
            return TRUE;

        to_chat(user, span_hypnophrase(I18n.T("You find yourself gazing at [], unable to look away...", this)));
        await user.Immobilize(5 * SECONDS);
        _addtimer(Dm.@new<datum_callback>(this, nameof(change_something), user, picked), 5 * SECONDS, file: "code/game/objects/structures/mirror.dm", line: 467);
        return FALSE;
    }

    // tg: code/game/objects/structures/mirror.dm:470
    public virtual async DmTask change_something(mob_living_carbon_human user = null, object picked = null)
    {
        if (QDELETED(this) || QDELETED(user))
            return;

        if (!Dm.truthy(await user.Adjacent(this)))
        {
            to_chat(user, span_warning(I18n.T("Pulled away from [], you snap out of your trance.", this)));
            return;
        }
        if (Dm.truthy(broken))
        {
            to_chat(user, span_warning(I18n.T("With the shattering of [], you snap out of your trance.", this)));
            return;
        }

        // randomizes your stuff, doesn't affect DNA though so you could easily get genetics to fix you
        switch (Dm.str(picked))
        {
            case CHANGE_HAIR:
            case CHANGE_BEARD:
                to_chat(user, span_hypnophrase(I18n.T("The hair - yes, the hair is wrong - []...", I18n.T(Dm.str(pick("it can be improved", "it needs to be changed", "it must be different"))))));
                user.set_hairstyle(random_hairstyle(user.gender));
                user.set_facial_hairstyle(random_facial_hairstyle(user.gender));
                break;
            case CHANGE_RACE:
            {
                to_chat(user, span_hypnophrase(I18n.T("The body - no, the body is all wrong - []...", I18n.T(Dm.str(pick("I must become anew", "I need to be different", "I want to be something else"))))));
                DmList options = Dm.sub(assoc_to_values(selectable_races), (user.dna.species as datum_species).GetType()) as DmList;
                var newrace = GLOB.species_prototypes[pick(options)] as datum_species;
                await on_species_change(user, newrace);
                user.set_species(newrace.GetType(), icon_update: FALSE);
                break;
            }
            case CHANGE_SEX:
            {
                to_chat(user, span_hypnophrase(I18n.T("My form - yes, the form is all wrong - I  need to change it..."))); // the wording used here needs to be very deliberate...
                DmList options = Dm.sub(Dm.list(MALE, FEMALE, PLURAL, NEUTER), user.gender) as DmList;
                user.gender = Dm.str(pick(options));
                user.physique = user.gender;
                break;
            }
            case CHANGE_EYES:
                to_chat(user, span_hypnophrase(I18n.T("My eyes - I cannot see clearly - []...", I18n.T(Dm.str(pick("They're simply not right", "they must be replaced", "they are both wrong"))))));
                await user.set_eye_color(random_eye_color());
                break;
            case CHANGE_NAME: // not enabled by default but for badmin support
                to_chat(user, span_hypnophrase(I18n.T("My name - it doesn't fit me - []...", I18n.T(Dm.str(pick("Something else would be better", "it must be changed", "Something more appropriate would be good"))))));
                user.real_name = Dm.str(user.generate_random_mob_name());
                user.update_visible_name();
                break;
        }
    }
}

// tg: code/game/objects/structures/mirror.dm:509
public partial class obj_structure_mirror_magic_pride : obj_structure_mirror_magic
{
    // tg: code/game/objects/structures/mirror.dm:517
    public override async DmTask<object> pre_change(mob_living_carbon_human user = null, object picked = null)
    {
        object dot = await base.pre_change(user, picked);
        changed = TRUE;
        return dot;
    }

    // tg: code/game/objects/structures/mirror.dm:521
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        changed = FALSE;
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(changed) || QDELETED(user) || !IN_GIVEN_RANGE(user, this, 3)) // 3 range gives a tiny bit of leeway if you try to run away after using it
            return dot;
        user.visible_message(
            span_bolddanger(I18n.T("The ground splits beneath [] as [] hand leaves the mirror!", user, user.p_their())),
            span_notice(I18n.T("Perfect. Much better! Now <i>nobody</i> will be able to resist yo-")));

        turf user_turf = get_turf(user);
        var new_chasm = await user_turf.ChangeTurf(typeof(turf_open_chasm_pride), flags: CHANGETURF_INHERIT_AIR) as turf_open_chasm_pride;
        // `ChangeTurf()` can itself lead to `drop()` if there's lattice present, so
        // we only force the user down if they're still on the same turf.
        if (get_turf(user) == user_turf)
            await new_chasm.drop(user);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/mirror.dm:58  var/static/list/reflection_filter
    internal DmList obj_structure_mirror__Initialize__reflection_filter;
    // tg: code/game/objects/structures/mirror.dm:59  var/static/matrix/reflection_matrix
    internal matrix obj_structure_mirror__Initialize__reflection_matrix;
}
