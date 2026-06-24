using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Gantasmo.Colocation;

namespace Gantasmo.Colocation.EditorTools
{
    /// <summary>
    /// One window that turns the QuestMIDI scene into a co-located multiplayer
    /// scene: installs the Meta colocation building blocks, builds and registers
    /// the networked-presence player prefab, and parents the shared content under
    /// a ColocationRoot. Mirrors the QuestMidiSetupWizard convention.
    ///
    /// The building-block installs run through Meta's async installer, which only
    /// completes while the editor's update loop is ticking. Triggering them from a
    /// button here (with the editor focused) is what makes them complete reliably.
    /// </summary>
    public class ColocationSetupWizard : EditorWindow
    {
        // Building block ids (from the Meta XR SDK block catalog, v203).
        const string IdNetworkManager = "1d8db162-54f6-43df-b4ef-b499df1f6769";
        const string IdColocation = "f308c8f0-7a4b-4cd5-88be-c15b6399f823";
        const string IdLocalMatchmaking = "9141594f-eab4-43f9-b05e-17f2f40db586";
        const string IdNetworkedGrabbable = "e9b4b64f-1c7e-4dff-8f3c-ce409bdc3951";
        const string IdPlayerNameTag = "97a7e1ae-ac65-4ee6-9167-10b3b94782f6";

        const string PrefabDir = "Assets/GantasmoColocation/Prefabs";
        const string PrefabPath = PrefabDir + "/NetworkedPresence.prefab";
        const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";

        // Names of the world-anchored content that should sit under the shared
        // root. The passthrough-stitch object is per-headset camera streaming, not
        // shared world content, so it deliberately stays at the scene root.
        static readonly string[] ContentRootNames =
        {
            "GANTASMO XR MIDI Surface",
        };

        Vector2 _scroll;
        static readonly Color Green = new Color(0.36f, 0.78f, 0.40f);
        static readonly Color Amber = new Color(0.93f, 0.61f, 0.22f);

        [MenuItem("GANTASMO/Colocation/Setup Wizard", false, 60)]
        public static void Open()
        {
            var w = GetWindow<ColocationSetupWizard>(false, "Colocation", true);
            w.minSize = new Vector2(460, 600);
            w.Show();
        }

        // ------------------------------------------------------------------ GUI
        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            Header();

            Step("1.  Project prerequisites");
            bool ngo = TypeExists("Unity.Netcode.NetworkManager");
            StatusRow(ngo, ngo ? "Netcode for GameObjects installed" : "NGO missing (install com.unity.netcode.gameobjects)");
            bool perm = ManifestHasColocation();
            StatusRow(perm, perm ? "AndroidManifest has Colocation Discovery permission" : "AndroidManifest missing USE_COLOCATION_DISCOVERY_API");
            bool ovr = OvrColocationEnabled();
            StatusRow(ovr, ovr ? "OVR shared-anchor + colocation-session enabled" : "OVR features off (set sharedAnchorSupport + colocationSessionSupport)");
            if (!ovr && GUILayout.Button("Enable OVR colocation features")) EnableOvrFeatures();

            Step("2.  Meta colocation building blocks");
            EditorGUILayout.HelpBox(
                "Installs Network Manager, Colocation, Local Matchmaking, Networked Grabbable and " +
                "Player Name Tag in dependency order. Only the Netcode-for-GameObjects variant is " +
                "installable (Photon is not present), so no framework picker should appear. If the " +
                "Colocation block asks, choose 'Use Colocation Session'. Keep this editor focused " +
                "while it runs.",
                MessageType.None);
            bool nmInScene = FindAnyObjectByType<NetworkManager>() != null;
            StatusRow(nmInScene, nmInScene ? "Network Manager present in scene" : "No Network Manager in scene yet");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Install all required blocks", GUILayout.Height(28))) _ = InstallAllBlocks();
                if (GUILayout.Button("Open Meta Building Blocks window")) OpenBuildingBlocksWindow();
            }

            Step("3.  Networked presence player prefab");
            bool prefab = File.Exists(PrefabPath);
            StatusRow(prefab, prefab ? "Presence prefab built" : "Presence prefab not built yet");
            bool registered = prefab && PlayerPrefabRegistered();
            StatusRow(registered, registered ? "Presence prefab registered as Player Prefab" : "Player Prefab not set on Network Manager");
            if (GUILayout.Button("Build + register presence prefab")) BuildAndRegisterPresence();

            Step("4.  Scene wiring");
            bool root = FindAnyObjectByType<ColocationRoot>() != null;
            StatusRow(root, root ? "ColocationRoot present" : "No ColocationRoot in scene");
            if (GUILayout.Button("Create ColocationRoot + reparent content")) WireScene();

            Step("5.  Save");
            if (GUILayout.Button("Save scene")) SaveScene();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("One-shot", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Runs blocks -> presence prefab -> scene wiring -> save in order. Keep the editor " +
                "focused; block installs need the editor update loop to finish.",
                MessageType.Info);
            if (GUILayout.Button("Do everything (keep editor focused)", GUILayout.Height(30))) _ = DoEverything();

            EditorGUILayout.Space(10);
            EditorGUILayout.EndScrollView();
        }

        // -------------------------------------------------------------- actions
        async Task DoEverything()
        {
            await InstallAllBlocks();
            BuildAndRegisterPresence();
            WireScene();
            SaveScene();
            Debug.Log("[Coloc] Do-everything finished.");
        }

        async Task InstallAllBlocks()
        {
            // Order matters: foundational + dependency targets first.
            await InstallBlock(IdNetworkManager, "Network Manager");
            await InstallBlock(IdColocation, "Colocation");
            await InstallBlock(IdLocalMatchmaking, "Local Matchmaking");
            // Networked Grabbable is not a singleton, so guard against re-adding its demo cube on re-runs.
            if (GameObject.Find("[BuildingBlock] Cube") == null)
                await InstallBlock(IdNetworkedGrabbable, "Networked Grabbable Object");
            else
                Debug.Log("[Coloc] Networked Grabbable already present, skipped.");
            await InstallBlock(IdPlayerNameTag, "Player Name Tag");
            Debug.Log("[Coloc] Building-block install pass complete.");
            Repaint();
        }

        static async Task InstallBlock(string id, string label)
        {
            var block = FindBlock(id);
            if (block == null) { Debug.LogError($"[Coloc] Block '{label}' not found in catalog."); return; }

            var mi = block.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "AddToProject" && m.GetParameters().Length == 2);
            if (mi == null) { Debug.LogError($"[Coloc] AddToProject not found for '{label}'."); return; }

            try
            {
                var task = mi.Invoke(block, new object[] { null, null }) as Task;
                if (task != null) await task;
                Debug.Log($"[Coloc] Installed block: {label}");
            }
            catch (Exception e)
            {
                var msg = (e.InnerException ?? e).Message;
                if (msg.Contains("already present") || msg.Contains("singleton"))
                    Debug.Log($"[Coloc] '{label}' already present, skipped.");
                else
                    Debug.LogError($"[Coloc] Install of '{label}' failed: {msg}");
            }
        }

        void BuildAndRegisterPresence()
        {
            try
            {
                if (!AssetDatabase.IsValidFolder(PrefabDir))
                    AssetDatabase.CreateFolder("Assets/GantasmoColocation", "Prefabs");

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null)
                {
                    var go = new GameObject("NetworkedPresence");
                    go.AddComponent<NetworkObject>();
                    go.AddComponent<NetworkedPresence>();
                    prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                    DestroyImmediate(go);
                    Debug.Log("[Coloc] Built presence prefab at " + PrefabPath);
                }

                var nm = FindAnyObjectByType<NetworkManager>();
                if (nm == null)
                {
                    EditorUtility.DisplayDialog("Colocation", "Install the building blocks first so a Network Manager exists, then register the presence prefab.", "OK");
                    return;
                }
                if (nm.NetworkConfig != null)
                {
                    nm.NetworkConfig.PlayerPrefab = prefab;
                    EditorUtility.SetDirty(nm);
                    EditorSceneManager.MarkSceneDirty(nm.gameObject.scene);
                    Debug.Log("[Coloc] Registered presence prefab as Player Prefab.");
                }
                Repaint();
            }
            catch (Exception e) { Debug.LogError("[Coloc] BuildAndRegisterPresence failed: " + e.Message); }
        }

        void WireScene()
        {
            try
            {
                var root = FindAnyObjectByType<ColocationRoot>();
                if (root == null)
                {
                    var go = new GameObject("ColocationRoot");
                    go.AddComponent<ColocationRoot>();
                    Undo.RegisterCreatedObjectUndo(go, "Create ColocationRoot");
                    root = go.GetComponent<ColocationRoot>();
                    Debug.Log("[Coloc] Created ColocationRoot.");
                }

                int moved = 0;
                foreach (var n in ContentRootNames)
                {
                    var go = GameObject.Find(n);
                    if (go != null && go.transform.parent != root.transform)
                    {
                        Undo.SetTransformParent(go.transform, root.transform, "Reparent to ColocationRoot");
                        moved++;
                    }
                }
                // MIDI Reactor, found by component type without a hard reference.
                var reactorType = FindRuntimeType("GantasmoVisor");
                if (reactorType != null)
                {
                    var reactor = FindAnyObjectByType(reactorType) as Component;
                    if (reactor != null && reactor.transform.parent != root.transform)
                    {
                        Undo.SetTransformParent(reactor.transform, root.transform, "Reparent MIDI Reactor");
                        moved++;
                    }
                }

                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                Debug.Log($"[Coloc] Scene wired. Reparented {moved} content object(s) under ColocationRoot.");
                Repaint();
            }
            catch (Exception e) { Debug.LogError("[Coloc] WireScene failed: " + e.Message); }
        }

        static void SaveScene()
        {
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[Coloc] Scene + assets saved.");
        }

        void EnableOvrFeatures()
        {
            try
            {
                var cfgType = FindRuntimeType("OVRProjectConfig");
                if (cfgType == null) { Debug.LogError("[Coloc] OVRProjectConfig not found."); return; }
                object cfg = GetOvrConfig(cfgType);
                if (cfg == null) { Debug.LogError("[Coloc] OVRProjectConfig asset not found."); return; }
                SetEnumField(cfgType, cfg, "sharedAnchorSupport", "Required");
                SetEnumField(cfgType, cfg, "colocationSessionSupport", "Required");
                var commit = cfgType.GetMethod("CommitProjectConfig", BindingFlags.Public | BindingFlags.Static);
                if (commit != null) commit.Invoke(null, new[] { cfg });
                else { EditorUtility.SetDirty((UnityEngine.Object)cfg); AssetDatabase.SaveAssets(); }
                Debug.Log("[Coloc] OVR colocation features enabled.");
                Repaint();
            }
            catch (Exception e) { Debug.LogError("[Coloc] EnableOvrFeatures failed: " + e.Message); }
        }

        void OpenBuildingBlocksWindow()
        {
            if (!EditorApplication.ExecuteMenuItem("Meta/Tools/Building Blocks"))
                EditorApplication.ExecuteMenuItem("Oculus/Tools/Building Blocks");
        }

        // -------------------------------------------------------------- helpers
        static object FindBlock(string id)
        {
            var baseBlock = FindRuntimeType("Meta.XR.BuildingBlocks.Editor.BlockBaseData");
            if (baseBlock == null) return null;
            var idProp = baseBlock.GetProperty("Id");
            foreach (var g in AssetDatabase.FindAssets("t:BlockBaseData"))
            {
                var o = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(g), baseBlock);
                if (o != null && (idProp.GetValue(o) as string) == id) return o;
            }
            return null;
        }

        bool PlayerPrefabRegistered()
        {
            var nm = FindAnyObjectByType<NetworkManager>();
            return nm != null && nm.NetworkConfig != null && nm.NetworkConfig.PlayerPrefab != null;
        }

        static bool ManifestHasColocation()
        {
            try { return File.Exists(ManifestPath) && File.ReadAllText(ManifestPath).Contains("USE_COLOCATION_DISCOVERY_API"); }
            catch { return false; }
        }

        // OVRProjectConfig.GetProjectConfig() can return null depending on editor
        // state, so always fall back to loading the asset directly.
        static object GetOvrConfig(Type cfgType)
        {
            if (cfgType == null) return null;
            object cfg = null;
            var get = cfgType.GetMethod("GetProjectConfig", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (get != null) { try { cfg = get.Invoke(null, null); } catch { } }
            if (cfg == null)
            {
                var g = AssetDatabase.FindAssets("t:OVRProjectConfig");
                if (g.Length > 0) cfg = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(g[0]), cfgType);
            }
            return cfg;
        }

        static bool OvrColocationEnabled()
        {
            try
            {
                var cfgType = FindRuntimeType("OVRProjectConfig");
                var cfg = GetOvrConfig(cfgType);
                if (cfg == null) return false;
                var shared = cfgType.GetField("sharedAnchorSupport")?.GetValue(cfg)?.ToString();
                var coloc = cfgType.GetField("colocationSessionSupport")?.GetValue(cfg)?.ToString();
                return shared != null && shared != "None" && coloc != null && coloc != "None";
            }
            catch { return false; }
        }

        static void SetEnumField(Type t, object obj, string field, string value)
        {
            var f = t.GetField(field);
            if (f == null) return;
            var names = Enum.GetNames(f.FieldType);
            string chosen = names.FirstOrDefault(n => n == value) ?? names.FirstOrDefault(n => n == "Supported");
            if (chosen != null) f.SetValue(obj, Enum.Parse(f.FieldType, chosen));
        }

        static bool TypeExists(string fullName) => FindRuntimeType(fullName) != null;

        static Type FindRuntimeType(string name)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = a.GetType(name); } catch { }
                if (t != null) return t;
                if (!name.Contains("."))
                {
                    try { t = a.GetTypes().FirstOrDefault(x => x.Name == name); } catch { }
                    if (t != null) return t;
                }
            }
            return null;
        }

        // ---------------------------------------------------------- GUI drawing
        static void Header()
        {
            EditorGUILayout.Space(4);
            var t = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
            EditorGUILayout.LabelField("Quest Colocation Setup", t);
            EditorGUILayout.LabelField("Shared world frame + visible peers, NGO LAN-direct over Meta Colocation Discovery", EditorStyles.miniLabel);
            EditorGUILayout.Space(6);
        }

        static void Step(string title)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        static void StatusRow(bool ok, string text)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var prev = GUI.color;
                GUI.color = ok ? Green : Amber;
                GUILayout.Label(ok ? "●" : "○", GUILayout.Width(16));
                GUI.color = prev;
                GUILayout.Label(text);
            }
        }
    }
}
