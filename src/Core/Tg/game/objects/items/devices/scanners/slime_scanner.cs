// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/scanners/slime_scanner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The slime scanner: a slime clicked shows its colour and age, nutrition and hunger, charge, health,
// possible mutations, instability, extra cores, growth and core mutation (the global `slime_scan`, which the xenobiology console's
// scan action shares). The var block is generated (D111).

// tg: code/game/objects/items/devices/scanners/slime_scanner.dm:1
public partial class obj_item_slime_scanner : obj_item
{
    // tg: code/game/objects/items/devices/scanners/slime_scanner.dm:16
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return NONE;
        if (!Dm.truthy(user.can_read(this)) || Dm.truthy(user.is_blind()))
            return ITEM_INTERACT_BLOCKING;
        if (!isslime(interacting_with))
        {
            to_chat(user, span_warning(I18n.T("This device can only scan slimes!")));
            return ITEM_INTERACT_BLOCKING;
        }
        mob_living_basic_slime scanned_slime = (mob_living_basic_slime)interacting_with;
        playsound(this, SFX_INDUSTRIAL_SCAN, 20, TRUE, -2, TRUE, FALSE);
        slime_scan(scanned_slime, user);
        return ITEM_INTERACT_SUCCESS;
    }
}

public static partial class GlobalProcs
{
    // tg: code/game/objects/items/devices/scanners/slime_scanner.dm:29
    public static void slime_scan(mob_living_basic_slime scanned_slime = null, mob_living user = null)
    {
        string to_render = I18n.T("<b>Slime scan results:</b>\n[]\nNutrition: []/[]",
            span_notice(I18n.T("[] [] slime", scanned_slime.slime_type.colour, scanned_slime.life_stage)), scanned_slime.nutrition, SLIME_MAX_NUTRITION);

        if (scanned_slime.nutrition < SLIME_STARVE_NUTRITION)
            to_render += Dm.text("\n[]", span_warning(I18n.T("Warning: slime is starving!")));
        else if (scanned_slime.nutrition < SLIME_HUNGER_NUTRITION)
            to_render += Dm.text("\n[]", span_warning(I18n.T("Warning: slime is hungry")));

        to_render += I18n.T("\nElectric charge strength: []\nHealth: []%", scanned_slime.powerlevel, round(Dm.num(scanned_slime.health) / Dm.num(scanned_slime.maxHealth), 0.01f) * 100);

        to_render += I18n.T("\nPossible mutation[]: ", scanned_slime.slime_type.mutations.len > 1 ? "s" : "");
        DmList mutation_text = Dm.list();
        foreach (object key in Dm.each(scanned_slime.slime_type.mutations))
            Dm.append(ref mutation_text, Dm.initial((Type)key, (datum_slime_type p) => p.colour));

        if (!Dm.truthy(mutation_text.len))
            to_render += I18n.T(" None detected.");

        to_render += Dm.text("[]", mutation_text.Join(", "));
        to_render += I18n.T("\nGenetic instability: [] % chance of mutation attempt on splitting.", scanned_slime.mutation_chance);

        if (scanned_slime.cores > 1)
            to_render += I18n.T("\nMultiple cores detected");
        to_render += I18n.T("\nGrowth progress: []/[]", scanned_slime.amount_grown, SLIME_EVOLUTION_THRESHOLD);

        if (Dm.truthy(scanned_slime.crossbreed_modification))
            to_render += Dm.text("\n[]\n[]", span_notice(I18n.T("Core mutation in progress: []", scanned_slime.crossbreed_modification)),
                span_notice(I18n.T("Progress in core mutation: [] / []", scanned_slime.applied_crossbreed_amount, SLIME_EXTRACT_CROSSING_REQUIRED)));

        to_chat(user, boxed_message(to_render)); // jointext(to_render,""): to_render is text here, which BYOND's jointext gives back as it is (the shim's takes lists only)
    }
}
