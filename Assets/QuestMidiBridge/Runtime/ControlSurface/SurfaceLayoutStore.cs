using System;
using System.IO;
using UnityEngine;

namespace Gantasmo.XRMidi
{
    /// <summary>
    /// Persists a surface root's world transform per scene to a JSON file under
    /// <see cref="Application.persistentDataPath"/>. This works the same in the
    /// Editor and in a deployed Quest build, so "save as the default for this scene"
    /// survives a restart. The file is keyed by scene name and root name, so several
    /// scenes (and several surfaces) keep independent layouts.
    /// </summary>
    public static class SurfaceLayoutStore
    {
        [Serializable]
        class Layout
        {
            public bool saved;
            public Vector3 position;
            public Vector3 euler;
            public Vector3 scale = Vector3.one;
        }

        static string PathFor(Transform root)
        {
            string scene = root.gameObject.scene.IsValid() ? root.gameObject.scene.name : "default";
            string safeScene = Sanitize(scene);
            string safeRoot = Sanitize(root.name);
            return Path.Combine(Application.persistentDataPath, $"gantasmo_surface_{safeScene}_{safeRoot}.json");
        }

        static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }

        /// <summary>Write the surface's current placement as the scene default.</summary>
        public static void SaveFrom(Transform root)
        {
            var layout = new Layout
            {
                saved = true,
                position = root.position,
                euler = root.eulerAngles,
                scale = root.localScale,
            };
            try
            {
                string path = PathFor(root);
                File.WriteAllText(path, JsonUtility.ToJson(layout, true));
                Debug.Log($"[GANTASMO] Saved surface layout to {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GANTASMO] Could not save surface layout: {e.Message}");
            }
        }

        /// <summary>Apply the saved placement if one exists. Returns true when applied.</summary>
        public static bool LoadInto(Transform root)
        {
            try
            {
                string path = PathFor(root);
                if (!File.Exists(path)) return false;
                var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(path));
                if (layout == null || !layout.saved) return false;
                root.position = layout.position;
                root.eulerAngles = layout.euler;
                root.localScale = layout.scale;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GANTASMO] Could not load surface layout: {e.Message}");
                return false;
            }
        }

        /// <summary>Delete the saved layout so the surface returns to its built placement.</summary>
        public static void Clear(Transform root)
        {
            try
            {
                string path = PathFor(root);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GANTASMO] Could not clear surface layout: {e.Message}");
            }
        }
    }
}
