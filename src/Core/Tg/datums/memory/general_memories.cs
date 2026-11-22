// Translated from tgstation (snapshot 1d99bd7f): code/datums/memory/general_memories.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package G4 (D291): the whole file. The var blocks are generated (but good_food's food and witnessed_russian_roulette's result,
// which the generator typed as typepaths). Every name, start and mood is player-visible (the memory panel, the stories).

// tg: code/datums/memory/general_memories.dm:2
public partial class datum_memory_surgery : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:9
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "surgery_type")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        object surgery_type = Dm.arg(__extra, 4);
        this.surgery_type = surgery_type;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, surgery_type));
    }

    // tg: code/datums/memory/general_memories.dm:19
    public override object get_names()
    {
        return Dm.list(I18n.T("The [] of [] by [].", surgery_type, deuteragonist_name, protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:22
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] carefully performing [] on []", protagonist_name, surgery_type, deuteragonist_name),
            I18n.T("[] using a bone saw on []", protagonist_name, deuteragonist_name),
            I18n.T("[] being operated on by []", deuteragonist_name, protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:29
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] after finishing [].", protagonist_name, mood_verb, surgery_type),
            I18n.T("[] [] as a blood splatter lands on []'s face.", protagonist_name, mood_verb, protagonist_name),
            I18n.T("[] [] as the [] continues.", protagonist_name, mood_verb, surgery_type),
            I18n.T("[] [] as they pick apart [].", protagonist_name, mood_verb, deuteragonist_name),
            I18n.T("[] [] as they tear into [].", protagonist_name, mood_verb, deuteragonist_name));
    }
}

// tg: code/datums/memory/general_memories.dm:39
public partial class datum_memory_bomb_planted : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:44
    public override object get_names()
    {
        return Dm.list(I18n.T("The arming of [] by [].", antagonist_name, protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:47
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] pressing an ominous button, causing [] to begin beeping", protagonist_name, antagonist_name),
            I18n.T("[] slapping down []", protagonist_name, antagonist_name),
            I18n.T("[] being armed by []", antagonist_name, protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:54
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] and begins to walk away from it.", protagonist_name, mood_verb),
            I18n.T("[] [] as it begins to tick.", protagonist_name, mood_verb),
            I18n.T("[] [] with it winding down.", protagonist_name, mood_verb),
            I18n.T("beep... beep... [] [].", protagonist_name, mood_verb));
    }

    // tg: code/datums/memory/general_memories.dm:62
    public override object get_happy_moods()
    {
        return Dm.list(I18n.T("feels too cool to look at []", antagonist_name));
    }
}

// tg: code/datums/memory/general_memories.dm:74
public partial class datum_memory_high_five : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:81
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "high_five_type", "high_ten")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        object high_five_type = Dm.arg(__extra, 4);
        object high_ten = Dm.arg(__extra, 5);
        high_ten ??= FALSE;
        this.high_five_type = high_five_type;
        this.story_value = Dm.truthy(high_ten) ? STORY_VALUE_OKAY : STORY_VALUE_MEH;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, high_five_type, high_ten));
    }

    // tg: code/datums/memory/general_memories.dm:93
    public override object get_names()
    {
        return Dm.list(I18n.T("The [] between [] and [].", high_five_type, protagonist_name, deuteragonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:96
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] and [] having a legendary []", protagonist_name, deuteragonist_name, high_five_type),
            I18n.T("[] giving [] a []", protagonist_name, deuteragonist_name, high_five_type),
            I18n.T("[] and [] giving each other a []", protagonist_name, deuteragonist_name, high_five_type));
    }

    // tg: code/datums/memory/general_memories.dm:103
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] as the [] connects.", protagonist_name, mood_verb, high_five_type),
            I18n.T("[] [] at all the compatriotism going on.", protagonist_name, mood_verb),
            I18n.T("What a []! [] [].", high_five_type, protagonist_name, mood_verb),
            I18n.T("Wow! [] []!", protagonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:112
public partial class datum_memory_was_cyborged : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:117
    public override object get_names()
    {
        return Dm.list(I18n.T("The borging of [].", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:120
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] having their brain put into a robot", protagonist_name),
            I18n.T("[] getting turned into a bucket of bolts", protagonist_name));
    }
}

// tg: code/datums/memory/general_memories.dm:127
public partial class datum_memory_witnessed_death : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:132
    public override object get_names()
    {
        return Dm.list(I18n.T("The death of [].", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:135
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] having perished", protagonist_name),
            I18n.T("[] seizing up and falling limp, their eyes appearing dead and lifeless", protagonist_name),
            I18n.T("[]'s heart stopping", protagonist_name),
            I18n.T("the death of []", protagonist_name));
    }
}

// tg: code/datums/memory/general_memories.dm:144
public partial class datum_memory_witnessed_creampie : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:149
    public override object get_names()
    {
        return Dm.list(I18n.T("The creaming of [].", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:152
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[]'s face being covered in cream", protagonist_name),
            I18n.T("[] getting cream-pied", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:158
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] as the cream drips off their face", protagonist_name, mood_verb),
            I18n.T("[] [] because of their now expanded laundry task.", protagonist_name, mood_verb),
            I18n.T("[] [] as they lick off some of the pie", protagonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:166
public partial class datum_memory_witnessed_inking : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:171
    public override object get_names()
    {
        return Dm.list(I18n.T("The inking of [].", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:174
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[]'s face being covered in squid ink", protagonist_name),
            I18n.T("[] getting squid-inked", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:180
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] as ink drips off their face", protagonist_name, mood_verb),
            I18n.T("[] [] because of their now expanded laundry task.", protagonist_name, mood_verb),
            I18n.T("[] [] as they wipe the ink off their face.", protagonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:188
public partial class datum_memory_was_slipped : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:193
    public override object get_names()
    {
        return Dm.list(I18n.T("The slipping of [].", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:196
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] not being able to keep standing when faced with []", protagonist_name, antagonist_name),
            I18n.T("[] tumbling right over []", protagonist_name, antagonist_name),
            I18n.T("[] which took [] down a notch", antagonist_name, protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:203
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] as they crawl up from the ground.", protagonist_name, mood_verb),
            I18n.T("[] [] while on the ground.", protagonist_name, mood_verb));
    }

    // tg: code/datums/memory/general_memories.dm:209
    public override object get_sad_moods()
    {
        return Dm.list(I18n.T("doesn't even want to get up and looks depressed"));
    }

    // tg: code/datums/memory/general_memories.dm:212
    public override object build_story_character(object character = null)
    {
        // We can slip on turfs, so we should account for it
        if (isturf(character))
        {
            turf place = character as turf;
            return I18n.T("the [][] []", prob(50) ? I18n.T("perilous ") : "", pick(I18n.T("wet"), I18n.T("lubed"), I18n.T("slippery"), I18n.T("cold")), place.name);
        }

        return base.build_story_character(character);
    }
}

// tg: code/datums/memory/general_memories.dm:221
public partial class datum_memory_lost_spaghetti : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:226
    public override object get_names()
    {
        return Dm.list(I18n.T("[]'s spaghetti blunder.", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:229
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[]'s spaghetti pouring out of their pockets", protagonist_name),
            I18n.T("[]'s pockets not being able to contain their spaghetti", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:235
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] as the spaghetti poured out.", protagonist_name, mood_verb),
            I18n.T("[] [] as they try to pick up the scraps.", protagonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:242
public partial class datum_memory_kissed : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:249
    public override object get_names()
    {
        return Dm.list(I18n.T("the kiss blown to []", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:252
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[]'s receiving a blown kiss from []", protagonist_name, deuteragonist_name),
            I18n.T("[] blowing a kiss to []", deuteragonist_name, protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:258
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] as the kiss lands on their cheek.", protagonist_name, mood_verb),
            I18n.T("[] [] as it happen.", protagonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:265
public partial class datum_memory_good_food : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:269  var/food (types-converter.md 3.5 typed it Type, from the parameter's name; it holds the food's
    // name, text: hand-declared, so the constructor is hand-written too)
    public object food;

    // tg: code/datums/memory/general_memories.dm:266
    public datum_memory_good_food()
    {
        story_value = STORY_VALUE_MEH;
    }

    // tg: code/datums/memory/general_memories.dm:271
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "food")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        obj_item food = Dm.arg<obj_item>(__extra, 4);
        this.food = food.name;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, food));
    }

    // tg: code/datums/memory/general_memories.dm:281
    public override object get_names()
    {
        return Dm.list(I18n.T("A delicious [] [] ate", food, protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:284
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] changing []'s outlook on food", food, protagonist_name),
            I18n.T("[] is leaving [] round and full", food, protagonist_name),
            I18n.T("[] leaving a long lasting impression on []", food, protagonist_name),
            I18n.T("[] enjoying an incredibly good []", protagonist_name, food),
            I18n.T("[] producing a slice of life anime reaction to eating []", protagonist_name, food));
    }

    // tg: code/datums/memory/general_memories.dm:293
    public override object get_moods()
    {
        return Dm.list(I18n.T("[] [] as they take another bite.", protagonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:297
public partial class datum_memory_good_drink : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:303
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "drink")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        datum_reagent drink = Dm.arg<datum_reagent>(__extra, 4);
        this.drink = drink.name;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, drink));
    }

    // tg: code/datums/memory/general_memories.dm:313
    public override object get_names()
    {
        return Dm.list(I18n.T("a delicious [] [] consumed", drink, protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:316
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] changing []'s outlook on classy drinking", drink, protagonist_name),
            I18n.T("[] leaving a long lasting impression on []", drink, protagonist_name),
            I18n.T("[] enjoying an incredibly good []", protagonist_name, drink),
            I18n.T("[] slurping some tasty []", protagonist_name, drink));
    }

    // tg: code/datums/memory/general_memories.dm:324
    public override object get_moods()
    {
        return Dm.list(I18n.T("[] [] as they take another sip.", protagonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:328
public partial class datum_memory_was_burning : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:332
    public override object get_names()
    {
        return Dm.list(I18n.T("The burning of [].", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:335
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] bursting into flames", protagonist_name),
            I18n.T("[] turning into a human torch", protagonist_name),
            I18n.T("the fire that engulfed []", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:342
    public override object get_moods()
    {
        return Dm.list(I18n.T("[] [] as their skin melts.", protagonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:346
public partial class datum_memory_was_dismembered : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:352
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "lost_limb")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        obj_item_bodypart lost_limb = Dm.arg<obj_item_bodypart>(__extra, 4);
        this.lost_limb = lost_limb.plaintext_zone;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, lost_limb));
    }

    // tg: code/datums/memory/general_memories.dm:362
    public override object get_names()
    {
        return Dm.list(I18n.T("The loss of []'s [].", protagonist_name, lost_limb));
    }

    // tg: code/datums/memory/general_memories.dm:365
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] becoming eligible for handicapped parking", protagonist_name),
            I18n.T("[]'s [] being shot into the abyss", protagonist_name, lost_limb),
            I18n.T("[]'s [] flinging away", protagonist_name, lost_limb));
    }

    // tg: code/datums/memory/general_memories.dm:372
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] after losing [].", protagonist_name, mood_verb, lost_limb),
            I18n.T("Without [], [] [].", lost_limb, protagonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:379
public partial class datum_memory_pet_died : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:385
    public override object get_names()
    {
        return Dm.list(I18n.T("The death of [].", deuteragonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:388
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("honoring [], the station's pet", deuteragonist_name),
            I18n.T("[]'s funeral, which is attended by a group of crew members", deuteragonist_name),
            I18n.T("a shallow hole, with [] inside", deuteragonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:395
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] without [].", protagonist_name, mood_verb, deuteragonist_name),
            I18n.T("Without [], [] [].", deuteragonist_name, protagonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:403
public partial class datum_memory_revolution_rev_victory : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:408
    public override object get_names()
    {
        return Dm.list(I18n.T("The revolution of [] by [].", station_name(), protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:411
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] raising the flag of the revolution over the corpses of the former dictators", protagonist_name),
            I18n.T("a flag waving above a pile of corpses with [] standing over it", protagonist_name),
            I18n.T("a poster that says [] with a cross in it, hailing in a new era", station_name()),
            I18n.T("a statue of the former captain toppled over, with [] next to it", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:419
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] at the fall of [].", protagonist_name, mood_verb, station_name()),
            I18n.T("[] [] at the idea of the new era.", protagonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:426
public partial class datum_memory_revolution_heads_defeated : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:431
    public override object get_names()
    {
        return Dm.list(I18n.T("The defeat of [] at the hands of the revolution", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:434
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] fleeing [] in shame due to the success of the revolution", protagonist_name, station_name()),
            I18n.T("[] looking at a camera feed of rampaging revolutionaries", protagonist_name),
            I18n.T("a poster with []'s face stratched out", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:441
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] at the fall of [].", protagonist_name, mood_verb, station_name()),
            I18n.T("[] [] at their defeat.", protagonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:448
public partial class datum_memory_revolution_rev_defeat : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:453
    public override object get_names()
    {
        return Dm.list(
            I18n.T("The defeat of [] at the hands of the Nanotrasen", protagonist_name),
            I18n.T("The end of []'s glorious revolution", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:459
    public override object get_starts()
    {
        return Dm.list(I18n.T("[] fleeing [] in shame due to the failure of their revolution", protagonist_name, station_name()));
    }

    // tg: code/datums/memory/general_memories.dm:462  (tg defines get_moods twice on this type; the later one, :480, wins: rules 7.5)
    public object get_moods__d0()
    {
        return Dm.list(I18n.T("[] [] at their defeat.", protagonist_name, mood_verb));
    }

    // tg: code/datums/memory/general_memories.dm:480
    public override object get_moods()
    {
        return Dm.list(I18n.T("[] [] over the defeat of the revolution by the hands of Nanotrasen.", protagonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:466
public partial class datum_memory_revolution_heads_victory : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:471
    public override object get_names()
    {
        return Dm.list(I18n.T("The success of [] and Nanotrasen over the hateful revolution", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:474
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] dusting off their hands in victory over the revoution", protagonist_name),
            I18n.T("the banner of Nanotrasen flying on the bridge of [] with [] proudly beside it", station_name(), protagonist_name));
    }
}

// tg: code/datums/memory/general_memories.dm:484
public partial class datum_memory_received_medal : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:494
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "medal_type", "medal_text")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        obj_item medal_type = Dm.arg<obj_item>(__extra, 4);
        object medal_text = Dm.arg(__extra, 5);
        this.medal_type = medal_type.name;
        this.medal_text = medal_text;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, medal_type, medal_text));
    }

    // tg: code/datums/memory/general_memories.dm:506
    public override object get_names()
    {
        return Dm.list(I18n.T("The award ceremony of [] to [].", medal_type, protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:509
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] accepting a [] inscribed with \"[]\" from []", protagonist_name, medal_type, medal_text, deuteragonist_name),
            I18n.T("[] receiving a [] with the inscription \"[]\"", protagonist_name, medal_type, medal_text),
            I18n.T("a [] with the inscription \"[]\" being awarded to [] by []", medal_type, medal_text, protagonist_name, deuteragonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:516
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] as they receive their medal.", protagonist_name, mood_verb),
            I18n.T("[] [] with their newly received award.", protagonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:523
public partial class datum_memory_megafauna_slayer : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:528
    public override object get_names()
    {
        return Dm.list(I18n.T("The slaughter of [].", antagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:531
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] performing the final strike on [], taking it down", protagonist_name, antagonist_name),
            I18n.T("[] standing with the head of [] in their hand", protagonist_name, antagonist_name),
            I18n.T("the killing of [], the dangerous megafauna, by []", antagonist_name, protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:538
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] as the blood lust fades from their eyes.", protagonist_name, mood_verb),
            I18n.T("[] [] as they search the corpse for valuables.", protagonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:545
public partial class datum_memory_held_at_gunpoint : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:552
    public override object get_names()
    {
        return Dm.list(I18n.T("[] being held at gunpoint.", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:555
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] with [] pressed to their skull by []", protagonist_name, antagonist_name, deuteragonist_name),
            I18n.T("[] whipping out [] and pointing it at []", deuteragonist_name, antagonist_name, protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:561
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] as they are faced with the situation.", protagonist_name, mood_verb),
            I18n.T("[] [] as they stare down []'s barrel.", protagonist_name, mood_verb, antagonist_name));
    }
}

// tg: code/datums/memory/general_memories.dm:568
public partial class datum_memory_won_jackpot : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:571
    public override object get_names()
    {
        return Dm.list(I18n.T("[] winning it big.", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:574
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] hits []'s lever, with money symbols in their eyes.", protagonist_name, deuteragonist_name),
            I18n.T("[] plays a jingle as [] pulls the lever.", deuteragonist_name, protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:580
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] as they collect the money from the [].", protagonist_name, mood_verb, deuteragonist_name),
            I18n.T("[] [] as [] starts spitting out money with a blare.", protagonist_name, mood_verb, deuteragonist_name));
    }
}

// tg: code/datums/memory/general_memories.dm:587
public partial class datum_memory_witness_gib : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:592
    public override object get_names()
    {
        return Dm.list(I18n.T("[] exploding into bits.", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:595
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] exploding into little fleshy bits", protagonist_name),
            I18n.T("[] becoming flesh paste in the blink of an eye", protagonist_name));
    }
}

// tg: code/datums/memory/general_memories.dm:602
public partial class datum_memory_witness_vendor_crush : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:608
    public override object get_names()
    {
        return Dm.list(I18n.T("[] being crushed by [].", protagonist_name, antagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:611
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] being crushed by the []", protagonist_name, antagonist_name),
            I18n.T("the [] that crashed on top of []", antagonist_name, protagonist_name),
            I18n.T("the fall of [] onto []", antagonist_name, protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:618
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] as they lie under the machine.", protagonist_name, mood_verb),
            I18n.T("[] [] as a goodie falls out of the [].", protagonist_name, mood_verb, antagonist_name));
    }
}

// tg: code/datums/memory/general_memories.dm:625
public partial class datum_memory_witness_supermatter_dusting : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:631
    public override object get_names()
    {
        return Dm.list(I18n.T("The dusting of [] by the [].", protagonist_name, antagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:634
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] turning into a pile of bones after touching the []", protagonist_name, antagonist_name),
            I18n.T("The [] turning [] into ash", antagonist_name, protagonist_name),
            I18n.T("The dusting of [] after they got too close to the []", protagonist_name, antagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:641
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] as they faded way.", protagonist_name, mood_verb),
            I18n.T("[] [] as they are reduced to atoms.", protagonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:648
public partial class datum_memory_playing_cards : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:660
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "game", "protagonist_held_card", "other_players")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        object game = Dm.arg(__extra, 4);
        obj_item protagonist_held_card = Dm.arg<obj_item>(__extra, 5);
        DmList other_players = Dm.arg<DmList>(__extra, 6);
        this.game = game;
        this.protagonist_held_card = protagonist_held_card.name;

        DmList story_players = Dm.list();
        foreach (mob_living player in Dm.each_cast<mob_living>(other_players))
        {
            // This will result in some strange structure sometimes -
            // "The assistant, the assistant, and the assistant playing a game",
            // but meh. Someone can improve upon it in the future
            Dm.append(ref story_players, build_story_character(player));
        }

        this.formatted_players_list = english_list(story_players, nothing_text: I18n.T("no-one"));
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, game, protagonist_held_card, other_players));
    }

    // tg: code/datums/memory/general_memories.dm:682
    public override object get_names()
    {
        return Dm.list(I18n.T("The [] of [] with [].", game, protagonist_name, formatted_players_list));
    }

    // tg: code/datums/memory/general_memories.dm:685
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] waiting for [] to start the []", formatted_players_list, protagonist_name, game),
            I18n.T("The [] has been setup by []", game, deuteragonist_name),
            I18n.T("[] starts shuffling the deck for the []", deuteragonist_name, game));
    }

    // tg: code/datums/memory/general_memories.dm:692
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] as they hold the [] for the [].", protagonist_name, mood_verb, protagonist_held_card, game),
            I18n.T("[] [] as they pickup the [].", protagonist_name, mood_verb, protagonist_held_card),
            I18n.T("[] [] as they put down the [].", protagonist_name, mood_verb, protagonist_held_card));
    }
}

// tg: code/datums/memory/general_memories.dm:700
public partial class datum_memory_playing_card_pickup : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:707
    public override object get_names()
    {
        return Dm.list(I18n.T("[] tricking [] into playing 52 pickup with [].", protagonist_name, deuteragonist_name, antagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:710
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] tossing the [] at [] spilling cards all over the floor", protagonist_name, antagonist_name, deuteragonist_name),
            I18n.T("A [] thrown by [] splatters across [] face", antagonist_name, protagonist_name, deuteragonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:716
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] as they taunt [].", protagonist_name, mood_verb, deuteragonist_name),
            I18n.T("[] [] as they shamefully pickup the cards.", deuteragonist_name, mood_verb));
    }
}

// tg: code/datums/memory/general_memories.dm:723
public partial class datum_memory_witnessed_russian_roulette : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:732  var/result (types-converter.md 3.5 typed it Type, from its name; it holds "won" or "lost":
    // hand-declared, so the constructor is hand-written too)
    public object result;

    // tg: code/datums/memory/general_memories.dm:724
    public datum_memory_witnessed_russian_roulette()
    {
        memory_flags = MEMORY_CHECK_BLINDNESS;
    }

    // tg: code/datums/memory/general_memories.dm:734
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "aimed_at", "rounds_loaded", "result")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        object aimed_at = Dm.arg(__extra, 4);
        object rounds_loaded = Dm.arg(__extra, 5);
        object result = Dm.arg(__extra, 6);
        rounds_loaded ??= 0f;
        this.aimed_at = aimed_at;
        this.rounds_loaded = rounds_loaded;
        this.result = result;

        if (Dm.eq(result, "won"))
            // The more bullets, the better the story.
            story_value = max(STORY_VALUE_NONE, Dm.num(rounds_loaded));
        else
            story_value = STORY_VALUE_SHIT;

        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, aimed_at, rounds_loaded, result));
    }

    // tg: code/datums/memory/general_memories.dm:755
    public override object get_names()
    {
        return Dm.list(I18n.T("[] playing a game of russian roulette.", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:758
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] aiming at their [] right before they pull the trigger.", protagonist_name, aimed_at),
            I18n.T("The revolver has [] rounds loaded in the chamber.", rounds_loaded),
            I18n.T("[] is gambling their life as they spin the revolver.", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:765
    public override object get_moods()
    {
        return Dm.list(I18n.T("[] [] as they [] the deadly game of roulette.", protagonist_name, mood_verb, result));
    }
}

// tg: code/datums/memory/general_memories.dm:769
public partial class datum_memory_heretic_knowledge_ritual : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:773
    public override object get_names()
    {
        return Dm.list(I18n.T("[] absorbing boundless knowledge through eldritch research.", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:776
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] laying out a circle of green tar and candles", protagonist_name),
            I18n.T("multiple books around [] flipping open", protagonist_name),
            I18n.T("green and purple energy surrounding []", protagonist_name),
            I18n.T("[], eyes wide open and unblinking, reading a strange book", protagonist_name),
            I18n.T("a pile of gore and viscera on a complex looking rune"),
            I18n.T("a wide, strange looking circle, with [] sketching it", protagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:786
    public override object get_moods()
    {
        return Dm.list(I18n.T("[] [] as their hand glows with power.", protagonist_name, mood_verb));
    }

    // tg: code/datums/memory/general_memories.dm:789
    public override object get_happy_moods()
    {
        return Dm.list(I18n.T("cackling madly"));
    }

    // tg: code/datums/memory/general_memories.dm:792
    public override object get_neutral_moods()
    {
        return Dm.list(I18n.T("staring blankly with a wide grin"));
    }

    // tg: code/datums/memory/general_memories.dm:795
    public override object get_sad_moods()
    {
        return Dm.list(I18n.T("cackling insanely"));
    }
}

// tg: code/datums/memory/general_memories.dm:799
public partial class datum_memory_bomb_defuse_failure : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:805
    public override object get_names()
    {
        return Dm.list(I18n.T("[] failing to defuse [].", protagonist_name, antagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:808
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] cutting the wrong wire on []", protagonist_name, antagonist_name),
            I18n.T("[] sweating nervously and shielding their face as [] makes a loud noise", protagonist_name, antagonist_name),
            I18n.T("The clock on [] suddenly jumping to 0 seconds", antagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:815
    public override object get_moods()
    {
        return Dm.list(I18n.T("[] [] as they snip a wire on [].", protagonist_name, mood_verb, antagonist_name));
    }
}

// tg: code/datums/memory/general_memories.dm:819
public partial class datum_memory_bomb_defuse_success : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:827
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "bomb_time_left")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        object bomb_time_left = Dm.arg(__extra, 4);
        bomb_time_left ??= -1f;
        this.bomb_time_left = bomb_time_left;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, bomb_time_left));
    }

    // tg: code/datums/memory/general_memories.dm:837
    public override object get_names()
    {
        return Dm.list(I18n.T("[] successfully defusing [].", protagonist_name, antagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:840
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] cutting the right wire on []", protagonist_name, antagonist_name),
            I18n.T("[] sweating nervously and shielding their face as [] makes a shrill beep", protagonist_name, antagonist_name),
            I18n.T("The clock on [] stopping at []", antagonist_name, bomb_time_left));
    }

    // tg: code/datums/memory/general_memories.dm:847
    public override object get_moods()
    {
        return Dm.list(I18n.T("[] [] as they snip a wire on [].", protagonist_name, mood_verb, antagonist_name));
    }
}

// tg: code/datums/memory/general_memories.dm:851
public partial class datum_memory_helped_up : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:854
    public override object get_names()
    {
        return Dm.list(I18n.T("[] gentlemanly helping up [].", protagonist_name, deuteragonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:857
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] helping up []", protagonist_name, deuteragonist_name),
            I18n.T("[] taking the hand offered graciously by [] to get up", deuteragonist_name, protagonist_name));
    }
}

// tg: code/datums/memory/general_memories.dm:864
public partial class datum_memory_caught_fish : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:867
    public override object get_names()
    {
        return Dm.list(
            I18n.T("[] catching an absolute honker.", protagonist_name),
            I18n.T("[] caught a [].", protagonist_name, deuteragonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:873
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] reels in the line", protagonist_name),
            I18n.T("[]'s eye glints, and they begin reeling", protagonist_name),
            I18n.T("in a fishing trance, [] catches something", protagonist_name),
            I18n.T("[] begins battle with a fish", protagonist_name),
            I18n.T("a whole lot of fishing going on"));
    }

    // tg: code/datums/memory/general_memories.dm:882
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] as a [] flies out of the water!", protagonist_name, mood_verb, deuteragonist_name),
            I18n.T("[] [] as they catch a []!", protagonist_name, mood_verb, deuteragonist_name),
            I18n.T("[] [] as they pose holding a []!", protagonist_name, mood_verb, deuteragonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:889
    public override object get_sad_moods()
    {
        return Dm.list(I18n.T("partakes in therapy fishing"));
    }
}

// tg: code/datums/memory/general_memories.dm:893
public partial class datum_memory_dna_infusion : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:898
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "mutantlike")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        object mutantlike = Dm.arg(__extra, 4);
        this.mutantlike = mutantlike;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, mutantlike));
    }

    // tg: code/datums/memory/general_memories.dm:908
    public override object get_names()
    {
        return Dm.list(
            I18n.T("[] infusing with a [].", protagonist_name, deuteragonist_name),
            I18n.T("[] infusing a [] into themselves.", protagonist_name, deuteragonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:914
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] enters a creepy DNA machine", protagonist_name),
            I18n.T("[]'s partakes in some mad science", protagonist_name),
            I18n.T("the DNA infuser closes with [] inside", protagonist_name),
            I18n.T("a [] is in the infusion slot", deuteragonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:922
    public override object get_moods()
    {
        return Dm.list(
            I18n.T("[] [] as they infuse with a []!", protagonist_name, mood_verb, deuteragonist_name),
            I18n.T("[] [] as they become one the [].", protagonist_name, mood_verb, deuteragonist_name),
            I18n.T("[] [] as their DNA has [] folded into it.", protagonist_name, mood_verb, deuteragonist_name),
            I18n.T("[] becomes more [] as they infuse with a []!", protagonist_name, mutantlike, deuteragonist_name),
            I18n.T("[] becomes more [] as they become one the [].", protagonist_name, mutantlike, deuteragonist_name),
            I18n.T("[] becomes more [] as their DNA has [] folded into it.", protagonist_name, mutantlike, deuteragonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:932
    public override object get_happy_moods()
    {
        return Dm.list(
            I18n.T("endures the pain for science"),
            I18n.T("confidently winces through the pain"));
    }

    // tg: code/datums/memory/general_memories.dm:938
    public override object get_neutral_moods()
    {
        return Dm.list(
            I18n.T("screams with pain"),
            I18n.T("begins to have second thoughts"));
    }

    // tg: code/datums/memory/general_memories.dm:944
    public override object get_sad_moods()
    {
        return Dm.list(I18n.T("bitterly rejects their humanity"));
    }
}

// tg: code/datums/memory/general_memories.dm:948
public partial class datum_memory_recruited_by_headrev : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:950
    public override object get_names()
    {
        return Dm.list(I18n.T("[] is converted into a revolutionary by []", protagonist_name, antagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:953
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[]'s mind sets itself on a singular, violent purpose as they're flashed by []: Kill the heads.", protagonist_name, antagonist_name),
            I18n.T("[] lifts an odd device to []'s eyes and flashes him, imprinting murderous instructions.", antagonist_name, protagonist_name));
    }
}

// tg: code/datums/memory/general_memories.dm:960
public partial class datum_memory_recruited_by_blood_brother : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:962
    public override object get_names()
    {
        return Dm.list(I18n.T("[] is converted into a blood brother by []", protagonist_name, antagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:965
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] acts just a bit too friendly with [], moments away from converting them into a blood brother.", antagonist_name, protagonist_name),
            I18n.T("[] is brought into []'s life of crime and espionage.", protagonist_name, antagonist_name));
    }
}

// tg: code/datums/memory/general_memories.dm:972
public partial class datum_memory_witnessed_gods_wrath : datum_memory
{
    // tg: code/datums/memory/general_memories.dm:976  (tg's New has no body: it does not call ..(), so the memory gets no names and no name)
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist")]
    public override async DmTask New(params object[] __extra)
    {
    }

    // tg: code/datums/memory/general_memories.dm:983
    public override object get_names()
    {
        return Dm.list(I18n.T("[] suffering the wrath of [].", protagonist_name, antagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:986
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] burns [], and [] turns [] into a fine red mist.", protagonist_name, deuteragonist_name, antagonist_name, protagonist_name),
            I18n.T("[] explodes [] into a million pieces for defiling [].", antagonist_name, protagonist_name, deuteragonist_name),
            I18n.T("[] angers [] by defiling [], and gets obliterated.", protagonist_name, antagonist_name, deuteragonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:993
    public override object get_moods()
    {
        return Dm.list(I18n.T("[] [] as they get annihilated by [].", protagonist_name, mood_verb, antagonist_name));
    }

    // tg: code/datums/memory/general_memories.dm:996
    public override object get_happy_moods()
    {
        return Dm.list(
            I18n.T("cackles hysterically"),
            I18n.T("laughs maniacally"),
            I18n.T("grins widely"));
    }

    // tg: code/datums/memory/general_memories.dm:1003
    public override object get_neutral_moods()
    {
        return Dm.list(
            I18n.T("appears concerned"),
            I18n.T("reconsiders their life decisions"),
            I18n.T("has a blank expression"));
    }

    // tg: code/datums/memory/general_memories.dm:1010
    public override object get_sad_moods()
    {
        return Dm.list(
            I18n.T("appears dejected"),
            I18n.T("is filled with regret"),
            I18n.T("winces in despair"));
    }
}

