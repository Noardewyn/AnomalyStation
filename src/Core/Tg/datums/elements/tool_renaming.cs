// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/tool_renaming.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298: renaming things with a pen). The var block is generated.

/**
 * Renaming tool element
 *
 * When using this tool on an object with UNIQUE_RENAME,
 * lets the user rename/redesc it.
 */
// tg: code/datums/elements/tool_renaming.dm:11
public partial class datum_element_tool_renaming : datum_element
{
    // tg: code/datums/elements/tool_renaming.dm:1  #define OPTION_RENAME "Rename" (file-local)
    private const string OPTION_RENAME = "Rename";
    // tg: code/datums/elements/tool_renaming.dm:2  #define OPTION_DESCRIPTION "Description"
    private const string OPTION_DESCRIPTION = "Description";
    // tg: code/datums/elements/tool_renaming.dm:3  #define OPTION_RESET "Reset"
    private const string OPTION_RESET = "Reset";

    // tg: code/datums/elements/tool_renaming.dm:13
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_ITEM_INTERACTING_WITH_ATOM, nameof(attempt_rename));
        return dot;
    }

    // tg: code/datums/elements/tool_renaming.dm:20
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_ITEM_INTERACTING_WITH_ATOM);
        return dot;
    }

    // tg: code/datums/elements/tool_renaming.dm:24
    [SignalHandler]
    public virtual object attempt_rename(datum source = null, mob_living user = null, atom interacting_with = null, DmList modifiers = null)
    {
        if (!isobj(interacting_with))
            return NONE;

        obj renamed_obj = (obj)interacting_with;
        obj_item tool = source as obj_item;

        if (!Dm.truthy(Dm.bitand(renamed_obj.obj_flags, UNIQUE_RENAME)) || !Dm.truthy(Waiting.NoSleep(user.can_write(tool))))
            return NONE;
        Waiting.Detach(async_rename(user, renamed_obj, Dm.num(!Dm.truthy(Dm.bitand(renamed_obj.obj_flags, RENAME_NO_DESC)))));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/datums/elements/tool_renaming.dm:38
    public virtual async DmTask async_rename(mob_living user = null, obj renamed_obj = null, object description_option = null)
    {
        if (!Dm.truthy(renamed_obj.rename_checks(user)))
            return;
        object custom_choice = await tgui_input_list(user, I18n.T("What would you like to edit?"), I18n.T("Customization"), Dm.list(OPTION_RENAME, Dm.truthy(description_option) ? OPTION_DESCRIPTION : null, OPTION_RESET));
        if (QDELETED(renamed_obj) || !Dm.truthy(await user.can_perform_action(renamed_obj)) || custom_choice == null)
            return;

        switch (custom_choice)
        {
            case OPTION_RENAME:
            {
                string old_name = renamed_obj.name;
                object input = await tgui_input_text(user, I18n.T("What do you want to name []?", renamed_obj), I18n.T("Object Name"), Dm.text("[]", old_name), MAX_NAME_LEN);
                if (QDELETED(renamed_obj) || !Dm.truthy(await user.can_perform_action(renamed_obj)))
                    return;
                if (Dm.eq(input, old_name) || !Dm.truthy(input))
                {
                    to_chat(user, span_notice(I18n.T("You changed [] to... well... [].", renamed_obj, renamed_obj)));
                    return;
                }
                await renamed_obj.AddComponent(typeof(datum_component_rename), await renamed_obj.nameformat(input, user), renamed_obj.desc);
                to_chat(user, span_notice(I18n.T("You have successfully renamed \\the [] to [].", old_name, renamed_obj)));
                renamed_obj.update_appearance(UPDATE_NAME);
                break;
            }
            case OPTION_DESCRIPTION:
            {
                string old_desc = renamed_obj.desc;
                object input = await tgui_input_text(user, I18n.T("Describe []", renamed_obj), I18n.T("Description"), Dm.text("[]", old_desc), MAX_DESC_LEN);
                if (QDELETED(renamed_obj) || !Dm.truthy(await user.can_perform_action(renamed_obj)))
                    return;
                if (Dm.eq(input, old_desc) || !Dm.truthy(input))
                {
                    to_chat(user, span_notice(I18n.T("You decide against changing []'s description.", renamed_obj)));
                    return;
                }
                await renamed_obj.AddComponent(typeof(datum_component_rename), renamed_obj.name, await renamed_obj.descformat(input, user));
                to_chat(user, span_notice(I18n.T("You have successfully changed []'s description.", renamed_obj)));
                renamed_obj.update_appearance(UPDATE_DESC);
                break;
            }
            case OPTION_RESET:
                qdel(renamed_obj.GetComponent(typeof(datum_component_rename)));
                to_chat(user, span_notice(I18n.T("You have successfully reset []'s name[]", renamed_obj, Dm.truthy(Dm.bitand(renamed_obj.obj_flags, RENAME_NO_DESC)) ? "." : I18n.T(" and description."))));
                await renamed_obj.rename_reset();
                renamed_obj.update_appearance(Dm.bitor(UPDATE_NAME, UPDATE_DESC));
                break;
        }
    }
}
