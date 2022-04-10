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

        public void AddItems(InventoryData item, int number)
        {
            store.AddAction(item, index, number);
        }

        public InventoryData GetItem()
        {
            return store.GetAction(index);
        }

        public int GetNumber()
        {
            return store.GetNumber(index);
        }

        public int MaxAcceptable(InventoryData item)
        {
            return store.getAcceptableNumber(item, index);
        }

        public void RemoveItems(int number)
        {
            store.RemoveItems(index, number);
        }

        // PRIVATE

        void UpdateIcon()
        {
            icon.SetItem(GetItem(), GetNumber());
        }
    }
}
