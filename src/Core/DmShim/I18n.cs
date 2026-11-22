namespace Anomaly.Core.DmShim;

/// <summary>
/// Player-visible text (docs/ai/translation-rules.md 5.3, [C0]): every player-visible DM text, literal or
/// interpolated, is <c>I18n.T(template, args...)</c> with the template built as for <c>Dm.text</c>. It returns the
/// same English string as <c>Dm.text</c>, so core logic keeps comparing and storing plain English; the template is
/// the localization key and the arguments are its parameters (D17, D75). How keys reach the client is decided in
/// M9 without changing this call form.
/// </summary>
public static class I18n
{
    /// <summary>
    /// The English text of <paramref name="template"/> with <paramref name="args"/>: exactly
    /// <c>Dm.text(template, args)</c>. A template with no arguments, no <c>[</c> and no backslash is returned as is.
    /// </summary>
    public static string T(string template, params object[] args)
    {
        if (args != null && args.Length == 0 && template != null && template.IndexOf('[') < 0 && template.IndexOf('\\') < 0)
            return template;
        return Dm.text(template, args);
    }
}
