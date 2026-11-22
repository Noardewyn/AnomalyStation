// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/assistant/colorful_assistants.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M2 F1): the assistants' jumpsuit styles that give_jumpsuit picks from.

public static partial class GlobalProcs
{
    // tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:1
    public static object get_configured_colored_assistant_type()
    {
        return (Dm.truthy(config.Get(typeof(datum_config_entry_flag_grey_assistants))) ? typeof(datum_colored_assistant_grey) : typeof(datum_colored_assistant_random));
    }
}

/// Defines a style of jumpsuit/jumpskirt for assistants.
/// Jumpsuit and jumpskirt lists should match in colors, as they are used interchangably.
// tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:6
[DmPath("/datum/colored_assistant")]
public partial class datum_colored_assistant : datum
{
    // tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:7
    public DmList jumpsuits;
    // tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:8
    public DmList jumpskirts;
}

// tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:10
[DmPath("/datum/colored_assistant/grey")]
public partial class datum_colored_assistant_grey : datum_colored_assistant
{
    // tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:11-12  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        jumpsuits = Dm.list(typeof(obj_item_clothing_under_color_grey));
        jumpskirts = Dm.list(typeof(obj_item_clothing_under_color_jumpskirt_grey));
    }
}

// tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:14
[DmPath("/datum/colored_assistant/random")]
public partial class datum_colored_assistant_random : datum_colored_assistant
{
    // tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:15-16
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        jumpsuits = Dm.list(typeof(obj_item_clothing_under_color_random));
        jumpskirts = Dm.list(typeof(obj_item_clothing_under_color_jumpskirt_random));
    }
}

// tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:18
[DmPath("/datum/colored_assistant/christmas")]
public partial class datum_colored_assistant_christmas : datum_colored_assistant
{
    // tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:19-27
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        jumpsuits = Dm.list(
            typeof(obj_item_clothing_under_color_green),
            typeof(obj_item_clothing_under_color_red));

        jumpskirts = Dm.list(
            typeof(obj_item_clothing_under_color_jumpskirt_green),
            typeof(obj_item_clothing_under_color_jumpskirt_red));
    }
}

// tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:29
[DmPath("/datum/colored_assistant/mcdonalds")]
public partial class datum_colored_assistant_mcdonalds : datum_colored_assistant
{
    // tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:30-38
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        jumpsuits = Dm.list(
            typeof(obj_item_clothing_under_color_yellow),
            typeof(obj_item_clothing_under_color_red));

        jumpskirts = Dm.list(
            typeof(obj_item_clothing_under_color_jumpskirt_yellow),
            typeof(obj_item_clothing_under_color_jumpskirt_red));
    }
}

// tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:40
[DmPath("/datum/colored_assistant/halloween")]
public partial class datum_colored_assistant_halloween : datum_colored_assistant
{
    // tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:41-49
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        jumpsuits = Dm.list(
            typeof(obj_item_clothing_under_color_orange),
            typeof(obj_item_clothing_under_color_black));

        jumpskirts = Dm.list(
            typeof(obj_item_clothing_under_color_jumpskirt_orange),
            typeof(obj_item_clothing_under_color_jumpskirt_black));
    }
}

// tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:51
[DmPath("/datum/colored_assistant/ikea")]
public partial class datum_colored_assistant_ikea : datum_colored_assistant
{
    // tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:52-60
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        jumpsuits = Dm.list(
            typeof(obj_item_clothing_under_color_yellow),
            typeof(obj_item_clothing_under_color_blue));

        jumpskirts = Dm.list(
            typeof(obj_item_clothing_under_color_jumpskirt_yellow),
            typeof(obj_item_clothing_under_color_jumpskirt_blue));
    }
}

// tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:62
[DmPath("/datum/colored_assistant/mud")]
public partial class datum_colored_assistant_mud : datum_colored_assistant
{
    // tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:63-71
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        jumpsuits = Dm.list(
            typeof(obj_item_clothing_under_color_brown),
            typeof(obj_item_clothing_under_color_lightbrown));

        jumpskirts = Dm.list(
            typeof(obj_item_clothing_under_color_jumpskirt_brown),
            typeof(obj_item_clothing_under_color_jumpskirt_lightbrown));
    }
}

// tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:73
[DmPath("/datum/colored_assistant/warm")]
public partial class datum_colored_assistant_warm : datum_colored_assistant
{
    // tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:74-86
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        jumpsuits = Dm.list(
            typeof(obj_item_clothing_under_color_red),
            typeof(obj_item_clothing_under_color_pink),
            typeof(obj_item_clothing_under_color_orange),
            typeof(obj_item_clothing_under_color_yellow));

        jumpskirts = Dm.list(
            typeof(obj_item_clothing_under_color_jumpskirt_red),
            typeof(obj_item_clothing_under_color_jumpskirt_pink),
            typeof(obj_item_clothing_under_color_jumpskirt_orange),
            typeof(obj_item_clothing_under_color_jumpskirt_yellow));
    }
}

// tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:88
[DmPath("/datum/colored_assistant/cold")]
public partial class datum_colored_assistant_cold : datum_colored_assistant
{
    // tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:89-106
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        jumpsuits = Dm.list(
            typeof(obj_item_clothing_under_color_blue),
            typeof(obj_item_clothing_under_color_darkblue),
            typeof(obj_item_clothing_under_color_darkgreen),
            typeof(obj_item_clothing_under_color_green),
            typeof(obj_item_clothing_under_color_lightpurple),
            typeof(obj_item_clothing_under_color_teal));

        jumpskirts = Dm.list(
            typeof(obj_item_clothing_under_color_jumpskirt_blue),
            typeof(obj_item_clothing_under_color_jumpskirt_darkblue),
            typeof(obj_item_clothing_under_color_jumpskirt_darkgreen),
            typeof(obj_item_clothing_under_color_jumpskirt_green),
            typeof(obj_item_clothing_under_color_jumpskirt_lightpurple),
            typeof(obj_item_clothing_under_color_jumpskirt_teal));
    }
}

/// Will pick one color, and stick with it
// tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:108
[DmPath("/datum/colored_assistant/solid")]
public partial class datum_colored_assistant_solid : datum_colored_assistant
{
    // tg: code/modules/jobs/job_types/assistant/colorful_assistants.dm:110
    public override async DmTask New(params object[] __extra)
    {
        Type random_jumpsuit_type = get_random_jumpsuit() as Type;
        jumpsuits = Dm.list(random_jumpsuit_type);

        foreach (Type jumpskirt_type in Dm.each_cast<Type>(subtypesof(typeof(obj_item_clothing_under_color_jumpskirt))))
        {
            if (Dm.eq(Dm.initial(jumpskirt_type, (obj_item_clothing_under_color_jumpskirt p) => p.greyscale_colors), Dm.initial(random_jumpsuit_type, (obj_item_clothing_under_color p) => p.greyscale_colors)))
            {
                jumpskirts = Dm.list(jumpskirt_type);
                return;
            }
        }

        // Couldn't find a matching jumpskirt, oh well
        jumpskirts = Dm.list(get_random_jumpskirt());
    }
}
