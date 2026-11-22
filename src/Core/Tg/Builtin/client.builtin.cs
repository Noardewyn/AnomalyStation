// Engine ("BYOND builtin") part of tg's /client (docs/ai/translation-rules.md 1.1 and 2.3). Not translated from a tg file:
// BYOND declares /client (a builtin type, not a datum: tg re-parents it with parent_type = /datum,
// code/modules/client/client_defines.dm:36); tg's own vars and procs are in modules/client/client_defines.cs and the
// other mirror files.
//
// Owner in M1: P1 (the client layer, docs/ai/specs/m1-ownership.md). The M1 contracts step declared BYOND's client vars below
// with BYOND's defaults (BYOND ref /client/var/*); P1 wrote the engine behaviour: how BYOND makes a connecting player's
// client (dm_engine_create, called by ClientSeam.Connect), BYOND's own definitions of /client/New, /client/Del,
// /client/Move and (M2) /client/Click that tg's `..()` reaches (New__d0, Del__d0, Move__d0, Click__d0, rules 7.5), the client.mob assignment, and what the
// engine does when tg deletes a client (BYOND's del: the player is disconnected). Pinned in docs/ai/specs/dm-conformance.md,
// section "The /client engine part (M1 P1)".
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tg;

// RULE-GAP: as for /datum (Builtin/datum.builtin.cs), the engine part of a BYOND builtin type carries its [DmPath]
// (moved here from the skeleton modules/client/client_defines.cs).
[DmPath("/client")]
public partial class client : datum
{
    /// <summary>
    /// BYOND's defaults of /client (BYOND ref <c>/client/var</c>); tg's overrides follow in <see cref="dm_tg_vars"/> (rules
    /// 10.2, "Builtin types"). The defaults BYOND computes from the world or the connection (<c>view</c> from
    /// <c>world.view</c>, <c>eye</c> and <c>statobj</c> from <c>mob</c>, the key's vars) are set by the engine when the
    /// player connects (<see cref="dm_engine_create"/>, <see cref="dm_attach"/>): a constructor never touches the World
    /// (object-model.md section 3).
    /// </summary>
    public client()
    {
        gender = NEUTER;
        dir = NORTH;
        perspective = MOB_PERSPECTIVE;
        show_map = 1;
        show_popup_menus = 1;
        show_verb_panel = 1;
        preload_rsc = 1;
        default_verb_category = "Commands";
        authenticate = 1;
        dm_tg_vars();
    }

    // tg's overrides of /client's defaults, implemented in modules/client/client_defines.cs (rules 10.2, "Builtin types").
    private partial void dm_tg_vars();

    // ---- the player's identity: set by the engine before New, read-only in DM ----

    /// <summary>BYOND <c>key</c>: the player's key ("Once the player is attached to a mob M, M.key == M.client.key").</summary>
    public string key;
    /// <summary>BYOND <c>ckey</c>: the canonical form of <see cref="key"/> (<c>ckey(key)</c>).</summary>
    public string ckey;
    /// <summary>BYOND <c>address</c>: the player's network address, as the host gives it (D67: the port does not store it beyond the session).</summary>
    public string address;
    /// <summary>BYOND <c>computer_id</c>: a numeric text identifying the player's computer, as the host gives it.</summary>
    public string computer_id;
    /// <summary>BYOND <c>connection</c>: how the player is connected; the port's clients are <c>"seeker"</c>, which tg's <c>client/New</c> requires (client_procs.dm:259).</summary>
    public string connection;
    /// <summary>BYOND <c>byond_version</c>: the client's major BYOND version (the port's clients report <see cref="EngineByondVersion"/>).</summary>
    public float byond_version;
    /// <summary>BYOND <c>byond_build</c>: the client's BYOND build (the port's clients report <see cref="EngineByondBuild"/>).</summary>
    public float byond_build;
    /// <summary>BYOND <c>gender</c>: the key's gender.</summary>
    public string gender;
    /// <summary>BYOND <c>timezone</c>: the client's offset from UTC in hours (0 in the port).</summary>
    public float timezone;
    /// <summary>BYOND <c>inactivity</c>: ticks since the player's last action. The port does not count it (nothing tg runs in M1 reads it): always 0.</summary>
    public float inactivity;

    // ---- the mob and the view ----

    // The connected mob; hashed through this backing field (object-model.md section 8).
    private mob _mob;

    /// <summary>
    /// BYOND <c>mob</c>: the mob the player controls. Assigning it moves the player: Logout on the old mob (its <c>key</c>
    /// becomes null), Login on the new one, and the symmetry <c>mob.client == client</c> (BYOND ref <c>/client/var/mob</c>).
    /// </summary>
    public mob mob
    {
        get => _mob;
        set => dm_set_mob(value);
    }

    /// <summary>
    /// The engine's side of a <see cref="mob"/> assignment (BYOND ref <c>/client/var/mob</c>, <c>/mob/proc/Logout</c>):
    /// the player moves to <paramref name="value"/> at once (<see cref="dm_attach"/>), then BYOND calls Logout on the old
    /// mob and Login on the new one, each run as the engine runs a proc (detached: the assigning proc does not wait for a
    /// Login that sleeps), with <c>usr</c> that mob.
    /// </summary>
    private protected virtual void dm_set_mob(mob value)
    {
        if (value == _mob)
            return;
        mob old_mob = dm_attach(value);
        if (old_mob != null)
            dm_run_detached(old_mob, () => old_mob.Logout());
        if (value != null)
            dm_run_detached(value, () => value.Login());
    }

    /// <summary>BYOND <c>eye</c>: the center of the player's view (the mob unless changed; tg's <c>reset_perspective</c> sets it).</summary>
    public atom eye;
    /// <summary>BYOND <c>perspective</c> (default <c>MOB_PERSPECTIVE</c>).</summary>
    public float perspective;
    /// <summary>BYOND <c>view</c>: a number or "WIDTHxHEIGHT" text (default <c>world.view</c>, set at connection).</summary>
    public object view;
    /// <summary>BYOND <c>dir</c>: the map's rotation for the player (default <c>NORTH</c>; tg's <c>keyLoop</c> turns input by it).</summary>
    public float dir;
    /// <summary>BYOND <c>lazy_eye</c>.</summary>
    public float lazy_eye;
    /// <summary>BYOND <c>edge_limit</c>.</summary>
    public object edge_limit;
    /// <summary>BYOND <c>pixel_x</c>.</summary>
    public float pixel_x;
    /// <summary>BYOND <c>pixel_y</c>.</summary>
    public float pixel_y;
    /// <summary>BYOND <c>pixel_w</c>.</summary>
    public float pixel_w;
    /// <summary>BYOND <c>pixel_z</c>.</summary>
    public float pixel_z;
    /// <summary>BYOND <c>glide_size</c>.</summary>
    public float glide_size;
    /// <summary>BYOND <c>color</c>.</summary>
    public object color;
    /// <summary>BYOND <c>fps</c> (0: <c>world.fps</c>).</summary>
    public float fps;
    /// <summary>BYOND <c>tick_lag</c> (0: <c>world.tick_lag</c>).</summary>
    public float tick_lag;
    /// <summary>BYOND <c>mouse_pointer_icon</c>.</summary>
    public object mouse_pointer_icon;

    // ---- the client's UI (presentation, D44: the Godot client draws its own; kept as state tg reads and writes) ----

    /// <summary>BYOND <c>statobj</c> (default the mob, set at connection).</summary>
    public object statobj;
    /// <summary>BYOND <c>statpanel</c>.</summary>
    public string statpanel;
    /// <summary>BYOND <c>show_map</c> (default 1).</summary>
    public float show_map;
    /// <summary>BYOND <c>show_popup_menus</c> (default 1).</summary>
    public float show_popup_menus;
    /// <summary>BYOND <c>show_verb_panel</c> (default 1; tg sets FALSE).</summary>
    public float show_verb_panel;
    /// <summary>BYOND <c>control_freak</c> (default 0; tg sets 1).</summary>
    public float control_freak;
    /// <summary>BYOND <c>preload_rsc</c> (default 1; tg sets <c>PRELOAD_RSC</c>).</summary>
    public object preload_rsc;
    /// <summary>BYOND <c>default_verb_category</c> (default "Commands").</summary>
    public string default_verb_category;
    /// <summary>BYOND <c>authenticate</c> (default 1: hub authentication; the port authenticates by its own token).</summary>
    public float authenticate;
    /// <summary>BYOND <c>command_text</c> (deprecated in BYOND 4.0; default null).</summary>
    public string command_text;

    private readonly DmList _screen = new DmList { dm_unique = true }; // BYOND: an object is on a client's screen once
    /// <summary>
    /// BYOND <c>screen</c>: the screen objects (HUD) shown to the player; never null, assigning null clears it and assigning
    /// a list replaces its items (as the atom engine lists, rules 4.3, object-model.md section 2). Presentation: the M1
    /// client does not draw it.
    /// </summary>
    public DmList screen
    {
        get => _screen;
        set => dm_replace_list(_screen, value);
    }

    private readonly DmList _images = new DmList();
    /// <summary>BYOND <c>images</c>: the images shown to this player only (as <see cref="screen"/>).</summary>
    public DmList images
    {
        get => _images;
        set => dm_replace_list(_images, value);
    }

    /// <summary>
    /// BYOND <c>verbs</c>, M10x slice G6: the verbs tg adds and removes at run time (add_verb, remove_verb), by their proc paths. The
    /// port's other verbs are the client's typed commands (rules 7.10), always there; of those tg removes only the admin help's while a
    /// ticket waits, so a client starts with it.
    /// </summary>
    public DmList verbs { get; } = Dm.list(ADMINHELP_VERB);

    // BYOND bounds and virtual_eye: not declared until a translated caller needs them. CGI, script (compile-time skin settings) and pixel_step_size (renamed glide_size) are not ported.

    private static void dm_replace_list(DmList target, DmList value)
    {
        if (ReferenceEquals(value, target)) return;
        target.Cut();
        if (value != null) target.Add(value);
    }

    // ---- the engine: connecting, moving between mobs, disconnecting ----

    /// <summary>The BYOND version the port's clients report (rules 1.7: tg's build pins <c>DM_VERSION</c> 516).</summary>
    public const float EngineByondVersion = 516;

    /// <summary>The BYOND build the port's clients report (rules 1.7: <c>DM_BUILD</c> 1685, tg's dependencies.sh).</summary>
    public const float EngineByondBuild = 1685;

    // How far the engine's side of this client got: 0 for a client the engine did not make (Dm.@new of a /client, which
    // BYOND never does: the tests' fixture clients), then Connected (ClientSeam.Connect), Disconnecting (BYOND's Del is
    // running or queued) and Gone (BYOND's Del finished: the client is deleted). Hashed like any field.
    private int _dm_connection;

    private const int ConnectionConnected = 1;
    private const int ConnectionDisconnecting = 2;
    private const int ConnectionGone = 3;

    /// <summary>True while the engine counts this client as a connected player (from <c>ClientSeam.Connect</c> until a disconnect or a deletion starts).</summary>
    public bool dm_connected => _dm_connection == ConnectionConnected;

    /// <summary>True once BYOND's <c>/client/Del</c> finished: the player is gone and the client is deleted.</summary>
    public bool dm_gone => _dm_connection == ConnectionGone;

    /// <summary>
    /// Protocol version 14 (M4c slice 3): the thing the player's pointer is over, as BYOND's client knows it, the last target of
    /// <c>ClientSeam.Hover</c> (null after <c>ClientSeam.Unhover</c>): BYOND calls <c>MouseExited</c> on it when the mouse
    /// leaves it (BYOND ref <c>/atom/proc/MouseExited</c>). Engine state, hashed like any field.
    /// </summary>
    internal atom dm_mouse_over;

    /// <summary>
    /// DEVIATION(DV65): the turf of the last map thing the player's crosshair (or pointer) was over, kept when the pointer moves
    /// onto a window or the HUD (a stack's recipe window is open while the player picks what to build). The stack builds there
    /// (<c>obj_item_stack.build_turf</c>) instead of on the builder's own turf. Engine state, hashed like any field.
    /// </summary>
    internal turf dm_aim_turf;

    /// <summary>
    /// Protocol version 34: the thing the player's button went down over (<c>ClientSeam.MouseDown</c>), BYOND's <c>src_object</c> of the
    /// <c>MouseDrag</c> calls while the button is held; null after <c>ClientSeam.MouseUp</c>. Engine state, hashed like any field.
    /// </summary>
    public atom dm_mouse_down;

    /// <summary>DV348: what is under the crosshair while the button is held (the press's target, then each drag's); null after <c>ClientSeam.MouseUp</c>.</summary>
    public atom dm_mouse_held_over;

    /// <summary>
    /// DEVIATION(DV96): the client's last <c>animate()</c> chain is tg's <c>recoil_camera</c> kick (true) or <c>shake_camera</c>'s path
    /// (false): the replicator sends it as a CameraShake of that kind (protocol version 34), which the first-person client turns into a
    /// camera rotation instead of an eye shift. Engine state, hashed like any field.
    /// </summary>
    public bool dm_camera_recoil;

    /// <summary>
    /// The dev command <c>shoot WHO TYPE</c> (M7x slice A, for tests and playtests before the guns): the projectile type the player's clicks
    /// fire with their params (<c>DevCommands.FireAimed</c>), null for none. Engine state, hashed like any field.
    /// </summary>
    public Type dm_dev_shot;

    /// <summary>
    /// DEVIATION(DV15): the direction <c>client/Move</c>'s <c>direct</c> holds after its <c>COMSIG_MOB_CLIENT_PRE_MOVE</c> (a confusion
    /// scrambles it through the signal's <c>move_args</c>, which are the proc's <c>args</c>, so tg's <c>. = ..()</c> moves the mob the
    /// new way): the last <c>capsule_move_gate</c> that let the mob move leaves it here, and the locomotion bridge turns the capsule's
    /// wish to it. Engine state, hashed like any field.
    /// </summary>
    internal object dm_capsule_direct;

    /// <summary>DEVIATION(DV422): the Space press of a mob that could not change level held tg's throw mode, so its release lets it go.</summary>
    internal bool dm_level_key_threw;

    /// <summary>
    /// Protocol version 15 (M4c slice 4, DV40): the radial menus (<c>/datum/radial_menu</c>) shown to this client, oldest first.
    /// tg shows a menu by putting its holder image, whose <c>vis_contents</c> are the menu's slices, in <c>client.images</c>
    /// (<c>radial.dm:362</c>); the port's <c>show_to</c> lists the menu here instead and the replicator sends it to the client,
    /// which draws it at its crosshair. Engine state, hashed like any field (the menus by their ids).
    /// </summary>
    public DmList dm_radial_menus = new DmList();

    /// <summary>
    /// Protocol version 78 (silicons SI4, DV386): the popup windows open on this client (tg's <c>create_popup</c> clones BYOND's
    /// <c>popupwindow</c> skin element): the window's name to the list (title, width, height); the replicator sends the list when
    /// <see cref="dm_popups_serial"/> changes and the client opens each as a floating window with a second 3D view of its map
    /// (<c>name_map</c>). Engine state, hashed like any field.
    /// </summary>
    public DmList dm_popups = new DmList();

    /// <summary>Bumped at each change of <see cref="dm_popups"/>, so the replicator sends the list again.</summary>
    public long dm_popups_serial;

    /// <summary>
    /// BYOND makes the client of a connecting player (ClientSeam.Connect): the object with its world id and registration,
    /// the pre-New hook (tg's var block), then BYOND's own vars of the connection; tg's <c>client/New</c> is run by the
    /// caller afterwards (BYOND ref <c>/client/proc/New</c>: the vars are set when New is called).
    /// </summary>
    internal static client dm_engine_create(string key, string address, string computer_id)
    {
        World world = World.Current;
        // Engine code, not translated code (rules 10.1 binds translated code to Dm.@new): BYOND creates the client without
        // a `new` and calls New only once its vars are set, which Dm.@new cannot do (it calls New at once).
        var player = new client();
        long id = world.AllocateId();
        // SHIM-GAP: Dm.@new skips the world ids that would print as a list, alist, typepath or world ref
        // (DmRefs.IsReservedId, internal to the shim); the same rule, written out until the shim exposes it.
        while (dm_is_reserved_ref_id(id))
            id = world.AllocateId();
        player.dm_assign_id(id);
        world.Objects.Register(id, player);
        player.dm_pre_new(DmArgs.Empty);
        player.key = key;
        player.ckey = mob.dm_canonical_key(key);
        player.address = address;
        player.computer_id = computer_id;
        player.connection = "seeker";
        player.byond_version = EngineByondVersion;
        player.byond_build = EngineByondBuild;
        player.view = Dm.world.view;
        player._dm_connection = ConnectionConnected;
        Engine.Of(world).Connected.Add(player);
        return player;
    }

    // DmRefs.IsReservedId (src/Core/DmShim/DmRefs.cs): the top byte of a list (0x0F), alist (0x55), typepath (0x09) or
    // world (0x0E) ref.
    private static bool dm_is_reserved_ref_id(long id)
    {
        long kind = id & ~0x00FFFFFFL;
        return kind == 0x0F000000L || kind == 0x55000000L || kind == 0x09000000L || kind == 0x0E000000L;
    }

    /// <summary>
    /// Moves the player to <paramref name="new_mob"/> without calling Logout or Login (the caller does): the symmetry
    /// <c>client == mob.client</c>, <c>client.mob == mob</c>, <c>client.key == mob.key</c> (BYOND ref <c>/client/var/mob</c>);
    /// the old mob loses the key ("by the time Logout() is called, the original mob's key will be null", BYOND ref
    /// <c>/mob/proc/Logout</c>), any other mob with the key loses it (<c>/mob/var/key</c>), a client already on
    /// <paramref name="new_mob"/> loses its mob, and the eye moves to the new mob (<c>/client/var/eye</c>). The host is told
    /// (<see cref="IClientHost.ClientMobChanged"/>). Returns the old mob.
    /// </summary>
    internal mob dm_attach(mob new_mob)
    {
        mob old_mob = _mob;
        if (old_mob == new_mob)
            return old_mob;
        if (old_mob != null)
        {
            old_mob.client = null;
            old_mob.dm_raw_key(null);
        }
        if (new_mob != null)
        {
            client other = new_mob.client;
            if (other != null && other != this && other._mob == new_mob)
            {
                other._mob = null;
                ClientSeam.NotifyClientMobChanged(other, new_mob, null);
            }
            if (ckey != null)
            {
                foreach (mob keyed in dm_mobs_with_key(ckey))
                {
                    if (keyed != new_mob)
                        keyed.dm_raw_key(null);
                }
            }
            new_mob.dm_raw_key(key);
            new_mob.client = this;
        }
        _mob = new_mob;
        if (new_mob != null)
        {
            eye = new_mob;
            statobj = new_mob;
        }
        ClientSeam.NotifyClientMobChanged(this, old_mob, new_mob);
        return old_mob;
    }

    // Runs a proc of `who` as BYOND's engine runs one (a mob.key or client.mob assignment calling Logout or Login): with
    // usr = `who`, detached (Waiting.Detach: the caller continues at the proc's first sleep; its runtime is reported).
    private static void dm_run_detached(mob who, Func<DmTask<object>> call)
    {
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = who;
            Waiting.Detach(call());
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    /// <summary>
    /// BYOND's own <c>/client/New</c>, which tg's <c>. = ..()</c> reaches (client_procs.dm:369; rules 7.5). BYOND ref
    /// <c>/client/proc/New</c>, default action: look for an existing mob with the player's key and connect the player to
    /// it (<c>mob.Login()</c>); otherwise create a mob of type <c>world.mob</c> with the key's name and gender and connect
    /// to it; then call <c>client.Topic(TopicData)</c> when TopicData is not null; return the player's mob. The caller
    /// waits for Login. A client deleted while Login slept (tg's warning, login.dm:2-5) ends here: BYOND silently kills the
    /// procs whose src is a deleted object (BYOND ref <c>del</c>), so the rest of tg's <c>client/New</c> never runs.
    /// </summary>
    internal async DmTask<object> New__d0(object TopicData = null)
    {
        mob player_mob = dm_mob_with_key(ckey);
        if (player_mob == null)
        {
            // BYOND ref /world/var/mob: "Setting world.mob to 0 prevents the creation of default mobs."
            if (Dm.world.mob == null)
                return null;
            player_mob = await Dm.new_async(Dm.world.mob) as mob;
            player_mob.name = key;
            player_mob.gender = gender;
        }
        dm_attach(player_mob);
        await player_mob.Login();
        if (_dm_connection == ConnectionGone)
            throw new ClientDeletedKill(this);
        if (TopicData != null)
        {
            Dm.NotTranslated("code/modules/client/client_procs.dm:255", "BYOND's client/New: client.Topic(TopicData), a connection topic (Topic flows, translation-rules.md 13)");
            return _mob;
        }
        return _mob;
    }

    /// <summary>
    /// BYOND's own <c>/client/Del</c>, which tg's <c>return ..()</c> reaches (client_procs.dm:614). BYOND ref
    /// <c>/client/proc/Del</c>, default action: if the player is connected to a mob, call <c>mob.Logout()</c> to
    /// disconnect (the mob keeps its key: "Note that this does not automatically delete the player's mob"); if the
    /// player's connection is still not dead, kill it: the host is told (<see cref="IClientHost.ClientDeleted"/>) and the
    /// client, deleted, leaves the world registry.
    /// </summary>
    internal async DmTask Del__d0()
    {
        if (_dm_connection == ConnectionGone)
            return;
        mob player_mob = _mob;
        if (player_mob != null)
        {
            if (player_mob.client == this)
                player_mob.client = null;
            _mob = null;
            World world = World.Current;
            object caller_usr = world.usr;
            world.usr = player_mob;
            try
            {
                await player_mob.Logout();
            }
            finally
            {
                world.usr = caller_usr;
            }
        }
        bool engine_made = _dm_connection != 0;
        _dm_connection = ConnectionGone;
        if (engine_made)
        {
            Engine.Of(World.Current).Connected.Remove(this);
            ClientSeam.NotifyClientDeleted(this);
            World.Current.Objects.Unregister(dm_id);
        }
    }

    /// <summary>
    /// BYOND's own <c>/client/Move</c>, which tg's <c>. = ..()</c> reaches (mob_movement.dm:121). BYOND ref
    /// <c>/client/proc/Move</c>, default action: call <c>src.mob.Move()</c> and cancel any automated movement with
    /// <c>walk(usr, 0)</c>; returns 1 on success, 0 on failure.
    /// </summary>
    internal async DmTask<object> Move__d0(object loc = null, object dir = null)
    {
        // walk(usr, 0): BYOND's walk procs are not in the port (Appendix C, planned with the map builtins) and nothing
        // starts automated movement, so there is none to cancel.
        return await _mob.Move(loc as atom, dir);
    }

    /// <summary>
    /// BYOND's own <c>/client/Click</c>, which tg's <c>..()</c> calls (client_procs.dm:943; BYOND ref
    /// <c>/client/proc/Click</c>: "The default action is to call object.Click(location, control, params)"). Moved here from
    /// M2 P4's client_procs.click.cs (rules 7.5).
    /// </summary>
    internal async DmTask Click__d0(atom @object = null, atom location = null, object control = null, object @params = null)
    {
        if (@object != null)
            await @object.Click(location, control, @params);
    }

    /// <summary>
    /// BYOND's own <c>/client/MouseDrop</c>, which tg's <c>..()</c> calls (drag_drop.dm:199; BYOND ref <c>/client/proc/MouseDrop</c>: "The
    /// default action is to call src_object.MouseDrop(over_object,src_location,over_location,src_control,over_control,params)"; OpenDream's
    /// DMStandard Client.dm). Protocol version 17 (drag and drop).
    /// </summary>
    internal async DmTask MouseDrop__d0(atom src_object = null, atom over_object = null, object src_location = null, object over_location = null, object src_control = null, object over_control = null, object @params = null)
    {
        if (src_object != null)
            await src_object.MouseDrop(over_object, src_location, over_location, src_control, over_control, @params);
    }

    /// <summary>
    /// BYOND's own <c>/client/MouseDrag</c>, which tg's <c>return ..()</c> calls (drag_drop.dm:191; BYOND ref <c>/client/proc/MouseDrag</c>:
    /// the default calls src_object.MouseDrag(over_object,src_location,over_location,src_control,over_control,params); OpenDream's
    /// DMStandard Client.dm). Protocol version 17 (drag and drop).
    /// </summary>
    internal async DmTask MouseDrag__d0(atom src_object = null, atom over_object = null, object src_location = null, object over_location = null, object src_control = null, object over_control = null, object @params = null)
    {
        if (src_object != null)
            await src_object.MouseDrag(over_object, src_location, over_location, src_control, over_control, @params);
    }

    // The mob the engine attaches a connecting player to: the first mob in the world, in id order, whose ckey is the
    // player's (BYOND ref /client/proc/New; keys are compared in canonical form, /mob/var/key).
    internal static mob dm_mob_with_key(string canonical_key)
    {
        if (canonical_key == null)
            return null;
        List<mob> keyed = dm_mobs_with_key(canonical_key);
        return keyed.Count > 0 ? keyed[0] : null;
    }

    // The mobs of the world whose ckey is canonical_key, in the world's order (what for (var/mob/M in world) with that
    // check finds: live registered mobs, world order then id). M3 I1d (the join stall): a join made four scans of every
    // atom of the world (about 1M on MetaStation, 0.3 s a join); the engine's index of keyed mobs gives the same mobs. A
    // world whose index missed a mob (a ckey written before the mob had an id) keeps the scan.
    internal static List<mob> dm_mobs_with_key(string canonical_key)
    {
        var found = new List<mob>();
        if (canonical_key == null)
            return found;
        World world = World.Current;
        Engine engine = Engine.Of(world);
        if (engine.KeyedIncomplete)
        {
            foreach (mob keyed in Dm.each<mob>(Dm.world))
            {
                if (keyed.ckey == canonical_key)
                    found.Add(keyed);
            }
            return found;
        }
        engine.Keyed(canonical_key, found);
        return found;
    }

    // A mob's ckey was written (mob.dm_store_ckey): the index learns it. A mob without an id yet (still in its constructor)
    // cannot be indexed, so its world falls back to the scan.
    internal static void dm_index_keyed(mob keyed, string canonical_key)
    {
        World world = World.CurrentOrNull;
        if (world == null)
            return;
        Engine engine = Engine.Of(world);
        if (keyed.dm_id == 0)
            engine.KeyedIncomplete = true;
        else
            engine.AddKeyed(canonical_key, keyed.dm_id);
    }

    // The connected client of a key (one per key: BYOND lets a key connect once).
    internal static client dm_connected_client(string canonical_key)
    {
        if (canonical_key == null)
            return null;
        foreach (client player in Engine.Of(World.Current).Connected)
        {
            if (player._dm_connection == ConnectionConnected && player.ckey == canonical_key)
                return player;
        }
        return null;
    }

    /// <summary>
    /// ClientSeam.Disconnect: the player's connection ended (BYOND's side of a disconnect): BYOND calls
    /// <c>client/Del()</c> (BYOND ref <c>/client/proc/Del</c>, "Called when the player disconnects from the world"), here
    /// detached with <c>usr</c> the client's mob. False when the client is already disconnecting or gone.
    /// </summary>
    internal bool dm_disconnect()
    {
        if (_dm_connection != ConnectionConnected)
            return false;
        _dm_connection = ConnectionDisconnecting;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = _mob;
            Waiting.Detach(Del());
        }
        finally
        {
            world.usr = caller_usr;
        }
        return true;
    }

    /// <summary>
    /// BYOND's <c>del</c> of a client (BYOND ref <c>del</c>: "its Del() procedure is called"), which tg reaches with qdel
    /// (<c>client/Destroy</c> returns <c>QDEL_HINT_HARDDEL_NOW</c>, whose <c>del(D)</c> in <c>SSgarbage.HardDelete</c>
    /// calls <c>client/Del</c>: client_procs.dm:659, garbage.dm:292): tg's <c>client/Del</c> runs at once, detached with
    /// <c>usr</c> the client's mob, and disconnects the player. Nothing for a client the engine did not make or that is
    /// already disconnecting.
    /// </summary>
    internal void dm_del()
    {
        if (_dm_connection != ConnectionConnected)
            return;
        _dm_connection = ConnectionDisconnecting;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = _mob;
            Waiting.Detach(Del());
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    // BYOND's del of a mob that a connected client is attached to nulls every reference to it, the client's mob too (BYOND
    // ref del: "Destroy an object and null out all references to it"); the host is told that the client has no mob.
    private void dm_mob_deleted(mob deleted)
    {
        if (_dm_connection != ConnectionConnected || _mob != deleted)
            return;
        _mob = null;
        if (deleted.client == this)
            deleted.client = null;
        ClientSeam.NotifyClientMobChanged(this, deleted, null);
    }

    /// <summary>
    /// What ends tg's <c>client/New</c> when the client was deleted while BYOND's default action waited for a sleeping
    /// Login: BYOND silently kills a proc whose src is deleted (BYOND ref <c>del</c>). Only the engine's own call of
    /// <c>client/New</c> (ClientSeam.Connect) catches it, and it is not a runtime error.
    /// </summary>
    internal sealed class ClientDeletedKill : Exception
    {
        internal ClientDeletedKill(client player)
            : base("the client of " + player.key + " was deleted while its New slept (BYOND kills procs whose src is deleted)")
        {
        }
    }

    // The engine's per-world state of the clients (a Kernel extension: no static state). The list is in connection order;
    // every client in it is also a datum of the world, whose fields are hashed.
    private sealed class Engine
    {
        private readonly World _world;

        internal readonly List<client> Connected = new List<client>();

        // The engine's index of keyed mobs (M3 I1d): for each ckey, the ids of the mobs that were given it, a superset that
        // is checked on every read (the object must still be live and still have that ckey) and pruned there. Ids, not
        // references, so the index keeps nothing alive; derived from the mobs' ckeys, so not hashed.
        private readonly Dictionary<string, List<long>> _keyed = new Dictionary<string, List<long>>(StringComparer.Ordinal);

        internal bool KeyedIncomplete;

        internal void AddKeyed(string ckey, long id)
        {
            if (!_keyed.TryGetValue(ckey, out List<long> ids))
                _keyed[ckey] = ids = new List<long>(1);
            if (!ids.Contains(id))
                ids.Add(id);
        }

        // The live mobs with this ckey, in the world's order (dm_world_order, then id: Dm.each over world).
        internal void Keyed(string ckey, List<mob> into)
        {
            if (!_keyed.TryGetValue(ckey, out List<long> ids))
                return;
            for (int i = ids.Count - 1; i >= 0; i--)
            {
                if (_world.Objects.TryGet(ids[i], out object o) && o is mob m && m.ckey == ckey)
                    into.Add(m);
                else
                    ids.RemoveAt(i);
            }
            if (ids.Count == 0)
                _keyed.Remove(ckey);
            if (into.Count > 1)
                into.Sort(static (a, b) =>
                {
                    int byGroup = ((IDmAtom)a).dm_world_order.CompareTo(((IDmAtom)b).dm_world_order);
                    return byGroup != 0 ? byGroup : a.dm_id.CompareTo(b.dm_id);
                });
        }

        private Engine(World world)
        {
            _world = world;
            world.Objects.Unregistered += OnUnregistered;
        }

        internal static Engine Of(World world) => world.GetExtension(static w => new Engine(w));

        // Only records: the deletion is handled by a raw scheduler entry later in this tick (an isolation boundary). A deleted
        // client itself is handled inside qdel (SSgarbage.HardDelete calls dm_del).
        private void OnUnregistered(long id)
        {
            foreach (client player in Connected)
            {
                if (player._mob != null && player._mob.dm_id == id)
                {
                    mob deleted = player._mob;
                    _world.Scheduler.After(0, () => player.dm_mob_deleted(deleted));
                }
            }
        }
    }
}
