// Translated from tgstation (snapshot 1d99bd7f): code/datums/memory/key_memories.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package G4 (D291): the whole file. The var blocks are generated; every name, start and mood is player-visible (the memory panel,
// the stories).

// tg: code/datums/memory/key_memories.dm:12
public partial class datum_memory_key_account : datum_memory_key
{
    // tg: code/datums/memory/key_memories.dm:15
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "remembered_id")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        object remembered_id = Dm.arg(__extra, 4);
        this.remembered_id = remembered_id;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, remembered_id));
    }

    // tg: code/datums/memory/key_memories.dm:25
    public override object get_names()
    {
        return Dm.list(I18n.T("The bank ID of [], [].", protagonist_name, remembered_id));
    }

    // tg: code/datums/memory/key_memories.dm:28
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] flexing their last brain cells, proudly showing their lucky numbers [].", protagonist_name, remembered_id),
            I18n.T("[]. The numbers mason, what do they mean!?", remembered_id));
    }
}

// tg: code/datums/memory/key_memories.dm:35
public partial class datum_memory_key_captains_spare_code : datum_memory_key
{
    // tg: code/datums/memory/key_memories.dm:38
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "safe_code")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        object safe_code = Dm.arg(__extra, 4);
        this.safe_code = safe_code;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, safe_code));
    }

    // tg: code/datums/memory/key_memories.dm:48
    public override object get_names()
    {
        return Dm.list(I18n.T("The code to the golden safe on the bridge, [].", safe_code));
    }

    // tg: code/datums/memory/key_memories.dm:51
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] struggling at a wall safe, until finally entering [].", protagonist_name, safe_code),
            I18n.T("[][]. The numbers mason, what do they mean!?", safe_code, rand(0, 9)));
    }
}

// tg: code/datums/memory/key_memories.dm:59
public partial class datum_memory_key_nuke_code : datum_memory_key
{
    // tg: code/datums/memory/key_memories.dm:62
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "nuclear_code")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        object nuclear_code = Dm.arg(__extra, 4);
        this.nuclear_code = nuclear_code;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, nuclear_code));
    }

    // tg: code/datums/memory/key_memories.dm:72
    public override object get_names()
    {
        return Dm.list(I18n.T("[] learns the detonation codes for a nuclear weapon, [].", protagonist_name, nuclear_code));
    }

    // tg: code/datums/memory/key_memories.dm:75
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("The number [] written on a sticky note with the words \"FOR SYNDICATE EYES ONLY\" scrawled next to it.", nuclear_code),
            I18n.T("A piece of paper with the number [] being handed to [] from a figure in a blood-red MODsuit.", nuclear_code, protagonist_name));
    }
}

// tg: code/datums/memory/key_memories.dm:82
public partial class datum_memory_key_quirk_allergy : datum_memory_key
{
    // tg: code/datums/memory/key_memories.dm:86
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "allergy_string")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        object allergy_string = Dm.arg(__extra, 4);
        this.allergy_string = allergy_string;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, allergy_string));
    }

    // tg: code/datums/memory/key_memories.dm:96
    public override object get_names()
    {
        return Dm.list(I18n.T("The [] allergy of [].", allergy_string, protagonist_name));
    }

    // tg: code/datums/memory/key_memories.dm:99
    public override object get_starts()
    {
        return Dm.list(I18n.T("[] sneezing after coming into contact with [].", protagonist_name, allergy_string));
    }
}

// tg: code/datums/memory/key_memories.dm:103
public partial class datum_memory_key_quirk_heirloom : datum_memory_key
{
    // tg: code/datums/memory/key_memories.dm:106
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "heirloom_name")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        object heirloom_name = Dm.arg(__extra, 4);
        this.heirloom_name = heirloom_name;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, heirloom_name));
    }

    // tg: code/datums/memory/key_memories.dm:116
    public override object get_names()
    {
        return Dm.list(I18n.T("[]'s heirloom [].", protagonist_name, heirloom_name));
    }

    // tg: code/datums/memory/key_memories.dm:119
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] being bequeathed the heirloom [] by a dear relative.", protagonist_name, heirloom_name),
            I18n.T("[] discovering the heirloom [] in some long-forgotten boxes.", protagonist_name, heirloom_name),
            I18n.T("[] stealing the heirloom [] from an undeserving family member.", protagonist_name, heirloom_name));
    }
}

// tg: code/datums/memory/key_memories.dm:127
public partial class datum_memory_key_quirk_smoker : datum_memory_key
{
    // tg: code/datums/memory/key_memories.dm:131
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "preferred_brand")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        object preferred_brand = Dm.arg(__extra, 4);
        this.preferred_brand = preferred_brand;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, preferred_brand));
    }

    // tg: code/datums/memory/key_memories.dm:141
    public override object get_names()
    {
        return Dm.list(I18n.T("[]'s addiction to [] cigarettes.", protagonist_name, preferred_brand));
    }

    // tg: code/datums/memory/key_memories.dm:144
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] cigarettes being plundered by [].", preferred_brand, protagonist_name),
            I18n.T("[] buying a box of [] nicotine sticks.", protagonist_name, preferred_brand),
            I18n.T("[] fiending for some [] ciggies.", protagonist_name, preferred_brand));
    }

    // tg: code/datums/memory/key_memories.dm:151
    public override object get_moods()
    {
        return Dm.list(I18n.T("[] [] as they light another up.", memorizer, mood_verb));
    }
}

// tg: code/datums/memory/key_memories.dm:155
public partial class datum_memory_key_quirk_alcoholic : datum_memory_key
{
    // tg: code/datums/memory/key_memories.dm:159
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "preferred_brandy")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        object preferred_brandy = Dm.arg(__extra, 4);
        this.preferred_brandy = preferred_brandy;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, preferred_brandy));
    }

    // tg: code/datums/memory/key_memories.dm:169
    public override object get_names()
    {
        return Dm.list(I18n.T("[]'s addiction to [] alcohol.", protagonist_name, preferred_brandy));
    }

    // tg: code/datums/memory/key_memories.dm:172
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] being downed by [].", preferred_brandy, protagonist_name),
            I18n.T("[] buying a box of [] bottles.", protagonist_name, preferred_brandy),
            I18n.T("[] fiending for some [].", protagonist_name, preferred_brandy));
    }

    // tg: code/datums/memory/key_memories.dm:179
    public override object get_moods()
    {
        return Dm.list(I18n.T("[] [] as they drink some [].", memorizer, mood_verb, preferred_brandy));
    }
}

// tg: code/datums/memory/key_memories.dm:183
public partial class datum_memory_key_traitor_uplink : datum_memory_key
{
    // tg: code/datums/memory/key_memories.dm:187
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "uplink_loc", "uplink_code")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        object uplink_loc = Dm.arg(__extra, 4);
        object uplink_code = Dm.arg(__extra, 5);
        this.uplink_loc = uplink_loc;
        this.uplink_code = uplink_code;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, uplink_loc, uplink_code));
    }

    // tg: code/datums/memory/key_memories.dm:199
    public override object get_names()
    {
        return Dm.list(I18n.T("[]'s equipment uplink in their [], opened via [].", protagonist_name, uplink_loc, uplink_code));
    }

    // tg: code/datums/memory/key_memories.dm:202
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] punching in [] into their [].", protagonist_name, uplink_code, uplink_loc),
            I18n.T("[] writing down [] with their [] besides them, so as to not forget it.", protagonist_name, uplink_code, uplink_loc));
    }
}

// tg: code/datums/memory/key_memories.dm:208
public partial class datum_memory_key_traitor_uplink_implant : datum_memory_key_traitor_uplink
{
    // tg: code/datums/memory/key_memories.dm:210
    public override object get_names()
    {
        return Dm.list(I18n.T("[]'s equipment uplink implanted into their body.", protagonist_name));
    }

    // tg: code/datums/memory/key_memories.dm:213
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] being implanted by a scientist.", protagonist_name),
            I18n.T("[] having surgery done on them by a scientist.", protagonist_name));
    }
}

// tg: code/datums/memory/key_memories.dm:219
public partial class datum_memory_key_permabrig_crimes : datum_memory_key
{
    // tg: code/datums/memory/key_memories.dm:222
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "crimes")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        object crimes = Dm.arg(__extra, 4);
        this.crimes = crimes;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, crimes));
    }

    // tg: code/datums/memory/key_memories.dm:232
    public override object get_names()
    {
        return Dm.list(I18n.T("[]'s crime of \"[]\".", protagonist_name, crimes));
    }

    // tg: code/datums/memory/key_memories.dm:235
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("[] being arrested by security for [].", protagonist_name, crimes),
            I18n.T("[] committing the crimes of [].", protagonist_name, crimes));
    }
}

// tg: code/datums/memory/key_memories.dm:241
public partial class datum_memory_key_message_server_key : datum_memory_key
{
    // tg: code/datums/memory/key_memories.dm:244
    [DmParams("memorizer_mind", "protagonist", "deuteragonist", "antagonist", "decrypt_key")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mind memorizer_mind = Dm.arg<datum_mind>(__extra, 0);
        object protagonist = Dm.arg(__extra, 1);
        object deuteragonist = Dm.arg(__extra, 2);
        object antagonist = Dm.arg(__extra, 3);
        object decrypt_key = Dm.arg(__extra, 4);
        this.decrypt_key = decrypt_key;
        await base.New(Dm.forward(__extra, memorizer_mind, protagonist, deuteragonist, antagonist, decrypt_key));
    }

    // tg: code/datums/memory/key_memories.dm:254
    public override object get_names()
    {
        return Dm.list(I18n.T("The daily message server key is []. Keep it a secret from the clown.", decrypt_key));
    }

    // tg: code/datums/memory/key_memories.dm:257
    public override object get_starts()
    {
        return Dm.list(
            I18n.T("A sticky note attached to a monitor with [] written on it.", decrypt_key),
            I18n.T("Poly the parrot screaming \"[]!\" over and over again.", decrypt_key),
            I18n.T("[] spilling coffee over the message monitor while typing [].", protagonist_name, decrypt_key));
    }
}

