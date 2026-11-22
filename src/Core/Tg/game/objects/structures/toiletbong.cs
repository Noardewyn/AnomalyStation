// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/toiletbong.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. The toilet bong (crafted from a toilet and a flamethrower): food is loaded through its storage
// (twelve items, the weed overlay while it holds any), a long drag burns one item into a chem smoke of its reagents (an emagged one
// burns them all, with a wider smoke) with the flamethrower's sound, one drag in twenty brings up a mouse; wrenched, rotated, and
// taken apart with a crowbar back into the flamethrower and an empty plasma tank. Its storage kind is in
// datums/storage/subtypes/others/misc.cs. The var block is generated (D111).

// tg: code/game/objects/structures/toiletbong.dm:1
public partial class obj_structure_toiletbong : obj_structure
{
    // tg: code/game/objects/structures/toiletbong.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation), Dm.named("post_rotation_proccall", nameof(post_rotation))));
        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_toiletbong)));

        weed_overlay = (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/watercloset.dmi"), Dm.text("[]_overlay", base_icon_state));
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/game/objects/structures/toiletbong.dm:21
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        obj_structure_toilet toilet = Dm.locate<obj_structure_toilet>(components);
        if (toilet != null)
        {
            foreach (obj_item cistern_item in Dm.each<obj_item>(toilet.contents))
            {
                await cistern_item.forceMove(crafter.drop_location() as atom);
                to_chat(crafter, span_warning(I18n.T("[] falls out of the toilet!", cistern_item)));
            }
            await setDir(toilet.dir);
            await forceMove(toilet.loc);
        }

        crafter.visible_message(
            span_notice(I18n.T("[] attaches the flamethrower to the repurposed toilet.", crafter)),
            span_notice(I18n.T("You attach the flamethrower to the repurposed toilet.")));
        await base.on_craft_completion(components, current_recipe, crafter);
    }

    // tg: code/game/objects/structures/toiletbong.dm:36
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(LAZYLEN(contents)))
            Dm.append(ref dot, weed_overlay);
        return dot;
    }

    // tg: code/game/objects/structures/toiletbong.dm:41
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers);
        if (!Dm.truthy(anchored))
        {
            user.balloon_alert(user, I18n.T("secure it first!"));
            return dot;
        }
        if (!Dm.truthy(LAZYLEN(contents)))
        {
            user.balloon_alert(user, I18n.T("it's empty!"));
            return dot;
        }
        user.visible_message(span_boldnotice(I18n.T("[] takes a huge drag on the [].", user, this)));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
            return dot;
        turf toiletbong_location = loc as turf;
        toiletbong_location.hotspot_expose(1000, 5);
        foreach (obj_item item in Dm.each<obj_item>(contents))
        {
            if (Dm.truthy(Dm.bitand(item.resistance_flags, INDESTRUCTIBLE)))
            {
                user.balloon_alert(user, I18n.T("[] is blocking the pipes!", item.name));
                continue;
            }
            playsound(this, Dm.resource("sound/items/modsuit/flamethrower.ogg"), 50);

            float smoke_amount = DIAMOND_AREA(smokeradius);
            await do_chem_smoke(amount: smoke_amount, holder: this, location: loc, carry: item.reagents as datum_reagents, carry_limit: 20, smoke_type: typeof(datum_effect_system_fluid_spread_smoke_chem_smoke_machine));
            if (prob(5) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            {
                if (Dm.truthy(Dm.bitand((user as mob_living)?.get_liked_foodtypes(), GORE)))
                {
                    user.balloon_alert(user, I18n.T("a hidden treat!"));
                    user.visible_message(span_danger(I18n.T("[] fishes a mouse out of the pipes.", user)));
                }
                else
                {
                    to_chat(user, span_userdanger(I18n.T("There was something disgusting in the pipes!")));
                    user.visible_message(span_danger(I18n.T("[] spits out a mouse.", user)));
                    user.adjust_disgust(50);
                    if (user is mob_living_carbon carbon_user)
                        await carbon_user.vomit(VOMIT_CATEGORY_DEFAULT);
                }
                mob_living spawned_mob = Dm.@new<mob_living_basic_mouse>(get_turf(user));
                spawned_mob.add_faction(Dm.text("[]", REF(user)));
                if (prob(50))
                {
                    float steps = rand(1, 3);
                    for (int j = 1; j <= steps; j++)
                        await step(spawned_mob, Dm.num(pick(NORTH, SOUTH, EAST, WEST)));
                }
            }
            qdel(item);
            if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                break;
        }
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/game/objects/structures/toiletbong.dm:81
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    ///Called in the simple rotation's post_rotation callback, playing a sound cue to players.
    // tg: code/game/objects/structures/toiletbong.dm:86
    public virtual void post_rotation(mob user = null, object degrees = null)
    {
        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50);
    }

    // tg: code/game/objects/structures/toiletbong.dm:89
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(anchored))
            return FALSE;
        tool.play_tool_sound(this);
        to_chat(user, span_notice(I18n.T("You begin taking apart the [].", this)));
        if (!Dm.truthy(await do_after(user, 10 * SECONDS, target: this)))
            return FALSE;
        Dm.@new<obj_item_flamethrower>(get_turf(this));
        obj_item_tank_internals_plasma ptank = Dm.@new<obj_item_tank_internals_plasma>(get_turf(this));
        ptank.air_contents.set_gas(typeof(datum_gas_plasma), 0);
        await drop_custom_materials();
        qdel(this);
        return TRUE;
    }

    // tg: code/game/objects/structures/toiletbong.dm:103
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.num(Dm.bitor(obj_flags, EMAGGED));
        smokeradius = 2;
        playsound(this, Dm.resource("sound/effects/fish_splash.ogg"), 50);
        balloon_alert(user, I18n.T("toilet broke"));
        if (emag_card != null)
            to_chat(user, span_boldwarning(I18n.T("The [] falls into the toilet. You fish it back out. Looks like you broke the toilet.", emag_card)));
        await DmTask.CompletedTask;
        return TRUE;
    }
}
