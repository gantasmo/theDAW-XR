using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Gantasmo.XRMidi;
using Oculus.Interaction.HandGrab;
using Debug = UnityEngine.Debug;

namespace QuestMidiBridge.EditorTools
{
    /// <summary>
    /// One window that gets you from nothing to "MIDI flowing into my DAW":
    /// checks Node + adb, writes the bridge config, installs deps, launches the
    /// bridge, and drops a sender into your scene.
    /// </summary>
    public class QuestMidiSetupWizard : EditorWindow
    {
        const string K_PORT = "QuestMidiBridge.tcpPort";
        const string K_NAME = "QuestMidiBridge.midiPortName";
        const string K_INNAME = "QuestMidiBridge.midiInPortName";
        const string K_AUTOADB = "QuestMidiBridge.autoAdbReverse";
        const string K_VERBOSE = "QuestMidiBridge.verbose";
        const string K_ADB = "QuestMidiBridge.adbPath";

        int _tcpPort = 8765;
        string _midiPortName = "QuestMIDI";
        string _midiInPortName = "QuestMIDI-Return";
        bool _autoAdbReverse = true;
        bool _verbose = false;

        // detection cache
        bool _checked;
        bool _nodeOk; string _nodeVer = "";
        bool _adbOk; string _adbPathResolved = "";
        Vector2 _scroll;

        static readonly Color Green = new Color(0.36f, 0.78f, 0.40f);
        static readonly Color Amber = new Color(0.93f, 0.61f, 0.22f);

        [MenuItem("GANTASMO/MIDI Bridge/Setup Wizard", false, 0)]
        public static void Open()
        {
            var w = GetWindow<QuestMidiSetupWizard>(false, "Quest MIDI", true);
            w.minSize = new Vector2(440, 560);
            w.Show();
        }

        [MenuItem("GANTASMO/MIDI Bridge/Open Bridge Folder", false, 1)]
        public static void OpenBridgeFolderMenu() => EditorUtility.RevealInFinder(BridgeDir + "/");

        static string BridgeDir =>
            Path.Combine(Application.dataPath, "QuestMidiBridge", "Bridge~").Replace('\\', '/');

        void OnEnable()
        {
            _tcpPort = EditorPrefs.GetInt(K_PORT, 8765);
            _midiPortName = EditorPrefs.GetString(K_NAME, "QuestMIDI");
            _midiInPortName = EditorPrefs.GetString(K_INNAME, "QuestMIDI-Return");
            _autoAdbReverse = EditorPrefs.GetBool(K_AUTOADB, true);
            _verbose = EditorPrefs.GetBool(K_VERBOSE, false);
            RunChecks();
        }

        // -------------------------------------------------------------- GUI
        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            Header();

            Step("1.  On the Quest (one-time)");
            EditorGUILayout.HelpBox(
                "• Enable Developer Mode in the Meta Quest phone app.\n" +
                "• Plug the Quest into this PC with the USB-C cable.\n" +
                "• Put on the headset and accept 'Allow USB debugging'.",
                MessageType.None);
            if (GUILayout.Button("Open Meta 'Set up development' docs"))
                Application.OpenURL("https://developers.meta.com/horizon/documentation/native/android/mobile-device-setup/");

            Step("2.  Virtual MIDI port (loopMIDI)");
            EditorGUILayout.HelpBox(
                "Your DAW's WebMIDI reads OS MIDI ports. loopMIDI creates one.\n" +
                "Install it, then create a port named exactly the same as below.",
                MessageType.None);
            _midiPortName = EditorGUILayout.TextField("MIDI port name", _midiPortName);
            EditorGUILayout.HelpBox(
                "Return circuit (DAW to headset): create a SECOND loopMIDI port with the " +
                "name below. The DAW writes to it and the bridge forwards it to the Quest " +
                "(the MIDI Reactor reacts). Use a different name than the one above, since " +
                "sharing one port would echo the Quest's own data back to it.",
                MessageType.None);
            _midiInPortName = EditorGUILayout.TextField("Return port name", _midiInPortName);
            if (GUILayout.Button("Download loopMIDI"))
                Application.OpenURL("https://www.tobias-erichsen.de/software/loopmidi.html");

            Step("3.  Tools on this PC");
            StatusRow(_nodeOk, _nodeOk ? $"Node.js found ({_nodeVer})" : "Node.js not found on PATH");
            if (!_nodeOk && GUILayout.Button("Download Node.js"))
                Application.OpenURL("https://nodejs.org/");
            StatusRow(_adbOk, _adbOk ? $"adb found: {_adbPathResolved}" : "adb not found");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Re-check")) RunChecks();
                if (GUILayout.Button("Locate adb.exe…")) LocateAdb();
            }

            Step("4.  Settings");
            _tcpPort = EditorGUILayout.IntField(new GUIContent("TCP port", "Must match the QuestMidiSender component."), _tcpPort);
            _autoAdbReverse = EditorGUILayout.Toggle(new GUIContent("Auto 'adb reverse'", "Bridge runs adb reverse itself on startup."), _autoAdbReverse);
            _verbose = EditorGUILayout.Toggle(new GUIContent("Verbose MIDI log", "Bridge prints every message (hex)."), _verbose);
            PersistPrefs();

            bool filesOk = File.Exists(Path.Combine(BridgeDir, "bridge.js"));
            StatusRow(filesOk, filesOk ? "Bridge files present" : "Bridge files MISSING — reinstall the QuestMidiBridge folder");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save bridge config")) { SaveConfig(); ShowNotification(new GUIContent("Config saved")); }
                if (GUILayout.Button("Open bridge folder")) EditorUtility.RevealInFinder(BridgeDir + "/");
            }

            Step("5.  Install dependencies (first time)");
            bool depsOk = Directory.Exists(Path.Combine(BridgeDir, "node_modules"));
            StatusRow(depsOk, depsOk ? "Dependencies installed" : "Not installed yet (the bridge will auto-install on first run)");
            using (new EditorGUI.DisabledScope(!_nodeOk || !filesOk))
            {
                if (GUILayout.Button("Run 'npm install' now (optional)")) NpmInstall();
            }

            Step("6.  Run the bridge");
            EditorGUILayout.HelpBox("Saves the config, then opens a console running the bridge. Leave it open while you perform.", MessageType.None);
            using (new EditorGUI.DisabledScope(!filesOk))
            {
                if (GUILayout.Button("▶  Start Bridge", GUILayout.Height(30))) { SaveConfig(); StartBridge(); }
            }
            if (GUILayout.Button("Run 'adb reverse' now (quick re-tunnel)")) AdbReverseNow();

            Step("7.  Unity scene");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add MIDI Sender to scene")) AddSenderToScene();
                if (GUILayout.Button("Apply Android settings")) ApplyAndroidSettings();
            }
            EditorGUILayout.HelpBox(
                "Tip: press Play in the Editor with the bridge running — the app talks to this PC's " +
                "localhost directly, so you can test Unity → bridge → loopMIDI → DAW without the headset.\n" +
                "'Apply Android settings' enables Internet permission (needed for the socket on-device).",
                MessageType.Info);

            Step("8.  GANTASMO XR MIDI surface");
            EditorGUILayout.HelpBox(
                "Build the floating 3D control surface (default 8 sliders, 6 knobs, 12 buttons) wired to " +
                "one sender. Layout/count/scale/materials/custom objects come from a GantasmoSurfaceConfig " +
                "preset — edit the asset and rebuild. If hand-tracked sliders/knobs don't move (and so send " +
                "no MIDI), run Repair — it adds the hand-grab interactable the Building Blocks rig needs.",
                MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build XR MIDI Surface"))
                    EditorApplication.ExecuteMenuItem("GANTASMO/Control Surface/Build XR MIDI Control Surface");
                if (GUILayout.Button("Repair Interactions"))
                    EditorApplication.ExecuteMenuItem("GANTASMO/Control Surface/Repair XR MIDI Surface Interactions");
            }
            if (GUILayout.Button("Validate scene wiring")) ValidateScene();

            Step("9.  Project diagnostics");
            bool androidOk = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
            StatusRow(androidOk, androidOk ? "Active build target is Android" : "Active build target is NOT Android (switch in Build Settings)");

            string company = PlayerSettings.companyName;
            string appId = Application.identifier;
            bool identityOk = !string.IsNullOrEmpty(company) && company != "DefaultCompany"
                              && !appId.Contains("DefaultCompany") && !appId.Contains("com.unity");
            StatusRow(identityOk, identityOk
                ? $"App identity set ({appId})"
                : $"Default app identity ({appId}) — set Company Name + Package Name before a real build");

            bool nodeModulesPresent = Directory.Exists(Path.Combine(BridgeDir, "node_modules"));
            if (nodeModulesPresent)
                EditorGUILayout.HelpBox("Bridge~/node_modules is present. Fine for running locally, but exclude it from any release artifact (the bridge re-installs from package.json).", MessageType.None);

            EditorGUILayout.Space(10);
            EditorGUILayout.EndScrollView();
        }

        // ----------------------------------------------------------- actions
        void RunChecks()
        {
            _nodeOk = RunCaptured("cmd.exe", "/c node --version", null, out var nout, out _, 8000)
                      && nout.Trim().StartsWith("v");
            _nodeVer = _nodeOk ? nout.Trim() : "";
            _adbPathResolved = ResolveAdb();
            _adbOk = !string.IsNullOrEmpty(_adbPathResolved);
            _checked = true;
            Repaint();
        }

        string ResolveAdb()
        {
            var stored = EditorPrefs.GetString(K_ADB, "");
            if (!string.IsNullOrEmpty(stored) && File.Exists(stored)) return stored;

            if (RunCaptured("cmd.exe", "/c adb version", null, out var o, out _, 8000) && o.ToLower().Contains("android debug bridge"))
                return "adb";

            var sdk = EditorPrefs.GetString("AndroidSdkRoot", "");
            if (!string.IsNullOrEmpty(sdk))
            {
                var p = Path.Combine(sdk, "platform-tools", "adb.exe");
                if (File.Exists(p)) return p;
            }
            return "";
        }

        void LocateAdb()
        {
            var sdk = EditorPrefs.GetString("AndroidSdkRoot", "");
            var start = string.IsNullOrEmpty(sdk) ? "" : Path.Combine(sdk, "platform-tools");
            var picked = EditorUtility.OpenFilePanel("Locate adb.exe", start, "exe");
            if (!string.IsNullOrEmpty(picked) && File.Exists(picked))
            {
                EditorPrefs.SetString(K_ADB, picked);
                RunChecks();
            }
        }

        void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(BridgeDir);
                string adb = string.IsNullOrEmpty(_adbPathResolved) ? "adb" : _adbPathResolved;
                string json =
                    "{\n" +
                    "  \"tcpPort\": " + _tcpPort + ",\n" +
                    "  \"midiPortName\": \"" + EscJson(_midiPortName) + "\",\n" +
                    "  \"midiInPortName\": \"" + EscJson(_midiInPortName) + "\",\n" +
                    "  \"adbPath\": \"" + EscJson(adb) + "\",\n" +
                    "  \"autoAdbReverse\": " + (_autoAdbReverse ? "true" : "false") + ",\n" +
                    "  \"verbose\": " + (_verbose ? "true" : "false") + "\n" +
                    "}\n";
                File.WriteAllText(Path.Combine(BridgeDir, "bridge-config.json"), json);
                Debug.Log("[QuestMidi] Wrote bridge-config.json (port " + _tcpPort + ", \"" + _midiPortName + "\").");
            }
            catch (Exception e) { Debug.LogError("[QuestMidi] Failed to write config: " + e.Message); }
        }

        void NpmInstall()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c npm install & echo. & echo --- Done. You can close this window. & pause",
                    WorkingDirectory = BridgeDir,
                    UseShellExecute = true
                });
            }
            catch (Exception e) { Debug.LogError("[QuestMidi] npm install failed to launch: " + e.Message); }
        }

        void StartBridge()
        {
            var bat = Path.Combine(BridgeDir, "start-bridge.bat");
            if (!File.Exists(bat)) { Debug.LogError("[QuestMidi] start-bridge.bat not found in " + BridgeDir); return; }
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = bat,
                    WorkingDirectory = BridgeDir,
                    UseShellExecute = true
                });
            }
            catch (Exception e) { Debug.LogError("[QuestMidi] Could not start the bridge: " + e.Message); }
        }

        void AdbReverseNow()
        {
            var adb = ResolveAdb();
            if (string.IsNullOrEmpty(adb)) { EditorUtility.DisplayDialog("adb not found", "Locate adb.exe first (step 3).", "OK"); return; }

            bool ok;
            string outp, errp;
            string ruleArgs = "reverse tcp:" + _tcpPort + " tcp:" + _tcpPort;
            if (adb == "adb")
                ok = RunCaptured("cmd.exe", "/c adb " + ruleArgs, null, out outp, out errp, 10000);
            else
                ok = RunCaptured(adb, ruleArgs, null, out outp, out errp, 10000);

            string msg = ok
                ? "adb reverse tcp:" + _tcpPort + " is set.\nThe Quest's localhost:" + _tcpPort + " now reaches this PC."
                : "adb reverse failed.\n\n" + (string.IsNullOrEmpty(errp) ? outp : errp) +
                  "\n\nIs the Quest connected and 'USB debugging' allowed? Check 'adb devices'.";
            EditorUtility.DisplayDialog("adb reverse", msg, "OK");
        }

        void AddSenderToScene()
        {
            var go = new GameObject("Quest MIDI Sender");
            var s = go.AddComponent<QuestMidiSender>();
            s.port = _tcpPort;
            Undo.RegisterCreatedObjectUndo(go, "Create Quest MIDI Sender");
            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
        }

        void ApplyAndroidSettings()
        {
            try
            {
                PlayerSettings.Android.forceInternetPermission = true;
                PlayerSettings.runInBackground = true;
                Debug.Log("[QuestMidi] Enabled Android Internet permission + Run In Background.");
                ShowNotification(new GUIContent("Android settings applied"));
            }
            catch (Exception e) { Debug.LogError("[QuestMidi] Could not apply Android settings: " + e.Message); }
        }

        void ValidateScene()
        {
            var issues = new System.Collections.Generic.List<string>();

            var surfaces = UnityEngine.Object.FindObjectsByType<MidiControlSurface>(FindObjectsInactive.Include);
            if (surfaces.Length == 0)
                issues.Add("No MidiControlSurface in the open scene. Build the XR MIDI Surface (step 8).");

            foreach (var surf in surfaces)
            {
                if (surf.sender == null && surf.GetComponentInParent<QuestMidiSender>() == null)
                    issues.Add($"'{surf.name}' has no QuestMidiSender (sender is null and none on a parent).");

                int sliders = surf.GetComponentsInChildren<MidiSlider>(true).Length;
                int knobs = surf.GetComponentsInChildren<MidiKnob>(true).Length;
                int missingGrab =
                    surf.GetComponentsInChildren<MidiSlider>(true).Count(s => s.GetComponent<HandGrabInteractable>() == null) +
                    surf.GetComponentsInChildren<MidiKnob>(true).Count(k => k.GetComponent<HandGrabInteractable>() == null);
                if (missingGrab > 0)
                    issues.Add($"'{surf.name}': {missingGrab} of {sliders + knobs} sliders/knobs lack a HandGrabInteractable — they won't move or send MIDI. Run 'Repair Interactions'.");
            }

            if (UnityEngine.Object.FindObjectsByType<QuestMidiSender>(FindObjectsInactive.Include).Length == 0)
                issues.Add("No QuestMidiSender anywhere in the scene — nothing will ship MIDI to the bridge.");

            string msg = issues.Count == 0
                ? "Scene wiring looks good: a control surface with a sender, and every slider/knob has a hand-grab interactable."
                : "Found " + issues.Count + " issue(s):\n\n• " + string.Join("\n• ", issues);
            EditorUtility.DisplayDialog("GANTASMO scene validation", msg, "OK");
        }

        void PersistPrefs()
        {
            EditorPrefs.SetInt(K_PORT, _tcpPort);
            EditorPrefs.SetString(K_NAME, _midiPortName);
            EditorPrefs.SetString(K_INNAME, _midiInPortName);
            EditorPrefs.SetBool(K_AUTOADB, _autoAdbReverse);
            EditorPrefs.SetBool(K_VERBOSE, _verbose);
        }

        // ----------------------------------------------------------- drawing
        void Header()
        {
            EditorGUILayout.Space(4);
            var t = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
            EditorGUILayout.LabelField("Quest → DAW MIDI Setup", t);
            EditorGUILayout.LabelField("Quest app → USB (adb) → bridge → loopMIDI → your WebMIDI DAW", EditorStyles.miniLabel);
            EditorGUILayout.Space(6);
            if (!_checked) EditorGUILayout.HelpBox("Click 'Re-check' to detect Node and adb.", MessageType.None);
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

        // ----------------------------------------------------------- process
        static bool RunCaptured(string fileName, string args, string workingDir, out string stdout, out string stderr, int timeoutMs)
        {
            stdout = ""; stderr = "";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = string.IsNullOrEmpty(workingDir) ? Environment.CurrentDirectory : workingDir
                };
                using (var p = Process.Start(psi))
                {
                    string so = p.StandardOutput.ReadToEnd();
                    string se = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } stderr = "timed out"; return false; }
                    stdout = so; stderr = se;
                    return p.ExitCode == 0;
                }
            }
            catch (Exception e) { stderr = e.Message; return false; }
        }

        static string EscJson(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
