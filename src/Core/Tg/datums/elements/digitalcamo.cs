// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/digitalcamo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: the digital camouflage element (the changeling's Digital Camouflage): the AIs cannot track the
// mob and see an empty override image in its place, their silicon huds lose it, and examiners see the shifting skin. The var block
// is generated (D111).

// tg: code/datums/elements/digitalcamo.dm:1
public partial class datum_element_digitalcamo : datum_element
{
    // tg: code/datums/elements/digitalcamo.dm:5
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        START_PROCESSING(SSdcs, this);
    }

    // tg: code/datums/elements/digitalcamo.dm:9
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.Attach(target, __extra);
        if (!isliving(target) || Dm.@in(target, attached_mobs))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(target, COMSIG_LIVING_CAN_TRACK, nameof(can_track));
        image img = image(loc: target);
        img.@override = TRUE;
        attached_mobs[target] = img;
        HideFromAIHuds(target as mob_living);
        return dot;
    }

    // tg: code/datums/elements/digitalcamo.dm:20
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Detach(target, __extra);
        UnregisterSignal(target, Dm.list(COMSIG_ATOM_EXAMINE, COMSIG_LIVING_CAN_TRACK));
        foreach (mob_living_silicon_ai AI in Dm.each<mob_living_silicon_ai>(GLOB.player_list))
            Dm.remove(AI.client.images, attached_mobs[target]);
        Dm.remove(ref attached_mobs, target);
        UnhideFromAIHuds(target as mob_living);
        return dot;
    }

    // tg: code/datums/elements/digitalcamo.dm:28
    public virtual void HideFromAIHuds(mob_living target = null)
    {
        foreach (mob_living_silicon_ai AI in Dm.each<mob_living_silicon_ai>(GLOB.ai_list))
        {
            foreach (object hud_trait in Dm.each(AI.silicon_huds))
            {
                datum_atom_hud silicon_hud = GLOB.huds[GLOB.trait_to_hud[hud_trait]] as datum_atom_hud;
                silicon_hud.hide_single_atomhud_from(AI, target);
            }
        }
    }

    // tg: code/datums/elements/digitalcamo.dm:34
    public virtual void UnhideFromAIHuds(mob_living target = null)
    {
        foreach (mob_living_silicon_ai AI in Dm.each<mob_living_silicon_ai>(GLOB.ai_list))
        {
            foreach (object hud_trait in Dm.each(AI.silicon_huds))
            {
                datum_atom_hud silicon_hud = GLOB.huds[GLOB.trait_to_hud[hud_trait]] as datum_atom_hud;
                silicon_hud.unhide_single_atomhud_from(AI, target);
            }
        }
    }

    // tg: code/datums/elements/digitalcamo.dm:40
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob M = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_warning(I18n.T("[] skin seems to be shifting like something is moving below it.", source.p_their())));
    }

    // tg: code/datums/elements/digitalcamo.dm:45
    [SignalHandler]
    public virtual object can_track(datum source = null, mob user = null)
    {
        return COMPONENT_CANT_TRACK;
    }

    // tg: code/datums/elements/digitalcamo.dm:50
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        foreach (mob_living_silicon_ai AI in Dm.each<mob_living_silicon_ai>(GLOB.player_list))
        {
            foreach (object mob in Dm.each(attached_mobs))
                Dm.combine(AI.client.images, attached_mobs[mob]);
        }
        return null;
    }
}
