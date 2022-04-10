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
        [SerializeField] GameObject count_obj = null;
        [SerializeField] TextMeshProUGUI count_text = null;

        Image icon;

        private void Start()
        {
            icon = GetComponent<Image>();
        }

        public void setItem(InventoryData item)
        {
            setItem(item, 0);
        }

        public void setItem(InventoryData item, int number)
        {
            if (item == null)
            {
                icon.enabled = false;
            }
            else
            {
                icon.enabled = true;
                icon.sprite = item.getIcon();
            }

            if (count_text)
            {
                if (number <= 1)
                {
                    count_obj.SetActive(false);
                }
                else
                {
                    count_obj.SetActive(true);
                    count_text.text = number.ToString();
                }
            }
        }
    }
}