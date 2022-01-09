using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

[Serializable]
public class WorldData
{
    //HashSet<Vector3Int> chunkChecker = new HashSet<Vector3Int>();
    //HashSet<Vector2Int> chunkColumns = new HashSet<Vector2Int>();
    //Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();
    public int[] chunkCheckerValues;
    public int[] chunkColumnsValues;
    public int[] allChunkData;

    public int fpcX;
    public int fpcY;
    public int fpcZ;

    public WorldData() { }

    public WorldData(HashSet<Vector3Int> cc, HashSet<Vector2Int> cCols, Dictionary<Vector3Int, Chunk> chks, Vector3 fpc)
    {
        chunkCheckerValues = new int[cc.Count * 3];
        int index = 0;

        foreach(Vector3Int v in cc)
        {
            chunkCheckerValues[index] = v.x;
            chunkCheckerValues[index + 1] = v.y;
            chunkCheckerValues[index + 2] = v.z;
            index += 3;
        }

        chunkColumnsValues = new int[cCols.Count * 2];
        index = 0;

        foreach (Vector2Int v in cCols)
        {
            chunkColumnsValues[index] = v.x;
            chunkColumnsValues[index + 1] = v.y;
            index += 2;
        }

        allChunkData = new int[chks.Count * World.chunkDimensions.x * World.chunkDimensions.y * World.chunkDimensions.z];
        index = 0;

        foreach(KeyValuePair<Vector3Int, Chunk> ch in chks)
        {
            foreach(MeshUtils.BlockType bt in ch.Value.chunkData)
            {
                allChunkData[index] = (int)bt;
                index++;
            }
        }

        fpcX = (int)fpc.x;
        fpcY = (int)fpc.y;
        fpcZ = (int)fpc.z;
    }
}

public static class FileSaver
{
    private static WorldData wd;

    static string BuildFileName()
    {
        return Application.persistentDataPath + $"/saveddata/World" +
            $"_{World.chunkDimensions.x}_{World.chunkDimensions.y}_{World.chunkDimensions.z}" +
            $"_{World.worldDimesions.x}_{World.worldDimesions.y}_{World.worldDimesions.z}.dat";
    }

    public static void Save(World world)
    {
        string filename = BuildFileName();

        if (!File.Exists(filename))
        {
            // 確保資料夾存在
            Directory.CreateDirectory(Path.GetDirectoryName(filename));
        }

        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Open(filename, FileMode.OpenOrCreate);
        wd = new WorldData(world.chunkChecker, world.chunkColumns, world.chunks, world.fpc.transform.position);
        bf.Serialize(file, wd);
        file.Close();
        Debug.Log($"Saving world to file: {filename}");
    }
}
