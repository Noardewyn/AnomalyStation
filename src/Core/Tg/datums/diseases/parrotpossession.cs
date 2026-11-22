// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/parrotpossession.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111). Poly's ghost gives it (parrot/poly.dm).

// tg: code/datums/diseases/parrotpossession.dm:1
public partial class datum_disease_parrot_possession : datum_disease
{


    // tg: code/datums/diseases/parrotpossession.dm:22
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);

        if (!Dm.truthy(dot) || parrot_controller == null)
            return dot;

        object potential_phrase = parrot_controller.blackboard[BB_PARROT_REPEAT_STRING];

        if (SPT_PROB(speak_chance, seconds_per_tick) && potential_phrase != null)
            await affected_mob.say(potential_phrase, forced: "parrot possession");
        return dot;
    }


    // tg: code/datums/diseases/parrotpossession.dm:34
    public override async DmTask cure(float? add_resistance = null)
    {
        add_resistance ??= FALSE;
        atom_movable inside_parrot = Dm.locate<mob_living_basic_parrot_poly_ghost>(affected_mob);
        if (inside_parrot != null)
        {
            UnregisterSignal(inside_parrot, Dm.list(COMSIG_PREQDELETED, COMSIG_MOVABLE_MOVED));
            await inside_parrot.forceMove(affected_mob.drop_location() as atom);
            affected_mob.visible_message(
                span_danger(I18n.T("[] is violently driven out of []!", inside_parrot, affected_mob)),
                span_userdanger(I18n.T("[] bursts out of your chest!", inside_parrot))
            );
        }
        parrot_controller = null;
        await base.cure(add_resistance);
    }

    // tg: code/datums/diseases/parrotpossession.dm:46
    public virtual void set_parrot(mob_living parrot = null)
    {
        parrot_controller = parrot.ai_controller as datum_ai_controller_basic_controller;
        RegisterSignals(parrot, Dm.list(COMSIG_PREQDELETED, COMSIG_MOVABLE_MOVED), nameof(on_parrot_exit));
    }

    // tg: code/datums/diseases/parrotpossession.dm:50
    [SignalHandler]
    public virtual void on_parrot_exit(datum source = null)
    {
        UnregisterSignal(source, Dm.list(COMSIG_PREQDELETED, COMSIG_MOVABLE_MOVED));
        Waiting.NoSleep(cure());
    }
}
