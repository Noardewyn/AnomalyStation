// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/station_trait/human_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the Human AI's job, which the Human AI station trait opens in place of the AI. The player who signs up in the lobby
// starts at the AI's core spot in the robotic ID and the human AI's jumpsuit, with the omni door remote, the machine remote, the
// pre-packaged camera console, the crew sensor device, the infinite laser pointer, the flash and the lawset note, a robotic tongue and
// the silicons' and turrets' factions (and a void suit where the spot is not safe to breathe). The var blocks are generated.

// tg: code/modules/jobs/job_types/station_trait/human_ai.dm:1
public partial class datum_job_human_ai : datum_job
{
    // tg: code/modules/jobs/job_types/station_trait/human_ai.dm:45
    public override async DmTask<object> get_roundstart_spawn_point()
    {
        return await get_latejoin_spawn_point();
    }

    // tg: code/modules/jobs/job_types/station_trait/human_ai.dm:48
    public override async DmTask<object> get_latejoin_spawn_point()
    {
        await DmTask.CompletedTask;
        foreach (obj_structure_ai_core_latejoin_inactive inactive_core in Dm.each<obj_structure_ai_core_latejoin_inactive>(GLOB.latejoin_ai_cores.Copy()))
        {
            if (!inactive_core.is_available())
                continue;
            Dm.remove(ref GLOB.latejoin_ai_cores, inactive_core);
            inactive_core.available = FALSE;
            turf core_turf = get_turf(inactive_core);
            qdel(inactive_core);
            return core_turf;
        }
        DmList primary_spawn_points = Dm.list(); // Ideal locations.
        DmList secondary_spawn_points = Dm.list(); // Fallback locations.
        foreach (obj_effect_landmark_start_ai spawn_point in Dm.each<obj_effect_landmark_start_ai>(GLOB.landmarks_list))
        {
            if (Dm.truthy(spawn_point.used))
            {
                Dm.append(ref secondary_spawn_points, Dm.list(spawn_point));
                continue;
            }
            if (Dm.truthy(spawn_point.primary_ai))
            {
                primary_spawn_points = Dm.list(spawn_point);
                break; // Bingo.
            }
            Dm.append(ref primary_spawn_points, spawn_point);
        }
        obj_effect_landmark_start_ai chosen_spawn_point;
        if (Dm.truthy(length(primary_spawn_points)))
            chosen_spawn_point = pick(primary_spawn_points) as obj_effect_landmark_start_ai;
        else if (Dm.truthy(length(secondary_spawn_points)))
            chosen_spawn_point = pick(secondary_spawn_points) as obj_effect_landmark_start_ai;
        else
            throw Dm.CRASH("Failed to find any AI spawn points.", "code/modules/jobs/job_types/station_trait/human_ai.dm", 73);
        chosen_spawn_point.used = TRUE;
        return chosen_spawn_point;
    }

    // tg: code/modules/jobs/job_types/station_trait/human_ai.dm:77
    public override object special_check_latejoin(client latejoin = null)
    {
        foreach (obj_structure_ai_core_latejoin_inactive latejoin_core in Dm.each<obj_structure_ai_core_latejoin_inactive>(GLOB.latejoin_ai_cores))
        {
            if (latejoin_core.is_available())
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/jobs/job_types/station_trait/human_ai.dm:83
    public override async DmTask announce_job(mob_living joining_mob = null)
    {
        await base.announce_job(joining_mob);
        if (Dm.truthy(SSticker.HasRoundStarted()))
            minor_announce(Dm.text("Due to a research mishap, [] has been sent to be your replacement AI at []. Please treat them with respect.", joining_mob, AREACOORD(joining_mob)));
    }

    // tg: code/modules/jobs/job_types/station_trait/human_ai.dm:88
    public override object get_radio_information()
    {
        return I18n.T("<b>Prefix your message with :b to speak with cyborgs.</b>");
    }
}

// tg: code/modules/jobs/job_types/station_trait/human_ai.dm:91
public partial class datum_outfit_job_human_ai : datum_outfit_job
{
    // tg: code/modules/jobs/job_types/station_trait/human_ai.dm:114
    public override async DmTask pre_equip(mob_living_carbon_human equipped = null, float? visuals_only = null)
    {
        await base.pre_equip(equipped, visuals_only);
        if (Dm.truthy(visuals_only))
            return;
        if (Dm.truthy(is_safe_turf(equipped.loc as turf, dense_atoms: TRUE))) //skip this if it's safe. We allow dense atoms because we spawn out of the inactive core.
            return;
        if ((equipped.dna.species as datum_species).outfit_important_for_life == null) //custom species stuff will handle this for us.
        {
            internals_slot = ITEM_SLOT_SUITSTORE;
            suit_store = typeof(obj_item_tank_internals_oxygen);
        }
        suit = typeof(obj_item_clothing_suit_space_nasavoid);
        head = typeof(obj_item_clothing_head_helmet_space_nasavoid);
    }

    // tg: code/modules/jobs/job_types/station_trait/human_ai.dm:126
    public override async DmTask post_equip(mob_living_carbon_human equipped = null, float? visuals_only = null)
    {
        await base.post_equip(equipped, visuals_only);
        if (Dm.truthy(visuals_only))
            return;
        if (!Dm.truthy(equipped.get_quirk(typeof(datum_quirk_body_purist))))
        {
            var cybernetic = Dm.@new<obj_item_organ_tongue_robot>();
            await cybernetic.Insert(equipped, special: TRUE, movement_flags: DELETE_IF_REPLACED);
            //you only get respect if you go all the way, man.
            ADD_TRAIT(equipped, TRAIT_COMMISSIONED, INNATE_TRAIT);
        }
        equipped.add_faction(Dm.list(FACTION_SILICON, FACTION_TURRET));
    }
}

// tg: code/modules/jobs/job_types/station_trait/human_ai.dm:137
public partial class obj_item_paper_default_lawset_list : obj_item_paper
{
    // tg: code/modules/jobs/job_types/station_trait/human_ai.dm:141
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/modules/jobs/job_types/station_trait/human_ai.dm:145
    public override void LateInitialize(params object[] __extra)
    {
        datum_ai_laws reported = null;
        foreach (obj_machinery_ai_law_rack_base_core rack in Dm.each<obj_machinery_ai_law_rack_base_core>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_ai_law_rack_base_core))))
        {
            reported = rack.combined_lawset;
            break;
        }

        if (reported == null)
            return;

        DmList law_box = Dm.list(
            I18n.T("This is your lawset, you and your Cyborgs must adhere to this at all times."),
            I18n.T("Notably, if absolutely necessary, you can bend or even go against the lawset for your own survival, and Cyborgs report to you directly."),
            I18n.T("LAWS:"));
        Dm.append(ref law_box, reported.get_law_list(render_html: FALSE));
        add_raw_text(jointext(law_box, "\n"));
    }
}

// tg: code/modules/jobs/job_types/station_trait/human_ai.dm:162
public partial class obj_item_secure_camera_console_pod : obj_item
{
    // tg: code/modules/jobs/job_types/station_trait/human_ai.dm:174  var/static/list/allowed_areas (a proc-level static, rules 10.6)
    private static ref DmList attack_self__allowed_areas => ref TgState.Current.obj_item_secure_camera_console_pod__attack_self__allowed_areas;

    // tg: code/modules/jobs/job_types/station_trait/human_ai.dm:171
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        area current_area = get_area(user) as area;
        attack_self__allowed_areas ??= typecacheof(Dm.list(typeof(area_station_ai_satellite_chamber))) as DmList;
        if (!Dm.truthy(is_type_in_typecache(current_area, attack_self__allowed_areas)))
        {
            user.balloon_alert(user, I18n.T("not in the sat!"));
            return dot;
        }
        user.balloon_alert(user, I18n.T("unpacking..."));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, this)))
            return dot;
        playsound(this, Dm.resource("sound/items/tools/drill_use.ogg"), 40, TRUE);
        Dm.@new<obj_machinery_computer_camera_advanced_human_ai>(get_turf(this));
        qdel(this);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/jobs/job_types/station_trait/human_ai.dm:174  /obj/item/secure_camera_console_pod/attack_self's var/static/list/allowed_areas
    internal DmList obj_item_secure_camera_console_pod__attack_self__allowed_areas;
}
