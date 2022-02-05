using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    public enum BlockSide
    {
        Bottom,
        Top,
        Left,
        Right,
        Front,
        Back
    }

    public enum BlockType
    {
        GRASSTOP, GRASSSIDE, DIRT, WATER, STONE, LEAVES, WOOD, WOODBASE, FOREST, CACTUS, SAND, GOLD, BEDROCK, REDSTONE, DIAMOND, NOCRACK,
        CRACK1, CRACK2, CRACK3, CRACK4, AIR
    };

    public enum CrackState
    {
        None,
        Crack1, 
        Crack2, 
        Crack3, 
        Crack4
    }
}
