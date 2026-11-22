// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/ntdownloader.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: NtOS's app store): the NT Software Hub app, its stepwise download at the NTNet signal's speed and its
// NtosNetDownloader window. The apps of other milestones are listed and downloaded as tg lists them, as generated-only programs
// (not-translated.md 74). The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/ntdownloader.dm:1
public partial class datum_computer_file_program_ntnetdownload : datum_computer_file_program
{
    ///The list of categories to display in the UI, in order of which they appear.
    // tg: code/modules/modular_computers/file_system/programs/ntdownloader.dm:25  var/static/list/show_categories
    public DmList show_categories => TgState.Current.datum_computer_file_program_ntnetdownload__show_categories ??= Dm.list(
        PROGRAM_CATEGORY_DEVICE,
        PROGRAM_CATEGORY_EQUIPMENT,
        PROGRAM_CATEGORY_GAMES,
        PROGRAM_CATEGORY_SECURITY,
        PROGRAM_CATEGORY_ENGINEERING,
        PROGRAM_CATEGORY_SUPPLY,
        PROGRAM_CATEGORY_SCIENCE,
        PROGRAM_CATEGORY_SERVICE);

    // tg: code/modules/modular_computers/file_system/programs/ntdownloader.dm:36
    public override object kill_program(mob user = null)
    {
        abort_file_download();
        ui_header = null;
        return base.kill_program(user);
    }

    // tg: code/modules/modular_computers/file_system/programs/ntdownloader.dm:41
    public virtual object begin_file_download(object filename = null, mob user = null)
    {
        if (downloaded_file != null)
            return FALSE;

        datum_computer_file_program PRG = SSmodular_computers.find_ntnet_file_by_name(filename);

        if (PRG == null)
            return FALSE;

        // Attempting to download antag only program, but without having emagged/syndicate computer. No.
        if (Dm.truthy(Dm.bitand(PRG.program_flags, PROGRAM_ON_SYNDINET_STORE)) && !Dm.truthy(Dm.bitand(computer.obj_flags, EMAGGED)))
            return FALSE;

        if (computer == null || !Dm.truthy(computer.can_store_file(PRG)))
            return FALSE;

        ui_header = "downloader_running.gif";

        if (Dm.@in(PRG, SSmodular_computers.available_station_software))
        {
            generate_network_log(Dm.text("Began downloading file [].[] from NTNet Software Repository.", PRG.filename, PRG.filetype));
            hacked_download = FALSE;
        }
        else if (Dm.@in(PRG, SSmodular_computers.available_antag_software))
        {
            generate_network_log(Dm.text("Began downloading file **ENCRYPTED**.[] from unspecified server.", PRG.filetype));
            hacked_download = TRUE;
        }
        else
        {
            generate_network_log(Dm.text("Began downloading file [].[] from unspecified server.", PRG.filename, PRG.filetype));
            hacked_download = FALSE;
        }

        downloaded_file = PRG.clone() as datum_computer_file_program;
        download_user = WEAKREF(user) as datum_weakref;

        // If the filesize is 0 (or somehow lower), we instantly download to avoid invalid number issues with stepwise download.
        if (Dm.num(downloaded_file.size) <= 0)
            complete_file_download();
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/ntdownloader.dm:76
    public virtual void abort_file_download()
    {
        if (downloaded_file == null)
            return;
        generate_network_log(Dm.text("Aborted download of file [].", Dm.truthy(hacked_download) ? "**ENCRYPTED**" : Dm.text("[].[]", downloaded_file.filename, downloaded_file.filetype)));
        downloaded_file = null;
        download_completion = FALSE;
        ui_header = null;
        download_user = null;
    }

    // tg: code/modules/modular_computers/file_system/programs/ntdownloader.dm:85
    public virtual void complete_file_download()
    {
        if (downloaded_file == null)
            return;
        generate_network_log(Dm.text("Completed download of file [].", Dm.truthy(hacked_download) ? "**ENCRYPTED**" : Dm.text("[].[]", downloaded_file.filename, downloaded_file.filetype)));
        if (computer == null || !Dm.truthy(computer.store_file(downloaded_file, download_user?.resolve() as mob)))
        {
            // The download failed
            downloaderror = I18n.T("I/O ERROR - Unable to save file. Check whether you have enough free space on your hard drive and whether your hard drive is properly connected. If the issue persists contact your system administrator for assistance.");
        }
        downloaded_file = null;
        download_completion = FALSE;
        ui_header = "downloader_finished.gif";
        download_user = null;
    }

    // tg: code/modules/modular_computers/file_system/programs/ntdownloader.dm:97
    public override object process_tick(object seconds_per_tick = null)
    {
        if (downloaded_file == null)
            return null;
        if (Dm.num(download_completion) >= Dm.num(downloaded_file.size))
        {
            complete_file_download();
            return null;
        }
        // Download speed according to connectivity state. NTNet server is assumed to be on unlimited speed so we're limited by our local connectivity
        float download_netspeed = 0;
        // Speed defines are found in misc.dm
        if (ntnet_status == NTNET_LOW_SIGNAL)
            download_netspeed = NTNETSPEED_LOWSIGNAL;
        else if (ntnet_status == NTNET_GOOD_SIGNAL)
            download_netspeed = NTNETSPEED_HIGHSIGNAL;
        else if (ntnet_status == NTNET_ETHERNET_SIGNAL)
            download_netspeed = NTNETSPEED_ETHERNET;
        if (download_netspeed <= 0)
            return null;
        if (HAS_TRAIT(computer, TRAIT_MODPC_HALVED_DOWNLOAD_SPEED))
            download_netspeed *= 0.5f;
        // We don't complete it here so we stay on 100% for a cycle
        // We do cap out our completion to avoid UI issues
        download_completion = Dm.min(Dm.num(download_completion) + download_netspeed, Dm.num(downloaded_file.size));
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/ntdownloader.dm:121
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        await base.ui_act(action, @params, ui, state);
        if (Dm.eq(action, "PRG_downloadfile"))
        {
            if (downloaded_file == null)
                begin_file_download(@params["filename"], usr);
            return TRUE;
        }
        if (Dm.eq(action, "PRG_reseterror"))
        {
            if (Dm.truthy(downloaderror))
            {
                download_completion = FALSE;
                downloaded_file = null;
                downloaderror = "";
            }
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/modular_computers/file_system/programs/ntdownloader.dm:136
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList access = computer.GetAccess() as DmList;

        data["downloading"] = downloaded_file != null ? TRUE : FALSE;
        data["error"] = Dm.truthy(downloaderror) ? downloaderror : FALSE;

        // Download running. Wait please..
        if (downloaded_file != null)
        {
            data["downloadname"] = downloaded_file.filename;
            data["downloaddesc"] = downloaded_file.filedesc;
            data["downloadsize"] = downloaded_file.size;
            data["downloadcompletion"] = Dm.round(Dm.num(download_completion), 0.1f);
        }

        data["disk_size"] = computer.max_capacity;
        data["disk_used"] = computer.used_capacity;
        data["emagged"] = Dm.bitand(computer.obj_flags, EMAGGED);

        DmList repo = Dm.bitor(SSmodular_computers.available_antag_software, SSmodular_computers.available_station_software) as DmList;

        foreach (datum_computer_file_program programs in Dm.each<datum_computer_file_program>(repo))
        {
            data["programs"] = Dm.add(data["programs"], Dm.list(Dm.list(
                Dm.kv("icon", programs.program_icon),
                Dm.kv("filename", programs.filename),
                Dm.kv("filedesc", programs.filedesc),
                Dm.kv("fileinfo", programs.extended_desc),
                Dm.kv("category", programs.downloader_category),
                Dm.kv("installed", computer.find_file_by_name(programs.filename) != null ? TRUE : FALSE),
                Dm.kv("compatible", check_compatibility(programs) ? TRUE : FALSE),
                Dm.kv("size", programs.size),
                Dm.kv("access", programs.can_run(user, downloading: TRUE, access: access)),
                Dm.kv("verifiedsource", Dm.truthy(Dm.bitand(programs.program_flags, PROGRAM_ON_NTNET_STORE)) ? TRUE : FALSE))));
        }

        data["categories"] = show_categories;

        return data;
    }

    ///Checks if a provided `program_to_check` is compatible to be downloaded on our computer.
    // tg: code/modules/modular_computers/file_system/programs/ntdownloader.dm:175
    public virtual bool check_compatibility(datum_computer_file_program program_to_check = null)
    {
        if (program_to_check == null || !Dm.truthy(program_to_check.is_supported_by_hardware(hardware_flag: computer.hardware_flag, loud: FALSE)))
            return false;
        return true;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/modular_computers/file_system/programs/ntdownloader.dm:25  /datum/computer_file/program/ntnetdownload's
    // var/static/list/show_categories
    internal DmList datum_computer_file_program_ntnetdownload__show_categories;
}
