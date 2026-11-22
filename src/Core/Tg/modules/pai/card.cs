// Translated from tgstation (snapshot 1d99bd7f): code/modules/pai/card.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6 (D326): the whole file. The alert's emissive glow is DV12's.

public partial class obj_item_pai_card : obj_item
{
    // tg: code/modules/pai/card.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        SSpai.pai_card_list.Add(this);
        ADD_TRAIT(this, TRAIT_CASTABLE_LOC, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/pai/card.dm:35
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (pai == null || tool is not obj_item_encryptionkey)
            return NONE;
        if (!Dm.truthy(pai.encrypt_mod))
        {
            to_chat(user, span_alert(I18n.T("Encryption Key ports not configured.")));
            return ITEM_INTERACT_BLOCKING;
        }
        return await ((obj_item_radio)pai.radio).install_key(user, (obj_item_encryptionkey)tool);
    }

    // tg: code/modules/pai/card.dm:45
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!in_range(this, user))
            return null;
        await ui_interact(user);
        return null;
    }

    // tg: code/modules/pai/card.dm:50
    public override object Destroy(float? force = null)
    {
        //Will stop people throwing friend pAIs into the singularity so they can respawn
        SSpai.pai_card_list.Remove(this);
        if (!QDELETED(pai))
            QDEL_NULL(ref pai);
        return base.Destroy(force);
    }

    // tg: code/modules/pai/card.dm:57
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (pai != null)
            return pai.handle_emag(user as mob_living_carbon);
        return FALSE;
    }

    // tg: code/modules/pai/card.dm:62
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (pai != null && !Dm.truthy(pai.holoform))
            await pai.emp_act(severity);
        return dot;
    }

    // tg: code/modules/pai/card.dm:69
    public virtual void on_pai_del(atom source = null)
    {
        if (QDELETED(this))
            return;
        pai = null;
        screen_image = typeof(datum_pai_screen_image_off); // initial(screen_image)
        update_appearance();
    }

    // tg: code/modules/pai/card.dm:77
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        object dot = await base.on_saboteur(source, disrupt_duration);
        if (pai != null)
            return await pai.on_saboteur(source, disrupt_duration);
        return dot;
    }

    // tg: code/modules/pai/card.dm:82
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is staring sadly at []! [] can't keep living without real human intimacy!", user, this, user.p_They())));
        await DmTask.CompletedTask;
        return OXYLOSS;
    }

    // tg: code/modules/pai/card.dm:86
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList ?? Dm.list();
        dot.Add(image(icon: Dm.initial(screen_image, (datum_pai_screen_image s) => s.icon), icon_state: Dm.initial(screen_image, (datum_pai_screen_image s) => s.icon_state)));
        if (pai?.hacking_cable != null)
            dot.Add(Dm.text("[]-connector", Dm.initial(this, (obj_item_pai_card c) => c.icon_state)));
        return dot;
    }

    // tg: code/modules/pai/card.dm:97
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "PaiCard");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/pai/card.dm:104
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (Dm.@in(user, get_nested_locs(this)))
            return UI_INTERACTIVE;
        return await base.ui_status(user, state);
    }

    // tg: code/modules/pai/card.dm:109
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = await base.ui_static_data(user) as DmList ?? Dm.list();
        dot["range_max"] = HOLOFORM_MAX_RANGE;
        dot["range_min"] = HOLOFORM_MIN_RANGE;
        return dot;
    }

    // tg: code/modules/pai/card.dm:114
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        if (pai == null)
        {
            data["candidates"] = pool_candidates() ?? Dm.list();
            return data;
        }
        data["pai"] = Dm.list(
            Dm.kv("can_holo", pai.can_holo),
            Dm.kv("dna", pai.master_dna),
            Dm.kv("emagged", pai.emagged),
            Dm.kv("laws", pai.laws.inherent),
            Dm.kv("master", pai.master_name),
            Dm.kv("name", pai.name),
            Dm.kv("transmit", pai.can_transmit),
            Dm.kv("receive", pai.can_receive),
            Dm.kv("leashed", pai.leash),
            Dm.kv("range", pai.leash?.distance));
        return data;
    }

    // tg: code/modules/pai/card.dm:134
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return TRUE;
        mob usr = World.Current.usr as mob;
        // Actions that don't require a pAI
        if (Dm.eq(action, "download"))
        {
            await download_candidate(usr, @params["ckey"]);
            return TRUE;
        }
        if (Dm.eq(action, "request"))
        {
            find_pai(usr);
            return TRUE;
        }
        // pAI specific actions.
        if (pai == null)
            return FALSE;
        switch (Dm.str(action))
        {
            case "fix_speech":
                pai.fix_speech();
                return TRUE;
            case "reset_software":
                pai.reset_software();
                return TRUE;
            case "set_dna":
                pai.set_dna(usr);
                return TRUE;
            case "set_laws":
                await pai.set_laws(usr);
                return TRUE;
            case "toggle_holo":
                pai.toggle_holo();
                return TRUE;
            case "toggle_leash":
                pai.toggle_leash();
                return TRUE;
            case "toggle_radio":
                pai.toggle_radio(@params["option"]);
                return TRUE;
            case "increase_range":
                pai.increment_range(1);
                return TRUE;
            case "decrease_range":
                pai.increment_range(-1);
                return TRUE;
            case "wipe_pai":
                await pai.wipe_pai(usr);
                ui.close();
                return TRUE;
        }
        return FALSE;
    }

    /** Flashes the pai card screen */
    // tg: code/modules/pai/card.dm:183
    public virtual void add_alert()
    {
        if (pai != null)
            return;
        string state = Dm.text("[]-alert", Dm.initial(this, (obj_item_pai_card c) => c.icon_state));
        // DEVIATION(DV12): emissive_appearance(icon, "[initial(icon_state)]-alert", src, alpha = src.alpha)
        add_overlay(Dm.list(mutable_appearance(icon, state)));
    }

    /** Removes any overlays */
    // tg: code/modules/pai/card.dm:191
    public virtual void remove_alert()
    {
        if (pai != null)
            return;
        cut_overlays();
    }

    /** Alerts pAI cards that someone has submitted candidacy */
    // tg: code/modules/pai/card.dm:197
    public virtual void alert_update()
    {
        // COOLDOWN_FINISHED and COOLDOWN_START (__DEFINES/cooldowns.dm), inline (rules 11.1 (a))
        if (!(Dm.num(alert_cooldown) <= world.time))
            return;
        alert_cooldown = world.time + 5 * SECONDS;
        add_alert();
        _addtimer(Dm.@new<datum_callback>(this, nameof(remove_alert)), 5 * SECONDS, file: "code/modules/pai/card.dm", line: 202);
        playsound(this, Dm.resource("sound/machines/ping.ogg"), 30, TRUE);
        visible_message(span_notice(I18n.T("[] flashes a message across its screen: New personalities available for download!", this)), blind_message: span_notice(I18n.T("[] vibrates with an alert.", this)));
    }

    /**
     * Downloads a candidate from the list and removes them from SSpai.candidates
     *
     * @param {string} ckey The ckey of the candidate to download
     *
     * @returns {boolean} - TRUE if the candidate was downloaded, FALSE if not
     */
    // tg: code/modules/pai/card.dm:213
    public virtual async DmTask<object> download_candidate(mob user = null, object ckey = null)
    {
        if (pai != null)
            return FALSE;
        var candidate = SSpai.candidates[ckey] as datum_pai_candidate;
        if (!Dm.truthy(candidate?.check_ready()))
        {
            balloon_alert(user, "download interrupted");
            return FALSE;
        }
        var new_pai = Dm.@new<mob_living_silicon_pai>(this);
        new_pai.name = Dm.truthy(candidate.name) ? Dm.str(candidate.name) : Dm.str(pick(GLOB.ninja_names));
        new_pai.real_name = new_pai.name;
        new_pai.PossessByPlayer(candidate.ckey);
        set_personality(new_pai);
        Dm.remove(SSpai.candidates, ckey);
        return TRUE;
    }

    /**
     * Pings ghosts to announce that someone is requesting a pAI
     *
     * @param {mob} user - The user who is requesting a pAI
     *
     * @returns {boolean} - TRUE if the pAI was requested, FALSE if not
     */
    // tg: code/modules/pai/card.dm:235
    public virtual object find_pai(mob user = null)
    {
        if (pai != null)
            return FALSE;
        if (!Dm.truthy(Dm.bitand(GLOB.ghost_role_flags, GHOSTROLE_SILICONS)))
        {
            balloon_alert(user, "unavailable: NT blacklisted");
            return FALSE;
        }
        if (Dm.truthy(request_spam))
        {
            balloon_alert(user, "request sent too recently");
            return FALSE;
        }
        request_spam = TRUE;
        playsound(this, Dm.resource("sound/machines/ping.ogg"), 20, TRUE);
        balloon_alert(user, "pAI assistance requested");
        var alert_overlay = (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/aicards.dmi"), "pai");

        notify_ghosts(
            I18n.T("[] is requesting a pAI companion! Use the pAI button to submit yourself as one.", user.real_name),
            source: user,
            header: "pAI Request!",
            alert_overlay: alert_overlay,
            notify_flags: NOTIFY_CATEGORY_NOFLASH,
            ignore_key: POLL_IGNORE_PAI);

        _addtimer(Dm.@new<datum_callback>(this, nameof(reset_request_spam)), PAI_SPAM_TIME, Dm.bitor(TIMER_UNIQUE, TIMER_DELETE_ME), file: "code/modules/pai/card.dm", line: 258);
        return TRUE;
    }

    // tg: code/modules/pai/card.dm:258  VARSET_CALLBACK(src, request_spam, FALSE)
    public virtual void reset_request_spam() => request_spam = FALSE;

    /**
     * Gathers a list of candidates to display in the download candidate
     * window. If the candidate isn't marked ready, ie they have not
     * pressed submit, they will be skipped over.
     *
     * @returns - An array of candidate objects.
     */
    // tg: code/modules/pai/card.dm:268
    public virtual DmList pool_candidates()
    {
        DmList candidates = Dm.list();
        if (pai != null || Dm.length(SSpai?.candidates) == 0)
            return candidates;
        foreach (object key in Dm.each(SSpai.candidates.Copy()))
        {
            var candidate = SSpai.candidates[key] as datum_pai_candidate;
            if (!Dm.truthy(candidate?.check_ready()))
                continue;
            candidates.Add(Dm.list(Dm.list(
                Dm.kv("ckey", candidate.ckey),
                Dm.kv("comments", candidate.comments),
                Dm.kv("description", candidate.description),
                Dm.kv("name", candidate.name))));
        }
        return candidates;
    }

    /**
     * Sets the personality on the current pai_card
     *
     * @param {silicon/pai} downloaded - The new pAI to load into the card.
     */
    // tg: code/modules/pai/card.dm:289
    public virtual object set_personality(mob_living_silicon_pai downloaded = null)
    {
        if (pai != null)
            return FALSE;
        pai = downloaded;
        RegisterSignal(pai, COMSIG_QDELETING, nameof(on_pai_del));
        screen_image = typeof(datum_pai_screen_image_neutral);
        update_appearance();
        playsound(this, Dm.resource("sound/effects/pai_boot.ogg"), 50, TRUE, -1);
        audible_message(I18n.T("[] plays a cheerful startup noise!", this));
        return TRUE;
    }
}
