using UnityEngine;

namespace udemy
{
    /// <summary>
    /// Handles spawning pickups when item dropped into the world.
    /// 
    /// Must be placed on the root canvas where items can be dragged. 
    /// Will be called if dropped over empty space. 
    /// </summary>
    public class InventoryDropTarget : MonoBehaviour, IDragDestination<InventoryData>
    {
        GameObject player;

        private void Start()
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        /// <summary>
        /// 當物品從物品欄被丟到場景中，呼叫 ItemDropper 的 DropItem 來生成掉落物
        /// </summary>
        /// <param name="item"></param>
        /// <param name="number"></param>
        public void AddItems(InventoryData item, int number)
        {
            Debug.Log($"[InventoryDropTarget] AddItems | item: {item}, number: {number}");
            //player.GetComponent<ItemDropper>().DropItem(item, number);
        }

        public int MaxAcceptable(InventoryData item)
        {
            return int.MaxValue;
        }
    }
}