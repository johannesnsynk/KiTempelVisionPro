using System.Linq;
using NSYNK.HyperSlides.Network;
using UnityEngine;

namespace NSYNK.HyperSlides.Core
{
    [ExecuteAlways]
    public class DebugInfoGUI : MonoBehaviour
    {
        private float boxWidth = Screen.width / 3 - 40;
        private Rect NewAreaRect(float position) => new Rect(20 + boxWidth * position + position * 20, 20, boxWidth, Screen.height - 40);

        void OnGUI()
        {
            GUI.backgroundColor = new Color(0, 0, 0, 1f);
            GUI.Box(NewAreaRect(0), GUIContent.none);
            GUI.Box(NewAreaRect(1), GUIContent.none);
            GUI.Box(NewAreaRect(2), GUIContent.none);

            GUILayout.BeginArea(NewAreaRect(0));
            DrawSettingsInfo();
            GUILayout.EndArea();

            GUILayout.BeginArea(NewAreaRect(1));
            DrawServerConnectionInfo();
            GUILayout.EndArea();

            GUILayout.BeginArea(NewAreaRect(2));
            DrawNakamaMatchDetails();
            GUILayout.EndArea();

#if UNITY_EDITOR
            // Force editor to update/repaint so the Game view shows the GUI while not in Play mode
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
#endif
        }

        private void DrawSettingsInfo()
        {
            HyperSlidesStateManager foundStateManager = FindAnyObjectByType<HyperSlidesStateManager>();

            if (foundStateManager == null || foundStateManager.Settings == null)
                return;

            var settings = foundStateManager.Settings;

            GUILayout.Space(10);
            GUILayout.Label("Settings Info", UnityEngine.GUI.skin.box);

            foreach (var setting in settings.GetType().GetFields())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(setting.Name + ": ", GUILayout.Width(200));
                GUILayout.Label(setting.GetValue(settings)?.ToString());
                GUILayout.EndHorizontal();
            }
        }

        private void DrawServerConnectionInfo()
        {
            HyperSlidesStateManager foundStateManager = FindAnyObjectByType<HyperSlidesStateManager>();

            if (foundStateManager == null || foundStateManager.ServerConnections == null)
                return;

            var serverConnection = foundStateManager.CurrentServerConnection ?? foundStateManager.ServerConnections.FirstOrDefault();

            GUILayout.Space(10);
            GUILayout.Label("Server Connection Info", UnityEngine.GUI.skin.box);

            foreach (var field in serverConnection.GetType().GetFields())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(field.Name + ": ", GUILayout.Width(200));
                GUILayout.Label(field.GetValue(serverConnection)?.ToString());
                GUILayout.EndHorizontal();
            }
        }

        private void DrawNakamaMatchDetails()
        {
            if (XRNetworkManager.Instance == null || XRNetworkManager.Instance.Match == null)
                return;

            GUILayout.Space(10);
            GUILayout.Label("Nakama Match Details", UnityEngine.GUI.skin.box);

            GUILayout.BeginVertical();

            GUILayout.Label($"Match ID: {XRNetworkManager.Instance.Match.Id}");
            GUILayout.Label($"Current Network State: {XRNetworkManager.Instance.CurrentNetworkState}");
            GUILayout.Label($"Session Code: {XRNetworkManager.Instance.GetSessionCode()}");
            GUILayout.Label($"Connected Users ({XRNetworkManager.Instance.UserPresences.Count}):");
            foreach (var player in XRNetworkManager.Instance.UserPresences)
            {
                GUILayout.Label($"- {player.Username} ({(XRPlayer.Role)int.Parse(player.role)})");
            }
            GUILayout.EndVertical();
        }
    }
}