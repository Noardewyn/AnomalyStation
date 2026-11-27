// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/prismatic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the prismatic extracts, endless paintbrushes for floors and walls in their colour; the grey one washes the paint
// off, the rainbow one paints in a colour its user picks. Kept as tg has it: the paint is a colour filter, which leaves the colour
// var alone, so the grey one washes only a plain colour and paints grey over the extracts' paint. The var blocks are generated
// (D111).

/*
Prismatic extracts:
	Becomes an infinite-use paintbrush.
*/
// tg: code/modules/research/xenobiology/crossbreeding/prismatic.dm:6
public partial class obj_item_slimecross_prismatic : obj_item_slimecross
{
    // tg: code/modules/research/xenobiology/crossbreeding/prismatic.dm:13
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isturf(interacting_with) || isspaceturf(interacting_with))
            return NONE;
        await user.do_attack_animation(interacting_with);
        await interacting_with.add_atom_colour(color_transition_filter(paintcolor, SATURATION_OVERRIDE), WASHABLE_COLOUR_PRIORITY);
        playsound(interacting_with, "sound/effects/slosh.ogg", 20, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/prismatic.dm:22
public partial class obj_item_slimecross_prismatic_grey : obj_item_slimecross_prismatic
{
    // tg: code/modules/research/xenobiology/crossbreeding/prismatic.dm:25
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (isturf(interacting_with) && !Dm.eq(interacting_with.color, Dm.initial(interacting_with, "color")))
        {
            await user.do_attack_animation(interacting_with);
            await interacting_with.remove_atom_colour(WASHABLE_COLOUR_PRIORITY);
            playsound(interacting_with, "sound/effects/slosh.ogg", 20, TRUE);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.interact_with_atom(interacting_with, user, modifiers);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/prismatic.dm:114
public partial class obj_item_slimecross_prismatic_rainbow : obj_item_slimecross_prismatic
{
    // tg: code/modules/research/xenobiology/crossbreeding/prismatic.dm:117
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object newcolor = await tgui_color_picker(user, I18n.T("Choose the slime color:"), I18n.T("Color change"), paintcolor);
        if (user.get_active_held_item() != this || IS_UNCONSCIOUS_OR_CRIT(user) || HAS_TRAIT(user, TRAIT_HANDS_BLOCKED))
            return null;
        if (!Dm.truthy(newcolor))
            return null;
        paintcolor = Dm.str(newcolor);
        return null;
    }
}
