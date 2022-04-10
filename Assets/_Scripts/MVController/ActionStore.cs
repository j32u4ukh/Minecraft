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

        int GRID_SIZE = 9;
        int[] grid_indexs;

        // STATE
        Dictionary<int, DockedItemSlot> docked_items = new Dictionary<int, DockedItemSlot>();

        // PUBLIC

        /// <summary>
        /// Broadcasts when the items in the slots are added/removed.
        /// </summary>
        public event Action storeUpdated;

        private void Awake()
        {
            grid_indexs = new int[GRID_SIZE];

            int i, size0 = 9;

            for(i = 0; i < size0; i++)
            {
                grid_indexs[i] = i;
            }

            // TODO: 背包索引值或許將不會從 9 繼續編號
        }

        /// <summary>
        /// Get the action at the given index.
        /// </summary>
        public ActionData getActionData(int index)
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
        public int getNumber(int index)
        {
            if (docked_items.ContainsKey(index))
            {
                return docked_items[index].number;
            }

            return 0;
        }

        /// <summary>
        /// 自動尋找存放相同物品的欄位並放入；
        /// 若沒有現存物品，則尋找空格放入；
        /// 若沒有空格則會放入失敗。
        /// </summary>
        /// <param name="data">要放入的物品</param>
        /// <param name="number">要放入的數量</param>
        /// <returns>是否放入成功</returns>
        public bool addAction(InventoryData data, int number)
        {
            ActionData action_data = data as ActionData;

            // data 無法轉型為 ActionData
            if (!action_data)
            {
                return false;
            }

            int index = -1, capacity, n_added;
            bool need_update = false;

            foreach(int i in grid_indexs)
            {
                if (docked_items.ContainsKey(i))
                {
                    DockedItemSlot docked_item = docked_items[i];

                    if(ReferenceEquals(docked_item.item, data))
                    {
                        capacity = action_data.getCapacity() - docked_items[i].number;

                        // 若欄位容量大於 0，表示可以物品可移入此欄位
                        need_update = capacity > 0;

                        if (need_update)
                        {
                            // 考慮欄位容量 以及 要放入的數量，取得 實際放入的個數
                            n_added = Math.Min(capacity, number);

                            // 更新欄位內物品數量
                            docked_items[i].number += n_added;

                            // 更新要新增的物品數量
                            number -= n_added;
                        }
                    }
                }

                // 第 i 個欄位為空格 且 之前尚未發現空格(因此 index 仍維持 -1)
                else if (index.Equals(-1))
                {
                    index = i;
                }
            }

            // 若前面的步驟中還沒將要新增的物品放完，則將物品放入前一步驟中找到的空格
            if (!index.Equals(-1) && number > 0)
            {
                DockedItemSlot docked_item = new DockedItemSlot();
                docked_item.item = data as ActionData;
                docked_item.number = number;
                docked_items.Add(index, docked_item);
                need_update = true;
            }

            if (need_update)
            {
                storeUpdated?.Invoke();
            }

            return need_update;
        }

        /// <summary>
        /// Add an item to the given index.
        /// TODO: 自動找空格放入，若無空格則無法放入
        /// </summary>
        /// <param name="item">What item should be added.</param>
        /// <param name="index">Where should the item be added.</param>
        /// <param name="number">How many items to add.</param>
        public void addAction(InventoryData item, int index, int number)
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

            storeUpdated?.Invoke();
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
                    removeItems(index, 1);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Remove a given number of items from the given slot.
        /// 從指定欄位中移出物品，若數量歸 0，則從管理的字典中移除。
        /// </summary>
        /// <param name="index"></param>
        /// <param name="number"></param>
        public void removeItems(int index, int number)
        {
            if (docked_items.ContainsKey(index))
            {
                docked_items[index].number -= number;

                if (docked_items[index].number <= 0)
                {
                    docked_items.Remove(index);
                }

                storeUpdated?.Invoke();
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
        public int getCapacity(InventoryData item, int index)
        {
            ActionData action_item = item as ActionData;

            // actionItem 無法轉型為 ActionData
            if (!action_item)
            {
                return 0;
            }

            // index 指向的欄位為空，可放入數量等同於 item 在同一欄為最大容量
            if (!docked_items.ContainsKey(index))
            {
                return action_item.getCapacity();                
            }

            // index 指向的位置已放有物品，但要移入的物品與現有物品種類不同
            else if (!ReferenceEquals(action_item, docked_items[index].item))
            {
                return 0;
            }

            // index 指向的位置已放有物品，返回還可以放多少進去
            else
            {
                // item 在同一個欄位中的最大容量 扣掉 已存在的數量
                int capacity = action_item.getCapacity() - docked_items[index].number;

                return Math.Max(capacity, 0);
            }

            //// 若物品為消耗品，沒有存放上限
            //if (action_item.isConsumable())
            //{
            //    return int.MaxValue;
            //}

            //// index 指向的位置已放有物品，且非消耗品
            //if (docked_items.ContainsKey(index))
            //{
            //    return 0;
            //}

            //return 1;
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
                record.itemID = pair.Value.item.getID();
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
                addAction(InventoryData.getById(pair.Value.itemID), pair.Key, pair.Value.number);
            }
        }
    }
}