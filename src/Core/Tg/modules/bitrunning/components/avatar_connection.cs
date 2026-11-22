// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/components/avatar_connection.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the avatar's link to its bitrunner: the key moved into the avatar and back, the skills
// copied both ways, the damage passed to the body in the pod, the hacker alias and the alerts of a pried, damaged or rebooting
// netpod. The var block is generated (D111).

/**
 * Essentially temporary body with a twist - the virtual domain variant uses damage connections,
 * listens for vdom relevant signals.
 */
// tg: code/modules/bitrunning/components/avatar_connection.dm:5
public partial class datum_component_avatar_connection : datum_component
{
    // tg: code/modules/bitrunning/components/avatar_connection.dm:17
    [DmParams("old_mind", "old_body", "server", "pod", "help_text", "copy_body")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        datum_mind old_mind = Dm.arg(__extra, 0) as datum_mind;
        mob_living old_body = Dm.arg(__extra, 1) as mob_living;
        obj_machinery_quantum_server server = Dm.arg(__extra, 2) as obj_machinery_quantum_server;
        obj_machinery_netpod pod = Dm.arg(__extra, 3) as obj_machinery_netpod;
        object help_text = Dm.arg(__extra, 4);
        object copy_body = Dm.arg(__extra, 5);

        if (!isliving(parent) || !isliving(old_body) || old_mind == null || !Dm.truthy(server.is_operational) || !Dm.truthy(pod.is_operational))
            return COMPONENT_INCOMPATIBLE;

        mob_living avatar = parent as mob_living;

        netpod_ref = WEAKREF(pod) as datum_weakref;
        old_body_ref = WEAKREF(old_body) as datum_weakref;
        old_mind_ref = WEAKREF(old_mind) as datum_weakref;
        pod.avatar_ref = WEAKREF(avatar) as datum_weakref;
        server_ref = WEAKREF(server) as datum_weakref;
        server.avatar_connection_refs.Add(WEAKREF(this));

        avatar.PossessByPlayer(old_body.key);
        ADD_TRAIT(avatar, TRAIT_NO_MINDSWAP, REF(this)); // do not remove this one
        ADD_TRAIT(old_body, TRAIT_MIND_TEMPORARILY_GONE, REF(this));

        /**
         * Things that will disconnect forcefully:
         * - Server shutdown / broken
         * - Netpod power loss / broken
         * - Pilot dies/ is moved / falls unconscious
         */
        RegisterSignals(old_body, Dm.list(COMSIG_LIVING_DEATH, COMSIG_MOVABLE_MOVED, COMSIG_LIVING_STATUS_UNCONSCIOUS), nameof(on_sever_connection));
        RegisterSignal(pod, COMSIG_BITRUNNER_CROWBAR_ALERT, nameof(on_netpod_crowbar));
        RegisterSignal(pod, COMSIG_BITRUNNER_NETPOD_INTEGRITY, nameof(on_netpod_damaged));
        RegisterSignal(pod, COMSIG_BITRUNNER_NETPOD_SEVER, nameof(on_sever_connection));
        RegisterSignal(server, COMSIG_BITRUNNER_DOMAIN_COMPLETE, nameof(on_domain_completed));
        RegisterSignal(server, COMSIG_BITRUNNER_QSRV_SEVER, nameof(on_sever_connection));
        RegisterSignal(server, COMSIG_BITRUNNER_SHUTDOWN_ALERT, nameof(on_shutting_down));
        RegisterSignal(server, COMSIG_BITRUNNER_THREAT_CREATED, nameof(on_threat_created));
        RegisterSignal(server, COMSIG_BITRUNNER_STATION_SPAWN, nameof(on_station_spawn));
        // tg: #ifndef UNIT_TESTS (production)
        RegisterSignal(avatar.mind, COMSIG_MIND_TRANSFERRED, nameof(on_mind_transfer));

        if (Dm.locate<datum_action_avatar_domain_info>(avatar.actions) == null)
        {
            datum_avatar_help_text help_datum = await Dm.new_async<datum_avatar_help_text>(help_text);
            datum_action_avatar_domain_info action = await Dm.new_async<datum_action_avatar_domain_info>(help_datum);
            await action.Grant(avatar);
        }

        client our_client = avatar.client;
        object alias = our_client?.prefs?.read_preference(typeof(datum_preference_name_hacker_alias));
        if (!Dm.truthy(alias))
            alias = pick(GLOB.hacker_aliases);

        if (Dm.truthy(alias) && !Dm.eq(avatar.real_name, alias))
        {
            await avatar.fully_replace_character_name(newname: alias);
            avatar.voice = old_body.voice;
            avatar.voice_filter = old_body.voice_filter;
            if (ishuman(avatar) && ishuman(old_body) && Dm.truthy(copy_body))
            {
                mob_living_carbon_human human_avatar = avatar as mob_living_carbon_human;
                mob_living_carbon_human human_old_body = old_body as mob_living_carbon_human;
                human_avatar.dna.unique_identity = human_old_body.dna.unique_identity;
                human_avatar.physique = human_old_body.physique;
                await human_avatar.updateappearance(mutcolor_update: TRUE);
            }
        }

        await update_avatar_id();
        avatar.mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_bit_avatar)) as datum_job);

        foreach (object skill_type in Dm.each(old_mind.known_skills))
            avatar.mind.set_experience(skill_type, old_mind.get_skill_exp(skill_type), silent: TRUE);

        avatar.playsound_local(avatar, Dm.resource("sound/effects/magic/blink.ogg"), 25, TRUE);
        await avatar.set_static_vision(2 * SECONDS);
        await avatar.set_temp_blindness(1 * SECONDS); // I'm in
        return null;
    }

    // tg: code/modules/bitrunning/components/avatar_connection.dm:92
    public override async DmTask<object> PostTransfer(datum new_parent = null)
    {
        await DmTask.CompletedTask;
        obj_machinery_netpod pod = netpod_ref?.resolve() as obj_machinery_netpod;
        if (pod == null)
            return COMPONENT_INCOMPATIBLE;

        if (!isliving(new_parent))
            return COMPONENT_INCOMPATIBLE;

        pod.avatar_ref = WEAKREF(new_parent) as datum_weakref;
        return null;
    }

    // tg: code/modules/bitrunning/components/avatar_connection.dm:103
    public override async DmTask RegisterWithParent()
    {
        await DmTask.CompletedTask;
        ADD_TRAIT(parent, TRAIT_TEMPORARY_BODY, REF(this));
        /**
         * Things that cause safe disconnection:
         * - Click the alert
         * - Mailed in a cache
         * - Click / Stand on the ladder
         */
        RegisterSignals(parent, Dm.list(COMSIG_BITRUNNER_ALERT_SEVER, COMSIG_BITRUNNER_CACHE_SEVER, COMSIG_BITRUNNER_LADDER_SEVER), nameof(on_safe_disconnect));
        RegisterSignal(parent, COMSIG_LIVING_PILL_CONSUMED, nameof(disconnect_if_red_pill));
        RegisterSignals(parent, Dm.list(COMSIG_LIVING_DEATH, COMSIG_QDELETING), nameof(on_sever_connection));
        RegisterSignal(parent, COMSIG_MOB_APPLY_DAMAGE, nameof(on_linked_damage));
    }

    // tg: code/modules/bitrunning/components/avatar_connection.dm:117
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await DmTask.CompletedTask;
        REMOVE_TRAIT(parent, TRAIT_TEMPORARY_BODY, REF(this));
        UnregisterSignal(parent, Dm.list(
            COMSIG_BITRUNNER_ALERT_SEVER,
            COMSIG_BITRUNNER_CACHE_SEVER,
            COMSIG_BITRUNNER_LADDER_SEVER,
            COMSIG_LIVING_PILL_CONSUMED,
            COMSIG_LIVING_DEATH,
            COMSIG_QDELETING,
            COMSIG_MOB_APPLY_DAMAGE));
    }

    /// Updates our avatar's ID to match our avatar's name.
    // tg: code/modules/bitrunning/components/avatar_connection.dm:131
    public virtual async DmTask update_avatar_id()
    {
        mob_living avatar = parent as mob_living;
        obj_item_card_id our_id = Dm.locate<obj_item_card_id>(avatar.get_all_contents());
        if (our_id == null)
            return;

        our_id.registered_name = avatar.real_name;
        our_id.update_label();
        await our_id.update_icon();
        if (our_id.registered_account != null)
            our_id.registered_account.account_holder = Dm.str(avatar.real_name);
    }

    /// Disconnects the avatar and returns the mind to the old_body.
    // tg: code/modules/bitrunning/components/avatar_connection.dm:145
    public virtual async DmTask full_avatar_disconnect(float? cause_damage = null, datum source = null)
    {
        cause_damage ??= FALSE;
        // tg: #ifndef UNIT_TESTS (production)
        await return_to_old_body();

        obj_machinery_netpod hosting_netpod = netpod_ref?.resolve() as obj_machinery_netpod;
        if (hosting_netpod == null && source is obj_machinery_netpod)
            hosting_netpod = source as obj_machinery_netpod;

        if (hosting_netpod != null)
            await hosting_netpod.disconnect_occupant(cause_damage);

        obj_machinery_quantum_server server = server_ref?.resolve() as obj_machinery_quantum_server;
        server?.avatar_connection_refs.Remove(WEAKREF(this));

        qdel(this);
    }

    /// Triggers whenever the server gets a loot crate pushed to goal area
    // tg: code/modules/bitrunning/components/avatar_connection.dm:163
    [SignalHandler]
    public virtual void on_domain_completed(datum source = null, atom entered = null)
    {
        mob_living avatar = parent as mob_living;
        avatar.playsound_local(avatar, Dm.resource("sound/machines/terminal/terminal_success.ogg"), 50, vary: TRUE);
        avatar.throw_alert(
            ALERT_BITRUNNER_COMPLETED,
            typeof(atom_movable_screen_alert_bitrunning_qserver_domain_complete),
            new_master: entered);
    }

    /// Transfers damage from the avatar to the old_body
    // tg: code/modules/bitrunning/components/avatar_connection.dm:176
    [SignalHandler]
    public virtual void on_linked_damage(datum source = null, object damage = null, object damage_type = null, object def_zone = null, object blocked = null, params object[] __extra)
    {
        mob_living_carbon old_body = old_body_ref?.resolve() as mob_living_carbon;
        if (old_body == null || Dm.eq(damage_type, STAMINA) || Dm.eq(damage_type, OXYLOSS))
            return;

        if (Dm.num(damage) >= (old_body.health + MAX_LIVING_HEALTH))
        {
            Waiting.NoSleep(full_avatar_disconnect(cause_damage: TRUE));
            return;
        }

        if (Dm.num(damage) > 30 && prob(30))
            Waiting.Detach(old_body.emote("scream"));

        object zone = def_zone;
        if (isbodypart(def_zone)) // If the defined zone is a bodypart, then it is the bodypart of the domain avatar. We don't want that bodypart, we want the real body's bodypart, so we go back to zone.
            zone = (def_zone as obj_item_bodypart).body_zone;

        Waiting.NoSleep(old_body.apply_damage(Dm.num(damage), damage_type as string, zone, Dm.num_or_null(blocked), wound_bonus: CANT_WOUND));

        if (IS_UNCONSCIOUS(old_body)) // KO!
            Waiting.NoSleep(full_avatar_disconnect(cause_damage: TRUE));

        nohit = FALSE;
    }

    /// Handles minds being swapped around in subsequent avatars
    // tg: code/modules/bitrunning/components/avatar_connection.dm:202
    [SignalHandler]
    public virtual void on_mind_transfer(datum_mind source = null, mob_living previous_body = null)
    {
        datum_action_avatar_domain_info action = Dm.locate<datum_action_avatar_domain_info>(previous_body.actions);
        if (action != null)
            Waiting.NoSleep(action.Grant(source.current));

        Waiting.NoSleep(source.current.TakeComponent(this));
    }

    /// Triggers when someone starts prying open our netpod
    // tg: code/modules/bitrunning/components/avatar_connection.dm:213
    [SignalHandler]
    public virtual void on_netpod_crowbar(datum source = null, mob_living intruder = null)
    {
        mob_living avatar = parent as mob_living;
        avatar.playsound_local(avatar, Dm.resource("sound/machines/terminal/terminal_alert.ogg"), 50, vary: TRUE);
        atom_movable_screen_alert_bitrunning alert = avatar.throw_alert(
            ALERT_BITRUNNER_CROWBAR,
            typeof(atom_movable_screen_alert_bitrunning),
            new_master: intruder) as atom_movable_screen_alert_bitrunning;
        alert.name = "Netpod Breached";
        alert.desc = "Someone is prying open the netpod. Find an exit.";
    }

    /// Triggers when the netpod is taking damage and is under 50%
    // tg: code/modules/bitrunning/components/avatar_connection.dm:228
    [SignalHandler]
    public virtual void on_netpod_damaged(datum source = null)
    {
        mob_living avatar = parent as mob_living;
        atom_movable_screen_alert_bitrunning alert = avatar.throw_alert(
            ALERT_BITRUNNER_INTEGRITY,
            typeof(atom_movable_screen_alert_bitrunning),
            new_master: source as atom) as atom_movable_screen_alert_bitrunning;
        alert.name = "Integrity Compromised";
        alert.desc = "The netpod is damaged. Find an exit.";
    }

    //if your bitrunning avatar somehow manages to acquire and consume a red pill, they will be ejected from the Matrix
    // tg: code/modules/bitrunning/components/avatar_connection.dm:242
    [SignalHandler]
    public virtual void disconnect_if_red_pill(datum source = null, obj_item_reagent_containers_applicator_pill pill = null, mob feeder = null)
    {
        if (Dm.eq(pill.icon_state, "pill4"))
            Waiting.NoSleep(full_avatar_disconnect());
    }

    /// Triggers when a safe disconnect is called
    // tg: code/modules/bitrunning/components/avatar_connection.dm:249
    [SignalHandler]
    public virtual void on_safe_disconnect(datum source = null)
    {
        Waiting.NoSleep(full_avatar_disconnect());
    }

    /// Received message to sever connection
    // tg: code/modules/bitrunning/components/avatar_connection.dm:256
    [SignalHandler]
    public virtual void on_sever_connection(datum source = null)
    {
        Waiting.NoSleep(full_avatar_disconnect(cause_damage: TRUE, source: source));
    }

    /// Triggers when the server is shutting down
    // tg: code/modules/bitrunning/components/avatar_connection.dm:263
    [SignalHandler]
    public virtual void on_shutting_down(datum source = null, mob_living hackerman = null)
    {
        mob_living avatar = parent as mob_living;
        avatar.playsound_local(avatar, Dm.resource("sound/machines/terminal/terminal_alert.ogg"), 50, vary: TRUE);
        atom_movable_screen_alert_bitrunning alert = avatar.throw_alert(
            ALERT_BITRUNNER_SHUTDOWN,
            typeof(atom_movable_screen_alert_bitrunning),
            new_master: hackerman) as atom_movable_screen_alert_bitrunning;
        alert.name = "Domain Rebooting";
        alert.desc = "The domain is rebooting. Find an exit.";
    }

    /// Triggers whenever an antag steps onto an exit turf and the server is emagged
    // tg: code/modules/bitrunning/components/avatar_connection.dm:278
    [SignalHandler]
    public virtual void on_station_spawn(datum source = null)
    {
        mob_living avatar = parent as mob_living;
        avatar.playsound_local(avatar, Dm.resource("sound/machines/terminal/terminal_alert.ogg"), 50, vary: TRUE);
        atom_movable_screen_alert_bitrunning alert = avatar.throw_alert(
            ALERT_BITRUNNER_BREACH,
            typeof(atom_movable_screen_alert_bitrunning),
            new_master: source as atom) as atom_movable_screen_alert_bitrunning;
        alert.name = "Security Breach";
        alert.desc = "A hostile entity is breaching the safehouse. Find an exit.";
    }

    /// Server has spawned a ghost role threat
    // tg: code/modules/bitrunning/components/avatar_connection.dm:293
    [SignalHandler]
    public virtual void on_threat_created(datum source = null)
    {
        mob_living avatar = parent as mob_living;
        atom_movable_screen_alert_bitrunning alert = avatar.throw_alert(
            ALERT_BITRUNNER_THREAT,
            typeof(atom_movable_screen_alert_bitrunning),
            new_master: source as atom) as atom_movable_screen_alert_bitrunning;
        alert.name = "Threat Detected";
        alert.desc = "Data stream abnormalities present.";
    }

    /// Returns the mind to the old body
    // tg: code/modules/bitrunning/components/avatar_connection.dm:307
    public virtual async DmTask return_to_old_body()
    {
        datum_mind old_mind = old_mind_ref?.resolve() as datum_mind;
        mob_living old_body = old_body_ref?.resolve() as mob_living;
        mob_living avatar = parent as mob_living;

        mob_dead_observer ghost = avatar.ghostize() as mob_dead_observer;
        if (ghost == null)
            ghost = avatar.get_ghost() as mob_dead_observer;

        if (ghost == null)
            throw Dm.CRASH(Dm.text("[] belonging to [] was completely unable to find a ghost to put back into a body!", this, parent), "code/modules/bitrunning/components/avatar_connection.dm", 317);

        if (old_mind == null || old_body == null)
            return;

        foreach (object skill_type in Dm.each(avatar.mind.known_skills))
        {
            old_mind.set_experience(skill_type, avatar.mind.get_skill_exp(skill_type), silent: TRUE);
            avatar.mind.set_experience(skill_type, 0, silent: TRUE);
        }

        ghost.mind = old_mind;
        if (old_body.stat != DEAD)
            await old_mind.transfer_to(old_body, force_key_move: TRUE);
        else
            old_mind.set_current(old_body);

        REMOVE_TRAIT(old_body, TRAIT_MIND_TEMPORARILY_GONE, REF(this));
    }
}
