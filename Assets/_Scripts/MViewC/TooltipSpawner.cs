using UnityEngine;
using UnityEngine.EventSystems;

namespace udemy
{
    /// <summary>
    /// Abstract base class that handles the spawning of a tooltip prefab at the
    /// correct position on screen relative to a cursor.
    /// 
    /// Override the abstract functions to create a tooltip spawner for your own
    /// data.
    /// </summary>
    public abstract class TooltipSpawner : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        // CONFIG DATA
        [Tooltip("The prefab of the tooltip to spawn.")]
        [SerializeField] GameObject prefab = null;

        // PRIVATE STATE
        GameObject tooltip = null;
        RectTransform tooltip_rect_transform;

        Canvas canvas;
        RectTransform rect_transform;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            rect_transform = GetComponent<RectTransform>();
        }

        /// <summary>
        /// Called when it is time to update the information on the tooltip
        /// prefab.
        /// </summary>
        /// <param name="obj">
        /// The spawned tooltip prefab for updating.
        /// </param>
        public abstract void updateTooltip(GameObject obj);

        /// <summary>
        /// Return true when the tooltip spawner should be allowed to create a tooltip.
        /// 是否可產生提示
        /// </summary>
        public abstract bool canCreateTooltip();

        // PRIVATE

        private void OnDestroy()
        {
            clearTooltip();
        }

        private void OnDisable()
        {
            clearTooltip();
        }

        #region 實作 IPointerEnterHandler
        /// <summary>
        /// 當滑鼠移入
        /// </summary>
        /// <param name="eventData"></param>
        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
        {
            if (tooltip && !canCreateTooltip())
            {
                clearTooltip();
            }

            if (!tooltip && canCreateTooltip())
            {
                tooltip = Instantiate(prefab, canvas.transform);
                tooltip_rect_transform = tooltip.GetComponent<RectTransform>();
            }

            if (tooltip)
            {
                updateTooltip(tooltip);
                locateTooltip();
            }
        }

        /// <summary>
        /// 定位提示位置
        /// </summary>
        private void locateTooltip()
        {
            // Required to ensure corners are updated by positioning elements.
            Canvas.ForceUpdateCanvases();

            Vector3[] tooltip_corners = new Vector3[4];
            tooltip_rect_transform.GetWorldCorners(tooltip_corners);

            Vector3[] slot_corners = new Vector3[4];
            rect_transform.GetWorldCorners(slot_corners);

            bool below = transform.position.y > Screen.height / 2;
            bool right = transform.position.x < Screen.width / 2;

            int slot_corner = getCornerIndex(below, right);
            int tooltip_corner = getCornerIndex(!below, !right);

            tooltip.transform.position = slot_corners[slot_corner] - tooltip_corners[tooltip_corner] + tooltip.transform.position;
        }

        /// <summary>
        /// 根據角落的位置，取得角落對應的索引值
        /// </summary>
        /// <param name="below"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        private int getCornerIndex(bool below, bool right)
        {
            if (below && !right)
            {
                return 0;
            }
            else if (!below && !right)
            {
                return 1;
            }
            else if (!below && right)
            {
                return 2;
            }
            else
            {
                return 3;
            }
        }
        #endregion


        #region 實作 IPointerExitHandler
        /// <summary>
        /// 當滑鼠離開
        /// </summary>
        /// <param name="eventData"></param>
        void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
        {
            clearTooltip();
        } 
        #endregion

        /// <summary>
        /// 刪除現有提示
        /// </summary>
        private void clearTooltip()
        {
            if (tooltip)
            {
                Destroy(tooltip.gameObject);
            }
        }
    }
}