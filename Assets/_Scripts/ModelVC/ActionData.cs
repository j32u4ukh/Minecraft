using System;
using UnityEngine;

namespace udemy
{
    /// <summary>
    /// An inventory item that can be placed in the action bar and "Used".
    /// </summary>
    /// <remarks>
    /// This class should be used as a base. Subclasses must implement the `Use`
    /// method.
    /// </remarks>
    [CreateAssetMenu(menuName = "ComponentData/Action Item")]
    public class ActionData : InventoryData
    {
        // CONFIG DATA
        [Tooltip("Does an instance of this item get consumed every time it's used.")]
        [SerializeField] bool consumable = false;

        [Tooltip("同一個欄位中的最大容量")]
        [SerializeField] int capacity = 1;

        // PUBLIC

        /// <summary>
        /// Trigger the use of this item. Override to provide functionality.
        /// </summary>
        /// <param name="user">The character that is using this action.</param>
        public virtual void Use(GameObject user)
        {
            Debug.Log("Using action: " + this);
        }

        public bool isConsumable()
        {
            return consumable;
        }

        /// <summary>
        /// 同一個欄位中的最大容量
        /// </summary>
        /// <returns></returns>
        public int getCapacity()
        {
            return capacity;
        }
    }
}