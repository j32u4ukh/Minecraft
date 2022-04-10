using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace udemy
{
    // TODO: 是否轉為 JSON 來儲存？
    public static class WorldRecorder
    {
        // C:\Users\PC\AppData\LocalLow\DefaultCompany\Minecraft\recorder
        private static string folder = Path.Combine(Application.persistentDataPath, "recorder");

        public static void save(WorldData wd)
        {
            if (!Directory.Exists(folder))
            {
                // 確保資料夾存在
                Directory.CreateDirectory(folder);
            }

            string path = Path.Combine(folder, getFileName());

            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(path, FileMode.OpenOrCreate);
            Debug.Log($"[WorldRecorder] save | Save WorldData player @ ({wd.player_x}, {wd.player_y}, {wd.player_z})");
            bf.Serialize(file, wd);
            file.Close();
            Debug.Log($"[WorldRecorder] save | Saving world to file: {path}");
        }

        public static WorldData load()
        {
            string path = Path.Combine(folder, getFileName());

            // 確保檔案存在
            if (File.Exists(path))
            {
                BinaryFormatter bf = new BinaryFormatter();
                FileStream file = File.Open(path, FileMode.Open);
                WorldData wd = (WorldData)bf.Deserialize(file);
                
                file.Close();
                Debug.Log($"[WorldRecorder] load | Loading world from file: {path}");

                return wd;
            }

            return null;
        }

        private static string getFileName()
        {
            return $"World_{World.world_dimesions.x}_{World.world_dimesions.y}_{World.world_dimesions.z}" +
                   $"_{World.chunk_dimensions.x}_{World.chunk_dimensions.y}_{World.chunk_dimensions.z}.dat";
        }
    }
    
}
