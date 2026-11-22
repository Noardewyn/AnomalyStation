// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/gym/punching_bag.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST4 (D333): the file whole. The punching bag: a punch flicks its swing with one of tg's hit sounds and drains a carbon's
// stamina (less in boxing gloves, more in heavy gravity, less with a spine implant or the strong trait); wrenched loose and pried
// apart with the right button. The athletics experience and the exercised status effect a punch gives came with gaps SL1 (D333).
// The var block is generated (D111).

// tg: code/game/objects/structures/gym/punching_bag.dm:1
public partial class obj_structure_punching_bag : obj_structure
{
    // tg: code/game/objects/structures/gym/punching_bag.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_bare_hands), Dm.named("lmb_text", I18n.T("Punch"))));

        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_tools), TgState.Current.obj_structure_punching_bag__Initialize__tool_behaviors));
        return dot;
    }

    // tg: code/game/objects/structures/gym/punching_bag.dm:41
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        flick(Dm.text("[]-punch", icon_state), this);
        playsound(loc, pick(TgState.Current.obj_structure_punching_bag__hit_sounds), 25, TRUE, -1);

        if (!iscarbon(user))
            return dot;
        mob_living_carbon boxer_carbon = (mob_living_carbon)user;

        bool is_heavy_gravity = Dm.num(boxer_carbon.has_gravity()) > STANDARD_GRAVITY;

        float stamina_exhaustion = 3;
        if (user is mob_living_carbon_human boxer)
        {
            if (boxer.get_item_by_slot(ITEM_SLOT_GLOVES) is obj_item_clothing_gloves_boxing)
                stamina_exhaustion = 2;
        }
        if (is_heavy_gravity)
            stamina_exhaustion *= 1.5f;

        if (boxer_carbon.get_organ_slot(ORGAN_SLOT_SPINE) is obj_item_organ_cyberimp_chest_spine potential_spine)
            stamina_exhaustion *= potential_spine.athletics_boost_multiplier;

        if (HAS_TRAIT(user, TRAIT_STRENGTH)) //The strong get reductions to stamina damage taken while exercising
            stamina_exhaustion *= 0.5f;

        await boxer_carbon.adjust_stamina_loss(stamina_exhaustion);
        user.mind?.adjust_experience(typeof(datum_skill_athletics), is_heavy_gravity ? 0.6f : 0.3f);
        await boxer_carbon.apply_status_effect(typeof(datum_status_effect_exercised));
        return dot;
    }

    // tg: code/game/objects/structures/gym/punching_bag.dm:73
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this);
        balloon_alert(user, Dm.truthy(anchored) ? I18n.T("unsecured") : I18n.T("secured"));
        anchored = Dm.truthy(anchored) ? FALSE : TRUE;
        return TRUE;
    }

    // tg: code/game/objects/structures/gym/punching_bag.dm:79
    public override async DmTask<object> crowbar_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("still secured!"));
            return FALSE;
        }
        tool.play_tool_sound(this);
        balloon_alert(user, I18n.T("deconstructing..."));
        if (!Dm.truthy(await do_after(user, 10 * SECONDS, target: this)))
            return FALSE;
        Dm.@new<obj_item_stack_sheet_iron>(get_turf(this), 2);
        Dm.@new<obj_item_stack_rods>(get_turf(this));
        Dm.@new<obj_item_pillow>(get_turf(this));
        qdel(this);
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/gym/punching_bag.dm:10  var/static/list/hit_sounds
    internal DmList obj_structure_punching_bag__hit_sounds = Dm.list(
        Dm.resource("sound/items/weapons/genhit1.ogg"),
        Dm.resource("sound/items/weapons/genhit2.ogg"),
        Dm.resource("sound/items/weapons/genhit3.ogg"),
        Dm.resource("sound/items/weapons/punch1.ogg"),
        Dm.resource("sound/items/weapons/punch2.ogg"),
        Dm.resource("sound/items/weapons/punch3.ogg"),
        Dm.resource("sound/items/weapons/punch4.ogg"));

    // tg: code/game/objects/structures/gym/punching_bag.dm:28  /obj/structure/punching_bag/Initialize's var/static/list/tool_behaviors
    internal DmList obj_structure_punching_bag__Initialize__tool_behaviors = Dm.list(
        Dm.kv(TOOL_CROWBAR, Dm.list(Dm.kv(SCREENTIP_CONTEXT_RMB, "Deconstruct"))),
        Dm.kv(TOOL_WRENCH, Dm.list(Dm.kv(SCREENTIP_CONTEXT_RMB, "Anchor"))));
}
