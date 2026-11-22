// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/kitchen_spike.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the meat spike and its frame. The var blocks are generated (D111).

// tg: code/game/objects/structures/kitchen_spike.dm:3
public partial class obj_structure_kitchenspike_frame : obj_structure
{
    // tg: code/game/objects/structures/kitchen_spike.dm:1  #define MEATSPIKE_IRONROD_REQUIREMENT 4 (file-local, #undef at :179)
    internal const float MEATSPIKE_IRONROD_REQUIREMENT = 4;

    // tg: code/game/objects/structures/kitchen_spike.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/game/objects/structures/kitchen_spike.dm:17
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("It can be <b>welded</b> apart."));
        Dm.append(ref dot, I18n.T("You could attach <b>[]</b> iron rods to it to create a <b>Meat Spike</b>.", MEATSPIKE_IRONROD_REQUIREMENT));
        return dot;
    }

    // tg: code/game/objects/structures/kitchen_spike.dm:22
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
            return NONE;

        string message = "";
        if (Dm.eq(held_item.tool_behaviour, TOOL_WELDER))
            message = "Deconstruct";
        else if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
            message = "Bolt Down Frame";

        if (!Dm.truthy(message))
            return NONE;
        context[SCREENTIP_CONTEXT_LMB] = message;
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/objects/structures/kitchen_spike.dm:37
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(tool.tool_start_check(user, amount: 0, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return FALSE;
        to_chat(user, span_notice(I18n.T("You begin cutting \\the [] apart...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 5 * SECONDS, volume: 50)))
            return TRUE;
        visible_message(span_notice(I18n.T("[] slices apart \\the [].", user, this)),
            span_notice(I18n.T("You cut \\the [] apart with \\the [].", this, tool)),
            span_hear(I18n.T("You hear welding.")));
        Dm.@new<obj_item_stack_sheet_iron>(loc, MEATSPIKE_IRONROD_REQUIREMENT);
        qdel(this);
        return TRUE;
    }

    // tg: code/game/objects/structures/kitchen_spike.dm:50
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await default_unfasten_wrench(user, tool);
        return TRUE;
    }

    // tg: code/game/objects/structures/kitchen_spike.dm:54
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);
        if (tool is not obj_item_stack_rods)
            return NONE;

        obj_item_stack_rods used_rods = (obj_item_stack_rods)tool;
        if (Dm.num(used_rods.get_amount()) < MEATSPIKE_IRONROD_REQUIREMENT)
        {
            balloon_alert(user, I18n.T("[] rods needed!", MEATSPIKE_IRONROD_REQUIREMENT));
            return ITEM_INTERACT_BLOCKING;
        }

        await used_rods.use(MEATSPIKE_IRONROD_REQUIREMENT);
        obj_structure new_meatspike = Dm.@new<obj_structure_kitchenspike>(loc);
        new_meatspike.balloon_alert(user, I18n.T("meatspike built"));
        transfer_fingerprints_to(new_meatspike);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/structures/kitchen_spike.dm:71
public partial class obj_structure_kitchenspike : obj_structure
{
    // tg: code/game/objects/structures/kitchen_spike.dm:85
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        ADD_TRAIT(this, TRAIT_DANGEROUS_BUCKLE, INNATE_TRAIT);
        return dot;
    }

    // tg: code/game/objects/structures/kitchen_spike.dm:90
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("<b>Drag a mob</b> onto it to hook it in place."));
        Dm.append(ref dot, I18n.T("A <b>crowbar</b> could remove those spikes."));
        return dot;
    }

    // tg: code/game/objects/structures/kitchen_spike.dm:95
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
            return NONE;

        if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Remove Spikes";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        return NONE;
    }

    // tg: code/game/objects/structures/kitchen_spike.dm:105
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/structures/kitchen_spike.dm:108
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(has_buckled_mobs()))
        {
            to_chat(user, span_warning(I18n.T("You can't do that while something's on the spike!")));
            return TRUE;
        }

        if (Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 100)))
        {
            to_chat(user, span_notice(I18n.T("You pry the spikes out of the frame.")));
            await deconstruct(TRUE);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/structures/kitchen_spike.dm:119
    public override async DmTask<object> user_buckle_mob(mob_living target = null, mob user = null, float? check_loc = null)
    {
        check_loc ??= TRUE;
        if (!iscarbon(target) && !isanimal_or_basicmob(target))
            return null;
        if (target != user || target.loc == loc)
            return await base.user_buckle_mob(target, user, check_loc);
        if (!Dm.truthy(await do_after(user, 10 * SECONDS, target)))
            return null;
        if (!Dm.truthy(await is_user_buckle_possible(target, user, check_loc)))
            return FALSE;
        return await base.user_buckle_mob(target, user, check_loc);
    }

    // tg: code/game/objects/structures/kitchen_spike.dm:130
    public override async DmTask post_buckle_mob(mob_living target = null)
    {
        playsound(this.loc, Dm.resource("sound/effects/splat.ogg"), 25, TRUE);
        await target.emote("scream");
        await target.add_splatter_floor();
        await target.adjust_brute_loss(30);
        target.add_offsets(type, x_add: -1);
        await target.set_lying_angle(buckle_lying);
        ADD_TRAIT(target, TRAIT_MOVE_UPSIDE_DOWN, REF(this));
        // So you can butcher people too
        await target.AddComponentFrom(REF(this), typeof(datum_component_free_operation));
    }

    // tg: code/game/objects/structures/kitchen_spike.dm:141
    public override async DmTask<object> user_unbuckle_mob(mob_living buckled_mob = null, mob user = null)
    {
        if (buckled_mob != user)
        {
            buckled_mob.visible_message(span_notice(I18n.T("[] tries to pull [] free of []!", user, buckled_mob, this)),
                span_notice(I18n.T("[] is trying to pull you off [], opening up fresh wounds!", user, this)),
                span_hear(I18n.T("You hear a squishy wet noise.")));
            if (!Dm.truthy(await do_after(user, 30 * SECONDS, target: this)))
            {
                if (buckled_mob?.buckled != null)
                    buckled_mob.visible_message(span_notice(I18n.T("[] fails to free []!", user, buckled_mob)),
                        span_notice(I18n.T("[] fails to pull you off of [].", user, this)));
                return null;
            }
        }
        else
        {
            buckled_mob.visible_message(span_warning(I18n.T("[] struggles to break free from []!", buckled_mob, this)),
                span_notice(I18n.T("You struggle to break free from [], exacerbating your wounds! (Stay still for two minutes.)", this)),
                span_hear(I18n.T("You hear a wet squishing noise..")));
            await buckled_mob.adjust_brute_loss(30);
            if (!Dm.truthy(await do_after(buckled_mob, 2 * MINUTES, target: this, cog_icon: null)))
            {
                if (buckled_mob?.buckled != null)
                    to_chat(buckled_mob, span_warning(I18n.T("You fail to free yourself!")));
                return null;
            }
        }
        return await base.user_unbuckle_mob(buckled_mob, user);
    }

    // tg: code/game/objects/structures/kitchen_spike.dm:163
    public override async DmTask post_unbuckle_mob(mob_living buckled_mob = null)
    {
        await buckled_mob.adjust_brute_loss(30);
        Waiting.Detach(buckled_mob.emote("scream"));
        await buckled_mob.AdjustParalyzed(20);
        buckled_mob.remove_offsets(type);
        REMOVE_TRAIT(buckled_mob, TRAIT_MOVE_UPSIDE_DOWN, REF(this));
        buckled_mob.RemoveComponentSource(REF(this), typeof(datum_component_free_operation));
    }

    // tg: code/game/objects/structures/kitchen_spike.dm:171
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (Dm.truthy(disassembled))
        {
            obj_structure meatspike_frame = Dm.@new<obj_structure_kitchenspike_frame>(this.loc);
            transfer_fingerprints_to(meatspike_frame);
        }
        else
            Dm.@new<obj_item_stack_sheet_iron>(this.loc, 4);
        Dm.@new<obj_item_stack_rods>(loc, obj_structure_kitchenspike_frame.MEATSPIKE_IRONROD_REQUIREMENT);
    }
}
