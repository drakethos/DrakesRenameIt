using System;

namespace DrakeRenameit.Compat;

/// <summary>
/// Soft-dependency type lookup that stays quiet when the other mod isn't installed.
/// <c>AccessTools.TypeByName</c> logs a HarmonyX Warning for every miss, so a player without the optional ward / UI
/// mods saw a block of "Could not find type named …" warnings at every launch.
/// </summary>
internal static class CompatTypes
{
    internal static Type? Find(string fullName)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < assemblies.Length; i++)
        {
            try
            {
                var type = assemblies[i].GetType(fullName, throwOnError: false);
                if (type != null)
                    return type;
            }
            catch (Exception)
            {
                /* dynamic or reflection-only assemblies */
            }
        }

        return null;
    }
}
