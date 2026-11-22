// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/mining_tools.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the drills, shovels, spades, the entrenching tool and Big Slappy. The suicides are live-partial rows.
// The drill's cuffable_item element stays a row (the cuffed_item status effect, not ported).

// tg: code/modules/mining/equipment/mining_tools.dm:66
public partial class obj_item_pickaxe_drill : obj_item_pickaxe
{
    // tg: code/modules/mining/equipment/mining_tools.dm:77
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddElement(/datum/element/cuffable_item): cuffing the drill to the wrist (the cuffed_item status effect, not ported)
        Dm.Deferred("code/modules/mining/equipment/mining_tools.dm:79", "Initialize: AddElement(/datum/element/cuffable_item): cuffing the drill to the wrist (the cuffed_item status effect, not ported)");
        return dot;
    }
}

// tg: code/modules/mining/equipment/mining_tools.dm:113
public partial class obj_item_shovel : obj_item
{
    // tg: code/modules/mining/equipment/mining_tools.dm:135
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering), Dm.named("speed", 15 * SECONDS), Dm.named("effectiveness", 40)));
        //it's sharp, so it works, but barely.
        Waiting.NoSleep(AddElement(typeof(datum_element_gravedigger)));
        return dot;
    }
}

// tg: code/modules/mining/equipment/mining_tools.dm:173
public partial class obj_item_shovel_serrated : obj_item_shovel
{
    // tg: code/modules/mining/equipment/mining_tools.dm:191
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_bane), Dm.named("affected_biotypes", MOB_ORGANIC), Dm.named("damage_multiplier", 2))); //You may be horridly cursed now, but at least you kill the living a whole lot more easily!
        return dot;
    }

    // tg: code/modules/mining/equipment/mining_tools.dm:195
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!(user.mind != null && HAS_TRAIT(user.mind, TRAIT_MORBID)))
            return dot;
        Dm.append(ref dot, span_deadsay(I18n.T("You feel an intense, strange craving to 'dig' straight through living flesh with this shovel. Why else would it be serrated? The thought is mesmerizing...")));
        return dot;
    }
}

// tg: code/modules/mining/equipment/mining_tools.dm:210
public partial class obj_item_trench_tool : obj_item
{
    // tg: code/modules/mining/equipment/mining_tools.dm:229
    public override object get_all_tool_behaviours() => Dm.list(TOOL_MINING, TOOL_SHOVEL, TOOL_WRENCH);

    // tg: code/modules/mining/equipment/mining_tools.dm:232
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        Waiting.NoSleep(AddElement(typeof(datum_element_gravedigger)));
        return dot;
    }

    // tg: code/modules/mining/equipment/mining_tools.dm:237
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("Use in hand to switch configuration.")));
        Dm.append(ref dot, span_notice(I18n.T("It functions as a [] tool.", tool_behaviour)));
        Dm.append(ref dot, span_danger(I18n.T("<i>This weapon has no random critical hits.</i>")));
        return dot;
    }

    // tg: code/modules/mining/equipment/mining_tools.dm:243
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        string initial_state = Dm.str(Dm.initial(type, (obj_item_trench_tool t) => t.icon_state));
        if (Dm.eq(tool_behaviour, TOOL_WRENCH))
            inhand_icon_state = icon_state = initial_state;
        else if (Dm.eq(tool_behaviour, TOOL_SHOVEL))
            inhand_icon_state = icon_state = Dm.text("[]_shovel", initial_state);
        else if (Dm.eq(tool_behaviour, TOOL_MINING))
            inhand_icon_state = icon_state = Dm.text("[]_pick", initial_state);
        return dot;
    }

    // tg: code/modules/mining/equipment/mining_tools.dm:253
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (user == null)
            return dot;
        DmList tool_list = Dm.list(
            Dm.kv("Wrench", Dm.image(icon, null, "trench_tool")),
            Dm.kv("Shovel", Dm.image(icon, null, "trench_tool_shovel")),
            Dm.kv("Pick", Dm.image(icon, null, "trench_tool_pick")));
        object tool_result = await show_radial_menu(user, this, tool_list, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), require_near: TRUE, tooltips: TRUE);
        if (!Dm.truthy(check_menu(user)) || !Dm.truthy(tool_result))
            return dot;
        switch (tool_result)
        {
            case "Wrench":
                tool_behaviour = TOOL_WRENCH;
                sharpness = NONE;
                toolspeed = 0.75f;
                update_weight_class(WEIGHT_CLASS_SMALL);
                usesound = Dm.resource("sound/items/tools/ratchet.ogg");
                attack_verb_continuous = Dm.list("bashes", "bludgeons", "thrashes", "whacks");
                attack_verb_simple = Dm.list("bash", "bludgeon", "thrash", "whack");
                break;
            case "Shovel":
                tool_behaviour = TOOL_SHOVEL;
                sharpness = SHARP_EDGED;
                toolspeed = 0.25f;
                update_weight_class(WEIGHT_CLASS_NORMAL);
                usesound = Dm.resource("sound/effects/shovel_dig.ogg");
                attack_verb_continuous = Dm.list("slashes", "impales", "stabs", "slices");
                attack_verb_simple = Dm.list("slash", "impale", "stab", "slice");
                break;
            case "Pick":
                tool_behaviour = TOOL_MINING;
                sharpness = SHARP_POINTY;
                toolspeed = 0.5f;
                update_weight_class(WEIGHT_CLASS_NORMAL);
                usesound = Dm.resource("sound/effects/pickaxe/picaxe1.ogg");
                attack_verb_continuous = Dm.list("hits", "pierces", "slices", "attacks");
                attack_verb_simple = Dm.list("hit", "pierce", "slice", "attack");
                break;
        }
        playsound(this, Dm.resource("sound/items/tools/ratchet.ogg"), 50, vary: TRUE);
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/mining/equipment/mining_tools.dm:293
    public virtual object check_menu(mob user = null)
    {
        if (!istype(user, typeof(mob)))
            return FALSE;
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(Waiting.NoSleep(user.Adjacent(this))))
            return FALSE;
        return TRUE;
    }
}

// tg: code/modules/mining/equipment/mining_tools.dm:301
public partial class obj_item_shovel_giant_wrench : obj_item_shovel
{
    // tg: code/modules/mining/equipment/mining_tools.dm:337
    public override object get_all_tool_behaviours() => Dm.list(TOOL_SHOVEL, TOOL_WRENCH);

    // tg: code/modules/mining/equipment/mining_tools.dm:348
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        transform = (transform as matrix).Translate(-16, -16);
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("require_twohands", TRUE)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_transforming),
            Dm.named("force_on", 40),
            Dm.named("throwforce_on", throwforce),
            Dm.named("hitsound_on", hitsound),
            Dm.named("w_class_on", w_class),
            Dm.named("sharpness_on", SHARP_POINTY),
            Dm.named("clumsy_check", TRUE),
            Dm.named("inhand_icon_change", TRUE)));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return dot;
    }

    /// Used when the tool is transformed through the transforming component.
    // tg: code/modules/mining/equipment/mining_tools.dm:365
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        bool on = Dm.truthy(active);
        usesound = on ? Dm.resource("sound/items/tools/ratchet.ogg") : Dm.initial(type, (obj_item_shovel_giant_wrench w) => w.usesound);
        block_chance = on ? 0 : Dm.num(Dm.initial(type, (obj_item_shovel_giant_wrench w) => w.block_chance));
        recoil_factor = on ? 2 : Dm.num(Dm.initial(type, (obj_item_shovel_giant_wrench w) => w.recoil_factor));
        do_launch = on ? FALSE : Dm.num(Dm.initial(type, (obj_item_shovel_giant_wrench w) => w.do_launch));
        tool_behaviour = on ? TOOL_WRENCH : Dm.initial(type, (obj_item_shovel_giant_wrench w) => w.tool_behaviour) as string;
        armour_penetration = on ? 30 : Dm.num(Dm.initial(type, (obj_item_shovel_giant_wrench w) => w.armour_penetration));
        if (user != null)
            balloon_alert(user, I18n.T("folded Big Slappy []", on ? "open" : "closed"));
        playsound(this, Dm.resource("sound/items/tools/ratchet.ogg"), 50, TRUE);
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }

    // tg: code/modules/mining/equipment/mining_tools.dm:379
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (QDELETED(target_mob))
            return null;
        if (Dm.truthy(do_launch))
        {
            atom throw_target = get_edge_target_turf(target_mob, get_dir(user, get_step_away(target_mob, user))) as atom;
            await target_mob.throw_at(throw_target, 2, 2, user, gentle: TRUE);
            await target_mob.Knockdown(2 * SECONDS);
        }
        object body_zone = pick(user.get_all_limbs());
        await user.apply_damage(force / recoil_factor, BRUTE, body_zone, Dm.num(user.run_armor_check(body_zone, MELEE)));
        to_chat(user, span_danger(I18n.T("The weight of the Big Slappy recoils!")));
        log_combat(user, user, "recoiled Big Slappy into");
        return null;
    }
}
