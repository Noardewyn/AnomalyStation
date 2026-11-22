// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/scanners/sequence_scanner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps J5, D298): the scanner as an item: its examine, the genetic makeup scan, the sequence display and its recharge. Reading a target's mutations
// (gene_scan, get_display_name) and the DNA console's database are P3's (live-partial rows).

// tg: code/game/objects/items/devices/scanners/sequence_scanner.dm:1
public partial class obj_item_sequence_scanner : obj_item
{
    // tg: code/game/objects/items/devices/scanners/sequence_scanner.dm:30
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(span_notice(I18n.T("Use primary attack to scan mutations, Secondary attack to scan genetic makeup")));
        if (Dm.num(length(genetic_makeup_buffer)) > 0)
            dot.Add(span_notice(I18n.T("It has the genetic makeup of \"[]\" stored inside its buffer", genetic_makeup_buffer["name"])));
        return dot;
    }

    // tg: code/game/objects/items/devices/scanners/sequence_scanner.dm:36
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        // tg: the /obj/machinery/computer/dna_console branch (:37-:43): the console's research database is P3's
        if (!isliving(interacting_with))
            return NONE;

        add_fingerprint(user);

        //no scanning if its a husk or DNA-less Species
        if (!HAS_TRAIT(interacting_with, TRAIT_GENELESS) && !HAS_TRAIT(interacting_with, TRAIT_BADDNA))
        {
            user.visible_message(span_notice(I18n.T("[] analyzes []'s genetic sequence.", user, interacting_with)));
            balloon_alert(user, I18n.T("sequence analyzed"));
            playsound(user, Dm.resource("sound/items/healthanalyzer.ogg"), 50); // close enough
            gene_scan(interacting_with as mob_living_carbon, user);
            return ITEM_INTERACT_SUCCESS;
        }

        user.visible_message(span_notice(I18n.T("[] fails to analyze []'s genetic sequence.", user, interacting_with)), span_warning(I18n.T("[] has no readable genetic sequence!", interacting_with)));
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/items/devices/scanners/sequence_scanner.dm:59
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        // tg: the /obj/machinery/computer/dna_console branch (:60-:64): exporting to the console's buffer is P3's
        if (!isliving(interacting_with))
            return NONE;

        add_fingerprint(user);

        //no scanning if its a husk, DNA-less Species or DNA that isn't able to be copied by a changeling/disease
        if (!HAS_TRAIT(interacting_with, TRAIT_GENELESS) && !HAS_TRAIT(interacting_with, TRAIT_BADDNA) && !HAS_TRAIT(interacting_with, TRAIT_NO_DNA_COPY))
        {
            user.visible_message(span_warning(I18n.T("[] is scanning []'s genetic makeup.", user, interacting_with)));
            if (!Dm.truthy(await do_after(user, 3 * SECONDS, interacting_with)))
            {
                balloon_alert(user, I18n.T("scan failed!"));
                user.visible_message(span_warning(I18n.T("[] fails to scan []'s genetic makeup.", user, interacting_with)));
                return ITEM_INTERACT_BLOCKING;
            }
            makeup_scan(interacting_with as mob_living_carbon, user);
            balloon_alert(user, I18n.T("makeup scanned"));
            return ITEM_INTERACT_SUCCESS;
        }

        user.visible_message(span_notice(I18n.T("[] fails to analyze []'s genetic makeup.", user, interacting_with)), span_warning(I18n.T("[] has no readable genetic makeup!", interacting_with)));
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/items/devices/scanners/sequence_scanner.dm:85
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await display_sequence(user as mob_living);
        return null;
    }

    // tg: code/game/objects/items/devices/scanners/sequence_scanner.dm:88
    public override async DmTask<object> attack_self_tk(mob user = null) => null;

    ///proc for scanning someone's mutations
    // tg: code/game/objects/items/devices/scanners/sequence_scanner.dm:92
    public virtual void gene_scan(mob_living_carbon target = null, mob_living user = null)
    {
        if (target == null || !Dm.truthy(target.has_dna()))
            return;
        Dm.Deferred("code/game/objects/items/devices/scanners/sequence_scanner.dm:96", "gene_scan: the target's mutation_index and mutations (P3, DNA and genetics)");
    }

    // tg: code/game/objects/items/devices/scanners/sequence_scanner.dm:162
    public virtual object get_display_name(object mutation = null)
    {
        // GET_INITIALIZED_MUTATION: the mutations are P3's; with none initialized tg's branch is "ERROR"
        return "ERROR";
    }

    ///proc for scanning someone's genetic makeup
    // tg: code/game/objects/items/devices/scanners/sequence_scanner.dm:113
    public virtual void makeup_scan(mob_living_carbon target = null, mob_living user = null)
    {
        if (target == null || !Dm.truthy(target.has_dna()))
            return;

        genetic_makeup_buffer = Dm.list(
            Dm.kv("label", Dm.text("Analyzer Slot:[]", target.real_name)),
            Dm.kv("UI", target.dna.unique_identity),
            Dm.kv("UE", target.dna.unique_enzymes),
            Dm.kv("UF", target.dna.unique_features),
            Dm.kv("name", target.real_name),
            Dm.kv("blood_type", target.get_bloodtype()));
    }

    // tg: code/game/objects/items/devices/scanners/sequence_scanner.dm:124
    public virtual async DmTask display_sequence(mob_living user = null)
    {
        if (!Dm.truthy(length(buffer)) || !Dm.truthy(ready))
            return;
        DmList options = Dm.list();
        foreach (object mutation in Dm.each(buffer))
            options.Add(get_display_name(mutation));

        object answer = await tgui_input_list(user, I18n.T("Analyze Potential"), I18n.T("Sequence Analyzer"), sort_list(options) as DmList);
        if (answer == null)
            return;
        if (!Dm.truthy(ready) || !Dm.truthy(await user.can_perform_action(this, Dm.bitor(Dm.bitor(NEED_LITERACY, NEED_LIGHT), FORBID_TELEKINESIS_REACH))))
            return;

        object sequence = null;
        foreach (object mutation in Dm.each(buffer)) //this physically hurts but i dont know what anything else short of an assoc list
        {
            if (Dm.eq(get_display_name(mutation), answer))
            {
                sequence = buffer[mutation];
                break;
            }
        }

        if (Dm.truthy(sequence))
        {
            string display = "";
            float blocks = Dm.num(length_char(sequence)) / DNA_MUTATION_BLOCKS - 1;
            for (int i = 0; i <= blocks; i++)
            {
                if (i != 0)
                    display += "-";
                display += copytext_char(sequence, 1 + i * DNA_MUTATION_BLOCKS, DNA_MUTATION_BLOCKS * (1 + i) + 1);
            }

            to_chat(user, Dm.text("[]<br>", span_boldnotice(display)));
        }

        ready = FALSE;
        icon_state = $"{icon_state}_recharging";
        _addtimer(Dm.@new<datum_callback>(this, nameof(recharge)), cooldown, TIMER_UNIQUE, file: "code/game/objects/items/devices/scanners/sequence_scanner.dm", line: 154);
    }

    // tg: code/game/objects/items/devices/scanners/sequence_scanner.dm:156
    public virtual void recharge()
    {
        icon_state = Dm.initial(this, nameof(icon_state)) as string;
        ready = TRUE;
    }
}
