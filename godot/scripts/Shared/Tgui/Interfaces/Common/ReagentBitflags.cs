namespace Anomaly.Client.Shared.Tgui.Interfaces.Common;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Reagents/types.ts (gaps package P5): bitflagInfo, the
// reaction tags' icons, tooltips, categories and toggle acts that the Reagents and ChemDispenser windows show. Original copyright and
// license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ReagentBitflags
{
    public sealed record Info(string Flag, string Icon, string Tooltip, string Category, string Toggle);

    // tg: Reagents/types.ts:100  export const bitflagInfo
    public static readonly IReadOnlyList<Info> BitflagInfo = new Info[]
    {
        new("BRUTE", "gavel", "Produces a reagent that heals or deals brute damage.", "Affects", "toggle_tag_brute"),
        new("BURN", "burn", "Produces a reagent that heals or deals burn damage.", "Affects", "toggle_tag_burn"),
        new("TOXIN", "biohazard", "Produces a reagent that heals or deals toxin damage.", "Affects", "toggle_tag_toxin"),
        new("OXY", "wind", "Produces a reagent that heals or deals suffocation damage.", "Affects", "toggle_tag_oxy"),
        new("HEALING", "medkit", "Produces a healing reagent.", "Type", "toggle_tag_healing"),
        new("DAMAGING", "skull-crossbones", "Produces a damaging reagent.", "Type", "toggle_tag_damaging"),
        new("EXPLOSIVE", "bomb", "Produces a reagent that explodes or explodes on reaction.", "Type", "toggle_tag_explosive"),
        new("OTHER", "question", "Produces a reagent with some other side effect.", "Affects", "toggle_tag_other"),
        new("DANGEROUS", "exclamation-triangle", "Reaction may have a dangerous immediate effect.", "Difficulty", "toggle_tag_dangerous"),
        new("EASY", "chess-pawn", "Easy to perform reaction.", "Difficulty", "toggle_tag_easy"),
        new("MODERATE", "chess-knight", "Moderate difficulty reaction.", "Difficulty", "toggle_tag_moderate"),
        new("HARD", "chess-queen", "Hard to perform reaction.", "Difficulty", "toggle_tag_hard"),
        new("ORGAN", "brain", "Produces a reagent that heals or deals organ damage.", "Affects", "toggle_tag_organ"),
        new("DRINK", "cocktail", "Produces a drinkable reagent. Usually performed in the bar.", "Type", "toggle_tag_drink"),
        new("FOOD", "drumstick-bite", "Produces a food. Usually performed in the kitchen.", "Type", "toggle_tag_food"),
        new("SLIME", "microscope", "A reaction related to Xenobiology.", "Type", "toggle_tag_slime"),
        new("DRUG", "pills", "Produces an addictive reagent with positive and negative effects.", "Type", "toggle_tag_drug"),
        new("UNIQUE", "puzzle-piece", "A unique or special reaction.", "Type", "toggle_tag_unique"),
        new("CHEMICAL", "flask", "Produces a reagent which alters other reactions.", "Affects", "toggle_tag_chemical"),
        new("PLANT", "seedling", "Produces a reagent that can help or harm plants.", "Affects", "toggle_tag_plant"),
        new("COMPETITIVE", "recycle", "A reaction that competes with other reactions.", "Difficulty", "toggle_tag_competitive"),
        new("COMPONENT", "question", "Produces a reagent commonly used in other reactions.", "Type", "toggle_tag_component"),
        new("ACTIVE", "question", "Reaction has an active, immediate effect.", "Type", "toggle_tag_active"),
    };
}
