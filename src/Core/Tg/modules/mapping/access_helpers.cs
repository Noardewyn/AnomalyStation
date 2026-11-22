// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapping/access_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P3. The access helpers' payloads (the req_access or req_one_access of the airlock under them) and the get_access of
// every helper, in tg's order. The 180 get_access overrides are mechanical (one form each) and were written by a one-off
// script from tg's file. The var blocks are generated (D111).

// tg: code/modules/mapping/access_helpers.dm:6
public partial class obj_effect_mapping_helpers_airlock_access_any : obj_effect_mapping_helpers_airlock_access
{
    // tg: code/modules/mapping/access_helpers.dm:6
    // Appendix A (M3 P3, decided): the payload slot's parameter is object (mapping_helpers.cs, the windoor helpers).
    public override void payload(object airlock = null)
    {
        if (((obj_machinery_door_airlock)airlock).req_access != null)
            log_mapping(Dm.text("[] access at [] failed to apply (cannot mix any/all access helpers: all existed, any failed)", this, AREACOORD__gap()));
        else
        {
            DmList access_list = get_access() as DmList;
            Dm.append(ref ((obj_machinery_door_airlock)airlock).req_one_access, access_list);
        }
    }
}

// tg: code/modules/mapping/access_helpers.dm:13
public partial class obj_effect_mapping_helpers_airlock_access_all : obj_effect_mapping_helpers_airlock_access
{
    // tg: code/modules/mapping/access_helpers.dm:13
    // Appendix A (M3 P3, decided): the payload slot's parameter is object (mapping_helpers.cs, the windoor helpers).
    public override void payload(object airlock = null)
    {
        if (((obj_machinery_door_airlock)airlock).req_one_access != null)
            log_mapping(Dm.text("[] access at [] failed to apply (cannot mix any/all access helpers: any existed, all failed)", this, AREACOORD__gap()));
        else
        {
            DmList access_list = get_access() as DmList;
            Dm.append(ref ((obj_machinery_door_airlock)airlock).req_access, access_list);
        }
    }
}

// tg: code/modules/mapping/access_helpers.dm:20
public partial class obj_effect_mapping_helpers_airlock_access : obj_effect_mapping_helpers_airlock
{
    // tg: code/modules/mapping/access_helpers.dm:20
    public virtual object get_access()
    {
        DmList access = Dm.list();
        return access;
    }
}

// tg: code/modules/mapping/access_helpers.dm:29
public partial class obj_effect_mapping_helpers_airlock_access_any_command_general : obj_effect_mapping_helpers_airlock_access_any_command
{
    // tg: code/modules/mapping/access_helpers.dm:29
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_COMMAND);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:34
public partial class obj_effect_mapping_helpers_airlock_access_any_command_ai_upload : obj_effect_mapping_helpers_airlock_access_any_command
{
    // tg: code/modules/mapping/access_helpers.dm:34
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_AI_UPLOAD);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:39
public partial class obj_effect_mapping_helpers_airlock_access_any_command_teleporter : obj_effect_mapping_helpers_airlock_access_any_command
{
    // tg: code/modules/mapping/access_helpers.dm:39
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_TELEPORTER);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:44
public partial class obj_effect_mapping_helpers_airlock_access_any_command_eva : obj_effect_mapping_helpers_airlock_access_any_command
{
    // tg: code/modules/mapping/access_helpers.dm:44
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_EVA);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:49
public partial class obj_effect_mapping_helpers_airlock_access_any_command_minisat : obj_effect_mapping_helpers_airlock_access_any_command
{
    // tg: code/modules/mapping/access_helpers.dm:49
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MINISAT);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:55
public partial class obj_effect_mapping_helpers_airlock_access_any_command_gateway : obj_effect_mapping_helpers_airlock_access_any_command
{
    // tg: code/modules/mapping/access_helpers.dm:55
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_GATEWAY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:60
public partial class obj_effect_mapping_helpers_airlock_access_any_command_hop : obj_effect_mapping_helpers_airlock_access_any_command
{
    // tg: code/modules/mapping/access_helpers.dm:60
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_HOP);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:65
public partial class obj_effect_mapping_helpers_airlock_access_any_command_captain : obj_effect_mapping_helpers_airlock_access_any_command
{
    // tg: code/modules/mapping/access_helpers.dm:65
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CAPTAIN);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:70
public partial class obj_effect_mapping_helpers_airlock_access_any_command_maintenance : obj_effect_mapping_helpers_airlock_access_any_command
{
    // tg: code/modules/mapping/access_helpers.dm:70
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_COMMAND, ACCESS_MAINT_TUNNELS));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:79
public partial class obj_effect_mapping_helpers_airlock_access_any_engineering_general : obj_effect_mapping_helpers_airlock_access_any_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:79
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_ENGINEERING);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:84
public partial class obj_effect_mapping_helpers_airlock_access_any_engineering_engine_equipment : obj_effect_mapping_helpers_airlock_access_any_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:84
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_ENGINE_EQUIP);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:89
public partial class obj_effect_mapping_helpers_airlock_access_any_engineering_construction : obj_effect_mapping_helpers_airlock_access_any_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:89
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CONSTRUCTION);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:94
public partial class obj_effect_mapping_helpers_airlock_access_any_engineering_aux_base : obj_effect_mapping_helpers_airlock_access_any_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:94
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_AUX_BASE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:99
public partial class obj_effect_mapping_helpers_airlock_access_any_engineering_maintenance : obj_effect_mapping_helpers_airlock_access_any_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:99
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MAINT_TUNNELS);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:104
public partial class obj_effect_mapping_helpers_airlock_access_any_engineering_maintenance_departmental : obj_effect_mapping_helpers_airlock_access_any_engineering_maintenance
{
    // tg: code/modules/mapping/access_helpers.dm:104
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_ENGINEERING, ACCESS_MAINT_TUNNELS));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:109
public partial class obj_effect_mapping_helpers_airlock_access_any_engineering_external : obj_effect_mapping_helpers_airlock_access_any_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:109
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_EXTERNAL_AIRLOCKS);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:114
public partial class obj_effect_mapping_helpers_airlock_access_any_engineering_tech_storage : obj_effect_mapping_helpers_airlock_access_any_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:114
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_TECH_STORAGE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:119
public partial class obj_effect_mapping_helpers_airlock_access_any_engineering_atmos : obj_effect_mapping_helpers_airlock_access_any_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:119
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_ATMOSPHERICS);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:124
public partial class obj_effect_mapping_helpers_airlock_access_any_engineering_tcoms : obj_effect_mapping_helpers_airlock_access_any_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:124
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_TCOMMS);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:129
public partial class obj_effect_mapping_helpers_airlock_access_any_engineering_ce : obj_effect_mapping_helpers_airlock_access_any_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:129
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:138
public partial class obj_effect_mapping_helpers_airlock_access_any_medical_general : obj_effect_mapping_helpers_airlock_access_any_medical
{
    // tg: code/modules/mapping/access_helpers.dm:138
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MEDICAL);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:143
public partial class obj_effect_mapping_helpers_airlock_access_any_medical_morgue : obj_effect_mapping_helpers_airlock_access_any_medical
{
    // tg: code/modules/mapping/access_helpers.dm:143
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MORGUE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:148
public partial class obj_effect_mapping_helpers_airlock_access_any_medical_coroner : obj_effect_mapping_helpers_airlock_access_any_medical
{
    // tg: code/modules/mapping/access_helpers.dm:148
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MORGUE_SECURE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:153
public partial class obj_effect_mapping_helpers_airlock_access_any_medical_chemistry : obj_effect_mapping_helpers_airlock_access_any_medical
{
    // tg: code/modules/mapping/access_helpers.dm:153
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_PLUMBING);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:158
public partial class obj_effect_mapping_helpers_airlock_access_any_medical_virology : obj_effect_mapping_helpers_airlock_access_any_medical
{
    // tg: code/modules/mapping/access_helpers.dm:158
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_VIROLOGY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:163
public partial class obj_effect_mapping_helpers_airlock_access_any_medical_surgery : obj_effect_mapping_helpers_airlock_access_any_medical
{
    // tg: code/modules/mapping/access_helpers.dm:163
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_SURGERY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:168
public partial class obj_effect_mapping_helpers_airlock_access_any_medical_cmo : obj_effect_mapping_helpers_airlock_access_any_medical
{
    // tg: code/modules/mapping/access_helpers.dm:168
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CMO);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:173
public partial class obj_effect_mapping_helpers_airlock_access_any_medical_pharmacy : obj_effect_mapping_helpers_airlock_access_any_medical
{
    // tg: code/modules/mapping/access_helpers.dm:173
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_PHARMACY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:178
public partial class obj_effect_mapping_helpers_airlock_access_any_medical_psychology : obj_effect_mapping_helpers_airlock_access_any_medical
{
    // tg: code/modules/mapping/access_helpers.dm:178
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_PSYCHOLOGY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:183
public partial class obj_effect_mapping_helpers_airlock_access_any_medical_maintenance : obj_effect_mapping_helpers_airlock_access_any_medical
{
    // tg: code/modules/mapping/access_helpers.dm:183
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_MEDICAL, ACCESS_MAINT_TUNNELS));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:188
public partial class obj_effect_mapping_helpers_airlock_access_any_medical_paramedic : obj_effect_mapping_helpers_airlock_access_any_medical
{
    // tg: code/modules/mapping/access_helpers.dm:188
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_PARAMEDIC);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:197
public partial class obj_effect_mapping_helpers_airlock_access_any_science_general : obj_effect_mapping_helpers_airlock_access_any_science
{
    // tg: code/modules/mapping/access_helpers.dm:197
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_SCIENCE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:202
public partial class obj_effect_mapping_helpers_airlock_access_any_science_research : obj_effect_mapping_helpers_airlock_access_any_science
{
    // tg: code/modules/mapping/access_helpers.dm:202
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_RESEARCH);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:207
public partial class obj_effect_mapping_helpers_airlock_access_any_science_ordnance : obj_effect_mapping_helpers_airlock_access_any_science
{
    // tg: code/modules/mapping/access_helpers.dm:207
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_ORDNANCE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:212
public partial class obj_effect_mapping_helpers_airlock_access_any_science_ordnance_storage : obj_effect_mapping_helpers_airlock_access_any_science
{
    // tg: code/modules/mapping/access_helpers.dm:212
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_ORDNANCE_STORAGE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:217
public partial class obj_effect_mapping_helpers_airlock_access_any_science_genetics : obj_effect_mapping_helpers_airlock_access_any_science
{
    // tg: code/modules/mapping/access_helpers.dm:217
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_GENETICS);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:222
public partial class obj_effect_mapping_helpers_airlock_access_any_science_robotics : obj_effect_mapping_helpers_airlock_access_any_science
{
    // tg: code/modules/mapping/access_helpers.dm:222
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_ROBOTICS);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:227
public partial class obj_effect_mapping_helpers_airlock_access_any_science_xenobio : obj_effect_mapping_helpers_airlock_access_any_science
{
    // tg: code/modules/mapping/access_helpers.dm:227
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_XENOBIOLOGY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:232
public partial class obj_effect_mapping_helpers_airlock_access_any_science_minisat : obj_effect_mapping_helpers_airlock_access_any_science
{
    // tg: code/modules/mapping/access_helpers.dm:232
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MINISAT);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:237
public partial class obj_effect_mapping_helpers_airlock_access_any_science_rd : obj_effect_mapping_helpers_airlock_access_any_science
{
    // tg: code/modules/mapping/access_helpers.dm:237
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_RD);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:242
public partial class obj_effect_mapping_helpers_airlock_access_any_science_maintenance : obj_effect_mapping_helpers_airlock_access_any_science
{
    // tg: code/modules/mapping/access_helpers.dm:242
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_SCIENCE, ACCESS_MAINT_TUNNELS));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:251
public partial class obj_effect_mapping_helpers_airlock_access_any_security_general : obj_effect_mapping_helpers_airlock_access_any_security
{
    // tg: code/modules/mapping/access_helpers.dm:251
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_SECURITY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:256
public partial class obj_effect_mapping_helpers_airlock_access_any_security_entrance : obj_effect_mapping_helpers_airlock_access_any_security
{
    // tg: code/modules/mapping/access_helpers.dm:256
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_BRIG_ENTRANCE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:261
public partial class obj_effect_mapping_helpers_airlock_access_any_security_brig : obj_effect_mapping_helpers_airlock_access_any_security
{
    // tg: code/modules/mapping/access_helpers.dm:261
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_BRIG);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:266
public partial class obj_effect_mapping_helpers_airlock_access_any_security_armory : obj_effect_mapping_helpers_airlock_access_any_security
{
    // tg: code/modules/mapping/access_helpers.dm:266
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_ARMORY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:271
public partial class obj_effect_mapping_helpers_airlock_access_any_security_detective : obj_effect_mapping_helpers_airlock_access_any_security
{
    // tg: code/modules/mapping/access_helpers.dm:271
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_DETECTIVE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:276
public partial class obj_effect_mapping_helpers_airlock_access_any_security_court : obj_effect_mapping_helpers_airlock_access_any_security
{
    // tg: code/modules/mapping/access_helpers.dm:276
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_COURT);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:281
public partial class obj_effect_mapping_helpers_airlock_access_any_security_hos : obj_effect_mapping_helpers_airlock_access_any_security
{
    // tg: code/modules/mapping/access_helpers.dm:281
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_HOS);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:286
public partial class obj_effect_mapping_helpers_airlock_access_any_security_maintenance : obj_effect_mapping_helpers_airlock_access_any_security
{
    // tg: code/modules/mapping/access_helpers.dm:286
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_SECURITY, ACCESS_MAINT_TUNNELS));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:295
public partial class obj_effect_mapping_helpers_airlock_access_any_service_general : obj_effect_mapping_helpers_airlock_access_any_service
{
    // tg: code/modules/mapping/access_helpers.dm:295
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_SERVICE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:300
public partial class obj_effect_mapping_helpers_airlock_access_any_service_kitchen : obj_effect_mapping_helpers_airlock_access_any_service
{
    // tg: code/modules/mapping/access_helpers.dm:300
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_KITCHEN);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:305
public partial class obj_effect_mapping_helpers_airlock_access_any_service_bar : obj_effect_mapping_helpers_airlock_access_any_service
{
    // tg: code/modules/mapping/access_helpers.dm:305
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_BAR);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:310
public partial class obj_effect_mapping_helpers_airlock_access_any_service_hydroponics : obj_effect_mapping_helpers_airlock_access_any_service
{
    // tg: code/modules/mapping/access_helpers.dm:310
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_HYDROPONICS);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:315
public partial class obj_effect_mapping_helpers_airlock_access_any_service_janitor : obj_effect_mapping_helpers_airlock_access_any_service
{
    // tg: code/modules/mapping/access_helpers.dm:315
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_JANITOR);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:320
public partial class obj_effect_mapping_helpers_airlock_access_any_service_chapel_office : obj_effect_mapping_helpers_airlock_access_any_service
{
    // tg: code/modules/mapping/access_helpers.dm:320
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CHAPEL_OFFICE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:325
public partial class obj_effect_mapping_helpers_airlock_access_any_service_crematorium : obj_effect_mapping_helpers_airlock_access_any_service
{
    // tg: code/modules/mapping/access_helpers.dm:325
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CREMATORIUM);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:330
public partial class obj_effect_mapping_helpers_airlock_access_any_service_library : obj_effect_mapping_helpers_airlock_access_any_service
{
    // tg: code/modules/mapping/access_helpers.dm:330
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_LIBRARY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:335
public partial class obj_effect_mapping_helpers_airlock_access_any_service_theatre : obj_effect_mapping_helpers_airlock_access_any_service
{
    // tg: code/modules/mapping/access_helpers.dm:335
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_THEATRE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:340
public partial class obj_effect_mapping_helpers_airlock_access_any_service_lawyer : obj_effect_mapping_helpers_airlock_access_any_service
{
    // tg: code/modules/mapping/access_helpers.dm:340
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_LAWYER);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:345
public partial class obj_effect_mapping_helpers_airlock_access_any_service_maintenance : obj_effect_mapping_helpers_airlock_access_any_service
{
    // tg: code/modules/mapping/access_helpers.dm:345
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_SERVICE, ACCESS_MAINT_TUNNELS));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:354
public partial class obj_effect_mapping_helpers_airlock_access_any_supply_general : obj_effect_mapping_helpers_airlock_access_any_supply
{
    // tg: code/modules/mapping/access_helpers.dm:354
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CARGO);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:359
public partial class obj_effect_mapping_helpers_airlock_access_any_supply_shipping : obj_effect_mapping_helpers_airlock_access_any_supply
{
    // tg: code/modules/mapping/access_helpers.dm:359
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_SHIPPING);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:364
public partial class obj_effect_mapping_helpers_airlock_access_any_supply_mining : obj_effect_mapping_helpers_airlock_access_any_supply
{
    // tg: code/modules/mapping/access_helpers.dm:364
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MINING);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:369
public partial class obj_effect_mapping_helpers_airlock_access_any_supply_mining_station : obj_effect_mapping_helpers_airlock_access_any_supply
{
    // tg: code/modules/mapping/access_helpers.dm:369
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MINING_STATION);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:374
public partial class obj_effect_mapping_helpers_airlock_access_any_supply_mineral_storage : obj_effect_mapping_helpers_airlock_access_any_supply
{
    // tg: code/modules/mapping/access_helpers.dm:374
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MINERAL_STOREROOM);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:379
public partial class obj_effect_mapping_helpers_airlock_access_any_supply_qm : obj_effect_mapping_helpers_airlock_access_any_supply
{
    // tg: code/modules/mapping/access_helpers.dm:379
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_QM);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:384
public partial class obj_effect_mapping_helpers_airlock_access_any_supply_vault : obj_effect_mapping_helpers_airlock_access_any_supply
{
    // tg: code/modules/mapping/access_helpers.dm:384
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_VAULT);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:389
public partial class obj_effect_mapping_helpers_airlock_access_any_supply_maintenance : obj_effect_mapping_helpers_airlock_access_any_supply
{
    // tg: code/modules/mapping/access_helpers.dm:389
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_CARGO, ACCESS_MAINT_TUNNELS));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:394
public partial class obj_effect_mapping_helpers_airlock_access_any_supply_bit_den : obj_effect_mapping_helpers_airlock_access_any_supply
{
    // tg: code/modules/mapping/access_helpers.dm:394
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_BIT_DEN);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:403
public partial class obj_effect_mapping_helpers_airlock_access_any_syndicate_general : obj_effect_mapping_helpers_airlock_access_any_syndicate
{
    // tg: code/modules/mapping/access_helpers.dm:403
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_SYNDICATE));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:408
public partial class obj_effect_mapping_helpers_airlock_access_any_syndicate_leader : obj_effect_mapping_helpers_airlock_access_any_syndicate
{
    // tg: code/modules/mapping/access_helpers.dm:408
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_SYNDICATE_LEADER));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:417
public partial class obj_effect_mapping_helpers_airlock_access_any_away_general : obj_effect_mapping_helpers_airlock_access_any_away
{
    // tg: code/modules/mapping/access_helpers.dm:417
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_AWAY_GENERAL));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:422
public partial class obj_effect_mapping_helpers_airlock_access_any_away_command : obj_effect_mapping_helpers_airlock_access_any_away
{
    // tg: code/modules/mapping/access_helpers.dm:422
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_AWAY_COMMAND));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:427
public partial class obj_effect_mapping_helpers_airlock_access_any_away_security : obj_effect_mapping_helpers_airlock_access_any_away
{
    // tg: code/modules/mapping/access_helpers.dm:427
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_AWAY_SEC));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:432
public partial class obj_effect_mapping_helpers_airlock_access_any_away_engineering : obj_effect_mapping_helpers_airlock_access_any_away
{
    // tg: code/modules/mapping/access_helpers.dm:432
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_AWAY_ENGINEERING));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:437
public partial class obj_effect_mapping_helpers_airlock_access_any_away_medical : obj_effect_mapping_helpers_airlock_access_any_away
{
    // tg: code/modules/mapping/access_helpers.dm:437
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_AWAY_MEDICAL));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:442
public partial class obj_effect_mapping_helpers_airlock_access_any_away_supply : obj_effect_mapping_helpers_airlock_access_any_away
{
    // tg: code/modules/mapping/access_helpers.dm:442
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_AWAY_SUPPLY));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:447
public partial class obj_effect_mapping_helpers_airlock_access_any_away_science : obj_effect_mapping_helpers_airlock_access_any_away
{
    // tg: code/modules/mapping/access_helpers.dm:447
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_AWAY_SCIENCE));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:452
public partial class obj_effect_mapping_helpers_airlock_access_any_away_maintenance : obj_effect_mapping_helpers_airlock_access_any_away
{
    // tg: code/modules/mapping/access_helpers.dm:452
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_AWAY_MAINTENANCE));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:457
public partial class obj_effect_mapping_helpers_airlock_access_any_away_generic1 : obj_effect_mapping_helpers_airlock_access_any_away
{
    // tg: code/modules/mapping/access_helpers.dm:457
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_AWAY_GENERIC1));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:462
public partial class obj_effect_mapping_helpers_airlock_access_any_away_generic2 : obj_effect_mapping_helpers_airlock_access_any_away
{
    // tg: code/modules/mapping/access_helpers.dm:462
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_AWAY_GENERIC2));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:467
public partial class obj_effect_mapping_helpers_airlock_access_any_away_generic3 : obj_effect_mapping_helpers_airlock_access_any_away
{
    // tg: code/modules/mapping/access_helpers.dm:467
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_AWAY_GENERIC3));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:472
public partial class obj_effect_mapping_helpers_airlock_access_any_away_generic4 : obj_effect_mapping_helpers_airlock_access_any_away
{
    // tg: code/modules/mapping/access_helpers.dm:472
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_AWAY_GENERIC4));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:481
public partial class obj_effect_mapping_helpers_airlock_access_any_admin_general : obj_effect_mapping_helpers_airlock_access_any_admin
{
    // tg: code/modules/mapping/access_helpers.dm:481
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_CENT_GENERAL));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:486
public partial class obj_effect_mapping_helpers_airlock_access_any_admin_thunderdome : obj_effect_mapping_helpers_airlock_access_any_admin
{
    // tg: code/modules/mapping/access_helpers.dm:486
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_CENT_THUNDER));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:491
public partial class obj_effect_mapping_helpers_airlock_access_any_admin_medical : obj_effect_mapping_helpers_airlock_access_any_admin
{
    // tg: code/modules/mapping/access_helpers.dm:491
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_CENT_MEDICAL));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:496
public partial class obj_effect_mapping_helpers_airlock_access_any_admin_living : obj_effect_mapping_helpers_airlock_access_any_admin
{
    // tg: code/modules/mapping/access_helpers.dm:496
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_CENT_LIVING));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:501
public partial class obj_effect_mapping_helpers_airlock_access_any_admin_storage : obj_effect_mapping_helpers_airlock_access_any_admin
{
    // tg: code/modules/mapping/access_helpers.dm:501
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_CENT_STORAGE));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:506
public partial class obj_effect_mapping_helpers_airlock_access_any_admin_teleporter : obj_effect_mapping_helpers_airlock_access_any_admin
{
    // tg: code/modules/mapping/access_helpers.dm:506
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_CENT_TELEPORTER));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:511
public partial class obj_effect_mapping_helpers_airlock_access_any_admin_captain : obj_effect_mapping_helpers_airlock_access_any_admin
{
    // tg: code/modules/mapping/access_helpers.dm:511
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_CENT_CAPTAIN));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:516
public partial class obj_effect_mapping_helpers_airlock_access_any_admin_bar : obj_effect_mapping_helpers_airlock_access_any_admin
{
    // tg: code/modules/mapping/access_helpers.dm:516
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_CENT_BAR));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:521
public partial class obj_effect_mapping_helpers_airlock_access_any_admin_officer : obj_effect_mapping_helpers_airlock_access_any_admin
{
    // tg: code/modules/mapping/access_helpers.dm:521
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_CENT_OFFICER));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:526
public partial class obj_effect_mapping_helpers_airlock_access_any_admin_specops : obj_effect_mapping_helpers_airlock_access_any_admin
{
    // tg: code/modules/mapping/access_helpers.dm:526
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, Dm.list(ACCESS_CENT_SPECOPS));
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:536
public partial class obj_effect_mapping_helpers_airlock_access_all_command_general : obj_effect_mapping_helpers_airlock_access_all_command
{
    // tg: code/modules/mapping/access_helpers.dm:536
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_COMMAND);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:541
public partial class obj_effect_mapping_helpers_airlock_access_all_command_ai_upload : obj_effect_mapping_helpers_airlock_access_all_command
{
    // tg: code/modules/mapping/access_helpers.dm:541
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_AI_UPLOAD);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:546
public partial class obj_effect_mapping_helpers_airlock_access_all_command_teleporter : obj_effect_mapping_helpers_airlock_access_all_command
{
    // tg: code/modules/mapping/access_helpers.dm:546
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_TELEPORTER);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:551
public partial class obj_effect_mapping_helpers_airlock_access_all_command_eva : obj_effect_mapping_helpers_airlock_access_all_command
{
    // tg: code/modules/mapping/access_helpers.dm:551
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_EVA);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:556
public partial class obj_effect_mapping_helpers_airlock_access_all_command_minisat : obj_effect_mapping_helpers_airlock_access_all_command
{
    // tg: code/modules/mapping/access_helpers.dm:556
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MINISAT);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:561
public partial class obj_effect_mapping_helpers_airlock_access_all_command_gateway : obj_effect_mapping_helpers_airlock_access_all_command
{
    // tg: code/modules/mapping/access_helpers.dm:561
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_GATEWAY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:566
public partial class obj_effect_mapping_helpers_airlock_access_all_command_hop : obj_effect_mapping_helpers_airlock_access_all_command
{
    // tg: code/modules/mapping/access_helpers.dm:566
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_HOP);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:571
public partial class obj_effect_mapping_helpers_airlock_access_all_command_captain : obj_effect_mapping_helpers_airlock_access_all_command
{
    // tg: code/modules/mapping/access_helpers.dm:571
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CAPTAIN);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:580
public partial class obj_effect_mapping_helpers_airlock_access_all_engineering_general : obj_effect_mapping_helpers_airlock_access_all_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:580
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_ENGINEERING);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:585
public partial class obj_effect_mapping_helpers_airlock_access_all_engineering_engine_equipment : obj_effect_mapping_helpers_airlock_access_all_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:585
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_ENGINE_EQUIP);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:590
public partial class obj_effect_mapping_helpers_airlock_access_all_engineering_construction : obj_effect_mapping_helpers_airlock_access_all_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:590
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CONSTRUCTION);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:595
public partial class obj_effect_mapping_helpers_airlock_access_all_engineering_aux_base : obj_effect_mapping_helpers_airlock_access_all_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:595
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_AUX_BASE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:600
public partial class obj_effect_mapping_helpers_airlock_access_all_engineering_maintenance : obj_effect_mapping_helpers_airlock_access_all_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:600
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MAINT_TUNNELS);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:605
public partial class obj_effect_mapping_helpers_airlock_access_all_engineering_external : obj_effect_mapping_helpers_airlock_access_all_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:605
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_EXTERNAL_AIRLOCKS);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:610
public partial class obj_effect_mapping_helpers_airlock_access_all_engineering_tech_storage : obj_effect_mapping_helpers_airlock_access_all_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:610
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_TECH_STORAGE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:615
public partial class obj_effect_mapping_helpers_airlock_access_all_engineering_atmos : obj_effect_mapping_helpers_airlock_access_all_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:615
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_ATMOSPHERICS);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:620
public partial class obj_effect_mapping_helpers_airlock_access_all_engineering_tcoms : obj_effect_mapping_helpers_airlock_access_all_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:620
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_TCOMMS);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:625
public partial class obj_effect_mapping_helpers_airlock_access_all_engineering_ce : obj_effect_mapping_helpers_airlock_access_all_engineering
{
    // tg: code/modules/mapping/access_helpers.dm:625
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:634
public partial class obj_effect_mapping_helpers_airlock_access_all_medical_general : obj_effect_mapping_helpers_airlock_access_all_medical
{
    // tg: code/modules/mapping/access_helpers.dm:634
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MEDICAL);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:639
public partial class obj_effect_mapping_helpers_airlock_access_all_medical_morgue : obj_effect_mapping_helpers_airlock_access_all_medical
{
    // tg: code/modules/mapping/access_helpers.dm:639
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MORGUE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:644
public partial class obj_effect_mapping_helpers_airlock_access_all_medical_coroner : obj_effect_mapping_helpers_airlock_access_all_medical
{
    // tg: code/modules/mapping/access_helpers.dm:644
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MORGUE_SECURE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:649
public partial class obj_effect_mapping_helpers_airlock_access_all_medical_chemistry : obj_effect_mapping_helpers_airlock_access_all_medical
{
    // tg: code/modules/mapping/access_helpers.dm:649
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_PLUMBING);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:654
public partial class obj_effect_mapping_helpers_airlock_access_all_medical_virology : obj_effect_mapping_helpers_airlock_access_all_medical
{
    // tg: code/modules/mapping/access_helpers.dm:654
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_VIROLOGY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:659
public partial class obj_effect_mapping_helpers_airlock_access_all_medical_surgery : obj_effect_mapping_helpers_airlock_access_all_medical
{
    // tg: code/modules/mapping/access_helpers.dm:659
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_SURGERY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:664
public partial class obj_effect_mapping_helpers_airlock_access_all_medical_cmo : obj_effect_mapping_helpers_airlock_access_all_medical
{
    // tg: code/modules/mapping/access_helpers.dm:664
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CMO);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:669
public partial class obj_effect_mapping_helpers_airlock_access_all_medical_pharmacy : obj_effect_mapping_helpers_airlock_access_all_medical
{
    // tg: code/modules/mapping/access_helpers.dm:669
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_PHARMACY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:674
public partial class obj_effect_mapping_helpers_airlock_access_all_medical_psychology : obj_effect_mapping_helpers_airlock_access_all_medical
{
    // tg: code/modules/mapping/access_helpers.dm:674
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_PSYCHOLOGY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:679
public partial class obj_effect_mapping_helpers_airlock_access_all_medical_paramedic : obj_effect_mapping_helpers_airlock_access_all_medical
{
    // tg: code/modules/mapping/access_helpers.dm:679
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_PARAMEDIC);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:688
public partial class obj_effect_mapping_helpers_airlock_access_all_science_general : obj_effect_mapping_helpers_airlock_access_all_science
{
    // tg: code/modules/mapping/access_helpers.dm:688
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_SCIENCE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:693
public partial class obj_effect_mapping_helpers_airlock_access_all_science_research : obj_effect_mapping_helpers_airlock_access_all_science
{
    // tg: code/modules/mapping/access_helpers.dm:693
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_RESEARCH);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:698
public partial class obj_effect_mapping_helpers_airlock_access_all_science_ordnance : obj_effect_mapping_helpers_airlock_access_all_science
{
    // tg: code/modules/mapping/access_helpers.dm:698
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_ORDNANCE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:703
public partial class obj_effect_mapping_helpers_airlock_access_all_science_ordnance_storage : obj_effect_mapping_helpers_airlock_access_all_science
{
    // tg: code/modules/mapping/access_helpers.dm:703
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_ORDNANCE_STORAGE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:708
public partial class obj_effect_mapping_helpers_airlock_access_all_science_genetics : obj_effect_mapping_helpers_airlock_access_all_science
{
    // tg: code/modules/mapping/access_helpers.dm:708
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_GENETICS);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:713
public partial class obj_effect_mapping_helpers_airlock_access_all_science_robotics : obj_effect_mapping_helpers_airlock_access_all_science
{
    // tg: code/modules/mapping/access_helpers.dm:713
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_ROBOTICS);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:718
public partial class obj_effect_mapping_helpers_airlock_access_all_science_xenobio : obj_effect_mapping_helpers_airlock_access_all_science
{
    // tg: code/modules/mapping/access_helpers.dm:718
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_XENOBIOLOGY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:723
public partial class obj_effect_mapping_helpers_airlock_access_all_science_minisat : obj_effect_mapping_helpers_airlock_access_all_science
{
    // tg: code/modules/mapping/access_helpers.dm:723
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MINISAT);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:728
public partial class obj_effect_mapping_helpers_airlock_access_all_science_rd : obj_effect_mapping_helpers_airlock_access_all_science
{
    // tg: code/modules/mapping/access_helpers.dm:728
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_RD);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:737
public partial class obj_effect_mapping_helpers_airlock_access_all_security_general : obj_effect_mapping_helpers_airlock_access_all_security
{
    // tg: code/modules/mapping/access_helpers.dm:737
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_SECURITY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:742
public partial class obj_effect_mapping_helpers_airlock_access_all_security_entrance : obj_effect_mapping_helpers_airlock_access_all_security
{
    // tg: code/modules/mapping/access_helpers.dm:742
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_BRIG_ENTRANCE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:747
public partial class obj_effect_mapping_helpers_airlock_access_all_security_brig : obj_effect_mapping_helpers_airlock_access_all_security
{
    // tg: code/modules/mapping/access_helpers.dm:747
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_BRIG);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:752
public partial class obj_effect_mapping_helpers_airlock_access_all_security_armory : obj_effect_mapping_helpers_airlock_access_all_security
{
    // tg: code/modules/mapping/access_helpers.dm:752
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_ARMORY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:757
public partial class obj_effect_mapping_helpers_airlock_access_all_security_detective : obj_effect_mapping_helpers_airlock_access_all_security
{
    // tg: code/modules/mapping/access_helpers.dm:757
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_DETECTIVE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:762
public partial class obj_effect_mapping_helpers_airlock_access_all_security_court : obj_effect_mapping_helpers_airlock_access_all_security
{
    // tg: code/modules/mapping/access_helpers.dm:762
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_COURT);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:767
public partial class obj_effect_mapping_helpers_airlock_access_all_security_hos : obj_effect_mapping_helpers_airlock_access_all_security
{
    // tg: code/modules/mapping/access_helpers.dm:767
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_HOS);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:776
public partial class obj_effect_mapping_helpers_airlock_access_all_service_general : obj_effect_mapping_helpers_airlock_access_all_service
{
    // tg: code/modules/mapping/access_helpers.dm:776
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_SERVICE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:781
public partial class obj_effect_mapping_helpers_airlock_access_all_service_kitchen : obj_effect_mapping_helpers_airlock_access_all_service
{
    // tg: code/modules/mapping/access_helpers.dm:781
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_KITCHEN);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:786
public partial class obj_effect_mapping_helpers_airlock_access_all_service_bar : obj_effect_mapping_helpers_airlock_access_all_service
{
    // tg: code/modules/mapping/access_helpers.dm:786
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_BAR);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:791
public partial class obj_effect_mapping_helpers_airlock_access_all_service_hydroponics : obj_effect_mapping_helpers_airlock_access_all_service
{
    // tg: code/modules/mapping/access_helpers.dm:791
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_HYDROPONICS);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:796
public partial class obj_effect_mapping_helpers_airlock_access_all_service_janitor : obj_effect_mapping_helpers_airlock_access_all_service
{
    // tg: code/modules/mapping/access_helpers.dm:796
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_JANITOR);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:801
public partial class obj_effect_mapping_helpers_airlock_access_all_service_chapel_office : obj_effect_mapping_helpers_airlock_access_all_service
{
    // tg: code/modules/mapping/access_helpers.dm:801
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CHAPEL_OFFICE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:806
public partial class obj_effect_mapping_helpers_airlock_access_all_service_crematorium : obj_effect_mapping_helpers_airlock_access_all_service
{
    // tg: code/modules/mapping/access_helpers.dm:806
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CREMATORIUM);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:811
public partial class obj_effect_mapping_helpers_airlock_access_all_service_library : obj_effect_mapping_helpers_airlock_access_all_service
{
    // tg: code/modules/mapping/access_helpers.dm:811
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_LIBRARY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:816
public partial class obj_effect_mapping_helpers_airlock_access_all_service_theatre : obj_effect_mapping_helpers_airlock_access_all_service
{
    // tg: code/modules/mapping/access_helpers.dm:816
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_THEATRE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:821
public partial class obj_effect_mapping_helpers_airlock_access_all_service_lawyer : obj_effect_mapping_helpers_airlock_access_all_service
{
    // tg: code/modules/mapping/access_helpers.dm:821
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_LAWYER);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:830
public partial class obj_effect_mapping_helpers_airlock_access_all_supply_general : obj_effect_mapping_helpers_airlock_access_all_supply
{
    // tg: code/modules/mapping/access_helpers.dm:830
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CARGO);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:835
public partial class obj_effect_mapping_helpers_airlock_access_all_supply_shipping : obj_effect_mapping_helpers_airlock_access_all_supply
{
    // tg: code/modules/mapping/access_helpers.dm:835
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_SHIPPING);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:840
public partial class obj_effect_mapping_helpers_airlock_access_all_supply_mining : obj_effect_mapping_helpers_airlock_access_all_supply
{
    // tg: code/modules/mapping/access_helpers.dm:840
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MINING);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:845
public partial class obj_effect_mapping_helpers_airlock_access_all_supply_mining_station : obj_effect_mapping_helpers_airlock_access_all_supply
{
    // tg: code/modules/mapping/access_helpers.dm:845
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MINING_STATION);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:850
public partial class obj_effect_mapping_helpers_airlock_access_all_supply_mineral_storage : obj_effect_mapping_helpers_airlock_access_all_supply
{
    // tg: code/modules/mapping/access_helpers.dm:850
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_MINERAL_STOREROOM);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:855
public partial class obj_effect_mapping_helpers_airlock_access_all_supply_qm : obj_effect_mapping_helpers_airlock_access_all_supply
{
    // tg: code/modules/mapping/access_helpers.dm:855
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_QM);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:860
public partial class obj_effect_mapping_helpers_airlock_access_all_supply_vault : obj_effect_mapping_helpers_airlock_access_all_supply
{
    // tg: code/modules/mapping/access_helpers.dm:860
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_VAULT);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:865
public partial class obj_effect_mapping_helpers_airlock_access_all_supply_bit_den : obj_effect_mapping_helpers_airlock_access_all_supply
{
    // tg: code/modules/mapping/access_helpers.dm:865
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_BIT_DEN);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:874
public partial class obj_effect_mapping_helpers_airlock_access_all_syndicate_general : obj_effect_mapping_helpers_airlock_access_all_syndicate
{
    // tg: code/modules/mapping/access_helpers.dm:874
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_SYNDICATE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:879
public partial class obj_effect_mapping_helpers_airlock_access_all_syndicate_leader : obj_effect_mapping_helpers_airlock_access_all_syndicate
{
    // tg: code/modules/mapping/access_helpers.dm:879
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_SYNDICATE_LEADER);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:888
public partial class obj_effect_mapping_helpers_airlock_access_all_hunter : obj_effect_mapping_helpers_airlock_access_all
{
    // tg: code/modules/mapping/access_helpers.dm:888
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_HUNTER);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:897
public partial class obj_effect_mapping_helpers_airlock_access_all_away_general : obj_effect_mapping_helpers_airlock_access_all_away
{
    // tg: code/modules/mapping/access_helpers.dm:897
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_AWAY_GENERAL);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:902
public partial class obj_effect_mapping_helpers_airlock_access_all_away_command : obj_effect_mapping_helpers_airlock_access_all_away
{
    // tg: code/modules/mapping/access_helpers.dm:902
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_AWAY_COMMAND);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:907
public partial class obj_effect_mapping_helpers_airlock_access_all_away_security : obj_effect_mapping_helpers_airlock_access_all_away
{
    // tg: code/modules/mapping/access_helpers.dm:907
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_AWAY_SEC);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:912
public partial class obj_effect_mapping_helpers_airlock_access_all_away_engineering : obj_effect_mapping_helpers_airlock_access_all_away
{
    // tg: code/modules/mapping/access_helpers.dm:912
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_AWAY_ENGINEERING);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:917
public partial class obj_effect_mapping_helpers_airlock_access_all_away_medical : obj_effect_mapping_helpers_airlock_access_all_away
{
    // tg: code/modules/mapping/access_helpers.dm:917
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_AWAY_MEDICAL);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:922
public partial class obj_effect_mapping_helpers_airlock_access_all_away_supply : obj_effect_mapping_helpers_airlock_access_all_away
{
    // tg: code/modules/mapping/access_helpers.dm:922
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_AWAY_SUPPLY);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:927
public partial class obj_effect_mapping_helpers_airlock_access_all_away_science : obj_effect_mapping_helpers_airlock_access_all_away
{
    // tg: code/modules/mapping/access_helpers.dm:927
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_AWAY_SCIENCE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:932
public partial class obj_effect_mapping_helpers_airlock_access_all_away_maintenance : obj_effect_mapping_helpers_airlock_access_all_away
{
    // tg: code/modules/mapping/access_helpers.dm:932
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_AWAY_MAINTENANCE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:937
public partial class obj_effect_mapping_helpers_airlock_access_all_away_generic1 : obj_effect_mapping_helpers_airlock_access_all_away
{
    // tg: code/modules/mapping/access_helpers.dm:937
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_AWAY_GENERIC1);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:942
public partial class obj_effect_mapping_helpers_airlock_access_all_away_generic2 : obj_effect_mapping_helpers_airlock_access_all_away
{
    // tg: code/modules/mapping/access_helpers.dm:942
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_AWAY_GENERIC2);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:947
public partial class obj_effect_mapping_helpers_airlock_access_all_away_generic3 : obj_effect_mapping_helpers_airlock_access_all_away
{
    // tg: code/modules/mapping/access_helpers.dm:947
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_AWAY_GENERIC3);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:952
public partial class obj_effect_mapping_helpers_airlock_access_all_away_generic4 : obj_effect_mapping_helpers_airlock_access_all_away
{
    // tg: code/modules/mapping/access_helpers.dm:952
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_AWAY_GENERIC4);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:961
public partial class obj_effect_mapping_helpers_airlock_access_all_admin_general : obj_effect_mapping_helpers_airlock_access_all_admin
{
    // tg: code/modules/mapping/access_helpers.dm:961
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CENT_GENERAL);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:966
public partial class obj_effect_mapping_helpers_airlock_access_all_admin_thunderdome : obj_effect_mapping_helpers_airlock_access_all_admin
{
    // tg: code/modules/mapping/access_helpers.dm:966
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CENT_THUNDER);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:971
public partial class obj_effect_mapping_helpers_airlock_access_all_admin_medical : obj_effect_mapping_helpers_airlock_access_all_admin
{
    // tg: code/modules/mapping/access_helpers.dm:971
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CENT_MEDICAL);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:976
public partial class obj_effect_mapping_helpers_airlock_access_all_admin_living : obj_effect_mapping_helpers_airlock_access_all_admin
{
    // tg: code/modules/mapping/access_helpers.dm:976
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CENT_LIVING);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:981
public partial class obj_effect_mapping_helpers_airlock_access_all_admin_storage : obj_effect_mapping_helpers_airlock_access_all_admin
{
    // tg: code/modules/mapping/access_helpers.dm:981
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CENT_STORAGE);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:986
public partial class obj_effect_mapping_helpers_airlock_access_all_admin_teleporter : obj_effect_mapping_helpers_airlock_access_all_admin
{
    // tg: code/modules/mapping/access_helpers.dm:986
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CENT_TELEPORTER);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:991
public partial class obj_effect_mapping_helpers_airlock_access_all_admin_captain : obj_effect_mapping_helpers_airlock_access_all_admin
{
    // tg: code/modules/mapping/access_helpers.dm:991
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CENT_CAPTAIN);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:996
public partial class obj_effect_mapping_helpers_airlock_access_all_admin_bar : obj_effect_mapping_helpers_airlock_access_all_admin
{
    // tg: code/modules/mapping/access_helpers.dm:996
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CENT_BAR);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:1001
public partial class obj_effect_mapping_helpers_airlock_access_all_admin_officer : obj_effect_mapping_helpers_airlock_access_all_admin
{
    // tg: code/modules/mapping/access_helpers.dm:1001
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CENT_OFFICER);
        return access_list;
    }
}

// tg: code/modules/mapping/access_helpers.dm:1006
public partial class obj_effect_mapping_helpers_airlock_access_all_admin_specops : obj_effect_mapping_helpers_airlock_access_all_admin
{
    // tg: code/modules/mapping/access_helpers.dm:1006
    public override object get_access()
    {
        DmList access_list = base.get_access() as DmList;
        Dm.append(ref access_list, ACCESS_CENT_SPECOPS);
        return access_list;
    }
}
