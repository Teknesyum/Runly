using Runly.Core.Models;

namespace Runly.Settings;

/// <summary>The user's text and spacing scale (config <c>uiScale</c>). Fixed once per process, before
/// <see cref="Palette"/> builds its fonts; a change takes effect on the next start.</summary>
internal static class UiScale
{
    public static int Percent { get; private set; } = 100;

    public static float Factor => Percent / 100f;

    public static void Set(int percent) => Percent = RunlyConfig.NormalizeUiScale(percent);

    public static int Next(int percent)
    {
        var scales = RunlyConfig.UiScales;
        var index = -1;
        for (var i = 0; i < scales.Count; i++)
        {
            if (scales[i] == percent)
            {
                index = i;
            }
        }

        return scales[(index + 1) % scales.Count];
    }
}
