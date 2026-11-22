// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/cult/cult_structures.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the base /obj/structure/destructible/cult translated (gaps ST5, D333: the base of the maintenance pants altar,
// `structures/maintenance.dm`, which a maintenance crate spawner can pick): its cult team, its examine (secured or not; the
// stability, tip and cooldown a cultist or a ghost reads), its "_off" sprite when unsecured, and the concealing spell's hide and
// reveal. The cult's item dispensers (the altar, the forge, the archives, the pylon) are the cult's and stay inert with the
// antagonist (D333, not-translated.md 213). The var block is generated (D111).

// Cult buildings!
// tg: code/modules/antagonists/cult/cult_structures.dm:2
public partial class obj_structure_destructible_cult : obj_structure_destructible
{
    // tg: code/modules/antagonists/cult/cult_structures.dm:18
    public override object Destroy(float? force = null)
    {
        cult_team = null;
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/cult/cult_structures.dm:22
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        await base.on_craft_completion(components, current_recipe, crafter);
        if (!ismob(crafter))
            return;
        mob_living builder = crafter as mob_living;
        datum_antagonist_cult cultist = builder?.mind?.has_antag_datum(typeof(datum_antagonist_cult), TRUE) as datum_antagonist_cult;
        cult_team = cultist?.get_team() as datum_team_cult;
    }

    /// Tries to find a cultist. If it succeeds, it also takes advantage of the moment to define the structure's cult team if it's not set yet.
    // tg: code/modules/antagonists/cult/cult_structures.dm:31
    public virtual object is_cultist_check(mob fool = null)
    {
        if (!Dm.truthy(IS_CULTIST(fool)))
            return FALSE;

        if (cult_team == null)
        {
            datum_antagonist_cult cultist = fool.mind?.has_antag_datum(typeof(datum_antagonist_cult), TRUE) as datum_antagonist_cult;
            cult_team = cultist?.get_team() as datum_team_cult;
        }

        return TRUE;
    }

    // tg: code/modules/antagonists/cult/cult_structures.dm:42
    public override object examine_status(mob user = null)
    {
        if (Dm.truthy(is_cultist_check(user)) || isobserver(user))
            return span_cult(I18n.T("It's at <b>[]%</b> stability.", round(Dm.num(atom_integrity) * 100 / Dm.num(max_integrity))));
        return base.examine_status(user);
    }

    // tg: code/modules/antagonists/cult/cult_structures.dm:47
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList ?? Dm.list();
        Dm.append(ref dot, span_notice(I18n.T("[] is [] the floor.", this, Dm.truthy(anchored) ? I18n.T("secured to") : I18n.T("unsecured from"))));
        if (Dm.truthy(is_cultist_check(user)) || isobserver(user))
        {
            if (Dm.truthy(cult_examine_tip))
                Dm.append(ref dot, span_cult(Dm.str(cult_examine_tip)));
            if (!(use_cooldown_duration <= world.time)) // COOLDOWN_FINISHED(src, use_cooldown_duration), as tg names it
                Dm.append(ref dot, span_cult_italic(I18n.T("The magic in [] is too weak, it will be ready to use again in <b>[]</b>.", this, DisplayTimeText(max(0, use_cooldown_duration - world.time))))); // COOLDOWN_TIMELEFT
        }
        return dot;
    }

    // tg: code/modules/antagonists/cult/cult_structures.dm:56
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = await base.set_anchored(anchorvalue);
        if (dot == null)
            return dot;
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/antagonists/cult/cult_structures.dm:62
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", Dm.initial(this, (atom p) => p.icon_state), Dm.truthy(anchored) ? "" : "_off");
        return await base.update_icon_state(__extra);
    }

    /*
     * Proc for use with the concealing spell. Hides the building (makes it invisible).
     */
    // tg: code/modules/antagonists/cult/cult_structures.dm:69
    public virtual void conceal()
    {
        set_density(FALSE);
        visible_message(span_danger(I18n.T("[] fades away.", this)));
        SetInvisibility(INVISIBILITY_OBSERVER, id: type);
        alpha = 100;
        set_light_power(0);
        set_light_range(0);
        update_light();
    }

    /*
     * Proc for use with the concealing spell. Reveals the building (makes it visible).
     */
    // tg: code/modules/antagonists/cult/cult_structures.dm:81
    public virtual void reveal()
    {
        set_density(Dm.initial(this, (atom p) => p.density));
        RemoveInvisibility(type);
        visible_message(span_danger(I18n.T("[] suddenly appears!", this)));
        alpha = Dm.initial(this, (atom p) => p.alpha);
        set_light_range(Dm.initial(this, (atom p) => p.light_range));
        set_light_power(Dm.initial(this, (atom p) => p.light_power));
        update_light();
    }
}
