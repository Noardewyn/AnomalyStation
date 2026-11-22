// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/diggable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps G2 (D291): the whole file (the grass, fake snow and fake basalt floors dig up with a shovel).

/// Lets you make hitting a turf with a shovel pop something out, and scrape the turf
// tg: code/datums/elements/diggable.dm:2
public partial class datum_element_diggable : datum_element
{
    // tg: code/datums/elements/diggable.dm:16
    [DmParams("target", "to_spawn", "amount", "worm_chance", "action_text", "action_text_third_person")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object to_spawn = Dm.arg(__extra, 0);
        object amount = Dm.arg(__extra, 1) ?? 1;
        object worm_chance = Dm.arg(__extra, 2) ?? 30;
        object action_text = Dm.arg(__extra, 3) ?? I18n.T("dig up");
        object action_text_third_person = Dm.arg(__extra, 4) ?? I18n.T("digs up");
        object dot = await base.Attach(target, Dm.forward(__extra, to_spawn, amount, worm_chance, action_text, action_text_third_person));
        if (!isturf(target))
            return ELEMENT_INCOMPATIBLE;
        if (!Dm.truthy(to_spawn))
        {
            stack_trace(Dm.text("[] wasn't passed a typepath to spawn attaching to [].", type, target), "code/datums/elements/diggable.dm", 21);
            return ELEMENT_INCOMPATIBLE;
        }

        this.to_spawn = to_spawn as System.Type;
        this.amount = amount;
        this.worm_chance = worm_chance;
        this.action_text = action_text;
        this.action_text_third_person = action_text_third_person;

        RegisterSignal(target, COMSIG_ATOM_TOOL_ACT(TOOL_SHOVEL), nameof(on_shovel));
        return dot;
    }

    // tg: code/datums/elements/diggable.dm:33
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_ATOM_TOOL_ACT(TOOL_SHOVEL));
        return dot;
    }

    /// Signal proc for [COMSIG_ATOM_TOOL_ACT] via [TOOL_SHOVEL].
    // tg: code/datums/elements/diggable.dm:38  (SIGNAL_HANDLER is dropped, rules 7.9; ScrapeAway does not sleep here)
    [SignalHandler]
    public virtual void on_shovel(turf source = null, mob user = null, obj_item tool = null)
    {
        for (int i = 1; i <= Dm.num(amount); i++)
            Dm.@new(to_spawn, source);
        if (prob(Dm.num(worm_chance)))
            Dm.@new<obj_item_food_bait_worm>(source);

        user.visible_message(
            span_notice(Dm.text("[] [] [].", user, action_text_third_person, source)),
            span_notice(Dm.text("You [] [].", action_text, source)));

        playsound(source, Dm.resource("sound/effects/shovel_dig.ogg"), 50, TRUE);
        Waiting.NoSleep(source.ScrapeAway(flags: CHANGETURF_INHERIT_AIR));
    }
}
