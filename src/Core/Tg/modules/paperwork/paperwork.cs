// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/paperwork.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298). The documents are made by the paperwork event and sold by cargo (P4): until then they come from
// the ancient paperwork folder and the photocopier. The var blocks are generated.

/**
 * # Paperwork
 *
 * Paperwork documents that can be stamped by their associated stamp to provide a bonus to cargo.
 *
 * Paperwork documents are a cargo item meant to provide the opportunity to make money.
 * Each piece of paperwork has its own associated stamp it needs to be stamped with. Selling a
 * properly stamped piece of paperwork will provide a cash bonus to the cargo budget. If a document is
 * not properly stamped it will instead drain a small stipend from the cargo budget.
 *
 */
// tg: code/modules/paperwork/paperwork.dm:13
public partial class obj_item_paperwork : obj_item
{
    // tg: code/modules/paperwork/paperwork.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        detailed_desc = span_notice(I18n.T("<i>As you sift through the papers, you slowly start to piece together what you're reading.</i>"));
        return dot;
    }

    // tg: code/modules/paperwork/paperwork.dm:46
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_stamp)))
            return NONE;

        if (Dm.truthy(stamped))
            return ITEM_INTERACT_BLOCKING;

        if (istype(tool, stamp_requested))
        {
            add_stamp();
            to_chat(user, span_notice(I18n.T("You skim through the papers until you find a field reading 'STAMP HERE', and complete the paperwork.")));
            return ITEM_INTERACT_SUCCESS;
        }

        datum_action_item_action_chameleon_change_stamp stamp_action = Dm.locate<datum_action_item_action_chameleon_change_stamp>(tool.actions);
        if (stamp_action == null)
        {
            to_chat(user, span_warning(I18n.T("You hunt through the papers for somewhere to use [], but can't find anything.", tool)));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("[] morphs into the appropriate stamp, which you use to complete the paperwork.", tool)));
        // stamp_action.update_look(stamp_requested): the chameleon stamp's action is inert (the chameleon kit, antagonists)
        Dm.Deferred("code/modules/paperwork/paperwork.dm:65", "item_interaction: the chameleon stamp's update_look (the chameleon actions are inert, antagonists)");
        add_stamp();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/paperwork/paperwork.dm:69
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);

        if (ishuman(user))
        {
            mob_living_carbon_human viewer = (mob_living_carbon_human)user;
            if (stamp_job != null && istype(viewer.mind?.assigned_role, stamp_job)) //Examining the paperwork as the proper job gets you some bonus details
                Dm.append(ref dot, detailed_desc);
            else
            {
                if (Dm.truthy(stamped))
                    Dm.append(ref dot, span_info(I18n.T("It looks like these documents have already been stamped. Now they can be returned to Central Command.")));
                else
                {
                    object title = stamp_job == null ? null : Dm.initial(stamp_job, (datum_job j) => j.title);
                    Dm.append(ref dot, span_info(I18n.T("Trying to read through it makes your head spin. Judging by the few words you can make out, this looks like a job for the [].", title)));
                }
            }
        }
        return dot;
    }

    // tg: code/modules/paperwork/paperwork.dm:84
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins insulting the inefficiency of paperwork and bureaucracy. It looks like [] trying to commit suicide!", user, user.p_theyre())));

        obj_item_paper new_paper = Dm.@new<obj_item_paper>(get_turf(this));
        turf turf_to_throw_at = get_ranged_target_turf(get_turf(this), pick(GLOB.alldirs)) as turf;
        await new_paper.throw_at(turf_to_throw_at, 2);

        obj_item_bodypart BP = user.get_bodypart(pick(BODY_ZONE_HEAD) as string) as obj_item_bodypart;
        if (BP != null && Dm.truthy(await BP.dismember()))
            new_paper.visible_message(span_alert(I18n.T("The [] launches a sheet of paper, instantly slicing off []'s head!", this, user)));
        else
        {
            user.visible_message(span_suicide(I18n.T("[] panics and starts choking to death!", user)));
            return OXYLOSS;
        }

        return MANUAL_SUICIDE;
    }

    /**
     * Adds the stamp overlay and sets "stamped" to true
     *
     * Adds the stamp overlay to a piece of paperwork, and sets "stamped" to true.
     * Handled as a proc so that an object may be marked as "stamped" even when a stamp isn't present (like the photocopier)
     */
    // tg: code/modules/paperwork/paperwork.dm:108
    public virtual void add_stamp()
    {
        stamp_overlay = mutable_appearance(Dm.resource("icons/obj/service/bureaucracy.dmi"), stamp_icon) as mutable_appearance;
        add_overlay(stamp_overlay);
        stamped = TRUE;
    }

    /**
     * Copies the requested stamp, associated job, and associated icon of a given paperwork type
     *
     * Copies the stamp/job related info of a given paperwork type to the object
     * Used to mutate photocopied/ancient paperwork into behaving like their subtype counterparts without the extra details
     */
    // tg: code/modules/paperwork/paperwork.dm:119  (paperwork_type is object, Appendix A: the ancient paperwork passes a typepath, the
    // photocopier an instance; initial() reads either)
    public virtual void copy_stamp_info(object paperwork_type = null)
    {
        stamp_requested = Dm.initial(paperwork_type, (obj_item_paperwork p) => p.stamp_requested);
        stamp_job = Dm.initial(paperwork_type, (obj_item_paperwork p) => p.stamp_job);
        stamp_icon = Dm.initial(paperwork_type, (obj_item_paperwork p) => p.stamp_icon);
    }
}

//HEAD OF STAFF DOCUMENTS

// tg: code/modules/paperwork/paperwork.dm:126
public partial class obj_item_paperwork_cargo : obj_item_paperwork
{
    // tg: code/modules/paperwork/paperwork.dm:131
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        detailed_desc += span_info(I18n.T(" The papers are a mess of shipping order paperwork. There's no rhyme or reason to how these documents are sorted at all."));
        detailed_desc += span_info(I18n.T(" By the looks of it, there's nothing out of the ordinary here besides a high-priority request for a second engine."));
        detailed_desc += span_info(I18n.T(" The 'priority request reason' field is scribbled out, but a note in the margins reads 'we just want to try two engines, don't worry about it'."));
        detailed_desc += span_info(I18n.T(" Despite how disorganized the documents are, they're all appropriately filled in. You should probably stamp this."));
        return dot;
    }
}

// tg: code/modules/paperwork/paperwork.dm:139
public partial class obj_item_paperwork_security : obj_item_paperwork
{
    // tg: code/modules/paperwork/paperwork.dm:144
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        detailed_desc += span_info(I18n.T(" The stack of documents is related to a civil case being processed by a neighboring installation."));
        detailed_desc += span_info(I18n.T(" The document requests that you review a conduct report submitted by the lawyer of the station."));
        detailed_desc += span_info(I18n.T(" The case file details accusations against the station's security department, including misconduct, harassment, an-"));
        detailed_desc += span_info(I18n.T(" What a bunch of crap, the security team were clearly just doing what they had to. You should probably stamp this."));
        return dot;
    }
}

// tg: code/modules/paperwork/paperwork.dm:152
public partial class obj_item_paperwork_service : obj_item_paperwork
{
    // tg: code/modules/paperwork/paperwork.dm:157
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        detailed_desc += span_info(I18n.T(" You begin scanning over the document. This is a standard Nanotrasen NT-435Z3 form used for requests to Central Command."));
        detailed_desc += span_info(I18n.T(" Looks like a nearby station has sent in a MAXIMUM priority request for coal, in seemingly ridiculous quantities."));
        detailed_desc += span_info(I18n.T(" The reason listed for the request seems to be hastily filled in -- 'Seeking alternative methods to power the station.'"));
        detailed_desc += span_info(I18n.T(" A MAXIMUM priority request like this is nothing to balk at. You should probably stamp this."));
        return dot;
    }
}

// tg: code/modules/paperwork/paperwork.dm:165
public partial class obj_item_paperwork_medical : obj_item_paperwork
{
    // tg: code/modules/paperwork/paperwork.dm:170
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        detailed_desc += span_info(I18n.T(" The stack of documents appears to be a medical report from a nearby station, detailing the autopsy of an unknown xenofauna."));
        detailed_desc += span_info(I18n.T(" Skipping to the end of the report reveals that the specimen was the station bartender's pet monkey."));
        detailed_desc += span_info(I18n.T(" The specimen had been exposed to radiation during an 'unrelated incident with the engine', leading to its mutated form."));
        detailed_desc += span_info(I18n.T(" Regardless, the autopsy results look like they could be useful. You should probably stamp this."));
        return dot;
    }
}

// tg: code/modules/paperwork/paperwork.dm:179
public partial class obj_item_paperwork_engineering : obj_item_paperwork
{
    // tg: code/modules/paperwork/paperwork.dm:184
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        detailed_desc += span_info(I18n.T(" These papers are a power output report from a neighboring station. It details the power output and other engineering data regarding the station during a typical shift."));
        detailed_desc += span_info(I18n.T(" Checking the logs, you notice the energy output and engine temperature spike dramatically, and shortly after, the surrounding department appears to be depressurized by an unknown force."));
        detailed_desc += span_info(I18n.T(" Clearly the station's engineering department was testing an experimental engine setup, and had to use the air in the nearby rooms to help cool the engine. Totally."));
        detailed_desc += span_info(I18n.T(" Damn, that's impressive stuff. You should probably stamp this."));
        return dot;
    }
}

// tg: code/modules/paperwork/paperwork.dm:192
public partial class obj_item_paperwork_research : obj_item_paperwork
{
    // tg: code/modules/paperwork/paperwork.dm:197
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        detailed_desc += span_info(I18n.T(" The documents detail the results of a standard ordnance test that occured on a nearby station."));
        detailed_desc += span_info(I18n.T(" As you read further, you realize something strange with the results -- The epicenter doesn't seem to be correct."));
        detailed_desc += span_info(I18n.T(" If your math is correct, this explosion didn't happen at the station's ordnance site, it occured in the station's engine room."));
        detailed_desc += span_info(I18n.T(" Regardless, they're still perfectly usable test results. You should probably stamp this."));
        return dot;
    }
}

// tg: code/modules/paperwork/paperwork.dm:205
public partial class obj_item_paperwork_captain : obj_item_paperwork
{
    // tg: code/modules/paperwork/paperwork.dm:210
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        detailed_desc += span_info(I18n.T(" The documents are an unsigned correspondence from the captain's desk of a nearby station."));
        detailed_desc += span_info(I18n.T(" It seems to be a standard check-in message, reporting that the station is functioning at optimal efficiency."));
        detailed_desc += span_info(I18n.T(" The message repeatedly asserts that the engine is functioning 'perfectly fine' and is generating 'buttloads' of power."));
        detailed_desc += span_info(I18n.T(" Everything checks out. You should probably stamp this."));
        return dot;
    }
}

//Photocopied paperwork. These are created when paperwork, whether stamped or otherwise, is printed. If it is stamped, it can be sold to cargo at the risk of the paperwork not being accepted (which takes a small fee from cargo).
//If it is unstamped it will lose you money like normal, unless it has been marked with a VOID stamp
// tg: code/modules/paperwork/paperwork.dm:220
public partial class obj_item_paperwork_photocopy : obj_item_paperwork
{
    // tg: code/modules/paperwork/paperwork.dm:227
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        detailed_desc = span_notice(I18n.T("The print job on this paperwork has rendered it almost entirely unreadable."));
        return dot;
    }

    // tg: code/modules/paperwork/paperwork.dm:232
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);

        if (Dm.truthy(stamped))
        {
            if (Dm.truthy(voided))
                Dm.append(ref dot, span_notice(I18n.T("It looks like it's been marked as 'VOID' on the front. It's unlikely that anyone will accept these now.")));
            else
                Dm.append(ref dot, span_notice(I18n.T("The stamp on the front appears to be smudged and faded. Central Command will probably still accept these, right?")));
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("These appear to just be a photocopy of the original documents.")));
        return dot;
    }

    // tg: code/modules/paperwork/paperwork.dm:244
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_stamp_void)) || Dm.truthy(stamped) || Dm.truthy(voided))
            return await base.item_interaction(user, tool, modifiers, __extra);
        to_chat(user, span_notice(I18n.T("You plant the [] firmly onto the front of the documents.", tool)));
        stamp_overlay = mutable_appearance(Dm.resource("icons/obj/service/bureaucracy.dmi"), "paper_stamp-void") as mutable_appearance;
        add_overlay(stamp_overlay);
        voided = TRUE;
        stamped = TRUE; //It won't get you any money, but it also can't LOSE you money now.
        return ITEM_INTERACT_SUCCESS;
    }
}

//Ancient paperwork is a subtype of paperwork, meant to be used for any paperwork not spawned by the event.
//It doesn't have any of the flavor text that the event ones spawn with.
// tg: code/modules/paperwork/paperwork.dm:258
public partial class obj_item_paperwork_ancient : obj_item_paperwork
{
    // tg: code/modules/paperwork/paperwork.dm:262
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        detailed_desc = span_notice(I18n.T("It's impossible to really tell how old these are or what they're for, but Central Command might appreciate them anyway."));

        //Make the ancient paperwork function like one of the main types
        if (Initialize__paperwork_to_use == null)
        {
            Initialize__paperwork_to_use = subtypesof(typeof(obj_item_paperwork)) as DmList;
            Initialize__paperwork_to_use.Remove(Dm.list(typeof(obj_item_paperwork_ancient), typeof(obj_item_paperwork_photocopy))); //Get rid of the uncopiable paperwork types
        }

        object paperwork_type = pick(Initialize__paperwork_to_use);
        copy_stamp_info(paperwork_type);
        return dot;
    }

    // tg: code/modules/paperwork/paperwork.dm:267  Initialize's var/static/list/paperwork_to_use (rules 10.6)
    private static ref DmList Initialize__paperwork_to_use => ref TgState.Current.obj_item_paperwork_ancient__Initialize__paperwork_to_use;
}

public sealed partial class TgState
{
    // tg: code/modules/paperwork/paperwork.dm:267  /obj/item/paperwork/ancient/Initialize's var/static/list/paperwork_to_use (made on the first call)
    internal DmList obj_item_paperwork_ancient__Initialize__paperwork_to_use;
}
