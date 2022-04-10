using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace udemy
{
    /// <summary>
    /// To be put on the icon representing an inventory item. 
    /// Allows the slot to update the icon and number.
    /// 原專案名稱為 InventoryItemIcon
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class InventoryIcon : MonoBehaviour
    {
        // CONFIG DATA
        [SerializeField] GameObject textContainer = null;
        [SerializeField] TextMeshProUGUI itemNumber = null;

        // PUBLIC

        public void SetItem(InventoryData item)
        {
            SetItem(item, 0);
        }

        public void SetItem(InventoryData item, int number)
        {
            var iconImage = GetComponent<Image>();

            if (item == null)
            {
                iconImage.enabled = false;
            }
            else
            {
                iconImage.enabled = true;
                iconImage.sprite = item.GetIcon();
            }

            if (itemNumber)
            {
                if (number <= 1)
                {
                    textContainer.SetActive(false);
                }
                else
                {
                    textContainer.SetActive(true);
                    itemNumber.text = number.ToString();
                }
            }
        }
    }
}