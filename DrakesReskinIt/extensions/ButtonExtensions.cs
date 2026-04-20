namespace DrakesReskinIt.Ext.UI;

using UnityEngine.Events;
using UnityEngine.UI;

public static class ButtonExtensions
{
    public static bool HasListener(this Button button, UnityAction call)
    {
        if (button == null || button.onClick == null)
            return false;
        return button.onClick.GetPersistentEventCount() > 0;
    }

    public static void AddUniqueListener(this Button button, UnityAction call)
    {
        if (!button.HasListener(call))
            button.onClick.AddListener(call);
    }
}
