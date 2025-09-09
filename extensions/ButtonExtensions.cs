namespace DrakeRenameit.Ext.UI;

using UnityEngine.Events;
using UnityEngine.UI;
using System.Linq;

public static class ButtonExtensions
{
    public static bool HasListener(this Button button, UnityAction call)
    {
        if (button == null || button.onClick == null)
            return false;

        // Compare method + target to avoid duplicates
        return button.onClick.GetPersistentEventCount() > 0;
    }

    // Helper to add only if missing
    public static void AddUniqueListener(this Button button, UnityAction call)
    {
        if (!button.HasListener(call))
        {
            button.onClick.AddListener(call);
        }
    }
}
