using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrakeRenameit.API
{
    public static class RenameEvents
    {
        // Fired when an item name is changed
        public static event Action<Player, ItemDrop.ItemData, string, string> OnItemNameChanged;

        // Fired when description changes (optional)
        public static event Action<Player, ItemDrop.ItemData, string, string> OnItemDescriptionChanged;

        internal static void RaiseNameChanged(
            Player player,
            ItemDrop.ItemData item,
            string oldName,
            string newName)
        {
            OnItemNameChanged?.Invoke(player, item, oldName, newName);
        }

        internal static void RaiseDescriptionChanged(
            Player player,
            ItemDrop.ItemData item,
            string oldDesc,
            string newDesc)
        {
            OnItemDescriptionChanged?.Invoke(player, item, oldDesc, newDesc);
        }
    }

}
