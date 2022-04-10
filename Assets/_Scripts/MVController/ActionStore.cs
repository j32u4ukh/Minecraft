using System;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    /// <summary>
    /// Provides the storage for an action bar. The bar has a finite number of
    /// slots that can be filled and actions in the slots can be "used".
    /// 
    /// This component should be placed on the GameObject tagged "Player".
    /// </summary>
    public class ActionStore : MonoBehaviour, ISaveable
    {
        private class DockedItemSlot
        {
            public ActionData item;
            public int number;
        }

        // STATE
        Dictionary<int, DockedItemSlot> docked_items = new Dictionary<int, DockedItemSlot>();

        // PUBLIC

        /// <summary>
        /// Broadcasts when the items in the slots are added/removed.
        /// </summary>
        public event Action storeUpdated;

        /// <summary>
        /// Get the action at the given index.
        /// </summary>
        public ActionData GetAction(int index)
        {
            if (docked_items.ContainsKey(index))
            {
                return docked_items[index].item;
            }

            return null;
        }

        /// <summary>
        /// Get the number of items left at the given index.
        /// </summary>
        /// <returns>
        /// Will return 0 if no item is in the index or the item has
        /// been fully consumed.
        /// </returns>
        public int GetNumber(int index)
        {
            if (docked_items.ContainsKey(index))
            {
                return docked_items[index].number;
            }

            return 0;
        }

        /// <summary>
        /// Add an item to the given index.
        /// TODO: 自動找空格放入，若無空格則無法放入
        /// </summary>
        /// <param name="item">What item should be added.</param>
        /// <param name="index">Where should the item be added.</param>
        /// <param name="number">How many items to add.</param>
        public void AddAction(InventoryData item, int index, int number)
        {
            if (docked_items.ContainsKey(index))
            {
                if (ReferenceEquals(item, docked_items[index].item))
                {
                    docked_items[index].number += number;
                }
            }
            else
            {
                DockedItemSlot slot = new DockedItemSlot();
                slot.item = item as ActionData;
                slot.number = number;

                docked_items.Add(index, slot);
            }

            if (storeUpdated != null)
            {
                storeUpdated();
            }
        }

        /// <summary>
        /// Use the item at the given slot. If the item is consumable one
        /// instance will be destroyed until the item is removed completely.
        /// </summary>
        /// <param name="user">The character that wants to use this action.</param>
        /// <returns>False if the action could not be executed.</returns>
        public bool Use(int index, GameObject user)
        {
            if (docked_items.ContainsKey(index))
            {
                docked_items[index].item.Use(user);
                if (docked_items[index].item.isConsumable())
                {
                    RemoveItems(index, 1);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Remove a given number of items from the given slot.
        /// </summary>
        public void RemoveItems(int index, int number)
        {
            if (docked_items.ContainsKey(index))
            {
                docked_items[index].number -= number;
                if (docked_items[index].number <= 0)
                {
                    docked_items.Remove(index);
                }
                if (storeUpdated != null)
                {
                    storeUpdated();
                }
            }

        }

        /// <summary>
        /// What is the maximum number of items allowed in this slot.
        /// 
        /// 相同物品且為消耗品，才能累加疊放；否則都只能放一個物品。
        /// TODO: 大部分都能疊加，根據不同物品會有不同的疊加數量上限
        /// This takes into account whether the slot already contains an item and whether it is the same type. 
        /// Will only accept multiple if the item is consumable.
        /// </summary>
        /// <returns>Will return int.MaxValue when there is not effective bound.</returns>
        public int getAcceptableNumber(InventoryData item, int index)
        {
            var action_item = item as ActionData;

            // actionItem 無法轉型為 ActionData
            if (!action_item)
            {
                return 0;
            }

            // index 指向的位置已放有物品，但要移入的物品與現有物品種類不同
            if (docked_items.ContainsKey(index) && !ReferenceEquals(item, docked_items[index].item))
            {
                return 0;
            }

            // 若物品為消耗品，沒有存放上限
            if (action_item.isConsumable())
            {
                return int.MaxValue;
            }

            // index 指向的位置已放有物品，且非消耗品
            if (docked_items.ContainsKey(index))
            {
                return 0;
            }

            return 1;
        }

        /// PRIVATE

        [System.Serializable]
        private struct DockedItemRecord
        {
            public string itemID;
            public int number;
        }

        object ISaveable.CaptureState()
        {
            var state = new Dictionary<int, DockedItemRecord>();

            foreach (var pair in docked_items)
            {
                var record = new DockedItemRecord();
                record.itemID = pair.Value.item.GetItemID();
                record.number = pair.Value.number;
                state[pair.Key] = record;
            }

            return state;
        }

        void ISaveable.RestoreState(object state)
        {
            var stateDict = (Dictionary<int, DockedItemRecord>)state;

            foreach (var pair in stateDict)
            {
                AddAction(InventoryData.getById(pair.Value.itemID), pair.Key, pair.Value.number);
            }
        }
    }
}