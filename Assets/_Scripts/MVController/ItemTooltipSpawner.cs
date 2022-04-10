using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    /// <summary>
    /// To be placed on a UI slot to spawn and show the correct item tooltip.
    /// TODO: ���~��T(�W��)�T�w�e�{�b���~��W��Y�i�A���ݭn���C�Ӫ��~���ͦ�
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