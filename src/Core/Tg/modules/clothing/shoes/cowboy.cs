// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/shoes/cowboy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: /obj/item/clothing/shoes/cowboy (M4a clothing: cowboy boots are put on and taken off; a snake hiding in them bites on the
// way in; syndicate N2b, D316: the boot pocket, the snake, the spurs' jingle, a creature put into the boots and climbing out of them, for
// the outlaw's spurred boots); gaps CL1 (D333): the lizardskin boots take the crafting hide's colour.

// tg: code/modules/clothing/shoes/cowboy.dm:1
public partial class obj_item_clothing_shoes_cowboy : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/cowboy.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_shoes)));
        if (prob(2)) //There's a snake in my boot
            Dm.@new<mob_living_basic_snake>(this);
        if (Dm.truthy(has_spurs))
            Waiting.NoSleep(LoadComponent(typeof(datum_component_squeak), spur_sound, 50, Dm.named("falloff_exponent", 20)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ignites_matches)));
        return dot;
    }

    // tg: code/modules/clothing/shoes/cowboy.dm:28
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        RegisterSignal(user, COMSIG_LIVING_SLAM_TABLE, nameof(table_slam), @override: TRUE);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_FEET)))
        {
            foreach (mob_living occupant in Dm.each<mob_living>(contents))
            {
                object target_zone = user.get_random_valid_zone(blacklisted_parts: Dm.list(BODY_ZONE_CHEST, BODY_ZONE_HEAD, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM), even_weights: TRUE, bypass_warning: TRUE);
                if (!Dm.truthy(target_zone)) //we broke their legs right on off!
                    break;
                await occupant.forceMove(user.drop_location() as atom);
                user.visible_message(span_warning(I18n.T("[] recoils as something slithers out of [].", user, this)), span_userdanger(I18n.T("You feel a sudden stabbing pain in your []!", pick(I18n.T("foot"), I18n.T("toe"), I18n.T("ankle")))));
                await ((mob_living)user).Knockdown(20); //Is one second paralyze better here? I feel you would fall on your ass in some fashion.
                await occupant.UnarmedAttack(user, proximity_flag: TRUE);
            }
        }
    }

    // tg: code/modules/clothing/shoes/cowboy.dm:41
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.dropped(user, silent, __extra);
        UnregisterSignal(user, COMSIG_LIVING_SLAM_TABLE);
        return dot;
    }

    // tg: code/modules/clothing/shoes/cowboy.dm:45
    [SignalHandler]
    public virtual void table_slam(mob_living source = null, obj_structure_table the_table = null)
    {
        Waiting.Detach(handle_table_slam(source));
    }

    // tg: code/modules/clothing/shoes/cowboy.dm:49
    public virtual async DmTask handle_table_slam(mob_living user = null)
    {
        // user.say(pick("Hot damn!", "Hoo-wee!", "Got-dang!"), spans = list(SPAN_YELL), forced=TRUE) and
        // user.client?.give_award(/datum/award/achievement/misc/hot_damn, user): speech (M9) and the achievements (M10).
        Dm.NotTranslated("code/modules/clothing/shoes/cowboy.dm:50", "handle_table_slam: the cowboy's yell and its achievement (table slams, after M10: D249)");
    }

    // tg: code/modules/clothing/shoes/cowboy.dm:53
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        object dot = await base.mouse_drop_receive(dropped, user, @params);
        var target = dropped as mob_living;
        if (!Dm.truthy(Dm.bitand((user as mob_living)?.mobility_flags ?? 0, MOBILITY_USE)) || !isliving(target))
            return dot;
        if (contents.len >= max_occupants)
        {
            to_chat(user, span_warning(I18n.T("[] are full!", this)));
            return dot;
        }
        if (istype(target, typeof(mob_living_basic_snake)) || istype(target, typeof(mob_living_basic_headslug)) || islarva(target))
        {
            await target.forceMove(this);
            to_chat(user, span_notice(I18n.T("[] slithers into [].", target, this)));
        }
        return dot;
    }

    // tg: code/modules/clothing/shoes/cowboy.dm:64
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        if (!Dm.truthy(await do_after(user, 1 * SECONDS, target: user)))
            return null;
        await user.forceMove(drop_location() as atom);
        return null;
    }
}

// tg: code/modules/clothing/shoes/cowboy.dm:87
public partial class obj_item_clothing_shoes_cowboy_lizard : obj_item_clothing_shoes_cowboy
{
    // tg: code/modules/clothing/shoes/cowboy.dm:102
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        obj_item_stack_sheet_animalhide_carbon_lizard skin = Dm.locate_type(typeof(obj_item_stack_sheet_animalhide_carbon_lizard), components) as obj_item_stack_sheet_animalhide_carbon_lizard;
        if (skin == null || length(skin.skin_color) == 0) // what
        {
            await base.on_craft_completion(components, current_recipe, crafter);
            return;
        }
        await set_greyscale(skin.skin_color);
        await base.on_craft_completion(components, current_recipe, crafter);
    }
}
