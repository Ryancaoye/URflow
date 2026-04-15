using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace URflow
{
    /// <summary>
    /// Manages user-created presets: save, load, import, export.
    /// Presets are stored as JSON in the project's URflow settings folder.
    /// </summary>
    public static class PresetManager
    {
        private const string SettingsFolder = "ProjectSettings/URflow";
        private const string UserPresetsFile = "UserPresets.json";
        private const string FavoritesFile = "Favorites.json";

        [Serializable]
        private class PresetCollection
        {
            public List<BezierPreset> presets = new List<BezierPreset>();
        }

        [Serializable]
        private class FavoritesList
        {
            public List<string> names = new List<string>();
        }

        private static string GetSettingsPath()
        {
            string path = Path.Combine(Application.dataPath, "..", SettingsFolder);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        // ── User Presets ──

        public static List<BezierPreset> LoadUserPresets()
        {
            string filePath = Path.Combine(GetSettingsPath(), UserPresetsFile);
            if (!File.Exists(filePath))
                return new List<BezierPreset>();

            try
            {
                string json = File.ReadAllText(filePath);
                var collection = JsonUtility.FromJson<PresetCollection>(json);
                return collection?.presets ?? new List<BezierPreset>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[URflow] Failed to load user presets: {e.Message}");
                return new List<BezierPreset>();
            }
        }

        public static void SaveUserPresets(List<BezierPreset> presets)
        {
            string filePath = Path.Combine(GetSettingsPath(), UserPresetsFile);
            try
            {
                var collection = new PresetCollection { presets = presets };
                string json = JsonUtility.ToJson(collection, true);
                File.WriteAllText(filePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[URflow] Failed to save user presets: {e.Message}");
            }
        }

        public static void AddUserPreset(BezierPreset preset)
        {
            var presets = LoadUserPresets();
            presets.Add(preset);
            SaveUserPresets(presets);
        }

        public static void RemoveUserPreset(string name)
        {
            var presets = LoadUserPresets();
            presets.RemoveAll(p => p.name == name);
            SaveUserPresets(presets);
        }

        // ── Favorites ──

        public static HashSet<string> LoadFavorites()
        {
            string filePath = Path.Combine(GetSettingsPath(), FavoritesFile);
            if (!File.Exists(filePath))
                return new HashSet<string>();

            try
            {
                string json = File.ReadAllText(filePath);
                var list = JsonUtility.FromJson<FavoritesList>(json);
                return new HashSet<string>(list?.names ?? new List<string>());
            }
            catch
            {
                return new HashSet<string>();
            }
        }

        public static void SaveFavorites(HashSet<string> favorites)
        {
            string filePath = Path.Combine(GetSettingsPath(), FavoritesFile);
            try
            {
                var list = new FavoritesList { names = new List<string>(favorites) };
                string json = JsonUtility.ToJson(list, true);
                File.WriteAllText(filePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[URflow] Failed to save favorites: {e.Message}");
            }
        }

        public static void ToggleFavorite(string presetName)
        {
            var favorites = LoadFavorites();
            if (favorites.Contains(presetName))
                favorites.Remove(presetName);
            else
                favorites.Add(presetName);
            SaveFavorites(favorites);
        }

        // ── Export / Import (JSON file for team sharing) ──

        public static void ExportToFile(List<BezierPreset> presets, string filePath)
        {
            try
            {
                var collection = new PresetCollection { presets = presets };
                string json = JsonUtility.ToJson(collection, true);
                File.WriteAllText(filePath, json);
                Debug.Log($"[URflow] Exported {presets.Count} presets to {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[URflow] Export failed: {e.Message}");
            }
        }

        public static List<BezierPreset> ImportFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"[URflow] Import file not found: {filePath}");
                    return new List<BezierPreset>();
                }

                string json = File.ReadAllText(filePath);
                var collection = JsonUtility.FromJson<PresetCollection>(json);
                var imported = collection?.presets ?? new List<BezierPreset>();
                Debug.Log($"[URflow] Imported {imported.Count} presets from {filePath}");
                return imported;
            }
            catch (Exception e)
            {
                Debug.LogError($"[URflow] Import failed: {e.Message}");
                return new List<BezierPreset>();
            }
        }
    }
}
