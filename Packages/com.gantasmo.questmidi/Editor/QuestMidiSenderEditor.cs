using UnityEditor;
using UnityEngine;

namespace QuestMidiBridge.EditorTools
{
    [CustomEditor(typeof(QuestMidiSender))]
    public class QuestMidiSenderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var s = (QuestMidiSender)target;

            // ---- live connection status (Play mode) ----
            if (Application.isPlaying)
            {
                bool connected = s.IsConnected;
                EditorGUILayout.HelpBox(
                    connected
                        ? $"● Connected to bridge at {s.host}:{s.port}"
                        : $"○ Not connected — retrying…\nIs start-bridge.bat running? On the headset, is 'adb reverse tcp:{s.port} tcp:{s.port}' set?",
                    connected ? MessageType.Info : MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Enter Play mode to see live connection status and send test messages.", MessageType.None);
            }

            EditorGUILayout.Space(2);
            DrawDefaultInspector();

            // ---- test buttons (Play mode only) ----
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Test", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Note On C3")) s.SendNoteOn(60, 100);
                    if (GUILayout.Button("Note Off C3")) s.SendNoteOff(60);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("CC1 = 127")) s.SendControlChange(1, 127);
                    if (GUILayout.Button("CC1 = 0")) s.SendControlChange(1, 0);
                }
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Open Setup Wizard"))
                QuestMidiSetupWizard.Open();
        }

        // Keep the connection indicator live while playing.
        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
