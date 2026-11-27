// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/water_structures/water_source.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H1: the whole file (the infinite water source and the puddle, whose tadpoles are scooped since the fish were ported).
// The var blocks are generated (D111).

// tg: code/game/objects/structures/water_structures/water_source.dm:2
public partial class obj_structure_water_source : obj_structure
{
    // tg: code/game/objects/structures/water_structures/water_source.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(INFINITY, NO_REACT);
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(dispensedreagent, INFINITY));
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/water_source.dm:18
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!iscarbon(user))
            return dot;
        var living = (mob_living)user;
        if (!Dm.truthy(await Adjacent(living)))
            return dot;

        if (Dm.truthy(busy))
        {
            to_chat(living, span_warning(I18n.T("Someone's already washing here!")));
            return dot;
        }
        object selected_area = living.parse_zone_with_bodypart(living.zone_selected);
        bool washing_face = Dm.@in(selected_area, Dm.list(BODY_ZONE_HEAD, BODY_ZONE_PRECISE_MOUTH, BODY_ZONE_PRECISE_EYES));
        string part = washing_face ? I18n.T("face") : I18n.T("hands");
        living.visible_message(
            span_notice(I18n.T("[] starts washing [] []...", living, living.p_their(), part)),
            span_notice(I18n.T("You start washing your []...", part)));
        busy = TRUE;

        if (!Dm.truthy(await do_after(living, 4 * SECONDS, target: this)))
        {
            busy = FALSE;
            return dot;
        }

        busy = FALSE;

        if (washing_face)
            living.SEND_SIGNAL(COMSIG_COMPONENT_CLEAN_FACE_ACT, Dm.list(living, CLEAN_WASH));
        else if (living is mob_living_carbon_human human_user)
        {
            if (!Dm.truthy(await human_user.wash_hands(CLEAN_WASH)))
            {
                to_chat(living, span_warning(I18n.T("Your hands are covered by something!")));
                return dot;
            }
        }
        else
            await living.wash(CLEAN_WASH);

        living.visible_message(
            span_notice(I18n.T("[] washes [] [] using [].", living, living.p_their(), part, this)),
            span_notice(I18n.T("You wash your [] using [].", part, this)));
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/water_source.dm:60
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(busy))
        {
            to_chat(user, span_warning(I18n.T("Someone's already washing here!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT))) //Abstract items like grabs won't wash. No-drop items will though because it's still technically an item in your hand.
            return ITEM_INTERACT_BLOCKING;

        if (is_reagent_container(tool))
        {
            var container = (obj_item_reagent_containers)tool;
            if (Dm.truthy(container.is_refillable())) // no early return, we want items that cannot perform their unique interactions to wash
            {
                if (Dm.truthy(((datum_reagents)container.reagents).holder_full()))
                {
                    to_chat(user, span_notice(I18n.T("\\The [] is full.", container)));
                    return ITEM_INTERACT_BLOCKING;
                }

                await ((datum_reagents)container.reagents).add_reagent(dispensedreagent, min(Dm.num(container.volume) - ((datum_reagents)container.reagents).total_volume, container.amount_per_transfer_from_this));
                to_chat(user, span_notice(I18n.T("You fill [] from [].", container, this)));
                return ITEM_INTERACT_SUCCESS;
            }
        }

        if (tool is obj_item_melee_baton_security baton)
        {
            if (baton.cell != null && Dm.truthy(baton.cell.charge) && Dm.truthy(baton.active))
            {
                flick("baton_active", this);
                await user.Paralyze(baton.knockdown_time);
                await user.set_stutter(baton.knockdown_time);
                await baton.cell.use(baton.cell_hit_cost);
                user.visible_message(
                    span_warning(I18n.T("[] shocks []self while attempting to wash the active []!", user, user.p_them(), baton.name)),
                    span_userdanger(I18n.T("You unwisely attempt to wash [] while it's still on.", baton)));
                playsound(this, baton.on_stun_sound, 50, TRUE);
                return ITEM_INTERACT_SUCCESS;
            }
        }

        if (tool is obj_item_mop)
        {
            await ((datum_reagents)tool.reagents).add_reagent(dispensedreagent, 5);
            to_chat(user, span_notice(I18n.T("You wet [] in [].", tool, this)));
            playsound(loc, Dm.resource("sound/effects/slosh.ogg"), 25, TRUE);
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(tool.item_flags, NOBLUDGEON)))
        {
            to_chat(user, span_notice(I18n.T("You start washing []...", tool)));
            busy = TRUE;
            if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)))
            {
                busy = FALSE;
                return ITEM_INTERACT_BLOCKING;
            }
            busy = FALSE;
            await tool.wash(CLEAN_WASH);
            await ((datum_reagents)reagents).expose(tool, TOUCH, 5f / max(((datum_reagents)reagents).total_volume, 5f));
            user.visible_message(
                span_notice(I18n.T("[] washes [] using [].", user, tool, this)),
                span_notice(I18n.T("You wash [] using [].", tool, this)));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }
}

// tg: code/game/objects/structures/water_structures/water_source.dm:115
public partial class obj_structure_water_source_puddle : obj_structure_water_source
{
    // tg: code/game/objects/structures/water_structures/water_source.dm:122
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/water_source.dm:126
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (held_item == null)
            context[SCREENTIP_CONTEXT_RMB] = "Scoop Tadpoles";
        return dot;
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/game/objects/structures/water_structures/water_source.dm:132
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        icon_state = Dm.str(Dm.text("[]-splash", base_icon_state));
        object dot = await base.attack_hand(user, modifiers, __extra);
        icon_state = Dm.str(base_icon_state);
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/water_source.dm:137
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        icon_state = Dm.str(Dm.text("[]-splash", base_icon_state));
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        icon_state = Dm.str(base_icon_state);
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/water_source.dm:142
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, this)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        icon_state = Dm.str(Dm.text("[]-splash", base_icon_state));
        balloon_alert(user, "scooping tadpoles...");
        if (Dm.truthy(await do_after(user, 5 * SECONDS, this)))
        {
            playsound(loc, Dm.resource("sound/effects/slosh.ogg"), 15, TRUE);
            balloon_alert(user, "got a tadpole");
            obj_item_fish_tadpole tadpole = await Dm.new_async<obj_item_fish_tadpole>(loc);
            await tadpole.randomize_size_and_weight();
            await user.put_in_hands(tadpole);
        }
        icon_state = Dm.str(base_icon_state);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }
}
