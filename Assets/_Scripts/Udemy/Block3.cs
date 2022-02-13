using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    public class Block
    {
        public Mesh mesh;
        Chunk chunk;

        public Block(BlockType block_type, Vector3Int offset, Chunk chunk)
        {
            this.chunk = chunk;

            if (block_type == BlockType.AIR)
            {
                return;
            }

            List<Quad1> quads = new List<Quad1>();
            Vector3Int local_position = offset - chunk.location;

            //quads.Add(new Quad(block_type, BlockSide.Bottom, offset));
            //quads.Add(new Quad(block_type, BlockSide.Top, offset));
            //quads.Add(new Quad(block_type, BlockSide.Left, offset));
            //quads.Add(new Quad(block_type, BlockSide.Right, offset));
            //quads.Add(new Quad(block_type, BlockSide.Front, offset));
            //quads.Add(new Quad(block_type, BlockSide.Back, offset));

            /* �Q�� hasSolidNeighbour �ˬd�U�Ӥ�V�O�_�٦��U�@��A�]���ǤJ���y�Ь��Ӥ�V�U�@�� Block ���y�� */

            if (!hasSolidNeighbour(local_position.x, local_position.y - 1, local_position.z, block_type))
            {
                if (block_type == BlockType.GRASSSIDE)
                {
                    quads.Add(new Quad1(BlockType.DIRT, BlockSide.Bottom, offset));
                }
                else
                {
                    quads.Add(new Quad1(block_type, BlockSide.Bottom, offset));
                }
            }

            if (!hasSolidNeighbour(local_position.x, local_position.y + 1, local_position.z, block_type))
            {
                if (block_type == BlockType.GRASSSIDE)
                {
                    quads.Add(new Quad1(BlockType.GRASSTOP, BlockSide.Top, offset));
                }
                else
                {
                    quads.Add(new Quad1(block_type, BlockSide.Top, offset));
                }
            }

            if (!hasSolidNeighbour(local_position.x - 1, local_position.y, local_position.z, block_type))
            {
                quads.Add(new Quad1(block_type, BlockSide.Left, offset));
            }

            if (!hasSolidNeighbour(local_position.x + 1, local_position.y, local_position.z, block_type))
            {
                quads.Add(new Quad1(block_type, BlockSide.Right, offset));
            }

            if (!hasSolidNeighbour(local_position.x, local_position.y, local_position.z + 1, block_type))
            {
                quads.Add(new Quad1(block_type, BlockSide.Front, offset));
            }

            if (!hasSolidNeighbour(local_position.x, local_position.y, local_position.z - 1, block_type))
            {
                quads.Add(new Quad1(block_type, BlockSide.Back, offset));
            }

            if (quads.Count == 0)
            {
                return;
            }

            List<Mesh> meshes = new List<Mesh>();

            foreach (Quad1 quad in quads)
            {
                meshes.Add(quad.mesh);
            }

            mesh = MeshUtils.mergeMeshes(meshes);
            mesh.name = $"Cube_{offset.x}_{offset.y}_{offset.z}";
        }

        bool hasSolidNeighbour(float x, float y, float z, BlockType block_type)
        {
            if (x < 0 || chunk.width <= x ||
                y < 0 || chunk.height <= y ||
                z < 0 || chunk.depth <= z)
            {
                return false;
            }

            int block_idx = Utils.xyzToFlat((int)x, (int)y, (int)z,
                                            width: chunk.width,
                                            depth: chunk.depth);

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

    public class Block3
    {
        public Mesh mesh;
        Chunk2 chunk;

        public Block3(BlockType block_type, Vector3Int offset, Chunk2 chunk)
        {
            this.chunk = chunk;

            if (block_type == BlockType.AIR)
            {
                return;
            }

            List<Quad1> quads = new List<Quad1>();
            Vector3Int local_position = offset - chunk.location;

            //quads.Add(new Quad(block_type, BlockSide.Bottom, offset));
            //quads.Add(new Quad(block_type, BlockSide.Top, offset));
            //quads.Add(new Quad(block_type, BlockSide.Left, offset));
            //quads.Add(new Quad(block_type, BlockSide.Right, offset));
            //quads.Add(new Quad(block_type, BlockSide.Front, offset));
            //quads.Add(new Quad(block_type, BlockSide.Back, offset));

            /* �Q�� hasSolidNeighbour �ˬd�U�Ӥ�V�O�_�٦��U�@��A�]���ǤJ���y�Ь��Ӥ�V�U�@�� Block ���y�� */

            if (!hasSolidNeighbour(local_position.x, local_position.y - 1, local_position.z, block_type))
            {
                if (block_type == BlockType.GRASSSIDE)
                {
                    quads.Add(new Quad1(BlockType.DIRT, BlockSide.Bottom, offset));
                }
                else
                {
                    quads.Add(new Quad1(block_type, BlockSide.Bottom, offset));
                }
            }

            if (!hasSolidNeighbour(local_position.x, local_position.y + 1, local_position.z, block_type))
            {
                if (block_type == BlockType.GRASSSIDE)
                {
                    quads.Add(new Quad1(BlockType.GRASSTOP, BlockSide.Top, offset));
                }
                else
                {
                    quads.Add(new Quad1(block_type, BlockSide.Top, offset));
                }
            }

            if (!hasSolidNeighbour(local_position.x - 1, local_position.y, local_position.z, block_type))
            {
                quads.Add(new Quad1(block_type, BlockSide.Left, offset));
            }

            if (!hasSolidNeighbour(local_position.x + 1, local_position.y, local_position.z, block_type))
            {
                quads.Add(new Quad1(block_type, BlockSide.Right, offset));
            }

            if (!hasSolidNeighbour(local_position.x, local_position.y, local_position.z + 1, block_type))
            {
                quads.Add(new Quad1(block_type, BlockSide.Front, offset));
            }

            if (!hasSolidNeighbour(local_position.x, local_position.y, local_position.z - 1, block_type))
            {
                quads.Add(new Quad1(block_type, BlockSide.Back, offset));
            }

            if (quads.Count == 0)
            {
                return;
            }

            List<Mesh> meshes = new List<Mesh>();

            foreach (Quad1 quad in quads)
            {
                meshes.Add(quad.mesh);
            }

            mesh = MeshUtils.mergeMeshes(meshes);
            mesh.name = $"Cube_{offset.x}_{offset.y}_{offset.z}";
        }

        bool hasSolidNeighbour(float x, float y, float z, BlockType block_type)
        {
            if (x < 0 || chunk.width <= x ||
                y < 0 || chunk.height <= y ||
                z < 0 || chunk.depth <= z)
            {
                return false;
            }

            int block_idx = Utils.xyzToFlat((int)x, (int)y, (int)z,
                                            width: chunk.width,
                                            depth: chunk.depth);

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

    public class Block2
    {
        public Mesh mesh;
        Chunk1 chunk;

        public Block2(BlockType block_type, Vector3Int offset, Chunk1 chunk)
        {
            this.chunk = chunk;

            if (block_type == BlockType.AIR)
            {
                return;
            }

            List<Quad1> quads = new List<Quad1>();
            Vector3Int local_position = offset - chunk.location;

            //quads.Add(new Quad(block_type, BlockSide.Bottom, offset));
            //quads.Add(new Quad(block_type, BlockSide.Top, offset));
            //quads.Add(new Quad(block_type, BlockSide.Left, offset));
            //quads.Add(new Quad(block_type, BlockSide.Right, offset));
            //quads.Add(new Quad(block_type, BlockSide.Front, offset));
            //quads.Add(new Quad(block_type, BlockSide.Back, offset));

            /* �Q�� hasSolidNeighbour �ˬd�U�Ӥ�V�O�_�٦��U�@��A�]���ǤJ���y�Ь��Ӥ�V�U�@�� Block ���y�� */

            if (!hasSolidNeighbour(local_position.x, local_position.y - 1, local_position.z, block_type))
            {
                if (block_type == BlockType.GRASSSIDE)
                {
                    quads.Add(new Quad1(BlockType.DIRT, BlockSide.Bottom, offset));
                }
                else
                {
                    quads.Add(new Quad1(block_type, BlockSide.Bottom, offset));
                }
            }

            if (!hasSolidNeighbour(local_position.x, local_position.y + 1, local_position.z, block_type))
            {
                if (block_type == BlockType.GRASSSIDE)
                {
                    quads.Add(new Quad1(BlockType.GRASSTOP, BlockSide.Top, offset));
                }
                else
                {
                    quads.Add(new Quad1(block_type, BlockSide.Top, offset));
                }
            }

            if (!hasSolidNeighbour(local_position.x - 1, local_position.y, local_position.z, block_type))
            {
                quads.Add(new Quad1(block_type, BlockSide.Left, offset));
            }

            if (!hasSolidNeighbour(local_position.x + 1, local_position.y, local_position.z, block_type))
            {
                quads.Add(new Quad1(block_type, BlockSide.Right, offset));
            }

            if (!hasSolidNeighbour(local_position.x, local_position.y, local_position.z + 1, block_type))
            {
                quads.Add(new Quad1(block_type, BlockSide.Front, offset));
            }

            if (!hasSolidNeighbour(local_position.x, local_position.y, local_position.z - 1, block_type))
            {
                quads.Add(new Quad1(block_type, BlockSide.Back, offset));
            }

            if (quads.Count == 0)
            {
                return;
            }

            List<Mesh> meshes = new List<Mesh>();

            foreach (Quad1 quad in quads)
            {
                meshes.Add(quad.mesh);
            }

            mesh = MeshUtils.mergeMeshes(meshes);
            mesh.name = "Cube_0_0_0";
        }

        bool hasSolidNeighbour(float x, float y, float z, BlockType block_type)
        {
            if (x < 0 || chunk.width <= x ||
                y < 0 || chunk.height <= y ||
                z < 0 || chunk.depth <= z)
            {
                return false;
            }

            int block_idx = Utils.xyzToFlat((int)x, (int)y, (int)z, 
                                            width: chunk.width, 
                                            depth: chunk.depth);

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

    public class Block1
    {
        public Mesh mesh;

        public Block1(BlockType block_type, Vector3Int offset)
        {
            List<Quad1> quads = new List<Quad1>();
            quads.Add(new Quad1(block_type, BlockSide.Bottom, offset));
            quads.Add(new Quad1(block_type, BlockSide.Top, offset));
            quads.Add(new Quad1(block_type, BlockSide.Left, offset));
            quads.Add(new Quad1(block_type, BlockSide.Right, offset));
            quads.Add(new Quad1(block_type, BlockSide.Front, offset));
            quads.Add(new Quad1(block_type, BlockSide.Back, offset));

            List<Mesh> meshes = new List<Mesh>();

            foreach (Quad1 quad in quads)
            {
                meshes.Add(quad.mesh);
            }

            mesh = MeshUtils.mergeMeshes(meshes);
            mesh.name = "Cube_0_0_0";
        }
    }
}
