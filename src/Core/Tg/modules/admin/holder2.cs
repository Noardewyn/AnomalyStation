// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/holder2.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice I, D275): the admin holder: its creation, activation and association with the client, its rights
// (check_for_rights, rank_flags, check_rights, check_rights_for), and (M10x slice G6) GLOB.href_token, RawHrefToken and HrefToken
// for the admin help's links. Left out: HrefTokenFormField (the admin panels' forms, not ported), the database's feedback link, the 2FA of the database's admin connections (tg passes every
// connection without CONFIG admin_2fa_url, the port's case), the profiler access and the debug datums (plane, appearance, path).

public partial class datum_controller_global_vars
{
    // tg: code/modules/admin/holder2.dm:1  GLOBAL_LIST_EMPTY(admin_datums)
    public DmList admin_datums;

    // tg: code/modules/admin/holder2.dm:1  (GLOBAL_MANAGED(admin_datums, list()); the earlier of two definitions on this type, rules 7.5)
    public void InitGlobaladmin_datums__d0()
    {
        admin_datums = Dm.list();
        Dm.append(ref gvars_datum_init_order, "admin_datums");
    }

    // tg: code/modules/admin/holder2.dm:2  GLOBAL_PROTECT(admin_datums) (__DEFINES/__globals.dm:15)
    public virtual void InitGlobaladmin_datums()
    {
        InitGlobaladmin_datums__d0();
        gvars_datum_protected_varlist["admin_datums"] = TRUE;
    }

    // tg: code/modules/admin/holder2.dm:6  GLOBAL_VAR_INIT(href_token, GenerateToken()) (M10x slice G6: the admin help's links)
    public object href_token;

    // tg: code/modules/admin/holder2.dm:6
    public void InitGlobalhref_token__d0()
    {
        href_token = GenerateToken();
        Dm.append(ref gvars_datum_init_order, "href_token");
    }

    // tg: code/modules/admin/holder2.dm:7  GLOBAL_PROTECT(href_token) (__DEFINES/__globals.dm:15)
    public virtual void InitGlobalhref_token()
    {
        InitGlobalhref_token__d0();
        gvars_datum_protected_varlist["href_token"] = TRUE;
    }

    // tg: code/modules/admin/holder2.dm:3  GLOBAL_LIST_EMPTY(protected_admins)
    public DmList protected_admins;

    // tg: code/modules/admin/holder2.dm:3
    public void InitGlobalprotected_admins__d0()
    {
        protected_admins = Dm.list();
        Dm.append(ref gvars_datum_init_order, "protected_admins");
    }

    // tg: code/modules/admin/holder2.dm:4  GLOBAL_PROTECT(protected_admins)
    public virtual void InitGlobalprotected_admins()
    {
        InitGlobalprotected_admins__d0();
        gvars_datum_protected_varlist["protected_admins"] = TRUE;
    }
}

// The [DmPath] of /datum/admins is on admin.cs (the earliest .dme file that declares it, rules 2.3).
// tg: code/modules/admin/holder2.dm:12
public partial class datum_admins : datum
{
    // tg: code/modules/admin/holder2.dm:18
    public object fakekey = null;

    // tg: code/modules/admin/holder2.dm:56
    public override async DmTask New(params object[] __extra)
    {
        var ranks = __extra.Length > 0 ? __extra[0] as DmList : null;
        object ckey = __extra.Length > 1 ? __extra[1] : null;
        object force_active = __extra.Length > 2 ? __extra[2] : FALSE;
        object @protected = __extra.Length > 3 ? __extra[3] : null;
        if (Dm.truthy(IsAdminAdvancedProcCall()))
        {
            Dm.NotTranslated("code/modules/admin/holder2.dm:58", "alert_to_permissions_elevation_attempt (the admin proc call verbs, not ported)");
            return;
        }
        if (!Dm.truthy(ckey))
        {
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this), 0, file: "code/modules/admin/holder2.dm", line: 64); // QDEL_IN(src, 0), inline (rules 11.1 (f))
            throw Dm.CRASH("Admin datum created without a ckey", "code/modules/admin/holder2.dm", 65);
        }
        if (!istype(__extra.Length > 0 ? __extra[0] : null, typeof(DmList)))
        {
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this), 0, file: "code/modules/admin/holder2.dm", line: 67); // QDEL_IN(src, 0), inline (rules 11.1 (f))
            throw Dm.CRASH(Dm.text("Admin datum created with invalid ranks: [] ([])", ranks, json_encode(ranks)), "code/modules/admin/holder2.dm", 68);
        }
        target = ckey;
        name = Dm.text("[]'s admin datum ([])", ckey, join_admin_ranks(ranks));
        this.ranks = ranks;
        admin_signature = Dm.text("Nanotrasen Officer #[][][]", rand(0, 9), rand(0, 9), rand(0, 9));
        href_token = GenerateToken();
        //only admins with +ADMIN start admined
        if (Dm.truthy(@protected))
            GLOB.protected_admins[target] = this;
        if (Dm.truthy(force_active) || ((int)Dm.num(rank_flags()) & R_AUTOADMIN) != 0)
            activate();
        else
            deactivate();
    }

    // tg: code/modules/admin/holder2.dm:94
    public virtual void activate()
    {
        if (Dm.truthy(IsAdminAdvancedProcCall()))
        {
            Dm.NotTranslated("code/modules/admin/holder2.dm:96", "alert_to_permissions_elevation_attempt (the admin proc call verbs, not ported)");
            return;
        }
        Dm.remove(ref GLOB.deadmins, target);
        GLOB.admin_datums[target] = this;
        deadmined = FALSE;
        // tg: :101-102  plane_debug = new(src); appearance_debug = new(src): the admin debug tools (not ported)
        if (Dm.truthy(GLOB.directory[target]))
            associate(GLOB.directory[target] as client); //find the client for a ckey if they are connected and associate them with us
    }

    // tg: code/modules/admin/holder2.dm:107
    public virtual void deactivate()
    {
        if (Dm.truthy(IsAdminAdvancedProcCall()))
        {
            Dm.NotTranslated("code/modules/admin/holder2.dm:109", "alert_to_permissions_elevation_attempt (the admin proc call verbs, not ported)");
            return;
        }
        GLOB.deadmins[target] = this;
        Dm.remove(ref GLOB.admin_datums, target);
        // tg: :113-115  QDEL_NULL(plane_debug), QDEL_NULL(appearance_debug), QDEL_NULL(path_debug): the admin debug tools (not ported)
        deadmined = TRUE;

        client client = owner ?? GLOB.directory[target] as client;

        if (client != null)
        {
            disassociate();
            // tg: :122  ASSIGN_GAME_VERB(client, /client, readmin): the admin verbs (not ported, the console's commands stand in)
            client.disable_combo_hud();
            client.update_special_keybinds();
            // tg: :125  client.set_stat_panel(): the stat panel (not ported)
        }
    }

    // tg: code/modules/admin/holder2.dm:127
    public virtual void associate(client client = null)
    {
        if (Dm.truthy(IsAdminAdvancedProcCall()))
        {
            Dm.NotTranslated("code/modules/admin/holder2.dm:129", "alert_to_permissions_elevation_attempt (the admin proc call verbs, not ported)");
            return;
        }

        if (client == null)
            return;

        if (!Dm.eq(client?.ckey, target))
        {
            string msg = Dm.text(" has attempted to associate with []'s admin datum", target);
            message_admins(Dm.text("[][]", key_name_admin(client), msg));
            log_admin(Dm.text("[][]", key_name(client), msg));
            return;
        }

        DmList result_2fa = check_2fa(client);
        if (!Dm.truthy(result_2fa[RESULT_2FA_VALID]))
        {
            // tg: :143-147  blocked_by_2fa, alert_2fa_necessary, start_2fa_process: the database's admin connections (never reached
            // without CONFIG admin_2fa_url)
            Dm.NotTranslated("code/modules/admin/holder2.dm:143", "associate: the 2FA of an admin connection (the database, rules 13)");
            return;
        }
        // tg: :148-150  else if (blocked_by_2fa) sync_lastadminrank(...): the database (never: blocked_by_2fa stays FALSE)

        blocked_by_2fa = FALSE;

        if (Dm.truthy(deadmined))
            activate();

        // tg: :157  UNASSIGN_GAME_VERB(client, /client, admin_2fa_verify): the verbs (not ported)

        owner = client;
        owner.holder = this;
        // tg: :161-163  owner.add_admin_verbs(), UNASSIGN_GAME_VERB(owner, /client, readmin), owner.init_verbs(): the admin verbs (not
        // ported; the port's admin commands are the server's console, D275)
        owner.update_special_keybinds();
        Dm.combine(ref GLOB.admins, client);
        // tg: :166  client.set_stat_panel(): the stat panel (not ported)

        // tg: :168  try_give_profiling(): BYOND's profiler access (world.SetConfig, not ported)
    }

    // tg: code/modules/admin/holder2.dm:170
    public virtual void disassociate()
    {
        if (Dm.truthy(IsAdminAdvancedProcCall()))
        {
            Dm.NotTranslated("code/modules/admin/holder2.dm:172", "alert_to_permissions_elevation_attempt (the admin proc call verbs, not ported)");
            return;
        }
        if (owner != null)
        {
            Dm.remove(ref GLOB.admins, owner);
            // owner.remove_admin_verbs(): the admin verbs (not ported)
            owner.holder = null;
            owner = null;
        }
    }

    // tg: code/modules/admin/holder2.dm:212
    public virtual object check_for_rights(object rights_required = null)
    {
        if (Dm.truthy(rights_required) && ((int)Dm.num(rights_required) & (int)Dm.num(rank_flags())) == 0)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/admin/holder2.dm:217
    public virtual object check_if_greater_rights_than_holder(datum_admins other = null)
    {
        if (other == null)
            return TRUE; //they have no rights
        int mine = (int)Dm.num(rank_flags()), theirs = (int)Dm.num(other.rank_flags());
        if (mine == R_EVERYTHING)
            return TRUE; //we have all the rights
        if (this == other)
            return TRUE; //you always have more rights than yourself
        if (mine != theirs)
        {
            if ((mine & theirs) == theirs)
                return TRUE; //we have all the rights they have and more
        }
        return FALSE;
    }

    /// Returns whether or not the given client has a verified 2FA connection.
    // tg: code/modules/admin/holder2.dm:236
    public virtual DmList check_2fa(client client = null)
    {
        if (Dm.truthy(bypass_2fa))
            return Dm.list(TRUE, null); // VALID_2FA_CONNECTION

        object admin_2fa_url = config.Get(typeof(datum_config_entry_string_admin_2fa_url));

        // 2FA not being enabled == everyone passes
        if (admin_2fa_url == null || Dm.eq(admin_2fa_url, ""))
            return Dm.list(TRUE, null);

        // tg: :247-284  the database's admin connections and the local cache (rules 13)
        Dm.NotTranslated("code/modules/admin/holder2.dm:247", "check_2fa: the database's admin connections (rules 13)");
        return Dm.list(TRUE, null);
    }

    /// Get the rank name of the admin
    // tg: code/modules/admin/holder2.dm:424
    public virtual object rank_names() => join_admin_ranks(ranks);

    /// Get the rank flags of the admin
    // tg: code/modules/admin/holder2.dm:428
    public virtual object rank_flags()
    {
        int combined_flags = NONE;

        foreach (datum_admin_rank rank in Dm.each_cast<datum_admin_rank>(ranks))
            combined_flags |= (int)rank.rights;

        return combined_flags;
    }

    /// Get the permissions this admin is allowed to edit on other ranks
    // tg: code/modules/admin/holder2.dm:437
    public virtual object can_edit_rights_flags()
    {
        int combined_flags = NONE;

        foreach (datum_admin_rank rank in Dm.each_cast<datum_admin_rank>(ranks))
            combined_flags |= (int)rank.can_edit_rights;

        return combined_flags;
    }

    private const int RESULT_2FA_VALID = 1;
}

// tg: code/modules/admin/holder2.dm:475 (interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    /*
    checks if usr is an admin with at least ONE of the flags in rights_required. (Note, they don't need all the flags)
    if rights_required == 0, then it simply checks if they are an admin.
    if it doesn't return 1 and show_msg=1 it will prints a message explaining why the check has failed
    */
    // tg: code/modules/admin/holder2.dm:475
    public static object check_rights(object rights_required = null, object show_msg = null)
    {
        show_msg ??= 1;
        if (usr?.client != null)
        {
            if (Dm.truthy(check_rights_for(usr.client, rights_required)))
                return TRUE;
            if (Dm.truthy(show_msg))
                to_chat(usr, Dm.text("<font color='red'>Error: You do not have sufficient rights to do that. You require one of the following flags:[].</font>", rights2text(rights_required, " ")), confidential: TRUE);
        }
        return FALSE;
    }

    //This proc checks whether subject has at least ONE of the rights specified in rights_required.
    // tg: code/modules/admin/holder2.dm:494
    public static object check_rights_for(client subject = null, object rights_required = null)
    {
        if (subject?.holder != null)
            return subject.holder.check_for_rights(rights_required);
        return FALSE;
    }

    // tg: code/modules/admin/holder2.dm:504
    public static object RawHrefToken(object forceGlobal = null)
    {
        object tok = GLOB.href_token;
        if (!Dm.truthy(forceGlobal) && usr != null)
        {
            client C = usr.client;
            if (C == null)
                throw Dm.CRASH("No client for HrefToken()!", "code/modules/admin/holder2.dm", 509);
            datum_admins holder = C.holder;
            if (holder != null)
                tok = holder.href_token;
        }
        return tok;
    }

    // tg: code/modules/admin/holder2.dm:515
    public static string HrefToken(object forceGlobal = null) => Dm.text("admin_token=[]", RawHrefToken(forceGlobal));

    // tg: code/modules/admin/holder2.dm:499
    public static object GenerateToken()
    {
        string dot = "";
        for (int I = 1; I <= 32; I++)
            dot += Dm.text("[]", rand(10));
        return dot;
    }
}
