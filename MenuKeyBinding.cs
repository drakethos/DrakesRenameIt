using DrakeRenameit.ModText;
using DrakesWorkshopLibs.Input;
using static DrakeRenameit.ModText.RenameItLocalization;

namespace DrakeRenameit;

/// <summary>RenameIt menu chords; parser lives in <see cref="DrakesWorkshopLibs.Input.MenuKeyBinding"/>.</summary>
public static class MenuKeyBinding
{
    public static bool IsHeld(string? binding) => DrakesWorkshopLibs.Input.MenuKeyBinding.IsHeld(binding);

    public static string FormatForDisplay(string? binding) =>
        DrakesWorkshopLibs.Input.MenuKeyBinding.FormatForDisplay(binding, T(LKeys.MenuKeyFallback));
}
