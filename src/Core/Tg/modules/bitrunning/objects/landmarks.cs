// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/objects/landmarks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file. The landmarks the server reads at a domain's boot are var blocks (generated, D111); the crate
// randomizer swaps the encrypted cache with another crate of its area. The mob segment's spawn is in
// virtual_domain/modular_mob_segment.cs.

///Swaps the locations of an encrypted crate in the area with another randomly selected crate.
///Randomizes names, so you have to inspect crates manually.
// tg: code/modules/bitrunning/objects/landmarks.dm:38
public partial class obj_effect_landmark_bitrunning_crate_replacer : obj_effect_landmark_bitrunning
{
    // tg: code/modules/bitrunning/objects/landmarks.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        // tg: #ifdef UNIT_TESTS ... #endif not translated (inactive in production)

        DmList crate_list = Dm.list();
        obj_structure_closet_crate_secure_bitrunning_encrypted encrypted_crate = null;
        area my_area = get_area(this) as area;

        foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(my_area.get_zlevel_turf_lists()))
        {
            foreach (turf area_turf in Dm.each_cast<turf>(zlevel_turfs))
            {
                foreach (obj_structure_closet_crate crate_to_check in Dm.each<obj_structure_closet_crate>(area_turf))
                {
                    if (crate_to_check is obj_structure_closet_crate_secure_bitrunning_encrypted)
                    {
                        encrypted_crate = crate_to_check as obj_structure_closet_crate_secure_bitrunning_encrypted;
                        crate_to_check.desc += span_hypnophrase(I18n.T(" This feels like the crate we're looking for!"));
                    }
                    else
                        Dm.append(ref crate_list, crate_to_check);
                    crate_to_check.name = "Unidentified Crate";
                }
            }
        }

        if (encrypted_crate == null)
        {
            stack_trace("Bitrunning Goal Crate Randomizer failed to find an encrypted crate to swap positions for.", "code/modules/bitrunning/objects/landmarks.dm", 63);
            return dot;
        }
        if (!Dm.truthy(length(crate_list)))
        {
            stack_trace("Bitrunning Goal Crate Randomizer failed to find any NORMAL crates to swap positions for.", "code/modules/bitrunning/objects/landmarks.dm", 66);
            return dot;
        }

        atom original_location = encrypted_crate.loc;
        obj_structure_closet_crate selected_crate = pick(crate_list) as obj_structure_closet_crate;

        Waiting.NoSleep(encrypted_crate.abstract_move(selected_crate.loc));
        Waiting.NoSleep(selected_crate.abstract_move(original_location));
        return dot;
    }
}
