using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace minecraft
{
    public class Player : MonoBehaviour
    {
        public PanelButton[] buttons;
        private int button_index;

        public BlockType block_type;

        public static BlockTypeEvent onBlockTypeChanged = new BlockTypeEvent();

        // KeyCode.Alpha1 = 49
        private int KEY_OFFSET = 49;

        // Start is called before the first frame update
        void Start()
        {
            onBlockTypeChanged.AddListener(onBlockTypeChangedListener);

            if(buttons != null)
            {
                // If there are, select the first one
                button_index = 0;
                buttons[button_index].button.Select();
                buttons[button_index].button.onClick.Invoke();
            }
        }

        // NOTE: 玩家所能使用的 Input System 皆在此定義，避免在各處都定義，造成同一個輸入有不同的函式被觸發
        void Update()
        {
            checkBlockSelected();
        }

        // 檢查哪個方塊被選擇
        void checkBlockSelected()
        {
            int key;

            for(key = 0; key < 4; key++)
            {
                // Alpha1 ~ Alpha4
                if (Input.GetKeyDown((KeyCode)(key + KEY_OFFSET)))
                {
                    button_index = key;
                    buttons[button_index].button.Select();
                    buttons[button_index].button.onClick.Invoke();
                    return;
                }
            }

            // NOTE: 目前是被選到的 button 會有紅色濾鏡，但按了滑鼠後，顏色就會消失
            // TODO: button 前方覆蓋方框，被選擇時，方框移動到 button 前面
            if (Input.GetKeyDown(KeyCode.Tab) && buttons.Length > 1)
            {
                // If there are, check if either shift key is being pressed
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    // If shift is pressed, move up on the list - or, if at the top of the list, move to the bottom
                    if (button_index <= 0)
                    {
                        button_index = buttons.Length;
                    }

                    button_index--;
                    buttons[button_index].button.Select();
                    buttons[button_index].button.onClick.Invoke();
                }
                else
                {
                    //if shift is not pressed, move down on the list - or, if at the bottom, move to the top
                    if (buttons.Length <= button_index + 1)
                    {
                        button_index = -1;
                    }

                    button_index++;
                    buttons[button_index].button.Select();
                    buttons[button_index].button.onClick.Invoke();
                }
            }
        }

        void onBlockTypeChangedListener(BlockType block_type)
        {
            this.block_type = block_type;
        }

        public BlockType getBlockType()
        {
            return block_type;
        }
    }
}
