using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    /// <summary>
    /// To be placed on a UI slot to spawn and show the correct item tooltip.
    /// TODO: 物品資訊(名稱)固定呈現在物品欄上方即可，不需要幫每個物品都生成
    /// </summary>
    [RequireComponent(typeof(IItemHolder))]
    public class ItemTooltipSpawner : TooltipSpawner
    {
        public override bool canCreateTooltip()
        {
            return GetComponent<IItemHolder>().getItem() != null;
        }

        public override void updateTooltip(GameObject obj)
        {
            if(obj.TryGetComponent(out ItemTooltip tooltip))
            {
                InventoryData item = GetComponent<IItemHolder>().getItem();

                if (item != null)
                {
                    tooltip.setup(item);
                }
            }
        }
    }
}