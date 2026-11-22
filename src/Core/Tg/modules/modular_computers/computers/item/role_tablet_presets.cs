// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/computers/item/role_tablet_presets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: the job PDAs): the departments' PDAs and their apps, the heads' extra apps, the captain's and the
// lawyer's mass messaging, the mime's and the curator's silenced alerts, the clown's slipping PDA, the syndicate and clear PDAs.
// Left out with a comment or a marked leaf (the heads' fax notifier came with the faxes, gaps S3; the clown's virus disk charges
// on a slip with the virus disks, syndicate G3d): the clown's sitcom laughter (the wearertargeting/sitcomlaughter component). The apps of other milestones
// (records, robot control, cargo, status display and the like) are stored as tg stores them, as generated-only programs (their
// windows are not-translated.md 74). The var blocks are generated (D111).

// tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:5
public partial class obj_item_modular_computer_pda_crew : obj_item_modular_computer_pda
{
}

// tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:9
public partial class obj_item_modular_computer_pda_crew_heads : obj_item_modular_computer_pda_crew
{
    // tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:18  var/static/list/datum/computer_file/head_programs
    public DmList head_programs => TgState.Current.obj_item_modular_computer_pda_crew_heads__head_programs ??= Dm.list(
        typeof(datum_computer_file_program_status),
        typeof(datum_computer_file_program_science),
        typeof(datum_computer_file_program_robocontrol),
        typeof(datum_computer_file_program_budgetorders),
        typeof(datum_computer_file_program_faxbond));

    // tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (Type programs in Dm.each_cast<Type>(head_programs))
        {
            datum_computer_file_program program_type = Dm.@new(programs) as datum_computer_file_program;
            store_file(program_type);
        }

        if (ispath(fax_type, typeof(obj_machinery_fax))) // gaps S3: the faxes and FaxBond
        {
            datum_computer_file_program_faxbond fax_notifier = Dm.locate<datum_computer_file_program_faxbond>(stored_files);
            DmList faxes_list = SSmachines.get_machines_by_type(fax_type) as DmList;
            obj_machinery_fax heads_fax = Dm.truthy(length(faxes_list)) ? pick(faxes_list) as obj_machinery_fax : null; //there really shouldnt be more than one
            fax_notifier.connect_fax(heads_fax);
        }
        return dot;
    }
}

// tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:38
public partial class obj_item_modular_computer_pda_crew_heads_captain : obj_item_modular_computer_pda_crew_heads
{
    // tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_TABLET_CHECK_DETONATE, nameof(tab_no_detonate));
        foreach (datum_computer_file_program_messenger messenger_app in Dm.each<datum_computer_file_program_messenger>(stored_files))
            messenger_app.spam_mode = TRUE;
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:53
    [SignalHandler]
    public virtual object tab_no_detonate()
    {
        return COMPONENT_TABLET_NO_DETONATE;
    }
}

// tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:340
public partial class obj_item_modular_computer_pda_crew_lawyer : obj_item_modular_computer_pda_crew
{
    // tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:349
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (datum_computer_file_program_messenger messenger_app in Dm.each<datum_computer_file_program_messenger>(stored_files))
            messenger_app.spam_mode = TRUE;
        return dot;
    }
}

// tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:377
public partial class obj_item_modular_computer_pda_crew_clown : obj_item_modular_computer_pda_crew
{
    // tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:388
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_slippery),
            Dm.named("knockdown", 12 * SECONDS),
            Dm.named("lube_flags", NO_SLIP_WHEN_WALKING),
            Dm.named("on_slip_callback", Dm.@new<datum_callback>(this, nameof(AfterSlip))),
            Dm.named("can_slip_callback", Dm.@new<datum_callback>(this, nameof(try_slip))),
            Dm.named("slot_whitelist", ITEM_SLOT_ID | ITEM_SLOT_BELT)));
        // AddComponent(/datum/component/wearertargeting/sitcomlaughter, CALLBACK(src, PROC_REF(after_sitcom_laugh))): the sitcom
        // laughter component (the laughter of a slip, after the core version)
        return dot;
    }

    /// Returns whether the PDA can slip or not, if we have a wearer then check if they are in a position to slip someone.
    // tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:401
    public virtual object try_slip(mob_living slipper = null, mob_living slippee = null)
    {
        if (slipper == null)
            return TRUE;
        if (!(slipper.get_item_by_slot(ITEM_SLOT_FEET) is obj_item_clothing_shoes_clown_shoes))
        {
            to_chat(slipper, span_warning(I18n.T("[] failed to slip anyone. Perhaps I shouldn't have abandoned my legacy...", this)));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:409
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, mutable_appearance(icon, "pda_stripe_clown")); // clowns have eyes that go over their screen, so it needs to be compiled last
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:413
    public virtual void AfterSlip(mob_living_carbon_human M = null)
    {
        if (M != null && !Dm.eq(M.real_name, saved_identification))
        {
            if (inserted_disk is obj_item_disk_computer_virus_clown cart && cart.charges < 5)
            {
                cart.charges++;
                playsound(this, "sound/machines/ping.ogg", 30, TRUE);
            }
        }
    }

    // tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:420
    public virtual void after_sitcom_laugh(mob victim = null)
    {
        victim.visible_message(I18n.T("[] lets out a burst of laughter!", this));
    }
}

// tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:423
public partial class obj_item_modular_computer_pda_crew_mime : obj_item_modular_computer_pda_crew
{
    // tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:435
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (datum_computer_file_program_messenger msg in Dm.each<datum_computer_file_program_messenger>(stored_files))
        {
            msg.mime_mode = TRUE;
            msg.alert_silenced = TRUE;
        }
        return dot;
    }
}

// tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:441
public partial class obj_item_modular_computer_pda_crew_curator : obj_item_modular_computer_pda_crew
{
    // tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:458
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (datum_computer_file_program_messenger msg in Dm.each<datum_computer_file_program_messenger>(stored_files))
            msg.alert_silenced = TRUE;
        return dot;
    }
}

// tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:477
public partial class obj_item_modular_computer_pda_crew_assistant : obj_item_modular_computer_pda_crew
{
}

// tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:535
public partial class obj_item_modular_computer_pda_syndicate : obj_item_modular_computer_pda
{
    // tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:542
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (datum_computer_file_program_messenger msg in Dm.each<datum_computer_file_program_messenger>(stored_files))
        {
            msg.invisible = TRUE;
            break; // locate() in stored_files: the first one
        }
        return dot;
    }
}

// tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:548
public partial class obj_item_modular_computer_pda_clear : obj_item_modular_computer_pda
{
    // tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:557
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (datum_computer_file_program_themeify theme_app in Dm.each<datum_computer_file_program_themeify>(stored_files))
        {
            foreach (object theme_key in Dm.each(Dm.sub(GLOB.pda_name_to_theme, GLOB.default_pda_themes)))
                LAZYADD(ref theme_app.imported_themes, theme_key);
            break; // locate() in stored_files: the first one
        }
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:564
    public override object get_messenger_ending()
    {
        return "Sent from my crystal PDA";
    }
}

public sealed partial class TgState
{
    // tg: code/modules/modular_computers/computers/item/role_tablet_presets.dm:18  /obj/item/modular_computer/pda/crew/heads's
    // var/static/list/head_programs
    internal DmList obj_item_modular_computer_pda_crew_heads__head_programs;
}
