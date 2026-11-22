// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/detectiveboard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298). Photos have no picture yet (section 107, the camera's image), so an evidence photo has no photo_url; a paper evidence is read in a browse() page (gaps S3).

// tg: code/game/objects/structures/detectiveboard.dm:10
public partial class obj_structure_detectiveboard : obj_structure
{
    private const int MAX_ICON_NOTICES = 8;
    private const int MAX_CASES = 8;
    private const int MAX_EVIDENCE_Y = 3500;
    private const int MAX_EVIDENCE_X = 1180;
    private const string EVIDENCE_TYPE_PHOTO = "photo";
    private const string EVIDENCE_TYPE_PAPER = "paper";

    // tg: code/game/objects/structures/detectiveboard.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(mapload))
        {
            foreach (obj_item item in Dm.each<obj_item>(loc as atom))
            {
                if (istype(item, typeof(obj_item_paper)) || istype(item, typeof(obj_item_photo)))
                {
                    Waiting.NoSleep(item.forceMove(this));
                    // tg's list is empty at map load, so cases[current_case] is a runtime in tg when a paper lies under a mapped board; guarded
                    if (length(cases) >= current_case)
                        (cases[current_case] as datum_case).notices++;
                }
            }
            Waiting.NoSleep(find_and_mount_on_atom());
        }

        // register_context: the screentips, deferred (the board declares no add_context of its own)
        return dot;
    }

    /// Attaching evidences: photo and papers
    // tg: code/game/objects/structures/detectiveboard.dm:43
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_paper)) && !istype(tool, typeof(obj_item_photo)))
            return NONE;
        if (length(cases) == 0)
        {
            to_chat(user, I18n.T("There are no cases!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(attaching_evidence))
        {
            to_chat(user, I18n.T("You already attaching evidence!"));
            return ITEM_INTERACT_BLOCKING;
        }

        attaching_evidence = TRUE;
        object new_name = await tgui_input_text(user, I18n.T("Please enter the evidence name"), I18n.T("Detective's Board"), max_length: MAX_NAME_LEN);
        if (!Dm.truthy(new_name))
            new_name = tool.name;
        object new_desc = await tgui_input_text(user, I18n.T("Please enter the evidence description"), I18n.T("Detective's Board"), max_length: MAX_DESC_LEN);
        if (!Dm.truthy(new_desc))
            new_desc = tool.desc;

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            attaching_evidence = FALSE;
            return ITEM_INTERACT_BLOCKING;
        }

        datum_case the_case = cases[current_case] as datum_case;
        the_case.notices++;
        datum_evidence evidence = Dm.@new<datum_evidence>(new_name, new_desc, tool);
        Dm.append(ref the_case.evidences, evidence);
        to_chat(user, span_notice(I18n.T("You pin the [] to the detective board.", tool)));
        attaching_evidence = FALSE;
        update_appearance(UPDATE_ICON);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/detectiveboard.dm:74
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act_secondary(user, tool);
        balloon_alert(user, Dm.text("[]securing...", Dm.truthy(anchored) ? "un" : ""));
        tool.play_tool_sound(this);
        if (Dm.truthy(await tool.use_tool(this, user, 6 * SECONDS)))
        {
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            balloon_alert(user, Dm.text("[]secured", Dm.truthy(anchored) ? "un" : ""));
            await deconstruct();
            return TRUE;
        }
        return dot;
    }

    // tg: code/game/objects/structures/detectiveboard.dm:84
    public override object ui_state(mob user = null)
    {
        return GLOB.physical_state;
    }

    // tg: code/game/objects/structures/detectiveboard.dm:87
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "DetectiveBoard", name, 1200, 800);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/structures/detectiveboard.dm:93
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList data_cases = Dm.list();
        foreach (datum_case a_case in Dm.each<datum_case>(cases))
        {
            DmList data_case = Dm.list(Dm.kv("ref", REF(a_case)), Dm.kv("name", a_case.name), Dm.kv("color", a_case.color_value));
            DmList data_evidences = Dm.list();
            foreach (datum_evidence evidence in Dm.each<datum_evidence>(a_case.evidences))
            {
                DmList data_evidence = Dm.list(Dm.kv("ref", REF(evidence)), Dm.kv("name", evidence.name), Dm.kv("type", evidence.evidence_type), Dm.kv("description", evidence.description), Dm.kv("x", evidence.x), Dm.kv("y", evidence.y));
                DmList data_connections = Dm.list();
                foreach (datum_evidence connection in Dm.each<datum_evidence>(evidence.connections))
                    data_connections.Add(REF(connection)); // TODO: create array of strings
                data_evidence["connections"] = data_connections;
                switch (evidence.evidence_type)
                {
                    case EVIDENCE_TYPE_PHOTO:
                        // user << browse_rsc(photo.picture.picture_image, tmp_picture_name): photos have no picture yet (section 107, the camera's image)
                        Dm.Deferred("code/game/objects/structures/detectiveboard.dm:107", "ui_data: the evidence photo's picture (the camera's picture has no image, section 107)");
                        break;
                    case EVIDENCE_TYPE_PAPER:
                    {
                        obj_item_paper paper = evidence.item_value as obj_item_paper;
                        data_evidence["text"] = "";
                        if (length(paper.raw_text_inputs) > 0)
                            data_evidence["text"] = (paper.raw_text_inputs[1] as datum_paper_input)?.raw_text;
                        break;
                    }
                }
                data_evidences.Add(Dm.list(data_evidence));
            }
            data_case["evidences"] = data_evidences;
            DmList connections = Dm.list();
            foreach (datum_evidence evidence in Dm.each<datum_evidence>(a_case.evidences))
            {
                foreach (datum_evidence connection in Dm.each<datum_evidence>(evidence.connections))
                {
                    DmList from_pos = get_pin_position(evidence) as DmList;
                    DmList to_pos = get_pin_position(connection) as DmList;
                    bool found_in_connections = false;
                    foreach (DmList con in Dm.each<DmList>(connections))
                    {
                        DmList con_from = con["from"] as DmList;
                        DmList con_to = con["to"] as DmList;
                        if (Dm.eq(con_from["x"], to_pos["x"]) && Dm.eq(con_from["y"], to_pos["y"]) && Dm.eq(con_to["x"], from_pos["x"]) && Dm.eq(con_to["y"], from_pos["y"]))
                            found_in_connections = true;
                    }
                    if (!found_in_connections)
                    {
                        DmList data_connection = Dm.list(Dm.kv("color", "red"), Dm.kv("from", from_pos), Dm.kv("to", to_pos));
                        connections.Add(Dm.list(data_connection));
                    }
                }
            }
            data_case["connections"] = connections;
            data_cases.Add(Dm.list(data_case));
        }

        data["cases"] = data_cases;
        data["current_case"] = current_case;
        return data;
    }

    // tg: code/game/objects/structures/detectiveboard.dm:137
    public virtual object get_pin_position(datum_evidence evidence = null)
    {
        return Dm.list(Dm.kv("x", evidence.x + 15), Dm.kv("y", evidence.y + 15));
    }

    // locate(params["..."]) in the list
    private datum_case CaseByRef(object case_ref)
    {
        foreach (datum_case c in Dm.each<datum_case>(cases))
        {
            if (Dm.eq(REF(c), case_ref))
                return c;
        }
        return null;
    }

    private static datum_evidence EvidenceByRef(datum_case a_case, object evidence_ref)
    {
        foreach (datum_evidence e in Dm.each<datum_evidence>(a_case?.evidences))
        {
            if (Dm.eq(REF(e), evidence_ref))
                return e;
        }
        return null;
    }

    // tg: code/game/objects/structures/detectiveboard.dm:140
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        mob user = ui.user;
        switch (action)
        {
            case "add_case":
            {
                if (length(cases) == MAX_CASES)
                    return FALSE;
                object new_case = await tgui_input_text(user, I18n.T("Please enter the case name"), I18n.T("Detective's Board"), max_length: MAX_NAME_LEN);
                if (!Dm.truthy(new_case))
                    return FALSE;
                object case_color = await tgui_input_list(user, I18n.T("Please choose case color"), I18n.T("Detective's Board"), case_colors);
                if (!Dm.truthy(case_color))
                    return FALSE;

                datum_case a_case = Dm.@new<datum_case>(new_case, case_color);
                Dm.append(ref cases, a_case);
                current_case = (int)clamp(length(cases), 1, MAX_CASES);
                update_appearance(UPDATE_ICON);
                return TRUE;
            }
            case "set_case":
                if (Dm.truthy(cases) && Dm.truthy(@params["case"]) && Dm.num(@params["case"]) <= length(cases))
                {
                    current_case = (int)clamp(Dm.num(@params["case"]), 1, MAX_CASES);
                    update_appearance(UPDATE_ICON);
                    return TRUE;
                }
                break;
            case "remove_case":
            {
                datum_case a_case = CaseByRef(@params["case_ref"]);
                if (a_case != null)
                {
                    foreach (datum_evidence evidence in Dm.each<datum_evidence>(a_case.evidences))
                        await remove_item(evidence.item_value as obj_item, user);
                    cases.Remove(a_case);
                    current_case = (int)clamp(length(cases), 1, MAX_CASES);
                    update_appearance(UPDATE_ICON);
                    return TRUE;
                }
                break;
            }
            case "rename_case":
            {
                object new_name = await tgui_input_text(user, I18n.T("Please enter the new name for the case"), I18n.T("Detective's Board"), max_length: MAX_NAME_LEN);
                if (Dm.truthy(new_name))
                {
                    datum_case a_case = CaseByRef(@params["case_ref"]);
                    a_case.name = Dm.str(new_name);
                    return TRUE;
                }
                break;
            }
            case "look_evidence":
            {
                datum_case a_case = CaseByRef(@params["case_ref"]);
                datum_evidence evidence = EvidenceByRef(a_case, @params["evidence_ref"]);
                if (evidence.evidence_type == EVIDENCE_TYPE_PHOTO)
                {
                    obj_item_photo photo = evidence.item_value as obj_item_photo;
                    photo.show(user);
                    return TRUE;
                }

                obj_item_paper paper = evidence.item_value as obj_item_paper;
                string paper_text = "";
                foreach (datum_paper_input text_input in Dm.each<datum_paper_input>(paper.raw_text_inputs))
                    paper_text += text_input.raw_text;
                ClientSeam.Browse(user, Dm.text("<html><head><meta http-equiv='Content-Type' content='text/html; charset=UTF-8'><title>[]</title></head><body style='overflow:hidden;padding:5px'>[]</body></html>", paper.name, paper_text), "window=photo_showing;size=480x608"); // user << browse(...) (gaps S3)
                // onclose(user, "[name]"): the window's close callback (browse()'s onclose, not ported: the Browser window closes by itself)
                break;
            }
            case "remove_evidence":
            {
                datum_case a_case = cases[current_case] as datum_case;
                datum_evidence evidence = EvidenceByRef(a_case, @params["evidence_ref"]);
                if (evidence != null)
                {
                    obj_item item = evidence.item_value as obj_item;
                    if (item == null || item.loc != this)
                        return null;
                    await remove_item(item, user);
                    foreach (datum_evidence connection in Dm.each<datum_evidence>(evidence.connections))
                        connection.connections.Remove(evidence);
                    a_case.evidences.Remove(evidence);
                    update_appearance(UPDATE_ICON);
                    return TRUE;
                }
                break;
            }
            case "set_evidence_cords":
            {
                datum_case a_case = CaseByRef(@params["case_ref"]);
                if (a_case != null)
                {
                    datum_evidence evidence = EvidenceByRef(a_case, @params["evidence_ref"]);
                    if (evidence != null)
                    {
                        evidence.x = clamp(Dm.num(@params["rel_x"]), 0, MAX_EVIDENCE_X);
                        evidence.y = clamp(Dm.num(@params["rel_y"]), 0, MAX_EVIDENCE_Y);
                    }
                }
                return TRUE;
            }
            case "add_connection":
            {
                datum_case a_case = cases[current_case] as datum_case;
                datum_evidence from_evidence = EvidenceByRef(a_case, @params["from_ref"]);
                datum_evidence to_evidence = EvidenceByRef(a_case, @params["to_ref"]);
                if (from_evidence != null && to_evidence != null)
                {
                    Dm.append(ref from_evidence.connections, to_evidence);
                    Dm.append(ref to_evidence.connections, from_evidence);
                }
                return TRUE;
            }
        }

        return FALSE;
    }

    // tg: code/game/objects/structures/detectiveboard.dm:230
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (length(cases) == 0)
            return dot;
        float notices = (cases[current_case] as datum_case).notices;
        if (notices < MAX_ICON_NOTICES)
            dot.Add(Dm.text("notices_[]", notices));
        else
            dot.Add(Dm.text("notices_[]", MAX_ICON_NOTICES));
        return dot;
    }

    /**
     * Removes an item from the notice board
     *
     * Arguments:
     * * item - The item that is to be removed
     * * user - The mob that is trying to get the item removed, if there is one
     */
    // tg: code/game/objects/structures/detectiveboard.dm:245
    public virtual async DmTask remove_item(obj_item item = null, mob user = null)
    {
        await item.forceMove(drop_location() as atom);
        if (user != null)
        {
            await user.put_in_hands(item);
            balloon_alert(user, I18n.T("removed from board"));
        }
        // cases[current_case] when every case is gone is a runtime in tg (the deconstruction of a board with no cases); guarded
        if (length(cases) >= current_case)
            (cases[current_case] as datum_case).notices--;
        update_appearance(UPDATE_ICON);
    }

    // tg: code/game/objects/structures/detectiveboard.dm:253
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        if (!Dm.truthy(disassembled ?? TRUE))
            Dm.@new<obj_item_stack_sheet_mineral_wood>(loc);
        else
            Dm.@new<obj_item_wallframe_detectiveboard>(loc);
        foreach (obj_item content in Dm.each<obj_item>(contents.Copy()))
            await remove_item(content);
    }
}

// tg: code/game/objects/structures/detectiveboard.dm:274
public partial class datum_evidence : datum
{
    // GAP(types): tg's `var/obj/item/item` (:281) reads as a typepath in the generated var; the item lives here.
    public obj_item item_value;

    // tg: code/game/objects/structures/detectiveboard.dm:282
    public override async DmTask New(params object[] __extra)
    {
        await base.New();
        name = Dm.str(__extra[0]);
        description = Dm.str(__extra[1]);
        item_value = __extra[2] as obj_item;
        evidence_type = istype(item_value, typeof(obj_item_photo)) ? "photo" : "paper";
    }
}

// tg: code/game/objects/structures/detectiveboard.dm:292
public partial class datum_case : datum
{
    // GAP(types): tg's `var/color = 0` (:295) is generated a number but holds the colour's name.
    public object color_value;

    // tg: code/game/objects/structures/detectiveboard.dm:297
    public override async DmTask New(params object[] __extra)
    {
        await base.New();
        name = Dm.str(__extra[0]);
        color_value = __extra[1];
    }
}
