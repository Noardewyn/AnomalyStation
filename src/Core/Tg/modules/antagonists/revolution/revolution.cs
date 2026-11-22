// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/revolution/revolution.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps V1 (D327): the whole file: the revolutionary and the head revolutionary (conversion by flash, the code words only the heads
// hear, the conversion guideline, the flash and the syndicate security HUD they start with, promotion and demotion, the deconversion by
// mindshield, borging, blunt trauma or the revolution's defeat), the revolution team (its head cap, the promotion of new heads, the
// round result and the round-end report) and the revolutionary's preview outfit (a var block, generated, D111).

// tg: code/modules/antagonists/revolution/revolution.dm:1
public partial class datum_antagonist_rev : datum_antagonist
{
    // tg: code/modules/antagonists/revolution/revolution.dm:16
    public override object can_be_owned(datum_mind new_owner = null)
    {
        if (Dm.truthy(Dm.bitand(new_owner.assigned_role.job_flags, JOB_HEAD_OF_STAFF)))
            return FALSE;
        if (new_owner.current != null && HAS_MIND_TRAIT(new_owner.current, TRAIT_UNCONVERTABLE))
            return FALSE;
        return base.can_be_owned(new_owner);
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:23
    public override async DmTask admin_add(datum_mind new_owner = null, mob admin = null)
    {
        await new_owner.add_antag_datum(this);
        message_admins(Dm.text("[] has rev'ed [].", key_name_admin(admin), key_name_admin(new_owner)));
        log_admin(Dm.text("[] has rev'ed [].", key_name(admin), key_name(new_owner)));
        to_chat(new_owner.current, span_userdanger(I18n.T("You are a member of the revolution!")));
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:29
    public override async DmTask<object> apply_innate_effects(mob_living mob_override = null)
    {
        mob_living M = mob_override ?? owner.current as mob_living;
        await handle_clown_mutation(M, mob_override != null ? null : I18n.T("Your training has allowed you to overcome your clownish nature, allowing you to wield weapons without harming yourself."));
        add_team_hud(M, typeof(datum_antagonist_rev));
        return null;
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:34
    public override async DmTask remove_innate_effects(mob_living mob_override = null)
    {
        mob_living M = mob_override ?? owner.current as mob_living;
        await handle_clown_mutation(M, removing: FALSE);
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:38
    public override object on_mindshield(mob implanter = null, mob_living mob_override = null)
    {
        Waiting.Detach(remove_revolutionary(implanter));
        return COMPONENT_MINDSHIELD_DECONVERTED;
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:42
    public virtual async DmTask equip_rev()
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:45
    public override async DmTask on_gain()
    {
        await base.on_gain();
        await equip_rev();
        owner.current.log_message("has been converted to the revolution!", LOG_ATTACK, color: COLOR_CULT_RED);
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:50
    public override async DmTask create_team(datum_team new_team = null)
    {
        if (new_team == null)
        {
            GLOB.revolution_handler ??= Dm.@new<datum_revolution_handler>();
            rev_team = GLOB.revolution_handler.revs;
            await GLOB.revolution_handler.start_revolution();
            return;
        }
        if (new_team is not datum_team_revolution)
            stack_trace(Dm.text("Wrong team type passed to [] initialization.", GetType()), "code/modules/antagonists/revolution/revolution.dm", 57);
        rev_team = new_team as datum_team_revolution;
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:60
    public override object get_team() => rev_team;

    //Bump up to head_rev
    // tg: code/modules/antagonists/revolution/revolution.dm:64
    public virtual async DmTask promote()
    {
        datum_team old_team = rev_team;
        datum_mind old_owner = owner;
        silent = TRUE;
        await owner.remove_antag_datum(typeof(datum_antagonist_rev));
        var new_revhead = Dm.@new<datum_antagonist_rev_head>();
        new_revhead.silent = TRUE;
        await old_owner.add_antag_datum(new_revhead, old_team);
        new_revhead.silent = FALSE;
        to_chat(old_owner, span_userdanger(I18n.T("You have proved your devotion to revolution! You are a head revolutionary now!")));
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:75
    public override object get_admin_commands()
    {
        DmList dot = base.get_admin_commands() as DmList;
        dot["Promote"] = Dm.@new<datum_callback>(this, nameof(admin_promote));
        return dot;
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:79
    public virtual async DmTask admin_promote(mob admin = null)
    {
        datum_mind O = owner;
        await promote();
        message_admins(Dm.text("[] has head-rev'ed [].", key_name_admin(admin), O));
        log_admin(Dm.text("[] has head-rev'ed [].", key_name(admin), O));
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:85
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = await base.ui_static_data(user) as DmList;
        dot["leader"] = Dm.eq(pref_flag, ROLE_REV_HEAD) ? TRUE : FALSE;
        DmList heads = Dm.list();
        foreach (datum_mind head_of_staff in Dm.each_cast<datum_mind>(SSjob.get_all_heads()))
            Dm.append(ref heads, Dm.list(Dm.list(Dm.kv("name", head_of_staff.name), Dm.kv("role", head_of_staff.assigned_role.title))));
        dot["heads"] = heads;
        return dot;
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:282
    public virtual object can_be_converted(mob_living candidate = null)
    {
        if (candidate.mind == null)
            return FALSE;
        if (!Dm.truthy(can_be_owned(candidate.mind)))
            return FALSE;
        if (candidate is not mob_living_carbon) //Can't convert simple animals
            return FALSE;
        return TRUE;
    }

    /**
     * Adds a new mind to our revoltuion
     *
     * * rev_mind - the mind we're adding
     * * stun - If TRUE, we will flash act and apply a long stun when we're applied
     * * mute - If TRUE, we will apply a mute when we're applied
     */
    // tg: code/modules/antagonists/revolution/revolution.dm:299
    public virtual async DmTask<object> add_revolutionary(datum_mind rev_mind = null, float? stun = null, float? mute = null)
    {
        stun ??= TRUE;
        mute ??= TRUE;
        if (!Dm.truthy(can_be_converted(rev_mind.current as mob_living)))
            return FALSE;

        var current = (mob_living)rev_mind.current;
        if (Dm.truthy(mute))
            await current.set_silence_if_lower(10 * SECONDS);
        if (Dm.truthy(stun))
        {
            await current.flash_act(1, 1);
            await current.Stun(10 * SECONDS);
        }

        rev_mind.add_memory(typeof(datum_memory_recruited_by_headrev), Dm.named("protagonist", rev_mind.current), Dm.named("antagonist", owner.current));
        await rev_mind.add_antag_datum(typeof(datum_antagonist_rev), rev_team);
        return TRUE;
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:350
    public override async DmTask farewell()
    {
        if (owner.current == null)
            return;
        owner.current.balloon_alert_to_viewers(I18n.T("deconverted!"));
        if (ishuman(owner.current))
        {
            owner.current.visible_message(span_deconversion_message(I18n.T("[] looks like [] just remembered [] real allegiance!", owner.current, owner.current.p_theyve(), owner.current.p_their())), null, null, null, owner.current);
            to_chat(owner, Dm.text("<span class='deconversion_message bold'>[]</span>", I18n.T("You are no longer a brainwashed revolutionary! Your memory is hazy from the time you were a rebel...the only thing you remember is the name of the one who brainwashed you....")));
        }
        else if (issilicon(owner.current))
        {
            owner.current.visible_message(span_deconversion_message(I18n.T("The frame beeps contentedly, purging the hostile memory engram from the MMI before initializing it.")), null, null, null, owner.current);
            to_chat(owner, span_userdanger(I18n.T("The frame's firmware detects and deletes your neural reprogramming! You remember nothing but the name of the one who flashed you.")));
        }
        await DmTask.CompletedTask;
    }

    /// Handles rev removal via IC methods such as borging, mindshielding, blunt force trauma to the head or revs losing.
    // tg: code/modules/antagonists/revolution/revolution.dm:376
    public virtual async DmTask remove_revolutionary(object deconverter = null)
    {
        owner.current.log_message(Dm.text("has been deconverted from the revolution by []!", ismob(deconverter) ? key_name(deconverter) : deconverter), LOG_ATTACK, color: COLOR_CULT_RED);
        if (Dm.eq(deconverter, DECONVERTER_BORGED))
            message_admins(Dm.text("[] has been borged while being a []", ADMIN_LOOKUPFLW(owner.current), name));
        if (iscarbon(owner.current) && Dm.truthy(deconverter))
        {
            var formerrev = (mob_living_carbon)owner.current;
            await formerrev.Unconscious(10 * SECONDS);
        }
        deconversion_source = deconverter;
        await owner.remove_antag_datum(GetType());
    }
}

// tg: code/modules/antagonists/revolution/revolution.dm:171
public partial class datum_antagonist_rev_head : datum_antagonist_rev
{
    // tg: code/modules/antagonists/revolution/revolution.dm:92
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = await base.ui_static_data(user) as DmList;
        dot["code_phrases"] = rev_team.head_chose_phrase_raw;
        dot["code_responses"] = rev_team.head_code_responses_raw;
        dot["lone_wolf"] = !Dm.truthy(roundstart) || Dm.length(rev_team.get_head_revolutionaries()) == 1 ? TRUE : FALSE;
        dot["conversion_objective_max_length"] = MAX_CHARTER_LEN; // it's poetic, not lazy reuse, I swear
        dot["conversion_objective"] = html_decode(conversion_objective);
        return dot;
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:100
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.eq(action, "set_conversion_objective"))
        {
            if (!Dm.truthy(@params["conversion_objective"]))
            {
                conversion_objective = "";
                return dot;
            }

            object new_objective = trim(sanitize(@params["conversion_objective"]), MAX_CHARTER_LEN);
            if (!Dm.truthy(new_objective))
            {
                await tgui_alert(usr, I18n.T("Your objective is invalid. Please try again."));
                return dot;
            }
            if (Dm.truthy(is_ic_filtered(new_objective)))
            {
                await tgui_alert(usr, I18n.T("Your objective contains IC filtered words. Please remove them and try again."));
                return dot;
            }

            conversion_objective = Dm.str(new_objective);
            owner.current.log_message(Dm.text("set their conversion guideline to: []", conversion_objective), color: COLOR_CULT_RED);
        }
        return dot;
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:119
    public override async DmTask admin_add(datum_mind new_owner = null, mob admin = null)
    {
        give_flash = TRUE;
        give_hud = TRUE;
        remove_clumsy = TRUE;
        await new_owner.add_antag_datum(this);
        message_admins(Dm.text("[] has head-rev'ed [].", key_name_admin(admin), key_name_admin(new_owner)));
        log_admin(Dm.text("[] has head-rev'ed [].", key_name(admin), key_name(new_owner)));
        to_chat(new_owner.current, span_userdanger(I18n.T("You are a member of the revolutionaries' leadership now!")));
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:128
    public override object get_admin_commands()
    {
        DmList dot = base.get_admin_commands() as DmList;
        Dm.remove(ref dot, "Promote");
        dot["Take flash"] = Dm.@new<datum_callback>(this, nameof(admin_take_flash));
        dot["Give flash"] = Dm.@new<datum_callback>(this, nameof(admin_give_flash));
        dot["Repair flash"] = Dm.@new<datum_callback>(this, nameof(admin_repair_flash));
        dot["Demote"] = Dm.@new<datum_callback>(this, nameof(admin_demote));
        return dot;
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:136
    public virtual void admin_take_flash(mob admin = null)
    {
        var L = owner.current.get_contents() as DmList;
        var flash = Dm.locate<obj_item_assembly_flash_handheld>(L);
        if (flash == null)
        {
            to_chat(admin, span_danger("Deleting flash failed!"));
            return;
        }
        qdel(flash);
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:144
    public virtual async DmTask admin_give_flash(mob admin = null)
    {
        //This is probably overkill but making these impact state annoys me
        float old_give_flash = give_flash;
        float old_give_hud = give_hud;
        float old_remove_clumsy = remove_clumsy;
        give_flash = TRUE;
        give_hud = FALSE;
        remove_clumsy = FALSE;
        await equip_rev();
        give_flash = old_give_flash;
        give_hud = old_give_hud;
        remove_clumsy = old_remove_clumsy;
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:157
    public virtual void admin_repair_flash(mob admin = null)
    {
        var L = owner.current.get_contents() as DmList;
        var flash = Dm.locate<obj_item_assembly_flash_handheld>(L);
        if (flash == null)
            to_chat(admin, span_danger("Repairing flash failed!"));
        else
        {
            flash.burnt_out = FALSE;
            flash.update_appearance();
        }
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:166
    public virtual async DmTask admin_demote(mob admin = null)
    {
        message_admins(Dm.text("[] has demoted [] from head revolutionary.", key_name_admin(admin), key_name_admin(owner)));
        log_admin(Dm.text("[] has demoted [] from head revolutionary.", key_name(admin), key_name(owner)));
        await demote();
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:187
    public override object pre_mindshield(mob implanter = null, mob_living mob_override = null) => COMPONENT_MINDSHIELD_RESISTED;

    // tg: code/modules/antagonists/revolution/revolution.dm:190
    public override async DmTask on_removal()
    {
        if (!Dm.truthy(give_hud))
        {
            await base.on_removal();
            return;
        }
        var C = owner.current as mob_living_carbon;
        if (C == null)
        {
            await base.on_removal();
            return;
        }
        var S = C.get_organ_slot(ORGAN_SLOT_HUD) as obj_item_organ_cyberimp_eyes_hud_security_syndicate;
        if (S != null)
            await S.Remove(C);
        await base.on_removal();
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:201
    public override async DmTask<object> apply_innate_effects(mob_living mob_override = null)
    {
        object dot = await base.apply_innate_effects(mob_override);
        mob_living real_mob = mob_override ?? owner.current as mob_living;
        await real_mob.AddComponentFrom(REF(this), typeof(datum_component_can_flash_from_behind));
        RegisterSignal(real_mob, COMSIG_MOB_SUCCESSFUL_FLASHED_MOB, nameof(on_flash_success));

        await real_mob.AddComponent(typeof(datum_component_codeword_hearing), rev_team.head_code_phrases, "blue", this);
        await real_mob.AddComponent(typeof(datum_component_codeword_hearing), rev_team.head_code_responses, "red", this);
        return dot;
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:210
    public override async DmTask remove_innate_effects(mob_living mob_override = null)
    {
        await base.remove_innate_effects(mob_override);
        mob_living real_mob = mob_override ?? owner.current as mob_living;
        real_mob.RemoveComponentSource(REF(this), typeof(datum_component_can_flash_from_behind));
        UnregisterSignal(real_mob, COMSIG_MOB_SUCCESSFUL_FLASHED_MOB);

        foreach (datum_component_codeword_hearing component in Dm.each_cast<datum_component_codeword_hearing>(real_mob.GetComponents(typeof(datum_component_codeword_hearing))))
            component.delete_if_from_source(this);
    }

    /// Signal proc for [COMSIG_MOB_SUCCESSFUL_FLASHED_MOB].
    /// Bread and butter of revolution conversion, successfully flashing a carbon will make them a revolutionary
    // tg: code/modules/antagonists/revolution/revolution.dm:221
    [SignalHandler]
    public virtual void on_flash_success(mob_living source = null, mob_living flashed = null, obj_item_assembly_flash flash = null, object deviation = null)
    {
        if (flashed.stat == DEAD || issilicon(flashed) || isdrone(flashed))
            return;
        if (IS_UNCONSCIOUS_OR_CRIT(flashed))
        {
            to_chat(source, span_warning(I18n.T("[] must be conscious before you can convert []!", flashed.p_They(), flashed.p_them())));
            return;
        }

        if (flashed.mind == null || !Dm.truthy(GET_CLIENT(flashed)))
        {
            to_chat(source, span_warning(I18n.T("[]'s mind is so vacant that it is not susceptible to influence!", flashed)));
            return;
        }

        bool holiday_meme_chance = Dm.truthy(check_holidays(APRIL_FOOLS)) && Dm.truthy(prob(10));
        if (Dm.truthy(Waiting.NoSleep(add_revolutionary(flashed.mind, mute: holiday_meme_chance ? FALSE : TRUE)))) // don't mute if we roll the meme holiday chance
        {
            if (holiday_meme_chance)
                Waiting.Detach(_async_holiday_meme_say(flashed as mob_living_carbon)); // INVOKE_ASYNC
            flash.times_used--; // Flashes are less likely to burn out for headrevs, when used for conversion
        }
        else
            to_chat(source, span_warning(I18n.T("[] seems resistant to []!", flashed, flash)));
    }

    /// Used / called async from [proc/on_flash] to deliver a funny meme line
    // tg: code/modules/antagonists/revolution/revolution.dm:243
    public virtual async DmTask _async_holiday_meme_say(mob_living_carbon flashed = null)
    {
        if (ishuman(flashed))
        {
            var human_flashed = (mob_living_carbon_human)flashed;
            human_flashed.force_say();
        }
        await flashed.say(I18n.T("You son of a bitch! I'm in."), forced: "That son of a bitch! They're in. (April Fools)");
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:249
    public override object antag_listing_name() => Dm.text("[](Leader)", base.antag_listing_name());

    // tg: code/modules/antagonists/revolution/revolution.dm:252
    public override async DmTask<object> get_preview_icon()
    {
        var final_icon = await render_preview_outfit(preview_outfit) as icon;

        final_icon.Blend(await make_assistant_icon("Business Hair"), ICON_UNDERLAY, -8, 0);
        final_icon.Blend(await make_assistant_icon("CIA"), ICON_UNDERLAY, 8, 0);

        // Apply the rev head HUD, but scale up the preview icon a bit beforehand.
        // Otherwise, the R gets cut off.
        final_icon.Scale(64, 64);

        var rev_head_icon = Dm.icon(Dm.resource("icons/mob/huds/antag_hud.dmi"), "rev_head");
        rev_head_icon.Scale(48, 48);
        rev_head_icon.Crop(1, 1, 64, 64);
        rev_head_icon.Shift(EAST, 10);
        rev_head_icon.Shift(NORTH, 16);
        final_icon.Blend(rev_head_icon, ICON_OVERLAY);

        return finish_preview_icon(final_icon);
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:271
    public virtual async DmTask<object> make_assistant_icon(object hairstyle = null)
    {
        var assistant = Dm.@new<mob_living_carbon_human_dummy_consistent>();
        assistant.set_hairstyle(hairstyle, update: TRUE);

        var assistant_icon = await render_preview_outfit(typeof(datum_outfit_job_assistant_consistent), assistant) as icon;
        assistant_icon.Blend("#ffffff80", ICON_MULTIPLY); // universal_icon's change_opacity(0.5): the icon's alpha halved

        qdel(assistant);

        return assistant_icon;
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:299
    public override async DmTask<object> add_revolutionary(datum_mind rev_mind = null, float? stun = null, float? mute = null)
    {
        object dot = await base.add_revolutionary(rev_mind, stun, mute);
        if (!Dm.truthy(dot) || !Dm.truthy(conversion_objective))
            return dot;

        _addtimer(Dm.@new<datum_callback>(this, nameof(report_conversion_objective), rev_mind, conversion_objective), 2 * SECONDS, TIMER_DELETE_ME, file: "code/modules/antagonists/revolution/revolution.dm", line: 309);
        return dot;
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:320
    public virtual void report_conversion_objective(datum_mind rev_mind = null, object set_conversion_objective = null)
    {
        if (QDELETED(rev_mind))
            return;

        if (rev_mind?.current?.client == null)
        {
            if (rev_mind?.current == null)
                return;
            // Try again until they get a client - presumably they're actively being polled for (due to a jobban or something).
            _addtimer(Dm.@new<datum_callback>(this, nameof(report_conversion_objective), rev_mind, set_conversion_objective), 3 * SECONDS, TIMER_DELETE_ME, file: "code/modules/antagonists/revolution/revolution.dm", line: 319);
            return;
        }

        string conversion_formatted = span_slightly_larger(span_hypnophrase(Dm.str(set_conversion_objective)));
        string hint_formatted = span_warning(I18n.T("This is a guideline instilled upon you from your (new) leader - while you are expected to follow it where possible, it is not a strict order. Further orders from your (new) leader(s) may supersede this."));

        to_chat(rev_mind.current, fieldset_block(span_warning(I18n.T("A directive echoes through your mind...")), Dm.text("[]<br>[]", conversion_formatted, hint_formatted), "boxed_message red_box"));
        rev_mind.current.log_message(Dm.text("has been given a conversion guideline from []: []", key_name(owner.current), set_conversion_objective), color: COLOR_CULT_RED);
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:339
    public virtual async DmTask demote()
    {
        datum_mind old_owner = owner;
        datum_team old_team = rev_team;
        silent = TRUE;
        await owner.remove_antag_datum(typeof(datum_antagonist_rev_head));
        var new_rev = Dm.@new<datum_antagonist_rev>();
        new_rev.silent = TRUE;
        await old_owner.add_antag_datum(new_rev, old_team);
        new_rev.silent = FALSE;
        to_chat(old_owner, span_userdanger(I18n.T("Revolution has been disappointed of your leader traits! You are a regular revolutionary now!")));
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:361
    public override async DmTask farewell()
    {
        if (Dm.eq(deconversion_source, DECONVERTER_STATION_WIN) || owner.current == null)
            return;
        owner.current.balloon_alert_to_viewers(I18n.T("deconverted!"));
        if (ishuman(owner.current))
        {
            if (owner.current.stat != DEAD)
            {
                owner.current.visible_message(span_deconversion_message(I18n.T("[] looks like [] just remembered [] real allegiance!", owner.current, owner.current.p_theyve(), owner.current.p_their())), null, null, null, owner.current);
                to_chat(owner, Dm.text("<span class='deconversion_message bold'>[]</span>", I18n.T("You have given up your cause of overthrowing the command staff. You are no longer a Head Revolutionary.")));
            }
            else
                to_chat(owner, Dm.text("<span class='deconversion_message bold'>[]</span>", I18n.T("The sweet release of death. You are no longer a Head Revolutionary.")));
        }
        else if (issilicon(owner.current))
        {
            owner.current.visible_message(span_deconversion_message(I18n.T("The frame beeps contentedly, suppressing the disloyal personality traits from the MMI before initializing it.")), null, null, null, owner.current);
            to_chat(owner, span_userdanger(I18n.T("The frame's firmware detects and suppresses your unwanted personality traits! You feel more content with the leadership around these parts.")));
        }
        await DmTask.CompletedTask;
    }

    /// This is for revheads, for which they ordinarily shouldn't be deconverted outside of revs losing. As an exception, forceborging can de-headrev them.
    // tg: code/modules/antagonists/revolution/revolution.dm:387
    public override async DmTask remove_revolutionary(object deconverter = null)
    {
        // If they're living and the station won, turn them into an exiled headrev.
        if (owner.current.stat != DEAD && Dm.eq(deconverter, DECONVERTER_STATION_WIN))
            await owner.add_antag_datum(typeof(datum_antagonist_enemy_of_the_state));

        // Only actually remove headrev status on borging or when the station wins.
        if (Dm.eq(deconverter, DECONVERTER_BORGED) || Dm.eq(deconverter, DECONVERTER_STATION_WIN))
            await base.remove_revolutionary(deconverter);
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:396
    public override async DmTask equip_rev()
    {
        var carbon_owner = owner.current as mob_living_carbon;
        if (!ishuman(carbon_owner))
            return;

        if (Dm.truthy(give_flash))
        {
            object where = await ((mob_living_carbon_human)carbon_owner).equip_conspicuous_item(Dm.@new<obj_item_assembly_flash_handheld>());
            if (Dm.truthy(where))
                to_chat(carbon_owner, I18n.T("The flash in your [] will help you to persuade the crew to join your cause.", where));
            else
                to_chat(carbon_owner, I18n.T("The Syndicate were unfortunately unable to get you a flash."));
        }

        if (Dm.truthy(give_hud))
        {
            var hud = Dm.@new<obj_item_organ_cyberimp_eyes_hud_security_syndicate>();
            await hud.Insert(carbon_owner);
            if (carbon_owner.get_quirk(typeof(datum_quirk_body_purist)) != null)
                to_chat(carbon_owner, I18n.T("Being a body purist, you would never accept cybernetic implants. Upon hearing this, your employers signed you up for a special program, which... for some odd reason, you just can't remember... either way, the program must have worked, because you have gained the ability to keep track of who is mindshield-implanted, and therefore unable to be recruited."));
            else
                to_chat(carbon_owner, I18n.T("Your eyes have been implanted with a cybernetic security HUD which will help you keep track of who is mindshield-implanted, and therefore unable to be recruited."));
        }
    }
}

// tg: code/modules/antagonists/revolution/revolution.dm:418
public partial class datum_team_revolution : datum_team
{
    // tg: code/modules/antagonists/revolution/revolution.dm:437
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        head_chose_phrase_raw = generate_code_phrase(return_list: TRUE) as DmList;
        head_code_phrases = Dm.regex(Dm.text("([])", jointext(head_chose_phrase_raw, "|")), "ig");

        head_chose_phrase_raw = generate_code_phrase(return_list: TRUE) as DmList;
        head_code_responses = Dm.regex(Dm.text("([])", jointext(head_chose_phrase_raw, "|")), "ig");
    }

    /// Saves all current headrevs and revs
    // tg: code/modules/antagonists/revolution/revolution.dm:446
    public virtual void save_members()
    {
        ex_headrevs = get_head_revolutionaries() as DmList;
        ex_revs = Dm.sub(members, ex_headrevs) as DmList;
    }

    /// Returns a list of all headrevs.
    // tg: code/modules/antagonists/revolution/revolution.dm:451
    public virtual object get_head_revolutionaries()
    {
        DmList headrev_list = Dm.list();

        foreach (datum_mind revolutionary in Dm.each<datum_mind>(members))
        {
            if (Dm.truthy(revolutionary.has_antag_datum(typeof(datum_antagonist_rev_head))))
                Dm.append(ref headrev_list, revolutionary);
        }

        return headrev_list;
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:460
    public virtual object headrev_cap()
    {
        var heads = SSjob.get_all_heads() as DmList;
        var sec = SSjob.get_all_sec() as DmList;

        return clamp(round(Dm.length(heads) - ((8 - Dm.length(sec)) / 3f)), 1, max_headrevs);
    }

    /// Tries to make sure an appropriate number of headrevs are part of the revolution.
    /// Will promote up revs to headrevs as necessary based on the hard max_headrevs cap and the soft cap based on the number of heads of staff and sec.
    // tg: code/modules/antagonists/revolution/revolution.dm:468
    public virtual async DmTask update_rev_heads()
    {
        var head_revolutionaries = get_head_revolutionaries() as DmList;

        if (Dm.length(head_revolutionaries) >= Dm.num(headrev_cap()))
            return;

        DmList promotable = Dm.list();
        DmList monkey_promotable = Dm.list();
        foreach (datum_mind khrushchev in Dm.each_cast<datum_mind>(Dm.sub(members, head_revolutionaries)))
        {
            if (!Dm.truthy(can_be_headrev(khrushchev)))
                continue;
            if (HAS_TRAIT(khrushchev.current, TRAIT_LESSER_HUMANOID))
                Dm.append(ref monkey_promotable, khrushchev);
            else
                Dm.append(ref promotable, khrushchev);
        }
        if (Dm.length(promotable) == 0 && Dm.length(monkey_promotable) > 0)
            promotable = monkey_promotable;
        if (Dm.length(promotable) == 0)
            return;
        var new_leader = (datum_mind)pick(promotable);
        var rev = new_leader.has_antag_datum(typeof(datum_antagonist_rev)) as datum_antagonist_rev;
        await rev.promote();
    }

    /// Mutates the ticker to report that the revs have won
    // tg: code/modules/antagonists/revolution/revolution.dm:492
    public virtual void round_result(object finished = null)
    {
        if (Dm.eq(finished, REVOLUTION_VICTORY))
        {
            SSticker.mode_result = "win - heads killed";
            SSticker.news_report = REVS_WIN;
        }
        else if (Dm.eq(finished, STATION_VICTORY))
        {
            SSticker.mode_result = "loss - rev heads killed";
            SSticker.news_report = REVS_LOSE;
        }
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:500
    public override object roundend_report()
    {
        if (!Dm.truthy(members.len) && !Dm.truthy(ex_headrevs.len))
            return null;

        DmList result = Dm.list();

        Dm.append(ref result, "<div class='panel redborder'>");

        DmList targets = Dm.list();
        DmList headrevs;
        DmList revs;
        if (Dm.truthy(ex_headrevs.len))
            headrevs = ex_headrevs;
        else
            headrevs = get_antag_minds(typeof(datum_antagonist_rev_head), TRUE) as DmList;

        if (Dm.truthy(ex_revs.len))
            revs = ex_revs;
        else
            revs = get_antag_minds(typeof(datum_antagonist_rev), TRUE) as DmList;

        float num_revs = 0;
        float num_survivors = 0;
        foreach (mob_living_carbon survivor in Dm.each<mob_living_carbon>(GLOB.alive_mob_list))
        {
            if (Dm.truthy(survivor.ckey))
            {
                num_survivors += 1;
                if (Dm.@in(survivor.mind, revs) || Dm.@in(survivor.mind, headrevs))
                    num_revs += 1;
            }
        }

        if (num_survivors > 0)
            Dm.append(ref result, I18n.T("Command's Approval Rating: <B>[]%</B><br>", 100 - Dm.num(round((num_revs / num_survivors) * 100, 0.1f))));

        if (Dm.truthy(headrevs.len))
        {
            DmList headrev_part = Dm.list();
            Dm.append(ref headrev_part, span_header(I18n.T("The head revolutionaries were:")));
            Dm.append(ref headrev_part, printplayerlist(headrevs, !Dm.eq(GLOB.revolution_handler.result, REVOLUTION_VICTORY) ? TRUE : FALSE));
            Dm.append(ref result, headrev_part.Join("<br>"));
        }

        if (Dm.truthy(revs.len))
        {
            DmList rev_part = Dm.list();
            Dm.append(ref rev_part, span_header(I18n.T("The revolutionaries were:")));
            Dm.append(ref rev_part, printplayerlist(revs, !Dm.eq(GLOB.revolution_handler.result, REVOLUTION_VICTORY) ? TRUE : FALSE));
            Dm.append(ref result, rev_part.Join("<br>"));
        }

        var heads = SSjob.get_all_heads() as DmList;
        if (Dm.truthy(heads.len))
        {
            string head_text = span_header(I18n.T("The heads of staff were:"));
            head_text += "<ul class='playerlist'>";
            foreach (datum_mind head in Dm.each<datum_mind>(heads))
            {
                bool target = Dm.@in(head, targets);
                head_text += "<li>";
                if (target)
                    head_text += span_redtext(I18n.T("Target"));
                head_text += Dm.text("[]</li>", printplayer(head, 1));
            }
            head_text += "</ul><br>";
            Dm.append(ref result, head_text);
        }

        Dm.append(ref result, "</div>");

        return result.Join("");
    }

    // tg: code/modules/antagonists/revolution/revolution.dm:561
    public override object antag_listing_entry()
    {
        string common_part = "";
        DmList parts = Dm.list();
        Dm.append(ref parts, Dm.text("<b>[]</b><br>", antag_listing_name()));
        Dm.append(ref parts, "<table cellspacing=5>");

        var heads = get_team_antags(typeof(datum_antagonist_rev_head), FALSE) as DmList;

        foreach (datum_antagonist A in Dm.each<datum_antagonist>(Dm.bitor(heads, get_team_antags())))
            Dm.append(ref parts, A.antag_listing_entry());

        Dm.append(ref parts, "</table>");
        common_part = parts.Join("");

        string heads_report = "<b>Heads of Staff</b><br>";
        heads_report += "<table cellspacing=5>";
        foreach (datum_mind N in Dm.each_cast<datum_mind>(SSjob.get_living_heads()))
        {
            mob M = N.current;
            if (M != null)
            {
                heads_report += Dm.text("<tr><td><a href='byond://?_src_=holder;[];adminplayeropts=[]'>[]</a>[][]</td>", HrefToken(), REF(M), M.real_name, M.client != null ? "" : " <i>(No Client)</i>", M.stat == DEAD ? " <b><font color=red>(DEAD)</font></b>" : "");
                heads_report += Dm.text("<td><A href='byond://?priv_msg=[]'>PM</A></td>", M.ckey);
                heads_report += Dm.text("<td><A href='byond://?_src_=holder;[];adminplayerobservefollow=[]'>FLW</a></td>", HrefToken(), REF(M));
                turf mob_loc = get_turf(M);
                heads_report += Dm.text("<td>[]</td></tr>", mob_loc.loc);
            }
            else
            {
                heads_report += Dm.text("<tr><td><a href='byond://?_src_=vars;[];Vars=[]'>[]([])</a><i>Head body destroyed!</i></td>", HrefToken(), REF(N), N.name, N.key);
                heads_report += Dm.text("<td><A href='byond://?priv_msg=[]'>PM</A></td></tr>", N.key);
            }
        }
        heads_report += "</table>";
        return common_part + heads_report;
    }
}
