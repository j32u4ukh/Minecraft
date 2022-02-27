using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace minecraft
{
    public class PanelButton : MonoBehaviour
    {
        public Button button; 
        
        [SerializeField] private BlockType block_type = BlockType.DIRT;

        // Start is called before the first frame update
        void Start()
        {
            button.onClick.AddListener(()=> 
            {
                select();
            });
        }

        public void select()
        {
            Player.onBlockTypeChanged.Invoke(block_type);
        }
    }
}
