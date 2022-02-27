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

        // NOTE: ���a�ү�ϥΪ� Input System �Ҧb���w�q�A�קK�b�U�B���w�q�A�y���P�@�ӿ�J�����P���禡�QĲ�o
        void Update()
        {
            checkBlockSelected();
        }

        // �ˬd���Ӥ���Q���
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

            // NOTE: �ثe�O�Q��쪺 button �|�������o��A�����F�ƹ���A�C��N�|����
            // TODO: button �e���л\��ءA�Q��ܮɡA��ز��ʨ� button �e��
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
