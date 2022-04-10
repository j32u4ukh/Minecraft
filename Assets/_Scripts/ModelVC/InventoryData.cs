using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    /// <summary>
    /// A ScriptableObject that represents any item that can be put in an
    /// inventory.
    /// 原專案名稱為 InventoryItem
    /// </summary>
    /// <remarks>
    /// In practice, you are likely to use a subclass such as `ActionItem` or
    /// `EquipableItem`.
    /// </remarks>
    public abstract class InventoryData : ScriptableObject, ISerializationCallbackReceiver
    {
        #region CONFIG DATA
        [Tooltip("Auto-generated UUID for saving/loading. Clear this field if you want to generate a new one.")]
        [SerializeField] string id = null;

        [Tooltip("Item name to be displayed in UI.")]
        [SerializeField] string display_name = null;

        [Tooltip("Item description to be displayed in UI.")]
        [SerializeField] [TextArea] string description = null;

        [Tooltip("The UI icon to represent this item in the inventory.")]
        [SerializeField] Sprite icon = null;

        [Tooltip("The prefab that should be spawned when this item is dropped.")]
        [SerializeField] Pickup pickup = null;

        [Tooltip("If true, multiple items of this type can be stacked in the same inventory slot.")]
        [SerializeField] bool stackable = false; 
        #endregion

        // STATE
        static Dictionary<string, InventoryData> inventories;

        // PUBLIC

        /// <summary>
        /// Get the inventory item instance from its UUID.
        /// </summary>
        /// <param name="id">
        /// String UUID that persists between game instances.
        /// </param>
        /// <returns>
        /// Inventory item instance corresponding to the ID.
        /// </returns>
        public static InventoryData getById(string id)
        {
            if (inventories == null)
            {
                inventories = new Dictionary<string, InventoryData>();

                foreach (var item in Resources.LoadAll<InventoryData>(""))
                {
                    if (inventories.ContainsKey(item.id))
                    {
                        // TODO: 實作 ToString 以協助區分不同檔案
                        Debug.LogError($"Looks like there's a duplicate ID for objects: {inventories[item.id]} and {item}");
                        continue;
                    }

                    inventories.Add(item.id, item);
                }
            }

            if (id == null || !inventories.ContainsKey(id))
            {
                return null;
            }

            return inventories[id];
        }

        /// <summary>
        /// Spawn the pickup gameobject into the world.
        /// </summary>
        /// <param name="position">Where to spawn the pickup.</param>
        /// <param name="number">How many instances of the item does the pickup represent.</param>
        /// <returns>Reference to the pickup object spawned.</returns>
        public Pickup SpawnPickup(Vector3 position, int number)
        {
            var pickup = Instantiate(this.pickup);
            pickup.transform.position = position;
            pickup.Setup(this, number);
            return pickup;
        }

        public Sprite getIcon()
        {
            return icon;
        }

        public string getID()
        {
            return id;
        }

        public bool IsStackable()
        {
            return stackable;
        }

        public string getDisplayName()
        {
            return display_name;
        }

        public string getDescription()
        {
            return description;
        }

        // PRIVATE

        #region 實作 ISerializationCallbackReceiver
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            // Generate and save a new UUID if this is blank.
            if (string.IsNullOrWhiteSpace(id))
            {
                id = System.Guid.NewGuid().ToString();
            }
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            // Require by the ISerializationCallbackReceiver but we don't need
            // to do anything with it.
        } 
        #endregion
    }
}
