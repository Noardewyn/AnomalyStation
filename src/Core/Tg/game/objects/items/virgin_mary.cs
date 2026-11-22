// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/virgin_mary.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole. Burning the picture of the virgin mary with a lighter (burn_on_item_ignition) inducts its burner
// into the mafia: a nickname from tgui's text prompt goes between their first and last names (tg's double space before the last
// name kept), they swear the oath aloud and are told it grants no antagonist status; one picture, one nickname. Its suicide is a
// race of Hail Marys ending in suffocation after seven and a half seconds. The var block is generated (D111).

// tg: code/game/objects/items/virgin_mary.dm:1
public partial class obj_item_virgin_mary : obj_item
{
    private const int NICKNAME_CAP = (MAX_NAME_LEN / 2); // tg: virgin_mary.dm:15 (file-local, #undef at :49)

    // tg: code/game/objects/items/virgin_mary.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_burn_on_item_ignition), Dm.named("bypass_clumsy", TRUE)));
        RegisterSignal(this, COMSIG_ATOM_IGNITED_BY_ITEM, nameof(induct_new_initiate));
        return dot;
    }

    // tg: code/game/objects/items/virgin_mary.dm:17
    [SignalHandler]
    public virtual void induct_new_initiate(datum source = null, mob_living user = null, obj_item burning_tool = null)
    {
        Waiting.Detach(induct_new_initiate_async(user, burning_tool)); // INVOKE_ASYNC(src, PROC_REF(induct_new_initiate_async), user, burning_tool)
    }

    // tg: code/game/objects/items/virgin_mary.dm:22
    public virtual async DmTask induct_new_initiate_async(mob_living user = null, obj_item burning_tool = null)
    {
        if (user.mind == null)
            return;
        if (HAS_TRAIT(user, TRAIT_MAFIAINITIATE)) //Only one nickname fuckhead
        {
            to_chat(user, span_warning(I18n.T("You have already been initiated into the mafioso life.")));
            return;
        }
        if (Dm.truthy(used_up))
            return;

        ADD_TRAIT(user, TRAIT_MAFIAINITIATE, TRAIT_GENERIC); // Adding the trait early because you could burn multiple at once for a very long name
        to_chat(user, span_notice(I18n.T("As you burn the picture, a nickname comes to mind...")));
        object nickname = await tgui_input_text(user, I18n.T("Pick a nickname"), I18n.T("Mafioso Nicknames"), max_length: NICKNAME_CAP);
        nickname = reject_bad_name(nickname, allow_numbers: FALSE, max_length: NICKNAME_CAP, ascii_only: TRUE);
        if (!Dm.truthy(nickname))
        {
            REMOVE_TRAIT(user, TRAIT_MAFIAINITIATE, TRAIT_GENERIC);
            return;
        }
        object new_name = null;
        float space_position = findtext(user.real_name, " ");
        if (Dm.truthy(space_position)) //Can we find a space?
            new_name = I18n.T("[] \"[]\" []", copytext(user.real_name, 1, space_position), nickname, copytext(user.real_name, space_position));
        else //Append otherwise
            new_name = I18n.T("[] \"[]\"", user.real_name, nickname);
        user.real_name = new_name;
        used_up = TRUE;
        await user.say(I18n.T("My soul will burn like this saint if I betray my family. I enter alive and I will have to get out dead."), forced: typeof(obj_item_virgin_mary));
        to_chat(user, span_userdanger(I18n.T("Being inducted into the mafia does not grant antagonist status.")));
    }

    // tg: code/game/objects/items/virgin_mary.dm:51
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] starts saying their Hail Mary's at a terrifying pace! It looks like [] trying to enter the afterlife!", user, user.p_theyre())));
        await user.say(I18n.T("Hail Mary, full of grace, the Lord is with thee. Blessed are thou amongst women, and blessed is the fruit of thy womb, Jesus. Holy Mary, mother of God, pray for us sinners, now and at the hour of our death. Amen. "), forced: typeof(obj_item_virgin_mary));
        _addtimer(Dm.@new<datum_callback>(this, nameof(manual_suicide), user), 7.5f * SECONDS, file: "code/game/objects/items/virgin_mary.dm", line: 54);
        _addtimer(Dm.@new<datum_callback>(user, nameof(atom_movable.say), I18n.T("O my Mother, preserve me this day from mortal sin...")), 5 * SECONDS, file: "code/game/objects/items/virgin_mary.dm", line: 55);
        return MANUAL_SUICIDE;
    }

    // tg: code/game/objects/items/virgin_mary.dm:58
    public virtual async DmTask manual_suicide(mob_living user = null)
    {
        await user.adjust_oxy_loss(200);
        await user.death(FALSE);
    }
}
