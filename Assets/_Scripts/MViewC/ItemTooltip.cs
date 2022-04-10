using UnityEngine;
using TMPro;

namespace udemy
{
    /// <summary>
    /// Root of the tooltip prefab to expose properties to other classes.
    /// </summary>
    public class ItemTooltip : MonoBehaviour
    {
        // CONFIG DATA
        [SerializeField] TextMeshProUGUI title_text = null;
        [SerializeField] TextMeshProUGUI body_text = null;

        public void setup(InventoryData item)
        {
            title_text.text = item.getDisplayName();
            body_text.text = item.getDescription();
        }
    }
}
