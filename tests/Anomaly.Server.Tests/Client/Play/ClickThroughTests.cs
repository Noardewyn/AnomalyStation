using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// Package T (with slice T2): tg's short-lived visuals are mouse-transparent (<c>mouse_opacity = MOUSE_OPACITY_TRANSPARENT</c>), which is not
/// replicated, so the client knows them by type path and its pick shapes leave them out: a click goes to the person behind the hit effect.
/// </summary>
public class ClickThroughTests
{
    [Theory]
    [InlineData("/atom/movable/flick_visual", true)] // the attack's picture over the target (do_item_attack_animation, flick_overlay_view)
    [InlineData("/obj/effect/temp_visual", true)]
    [InlineData("/obj/effect/temp_visual/dir_setting/bloodsplatter", true)]
    [InlineData("/obj/effect/temp_visual/impact_effect", true)]
    [InlineData("/obj/effect/projectile", true)]
    [InlineData("/obj/effect/projectile/impact/laser", true)]
    [InlineData("/obj/effect/temp_visual_x", false)] // a sibling, not a subtype
    [InlineData("/obj/effect/temp", false)]
    [InlineData("/atom/movable/flick_visual_other", false)]
    [InlineData("/obj/effect/projectile_lighting", false)]
    [InlineData("/obj/effect/constructing_effect", false)] // MOUSE_OPACITY_OPAQUE in tg
    [InlineData("/obj/item/crowbar", false)]
    [InlineData("/mob/living/carbon/human", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TgsShortLivedVisualsAreClickedThrough(string typePath, bool through) => Assert.Equal(through, ClickThrough.IsType(typePath));
}
