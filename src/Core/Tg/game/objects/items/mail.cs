// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/mail.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the junk mail letter (gaps S3, D298); the mail, the envelopes, the traitor's mail, the mail crates and the GLA-2 mail
// counterfeit device (syndicate G4a, D316); the economy's mail crate (gaps P4b). The red pill letter reads Meta's self-destruct terminal (syndicate N1a). The var block is generated (D111).

/// Mail is tamper-evident and unresealable, postmarked by CentCom for an individual recepient.
// tg: code/game/objects/items/mail.dm:2
public partial class obj_item_mail : obj_item
{
    ///mail will have the color of the department the recipient is in.
    // tg: code/game/objects/items/mail.dm:45  var/static/list/department_colors
    public static DmList department_colors
    {
        get => TgState.Current.obj_item_mail__department_colors;
        set => TgState.Current.obj_item_mail__department_colors = value;
    }

    // tg: code/game/objects/items/mail.dm:54
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_MOVABLE_DISPOSING, nameof(disposal_handling));
        Waiting.NoSleep(AddElement(typeof(datum_element_item_scaling), 0.75f, 1));
        if (department_colors == null)
        {
            department_colors = Dm.list(
                Dm.kv(ACCOUNT_CIV, COLOR_WHITE),
                Dm.kv(ACCOUNT_ENG, COLOR_PALE_ORANGE),
                Dm.kv(ACCOUNT_SCI, COLOR_PALE_PURPLE_GRAY),
                Dm.kv(ACCOUNT_MED, COLOR_PALE_BLUE_GRAY),
                Dm.kv(ACCOUNT_SRV, COLOR_PALE_GREEN_GRAY),
                Dm.kv(ACCOUNT_CAR, COLOR_BEIGE),
                Dm.kv(ACCOUNT_SEC, COLOR_PALE_RED_GRAY));
        }

        // Icons
        // Add some random stamps.
        if (stamped == TRUE)
        {
            float stamp_count = rand(1, stamp_max);
            for (float i = 1; i <= stamp_count; i++)
                stamps.Add(Dm.list(Dm.text("stamp_[]", rand(2, 6))));
        }
        Waiting.NoSleep(update_icon());
        return dot;
    }

    // tg: code/game/objects/items/mail.dm:77
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        float bonus_stamp_offset = 0;
        foreach (object stamp in Dm.each(stamps))
        {
            image stamp_image = Dm.image(icon: icon, icon_state: stamp);
            stamp_image.pixel_w = pixel_w = stamp_offset_x;
            stamp_image.pixel_z = stamp_offset_y + bonus_stamp_offset;
            stamp_image.appearance_flags = Dm.bitor(stamp_image.appearance_flags, Dm.bitor(RESET_COLOR, KEEP_APART));
            bonus_stamp_offset -= 5;
            Dm.append(ref dot, stamp_image);
        }

        if (postmarked == TRUE)
        {
            image postmark_image = Dm.image(icon: icon, icon_state: "postmark");
            postmark_image.pixel_w = stamp_offset_x + rand(-3, 1);
            postmark_image.pixel_z = stamp_offset_y + rand(bonus_stamp_offset + 3, 1);
            postmark_image.appearance_flags = Dm.bitor(postmark_image.appearance_flags, Dm.bitor(RESET_COLOR, KEEP_APART));
            Dm.append(ref dot, postmark_image);
        }
        return dot;
    }

    // tg: code/game/objects/items/mail.dm:101
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        // Destination tagging
        if (tool is not obj_item_dest_tagger destination_tag)
            return NONE;

        if (Dm.eq(sort_tag, destination_tag.currTag))
            return ITEM_INTERACT_BLOCKING;
        object tag = uppertext(GLOB.TAGGERLOCATIONS[destination_tag.currTag]);
        to_chat(user, span_notice(Dm.text("*[]*", tag)));
        sort_tag = Dm.num(destination_tag.currTag);
        playsound(loc, Dm.resource("sound/machines/beep/twobeep_high.ogg"), vol: 100, vary: TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/mail.dm:115
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (user.get_inactive_held_item() == this)
        {
            balloon_alert(user, I18n.T("nothing to disable!"));
            return TRUE;
        }
        balloon_alert(user, I18n.T("hold it!"));
        return FALSE;
    }

    // tg: code/game/objects/items/mail.dm:123
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await unwrap(user)))
            return FALSE;
        return await after_unwrap(user);
    }

    /// Proc for attempting to open a letter. Returns FALSE if it fails.
    // tg: code/game/objects/items/mail.dm:129
    public virtual async DmTask<object> unwrap(mob user = null)
    {
        if (recipient_ref != null)
        {
            datum_mind recipient = recipient_ref.resolve() as datum_mind;
            // If the recipient's mind has gone, then anyone can open their mail
            // whether a mind can actually be qdel'd is an exercise for the reader
            if (recipient != null && recipient != user?.mind)
            {
                to_chat(user, span_notice(I18n.T("You can't open somebody else's mail! That's <em>illegal</em>!")));
                return FALSE;
            }
        }

        balloon_alert(user, I18n.T("unwrapping..."));
        if (!Dm.truthy(await do_after(user, 1.5f * SECONDS, target: user)))
            return FALSE;
        return TRUE;
    }

    /// Handles additional effects after a letter has successfully been opened, like moving contents to the user, and playing sound effects.
    // tg: code/game/objects/items/mail.dm:144
    public virtual async DmTask<object> after_unwrap(mob user = null)
    {
        await user.temporarilyRemoveItemFromInventory(this, force: TRUE);
        foreach (obj stuff in Dm.each_cast<obj>(contents)) // Mail and envelope actually can have more than 1 item.
        {
            if (isitem(stuff))
                await user.put_in_hands(stuff as obj_item);
            else
                await stuff.forceMove(drop_location() as atom);
        }
        playsound(loc, Dm.resource("sound/items/poster/poster_ripped.ogg"), vol: 50, vary: TRUE);
        if (recipient_ref != null) // We only want to care about mail with an owner.
            SSeconomy.mail_opened += 1;
        qdel(this);
        return TRUE;
    }

    // tg: code/game/objects/items/mail.dm:158
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        if (!Dm.truthy(postmarked))
            dot.Add(span_info(I18n.T("This mail has no postmarking of any sort...")));
        else
            dot.Add(span_notice(I18n.T("<i>You notice the postmarking on the front of the mail...</i>")));
        datum_mind recipient = recipient_ref?.resolve() as datum_mind;
        if (recipient != null)
            dot.Add(span_info(I18n.T("[] mail for [].", Dm.truthy(postmarked) ? "Certified NT" : "Uncertfieid", recipient)));
        else if (Dm.truthy(postmarked))
            dot.Add(span_info(I18n.T("Certified mail for [].", GLOB.station_name)));
        else
            dot.Add(span_info(I18n.T("This is a dead letter mail with no recipient.")));
        dot.Add(span_info(I18n.T("Distribute by hand or via destination tagger using the certified NT disposal system.")));
        return dot;
    }

    /// Accepts a mind to initialize goodies for a piece of mail.
    // tg: code/game/objects/items/mail.dm:174
    public virtual object initialize_for_recipient(datum_mind recipient = null)
    {
        name = Dm.text("[] for [] ([])", Dm.initial(this, (obj_item_mail p) => p.name), recipient.name, recipient.assigned_role.title);
        recipient_ref = WEAKREF(recipient) as datum_weakref;

        mob_living body = recipient.current as mob_living;
        DmList goodies = generic_goodies;

        datum_job this_job = recipient.assigned_role;
        float is_mail_restricted = FALSE; // certain roles and jobs (prisoner) do not receive generic gifts

        if (this_job != null)
        {
            if (Dm.truthy(this_job.paycheck_department) && Dm.truthy(department_colors[this_job.paycheck_department]))
                color = department_colors[this_job.paycheck_department];

            DmList job_goodies = this_job.get_mail_goodies() as DmList;
            is_mail_restricted = this_job.exclusive_mail_goodies;
            if (Dm.truthy(LAZYLEN(job_goodies)))
            {
                if (Dm.truthy(is_mail_restricted))
                    goodies = job_goodies;
                else
                    goodies.Add(job_goodies);
            }
        }

        if (!Dm.truthy(is_mail_restricted))
        {
            // the weighted list is 50 (generic items) + 50 (job items)
            // every quirk adds 5 to the final weighted list (regardless the number of items or weights in the quirk list)
            // 5% is not too high or low so that stacking multiple quirks doesn't tilt the weighted list too much
            foreach (datum_quirk quirk in Dm.each_cast<datum_quirk>(body.quirks))
            {
                if (Dm.truthy(LAZYLEN(quirk.mail_goodies)))
                {
                    object quirk_goodie = pick(quirk.mail_goodies);
                    goodies[quirk_goodie] = 5;
                }
            }

            if (Dm.truthy(LAZYLEN(GLOB.holiday_mail))) // gaps R3
            {
                object holiday_goodie = pick(GLOB.holiday_mail);
                goodies[holiday_goodie] = 5;
            }
        }

        for (float iterator = 1; iterator <= goodie_count; iterator++)
        {
            object target_good = pick_weight(goodies);
            atom_movable target_atom = Dm.@new((System.Type)target_good, this) as atom_movable;
            body.log_message(Dm.text("received [] in the mail ([])", target_atom.name, target_good), LOG_GAME);
        }

        return TRUE;
    }

    /// Alternate setup, just complete garbage inside and anyone can open
    // tg: code/game/objects/items/mail.dm:217
    public virtual object junk_mail()
    {
        object junk = typeof(obj_item_paper_fluff_junkmail_generic);
        float special_name = FALSE;

        if (prob(25))
        {
            special_name = TRUE;
            junk = pick(Dm.list(
                typeof(obj_item_paper_pamphlet_gateway),
                typeof(obj_item_paper_pamphlet_violent_video_games),
                typeof(obj_item_paper_fluff_junkmail_redpill),
                typeof(obj_effect_decal_cleanable_ash)));
        }

        string initial_name = Dm.initial(this, (obj_item_mail p) => p.name);
        DmList junk_names = Dm.list(
            Dm.kv(typeof(obj_item_paper_pamphlet_gateway), Dm.text("[] for [] adventurers", initial_name, pick(GLOB.adjectives))),
            Dm.kv(typeof(obj_item_paper_pamphlet_violent_video_games), Dm.text("[] for the truth about the arcade centcom doesn't want to hear", initial_name)),
            Dm.kv(typeof(obj_item_paper_fluff_junkmail_redpill), Dm.text("[] for those feeling [] working at Nanotrasen", initial_name, pick(GLOB.adjectives))),
            Dm.kv(typeof(obj_effect_decal_cleanable_ash), Dm.text("[] with INCREDIBLY IMPORTANT ARTIFACT- DELIVER TO SCIENCE DIVISION. HANDLE WITH CARE.", initial_name)));

        color = pick(department_colors); //eh, who gives a shit.
        name = Dm.truthy(special_name) ? Dm.str(junk_names[junk]) : Dm.text("important []", initial_name);

        junk = Dm.@new((System.Type)junk, this);
        return TRUE;
    }

    // tg: code/game/objects/items/mail.dm:244
    [SignalHandler]
    public virtual void disposal_handling(object disposal_source = null, obj_structure_disposalholder disposal_holder = null, obj_machinery_disposal disposal_machine = null, object hasmob = null)
    {
        if (!Dm.truthy(hasmob))
            disposal_holder.destinationTag = sort_tag;
    }
}

/// Subtype that's always junkmail
// tg: code/game/objects/items/mail.dm:250
public partial class obj_item_mail_junkmail : obj_item_mail
{
    // tg: code/game/objects/items/mail.dm:250
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        junk_mail();
        return dot;
    }
}

/// Crate for mail from CentCom.
// tg: code/game/objects/items/mail.dm:255
public partial class obj_structure_closet_crate_mail : obj_structure_closet_crate
{
    // tg: code/game/objects/items/mail.dm:270
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(opened))
        {
            icon_state = Dm.text("[]open", base_icon_state);
            if (Dm.locate<obj_item_mail>(this) != null)
                icon_state = Dm.str(base_icon_state);
        }
        else
            icon_state = Dm.text("[]sealed", base_icon_state);
        return dot;
    }

    // tg: code/game/objects/items/mail.dm:279
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(postmarked))
            Dm.append(ref dot, "mail_nt");
        return dot;
    }

    /// Fills this mail crate with N pieces of mail, where N is the lower of the amount var passed, and the maximum capacity of this crate. If N is larger than the number of alive human players, the excess will be junkmail.
    // tg: code/game/objects/items/mail.dm:285
    public virtual object populate(object amount = null)
    {
        float mail_count = min(Dm.num(amount), storage_capacity);
        // Fills the
        DmList mail_recipients = Dm.list();

        foreach (mob_living_carbon_human human in Dm.each<mob_living_carbon_human>(GLOB.player_list))
        {
            if (human.stat == DEAD || human.mind == null)
                continue;
            // Skip wizards, nuke ops, cyborgs; Centcom does not send them mail
            if (!Dm.truthy(Dm.bitand(human.mind.assigned_role.job_flags, JOB_CREW_MEMBER)))
                continue;

            mail_recipients.Add(human.mind);
        }

        for (float i = 1; i <= mail_count; i++)
        {
            obj_item_mail new_mail;
            if (prob(FULL_CRATE_LETTER_ODDS))
                new_mail = Dm.@new<obj_item_mail>(this);
            else
                new_mail = Dm.@new<obj_item_mail_envelope>(this);

            datum_mind recipient = pick_n_take(mail_recipients) as datum_mind;
            if (recipient != null)
                new_mail.initialize_for_recipient(recipient);
            else
                new_mail.junk_mail();
        }

        Waiting.NoSleep(update_icon());
        return null;
    }
}

/// Crate for mail that automatically depletes the economy subsystem's pending mail counter.
// tg: code/game/objects/items/mail.dm:314  (gaps P4b)
public partial class obj_structure_closet_crate_mail_economy : obj_structure_closet_crate_mail
{
    // tg: code/game/objects/items/mail.dm:315
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        populate(SSeconomy?.mail_waiting ?? 0); // SSeconomy?: a harness world without the Master (rules 10.6)
        if (SSeconomy != null)
            SSeconomy.mail_waiting = 0;
        return dot;
    }
}

/// Crate for mail that automatically generates a lot of mail. Usually only normal mail, but on lowpop it may end up just being junk.
// tg: code/game/objects/items/mail.dm:321
public partial class obj_structure_closet_crate_mail_full : obj_structure_closet_crate_mail
{
    // tg: code/game/objects/items/mail.dm:325
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        populate(INFINITY);
        return dot;
    }
}

///Used in the mail strike shuttle loan event
// tg: code/game/objects/items/mail.dm:330
public partial class obj_structure_closet_crate_mail_full_mail_strike : obj_structure_closet_crate_mail_full
{
    // tg: code/game/objects/items/mail.dm:334
    public override object populate(object amount = null)
    {
        float strike_mail_to_spawn = rand(1, storage_capacity - 1);
        for (float i = 1; i <= strike_mail_to_spawn; i++)
        {
            if (prob(95))
                Dm.@new<obj_item_mail_mail_strike>(this);
            else
                Dm.@new<obj_item_mail_traitor_mail_strike>(this);
        }
        return base.populate(storage_capacity - strike_mail_to_spawn);
    }
}

// tg: code/game/objects/items/mail.dm:359
public partial class obj_item_paper_fluff_junkmail_redpill : obj_item_paper_fluff
{
    // tg: code/game/objects/items/mail.dm:365
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_machinery_nuclearbomb_selfdestruct self_destruct = Dm.locate<obj_machinery_nuclearbomb_selfdestruct>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_nuclearbomb_selfdestruct)));
        if (self_destruct == null || !prob(nuclear_option_odds)) // 1 in 1000 chance of getting 2 random nuke code characters.
        {
            add_raw_text(Dm.text("<i>You need to escape the simulation. Don't forget the numbers, they help you remember:</i> '[][][]...'", rand(0, 9), rand(0, 9), rand(0, 9)));
            return base.Initialize(mapload, __extra);
        }

        if (Dm.eq(self_destruct.r_code, NUKE_CODE_UNSET))
        {
            self_destruct.r_code = random_nukecode();
            message_admins(Dm.text("Through junkmail, the self-destruct code was set to \"[]\".", self_destruct.r_code));
        }
        float first = rand(1, 5);
        float second = rand(1, 5);
        add_raw_text(Dm.text("<i>You need to escape the simulation. Don't forget the numbers, they help you remember:</i> '[][]...'", copytext(self_destruct.r_code, first, first + 1), copytext(self_destruct.r_code, second, second + 1)));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/items/mail.dm:380
public partial class obj_item_paper_fluff_junkmail_generic : obj_item_paper_fluff
{
    // tg: code/game/objects/items/mail.dm:384
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        default_raw_text = pick(GLOB.junkmail_messages);
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/items/mail.dm:389
public partial class obj_item_mail_traitor : obj_item_mail
{
    // tg: code/game/objects/items/mail.dm:404
    public override async DmTask<object> after_unwrap(mob user = null)
    {
        await user.temporarilyRemoveItemFromInventory(this, force: TRUE);
        playsound(loc, Dm.resource("sound/items/poster/poster_ripped.ogg"), vol: 50, vary: TRUE);
        foreach (obj_item stuff in Dm.each_cast<obj_item>(contents)) // Mail and envelope actually can have more than 1 item.
        {
            if (!Dm.truthy(armed))
                continue;
            string whomst = Dm.truthy(made_by_cached_name) ? Dm.text("[] ([])", made_by_cached_name, made_by_cached_ckey) : "no one in particular";
            log_bomber(user, Dm.text("opened armed mail made by [], activating", whomst), stuff);

            if (Dm.truthy(Dm.bitand(stuff.SEND_SIGNAL(COMSIG_ITEM_IN_UNWRAPPED_TRAITOR_MAIL, Dm.list(stuff, user, this)), COMPONENT_TRAITOR_MAIL_HANDLED)))
                continue;

            if (!Dm.truthy(await user.put_in_hands(stuff)))
                continue;

            Waiting.Detach(stuff.attack_self(user)); // INVOKE_ASYNC
        }
        qdel(this);
        return TRUE;
    }

    // tg: code/game/objects/items/mail.dm:423
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (armed == FALSE || user.get_inactive_held_item() != this)
            return await base.multitool_act(user, tool);
        if (IS_WEAKREF_OF(user.mind, made_by_ref))
        {
            balloon_alert(user, I18n.T("disarming trap..."));
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
                return FALSE;
            balloon_alert(user, I18n.T("disarmed"));
            playsound(this, Dm.resource("sound/machines/defib/defib_ready.ogg"), vol: 100, vary: TRUE);
            armed = FALSE;
            return TRUE;
        }
        balloon_alert(user, I18n.T("tinkering with something..."));

        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
        {
            await after_unwrap(user);
            return FALSE;
        }
        if (prob(50))
        {
            balloon_alert(user, I18n.T("disarmed something...?"));
            playsound(this, Dm.resource("sound/machines/defib/defib_ready.ogg"), vol: 100, vary: TRUE);
            armed = FALSE;
            return TRUE;
        }
        await after_unwrap(user);
        return TRUE;
    }
}

///Generic mail used in the mail strike shuttle loan event
// tg: code/game/objects/items/mail.dm:448
public partial class obj_item_mail_mail_strike : obj_item_mail
{
    // tg: code/game/objects/items/mail.dm:462
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (prob(35))
            stamped = FALSE;
        if (prob(35))
        {
            name = "dead envelope";
            icon_state = "mail_large";
            goodie_count = 2;
            stamp_max = 2;
            stamp_offset_y = 5;
        }
        object dot = base.Initialize(mapload, __extra);
        color = pick(COLOR_SILVER, COLOR_DARK, COLOR_DRIED_TAN, COLOR_ORANGE_BROWN, COLOR_BROWN, COLOR_SYNDIE_RED);
        for (float goodie = 1; goodie <= goodie_count; goodie++)
        {
            object target_good = pick_weight(generic_goodies);
            Dm.@new((System.Type)target_good, this);
        }
        return dot;
    }
}

///Also found in the mail strike shuttle loan. It contains a random grenade that'll be triggered when unwrapped
// tg: code/game/objects/items/mail.dm:478
public partial class obj_item_mail_traitor_mail_strike : obj_item_mail_traitor
{
    // tg: code/game/objects/items/mail.dm:483
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (prob(35))
            stamped = FALSE;
        if (prob(35))
        {
            name = "dead envelope";
            icon_state = "mail_large";
            goodie_count = 2;
            stamp_max = 2;
            stamp_offset_y = 5;
        }
        object dot = base.Initialize(mapload, __extra);
        color = pick(COLOR_SILVER, COLOR_DARK, COLOR_DRIED_TAN, COLOR_ORANGE_BROWN, COLOR_BROWN, COLOR_SYNDIE_RED);
        Dm.@new<obj_effect_spawner_random_contraband_grenades_dangerous>(this);
        return dot;
    }
}

// tg: code/game/objects/items/mail.dm:496
public partial class obj_item_storage_mail_counterfeit_device : obj_item_storage
{
    // tg: code/game/objects/items/mail.dm:504
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        dot.Add(span_notice(I18n.T("<i>You notice the manufacturer information on the side of the device...</i>")));
        dot.Add(Dm.text("\t[]", span_info(I18n.T("Guerilla Letter Assembler"))));
        dot.Add(Dm.text("\t[]", span_info(I18n.T("GLA Postal Service, right on schedule."))));
        return dot;
    }

    // tg: code/game/objects/items/mail.dm:511
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object mail_type = await tgui_alert(user, I18n.T("Make it look like an envelope or like normal mail?"), I18n.T("Mail Counterfeiting"), Dm.list("Mail", "Envelope"));
        if (mail_type == null)
            return FALSE;
        if (loc != user)
            return FALSE;
        mail_type = LOWER_TEXT(mail_type);

        object mail_armed = Dm.eq(await tgui_alert(user, I18n.T("Arm it?"), I18n.T("Mail Counterfeiting"), Dm.list("Yes", "No")), "Yes") ? TRUE : FALSE;
        if (mail_armed == null)
            return FALSE;
        if (loc != user)
            return FALSE;

        DmList mail_recipients = Dm.list("Anyone");
        DmList mail_recipients_for_input = Dm.list("Anyone");
        DmList used_names = Dm.list();
        foreach (datum_record_locked person in Dm.each_cast<datum_record_locked>(sort_record(GLOB.manifest.locked)))
        {
            datum_mind locked_mind = person.mind_ref?.resolve() as datum_mind;
            if (locked_mind == null)
                continue;
            mail_recipients.Add(locked_mind);
            mail_recipients_for_input.Add(avoid_assoc_duplicate_keys(person.name, used_names));
        }

        object recipient = await tgui_input_list(user, I18n.T("Choose a recipient"), I18n.T("Mail Counterfeiting"), mail_recipients_for_input);
        if (recipient == null)
            return FALSE;
        if (!Dm.@in(this, user.contents))
            return FALSE;

        float index = mail_recipients_for_input.Find(recipient);

        obj_item_mail_traitor shady_mail;
        if (Dm.eq(mail_type, "mail"))
            shady_mail = Dm.@new<obj_item_mail_traitor>();
        else
            shady_mail = Dm.@new<obj_item_mail_traitor_envelope>();

        shady_mail.made_by_cached_ckey = user.ckey;
        shady_mail.made_by_cached_name = user.mind.name;

        if (index == 1)
        {
            object mail_name = await tgui_input_text(user, I18n.T("Enter mail title, or leave it blank"), I18n.T("Mail Counterfeiting"), max_length: MAX_LABEL_LEN);
            if (!Dm.@in(this, user.contents))
                return FALSE;
            if (Dm.truthy(reject_bad_text(mail_name, max_length: MAX_LABEL_LEN, ascii_only: FALSE)))
                shady_mail.name = Dm.str(mail_name);
            else
                shady_mail.name = Dm.str(mail_type);
        }
        else
            shady_mail.initialize_for_recipient(mail_recipients[index] as datum_mind);

        atom_storage.hide_contents(user);
        await user.temporarilyRemoveItemFromInventory(this, force: TRUE);
        Dm.append(shady_mail.contents, contents);
        shady_mail.armed = Dm.num(mail_armed);
        shady_mail.made_by_ref = WEAKREF(user.mind) as datum_weakref;
        await user.put_in_hands(shady_mail);
        qdel(this);
        return null;
    }
}

/// Unobtainable item mostly for (b)admin purposes.
// tg: code/game/objects/items/mail.dm:572
public partial class obj_item_storage_mail_counterfeit_device_advanced : obj_item_storage_mail_counterfeit_device
{
    // tg: code/game/objects/items/mail.dm:576
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        desc += " This model is highly advanced and capable of compressing items, making mail's storage space comparable to standard backpack.";
        return dot;
    }
}

/// Unobtainable item mostly for (b)admin purposes.
// tg: code/game/objects/items/mail.dm:581
public partial class obj_item_storage_mail_counterfeit_device_bluespace : obj_item_storage_mail_counterfeit_device
{
    // tg: code/game/objects/items/mail.dm:585
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        desc += " This model is the most advanced and capable of performing crazy bluespace compressions, making mail's storage space comparable to bluespace backpack.";
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/mail.dm:45  var/static/list/department_colors
    internal DmList obj_item_mail__department_colors;
}
