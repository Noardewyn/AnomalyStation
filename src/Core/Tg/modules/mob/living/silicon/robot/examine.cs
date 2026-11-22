// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/robot/examine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326). The chat icon of the held module (icon2html) is left out, as in carbon/examine.cs: the client's
// chat is text.

// tg: code/modules/mob/living/silicon/robot/examine.dm:1
public partial class mob_living_silicon_robot : mob_living_silicon
{
    // tg: code/modules/mob/living/silicon/robot/examine.dm:1
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = Dm.list();
        if (Dm.truthy(desc))
            Dm.append(ref dot, Dm.text("[]", desc));

        string model_name = model != null ? Dm.text("\\improper []", model.name) : "\\improper Default";
        Dm.append(ref dot, I18n.T("[] currently <b>\\a []-type</b> cyborg.", p_Theyre(), model_name));

        obj act_module = get_active_held_item() as obj;
        if (act_module != null)
            Dm.append(ref dot, I18n.T("[] holding \\a [].", p_Theyre(), act_module)); // [icon2html(act_module, user)]: the chat icons are not declared (as carbon/examine.cs)
        Dm.append(ref dot, get_status_effect_examinations(user));
        if (Dm.truthy(get_brute_loss()))
        {
            if (Dm.num(get_brute_loss()) < Dm.num(maxHealth) * 0.5f)
                Dm.append(ref dot, span_warning(I18n.T("[] look[] slightly dented.", p_They(), p_s())));
            else
                Dm.append(ref dot, span_boldwarning(I18n.T("[] look[] severely dented!", p_They(), p_s())));
        }
        if (Dm.truthy(get_fire_loss()) || Dm.truthy(get_tox_loss()))
        {
            float overall_fireloss = Dm.num(get_fire_loss()) + Dm.num(get_tox_loss());
            if (overall_fireloss < Dm.num(maxHealth) * 0.5f)
                Dm.append(ref dot, span_warning(I18n.T("[] look[] slightly charred.", p_They(), p_s())));
            else
                Dm.append(ref dot, span_boldwarning(I18n.T("[] look[] severely burnt and heat-warped!", p_They(), p_s())));
        }
        if (Dm.num(health) < -Dm.num(maxHealth) * 0.5f)
            Dm.append(ref dot, span_warning(I18n.T("[] look[] barely operational.", p_They(), p_s())));
        if (Dm.num(fire_stacks) < 0)
            Dm.append(ref dot, span_warning(I18n.T("[] covered in water.", p_Theyre())));
        else if (Dm.num(fire_stacks) > 0)
            Dm.append(ref dot, span_warning(I18n.T("[] coated in something flammable.", p_Theyre())));

        if (Dm.truthy(opened))
            Dm.append(ref dot, span_warning(cell != null ? I18n.T("[] cover is open and the power cell is installed.", p_Their()) : I18n.T("[] cover is open and the power cell is missing.", p_Their())));
        else
        {
            string cover_message = I18n.T("[] cover is closed", p_Their());
            if (Dm.truthy(locked))
            {
                if (user == this)
                    cover_message += I18n.T(", though <a href='byond://?src=[];unlock_self=1'>you may unlock it</a>", REF(this));
            }
            else
                cover_message += I18n.T(", and looks unlocked");
            Dm.append(ref dot, span_notice(Dm.text("[].", cover_message)));
        }

        if (cell != null && Dm.num(cell__obj.charge) <= 0)
            Dm.append(ref dot, span_warning(I18n.T("[] battery indicator is blinking red!", p_Their())));

        if (IS_UNCONSCIOUS_AND_ALIVE(this))
            Dm.append(ref dot, span_warning(I18n.T("[] do[]n't seem to be responding.", p_They(), p_es())));

        switch (stat)
        {
            case STABLE:
                if (Dm.truthy(shell))
                    Dm.append(ref dot, Dm.truthy(deployed) ? I18n.T("[] appear[] to be an active AI shell.", p_They(), p_s()) : I18n.T("[] appear[] to be an empty AI shell.", p_They(), p_s()));
                else if (client == null)
                    Dm.append(ref dot, I18n.T("[] appear[] to be in stand-by mode.", p_They(), p_s())); //afk
                break;
            case DEAD:
                Dm.append(ref dot, span_deadsay(I18n.T("[] look[] like its system is corrupted and requires a reset.", p_They(), p_s())));
                break;
        }

        Dm.append(ref dot, await base.examine(user, __extra));
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/examine.dm:59
    public override object examine_descriptor(mob user = null)
    {
        return "cyborg";
    }
}
