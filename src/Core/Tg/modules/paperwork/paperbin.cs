// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/paperbin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298). The var blocks are generated.

// tg: code/modules/paperwork/paperbin.dm:3
public partial class obj_item_paper_bin : obj_item
{
    // tg: code/modules/paperwork/paperbin.dm:1  #define PAPERS_PER_OVERLAY 8 (file-local)
    private const int PAPERS_PER_OVERLAY = 8;
    // tg: code/modules/paperwork/paperbin.dm:2  #define PAPER_OVERLAY_PIXEL_SHIFT 2 (file-local)
    private const int PAPER_OVERLAY_PIXEL_SHIFT = 2;

    // tg: code/modules/paperwork/paperbin.dm:153  update_overlays' var/static/obj/item/paper/reference_paper (rules 10.6)
    private static ref obj_item_paper update_overlays__reference_paper => ref TgState.Current.obj_item_paper_bin__update_overlays__reference_paper;

    // tg: code/modules/paperwork/paperbin.dm:27
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        interaction_flags_item = Dm.bitand(interaction_flags_item, Dm.bitnot(INTERACT_ITEM_ATTACK_HAND_PICKUP));
        Waiting.NoSleep(AddElement(typeof(datum_element_drag_pickup)));
        if (Dm.truthy(mapload))
        {
            obj_item_pen pen = Dm.locate<obj_item_pen>(loc);
            if (pen != null && bin_pen == null)
            {
                Waiting.NoSleep(pen.forceMove(this));
                bin_pen = pen;
            }
        }
        update_appearance();
        return dot;
    }

    // tg: code/modules/paperwork/paperbin.dm:38
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(paper_stack);
        return base.Destroy(force);
    }

    /// Returns a fresh piece of paper
    // tg: code/modules/paperwork/paperbin.dm:43
    public virtual async DmTask<object> generate_paper()
    {
        obj_item_paper paper = Dm.@new(papertype) as obj_item_paper;
        if (Dm.truthy(check_holidays(APRIL_FOOLS)))
        {
            if (prob(30))
            {
                paper.add_raw_text(Dm.text("<font face=\"[]\" color=\"red\"><b>HONK HONK HONK HONK HONK HONK HONK<br>HOOOOOOOOOOOOOOOOOOOOOONK<br>APRIL FOOLS</b></font>", CRAYON_FONT));
                await paper.AddElement(typeof(datum_element_honkspam));
                paper.update_appearance();
            }
        }
        return paper;
    }

    // tg: code/modules/paperwork/paperbin.dm:52
    [DmParams("droppoint", "collapse")]
    public override async DmTask dump_contents(params object[] __extra)
    {
        atom droppoint = Dm.arg(__extra, 0) as atom;
        object collapse = Dm.arg(__extra, 1) ?? FALSE;
        if (droppoint == null)
            droppoint = drop_location() as atom;
        if (Dm.truthy(collapse))
            visible_message(span_warning(I18n.T("The stack of paper collapses!")));
        foreach (obj_item_paper stacked_paper in Dm.each<obj_item_paper>(paper_stack)) //first, dump all of the paper that already exists
        {
            await stacked_paper.forceMove(droppoint);
            if (!Dm.truthy(stacked_paper.pixel_y))
                stacked_paper.pixel_y = rand(-3, 3);
            if (!Dm.truthy(stacked_paper.pixel_x))
                stacked_paper.pixel_x = rand(-3, 3);
            paper_stack.Remove(stacked_paper);
            total_paper -= 1;
        }
        for (float i = 1, i__end = total_paper; i <= i__end; i++) //second, generate new paper for the remainder
        {
            obj_item_paper new_paper = await generate_paper() as obj_item_paper;
            await new_paper.forceMove(droppoint);
            if (!Dm.truthy(new_paper.pixel_y))
                new_paper.pixel_y = rand(-3, 3);
            if (!Dm.truthy(new_paper.pixel_x))
                new_paper.pixel_x = rand(-3, 3);
        }
        if (bin_pen != null)
        {
            obj_item_pen pen = bin_pen;
            await pen.forceMove(droppoint);
            bin_pen = null;
        }
        total_paper = 0;
        update_appearance();
    }

    // tg: code/modules/paperwork/paperbin.dm:79
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        if (total_paper > 0)
        {
            total_paper = 0;
            QDEL_LIST(paper_stack);
            update_appearance();
        }

        return await base.fire_act(exposed_temperature, exposed_volume);
    }

    // tg: code/modules/paperwork/paperbin.dm:87
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/paperwork/paperbin.dm:91
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (isliving(user))
        {
            mob_living living_mob = (mob_living)user;
            if (!Dm.truthy(Dm.bitand(living_mob.mobility_flags, MOBILITY_PICKUP)))
                return null;
        }
        user.changeNext_move(CLICK_CD_RAPID);
        if (Dm.truthy(at_overlay_limit()))
        {
            await dump_contents(drop_location(), TRUE);
            return null;
        }
        if (bin_pen != null)
        {
            obj_item_pen pen = bin_pen;
            pen.add_fingerprint(user);
            await pen.forceMove(user.loc as atom);
            await user.put_in_hands(pen);
            to_chat(user, span_notice(I18n.T("You take [] out of [].", pen, this)));
            bin_pen = null;
            update_appearance();
        }
        else if (total_paper > 0)
        {
            obj_item_paper top_paper = pop(paper_stack) as obj_item_paper ?? await generate_paper() as obj_item_paper;
            total_paper -= 1;
            top_paper.add_fingerprint(user);
            await top_paper.forceMove(user.loc as atom);
            await user.put_in_hands(top_paper);
            to_chat(user, span_notice(I18n.T("You take [] out of [].", top_paper, this)));
            update_appearance();
        }
        else
            to_chat(user, span_warning(I18n.T("[] is empty!", this)));
        add_fingerprint(user);
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/paperwork/paperbin.dm:120
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(at_overlay_limit()))
        {
            await dump_contents(drop_location(), TRUE);
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_paper)))
        {
            if (!Dm.truthy(await user.transferItemToLoc(tool, this, silent: FALSE)))
                return ITEM_INTERACT_BLOCKING;
            to_chat(user, span_notice(I18n.T("You put [] in [].", tool, this)));
            Dm.append(paper_stack, tool);
            total_paper += 1;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_pen)) && bin_pen == null)
        {
            if (!Dm.truthy(await user.transferItemToLoc(tool, this, silent: FALSE)))
                return ITEM_INTERACT_BLOCKING;
            to_chat(user, span_notice(I18n.T("You put [] in [].", tool, this)));
            bin_pen = (obj_item_pen)tool;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/paperwork/paperbin.dm:144
    public virtual object at_overlay_limit()
    {
        return Dm.num(length(overlays) >= MAX_ATOM_OVERLAYS - 1);
    }

    // tg: code/modules/paperwork/paperbin.dm:147
    public virtual void remove_paper(float? amount = null)
    {
        amount ??= 1;
        obj_item_paper top_paper = pop(paper_stack) as obj_item_paper;
        if (top_paper != null)
            qdel(top_paper);
        total_paper -= amount.Value;
    }

    // tg: code/modules/paperwork/paperbin.dm:153
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(total_paper))
            Dm.append(ref dot, I18n.T("It contains [].", total_paper > 1 ? I18n.T("[] papers", total_paper) : I18n.T("one paper")));
        else
            Dm.append(ref dot, I18n.T("It doesn't contain anything."));
        return dot;
    }

    // tg: code/modules/paperwork/paperbin.dm:160
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (total_paper < 1)
            icon_state = "paper_bin0";
        else
            icon_state = Dm.text("[]", Dm.initial(this, nameof(icon_state)));
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/paperwork/paperbin.dm:167
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (update_overlays__reference_paper == null)
            update_overlays__reference_paper = Dm.@new<obj_item_paper>();
        obj_item_paper reference_paper = update_overlays__reference_paper;

        if (bin_pen != null)
            pen_overlay = mutable_appearance(bin_pen.icon, bin_pen.icon_state) as mutable_appearance;

        if (bin_overlay == null)
            bin_overlay = mutable_appearance(icon, bin_overlay_string) as mutable_appearance;

        if (total_paper > 0)
        {
            if (total_paper > length(paper_stack))
            {
                SET_PLANE_EXPLICIT(reference_paper, Dm.num(Dm.initial(reference_paper, nameof(reference_paper.plane))), this);
                reference_paper.update_appearance(); // Ensures all our overlays are on the right plane
            }
            for (float paper_number = 1, paper_number__end = total_paper; paper_number <= paper_number__end; paper_number++)
            {
                if (paper_number != total_paper && paper_number % PAPERS_PER_OVERLAY != 0) //only top paper and every nth paper get overlays
                    continue;

                obj_item_paper current_paper = paper_number > (total_paper - length(paper_stack))
                    ? paper_stack[length(paper_stack) - (total_paper - paper_number + 1) + 1] as obj_item_paper
                    : reference_paper;

                mutable_appearance paper_overlay = mutable_appearance(current_paper.icon, current_paper.icon_state) as mutable_appearance;
                if (current_paper == reference_paper)
                {
                    paper_overlay.color = Dm.initial(papertype, (obj_item_paper p) => p.color);
                    if (Dm.truthy(Dm.initial(papertype, (obj_item_paper p) => p.show_written_words)) && Dm.truthy(Dm.initial(papertype, (obj_item_paper p) => p.default_raw_text)))
                        paper_overlay.icon_state += "_words";
                }
                else
                    paper_overlay.color = current_paper.color;
                paper_overlay.pixel_z = paper_number / PAPERS_PER_OVERLAY - PAPER_OVERLAY_PIXEL_SHIFT; //gives the illusion of stacking
                Dm.append(ref dot, paper_overlay);
                if (paper_number == total_paper) //this is our top paper
                {
                    Dm.append(ref dot, current_paper.overlays); //add overlays only for top paper
                    if (istype(this, typeof(obj_item_paper_bin_bundlenatural)))
                        bin_overlay.pixel_z = paper_overlay.pixel_z; //keeps binding centred on stack
                    if (bin_pen != null)
                        pen_overlay.pixel_z = paper_overlay.pixel_z; //keeps pen on top of stack
                }
            }
            Dm.append(ref dot, bin_overlay);
        }

        if (bin_pen != null)
            Dm.append(ref dot, pen_overlay);
        return dot;
    }
}

// tg: code/modules/paperwork/paperbin.dm:213
public partial class obj_item_paper_bin_bundlenatural : obj_item_paper_bin
{
    // tg: code/modules/paperwork/paperbin.dm:229
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        binding_cable = Dm.@new<obj_item_stack_cable_coil>(this, 2);
        binding_cable.set_cable_color(CABLE_COLOR_BROWN);
        binding_cable.desc += " Non-natural.";
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/paperwork/paperbin.dm:235
    public override async DmTask dump_contents(params object[] __extra)
    {
        atom droppoint = Dm.arg(__extra, 0) as atom;
        await base.dump_contents(__extra);
        await binding_cable.forceMove(droppoint);
        binding_cable = null;
        qdel(this);
    }

    // tg: code/modules/paperwork/paperbin.dm:241
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        bin_overlay = mutable_appearance(icon, bin_overlay_string) as mutable_appearance;
        bin_overlay.color = binding_cable.color;
        return await base.update_overlays(__extra);
    }

    // tg: code/modules/paperwork/paperbin.dm:246
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (total_paper == 0)
            await deconstruct(FALSE);
        return dot;
    }

    // tg: code/modules/paperwork/paperbin.dm:251
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        await dump_contents(drop_location());
        await base.atom_deconstruct(disassembled);
    }

    // tg: code/modules/paperwork/paperbin.dm:255
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        qdel(this);
        return null;
    }

    // tg: code/modules/paperwork/paperbin.dm:258
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_paper_carbon)))
        {
            to_chat(user, span_warning(I18n.T("[] won't fit into [].", tool, this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(tool.get_sharpness()))
        {
            if (!Dm.truthy(await tool.use_tool(this, user, 1 * SECONDS)))
                return ITEM_INTERACT_BLOCKING;
            to_chat(user, span_notice(I18n.T("You slice the cable from [].", this)));
            await deconstruct(TRUE);
            return ITEM_INTERACT_SUCCESS;
        }

        return await base.item_interaction(user, tool, modifiers, __extra);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/paperwork/paperbin.dm:153  /obj/item/paper_bin/update_overlays' var/static/obj/item/paper/reference_paper (null until the
    // first paper bin draws a paper it has not made yet, as tg's)
    internal obj_item_paper obj_item_paper_bin__update_overlays__reference_paper;

    // tg: code/modules/paperwork/paper.dm:795  /obj/item/paper/get_input_field_count's var/static/regex/field_regex (made on the first call)
    internal regex obj_item_paper__get_input_field_count__field_regex;
}
