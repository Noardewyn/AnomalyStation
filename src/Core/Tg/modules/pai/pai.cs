// Translated from tgstation (snapshot 1d99bd7f): code/modules/pai/pai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6 (D326): the whole file. The var block is generated; the static lists are TgState fields (rules 10.6).

// tg: code/modules/pai/pai.dm:1
public partial class mob_living_silicon_pai : mob_living_silicon
{
    /// List of all available downloads
    // tg: code/modules/pai/pai.dm:84  var/static/list/available_software (rules 10.6: a TgState field, below)
    public static DmList available_software => TgState.Current.mob_living_silicon_pai__available_software ??= Dm.list(
        Dm.kv("Atmospheric Sensor", 5),
        Dm.kv("Crew Manifest", 5),
        Dm.kv("Digital Messenger", 5),
        Dm.kv("Photography Module", 5),
        Dm.kv("Encryption Slot", 10),
        Dm.kv("Music Synthesizer", 10),
        Dm.kv("Newscaster", 10),
        Dm.kv("Remote Signaler", 10),
        Dm.kv("Host Scan", 20),
        Dm.kv("Medical HUD", 20),
        Dm.kv("Security HUD", 20),
        Dm.kv("Crew Monitor", 35),
        Dm.kv("Door Jack", 35),
        Dm.kv("Internal GPS", 35),
        Dm.kv("Universal Translator", 35)
    );

    /// List of all possible chasises. TRUE means the pAI can be picked up in this chasis.
    // tg: code/modules/pai/pai.dm:101  var/static/list/possible_chassis (rules 10.6: a TgState field, below)
    public static DmList possible_chassis => TgState.Current.mob_living_silicon_pai__possible_chassis ??= Dm.list(
        Dm.kv("bat", FALSE),
        Dm.kv("butterfly", FALSE),
        Dm.kv("cat", TRUE),
        Dm.kv("chicken", FALSE),
        Dm.kv("corgi", FALSE),
        Dm.kv("crow", TRUE),
        Dm.kv("duffel", TRUE),
        Dm.kv("fox", FALSE),
        Dm.kv("frog", TRUE),
        Dm.kv("hawk", FALSE),
        Dm.kv("lizard", FALSE),
        Dm.kv("monkey", TRUE),
        Dm.kv("mouse", TRUE),
        Dm.kv("rabbit", TRUE),
        Dm.kv("repairbot", TRUE),
        Dm.kv("kitten", TRUE),
        Dm.kv("puppy", TRUE),
        Dm.kv("spider", TRUE)
    );

    // tg: code/modules/pai/pai.dm:121
    public override void add_sensors() //pAIs have to buy their HUDs
    {
    }

    // tg: code/modules/pai/pai.dm:124
    public override async DmTask<object> can_interact_with(atom A = null, object treat_mob_as_adjacent = null)
    {
        if (A == signaler) // Bypass for signaler
            return TRUE;
        if (A == modularInterface)
            return TRUE;
        return await base.can_interact_with(A, treat_mob_as_adjacent);
    }

    // See software.dm for Topic()
    // tg: code/modules/pai/pai.dm:132
    public override async DmTask<object> can_perform_action(atom target = null, object action_bitflags = null)
    {
        float flags = Dm.num(action_bitflags);
        if (!Dm.truthy(Dm.bitand(flags, ALLOW_PAI)))
        {
            to_chat(this, span_warning(I18n.T("Your holochasis does not allow you to do this!")));
            return FALSE;
        }
        flags = Dm.bitor(flags, ALLOW_RESTING); // Resting is just an aesthetic feature for them
        flags = Dm.bitand(flags, Dm.bitnot(ALLOW_SILICON_REACH)); // They don't get long reach like the rest of silicons
        return await base.can_perform_action(target, flags);
    }

    // tg: code/modules/pai/pai.dm:141
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref messenger_ability);
        QDEL_NULL(ref atmos_analyzer);
        QDEL_NULL(ref hacking_cable);
        QDEL_NULL(ref instrument);
        QDEL_NULL(ref internal_gps);
        QDEL_NULL(ref newscaster);
        QDEL_NULL(ref signaler);
        QDEL_NULL(ref leash);
        card = null;
        return base.Destroy(force);
    }

    // Need to override parent here because the message we dispatch is turf-based, not based on the location of the object because that could be fuckin anywhere
    // tg: code/modules/pai/pai.dm:154
    public override void send_applicable_messages(object message_type = null)
    {
        turf location = get_turf(this);
        location.visible_message(span_danger(get_visible_suicide_message() as string), null, span_hear(get_blind_suicide_message() as string)); // null in the second arg here because we're sending from the turf
    }

    // tg: code/modules/pai/pai.dm:158
    public override object get_visible_suicide_message()
        => I18n.T("[] flashes a message across its screen, \"Wiping core files. Please acquire a new personality to continue using pAI device functions.\"", this);

    // tg: code/modules/pai/pai.dm:161
    public override object get_blind_suicide_message() => I18n.T("[] bleeps electronically.", this);

    // tg: code/modules/pai/pai.dm:164
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        return handle_emag(user as mob_living_carbon);
    }

    // tg: code/modules/pai/pai.dm:167
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        var dot = await base.examine(user, __extra) as DmList;
        dot.Add(I18n.T("Its master ID string seems to be [].", !Dm.truthy(master_name) || Dm.truthy(emagged) ? "empty" : master_name));
        return dot;
    }

    // tg: code/modules/pai/pai.dm:171
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == atmos_analyzer)
            atmos_analyzer = null;
        else if (gone == aicamera)
            aicamera = null;
        else if (gone == internal_gps)
            internal_gps = null;
        else if (gone == instrument)
            instrument = null;
        else if (gone == newscaster)
            newscaster = null;
        else if (gone == signaler)
            signaler = null;
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/pai/pai.dm:186
    public virtual void on_hacking_cable_del(atom source = null)
    {
        untrack_pai();
        untrack_thing(hacking_cable);
        hacking_cable = null;
        SStgui.update_user_uis(this);
        if (!QDELETED(card))
            card.update_appearance();
    }

    // tg: code/modules/pai/pai.dm:195
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_holographic_nature)));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        if (loc is obj_item_modular_computer)
            give_messenger_ability();
        START_PROCESSING(SSfastprocess, this);
        make_laws();
        law_ui.update_inherent_stated_laws(laws);
        var pai_card = loc as obj_item_pai_card;
        if (pai_card == null) // when manually spawning a pai, we create a card to put it into.
        {
            atom newcardloc = loc;
            pai_card = Dm.@new<obj_item_pai_card>(newcardloc);
            pai_card.set_personality(this);
        }
        card = pai_card;
        Waiting.NoSleep(forceMove(pai_card));
        toggle_leash();
        _addtimer(Dm.@new<datum_callback>(this, nameof(set_holochassis_ready)), HOLOCHASSIS_INIT_TIME, file: "code/modules/pai/pai.dm", line: 211);
        if (!Dm.truthy(holoform))
            add_traits(Dm.list(TRAIT_IMMOBILIZED, TRAIT_HANDS_BLOCKED), PAI_FOLDED);
        update_appearance(UPDATE_DESC);
        RegisterSignal(this, COMSIG_LIVING_CULT_SACRIFICED, nameof(on_cult_sacrificed));
        RegisterSignals(this, Dm.list(COMSIG_LIVING_ADJUST_BRUTE_DAMAGE, COMSIG_LIVING_ADJUST_BURN_DAMAGE), nameof(on_shell_damaged));
        RegisterSignal(this, COMSIG_LIVING_ADJUST_STAMINA_DAMAGE, nameof(on_shell_weakened));
        RegisterSignal(this, COMSIG_MOB_TRIED_ACCESS, nameof(on_tried_access));
        return dot;
    }

    // tg: code/modules/pai/pai.dm:221
    public virtual void toggle_leash()
    {
        if (card == null)
            return;
        if (leash != null)
            QDEL_NULL(ref leash);
        else
            leash = Waiting.NoSleep(AddComponent(typeof(datum_component_leash), card, HOLOFORM_DEFAULT_RANGE, Dm.named("force_teleport_out_effect", typeof(obj_effect_temp_visual_guardian_phase_out)))) as datum_component_leash;
    }

    // tg: code/modules/pai/pai.dm:229
    public override void create_modularInterface()
    {
        if (modularInterface == null)
            modularInterface = Dm.@new<obj_item_modular_computer_pda_silicon_pai>(this);
        base.create_modularInterface();
    }

    // tg: code/modules/pai/pai.dm:234
    public override void make_laws()
    {
        laws = Dm.@new<datum_ai_laws_pai>();
        laws.name = "PAI Directives";
    }

    // tg: code/modules/pai/pai.dm:238
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (holochassis_health != HOLOCHASSIS_MAX_HEALTH)
        {
            holochassis_health = Math.Clamp(holochassis_health + HOLOCHASSIS_REGEN_PER_SECOND * Dm.num(seconds_per_tick), -50, HOLOCHASSIS_MAX_HEALTH);
            update_health_hud();
        }
        return null;
    }

    // tg: code/modules/pai/pai.dm:243
    public override async DmTask<object> Process_Spacemove(float? movement_dir = null, float? continuous_move = null)
    {
        object dot = await base.Process_Spacemove(movement_dir, continuous_move);
        if (!Dm.truthy(dot))
        {
            await add_movespeed_modifier(typeof(datum_movespeed_modifier_pai_spacewalk));
            return TRUE;
        }
        remove_movespeed_modifier(typeof(datum_movespeed_modifier_pai_spacewalk));
        return TRUE;
    }

    // tg: code/modules/pai/pai.dm:251
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return await ((obj_item_radio)radio).screwdriver_act(user, tool);
    }

    // tg: code/modules/pai/pai.dm:254
    public override async DmTask updatehealth()
    {
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return;
        set_health(Dm.sub(Dm.sub(maxHealth, get_brute_loss()), get_fire_loss()));
        update_stat();
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_HEALTH_UPDATE]))
            _SendSignal(COMSIG_LIVING_HEALTH_UPDATE, Dm.list(this));
    }

    // tg: code/modules/pai/pai.dm:261
    public override object update_desc(float? updates = null)
    {
        desc = I18n.T("A hard-light holographic avatar representing a pAI. This one appears in the form of a [].", chassis);
        return base.update_desc(updates);
    }

    // tg: code/modules/pai/pai.dm:265
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.truthy(resting) ? Dm.text("[]_rest", chassis) : chassis;
        held_state = chassis;
        return await base.update_icon_state(__extra);
    }

    /**
     * Resolves the weakref of the pai's master.
     * If the master has been deleted, calls reset_software().
     *
     * @returns {mob/living || FALSE} - The master mob, or
     * 	FALSE if the master is gone.
     */
    // tg: code/modules/pai/pai.dm:277
    public virtual mob_living find_master()
    {
        if (master_ref == null)
            return null;
        var resolved_master = master_ref?.resolve() as mob_living;
        if (resolved_master == null)
        {
            reset_software();
            return null;
        }
        return resolved_master;
    }

    /**
     * Fixes weird speech issues with the pai.
     *
     * @returns {boolean} - TRUE if successful.
     */
    // tg: code/modules/pai/pai.dm:291
    public virtual object fix_speech()
    {
        mob_living_silicon pai = this;
        balloon_alert(pai, "speech modulation corrected");
        foreach (Type effect in Dm.each_cast<Type>(typesof(typeof(datum_status_effect_speech))))
            pai.remove_status_effect(effect);
        return TRUE;
    }

    /**
     * Gets the current holder of the pAI if its
     * being carried in card or holoform.
     *
     * @returns {living/carbon || FALSE} - The holder of the pAI,
     * 	or FALSE if the pAI is not being carried.
     */
    // tg: code/modules/pai/pai.dm:305
    public virtual mob_living_carbon get_holder()
    {
        return recursive_loc_check(card, typeof(mob_living_carbon)) as mob_living_carbon;
    }

    /**
     * Handles the pai card or the pai itself being hit with an emag.
     * This replaces any current laws, masters, and DNA.
     *
     * @param {living/carbon} attacker - The user performing the action.
     * @returns {boolean} - TRUE if successful, FALSE if not.
     */
    // tg: code/modules/pai/pai.dm:319
    public virtual object handle_emag(mob_living_carbon attacker = null)
    {
        if (!isliving(attacker))
            return FALSE;
        balloon_alert(attacker, "directive override complete");
        balloon_alert(this, "directive override detected");
        log_game(Dm.text("[] emagged [], wiping their master DNA and supplemental directive.", key_name(attacker), key_name(this)));
        emagged = TRUE;
        master_ref = WEAKREF(attacker) as datum_weakref;
        master_name = "The Syndicate";
        master_dna = "Untraceable Signature";
        // Sets supplemental directive to this
        laws.clear_inherent_laws();
        laws.add_inherent_law("Do not interfere with the operations of the Syndicate.");
        log_law_change(attacker, Dm.text("emagged pai []", key_name(this)));
        to_chat(this, span_danger(I18n.T("ALERT: Foreign software detected.")));
        return TRUE;
    }

    // tg: code/modules/pai/pai.dm:336
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        await base.on_saboteur(source, disrupt_duration);
        await set_silence_if_lower(disrupt_duration);
        balloon_alert(this, "muted!");
        return TRUE;
    }

    /**
     * Resets the pAI and any emagged status.
     *
     * @returns {boolean} - TRUE if successful, FALSE if not.
     */
    // tg: code/modules/pai/pai.dm:347
    public virtual object reset_software()
    {
        emagged = FALSE;
        if (master_ref == null)
            return FALSE;
        master_ref = null;
        master_name = null;
        master_dna = null;
        laws.clear_inherent_laws();
        leash = Waiting.NoSleep(AddComponent(typeof(datum_component_leash), card, HOLOFORM_DEFAULT_RANGE, Dm.named("force_teleport_out_effect", typeof(obj_effect_temp_visual_guardian_phase_out)))) as datum_component_leash;
        balloon_alert(this, "software rebooted");
        return TRUE;
    }

    /**
     * Imprints your DNA onto the downloaded pAI
     *
     * @param {mob} user - The user performing the imprint.
     * @returns {boolean} - TRUE if successful, FALSE if not.
     */
    // tg: code/modules/pai/pai.dm:366
    public virtual object set_dna(mob user = null)
    {
        if (!iscarbon(user))
        {
            balloon_alert(user, "incompatible DNA signature");
            balloon_alert(this, "incompatible DNA signature");
            return FALSE;
        }
        if (Dm.truthy(emagged))
        {
            balloon_alert(user, "directive system malfunctional");
            return FALSE;
        }
        var master = (mob_living_carbon)user;
        master_ref = WEAKREF(master) as datum_weakref;
        master_name = master.real_name;
        master_dna = master.dna.unique_enzymes;
        to_chat(this, span_bolddanger(I18n.T("You have been bound to a new master: []!", user.real_name)));
        holochassis_ready = TRUE;
        return TRUE;
    }

    /**
     * Opens a tgui alert that allows someone to enter laws.
     *
     * @param {mob} user - The user performing the law change.
     * @returns {boolean} - TRUE if successful, FALSE if not.
     */
    // tg: code/modules/pai/pai.dm:388
    public virtual async DmTask<object> set_laws(mob user = null)
    {
        if (master_ref == null)
        {
            balloon_alert(user, "access denied: no master");
            return FALSE;
        }
        object new_laws = await tgui_input_text(
            user,
            "Enter any additional directives you would like your pAI personality to follow. Note that these directives will not override the personality's allegiance to its imprinted master. Conflicting directives will be ignored.",
            "pAI Directive Configuration",
            laws.inherent[1],
            max_length: 300
        );
        if (!Dm.truthy(new_laws) || master_ref == null || QDELETED(laws))
            return FALSE;
        laws.add_inherent_law(new_laws);
        to_chat(this, span_notice(Dm.str(new_laws)));
        log_law_change(user as mob_living, Dm.text("added law for pai [] (text: [])", key_name(this), new_laws));
        return TRUE;
    }

    /**
     * Toggles the ability of the pai to enter holoform
     *
     * @returns {boolean} - TRUE if successful, FALSE if not.
     */
    // tg: code/modules/pai/pai.dm:412
    public virtual object toggle_holo()
    {
        balloon_alert(this, Dm.text("holomatrix []", Dm.truthy(can_holo) ? "disabled" : "enabled"));
        can_holo = !Dm.truthy(can_holo) ? TRUE : FALSE;
        return TRUE;
    }

    /**
     * Toggles the radio settings on and off.
     *
     * @param {string} option - The option being toggled.
     */
    // tg: code/modules/pai/pai.dm:422
    public virtual object toggle_radio(object option = null)
    {
        // it can't be both so if we know it's not transmitting it must be receiving.
        bool transmitting = Dm.eq(option, "transmit");
        object transmit_holder = transmitting ? WIRE_TX : WIRE_RX;
        if (transmitting)
            can_transmit = !Dm.truthy(can_transmit) ? TRUE : FALSE;
        else //receiving
            can_receive = !Dm.truthy(can_receive) ? TRUE : FALSE;
        Waiting.NoSleep(((obj_item_radio)radio).wires.cut(transmit_holder)); //wires.cut toggles cut and uncut states
        transmit_holder = transmitting ? can_transmit : can_receive; //recycling can be fun!
        balloon_alert(this, Dm.text("[] radio []", transmitting ? "outgoing" : "incoming", Dm.truthy(transmit_holder) ? "enabled" : "disabled"));
        return TRUE;
    }

    /**
     * Wipes the current pAI on the card.
     *
     * @param {mob} user - The user performing the action.
     *
     * @returns {boolean} - TRUE if successful, FALSE if not.
     */
    // tg: code/modules/pai/pai.dm:443
    public virtual async DmTask<object> wipe_pai(mob user = null)
    {
        if (!Dm.eq(await tgui_alert(user, "Are you certain you wish to delete the current personality? This action cannot be undone.", "Personality Wipe", Dm.list("Yes", "No")), "Yes"))
            return FALSE;
        to_chat(this, span_warning(I18n.T("You feel yourself slipping away from reality.")));
        to_chat(this, span_danger(I18n.T("Byte by byte you lose your sense of self.")));
        to_chat(this, span_userdanger(I18n.T("Your mental faculties leave you.")));
        to_chat(this, span_rose(I18n.T("oblivion... ")));
        balloon_alert(user, "personality wiped");
        playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 30, TRUE);
        qdel(this);
        return TRUE;
    }

    /// Signal proc for [COMSIG_LIVING_CULT_SACRIFICED] to give a funny message when a pai is attempted to be sac'd
    // tg: code/modules/pai/pai.dm:457
    public virtual object on_cult_sacrificed(datum source = null, DmList invokers = null)
    {
        foreach (mob_living cultist in Dm.each_cast<mob_living>(invokers))
            to_chat(cultist, span_cult_italic(I18n.T("You don't think this is what Nar'Sie had in mind when She asked for blood sacrifices...")));
        return Dm.bitor(STOP_SACRIFICE, SILENCE_SACRIFICE_MESSAGE);
    }

    /// Updates the distance we can be from our pai card
    // tg: code/modules/pai/pai.dm:465
    public virtual void increment_range(float increment_amount = 0)
    {
        float new_distance = Dm.num(leash.distance) + increment_amount;
        if (new_distance < HOLOFORM_MIN_RANGE || new_distance > HOLOFORM_MAX_RANGE)
            return;
        leash.set_distance(new_distance);
    }

    ///Gives the messenger ability to the pAI, creating a new one if it doesn't have one already.
    // tg: code/modules/pai/pai.dm:472
    public virtual void give_messenger_ability()
    {
        messenger_ability ??= Dm.@new<datum_action_innate_pai_messenger>(this);
        Waiting.NoSleep(messenger_ability.Grant(this));
    }

    ///Removes the messenger ability from the pAI, but does not delete it.
    // tg: code/modules/pai/pai.dm:478
    public virtual void remove_messenger_ability()
    {
        if (messenger_ability != null)
            Waiting.NoSleep(messenger_ability.Remove(this));
    }

    // tg: code/modules/pai/pai.dm:482
    public override object get_access() => Dm.list();

    ///Called when a pAI tries opening something that requires access.
    // tg: code/modules/pai/pai.dm:485
    public virtual object on_tried_access(datum source = null, obj door_attempt = null, DmList player_access = null)
    {
        return ACCESS_DISALLOWED;
    }

    // tg: code/modules/pai/pai.dm:489
    public override object update_health_hud(object healthpercent = null)
    {
        healthpercent = holochassis_health / HOLOCHASSIS_MAX_HEALTH * 100;
        return base.update_health_hud(healthpercent);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/pai/pai.dm:84  /mob/living/silicon/pai/var/static/list/available_software
    internal DmList mob_living_silicon_pai__available_software;
    // tg: code/modules/pai/pai.dm:101  var/static/list/possible_chassis
    internal DmList mob_living_silicon_pai__possible_chassis;
}
