using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    public class Block
    {
        public Mesh mesh;
        Chunk chunk;

        public Block(BlockType block_type, CrackState crack_state, Vector3Int offset, Chunk chunk)
        {
            this.chunk = chunk;

            if (block_type == BlockType.AIR)
            {
                return;
            }

            List<Quad> quads = new List<Quad>();
            Vector3Int local_position = offset - chunk.location;

            //quads.Add(new Quad(block_type, BlockSide.Bottom, offset));
            //quads.Add(new Quad(block_type, BlockSide.Top, offset));
            //quads.Add(new Quad(block_type, BlockSide.Left, offset));
            //quads.Add(new Quad(block_type, BlockSide.Right, offset));
            //quads.Add(new Quad(block_type, BlockSide.Front, offset));
            //quads.Add(new Quad(block_type, BlockSide.Back, offset));

            /* 利用 hasSolidNeighbour 檢查各個方向是否還有下一格，因此傳入的座標為該方向下一格 Block 的座標 */

            if (!hasSolidNeighbour(local_position.x, local_position.y - 1, local_position.z, block_type))
            {
                if (block_type == BlockType.GRASSSIDE)
                {
                    quads.Add(new Quad(BlockType.DIRT, crack_state, BlockSide.Bottom, offset));
                }
                else
                {
                    quads.Add(new Quad(block_type, crack_state, BlockSide.Bottom, offset));
                }
            }

            if (!hasSolidNeighbour(local_position.x, local_position.y + 1, local_position.z, block_type))
            {
                if (block_type == BlockType.GRASSSIDE)
                {
                    quads.Add(new Quad(BlockType.GRASSTOP, crack_state, BlockSide.Top, offset));
                }
                else
                {
                    quads.Add(new Quad(block_type, crack_state, BlockSide.Top, offset));
                }
            }

            if (!hasSolidNeighbour(local_position.x - 1, local_position.y, local_position.z, block_type))
            {
                quads.Add(new Quad(block_type, crack_state, BlockSide.Left, offset));
            }

            if (!hasSolidNeighbour(local_position.x + 1, local_position.y, local_position.z, block_type))
            {
                quads.Add(new Quad(block_type, crack_state, BlockSide.Right, offset));
            }

            if (!hasSolidNeighbour(local_position.x, local_position.y, local_position.z + 1, block_type))
            {
                quads.Add(new Quad(block_type, crack_state, BlockSide.Front, offset));
            }

            if (!hasSolidNeighbour(local_position.x, local_position.y, local_position.z - 1, block_type))
            {
                quads.Add(new Quad(block_type, crack_state, BlockSide.Back, offset));
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

        // TODO: 應實作 Chunk 版本的這個函式，使得 Chunk 之間的交界可以不用顯現，如此一來，水下的 Chunk 邊界才不會出現
        bool hasSolidNeighbour(float x, float y, float z, BlockType block_type)
        {
            if (x < 0 || chunk.WIDTH <= x ||
                y < 0 || chunk.HEIGHT <= y ||
                z < 0 || chunk.DEPTH <= z)
            {
                return false;
            }

            int block_idx = Utils.xyzToFlat((int)x, (int)y, (int)z,
                                            width: chunk.WIDTH,
                                            depth: chunk.DEPTH);

            if (chunk.block_types[block_idx] == block_type)
            {
                return true;
            }

            if (chunk.block_types[block_idx] == BlockType.AIR ||
                chunk.block_types[block_idx] == BlockType.WATER)
            {
                return false;
            }

            return true;
        }
    }
}
