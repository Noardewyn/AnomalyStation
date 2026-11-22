// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/verminous_plague.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111). The vermin are spawned as tg does; one whose own code is not
// ported yet stands inert (D132).

// tg: code/datums/diseases/verminous_plague.dm:1
public partial class datum_controller_global_vars
{
    public DmList cursed_vermin_by_stage;

    // tg: code/datums/diseases/verminous_plague.dm:1  GLOBAL_LIST_INIT(cursed_vermin_by_stage, list(...))
    public virtual void InitGlobalcursed_vermin_by_stage()
    {
        cursed_vermin_by_stage = Dm.list(
            Dm.kv("1", Dm.list( // Mostly harmless
                Dm.kv(typeof(mob_living_basic_butterfly), 1),
                Dm.kv(typeof(mob_living_basic_cockroach), 4),
                Dm.kv(typeof(mob_living_basic_cockroach_bloodroach), 1),
                Dm.kv(typeof(mob_living_basic_frog), 2),
                Dm.kv(typeof(mob_living_basic_mouse), 2),
                Dm.kv(typeof(mob_living_basic_snail), 2),
                Dm.kv(typeof(mob_living_basic_spider_maintenance), 1)
            )),
            Dm.kv("2", Dm.list( // Deal damage but not that dangerous
                Dm.kv(typeof(mob_living_basic_cockroach_bloodroach), 1),
                Dm.kv(typeof(mob_living_basic_cockroach_sewer), 1),
                Dm.kv(typeof(mob_living_basic_bee), 4),
                Dm.kv(typeof(mob_living_basic_frog), 3),
                Dm.kv(typeof(mob_living_basic_frog_crazy), 2),
                Dm.kv(typeof(mob_living_basic_mouse_rat), 4),
                Dm.kv(typeof(mob_living_basic_snail_angry), 2)
            )),
            Dm.kv("3", Dm.list( // More annoying
                Dm.kv(typeof(mob_living_basic_bee_toxin), 4),
                Dm.kv(typeof(mob_living_basic_cockroach_hauberoach), 3),
                Dm.kv(typeof(mob_living_basic_frog_crazy), 3),
                Dm.kv(typeof(mob_living_basic_mouse_rat), 3),
                Dm.kv(typeof(mob_living_basic_spider_growing_spiderling_hunter), 1)
            ))
        );
        Dm.append(ref gvars_datum_init_order, nameof(cursed_vermin_by_stage));
    }
}

/// Makes you cough out rats, bugs, etc
// tg: code/datums/diseases/verminous_plague.dm:30
public partial class datum_disease_verminous_plague : datum_disease
{

    // tg: code/datums/diseases/verminous_plague.dm:46
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (SPT_PROB(spawn_chance, seconds_per_tick))
            await spawn_mob();
        return dot;
    }

    /// Creates a pest
    // tg: code/datums/diseases/verminous_plague.dm:52
    public virtual async DmTask spawn_mob()
    {
        object creating_type = pick_weight(GLOB.cursed_vermin_by_stage[Dm.text("[]", stage)] as DmList);
        mob_living created = Dm.@new((Type)creating_type, affected_mob.drop_location()) as mob_living;
        if (QDELETED(created))
            return;

        obj_item_organ lungs = affected_mob.get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ;
        if (lungs != null && Dm.truthy(await affected_mob.emote("cough")))
        {
            affected_mob.visible_message(span_warning(I18n.T("[] coughs out []!", affected_mob, created)));
            await lungs.apply_organ_damage(stage * 2);
        }
        else
        {
            await affected_mob.vomit(VOMIT_CATEGORY_BLOOD_STUNLESS | MOB_VOMIT_FORCE);
            affected_mob.visible_message(span_warning(I18n.T("[] vomits out []!", affected_mob, created)));
        }

        if (stage == 1 || created.ai_controller == null)
            return;

        ((datum_ai_controller)created.ai_controller).set_blackboard_key(BB_CURRENT_TARGET, affected_mob);
        ((datum_ai_controller)created.ai_controller).insert_blackboard_key_lazylist(BB_BASIC_MOB_RETALIATE_LIST, affected_mob);
    }

    // tg: code/datums/diseases/verminous_plague.dm:72
    public override async DmTask<object> update_stage(object new_stage = null)
    {
        float was_stage = stage;
        object dot = null;
        dot = await base.update_stage(new_stage);
        if (Dm.num(new_stage) > was_stage)
            await spawn_mob();
        return dot;
    }
}
