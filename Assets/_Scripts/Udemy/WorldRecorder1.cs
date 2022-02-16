using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace udemy
{
    public static class WorldRecorder1
    {
        private static string folder = Path.Combine(Application.persistentDataPath, "recorder");

        public static void save(WorldData1 wd)
        {
            if (!Directory.Exists(folder))
            {
                // 確保資料夾存在
                Directory.CreateDirectory(folder);
            }

            string path = Path.Combine(folder, getFileName());

            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(path, FileMode.OpenOrCreate);
            Debug.Log($"Save WorldData1 player @ ({wd.player_x}, {wd.player_y}, {wd.player_z})");
            bf.Serialize(file, wd);
            file.Close();
            Debug.Log($"Saving world to file: {path}");
        }

        public static WorldData1 load()
        {
            string path = Path.Combine(folder, getFileName());

            // 確保檔案存在
            if (File.Exists(path))
            {
                BinaryFormatter bf = new BinaryFormatter();
                FileStream file = File.Open(path, FileMode.Open);
                WorldData1 wd = (WorldData1)bf.Deserialize(file);
                
                file.Close();
                Debug.Log($"Loading world from file: {path}");

                return wd;
            }

            return null;
        }

        private static string getFileName()
        {
            return $"World_{WorldDemo3.world_dimesions.x}_{WorldDemo3.world_dimesions.y}_{WorldDemo3.world_dimesions.z}" +
                   $"_{WorldDemo3.chunk_dimensions.x}_{WorldDemo3.chunk_dimensions.y}_{WorldDemo3.chunk_dimensions.z}.dat";
        }
    }
}
