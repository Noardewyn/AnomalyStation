// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/etherealdiscoball.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole. The portable ethereal disco ball deploys the disco ball where its user stands; a hand switches
// it on ("ethdisco_head_1") and off ("ethdisco_head_0"), and while on it changes to a random colour and light every half second
// (its base, "ethdisco_base", keeps its own colour); Alt-click locks and unlocks it. As in tg, turning it off removes only the
// temporary colour priority, so the ball keeps the last colour it took. The var block is generated (D111).

// tg: code/game/objects/items/etherealdiscoball.dm:1
public partial class obj_item_etherealballdeployer : obj_item
{
    // tg: code/game/objects/items/etherealdiscoball.dm:7
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        to_chat(user, span_notice(I18n.T("You deploy the Ethereal Disco Ball.")));
        Dm.@new<obj_structure_etherealball>(user.loc);
        qdel(this);
        return dot;
    }
}

// tg: code/game/objects/items/etherealdiscoball.dm:13
public partial class obj_structure_etherealball : obj_structure
{
    // tg: code/game/objects/items/etherealdiscoball.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        if (Dm.truthy(TurnedOn))
            Waiting.NoSleep(TurnOn());
        return dot;
    }

    // tg: code/game/objects/items/etherealdiscoball.dm:32
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(await can_interact(user)))
            return dot;

        if (Dm.truthy(TurnedOn))
        {
            await TurnOff();
            to_chat(user, span_notice(I18n.T("You turn the disco ball off!")));
        }
        else
        {
            await TurnOn();
            to_chat(user, span_notice(I18n.T("You turn the disco ball on!")));
        }
        return dot;
    }

    // tg: code/game/objects/items/etherealdiscoball.dm:44
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await set_anchored(Dm.not(anchored));
        to_chat(user, span_notice(I18n.T("You []lock the disco ball.", Dm.truthy(anchored) ? null : I18n.T("un"))));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/etherealdiscoball.dm:49
    public virtual async DmTask TurnOn()
    {
        TurnedOn = TRUE; //Same
        await DiscoFever();
    }

    // tg: code/game/objects/items/etherealdiscoball.dm:53
    public virtual async DmTask TurnOff()
    {
        TurnedOn = FALSE;
        await set_light(0);
        await remove_atom_colour(TEMPORARY_COLOUR_PRIORITY);
        update_appearance();
        if (Dm.truthy(TimerID))
            deltimer(TimerID);
    }

    // tg: code/game/objects/items/etherealdiscoball.dm:61
    public virtual async DmTask DiscoFever()
    {
        await remove_atom_colour(TEMPORARY_COLOUR_PRIORITY);
        current_color = random_color();
        await set_light(range, power, Dm.text("#[]", current_color));
        await add_atom_colour(Dm.text("#[]", current_color), FIXED_COLOUR_PRIORITY);
        update_appearance();
        TimerID = _addtimer(Dm.@new<datum_callback>(this, nameof(DiscoFever)), 5, TIMER_STOPPABLE, file: "code/game/objects/items/etherealdiscoball.dm", line: 67); //Call ourselves every 0.5 seconds to change colors
    }

    // tg: code/game/objects/items/etherealdiscoball.dm:69
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("ethdisco_head_[]", TurnedOn);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/etherealdiscoball.dm:73
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, mutable_appearance(icon, "ethdisco_base", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)));
        return dot;
    }
}
