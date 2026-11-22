// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/obsessed/obsessed.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps B1 (D327): the whole file, the obsessed antagonist the obsession trauma makes (brain_damage/creepy_trauma.cs): its
// objectives (the murder, the time spent around the obsession, the polaroid, the hugs, the jealous coworker, the stolen heirloom),
// its preview and its outfit, its round-end report. The var blocks are generated (D111). The family heirloom quirk is inert
// (quirks), so nobody has an heirloom to steal and the heirloom objective is never picked; its find_target defers where the
// heirloom would become the steal target (the port's steal_target holds a typepath, objective.cs, DV340).

// tg: code/modules/antagonists/obsessed/obsessed.dm:7
public partial class datum_antagonist_obsessed : datum_antagonist
{
    // tg: code/modules/antagonists/obsessed/obsessed.dm:1  #define OBSESSED_OBJECTIVE_SPEND_TIME "spend_time" (#undef at :304)
    private const string OBSESSED_OBJECTIVE_SPEND_TIME = "spend_time";
    // tg: code/modules/antagonists/obsessed/obsessed.dm:2  #define OBSESSED_OBJECTIVE_POLAROID "polaroid" (#undef at :305)
    private const string OBSESSED_OBJECTIVE_POLAROID = "polaroid";
    // tg: code/modules/antagonists/obsessed/obsessed.dm:3  #define OBSESSED_OBJECTIVE_HUG "hug" (#undef at :306)
    private const string OBSESSED_OBJECTIVE_HUG = "hug";
    // tg: code/modules/antagonists/obsessed/obsessed.dm:4  #define OBSESSED_OBJECTIVE_HEIRLOOM "heirloom" (#undef at :307)
    private const string OBSESSED_OBJECTIVE_HEIRLOOM = "heirloom";
    // tg: code/modules/antagonists/obsessed/obsessed.dm:5  #define OBSESSED_OBJECTIVE_JEALOUS "jealous" (#undef at :308)
    private const string OBSESSED_OBJECTIVE_JEALOUS = "jealous";

    // tg: code/modules/antagonists/obsessed/obsessed.dm:27
    public override object can_be_owned(datum_mind new_owner = null)
    {
        if (!Dm.truthy(base.can_be_owned(new_owner)))
            return FALSE;
        return new_owner.current?.get_organ_by_type(typeof(obj_item_organ_brain)); // gotta have a brain to be obsessed!
    }

    // tg: code/modules/antagonists/obsessed/obsessed.dm:42
    public override async DmTask admin_add(datum_mind new_owner = null, mob admin = null)
    {
        mob_living_carbon C = new_owner.current as mob_living_carbon;
        if (!istype(C, typeof(mob_living_carbon)))
        {
            to_chat(admin, I18n.T("[] come from a brain trauma, so they need to at least be a carbon!", roundend_category));
            return;
        }
        if (C.get_organ_by_type(typeof(obj_item_organ_brain)) == null) // If only I had a brain
        {
            to_chat(admin, I18n.T("[] come from a brain trauma, so they need to HAVE A BRAIN.", roundend_category));
            return;
        }
        message_admins(Dm.text("[] made [] into [].", key_name_admin(admin), key_name_admin(new_owner), name));
        log_admin(Dm.text("[] made [] into [].", key_name(admin), key_name(new_owner), name));
        //PRESTO FUCKIN MAJESTO
        await C.gain_trauma(typeof(datum_brain_trauma_special_obsessed));//ZAP
    }

    // tg: code/modules/antagonists/obsessed/obsessed.dm:55
    public override async DmTask greet()
    {
        play_stinger();
        await owner.announce_objectives();
    }

    // tg: code/modules/antagonists/obsessed/obsessed.dm:59
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref trauma);
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/obsessed/obsessed.dm:63
    public override async DmTask<object> get_preview_icon()
    {
        icon obsessed_icon = await render_preview_outfit(preview_outfit) as icon;
        icon blood_icon = Dm.icon(Dm.resource("icons/effects/blood.dmi"), "uniformblood"); // uni_icon(...)
        blood_icon.Blend(BLOOD_COLOR_RED, ICON_MULTIPLY); // blend_color
        obsessed_icon.Blend(blood_icon, ICON_OVERLAY); // blend_icon

        icon final_icon = finish_preview_icon(obsessed_icon) as icon;

        final_icon.Blend(
            Dm.icon(Dm.resource("icons/ui/antags/obsessed.dmi"), "obsession"),
            ICON_OVERLAY,
            ANTAGONIST_PREVIEW_ICON_SIZE - 30,
            20);

        return final_icon;
    }

    // tg: code/modules/antagonists/obsessed/obsessed.dm:95
    public override async DmTask forge_objectives(params object[] __extra)
    {
        datum_mind obsessionmind = Dm.arg<datum_mind>(__extra, 0);
        DmList objectives_left = Dm.list(OBSESSED_OBJECTIVE_SPEND_TIME, OBSESSED_OBJECTIVE_POLAROID, OBSESSED_OBJECTIVE_HUG);
        datum_objective_assassinate_obsessed kill = Dm.@new<datum_objective_assassinate_obsessed>();
        kill.owner = owner;
        kill.target = obsessionmind;

        if (Dm.truthy((obsessionmind.current as mob_living)?.has_quirk(typeof(datum_quirk_item_quirk_family_heirloom))))
            Dm.append(objectives_left, OBSESSED_OBJECTIVE_HEIRLOOM);

        // If they have no coworkers, jealousy will pick someone else on the station. This will never be a free objective.
        if (!is_captain_job(obsessionmind.assigned_role))
            Dm.append(objectives_left, OBSESSED_OBJECTIVE_JEALOUS);

        for (float i = 1; i <= objectives_to_generate; i++)
        {
            object chosen_objective = pick_n_take(objectives_left);
            switch (Dm.str(chosen_objective))
            {
                case OBSESSED_OBJECTIVE_SPEND_TIME:
                {
                    datum_objective_spendtime spendtime = Dm.@new<datum_objective_spendtime>();
                    spendtime.owner = owner;
                    spendtime.target = obsessionmind;
                    Dm.append(ref objectives, spendtime);
                    break;
                }
                case OBSESSED_OBJECTIVE_POLAROID:
                {
                    datum_objective_polaroid polaroid = Dm.@new<datum_objective_polaroid>();
                    polaroid.owner = owner;
                    polaroid.target = obsessionmind;
                    polaroid.obsession_weakref = WEAKREF(obsessionmind.current) as datum_weakref;
                    Dm.append(ref objectives, polaroid);
                    break;
                }
                case OBSESSED_OBJECTIVE_HUG:
                {
                    datum_objective_hug hug = Dm.@new<datum_objective_hug>();
                    hug.owner = owner;
                    hug.target = obsessionmind;
                    Dm.append(ref objectives, hug);
                    break;
                }
                case OBSESSED_OBJECTIVE_HEIRLOOM:
                {
                    datum_objective_steal_heirloom_thief heirloom_thief = Dm.@new<datum_objective_steal_heirloom_thief>();
                    heirloom_thief.owner = owner;
                    heirloom_thief.target = obsessionmind;
                    await heirloom_thief.find_target();
                    Dm.append(ref objectives, heirloom_thief);
                    break;
                }
                case OBSESSED_OBJECTIVE_JEALOUS:
                {
                    datum_objective_assassinate_jealous jealous = Dm.@new<datum_objective_assassinate_jealous>();
                    jealous.owner = owner;
                    jealous.obsessed_target = obsessionmind;
                    await jealous.find_target();
                    Dm.append(ref objectives, jealous);
                    break;
                }
            }
        }

        Dm.append(ref objectives, kill);//finally add the assassinate last, because you'd have to complete it last to greentext.

        foreach (datum_objective O in Dm.each<datum_objective>(objectives))
            O.update_explanation_text();
    }

    // tg: code/modules/antagonists/obsessed/obsessed.dm:145
    public override object roundend_report_header()
    {
        return span_header(I18n.T("Someone became obsessed!<br>"));
    }

    // tg: code/modules/antagonists/obsessed/obsessed.dm:148
    public override object roundend_report()
    {
        DmList report = Dm.list();

        if (owner == null)
            throw Dm.CRASH("antagonist datum without owner", "code/modules/antagonists/obsessed/obsessed.dm", 152);

        Dm.append(report, Dm.text("<b>[]</b>", printplayer(owner)));

        bool objectives_complete = true;
        if (Dm.truthy(objectives.len))
        {
            Dm.append(report, printobjectives(objectives));
            foreach (datum_objective objective in Dm.each<datum_objective>(objectives))
            {
                if (!Dm.truthy(objective.check_completion()))
                {
                    objectives_complete = false;
                    break;
                }
            }
        }
        if (trauma != null)
        {
            if (trauma.total_time_creeping > 0)
                Dm.append(report, span_greentext(I18n.T("The [] spent a total of [] being near []!", name, DisplayTimeText(trauma.total_time_creeping), trauma.obsession)));
            else
                Dm.append(report, span_redtext(I18n.T("The [] did not go near their obsession the entire round! That's extremely impressive!", name)));
        }
        else
            Dm.append(report, span_redtext(I18n.T("The [] had no trauma attached to their antagonist ways! Either it bugged out or an admin incorrectly gave this good samaritan antag and it broke! You might as well show yourself!!", name)));

        if (objectives.len == 0 || objectives_complete)
            Dm.append(report, I18n.T("<span class='greentext big'>The [] was successful!</span>", name));
        else
            Dm.append(report, I18n.T("<span class='redtext big'>The [] has failed!</span>", name));

        return report.Join("<br>");
    }
}

// tg: code/modules/antagonists/obsessed/obsessed.dm:80
public partial class datum_outfit_obsessed : datum_outfit
{
    // tg: code/modules/antagonists/obsessed/obsessed.dm:90
    public override async DmTask post_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        mob_living_carbon_human H = user;
        foreach (obj_item carried_item in Dm.each<obj_item>(H.get_equipped_items(Dm.bitor(INCLUDE_POCKETS, INCLUDE_ACCESSORIES))))
            await carried_item.add_mob_blood(H);//Oh yes, there will be blood...
        await H.regenerate_icons();
    }
}

//////////////////////////////////////////////////
///CREEPY objectives (few chosen per obsession)///
//////////////////////////////////////////////////

// tg: code/modules/antagonists/obsessed/obsessed.dm:182
public partial class datum_objective_assassinate_obsessed : datum_objective_assassinate //just a creepy version of assassinate
{
    // tg: code/modules/antagonists/obsessed/obsessed.dm:184
    public override void update_explanation_text()
    {
        if (target?.current != null)
            explanation_text = I18n.T("Murder [], the [].", target.name, Dm.truthy(target_role_type) ? english_list(target.get_special_roles() as DmList) : target.assigned_role.title);
        else
        {
            message_admins(Dm.text("WARNING! [] obsessed objectives forged without an obsession!", ADMIN_LOOKUPFLW(owner)));
            explanation_text = I18n.T("Free Objective");
            completed = TRUE;
        }
    }
}

// tg: code/modules/antagonists/obsessed/obsessed.dm:192
public partial class datum_objective_assassinate_jealous : datum_objective_assassinate //assassinate, but it changes the target to someone else in the previous target's department. cool, right?
{
    // tg: code/modules/antagonists/obsessed/obsessed.dm:195
    public override void update_explanation_text()
    {
        if (target?.current != null && obsessed_target != null)
            explanation_text = I18n.T("Murder [], []'s coworker.", target.name, obsessed_target);
        else
        {
            explanation_text = I18n.T("Free Objective");
            completed = TRUE;
        }
    }

    // tg: code/modules/antagonists/obsessed/obsessed.dm:202
    public override async DmTask<object> find_target(object dupe_search_range = null, DmList blacklist = null)
    {
        if (is_unassigned_job(obsessed_target.assigned_role))
            return await base.find_target(dupe_search_range, blacklist);

        DmList viable_coworkers = Dm.list();
        DmList all_coworkers = Dm.list();
        object our_departments = obsessed_target.assigned_role.departments_bitflags;
        foreach (datum_mind crewmember in Dm.each_cast<datum_mind>(get_crewmember_minds()))
        {
            if (crewmember == obsessed_target || Dm.truthy(crewmember.has_antag_datum(typeof(datum_antagonist_obsessed))))
                continue; // the jealousy target has to have a job, and not be the obsession or obsessed.

            if (Dm.truthy(Dm.bitand(our_departments, crewmember.assigned_role.departments_bitflags)))
                Dm.append(viable_coworkers, crewmember);
            Dm.append(all_coworkers, crewmember);
        }

        if (Dm.truthy(length(viable_coworkers))) // find someone in the same department
            target = pick(viable_coworkers) as datum_mind;
        else if (Dm.truthy(length(all_coworkers))) // find someone who works on the station
            target = pick(all_coworkers) as datum_mind;
        update_explanation_text();
        return target;
    }
}

// tg: code/modules/antagonists/obsessed/obsessed.dm:224
public partial class datum_objective_spendtime : datum_objective //spend some time around someone, handled by the obsessed trauma since that ticks
{
    // tg: code/modules/antagonists/obsessed/obsessed.dm:228
    public override void update_explanation_text()
    {
        if (!Dm.truthy(timer))
            timer = 5 * MINUTES + Dm.num(pick(-60 * SECONDS, 0));

        if (target?.current != null)
            explanation_text = I18n.T("Spend [] around [] while they're alive.", DisplayTimeText(timer), target.name);
        else
        {
            explanation_text = I18n.T("Free Objective");
            completed = TRUE;
        }
    }

    // tg: code/modules/antagonists/obsessed/obsessed.dm:238
    public override object check_completion()
    {
        return Dm.num(timer <= 0 || Dm.truthy(completed));
    }
}

// tg: code/modules/antagonists/obsessed/obsessed.dm:241
public partial class datum_objective_hug : datum_objective//this objective isn't perfect. hugging the correct amount of times, then switching bodies, might fail the objective anyway. maybe i'll come back and fix this sometime.
{
    // tg: code/modules/antagonists/obsessed/obsessed.dm:245
    public override void update_explanation_text()
    {
        if (!Dm.truthy(hugs_needed))
            hugs_needed = rand(4, 6);

        if (target?.current != null)
            explanation_text = I18n.T("Hug [] [] times while they're alive.", target.name, hugs_needed);

        else
        {
            explanation_text = I18n.T("Free Objective");
            completed = TRUE;
        }
    }

    // tg: code/modules/antagonists/obsessed/obsessed.dm:256
    public override object check_completion()
    {
        return Dm.num(hugs_needed <= 0 || Dm.truthy(completed));
    }
}

// tg: code/modules/antagonists/obsessed/obsessed.dm:259
public partial class datum_objective_polaroid : datum_objective //take a picture of the target with you in it.
{
    // tg: code/modules/antagonists/obsessed/obsessed.dm:264
    public override void update_explanation_text()
    {
        if (target?.current != null)
            explanation_text = I18n.T("Take a photo of [] while they're alive, and keep it in your bag.", target.name);
        else
        {
            explanation_text = I18n.T("Free Objective");
            completed = TRUE;
        }
    }

    // tg: code/modules/antagonists/obsessed/obsessed.dm:271
    public override object check_completion()
    {
        foreach (obj_item_photo photo in Dm.each<obj_item_photo>(owner.current?.get_all_contents()))
        {
            if ((Dm.@in(obsession_weakref, photo.picture?.mobs_seen)) && !(Dm.@in(obsession_weakref, photo.picture?.dead_seen)))
                return TRUE;
        }
        return FALSE;
    }
}

// tg: code/modules/antagonists/obsessed/obsessed.dm:278
public partial class datum_objective_steal_heirloom_thief : datum_objective_steal //exactly what it sounds like, steal someone's heirloom.
{
    // tg: code/modules/antagonists/obsessed/obsessed.dm:281
    public override void update_explanation_text()
    {
        if (steal_target != null)
            explanation_text = I18n.T("Steal []'s family heirloom, [] they cherish.", target, steal_target);
        else
        {
            explanation_text = I18n.T("Free Objective");
            completed = TRUE;
        }
    }

    // tg: code/modules/antagonists/obsessed/obsessed.dm:288
    public override async DmTask<object> find_target(object dupe_search_range = null, DmList blacklist = null)
    {
        foreach (datum_quirk_item_quirk_family_heirloom quirky in Dm.each<datum_quirk_item_quirk_family_heirloom>((target?.current as mob_living)?.quirks))
        {
            // steal_target = quirky.heirloom?.resolve(); RegisterSignal(steal_target, COMSIG_QDELETING, PROC_REF(steal_target_deleted))
            Dm.Deferred("code/modules/antagonists/obsessed/obsessed.dm:290", "find_target: the heirloom as the steal target (the port's steal_target holds a typepath, objective.cs DV340; the family heirloom quirk is inert, quirks)");
            break;
        }

        update_explanation_text();
        await DmTask.CompletedTask;
        return steal_target;
    }

    // tg: code/modules/antagonists/obsessed/obsessed.dm:297
    public override object check_completion()
    {
        return Dm.num((steal_target != null && (Dm.@in(steal_target, owner.current?.get_all_contents()))) || Dm.truthy(completed));
    }

    // tg: code/modules/antagonists/obsessed/obsessed.dm:300
    [SignalHandler]
    public virtual void steal_target_deleted()
    {
        steal_target = null; // it's gone, and so are our hopes and dreams
    }
}
