// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/vending.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the whole file: the vendor's wires (throwing, the shock, the speaker and its language, the contraband, the ID scan) and
// the vendor's shock. The var block is generated.

// tg: code/datums/wires/vending.dm:1
public partial class datum_wires_vending : datum_wires
{
    // tg: code/datums/wires/vending.dm:7
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(
            WIRE_THROW, WIRE_SHOCK, WIRE_SPEAKER,
            WIRE_CONTRABAND, WIRE_IDSCAN
        );
        add_duds(1);

        obj_machinery_vending vending_machine = (obj_machinery_vending)holder;
        datum_language_holder vending_languages = vending_machine.get_language_holder();

        if (!Dm.truthy(length(vending_languages.spoken_languages)))
            throw Dm.CRASH(Dm.text("Vending machine [] does not have any spoken languages in its language holder.", vending_machine), "code/datums/wires/vending.dm", 18);

        // synch the current language to the language_iterator
        // (tg compares the selected language with each language's assoc value, a list, so this never matches and the iterator stays 1)
        foreach (object i in Dm.each(vending_languages.spoken_languages))
        {
            if (Dm.eq(vending_languages.selected_language, vending_languages.spoken_languages[i]))
            {
                language_iterator = Dm.num(i);
                break;
            }
        }
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/vending.dm:27
    public override async DmTask<object> interact(mob user)
    {
        obj_machinery_vending vending_machine = (obj_machinery_vending)holder;
        if (!HAS_SILICON_ACCESS(user) && vending_machine.seconds_electrified != 0 && Dm.truthy(await vending_machine.shock__proc(user as mob_living, 100)))
            return null;

        return await base.interact(user);
    }

    // tg: code/datums/wires/vending.dm:34
    public override bool interactable(mob user)
    {
        if (!base.interactable(user))
            return false;
        obj_machinery_vending vending_machine = (obj_machinery_vending)holder;
        if (!HAS_SILICON_ACCESS(user) && vending_machine.seconds_electrified != 0)
        {
            mob_living_carbon carbon_user = user as mob_living_carbon;
            if (carbon_user == null || Dm.truthy(carbon_user.should_electrocute(get_area(vending_machine))))
                return false;
        }
        if (Dm.truthy(vending_machine.panel_open))
            return true;
        return false;
    }

    // tg: code/datums/wires/vending.dm:45
    public override DmList get_status()
    {
        obj_machinery_vending vending_machine = (obj_machinery_vending)holder;
        datum_language_holder vending_languages = vending_machine.get_language_holder();
        datum_language current_language = GLOB.language_datum_instances[vending_languages.get_selected_language()] as datum_language;
        DmList status = Dm.list();
        Dm.append(ref status, I18n.T("The orange light is [].", vending_machine.seconds_electrified != 0 ? "on" : "off"));
        Dm.append(ref status, I18n.T("The red light is [].", Dm.truthy(vending_machine.shoot_inventory) ? "off" : "blinking"));
        Dm.append(ref status, I18n.T("The green light is [].", Dm.truthy(vending_machine.extended_inventory) ? "on" : "off"));
        Dm.append(ref status, I18n.T("A [] light is on.", Dm.truthy(vending_machine.scan_id) ? "purple" : "yellow"));
        Dm.append(ref status, I18n.T("A white light is [].", Dm.truthy(vending_machine.age_restrictions) ? "on" : "off"));
        Dm.append(ref status, I18n.T("The speaker light is []. The language is set to [].", Dm.truthy(vending_machine.shut_up) ? "off" : "on", current_language?.name));
        return status;
    }

    // tg: code/datums/wires/vending.dm:59
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_machinery_vending vending_machine = (obj_machinery_vending)holder;
        datum_language_holder vending_languages = vending_machine.get_language_holder();

        switch (wire)
        {
            case WIRE_THROW:
                vending_machine.shoot_inventory = !Dm.truthy(vending_machine.shoot_inventory) ? TRUE : FALSE;
                break;
            case WIRE_CONTRABAND:
                vending_machine.extended_inventory = !Dm.truthy(vending_machine.extended_inventory) ? TRUE : FALSE;
                break;
            case WIRE_SHOCK:
                vending_machine.seconds_electrified = MACHINE_DEFAULT_ELECTRIFY_TIME;
                await vending_machine.shock__proc(usr as mob_living, 100);
                break;
            case WIRE_IDSCAN:
                vending_machine.scan_id = !Dm.truthy(vending_machine.scan_id) ? TRUE : FALSE;
                break;
            case WIRE_SPEAKER:
                language_iterator = Dm.num(Dm.mod(language_iterator, length(vending_languages.spoken_languages)));
                language_iterator += 1;
                vending_languages.selected_language = vending_languages.spoken_languages[language_iterator];
                break;
            case WIRE_AGELIMIT:
                vending_machine.age_restrictions = !Dm.truthy(vending_machine.age_restrictions) ? TRUE : FALSE;
                break;
        }
    }

    // tg: code/datums/wires/vending.dm:80
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        obj_machinery_vending vending_machine = (obj_machinery_vending)holder;
        switch (wire)
        {
            case WIRE_THROW:
                vending_machine.shoot_inventory = !Dm.truthy(mend) ? TRUE : FALSE;
                break;
            case WIRE_CONTRABAND:
                vending_machine.extended_inventory = FALSE;
                break;
            case WIRE_SHOCK:
                if (Dm.truthy(mend))
                    vending_machine.seconds_electrified = MACHINE_NOT_ELECTRIFIED;
                else
                {
                    vending_machine.seconds_electrified = MACHINE_ELECTRIFIED_PERMANENT;
                    await vending_machine.shock__proc(usr as mob_living, 100);
                }
                break;
            case WIRE_IDSCAN:
                vending_machine.scan_id = Dm.num(mend);
                break;
            case WIRE_SPEAKER:
                vending_machine.shut_up = Dm.num(mend);
                break;
        }
    }
}

// tg: code/modules/vending/vendor/_vending.dm:37
public partial class obj_machinery_vending
{
    // tg: code/datums/wires/vending.dm:97
    public override async DmTask<object> shock__proc(mob_living shocking = null, float? chance = null, object shock_source = null, float? siemens_coeff = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
            return FALSE;
        if (siemens_coeff == null)
            siemens_coeff = 0.7f;
        return await base.shock__proc(shocking, chance, shock_source, siemens_coeff);
    }
}
