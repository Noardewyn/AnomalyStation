// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/knives.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The knives (gaps package C4): the box cutter (J6) and the switchblade (syndicate G4c: the monkey gun case's) came later; the knife's and the carrot
// shiv's suicides (gaps WP1); the bloodletter's stacks are a row.

// tg: code/game/objects/items/weaponry/melee/knives.dm:7
public partial class obj_item_knife : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/knives.dm:66
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(pick(span_suicide(I18n.T("[] is slitting [] wrists with \\the []! It looks like [] trying to commit suicide.", user, user.p_their(), this, user.p_theyre())),
            span_suicide(I18n.T("[] is slitting [] throat with \\the []! It looks like [] trying to commit suicide.", user, user.p_their(), this, user.p_theyre())),
            span_suicide(I18n.T("[] is slitting [] stomach open with \\the []! It looks like [] trying to commit seppuku.", user, user.p_their(), this, user.p_theyre()))));
        await DmTask.CompletedTask;
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/weaponry/melee/knives.dm:45
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_eyestab)));
        set_butchering();
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        make_stabby();
        return dot;
    }

    ///Adds the butchering component, used to override stats for special cases
    // tg: code/game/objects/items/weaponry/melee/knives.dm:54
    public virtual void set_butchering()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering), Dm.named("speed", 9 * SECONDS - force * 2), Dm.named("effectiveness", 100f), Dm.named("bonus_modifier", force - 10)));
        //bonus chance increases depending on force
    }

    ///Adds alt sharpness component, used for overrides
    // tg: code/game/objects/items/weaponry/melee/knives.dm:63
    public virtual void make_stabby()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple));
    }
}

// tg: code/game/objects/items/weaponry/melee/knives.dm:119
public partial class obj_item_knife_butcher : obj_item_knife
{
    // tg: code/game/objects/items/weaponry/melee/knives.dm:129
    public override void make_stabby()
    {
    }
}

// tg: code/game/objects/items/weaponry/melee/knives.dm:132
public partial class obj_item_knife_hunting : obj_item_knife
{
    // tg: code/game/objects/items/weaponry/melee/knives.dm:141
    public override void set_butchering()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering), Dm.named("speed", 9 * SECONDS - force * 2), Dm.named("effectiveness", 100f), Dm.named("bonus_modifier", force + 10)));
    }

    // tg: code/game/objects/items/weaponry/melee/knives.dm:148
    public override void make_stabby()
    {
    }
}

// tg: code/game/objects/items/weaponry/melee/knives.dm:151
public partial class obj_item_knife_combat : obj_item_knife
{
    // tg: code/game/objects/items/weaponry/melee/knives.dm:171
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_knockoff), 90f, Dm.list(BODY_ZONE_PRECISE_MOUTH), slot_flags)); //90% to knock off when wearing a mask
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/knives.dm:175
    public override void make_stabby()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple, -5f));
    }

    // tg: code/game/objects/items/weaponry/melee/knives.dm:178
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (user.get_item_by_slot(ITEM_SLOT_MASK) == this && !Dm.truthy(user.has_status_effect(typeof(datum_status_effect_choke))) && prob(20))
        {
            await (user as mob_living).apply_damage(5, BRUTE, BODY_ZONE_HEAD);
            playsound(user, Dm.resource("sound/items/weapons/slice.ogg"), 50, TRUE);
            user.visible_message(span_danger(Dm.text("[] accidentally cuts []self while pulling [] out of [] teeth! What a doofus!", user, user.p_them(), this, user.p_them())), span_userdanger(Dm.text("You accidentally cut your mouth with []!", this)));
        }
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/knives.dm:185
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (HAS_TRAIT(user, TRAIT_CLUMSY) && prob(20))
        {
            if (user.get_item_by_slot(ITEM_SLOT_MASK) == this)
            {
                await (user as mob_living).apply_status_effect(typeof(datum_status_effect_choke), this);
                user.visible_message(span_danger(Dm.text("[] accidentally swallows []!", user, this)));
                playsound(user, Dm.resource("sound/items/eatfood.ogg"), 100, TRUE);
            }
        }
    }
}

// tg: code/game/objects/items/weaponry/melee/knives.dm:238
public partial class obj_item_knife_shiv : obj_item_knife
{
    // tg: code/game/objects/items/weaponry/melee/knives.dm:256
    public override void make_stabby()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple, -3f));
    }
}

// Gaps J6 (D298): the cargo technician's box cutter.
// tg: code/game/objects/items/weaponry/melee/knives.dm:417
public partial class obj_item_boxcutter : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/knives.dm:439
    public override object get_all_tool_behaviours() => Dm.list(TOOL_KNIFE);

    // tg: code/game/objects/items/weaponry/melee/knives.dm:442
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering), Dm.named("speed", 7 * SECONDS), Dm.named("effectiveness", 100)));

        Waiting.NoSleep(AddComponent(typeof(datum_component_transforming),
            Dm.named("start_transformed", start_extended),
            Dm.named("force_on", 10),
            Dm.named("throwforce_on", 4),
            Dm.named("throw_speed_on", throw_speed),
            Dm.named("sharpness_on", SHARP_EDGED),
            Dm.named("hitsound_on", Dm.resource("sound/items/weapons/bladeslice.ogg")),
            Dm.named("w_class_on", WEIGHT_CLASS_NORMAL),
            Dm.named("attack_verb_continuous_on", Dm.list("cuts", "stabs", "slashes")),
            Dm.named("attack_verb_simple_on", Dm.list("cut", "stab", "slash"))));

        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/knives.dm:465
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        playsound(this, Dm.resource("sound/items/boxcutter_activate.ogg"), 50);
        tool_behaviour = Dm.truthy(active) ? TOOL_KNIFE : null;
        if (Dm.truthy(active))
            Waiting.NoSleep(AddElement(typeof(datum_element_cuffsnapping), snap_time_weak_handcuffs, snap_time_strong_handcuffs));
        else
            RemoveElement(typeof(datum_element_cuffsnapping), snap_time_weak_handcuffs, snap_time_strong_handcuffs);
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }
}

// Syndicate G4c (D316): the switchblade (the monkey gun case holds an extended one).
// tg: code/game/objects/items/weaponry/melee/knives.dm:349
public partial class obj_item_switchblade : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/knives.dm:374
    public override object get_all_tool_behaviours() => Dm.list(TOOL_KNIFE);

    // tg: code/game/objects/items/weaponry/melee/knives.dm:377
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering), Dm.named("speed", 7 * SECONDS), Dm.named("effectiveness", 100)));

        Waiting.NoSleep(AddComponent(typeof(datum_component_transforming),
            Dm.named("start_transformed", start_extended),
            Dm.named("force_on", 20),
            Dm.named("throwforce_on", 23),
            Dm.named("throw_speed_on", throw_speed),
            Dm.named("sharpness_on", SHARP_EDGED),
            Dm.named("hitsound_on", Dm.resource("sound/items/weapons/bladeslice.ogg")),
            Dm.named("w_class_on", WEIGHT_CLASS_NORMAL),
            Dm.named("attack_verb_continuous_on", Dm.list("slashes", "slices", "tears", "lacerates", "rips", "dices", "cuts")),
            Dm.named("attack_verb_simple_on", Dm.list("slash", "slice", "tear", "lacerate", "rip", "dice", "cut"))));

        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Waiting.NoSleep(AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple, -5, TRAIT_TRANSFORM_ACTIVE));

        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/knives.dm:404
    [SignalHandler]
    public virtual void on_transform(obj_item source = null, mob user = null, object active = null)
    {
        tool_behaviour = Dm.truthy(active) ? TOOL_KNIFE : null;
    }

    // tg: code/game/objects/items/weaponry/melee/knives.dm:409
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is slitting [] own throat with []! It looks like [] trying to commit suicide!", user, user.p_their(), this, user.p_theyre())));
        return BRUTELOSS;
    }
}

// tg: code/game/objects/items/weaponry/melee/knives.dm:321
public partial class obj_item_knife_shiv_carrot : obj_item_knife_shiv
{
    // tg: code/game/objects/items/weaponry/melee/knives.dm:329
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] forcefully drives \\the [] into [] eye! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        await DmTask.CompletedTask;
        return BRUTELOSS;
    }
}
