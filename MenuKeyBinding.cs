using DrakeRenameit.ModText;
using DrakeModsLibs.Input;
using static DrakeRenameit.ModText.RenameItLocalization;

namespace DrakeRenameit;

/// <summary>RenameIt menu chords; parser lives in <see cref="DrakeModsLibs.Input.MenuKeyBinding"/>.</summary>
public static class MenuKeyBinding
{
    public static bool IsHeld(string? binding) => DrakeModsLibs.Input.MenuKeyBinding.IsHeld(binding);

    public static string FormatForDisplay(string? binding) =>
        DrakeModsLibs.Input.MenuKeyBinding.FormatForDisplay(binding, T(LKeys.MenuKeyFallback));
}
