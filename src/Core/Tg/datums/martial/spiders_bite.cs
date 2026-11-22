// Translated from tgstation (snapshot 1d99bd7f): code/datums/martial/spiders_bite.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3b (D316): the Spider's Bite (kicks on staggered targets, the kick's disarm, the tackle, the harder grabs),
// whole. The var block is generated (D111).

// tg: code/datums/martial/spiders_bite.dm:1
public partial class datum_martial_art_spiders_bite : datum_martial_art
{
    // tg: code/datums/martial/spiders_bite.dm:14
    public override async DmTask activate_style(mob_living new_holder = null)
    {
        await base.activate_style(new_holder);
        RegisterSignal(new_holder, COMSIG_HUMAN_PUNCHED, nameof(kick_disarm));
        tackle_comp = await new_holder.AddComponent(typeof(datum_component_tackler),
            Dm.named("stamina_cost", 20),
            Dm.named("base_knockdown", 0.2f * SECONDS),
            Dm.named("range", 5),
            Dm.named("speed", 1.5f),
            Dm.named("skill_mod", 6),
            Dm.named("min_distance", 1),
            Dm.named("silent_gain", TRUE)) as datum_component_tackler;
    }

    // tg: code/datums/martial/spiders_bite.dm:27
    public override async DmTask deactivate_style(mob_living old_holder = null)
    {
        await base.deactivate_style(old_holder);
        UnregisterSignal(old_holder, COMSIG_HUMAN_PUNCHED);
        qdel(tackle_comp); // QDEL_NULL(tackle_comp)
        tackle_comp = null;
    }

    // tg: code/datums/martial/spiders_bite.dm:32
    [SignalHandler]
    public virtual void kick_disarm(mob_living source = null, mob_living target = null, object damage = null, object attack_type = null, object atk_effect = null, obj_item_bodypart affecting = null, object final_armor_block = null, object limb_sharpness = null)
    {
        if (!Dm.eq(atk_effect, ATTACK_EFFECT_KICK))
        {
            last_hit_ref = null;
            return;
        }
        object new_hit_ref = REF(target);
        if (Dm.eq(last_hit_ref, new_hit_ref))
        {
            last_hit_count++;
        }
        else
        {
            last_hit_count = 1;
            last_hit_ref = new_hit_ref;
        }

        if (!prob(33 * last_hit_count))
            return;

        var weapon = target.get_active_held_item() as obj_item;
        if (weapon == null || !Dm.truthy(Waiting.NoSleep(target.dropItemToGround(weapon))))
            return;
        source.visible_message(
            span_warning(I18n.T("[] knocks []'s [] out of [] hands with a kick!", source, target, weapon.name, target.p_their())),
            span_notice(I18n.T("You channel the flow of gravity and knock []'s [] out of [] hands with a kick!", target, weapon.name, target.p_their())),
            span_hear(I18n.T("You hear a thud, followed by a clatter.")));
    }

    // tg: code/datums/martial/spiders_bite.dm:57
    public override object get_prefered_attacking_limb(mob_living martial_artist = null, mob_living target = null)
    {
        if (!Dm.truthy(target.has_status_effect(typeof(datum_status_effect_staggered))))
            return null;

        return IS_LEFT_INDEX(Dm.num(martial_artist.active_hand_index)) ? BODY_ZONE_L_LEG : BODY_ZONE_R_LEG;
    }

    // tg: code/datums/martial/spiders_bite.dm:63
    public override object get_style_help()
    {
        DmList dot = Dm.list();

        Dm.append(dot, span_info(I18n.T("<b><i>You retreat inward and recall the Spider Clan's techniques...</i></b>\n&bull; Remember, <b>Many Legged Spider</b>: Unarmed attacks against staggered opponents will always be kicks - granting you greater accuracy and damage.\n&bull; Remember, <b>Jump and Climb</b>: Right clicking on throw mode will perform a tackle which is far far less likely to fail.\n&bull; Remember, <b>Flow of Gravity</b>: Kicking opponents will have a chance to knock their weapons to the floor. The chance increases for each sequential kick.\n&bull; Remember, <b>Wrap in Web</b>: Your grabs will be harder to escape from.")));
        return dot;
    }
}
