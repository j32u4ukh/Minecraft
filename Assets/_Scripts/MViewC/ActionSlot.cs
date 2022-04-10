using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    /// <summary>
    /// The UI slot for the player action bar.
    /// ��M�צW�٬� ActionSlotUI
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

        #region ��@ IItemHolder
        public InventoryData getItem()
        {
            return store.getActionData(index);
        }
        #endregion

        /// <summary>
        /// �K�[���~
        /// </summary>
        /// <param name="item"></param>
        /// <param name="number"></param>
        public void addItems(InventoryData item, int number)
        {
            store.addAction(item, index, number);
        }

        /// <summary>
        /// ���o��쪫�~�ƶq
        /// </summary>
        /// <returns></returns>
        public int getNumber()
        {
            return store.getNumber(index);
        }

        /// <summary>
        /// ���o�����i��h�ָӪ��~
        /// </summary>
        /// <param name="item">�n��J�����~</param>
        /// <returns></returns>
        public int getCapacity(InventoryData item)
        {
            return store.getCapacity(item, index);
        }

        /// <summary>
        /// ���X���~
        /// </summary>
        /// <param name="number"></param>
        public void removeItems(int number)
        {
            store.removeItems(index, number);
        }

        /// <summary>
        /// ��s��쪺�Ϥ��P�ƶq
        /// </summary>
        void updateIcon()
        {
            icon.setItem(getItem(), getNumber());
        }
    }
}
