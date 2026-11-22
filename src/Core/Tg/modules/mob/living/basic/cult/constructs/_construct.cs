// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/cult/constructs/_construct.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: holds the [DmPath] of a type that a translated GLOB list names (rules 2.3: the earliest .dme file that declares it), written by the W4.0 contracts; the file is translated with its module. X3: get_examine_name and examine.

// tg: code/modules/mob/living/basic/cult/constructs/_construct.dm:1
[DmPath("/mob/living/basic/construct")]
public partial class mob_living_basic_construct : mob_living_basic
{
    // tg: code/modules/mob/living/basic/cult/constructs/_construct.dm:56
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        throw_alert("bloodsense", typeof(atom_movable_screen_alert_bloodsense));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        object remains = ((DmList)Dm.list(Dm.kv(THEME_CULT, typeof(obj_item_ectoplasm_construct)), Dm.kv(THEME_HOLY, typeof(obj_item_ectoplasm_angelic)), Dm.kv(THEME_WIZARD, typeof(obj_item_ectoplasm_mystic)), Dm.kv(THEME_HERETIC, typeof(obj_item_ectoplasm_construct))))[theme];
        if (Dm.truthy(remains))
            Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), remains));
        if (Dm.truthy(smashes_walls))
            Dm.Deferred("code/modules/mob/living/basic/cult/constructs/_construct.dm:66", "Initialize: AddElement(/datum/element/wall_tearer), the wall tearing of a player construct (with the cult)");
        if (Dm.truthy(can_repair))
            Dm.Deferred("code/modules/mob/living/basic/cult/constructs/_construct.dm:67", "Initialize: the healing_touch component and the structure_repair element of artificers (with the cult)");
        add_traits(Dm.list(TRAIT_HEALS_FROM_CULT_PYLONS, TRAIT_SPACEWALK), INNATE_TRAIT);
        // grant_actions_by_list(construct_spells) and the button positions: the proteon has no spells; the other constructs (and their spells) are inert, with the cult

        if (Dm.truthy(icon_state))
            add_overlay(Dm.text("glow_[]_[]", icon_state, theme));
        return dot;
    }

    // tg: code/modules/mob/living/basic/cult/constructs/_construct.dm:110
    public override object get_examine_name(mob user = null)
    {
        string text_span = null;
        if (Dm.eq(theme, THEME_CULT))
            text_span = "cult";
        else if (Dm.eq(theme, THEME_WIZARD))
            text_span = "purple";
        else if (Dm.eq(theme, THEME_HOLY))
            text_span = "blue";

        if (text_span == null)
            return base.get_examine_name(user);

        return Dm.text("<span class='[]'>[]</span>", text_span, base.get_examine_name(user));
    }

    // tg: code/modules/mob/living/basic/cult/constructs/_construct.dm:125
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        object dot = Dm.list();
        if (health < maxHealth)
        {
            if (health >= maxHealth / 2)
                Dm.append(ref dot, span_warning(I18n.T("[] look[] slightly dented.", p_They(), p_s())));
            else
                Dm.append(ref dot, span_warning(span_bold(I18n.T("[] look[] severely dented!", p_They(), p_s()))));
        }

        return dot;
    }
}
