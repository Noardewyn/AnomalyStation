// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/fireaxe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 5 (windows, grilles, the fire axe): the fire axe (two handed: 5 force in one hand, 24 wielded; wielded, one hit
// destroys a window or a grille) and its subtypes, which only change vars; its suicide (gaps WP1, D333). The var block is
// generated (D111).

// tg: code/game/objects/items/weaponry/melee/fireaxe.dm:1  GLOBAL_DATUM(bridge_axe, /obj/item/fireaxe)
public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/weaponry/melee/fireaxe.dm:1  GLOBAL_DATUM(bridge_axe, /obj/item/fireaxe)
    public obj_item_fireaxe bridge_axe;

    // tg: code/game/objects/items/weaponry/melee/fireaxe.dm:1  (GLOBAL_UNMANAGED(bridge_axe), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalbridge_axe()
    {
        return;
    }
}

/*
 * Fireaxe
 */
// tg: code/game/objects/items/weaponry/melee/fireaxe.dm:6
public partial class obj_item_fireaxe : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/fireaxe.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (GLOB.bridge_axe == null && istype(get_area(this), typeof(area_station_command)))
            GLOB.bridge_axe = this;

        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering), Dm.named("speed", 10 * SECONDS), Dm.named("effectiveness", 80f), Dm.named("bonus_modifier", 0f), Dm.named("butcher_sound", hitsound)));
        //axes are not known for being precision butchering tools
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("force_unwielded", force_unwielded), Dm.named("force_wielded", force_wielded), Dm.named("icon_wielded", Dm.text("[]1", base_icon_state))));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/fireaxe.dm:52
    public override object Destroy(float? force = null)
    {
        if (GLOB.bridge_axe == this)
            GLOB.bridge_axe = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/weaponry/melee/fireaxe.dm:57
    public override object get_demolition_modifier(obj target = null)
    {
        return HAS_TRAIT(this, TRAIT_WIELDED) ? demolition_mod : 0.8f;
    }

    // tg: code/game/objects/items/weaponry/melee/fireaxe.dm:60
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]0", base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/weaponry/melee/fireaxe.dm:64
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] axes []self from head to toe! It looks like [] trying to commit suicide!", user, user.p_them(), user.p_theyre())));
        await DmTask.CompletedTask;
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/weaponry/melee/fireaxe.dm:68
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!HAS_TRAIT(this, TRAIT_WIELDED)) //destroys windows and grilles in one hit
            return;
        if (Dm.truthy(Dm.bitand(target.resistance_flags, INDESTRUCTIBLE)))
            return;
        if (QDELETED(target))
            return;
        if (istype(target, typeof(obj_structure_window)) || istype(target, typeof(obj_structure_grille)))
            await target.atom_destruction("fireaxe");
    }
}
