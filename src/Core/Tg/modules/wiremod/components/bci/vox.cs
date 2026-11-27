// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/bci/vox.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * # VOX Announcement Component
 *
 * These play a VOX announcement with inputed words from either a string or a list.
 * Requires a BCI shell.
 */
// tg: code/modules/wiremod/components/bci/vox.dm:8
public partial class obj_item_circuit_component_vox : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/bci/vox.dm:24
    public override void populate_options()
    {
        type_option = add_option_port("VOX Type", Dm.list(PORT_TYPE_LIST(PORT_TYPE_STRING), PORT_TYPE_STRING)) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/bci/vox.dm:27
    public override async DmTask populate_ports()
    {
        word_list = add_input_port("Word List", PORT_TYPE_LIST(PORT_TYPE_STRING)) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/bci/vox.dm:30
    public override async DmTask register_shell(atom_movable shell = null)
    {
        if (istype(shell, typeof(obj_item_organ_cyberimp_bci)))
            bci = shell as obj_item_organ_cyberimp_bci;
    }

    // tg: code/modules/wiremod/components/bci/vox.dm:34
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        bci = null;
    }

    // tg: code/modules/wiremod/components/bci/vox.dm:37
    public override void pre_input_received(datum_port_input port = null)
    {
        object current_option = type_option.value;
        if (!Dm.eq(current_type, current_option))
        {
            current_type = current_option;
            word_list.set_datatype(current_type);
        }
    }

    // tg: code/modules/wiremod/components/bci/vox.dm:43
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (bci == null)
            return null;

        mob_living owner = bci.owner;

        if (owner == null || !(owner is mob_living) || owner.client == null || !Dm.truthy(word_list.value))
            return null;

        if (Dm.eq(current_type, PORT_TYPE_STRING))
        {
            object words_list = splittext(trim(word_list.value), " ");

            foreach (object word in Dm.each(words_list))
                Waiting.NoSleep(play_vox_word(word, only_listener: owner));
        }
        else
        {
            foreach (object word in Dm.each(word_list.value))
                Waiting.NoSleep(play_vox_word(word, only_listener: owner));
        }
        return null;
    }
}
