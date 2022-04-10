using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace udemy
{
    /// <summary>
    /// Allows a UI element to be dragged and dropped from and to a container.
    /// 
    /// Create a subclass for the type you want to be draggable. 
    /// Then place on the UI element you want to make draggable.
    /// 
    /// During dragging, the item is reparented to the parent canvas.
    /// 
    /// After the item is dropped it will be automatically return to the original UI parent. 
    /// It is the job of components implementing `IDragContainer`, `IDragDestination and `IDragSource` 
    /// to update the interface after a drag has occurred.
    /// </summary>
    /// <typeparam name="T">The type that represents the item being dragged.</typeparam>
    public class DragItem<T> : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler where T : class
    {
        // PRIVATE STATE
        Vector3 start_position;
        Transform original_parent;
        IDragSource<T> source;

        Canvas canvas;
        CanvasGroup canvas_group;

        private void Start()
        {
            source = GetComponentInParent<IDragSource<T>>();
            canvas = GetComponentInParent<Canvas>();
            canvas_group = GetComponent<CanvasGroup>();
        }

        #region 實作 IBeginDragHandler
        // 開始拖曳的瞬間
        void IBeginDragHandler.OnBeginDrag(PointerEventData eventData)
        {
            start_position = transform.position;
            original_parent = transform.parent;

            // 允許 Raycast 才能使用拖曳功能
            canvas_group.blocksRaycasts = false;

            // 設 Canvas 作為父物件
            transform.SetParent(canvas.transform, true);
        }
        #endregion

        #region 實作 IDragHandler
        // 拖曳物品中
        void IDragHandler.OnDrag(PointerEventData eventData)
        {
            transform.position = eventData.position;
        }
        #endregion

        #region 實作 IEndDragHandler
        // 從拖曳狀態下放開滑鼠的瞬間
        void IEndDragHandler.OnEndDrag(PointerEventData eventData)
        {
            // 相對於原父物件的位置
            transform.position = start_position;

            // 避免 UI 擋住 Raycast
            canvas_group.blocksRaycasts = true;

            // 還原父物件
            transform.SetParent(original_parent, true);

            IDragDestination<T> container;

            // 若 pointer 沒有在遊戲物件上
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                // 以 canvas 作為被拖曳物品的父物件(之後應在場景中生成相對應的 3D 物品)
                // PS: 並飛逝將 canvas 下的 2D 物品刪除，而是在前面就將 InventoryItem 的父物件還原成原本的欄位
                container = canvas.GetComponent<IDragDestination<T>>();
            }

            // 若 pointer 在遊戲物件上
            else
            {
                // 嘗試取得該物件的 IDragDestination 以放置被拖曳的物品
                container = getContainer(eventData);
            }

            // 若拖曳的終點不為 null，將被拖曳的物品放入 container
            if (container != null)
            {
                dropItemIntoContainer(container);
            }
        }

        /// <summary>
        /// 於 pointer 指向的物件與其父物件當中搜尋 IDragDestination，並返回含有 IDragDestination 的物件。
        /// 由於會一直往上層尋找，最終將會尋找到 canvas 並返回；除非指向的物件不屬於 UI，而是指向遊戲空間等地方。
        /// </summary>
        /// <param name="eventData"></param>
        /// <returns></returns>
        private IDragDestination<T> getContainer(PointerEventData eventData)
        {
            IDragDestination<T> container = null;

            if (eventData.pointerEnter)
            {
                // GetComponentInParent: 會優先判斷物體自身是否有目標組件，若有直接返回該組件，不遍歷父物件；
                // 若物體自身沒有目標組件，遍歷父物件，按照父物件順序查找（比如：先判斷上一層父物件，若沒有獲取到目標組件，再遍歷上上一層父物件(目標物體祖父物體)，以此遞歸查找）
                container = eventData.pointerEnter.GetComponentInParent<IDragDestination<T>>();
            }

            return container;
        }

        /// <summary>
        /// 物品移動到目標欄位，或與目標欄位的物品進行交換
        /// </summary>
        /// <param name="destination"></param>
        private void dropItemIntoContainer(IDragDestination<T> destination)
        {
            // 若移動的 終點 與 起點 相同，則直接返回
            if (ReferenceEquals(source, destination))
            {
                return;
            }

            // 將身為 IDragDestination 的 destination 轉型為 IDragContainer
            IDragContainer<T> destinationContainer = destination as IDragContainer<T>;

            // 將身為 IDragSource 的 source 轉型為 IDragContainer
            IDragContainer<T> sourceContainer = source as IDragContainer<T>;

            // IDragContainer 同時包含 IDragSource 和 IDragDestination，轉型有可能會失敗的原因為何？
            // attempt: 嘗試；因為 attemptSimpleTransfer 和 attemptSwap 都有可能因為條件不符而沒有執行，什麼都不做

            // Swap won't be possible
            // 轉型失敗、拖曳的目的地不在管理列表之內 或 移動的 終點 與 起點 所存放物品相同
            // 主要處理目標欄位空的，或是來源欄位的物品與目標欄位的物品，兩者相同的情況。
            if (destinationContainer == null ||
                sourceContainer == null ||
                destinationContainer.getItem() == null ||
                ReferenceEquals(destinationContainer.getItem(), sourceContainer.getItem()))
            {
                attemptSimpleTransfer(destination);
                return;
            }

            // 處理目標欄位不為空，或來源欄位的物品與目標欄位的物品，兩者不相同。兩個欄位的物品交換，或不滿足交換條件而什麼都不做
            attemptSwap(destinationContainer, sourceContainer);
        }

        /// <summary>
        /// 若移入目標欄位有空位可放物品，則將物品移入。根據欄位狀態，可能無法移入、部分移入或全部移入。
        /// 主要處理目標欄位空的，或是來源欄位的物品與目標欄位的物品，兩者相同的情況。
        /// </summary>
        /// <param name="destination"></param>
        /// <returns></returns>
        private bool attemptSimpleTransfer(IDragDestination<T> destination)
        {
            //Debug.Log($"[DragItem] attemptSimpleTransfer | destination: {destination}");

            // 被拖曳的物品
            T draggingItem = source.getItem();

            // 被拖曳的物品的數量
            int draggingNumber = source.getNumber();

            // 可放入欄位的物品數量
            int capacity = destination.getCapacity(draggingItem);

            // 最多可放入欄位的物品數量(不超過欄位本身的限制)
            int toTransfer = Mathf.Min(capacity, draggingNumber);

            // 可移入目標欄位
            if (toTransfer > 0)
            {
                // 來源欄位的物品數量減少 toTransfer 個
                source.removeItems(toTransfer);

                // 目標欄位的物品數量增加 toTransfer 個
                destination.addItems(draggingItem, toTransfer);

                //Debug.Log($"[DragItem] attemptSimpleTransfer | source: {source}, toTransfer: {toTransfer}");

                return false;
            }

            return true;
        }

        /// <summary>
        /// 交換兩欄位的物品。
        /// 處理目標欄位不為空，或來源欄位的物品與目標欄位的物品，兩者不相同。兩個欄位的物品交換，或不滿足交換條件而什麼都不做
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        private void attemptSwap(IDragContainer<T> destination, IDragContainer<T> source)
        {
            //Debug.Log($"[DragItem] attemptSimpleTransfer | destination: {destination}, source: {source}");

            #region 暫時將物品從兩邊都移除。Provisionally remove item from both sides.
            T source_item = source.getItem();
            int n_source = source.getNumber();

            T destination_item = destination.getItem();
            int n_destination = destination.getNumber();

            source.removeItems(n_source);
            destination.removeItems(n_destination);
            #endregion

            // 計算超出 destination 欄位容許數量的個數，將會被放回 source 欄位
            var n_back_to_source = calculateTakeBack(source_item, n_source, source, destination);

            // 計算超出 source 欄位容許數量的個數，將會被放回 destination 欄位
            var n_back_to_destination = calculateTakeBack(destination_item, n_destination, destination, source);

            // 有部分數量的物品需要被放回 source 欄位
            if (n_back_to_source > 0)
            {
                source.addItems(source_item, n_back_to_source);
                n_source -= n_back_to_source;
            }

            // 有部分數量的物品需要被放回 destination 欄位
            if (n_back_to_destination > 0)
            {
                destination.addItems(destination_item, n_back_to_destination);
                n_destination -= n_back_to_destination;
            }

            // 若其中一邊欄位的容許數量不足以完成物品的移入，則終止交換欄位的流程
            if (source.getCapacity(destination_item) < n_destination ||
                destination.getCapacity(source_item) < n_source)
            {
                // 將剩餘數量加回原本的欄位
                destination.addItems(destination_item, n_destination);
                source.addItems(source_item, n_source);

                return;
            }

            #region 將剩餘數量加入各自的目標欄位中
            if (n_destination > 0)
            {
                source.addItems(destination_item, n_destination);
            }

            if (n_source > 0)
            {
                destination.addItems(source_item, n_source);
            } 
            #endregion
        }

        /// <summary>
        /// 計算超出目標欄位容許數量的個數，將會被放回原本的欄位
        /// </summary>
        /// <param name="item"></param>
        /// <param name="n_moved"></param>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        private int calculateTakeBack(T item, int n_moved, IDragContainer<T> source, IDragContainer<T> destination)
        {
            var n_take_back = 0;

            // 目標欄位可移入數量
            var n_destination = destination.getCapacity(item);

            // 若 可移入數量 少於 要移入數量
            if (n_destination < n_moved)
            {
                // 有 takeBackNumber 個物品無法移入，將移回原本的欄位
                n_take_back = n_moved - n_destination;

                // 來源欄位的可移入數量
                var n_source = source.getCapacity(item);

                // Abort and reset
                // 來源欄位的可移入數量 少於 移回原本的欄位的數量(從該欄位移出再移回去，數量就超出限制？)
                if (n_source < n_take_back)
                {
                    return 0;
                }
            }

            return n_take_back;
        }
        #endregion
    }
}