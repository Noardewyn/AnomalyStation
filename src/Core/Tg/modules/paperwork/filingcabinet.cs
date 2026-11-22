// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/filingcabinet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the telekinetic pulls (attack_tk, attack_self_tk: live-partial rows, telekinesis is M10+), gaps S3 (D298). The var
// blocks are generated.

/* Filing cabinets!
 * Contains:
 * Filing Cabinets
 * Security Record Cabinets
 * Medical Record Cabinets
 * Employment Contract Cabinets
 */

/*
 * Filing Cabinets
 */
// tg: code/modules/paperwork/filingcabinet.dm:13
public partial class obj_structure_filingcabinet : obj_structure
{
    // tg: code/modules/paperwork/filingcabinet.dm:35
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
        {
            foreach (obj_item I in Dm.each<obj_item>(loc))
            {
                if (I.w_class < WEIGHT_CLASS_NORMAL) //there probably shouldn't be anything placed ontop of filing cabinets in a map that isn't meant to go in them
                    Waiting.NoSleep(I.forceMove(this));
            }
        }
        return dot;
    }

    // tg: code/modules/paperwork/filingcabinet.dm:42
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_iron>(loc, 2);
        foreach (obj_item obj in Dm.each<obj_item>(this))
            await obj.forceMove(loc as atom);
    }

    // tg: code/modules/paperwork/filingcabinet.dm:47
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool.w_class < WEIGHT_CLASS_NORMAL)
        {
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            to_chat(user, span_notice(I18n.T("You put [] in [].", tool, this)));
            icon_state = Dm.text("[]-open", Dm.initial(this, nameof(icon_state)));
            await sleep(0.5f * SECONDS);
            icon_state = (string)Dm.initial(this, nameof(icon_state));
            return ITEM_INTERACT_SUCCESS;
        }
        if (!Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(tool.item_flags, NOBLUDGEON)))
        {
            to_chat(user, span_warning(I18n.T("You can't put [] in []!", tool, this)));
            return ITEM_INTERACT_BLOCKING;
        }
        return NONE;
    }

    // tg: code/modules/paperwork/filingcabinet.dm:61
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        to_chat(user, span_notice(I18n.T("You begin to [] [].", Dm.truthy(anchored) ? I18n.T("unwrench") : I18n.T("wrench"), this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 20, volume: 50)))
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("You successfully [] [].", Dm.truthy(anchored) ? I18n.T("unwrench") : I18n.T("wrench"), this)));
        await set_anchored(Dm.truthy(anchored) ? FALSE : TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/paperwork/filingcabinet.dm:69
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        await ui_interact(user);
        return dot;
    }

    // tg: code/modules/paperwork/filingcabinet.dm:73
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "FilingCabinet");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/paperwork/filingcabinet.dm:79
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["cabinet_name"] = Dm.text("[]", name);
        data["contents"] = Dm.list();
        data["contents_ref"] = Dm.list();
        foreach (obj_item content in Dm.each<obj_item>(this))
        {
            Dm.append((DmList)data["contents"], Dm.text("[]", content));
            Dm.append((DmList)data["contents_ref"], Dm.text("[]", REF(content)));
        }

        return data;
    }

    // tg: code/modules/paperwork/filingcabinet.dm:91
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            // Take the object out
            case "remove_object":
            {
                obj_item content = Dm.locate_ref(@params["ref"]) is obj_item found && found.loc == this ? found : null; // locate(params["ref"]) in src
                if (content != null && in_range(this, usr))
                {
                    await usr.put_in_hands(content);
                    icon_state = Dm.text("[]-open", Dm.initial(this, nameof(icon_state)));
                    // VARSET_CALLBACK(src, icon_state, initial(icon_state)): icon_state__varset below (the GAP(lead) of doorcontrol.cs)
                    _addtimer(Dm.@new<datum_callback>(this, nameof(icon_state__varset), Dm.initial(this, nameof(icon_state))), 0.5f * SECONDS, file: "code/modules/paperwork/filingcabinet.dm", line: 101);
                    return TRUE;
                }
                break;
            }
        }
        return dot;
    }

    /// VARSET_CALLBACK(src, icon_state, value)'s target: ___callbackvarset's datum branch (varset_callback.dm:8-23) for this var.
    public virtual void icon_state__varset(object var_value = null)
    {
        icon_state = var_value as string;
    }
}

/*
 * Security Record Cabinets
 */
// tg: code/modules/paperwork/filingcabinet.dm:125
public partial class obj_structure_filingcabinet_security : obj_structure_filingcabinet
{
    // tg: code/modules/paperwork/filingcabinet.dm:128
    public virtual async DmTask populate()
    {
        if (!Dm.truthy(virgin))
            return;
        foreach (datum_record_crew target in Dm.each<datum_record_crew>(GLOB.manifest.general))
        {
            obj_item_paper rapsheet = target.get_rapsheet() as obj_item_paper;
            await rapsheet.forceMove(this);
            virgin = FALSE; //tabbing here is correct- it's possible for people to try and use it
                            //before the records have been generated, so we do this inside the loop.
        }
    }

    // tg: code/modules/paperwork/filingcabinet.dm:137
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        await populate();
        return await base.attack_hand(user, modifiers, __extra);
    }
}

/*
 * Medical Record Cabinets
 */
// tg: code/modules/paperwork/filingcabinet.dm:148
public partial class obj_structure_filingcabinet_medical : obj_structure_filingcabinet
{
    // tg: code/modules/paperwork/filingcabinet.dm:152
    public virtual void populate()
    {
        if (!Dm.truthy(virgin))
            return;
        foreach (datum_record_crew record in Dm.each<datum_record_crew>(GLOB.manifest.general))
        {
            obj_item_paper med_record_paper = Dm.@new<obj_item_paper>(this);
            string med_record_text = "<CENTER><B>Medical Record</B></CENTER><BR>";
            med_record_text += Dm.text("Name: [] Rank: []<BR>\nGender: []<BR>\nAge: []<BR>", record.name, record.rank, record.gender, record.age);
            DmList note_texts = Dm.list();
            foreach (datum_medical_note note in Dm.each<datum_medical_note>(record.medical_notes))
                Dm.append(note_texts, note.content);
            med_record_text += Dm.text("<BR>\n<CENTER><B>Medical Data</B></CENTER><BR>\nBlood Type: []<BR>\nDNA: []<BR>\n<BR>\nPhysical Status: []<BR>\nMental Status: []<BR>\nMinor Disabilities: []<BR>\nDetails: []<BR>\n<BR>\nMajor Disabilities: []<BR>\nDetails: []<BR>\n<BR>\nImportant Notes:<BR>\n\t[]<BR>\n<BR>\n<CENTER><B>Comments/Log</B></CENTER><BR>",
                record.blood_type, record.dna_string, record.physical_status, record.mental_status, record.minor_disabilities, record.minor_disabilities_desc, record.major_disabilities, record.major_disabilities_desc, jointext(note_texts, "<BR>\n\t"));
            med_record_text += "</TT>";
            med_record_paper.add_raw_text(med_record_text);
            med_record_paper.name = I18n.T("paper - '[]'", record.name);
            med_record_paper.update_appearance();
            virgin = FALSE; //tabbing here is correct- it's possible for people to try and use it
                            //before the records have been generated, so we do this inside the loop.
        }
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/paperwork/filingcabinet.dm:171
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        populate();
        return await base.attack_hand(user, modifiers, __extra);
    }
}

/*
 * Employment contract Cabinets
 */

// tg: code/modules/paperwork/filingcabinet.dm:183  GLOBAL_LIST_EMPTY(employmentCabinets)
public partial class datum_controller_global_vars
{
    // tg: code/modules/paperwork/filingcabinet.dm:183
    public DmList employmentCabinets;

    // tg: code/modules/paperwork/filingcabinet.dm:183  (GLOBAL_LIST_EMPTY(employmentCabinets), __DEFINES/__globals.dm)
    public virtual void InitGlobalemploymentCabinets()
    {
        employmentCabinets = Dm.list();
        Dm.append(ref gvars_datum_init_order, "employmentCabinets");
    }
}

// tg: code/modules/paperwork/filingcabinet.dm:185
public partial class obj_structure_filingcabinet_employment : obj_structure_filingcabinet
{
    // tg: code/modules/paperwork/filingcabinet.dm:190
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(GLOB.employmentCabinets, this);
        return dot;
    }

    // tg: code/modules/paperwork/filingcabinet.dm:194
    public override object Destroy(float? force = null)
    {
        GLOB.employmentCabinets?.Remove(this);
        return base.Destroy(force);
    }

    // tg: code/modules/paperwork/filingcabinet.dm:198
    public virtual void fillCurrent()
    {
        //This proc fills the cabinet with the current crew.
        foreach (datum_record_locked target in Dm.each<datum_record_locked>(GLOB.manifest.locked))
        {
            datum_mind filed_mind = (target.mind_ref as datum_weakref)?.resolve() as datum_mind;
            if (filed_mind != null && ishuman(filed_mind.current))
                addFile(filed_mind.current as mob_living_carbon_human);
        }
    }

    // tg: code/modules/paperwork/filingcabinet.dm:205
    public virtual void addFile(mob_living_carbon_human employee = null)
    {
        Dm.@new<obj_item_paper_employment_contract>(this, employee.mind.name);
    }

    // tg: code/modules/paperwork/filingcabinet.dm:208
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(virgin))
        {
            fillCurrent();
            virgin = FALSE;
        }
        return await base.interact(user, __extra);
    }
}
