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
        private void Start()
        {
            store = GameObject.FindGameObjectWithTag("Player").GetComponent<ActionStore>();
            store.storeUpdated += updateIcon;

            updateIcon();
        }

        #region 實作 IItemHolder
        public InventoryData getItem()
        {
            return store.getActionData(index);
        }
        #endregion

        /// <summary>
        /// 添加物品
        /// </summary>
        /// <param name="item"></param>
        /// <param name="number"></param>
        public void addItems(InventoryData item, int number)
        {
            store.addAction(item, index, number);
        }

        /// <summary>
        /// 取得欄位物品數量
        /// </summary>
        /// <returns></returns>
        public int getNumber()
        {
            return store.getNumber(index);
        }

        /// <summary>
        /// 取得該欄位可放多少該物品
        /// </summary>
        /// <param name="item">要放入的物品</param>
        /// <returns></returns>
        public int getCapacity(InventoryData item)
        {
            return store.getCapacity(item, index);
        }

        /// <summary>
        /// 移出物品
        /// </summary>
        /// <param name="number"></param>
        public void removeItems(int number)
        {
            store.removeItems(index, number);
        }

        /// <summary>
        /// 更新欄位的圖片與數量
        /// </summary>
        void updateIcon()
        {
            icon.setItem(getItem(), getNumber());
        }
    }
}
