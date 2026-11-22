// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/file_browser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice E (NtOS's file manager): the File Manager app (file_browser.dm:96-280): deleting and renaming files, the alerts' mute
// and its NtosFileManager window. Left out with a comment or a marked leaf: printing an image file on photo paper or a canvas
// (GLOB.print_types, init_print_types and the print callbacks, :1-94, try_print: the camera's photos and the canvases). The
// inserted computer disk's files came with the PDA disks (syndicate G3d, D316). The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/file_browser.dm:96
public partial class datum_computer_file_program_filemanager : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/file_browser.dm:101
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        await base.ui_act(action, @params, ui, state);
        if (Dm.eq(action, "PRG_deletefile"))
        {
            datum_computer_file file = computer.find_file_by_name(@params["name"]);
            if (file == null || Dm.truthy(file.undeletable))
                return null;
            computer.remove_file(file);
            return TRUE;
        }
        if (Dm.eq(action, "PRG_renamefile"))
        {
            datum_computer_file file = computer.find_file_by_name(@params["name"]);
            if (file == null)
                return null;
            object newname = trim(@params["new_name"], MAX_MESSAGE_LEN);
            if (Dm.length(newname) == 0 || !Dm.truthy(filter_filename_pda(newname)))
            {
                playsound(computer, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 25, FALSE);
                return null;
            }
            file.filename = Dm.str(newname);
            return TRUE;
        }
        if (Dm.eq(action, "PRG_usbdeletefile"))
        {
            if (computer.inserted_disk is not obj_item_disk_computer disk)
                return null;
            datum_computer_file file = computer.find_file_by_name(@params["name"], disk);
            if (file == null || Dm.truthy(file.undeletable))
                return null;
            disk.remove_file(file);
            return TRUE;
        }
        if (Dm.eq(action, "PRG_usbrenamefile"))
        {
            if (computer.inserted_disk is not obj_item_disk_computer disk)
                return null;
            datum_computer_file file = computer.find_file_by_name(@params["name"], disk);
            if (file == null)
                return null;
            object newname = trim(@params["new_name"], MAX_MESSAGE_LEN);
            if (Dm.length(newname) == 0 || !Dm.truthy(filter_filename_pda(newname)))
            {
                playsound(computer, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 25, FALSE);
                return null;
            }
            file.filename = Dm.str(newname);
            return TRUE;
        }
        if (Dm.eq(action, "PRG_copytousb"))
        {
            if (computer.inserted_disk is not obj_item_disk_computer disk)
                return null;
            datum_computer_file F = computer.find_file_by_name(@params["name"]);
            if (F == null)
                return null;
            if (computer.find_file_by_name(@params["name"], disk) != null)
                return null;
            datum_computer_file C = F.clone(FALSE);
            disk.add_file(C);
            return TRUE;
        }
        if (Dm.eq(action, "PRG_copyfromusb"))
        {
            if (computer.inserted_disk is not obj_item_disk_computer disk)
                return null;
            datum_computer_file F = computer.find_file_by_name(@params["name"], disk);
            if (F == null)
                return null;
            if (!Dm.truthy(computer.can_store_file(F)))
                return FALSE;
            datum_computer_file C = F.clone(FALSE);
            computer.store_file(C, usr);
            return TRUE;
        }
        if (Dm.eq(action, "PRG_togglesilence"))
        {
            if (!(computer.find_file_by_name(@params["name"]) is datum_computer_file_program binary))
                return null;
            binary.alert_silenced = !Dm.truthy(binary.alert_silenced) ? TRUE : FALSE;
        }
        if (Dm.eq(action, "PRG_usbprint"))
        {
            if (computer.inserted_disk is not obj_item_disk_computer disk)
                return null;
            if (!(computer.find_file_by_name(@params["name"], disk) is datum_computer_file_image))
                return null;
            // try_print(picture, ...): printing an image file (the camera's photos and the canvases): no image file exists until then
            Dm.NotTranslated("code/modules/modular_computers/file_system/programs/file_browser.dm:178", "ui_act: PRG_usbprint, an image file's printout (the camera's photos and the canvases, after the core version)");
        }
        if (Dm.eq(action, "PRG_print"))
        {
            if (!(computer.find_file_by_name(@params["name"]) is datum_computer_file_image))
                return null;
            // try_print(picture, ...): printing an image file (the camera's photos and the canvases): no image file exists until then
            Dm.NotTranslated("code/modules/modular_computers/file_system/programs/file_browser.dm:177", "ui_act: PRG_print, an image file's printout (the camera's photos and the canvases, after the core version)");
        }
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/file_browser.dm:202  try_print: the photos and the canvases

    // tg: code/modules/modular_computers/file_system/programs/file_browser.dm:202
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        // for(var/dimensions, types_for_dimensions in GLOB.print_types) ...: the print types (photo paper and the canvases, the camera)
        // are not ported, so there is none
        DmList print_types = Dm.list();
        return Dm.list(Dm.kv("printTypes", print_types));
    }

    // tg: code/modules/modular_computers/file_system/programs/file_browser.dm:216
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        if (Dm.truthy(error))
            data["error"] = error;
        if (computer == null)
            data["error"] = I18n.T("I/O ERROR: Unable to access hard drive.");
        else
        {
            DmList files = Dm.list();
            foreach (datum_computer_file F in Dm.each<datum_computer_file>(computer.stored_files))
            {
                object noisy = FALSE;
                object silenced = FALSE;
                object printable = FALSE;
                object image_width = 0;
                object image_height = 0;
                object image_ref = null;
                if (F is datum_computer_file_program binary)
                {
                    noisy = binary.alert_able;
                    silenced = binary.alert_silenced;
                }
                if (F is datum_computer_file_image)
                {
                    // the image's width, height and image_ref (stored_icon, get_image_ref: the camera's photos): no image file exists
                    printable = TRUE;
                }
                Dm.append(ref files, Dm.list(Dm.list(
                    Dm.kv("name", F.filename),
                    Dm.kv("type", F.filetype),
                    Dm.kv("size", F.size),
                    Dm.kv("undeletable", F.undeletable),
                    Dm.kv("alert_able", noisy),
                    Dm.kv("alert_silenced", silenced),
                    Dm.kv("printable", printable),
                    Dm.kv("image_ref", image_ref),
                    Dm.kv("image_width", image_width),
                    Dm.kv("image_height", image_height))));
            }
            data["files"] = files;
            if (computer.inserted_disk is obj_item_disk_computer disk)
            {
                data["usbconnected"] = TRUE;
                DmList usbfiles = Dm.list();
                foreach (datum_computer_file F in Dm.each<datum_computer_file>(disk.stored_files))
                {
                    object printable = FALSE;
                    // the image's width, height and image_ref (stored_icon, get_image_ref: the camera's photos): no image file exists
                    if (F is datum_computer_file_image)
                        printable = TRUE;
                    Dm.append(ref usbfiles, Dm.list(Dm.list(
                        Dm.kv("name", F.filename),
                        Dm.kv("type", F.filetype),
                        Dm.kv("size", F.size),
                        Dm.kv("undeletable", F.undeletable),
                        Dm.kv("printable", printable),
                        Dm.kv("image_ref", null),
                        Dm.kv("image_width", 0),
                        Dm.kv("image_height", 0))));
                }
                data["usbfiles"] = usbfiles;
            }
        }

        return data;
    }
}
