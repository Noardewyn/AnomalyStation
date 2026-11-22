// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/unfriend_attacker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/**
 * # Unfriend Attacker
 *
 * Element which makes a mob remove you from its friends list you if you hurt it.
 * Doesn't make a callout post because we don't have twitter integration.
 */
// tg: code/datums/elements/unfriend_attacker.dm:11
public partial class datum_element_unfriend_attacker : datum_element
{
    // tg: code/datums/elements/unfriend_attacker.dm:1
    private const string UNFRIEND_REPLACE_KEY_SOURCE = "%SOURCE%";
    // tg: code/datums/elements/unfriend_attacker.dm:2
    private const string UNFRIEND_REPLACE_KEY_TARGET = "%TARGET%";

    // tg: code/datums/elements/unfriend_attacker.dm:16
    [DmParams("target", "untamed_reaction")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object untamed_reaction = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;

        this.untamed_reaction = untamed_reaction;
        await target.AddElement(typeof(datum_element_ai_retaliate));
        RegisterSignal(target, COMSIG_ATOM_WAS_ATTACKED, nameof(on_hurt));
        return dot;
    }

    // tg: code/datums/elements/unfriend_attacker.dm:25
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, COMSIG_ATOM_WAS_ATTACKED);
        return dot;
    }

    /// If it's a bad touch make enemies
    // tg: code/datums/elements/unfriend_attacker.dm:30
    [SignalHandler]
    public virtual void on_hurt(mob_living owner = null, atom attacker = null)
    {
        if (IS_UNCONSCIOUS(owner))
            return;
        if (!isliving(attacker))
            return;
        mob_living living_attacker = attacker as mob_living;
        if (!Dm.truthy(owner.unfriend(living_attacker)))
            return;
        if (!Dm.truthy(untamed_reaction))
            return;
        object display_message = replacetext(untamed_reaction, UNFRIEND_REPLACE_KEY_SOURCE, Dm.text("[]", owner));
        display_message = replacetext(display_message, UNFRIEND_REPLACE_KEY_TARGET, Dm.text("[]", attacker));
        owner.visible_message(span_notice(display_message as string));
    }
}
