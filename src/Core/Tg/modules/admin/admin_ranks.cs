// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/admin_ranks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice I (D275): the admin ranks of tg's text files (config/admin_ranks.txt, config/admins.txt, which the host writes into the
// world's file seam). DEVIATION(DV193): the port's admins are tg's legacy system: the database's admin and rank tables
// (sync_ranks_with_db, sync_admins_with_db, the backup json) are not ported, SQL is rules 13; the admin proc call guards
// (IsAdminAdvancedProcCall) stay, and their alert is the proc call verbs' (not ported).

public partial class datum_controller_global_vars
{
    // tg: code/modules/admin/admin_ranks.dm:1  GLOBAL_LIST_EMPTY(admin_ranks) //list of all admin_rank datums
    public DmList admin_ranks;

    // tg: code/modules/admin/admin_ranks.dm:1
    public void InitGlobaladmin_ranks__d0()
    {
        admin_ranks = Dm.list();
        Dm.append(ref gvars_datum_init_order, "admin_ranks");
    }

    // tg: code/modules/admin/admin_ranks.dm:2  GLOBAL_PROTECT(admin_ranks)
    public virtual void InitGlobaladmin_ranks()
    {
        InitGlobaladmin_ranks__d0();
        gvars_datum_protected_varlist["admin_ranks"] = TRUE;
    }

    // tg: code/modules/admin/admin_ranks.dm:4  GLOBAL_LIST_EMPTY(protected_ranks) //admin ranks loaded from txt
    public DmList protected_ranks;

    // tg: code/modules/admin/admin_ranks.dm:4
    public void InitGlobalprotected_ranks__d0()
    {
        protected_ranks = Dm.list();
        Dm.append(ref gvars_datum_init_order, "protected_ranks");
    }

    // tg: code/modules/admin/admin_ranks.dm:5  GLOBAL_PROTECT(protected_ranks)
    public virtual void InitGlobalprotected_ranks()
    {
        InitGlobalprotected_ranks__d0();
        gvars_datum_protected_varlist["protected_ranks"] = TRUE;
    }
}

// tg: code/modules/admin/admin_ranks.dm:7
[DmPath("/datum/admin_rank")]
public partial class datum_admin_rank : datum
{
    /// Rank name, key'd to the db
    public string name = "NoRank";
    /// Rank source, see RANK_SOURCE_TXT and friends
    public object source = null;
    /// Our functional rights, these are what we actually use in game
    public float rights = R_DEFAULT;
    /// Rights we're allowed to use, pre filtering
    public float include_rights = NONE;
    /// These are the rights of include_rights we aren't allowed to use. Frankly I have no idea why this exists
    public float exclude_rights = NONE;
    /// Rights we're allowed to edit on other folks, impact of this is dependent on R_PERMISSIONS and R_DBRANKS
    public float can_edit_rights = NONE;

    // tg: code/modules/admin/admin_ranks.dm:21
    public override async DmTask New(params object[] __extra)
    {
        object init_name = __extra.Length > 0 ? __extra[0] : null;
        object init_source = __extra.Length > 1 ? __extra[1] : null;
        object init_rights = __extra.Length > 2 ? __extra[2] : null;
        object init_exclude_rights = __extra.Length > 3 ? __extra[3] : null;
        object init_edit_rights = __extra.Length > 4 ? __extra[4] : null;
        if (Dm.truthy(IsAdminAdvancedProcCall()))
        {
            Dm.NotTranslated("code/modules/admin/admin_ranks.dm:23", "alert_to_permissions_elevation_attempt (the admin proc call verbs, not ported)");
            return;
        }
        name = init_name as string;
        source = init_source;
        if (!Dm.truthy(source))
        {
            qdel(this);
            throw Dm.CRASH("Admin rank created without a source.", "code/modules/admin/admin_ranks.dm", 32);
        }
        if (!Dm.truthy(name))
        {
            qdel(this);
            throw Dm.CRASH("Admin rank created without name.", "code/modules/admin/admin_ranks.dm", 35);
        }
        if (Dm.truthy(init_rights))
            rights = Dm.num(init_rights);
        include_rights = rights;
        if (Dm.truthy(init_exclude_rights))
        {
            exclude_rights = Dm.num(init_exclude_rights);
            rights = (int)rights & ~(int)exclude_rights;
        }
        if (Dm.truthy(init_edit_rights))
            can_edit_rights = Dm.num(init_edit_rights);
    }

    // tg: code/modules/admin/admin_ranks.dm:45
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(IsAdminAdvancedProcCall()))
        {
            Dm.NotTranslated("code/modules/admin/admin_ranks.dm:47", "alert_to_permissions_elevation_attempt (the admin proc call verbs, not ported)");
            return QDEL_HINT_LETMELIVE;
        }
        return base.Destroy(force);
    }

    // Adds/removes rights to this admin_rank
    // tg: code/modules/admin/admin_ranks.dm:63
    public virtual void process_keyword(object group = null, object group_count = null, datum_admin_rank previous_rank = null)
    {
        if (Dm.truthy(IsAdminAdvancedProcCall()))
        {
            Dm.NotTranslated("code/modules/admin/admin_ranks.dm:65", "alert_to_permissions_elevation_attempt (the admin proc call verbs, not ported)");
            return;
        }
        DmList keywords = splittext(group, " ");
        int flag = 0;
        foreach (object k in Dm.each(keywords))
        {
            switch (k as string)
            {
                case "BUILD": flag = R_BUILD; break;
                case "ADMIN": flag = R_ADMIN; break;
                case "BAN": flag = R_BAN; break;
                case "FUN": flag = R_FUN; break;
                case "SERVER": flag = R_SERVER; break;
                case "DEBUG": flag = R_DEBUG; break;
                case "PERMISSIONS": flag = R_PERMISSIONS; break;
                case "POSSESS": flag = R_POSSESS; break;
                case "STEALTH": flag = R_STEALTH; break;
                case "POLL": flag = R_POLL; break;
                case "VAREDIT": flag = R_VAREDIT; break;
                case "EVERYTHING": flag = R_EVERYTHING; break;
                case "SOUND": flag = R_SOUND; break;
                case "SPAWN": flag = R_SPAWN; break;
                case "AUTOADMIN": flag = R_AUTOADMIN; break;
                case "DBRANKS": flag = R_DBRANKS; break;
                case "@":
                    if (previous_rank != null)
                    {
                        switch ((int)Dm.num(group_count))
                        {
                            case 1: flag = (int)previous_rank.include_rights; break;
                            case 2: flag = (int)previous_rank.exclude_rights; break;
                            case 3: flag = (int)previous_rank.can_edit_rights; break;
                        }
                    }
                    else
                    {
                        continue;
                    }
                    break;
            }
            switch ((int)Dm.num(group_count))
            {
                case 1:
                    rights = (int)rights | flag;
                    include_rights = (int)include_rights | flag;
                    break;
                case 2:
                    rights = (int)rights & ~flag;
                    exclude_rights = (int)exclude_rights | flag;
                    break;
                case 3:
                    can_edit_rights = (int)can_edit_rights | flag;
                    break;
            }
        }
    }
}

public static partial class GlobalProcs
{
    /// Loads admin ranks.
    ///	Return a list containing the backup data if they were loaded from the database backup json
    // tg: code/modules/admin/admin_ranks.dm:139
    public static object load_admin_ranks(object dbfail = null, object no_update = null)
    {
        if (Dm.truthy(IsAdminAdvancedProcCall()))
        {
            to_chat(usr, span_adminprefix("Admin Reload blocked: Advanced ProcCall detected."), confidential: TRUE);
            return null;
        }
        GLOB.admin_ranks.Cut();
        GLOB.protected_ranks.Cut();
        //load text from file and process each entry
        string ranks_text = file2text(Dm.text("[]/admin_ranks.txt", config.directory));
        datum_admin_rank previous_rank = null;
        regex admin_ranks_regex = Dm.@new<regex>(@"^Name\s*=\s*(.+?)\s*\n+Include\s*=\s*([\l @]*?)\s*\n+Exclude\s*=\s*([\l @]*?)\s*\n+Edit\s*=\s*([\l @]*?)\s*\n*$", "gm");
        while (Dm.truthy(admin_ranks_regex.Find(ranks_text)))
        {
            var txt_rank = Dm.@new<datum_admin_rank>(admin_ranks_regex.group[1], RANK_SOURCE_TXT);
            if (QDELETED(txt_rank))
                continue;
            int count = 1;
            foreach (object i in Dm.each(Dm.sub(admin_ranks_regex.group, admin_ranks_regex.group[1])))
            {
                if (Dm.truthy(i))
                    txt_rank.process_keyword(i, count, previous_rank);
                count++;
            }
            Dm.append(ref GLOB.admin_ranks, txt_rank);
            Dm.append(ref GLOB.protected_ranks, txt_rank);
            previous_rank = txt_rank;
        }
        // tg: :161-188  the ranks of the database's admin_ranks table (!admin_legacy_system): DV193, the port's admins are the
        // text files'; :189-206  the backup json when the database failed: the same
        return null;
    }

    /// Converts a rank name (such as "Coder+Moth") into a list of /datum/admin_rank
    // tg: code/modules/admin/admin_ranks.dm:219
    public static DmList ranks_from_rank_name(object rank_name = null)
    {
        DmList rank_names = splittext(rank_name, "+");
        DmList ranks = Dm.list();

        foreach (datum_admin_rank rank in Dm.each_cast<datum_admin_rank>(GLOB.admin_ranks))
        {
            if (Dm.@in(rank.name, rank_names))
            {
                Dm.remove(ref rank_names, rank.name);
                Dm.append(ref ranks, rank);

                if (rank_names.len == 0)
                    break;
            }
        }

        if (rank_names.len > 0)
            log_config(Dm.text("Admin rank names were invalid: []", jointext(ranks, ", ")));

        return ranks;
    }

    /// Takes a list of rank names and joins them with +
    // tg: code/modules/admin/admin_ranks.dm:237
    public static object join_admin_ranks(DmList ranks = null)
    {
        DmList names = Dm.list();

        foreach (datum_admin_rank rank in Dm.each_cast<datum_admin_rank>(ranks))
            Dm.append(ref names, rank.name);

        return jointext(names, "+");
    }

    /// (Re)Loads the admin list.
    /// returns TRUE if database admins had to be loaded from the backup json
    // tg: code/modules/admin/admin_ranks.dm:247
    public static object load_admins(object no_update = null, object initial = null)
    {
        initial ??= FALSE;
        if (!Dm.truthy(initial))
        {
            // tg: :249-250  if(!global.config.PreConfigReload()) return: the config reload of the Reload Admins verb (admin verbs, not ported)
            Dm.NotTranslated("code/modules/admin/admin_ranks.dm:249", "load_admins: the configuration's PreConfigReload (the Reload Admins verb, not ported)");
            return null;
        }

        object dbfail = null;
        // tg: :253-256  if(!CONFIG_GET(flag/admin_legacy_system) && !SSdbcore.Connect()) the database's failure: DV193, the legacy system
        //clear the datums references
        GLOB.admin_datums.Cut();
        foreach (client C in Dm.each_cast<client>(GLOB.admins))
        {
            // C.remove_admin_verbs(): the admin verbs (not ported)
            C.holder = null;
        }
        GLOB.admins.Cut();
        GLOB.protected_admins.Cut();
        GLOB.deadmins.Cut();
        object backup_file_json = load_admin_ranks(dbfail, no_update);
        // tg: :265  dbfail = backup_file_json != null: always null here (DV193)
        //Clear profile access
        // tg: :267-268  world.SetConfig("APP/admin", A, null): BYOND's profiler access (not ported)
        DmList rank_names = Dm.list();
        foreach (datum_admin_rank R in Dm.each_cast<datum_admin_rank>(GLOB.admin_ranks))
            rank_names[R.name] = R;
        //ckeys listed in admins.txt are always made admins before sql loading is attempted
        string admins_text = file2text(Dm.text("[]/admins.txt", config.directory));
        regex admins_regex = Dm.@new<regex>(@"^(?!#)(.+?)\s+=\s+(.+)", "gm");

        while (Dm.truthy(admins_regex.Find(admins_text)))
        {
            object admin_key = admins_regex.group[1];
            object admin_rank = admins_regex.group[2];
            Dm.@new<datum_admins>(ranks_from_rank_name(admin_rank), ckey(admin_key), FALSE, TRUE);
        }

        // tg: :282-306  the database's admin table and the admins backup (DV193)
        return dbfail;
    }
}
