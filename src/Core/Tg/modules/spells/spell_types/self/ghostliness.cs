// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/self/ghostliness.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3b (D349): the whole file: Forsake Body, which turns a human into the ghost species and leaves a soulless husk behind.
// The var block is generated (D111).

// tg: code/modules/spells/spell_types/self/ghostliness.dm:2
public partial class datum_action_cooldown_spell_ghostliness : datum_action_cooldown_spell
{
    // tg: code/modules/spells/spell_types/self/ghostliness.dm:15
    public override async DmTask<object> can_cast_spell(float? feedback = null)
    {
        feedback ??= TRUE;
        object dot = await base.can_cast_spell(feedback);
        if (!Dm.truthy(dot))
            return FALSE;

        if (!Dm.truthy(await is_valid_target(owner)))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("no soul!"));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/spells/spell_types/self/ghostliness.dm:27
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        return ishuman(cast_on) && !HAS_TRAIT(owner, TRAIT_NO_SOUL) ? TRUE : FALSE;
    }

    // tg: code/modules/spells/spell_types/self/ghostliness.dm:30
    public override async DmTask<object> cast(atom cast_on_atom = null)
    {
        mob_living_carbon_human cast_on = cast_on_atom as mob_living_carbon_human;
        object dot = await base.cast(cast_on);

        if (HAS_TRAIT(cast_on, TRAIT_GHOSTLY_MOB))
            to_chat(cast_on, span_green(I18n.T("You begin to focus on loosening the bonds holding you to the material plane.")));
        else
            to_chat(cast_on, span_green(I18n.T("You begin to focus on your very being, drawing it out of its corporeal vessel...")));
        if (!Dm.truthy(await do_after(cast_on, 5 * SECONDS)))
        {
            if (HAS_TRAIT(cast_on, TRAIT_GHOSTLY_MOB))
                to_chat(cast_on, span_warning(I18n.T("Your focus is broken, and you feel your material bindings snap tight once more.")));
            else
                to_chat(cast_on, span_warning(I18n.T("Your focus is broken, and your soul snaps back into place.")));
            return dot;
        }
        if (HAS_TRAIT(cast_on, TRAIT_GHOSTLY_MOB))
        {
            to_chat(cast_on, span_green(I18n.T("You successfully loosen your bonds to the material plane, and can now slip partially out of it.")));
        }
        else
        {
            to_chat(cast_on, span_danger(I18n.T("As the last trailing filament of your essence ceases intersection with your body, your perspective abruptly snaps to your new, ghostly figure! Your former vessel falls to the ground, vacant and devoid of volition!")));
            mob_living_carbon_human soulless_husk = Dm.@new<mob_living_carbon_human>(cast_on.drop_location()) as mob_living_carbon_human;
            await soulless_husk.setDir(cast_on.dir);
            await cast_on.dna.copy_dna(soulless_husk.dna, ALL);
            soulless_husk.real_name = cast_on.real_name;
            await soulless_husk.updateappearance(icon_update: TRUE, mutcolor_update: TRUE, mutations_overlay_update: TRUE);
            await soulless_husk.domutcheck();
            ADD_TRAIT(soulless_husk, TRAIT_NO_SOUL, MAGIC_TRAIT);
            ADD_TRAIT(soulless_husk, TRAIT_FLOORED, MAGIC_TRAIT);
        }
        cast_on.set_species(typeof(datum_species_spirit_ghost));
        qdel(this);
        return dot;
    }
}
