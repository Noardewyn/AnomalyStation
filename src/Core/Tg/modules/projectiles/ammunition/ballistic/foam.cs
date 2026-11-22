// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/ammunition/ballistic/foam.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1a, D316: the Donksoft guns of G2 load these darts; inert, their materials were never set up, which broke
// the magazine's material count when a dart left it). The var block is generated (D111).

// tg: code/modules/projectiles/ammunition/ballistic/foam.dm:1
public partial class obj_item_ammo_casing_foam_dart : obj_item_ammo_casing
{
    // tg: code/modules/projectiles/ammunition/ballistic/foam.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_caseless), TRUE));
        return dot;
    }

    // tg: code/modules/projectiles/ammunition/ballistic/foam.dm:22
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(modified))
        {
            icon_state = Dm.text("[]_empty", base_icon_state);
            if (loaded_projectile != null)
                loaded_projectile.icon_state = Dm.text("[]_empty_proj", loaded_projectile.base_icon_state);
            return dot;
        }
        icon_state = Dm.text("[]", base_icon_state);
        if (loaded_projectile != null)
            loaded_projectile.icon_state = Dm.text("[]_proj", loaded_projectile.base_icon_state);
        return dot;
    }

    // tg: code/modules/projectiles/ammunition/ballistic/foam.dm:31
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = I18n.T("It's Donk or Don't! []", Dm.truthy(modified) ? I18n.T("... Although, this one doesn't look too safe.") : I18n.T("Ages 8 and up."));
        return dot;
    }

    // tg: code/modules/projectiles/ammunition/ballistic/foam.dm:35
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList ?? Dm.list();
        if (!HAS_TRAIT(this, TRAIT_DART_HAS_INSERT))
        {
            TgState.Current.obj_item_ammo_casing_foam_dart__insertable_items_hint ??= Dm.list(typeof(obj_item_pen));
            DmList type_initial_names = Dm.list();
            foreach (Type type in Dm.each<Type>(TgState.Current.obj_item_ammo_casing_foam_dart__insertable_items_hint))
                type_initial_names.Add(Dm.text("\\a []", Dm.initial(type, (obj_item i) => i.name)));
            dot.Add(span_notice(I18n.T("[] insert a small item[].",
                Dm.truthy(modified) ? I18n.T("You can") : I18n.T("If you removed the safety cap with a screwdriver, you could"),
                Dm.truthy(length(type_initial_names)) ? I18n.T(", such as []", english_list(type_initial_names, and_text: "or ", final_comma_text: ", ")) : "")));
        }
        return dot;
    }

    // tg: code/modules/projectiles/ammunition/ballistic/foam.dm:45
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(modified))
            return NONE;
        var dart = (obj_projectile_bullet_foam_dart)loaded_projectile;
        modified = TRUE;
        dart.modified = TRUE;
        dart.damage_type = BRUTE;
        dart.set_embed(null); // Cap is what makes them sticky
        to_chat(user, span_notice(I18n.T("You pop the safety cap off [].", this)));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/projectiles/ammunition/ballistic/foam.dm:14  var/static/list/insertable_items_hint
    internal DmList obj_item_ammo_casing_foam_dart__insertable_items_hint;
}
