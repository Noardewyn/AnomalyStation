// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/cult/shade.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the shade, a bound spirit a soulstone captures and releases. The var block is generated (D111).

// tg: code/modules/mob/living/basic/cult/shade.dm:1
public partial class mob_living_basic_shade : mob_living_basic
{
    /// The different flavors of goop shades can drop, depending on theme.
    // tg: code/modules/mob/living/basic/cult/shade.dm:36  var/static/list/remains_by_theme
    public static DmList remains_by_theme => TgState.Current.mob_living_basic_shade__remains_by_theme ??= Dm.list(
        Dm.kv(THEME_CULT, typeof(obj_item_ectoplasm_construct)),
        Dm.kv(THEME_HOLY, typeof(obj_item_ectoplasm_angelic)),
        Dm.kv(THEME_WIZARD, typeof(obj_item_ectoplasm_mystic)),
        Dm.kv(THEME_HERETIC, typeof(obj_item_ectoplasm_construct)));

    // tg: code/modules/mob/living/basic/cult/shade.dm:43
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        add_traits(Dm.list(TRAIT_HEALS_FROM_CULT_PYLONS, TRAIT_SPACEWALK, TRAIT_VENTCRAWLER_ALWAYS), INNATE_TRAIT);
        if (theme == null)
            return dot;
        icon_state = Dm.text("shade_[]", theme);
        object remains = remains_by_theme[theme];
        if (Dm.truthy(remains))
            Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), remains));
        return dot;
    }

    // tg: code/modules/mob/living/basic/cult/shade.dm:54
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (theme != null)
            icon_state = Dm.text("shade_[]", theme);
        icon_living = icon_state;
        return dot;
    }

    // tg: code/modules/mob/living/basic/cult/shade.dm:60
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        // tg: :60-61  SSblackbox.record_feedback("tally", "cult_shade_killed", 1): the statistics (the statistics database, not ported)
        if (death_message == Dm.initial(this, (mob_living_basic_shade p) => p.death_message))
            death_message = I18n.T("lets out a contented sigh as [] form unwinds.", p_their());
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/cult/shade.dm:67
    public override object can_suicide()
    {
        if (loc is obj_item_soulstone) //do not suicide inside the soulstone
            return FALSE;
        return base.can_suicide();
    }

    // tg: code/modules/mob/living/basic/cult/shade.dm:72
    public override void suicide_log(obj_item suicide_tool = null)
    {
        // tg: :72-73  SSblackbox.record_feedback("tally", "cult_shade_suicided", 1): the statistics (the statistics database, not ported)
        base.suicide_log(suicide_tool);
    }

    // tg: code/modules/mob/living/basic/cult/shade.dm:77
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_soulstone stone)
            return await base.item_interaction(user, tool, modifiers, __extra);

        await stone.capture_shade(this, user);
        return ITEM_INTERACT_SUCCESS;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_shade__remains_by_theme;
}
