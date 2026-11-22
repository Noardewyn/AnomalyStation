// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/research/anomalies.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;


namespace Anomaly.Core.Tg;

// PARTIAL: only the bioscrambler's lists translated (the M7 delamination slice, G6: the bioscrambler anomaly the delamination
// makes); the anomaly event defines are generated constants.

// tg: code/__DEFINES/research/anomalies.dm:22
public partial class datum_controller_global_vars
{
    /// Blacklist of parts which should not appear when bioscrambled, largely because they will make you look totally fucked up
    // tg: code/__DEFINES/research/anomalies.dm:22  GLOBAL_LIST_INIT(bioscrambler_parts_blacklist, typecacheof(list(...)))
    public DmList bioscrambler_parts_blacklist;

    // tg: code/__DEFINES/research/anomalies.dm:22  (GLOBAL_MANAGED(bioscrambler_parts_blacklist, typecacheof(list(...))), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbioscrambler_parts_blacklist()
    {
        bioscrambler_parts_blacklist = typecacheof(Dm.list(
            typeof(obj_item_bodypart_head_psyker),
            typeof(obj_item_bodypart_arm_left_skeleton_nonfunctional),
            typeof(obj_item_bodypart_arm_right_skeleton_nonfunctional),
            typeof(obj_item_bodypart_chest_skeleton_nonfunctional),
            typeof(obj_item_bodypart_head_skeleton_nonfunctional),
            typeof(obj_item_bodypart_leg_left_skeleton_nonfunctional),
            typeof(obj_item_bodypart_leg_right_skeleton_nonfunctional)
            )) as DmList;
        Dm.append(ref gvars_datum_init_order, "bioscrambler_parts_blacklist");
    }

    /// Blacklist of organs which should not appear when bioscrambled.
    /// Either will look terrible outside of intended host, give you magical powers, are irreversible, or kill you
    // tg: code/__DEFINES/research/anomalies.dm:34  GLOBAL_LIST_INIT(bioscrambler_organs_blacklist, typecacheof(list(...)))
    public DmList bioscrambler_organs_blacklist;

    // tg: code/__DEFINES/research/anomalies.dm:34  (GLOBAL_MANAGED(bioscrambler_organs_blacklist, typecacheof(list(...))), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbioscrambler_organs_blacklist()
    {
        bioscrambler_organs_blacklist = typecacheof(Dm.list(
            typeof(obj_item_organ_pod_hair),
            typeof(obj_item_organ_spines),
            typeof(obj_item_organ_wings),
            typeof(obj_item_organ_alien),
            typeof(obj_item_organ_brain),
            typeof(obj_item_organ_body_egg),
            typeof(obj_item_organ_cyberimp),
            typeof(obj_item_organ_ears_dullahan),
            typeof(obj_item_organ_eyes_dullahan),
            typeof(obj_item_organ_heart_cursed),
            typeof(obj_item_organ_heart_demon),
            typeof(obj_item_organ_lungs),
            typeof(obj_item_organ_monster_core),
            typeof(obj_item_organ_tongue_dullahan),
            typeof(obj_item_organ_vocal_cords_colossus),
            typeof(obj_item_organ_zombie_infection)
            )) as DmList;
        Dm.append(ref gvars_datum_init_order, "bioscrambler_organs_blacklist");
    }

    /// List of body parts we can apply to people
    // tg: code/__DEFINES/research/anomalies.dm:54  GLOBAL_LIST_EMPTY(bioscrambler_valid_parts)
    public DmList bioscrambler_valid_parts;

    // tg: code/__DEFINES/research/anomalies.dm:54  (GLOBAL_MANAGED(bioscrambler_valid_parts, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbioscrambler_valid_parts()
    {
        bioscrambler_valid_parts = Dm.list();
        Dm.append(ref gvars_datum_init_order, "bioscrambler_valid_parts");
    }

    /// List of organs we can apply to people
    // tg: code/__DEFINES/research/anomalies.dm:56  GLOBAL_LIST_EMPTY(bioscrambler_valid_organs)
    public DmList bioscrambler_valid_organs;

    // tg: code/__DEFINES/research/anomalies.dm:56  (GLOBAL_MANAGED(bioscrambler_valid_organs, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbioscrambler_valid_organs()
    {
        bioscrambler_valid_organs = Dm.list();
        Dm.append(ref gvars_datum_init_order, "bioscrambler_valid_organs");
    }
}
