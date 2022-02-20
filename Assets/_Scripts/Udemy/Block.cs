using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    public class Block
    {
        public Mesh mesh;

        [Obsolete("提供在 Chunk 中建立 Block 時的協助，延後添加 Mesh 的時間點，而非在建構子當中添加 Mesh")]
        public Block() { }

        public Block(BlockType block_type, CrackState crack_state, Vector3Int offset, Chunk chunk, Chunk up, Chunk down, Chunk left, Chunk right, Chunk forward, Chunk back)
        {
            if (block_type == BlockType.AIR)
            {
                return;
            }

            List<Quad> quads = new List<Quad>();
            Vector3Int xyz = offset - chunk.location;

            BlockSide[] sides = new BlockSide[] { BlockSide.Top, BlockSide.Bottom,
                                                  BlockSide.Left, BlockSide.Right,
                                                  BlockSide.Front, BlockSide.Back };

            //quads.Add(new Quad(block_type, BlockSide.Bottom, offset));
            //quads.Add(new Quad(block_type, BlockSide.Top, offset));
            //quads.Add(new Quad(block_type, BlockSide.Left, offset));
            //quads.Add(new Quad(block_type, BlockSide.Right, offset));
            //quads.Add(new Quad(block_type, BlockSide.Front, offset));
            //quads.Add(new Quad(block_type, BlockSide.Back, offset));

            /* 利用 hasSolidNeighbour 檢查各個方向是否還有下一格，因此傳入的座標為該方向下一格 Block 的座標 */
            (bool is_inside, int nx, int ny, int nz) info;
            Chunk neighbour_chunk = null;

            foreach (BlockSide side in sides)
            {
                info = getNeighbourInfo(xyz.x, xyz.y, xyz.z, side: side);

                // Chunk 內檢查 Neighbour
                if (info.is_inside)
                {
                    // 旁邊沒有被擋住/有生成這面 Mesh 的必要
                    if (!hasNeighbour(chunk, info.nx, info.ny, info.nz, block_type))
                    {
                        quads.Add(createQuad(side: side, block_type: block_type, crack_state: crack_state, offset: offset));
                    }
                }

                // Chunk 之間檢查 Neighbour
                else
                {
                    switch (side)
                    {
                        case BlockSide.Right:
                            neighbour_chunk = right;
                            break;
                        case BlockSide.Left:
                            neighbour_chunk = left;
                            break;
                        case BlockSide.Top:
                            neighbour_chunk = up;
                            break;
                        case BlockSide.Bottom:
                            neighbour_chunk = down;
                            break;
                        case BlockSide.Front:
                            neighbour_chunk = forward;
                            break;
                        case BlockSide.Back:
                            neighbour_chunk = back;
                            break;
                    }

                    if (neighbour_chunk != null)
                    {
                        // 旁邊沒有被擋住/有生成這面 Mesh 的必要
                        if (!hasNeighbour(neighbour_chunk, info.nx, info.ny, info.nz, block_type))
                        {
                            quads.Add(createQuad(side: side, block_type: block_type, crack_state: crack_state, offset: offset));
                        }
                    }
                }
            }

            if (quads.Count == 0)
            {
                return;
            }

            List<Mesh> meshes = new List<Mesh>();

            foreach (Quad quad in quads)
            {
                meshes.Add(quad.mesh);
            }

            mesh = MeshUtils.mergeMeshes(meshes);
            mesh.name = $"Block_{offset.x}_{offset.y}_{offset.z}";
        }

        bool hasNeighbour(Chunk chunk, int x, int y, int z, BlockType block_type)
        {
            int block_idx = World.xyzToFlat(x, y, z);

            if (chunk.getBlockType(block_idx).Equals(block_type))
            {
                return true;
            }

            if (chunk.getBlockType(block_idx).Equals(BlockType.AIR) || chunk.getBlockType(block_idx).Equals(BlockType.WATER))
            {
                return false;
            }

            return true;
        }

        static (bool is_inside, int nx, int ny, int nz) getNeighbourInfo(int bx, int by, int bz, BlockSide side)
        {
            bool is_inside = true;

            switch (side)
            {
                case BlockSide.Right:
                    bx += 1;

                    if (bx >= World.chunk_dimensions.x)
                    {
                        is_inside = false;
                        bx = 0;
                    }
                    break;

                case BlockSide.Left:
                    bx -= 1;
                    is_inside = bx >= 0;

                    if (bx < 0)
                    {
                        is_inside = false;
                        bx = World.chunk_dimensions.x - 1;
                    }
                    break;

                case BlockSide.Top:
                    by += 1;

                    if (by >= World.chunk_dimensions.y)
                    {
                        is_inside = false;
                        by = 0;
                    }
                    break;

                case BlockSide.Bottom:
                    by -= 1;

                    if (by < 0)
                    {
                        is_inside = false;
                        by = World.chunk_dimensions.y - 1;
                    }
                    break;

                case BlockSide.Front:
                    bz += 1;

                    if (bz >= World.chunk_dimensions.z)
                    {
                        is_inside = false;
                        bz = 0;
                    }
                    break;

                case BlockSide.Back:
                    bz -= 1;

                    if (bz < 0)
                    {
                        is_inside = false;
                        bz = World.chunk_dimensions.z - 1;
                    }
                    break;
            }

            return (is_inside, bx, by, bz);
        }

        public Quad createQuad(BlockSide side, BlockType block_type, CrackState crack_state, Vector3Int offset)
        {
            switch (side)
            {
                case BlockSide.Top:
                    if (block_type == BlockType.GRASSSIDE)
                    {
                        return new Quad(BlockType.GRASSTOP, crack_state, side, offset);
                    }
                    else
                    {
                        return new Quad(block_type, crack_state, side, offset);
                    }
                case BlockSide.Bottom:
                    if (block_type == BlockType.GRASSSIDE)
                    {
                        return new Quad(BlockType.DIRT, crack_state, side, offset);
                    }
                    else
                    {
                        return new Quad(block_type, crack_state, side, offset);
                    }
                default:
                    return new Quad(block_type, crack_state, side, offset);
            }
        }

        [Obsolete("提供在 Chunk 中建立 Block 時的協助，延後添加 Mesh 的時間點，而非在建構子當中添加 Mesh")]
        public void build(List<Quad> quads, string block_name)
        {
            if(quads.Count == 0)
            {
                return;
            }

            List<Mesh> meshes = new List<Mesh>();

            foreach (Quad quad in quads)
            {
                meshes.Add(quad.mesh);
            }

            mesh = MeshUtils.mergeMeshes(meshes);
            mesh.name = block_name;
        }
    }
}
