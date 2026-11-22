// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/glasses/hud.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the chameleon HUD's action (chameleon clothing, antagonists); the suicide act came with gaps CL1 (D333). The
// HUD glasses give their wearer the HUD trait (clothing traits); the trait shows the data HUD to the mob (mob.dm hud_trait_enabled,
// datums/hud.dm), and toggle_hud_display, the EMP and the emag scramble it (M8 slice F, D185).

// tg: code/modules/clothing/glasses/hud.dm:1
public partial class obj_item_clothing_glasses_hud : obj_item_clothing_glasses
{
    // tg: code/modules/clothing/glasses/hud.dm:11
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        obj_flags = Dm.num(Dm.bitor(obj_flags, EMAGGED));
        desc = Dm.text("[] The display is flickering slightly.", desc);
        return dot;
    }

    // tg: code/modules/clothing/glasses/hud.dm:18
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.num(Dm.bitor(obj_flags, EMAGGED));
        balloon_alert(user, I18n.T("display scrambled"));
        desc = Dm.text("[] The display is flickering slightly.", desc);
        return TRUE;
    }

    // tg: code/modules/clothing/glasses/hud.dm:40
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        display_active = TRUE;
    }

    // tg: code/modules/clothing/glasses/hud.dm:44
    public virtual void toggle_hud_display(mob_living_carbon eye_owner = null)
    {
        if (Dm.truthy(display_active))
        {
            display_active = FALSE;
            foreach (object hud_trait in Dm.each(clothing_traits))
            {
                // REMOVE_CLOTHING_TRAIT(eye_owner, hud_trait) (__DEFINES/clothing.dm:41), expanded inline (rules 11.1 (d): it reads src)
                REMOVE_TRAIT(eye_owner, hud_trait, Dm.text("[]_[]", CLOTHING_TRAIT, REF(this)));
            }
            balloon_alert(eye_owner, I18n.T("hud disabled"));
            return;
        }

        display_active = TRUE;
        foreach (object hud_trait in Dm.each(clothing_traits))
        {
            // ADD_CLOTHING_TRAIT(eye_owner, hud_trait) (__DEFINES/traits/_traits.dm), expanded inline (rules 11.1 (d): it reads src)
            ADD_TRAIT(eye_owner, hud_trait, Dm.text("[]_[]", CLOTHING_TRAIT, REF(this)));
        }
        balloon_alert(eye_owner, I18n.T("hud enabled"));
    }

    // tg: code/modules/clothing/glasses/hud.dm:26
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (Dm.truthy(user.is_blind()))
            return SHAME;
        mob_living living_user = user;
        user.visible_message(span_suicide(I18n.T("[] looks through [] and looks overwhelmed with the information! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        if (Dm.num(living_user.get_organ_loss(ORGAN_SLOT_BRAIN)) >= BRAIN_DAMAGE_SEVERE)
        {
            // var/mob/thing = pick((/mob in view()) - user)
            DmList others = Dm.list();
            foreach (mob seen in Dm.each<mob>(view()))
                Dm.append(ref others, seen);
            Dm.remove(ref others, user);
            mob thing = length(others) > 0 ? pick(others) as mob : null;
            if (thing != null)
            {
                await user.say(I18n.T("VALID MAN IS WANTER, ARREST HE!!"));
                await user.pointed(thing);
            }
            else
                await user.say(I18n.T("WHY IS THERE A BAR ON MY HEAD?!!"));
        }
        return OXYLOSS;
    }
}

// tg: code/modules/clothing/glasses/hud.dm:71
public partial class obj_item_clothing_glasses_hud_health_night : obj_item_clothing_glasses_hud_health
{
    // tg: code/modules/clothing/glasses/hud.dm:83
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(length(color_cutoffs)) ? Dm.initial(this, (obj_item_clothing_glasses_hud_health_night p) => p.icon_state) : "night_off";
        return dot;
    }
}

// tg: code/modules/clothing/glasses/hud.dm:99
public partial class obj_item_clothing_glasses_hud_health_sunglasses : obj_item_clothing_glasses_hud_health
{
    // tg: code/modules/clothing/glasses/hud.dm:110
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // tg: code/modules/clothing/glasses/hud.dm:111  var/static/list/slapcraft_recipe_list (a proc-level static, rules 10.6)
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), TgState.Current.obj_item_clothing_glasses_hud_health_sunglasses__Initialize__slapcraft_recipe_list));
        return dot;
    }
}

// tg: code/modules/clothing/glasses/hud.dm:127
public partial class obj_item_clothing_glasses_hud_diagnostic_night : obj_item_clothing_glasses_hud_diagnostic
{
    // tg: code/modules/clothing/glasses/hud.dm:139
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(length(color_cutoffs)) ? Dm.initial(this, (obj_item_clothing_glasses_hud_diagnostic_night p) => p.icon_state) : "night_off";
        return dot;
    }
}

// tg: code/modules/clothing/glasses/hud.dm:144
public partial class obj_item_clothing_glasses_hud_diagnostic_sunglasses : obj_item_clothing_glasses_hud_diagnostic
{
    // tg: code/modules/clothing/glasses/hud.dm:154
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // tg: code/modules/clothing/glasses/hud.dm:155  var/static/list/slapcraft_recipe_list (a proc-level static, rules 10.6)
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), TgState.Current.obj_item_clothing_glasses_hud_diagnostic_sunglasses__Initialize__slapcraft_recipe_list));
        return dot;
    }
}

// tg: code/modules/clothing/glasses/hud.dm:177
public partial class obj_item_clothing_glasses_hud_security_sunglasses_eyepatch : obj_item_clothing_glasses_hud_security_sunglasses
{
    // tg: code/modules/clothing/glasses/hud.dm:183
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        icon_state = Dm.eq(icon_state, base_icon_state) ? Dm.text("[]_flipped", base_icon_state) : (string)base_icon_state;
        await user.update_worn_glasses();
        return dot;
    }
}

// tg: code/modules/clothing/glasses/hud.dm:189
public partial class obj_item_clothing_glasses_hud_security_sunglasses : obj_item_clothing_glasses_hud_security
{
    // tg: code/modules/clothing/glasses/hud.dm:200
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // tg: code/modules/clothing/glasses/hud.dm:201  var/static/list/slapcraft_recipe_list (a proc-level static, rules 10.6)
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), TgState.Current.obj_item_clothing_glasses_hud_security_sunglasses__Initialize__slapcraft_recipe_list));
        return dot;
    }
}

// tg: code/modules/clothing/glasses/hud.dm:210
public partial class obj_item_clothing_glasses_hud_security_night : obj_item_clothing_glasses_hud_security
{
    // tg: code/modules/clothing/glasses/hud.dm:221
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(length(color_cutoffs)) ? Dm.initial(this, (obj_item_clothing_glasses_hud_security_night p) => p.icon_state) : "night_off";
        return dot;
    }
}

// tg: code/modules/clothing/glasses/hud.dm:248
public partial class obj_item_clothing_glasses_hud_toggle : obj_item_clothing_glasses_hud
{
    // tg: code/modules/clothing/glasses/hud.dm:253
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!ishuman(user))
            return null;
        mob_living_carbon_human wearer = (mob_living_carbon_human)user;
        if (wearer.glasses != this)
            return null;

        if (Dm.@in(TRAIT_MEDICAL_HUD, clothing_traits))
            detach_clothing_traits(TRAIT_MEDICAL_HUD);
        else if (Dm.@in(TRAIT_SECURITY_HUD, clothing_traits))
        {
            detach_clothing_traits(TRAIT_MEDICAL_HUD);
            attach_clothing_traits(TRAIT_SECURITY_HUD);
        }
        else
        {
            detach_clothing_traits(TRAIT_MEDICAL_HUD);
            attach_clothing_traits(TRAIT_SECURITY_HUD);
        }
        return null;
    }
}

// tg: code/modules/clothing/glasses/hud.dm:273
public partial class obj_item_clothing_glasses_hud_toggle_thermal : obj_item_clothing_glasses_hud_toggle
{
    // tg: code/modules/clothing/glasses/hud.dm:281
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        detach_clothing_traits(current_hud);
        if (Dm.eq(current_hud, TRAIT_MEDICAL_HUD))
        {
            current_hud = TRAIT_MESON_VISION;
            icon_state = "meson";
            color_cutoffs = Dm.list(5, 15, 5);
            change_glass_color(typeof(datum_client_colour_glass_colour_green));
        }
        else if (Dm.eq(current_hud, TRAIT_MESON_VISION))
        {
            current_hud = TRAIT_SECURITY_HUD;
            icon_state = "thermal";
            color_cutoffs = Dm.list(25, 8, 5);
            change_glass_color(typeof(datum_client_colour_glass_colour_red));
        }
        else
        {
            current_hud = TRAIT_MEDICAL_HUD;
            icon_state = "purple";
            color_cutoffs = Dm.list(15, 0, 25);
            change_glass_color(typeof(datum_client_colour_glass_colour_purple));
        }
        attach_clothing_traits(current_hud);
        await user.update_worn_glasses();
        return dot;
    }

    // tg: code/modules/clothing/glasses/hud.dm:303
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        await thermal_overload();
        return dot;
    }
}

// The static lists of the HUD glasses (rules 10.6: proc-level statics live in TgState).
public sealed partial class TgState
{
    // tg: code/modules/clothing/glasses/hud.dm:111  var/static/list/slapcraft_recipe_list = list(/datum/crafting_recipe/hudsunmedremoval)
    internal DmList obj_item_clothing_glasses_hud_health_sunglasses__Initialize__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_hudsunmedremoval));

    // tg: code/modules/clothing/glasses/hud.dm:155  var/static/list/slapcraft_recipe_list = list(/datum/crafting_recipe/hudsundiagremoval)
    internal DmList obj_item_clothing_glasses_hud_diagnostic_sunglasses__Initialize__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_hudsundiagremoval));

    // tg: code/modules/clothing/glasses/hud.dm:201  var/static/list/slapcraft_recipe_list = list(/datum/crafting_recipe/hudsunsecremoval)
    internal DmList obj_item_clothing_glasses_hud_security_sunglasses__Initialize__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_hudsunsecremoval));
}
