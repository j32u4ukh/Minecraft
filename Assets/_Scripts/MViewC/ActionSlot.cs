using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    /// <summary>
    /// The UI slot for the player action bar.
    /// 原專案名稱為 ActionSlotUI
    /// </summary>
    public class ActionSlot : MonoBehaviour, IItemHolder, IDragContainer<InventoryData>
    {
        // CONFIG DATA
        [SerializeField] InventoryIcon icon = null;
        [SerializeField] int index = 0;

        // CACHE
        ActionStore store;

        // LIFECYCLE METHODS
        private void Awake()
        {
            store = GameObject.FindGameObjectWithTag("Player").GetComponent<ActionStore>();
            store.storeUpdated += UpdateIcon;
        }

        // PUBLIC

        public void addItems(InventoryData item, int number)
        {
            store.addAction(item, index, number);
        }

        public InventoryData getItem()
        {
            return store.GetAction(index);
        }

        public int getNumber()
        {
            return store.GetNumber(index);
        }

        public int getCapacity(InventoryData item)
        {
            return store.getCapacity(item, index);
        }

        public void removeItems(int number)
        {
            store.RemoveItems(index, number);
        }

        // PRIVATE

        void UpdateIcon()
        {
            icon.SetItem(getItem(), getNumber());
        }
    }
}
