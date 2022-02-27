using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace minecraft
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
        GRASSTOP, GRASSSIDE, DIRT, WATER, STONE, LEAVES, WOOD, WOODBASE, FOREST, CACTUS, CACTUSBASE, SAND, GOLD, BEDROCK, REDSTONE, DIAMOND, NOCRACK,
        CRACK1, CRACK2, CRACK3, CRACK4, AIR
    };

    public enum CrackState
    {
        None = 0,
        Crack1 = 1, 
        Crack2 = 2, 
        Crack3 = 3, 
        Crack4 = 4
    }
}
