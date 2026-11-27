// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/bscrystal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the crystal as an ore (a mined rock drops it into the satchel): it blinks whoever crushes it or is hit by it,
// and it is a raptor's food (raptor_food.dm).

// tg: code/game/objects/items/stacks/bscrystal.dm:3
public partial class obj_item_stack_ore_bluespace_crystal : obj_item_stack_ore
{
    // tg: code/game/objects/items/stacks/bscrystal.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pixel_x = rand(-5, 5);
        pixel_y = rand(-5, 5);
        Waiting.NoSleep(AddElement(typeof(datum_element_raptor_food), Dm.named("ability_modifier", 0.05f), Dm.named("attack_modifier", 0.5f), Dm.named("color_chances", string_list(Dm.list(Dm.kv(typeof(datum_raptor_color_black), 1))))));
        return dot;
    }

    // tg: code/game/objects/items/stacks/bscrystal.dm:37
    public override object get_part_rating() => 1;

    // tg: code/game/objects/items/stacks/bscrystal.dm:40
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user._listen_lookup?[COMSIG_MOB_CRUSHED_BLUESPACE_CRYSTAL]))
            user._SendSignal(COMSIG_MOB_CRUSHED_BLUESPACE_CRYSTAL, Dm.list(user, this));
        user.visible_message(span_warning(I18n.T("[] crushes []!", user, this)), span_danger(I18n.T("You crush []!", this)));
        Dm.@new<obj_effect_particle_effect_sparks>(loc);
        playsound(loc, SFX_PORTAL_ENTER, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        blink_mob(user as mob_living);
        await use(1);
        return null;
    }

    // tg: code/game/objects/items/stacks/bscrystal.dm:48
    public virtual void blink_mob(mob_living L = null)
    {
        Waiting.Detach(do_teleport(L, get_turf(L), blink_range, asoundin: Dm.resource("sound/effects/phasein.ogg"), channel: TELEPORT_CHANNEL_BLUESPACE));
    }

    // tg: code/game/objects/items/stacks/bscrystal.dm:51
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        if (!Dm.truthy(base.throw_impact(hit_atom, throwingdatum, __extra))) // not caught in mid-air
        {
            visible_message(span_notice(I18n.T("[] fizzles and disappears upon impact!", this)));
            turf T = get_turf(hit_atom);
            Dm.@new<obj_effect_particle_effect_sparks>(T);
            playsound(loc, SFX_PORTAL_ENTER, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
            if (isliving(hit_atom))
                blink_mob(hit_atom as mob_living);
            Waiting.Detach(use(1));
        }
        return null;
    }

    // tg: code/game/objects/items/stacks/bscrystal.dm:61
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        return await interact(user);
    }
}

// Gaps SK1 (D333): the bluespace polycrystal (a crystal broken off by hand, its recipe list).
// tg: code/game/objects/items/stacks/bscrystal.dm:74
public partial class obj_item_stack_sheet_bluespace_crystal : obj_item_stack_sheet
{
    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/game/objects/items/stacks/bscrystal.dm:94
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!ReferenceEquals(user.get_inactive_held_item(), this))
            return await base.attack_hand(user, modifiers, __extra);

        if (Dm.truthy(is_zero_amount(delete_if_zero: TRUE)))
            return null;

        var BC = Dm.@new(crystal_type, this) as obj_item;
        await user.put_in_hands(BC);
        await use(1);
        if (!Dm.truthy(amount))
            to_chat(user, span_notice(I18n.T("You break the final crystal off.")));
        else
            to_chat(user, span_notice(I18n.T("You break off a crystal.")));
        return null;
    }

    // tg: code/game/objects/items/stacks/bscrystal.dm:116
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.bluespace_crystal_recipes);
        return dot;
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/bscrystal.dm:112  GLOBAL_LIST_INIT(bluespace_crystal_recipes, list(...))
    public DmList bluespace_crystal_recipes;

    // tg: code/game/objects/items/stacks/bscrystal.dm:112  (GLOBAL_MANAGED(bluespace_crystal_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbluespace_crystal_recipes()
    {
        bluespace_crystal_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("bluespace crystal tile", typeof(obj_item_stack_tile_mineral_bluespace), 1, 4, 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)));
        Dm.append(ref gvars_datum_init_order, "bluespace_crystal_recipes");
    }
}
