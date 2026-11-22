// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/persistent_client.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the W4.0 contracts declared the type; the M1 contracts step translated the GLOB lists and the var
// block; M1 P1 the procs).

public partial class datum_controller_global_vars
{
    ///assoc list of ckey -> /datum/persistent_client
    // tg: code/modules/client/persistent_client.dm:3  GLOBAL_LIST_EMPTY_TYPED(persistent_clients_by_ckey, /datum/persistent_client)
    public DmList persistent_clients_by_ckey;

    // tg: code/modules/client/persistent_client.dm:3  (GLOBAL_MANAGED(persistent_clients_by_ckey, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpersistent_clients_by_ckey()
    {
        persistent_clients_by_ckey = Dm.list();
        Dm.append(ref gvars_datum_init_order, "persistent_clients_by_ckey");
    }

    /// A flat list of all persistent clients, for her looping pleasure.
    // tg: code/modules/client/persistent_client.dm:5  GLOBAL_LIST_EMPTY_TYPED(persistent_clients, /datum/persistent_client)
    public DmList persistent_clients;

    // tg: code/modules/client/persistent_client.dm:5  (GLOBAL_MANAGED(persistent_clients, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpersistent_clients()
    {
        persistent_clients = Dm.list();
        Dm.append(ref gvars_datum_init_order, "persistent_clients");
    }
}

/// Tracks information about a client between log in and log outs
// tg: code/modules/client/persistent_client.dm:8
[DmPath("/datum/persistent_client")]
public partial class datum_persistent_client : datum
{
    /// The true client
    // tg: code/modules/client/persistent_client.dm:10
    public client client;
    /// The mob this persistent client is currently bound to.
    // tg: code/modules/client/persistent_client.dm:12
    public mob mob;

    /// Major version of BYOND this client was last using.
    // tg: code/modules/client/persistent_client.dm:15
    public object byond_version;
    /// Build number of BYOND this client was last using.
    // tg: code/modules/client/persistent_client.dm:17
    public object byond_build;

    /// Action datums assigned to this player
    // tg: code/modules/client/persistent_client.dm:20 (list(): dm_init_vars below)
    public DmList player_actions;
    /// Tracks client action logging
    // tg: code/modules/client/persistent_client.dm:22 (list(): dm_init_vars below)
    public DmList logging;

    /// Callbacks invoked when this client logs in again
    // tg: code/modules/client/persistent_client.dm:25 (list(): dm_init_vars below)
    public DmList post_login_callbacks;
    /// Callbacks invoked when this client logs out
    // tg: code/modules/client/persistent_client.dm:27 (list(): dm_init_vars below)
    public DmList post_logout_callbacks;

    /// List of names this key played under this round
    /// assoc list of name -> mob tag
    // tg: code/modules/client/persistent_client.dm:31 (list(): dm_init_vars below)
    public DmList played_names;
    /// Lazylist of preference slots this client has joined the round under
    /// Numbers are stored as strings
    // tg: code/modules/client/persistent_client.dm:34
    public DmList joined_as_slots;

    /// Tracks achievements they have earned
    // tg: code/modules/client/persistent_client.dm:37
    public datum_achievement_data achievements;

    /// World.time this player last died
    // tg: code/modules/client/persistent_client.dm:40
    public float time_of_death = 0;

    // The object-creating defaults of the var block (rules 10.2), in DM declaration order.
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        // tg: code/modules/client/persistent_client.dm:20
        player_actions = Dm.list();
        // tg: code/modules/client/persistent_client.dm:22
        logging = Dm.list();
        // tg: code/modules/client/persistent_client.dm:25
        post_login_callbacks = Dm.list();
        // tg: code/modules/client/persistent_client.dm:27
        post_logout_callbacks = Dm.list();
        // tg: code/modules/client/persistent_client.dm:31
        played_names = Dm.list();
    }

    // tg: code/modules/client/persistent_client.dm:42
    [DmParams("ckey")]
    public override async DmTask New(params object[] __extra)
    {
        object ckey = Dm.arg(__extra, 0);
        achievements = Dm.@new<datum_achievement_data>(ckey);
        GLOB.persistent_clients_by_ckey[ckey] = this;
        Dm.append(ref GLOB.persistent_clients, this);
    }

    // tg: code/modules/client/persistent_client.dm:47
    // Rules 9.3: `.` survives the CRASH (QDEL_HINT_LETMELIVE keeps the persistent client alive).
    public override object Destroy(float? force = null)
    {
        object dot = null;
        try
        {
            dot = QDEL_HINT_LETMELIVE;
            throw Dm.CRASH("Who the FUCK tried to delete a persistent client? Get your head checked you leadskull.", "code/modules/client/persistent_client.dm", 50);
        }
        catch (Exception e) { World.Current.ReportRuntime(e); return dot; } // DM: a runtime ends only this proc and returns '.'
    }

    /// Setter for the client var, updates any vars we have that might be dependent on client state
    // tg: code/modules/client/persistent_client.dm:53
    public virtual void set_client(client new_client = null)
    {
        if (client == new_client)
            return;

        if (Dm.truthy(client))
            client.persistent_client = null;
        client = new_client;
        if (Dm.truthy(client))
        {
            client.persistent_client = this;
            byond_build = client.byond_build;
            byond_version = client.byond_version;
        }
    }

    /// Setter for the mob var, handles both references.
    // tg: code/modules/client/persistent_client.dm:66
    public virtual void set_mob(mob new_mob = null)
    {
        if (mob == new_mob)
            return;

        if (mob != null)
            mob.persistent_client = null;
        new_mob?.persistent_client?.set_mob(null);

        mob = new_mob;
        if (new_mob != null)
            new_mob.persistent_client = this;
    }

    /// Writes all of the `played_names` into an HTML-escaped string.
    // tg: code/modules/client/persistent_client.dm:77
    public virtual object get_played_names()
    {
        DmList previous_names = Dm.list();
        foreach (object previous_name in Dm.each(played_names))
            Dm.append(ref previous_names, html_encode(Dm.text("[] ([])", previous_name, played_names[previous_name])));
        return previous_names.Join("; ");
    }

    /// Returns the full version string (i.e 515.1642) of the BYOND version and build.
    // tg: code/modules/client/persistent_client.dm:84
    public virtual object full_byond_version()
    {
        if (!Dm.truthy(byond_version))
            return "Unknown";
        return Dm.text("[].[]", byond_version, (byond_build is var or__1 && Dm.truthy(or__1) ? or__1 : "xxx"));
    }
}

public static partial class GlobalProcs
{
    /// Adds the new names to the player's played_names list on their /datum/persistent_client for use of admins.
    /// `ckey` should be their ckey, and `data` should be an associative list with the keys being the names they played under and the values being the unique mob ID tied to that name.
    // tg: code/modules/client/persistent_client.dm:91
    public static void log_played_names(object ckey = null, object data = null)
    {
        if (!Dm.truthy(ckey))
            return;

        datum_persistent_client writable = GLOB.persistent_clients_by_ckey[ckey] as datum_persistent_client;
        if (writable == null)
            return;

        foreach (object name in Dm.each(data))
        {
            if (!Dm.truthy(name))
                continue;
            object mob_tag = ((DmList)data)[name];
            object encoded_name = html_encode(name);
            if (Dm.truthy(writable.played_names.Find(Dm.text("[]", encoded_name))))
                continue;

            Dm.append(ref writable.played_names, Dm.list(Dm.kv(Dm.text("[]", encoded_name), mob_tag)));
        }
    }
}
