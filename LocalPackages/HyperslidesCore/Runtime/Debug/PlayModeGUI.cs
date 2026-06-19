using System.Collections;
using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Runtime;
using NSYNK.HyperSlides.UI;
using UnityEngine;

namespace NSYNK.HyperSlides
{
    /// <summary>
    /// A simple in-game GUI for play mode testing, allowing to toggle application focus handling, leave the match, and navigate slides.
    /// </summary>
    public class PlayModeGUI : MonoBehaviour
    {
#if UNITY_EDITOR
        private bool showGUI, showSlideDetails, cameraOverrideInProgress, cameraOverrideSuccess = false;
        private float dissolveProgress = 0f;

        private GUIStyle buttonStyle, disabledButtonStyle, headlineStyle, tagStyle;
        private Texture2D slidePreview;
        private Vector2 scrollPosition, rightScrollPosition = Vector2.zero;

        private void Awake()
        {
            // Create a new white texture
            slidePreview = new Texture2D(1, 1);
            slidePreview.SetPixel(0, 0, Color.white);
            slidePreview.Apply();
        }

        void OnEnable()
        {
            XRSlideManager.Instance.OnDissolveInProgressNormalized += UpdateDissolveInProgress;
            XRSlideManager.Instance.OnDissolveOutProgressNormalized += UpdateDissolveOutProgress;
        }

        void OnDisable()
        {
            if (XRSlideManager.Instance == null)
                return;

            XRSlideManager.Instance.OnDissolveInProgressNormalized -= UpdateDissolveInProgress;
            XRSlideManager.Instance.OnDissolveOutProgressNormalized -= UpdateDissolveOutProgress;
        }

        /// <summary>
        /// Style for the headline text
        /// </summary>
        /// <returns></returns>
        private GUIStyle HeadlineStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 22;
            style.fontStyle = FontStyle.Bold;
            return style;
        }

        /// <summary>
        /// Style for the tag text
        /// </summary>
        /// <returns></returns>
        private GUIStyle TagStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 10;
            style.fontStyle = FontStyle.Italic;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.background = Texture2D.whiteTexture;
            style.normal.textColor = Color.black;
            style.hover.background = Texture2D.whiteTexture;
            style.hover.textColor = Color.black;
            return style;
        }

        /// <summary>
        /// Style for the buttons
        /// </summary>
        /// <returns></returns>
        private GUIStyle ButtonStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.fixedHeight = 40f;
            return style;
        }

        /// <summary>
        /// Style for disabled buttons
        /// </summary>
        /// <returns></returns>
        private GUIStyle DisabledButtonStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.fixedHeight = 40f;
            style.normal.textColor = Color.gray;
            style.hover.textColor = Color.gray;
            style.active.textColor = Color.gray;
            return style;
        }

        private void UpdateDissolveInProgress(float progress)
        {
            dissolveProgress = progress;
        }

        private void UpdateDissolveOutProgress(float progress)
        {
            dissolveProgress = 1f - progress;
        }

        private void OnGUI()
        {
            buttonStyle = ButtonStyle();
            headlineStyle = HeadlineStyle();
            tagStyle = TagStyle();
            disabledButtonStyle = DisabledButtonStyle();

            GUILayout.BeginArea(new Rect(Screen.width - 220, Screen.height - 320, 200, 300));
            rightScrollPosition = GUILayout.BeginScrollView(rightScrollPosition, GUILayout.Width(200), GUILayout.Height(300));
            GUILayout.BeginVertical();

            if (GUILayout.Button("Application Focus: " + (XRNetworkManager.Instance.EnableApplicationFocusHandling ? "ON" : "OFF"), buttonStyle))
            {
                XRNetworkManager.Instance.EnableApplicationFocusHandling = !XRNetworkManager.Instance.EnableApplicationFocusHandling;
            }

            if (XRNetworkManager.Instance.Match != null)
            {
                if (GUILayout.Button("Toggle Debug GUI: " + (showGUI ? "ON" : "OFF"), buttonStyle))
                {
                    showGUI = !showGUI;

                    if (!showGUI)
                        XRUIManager.Instance.LoadUserUI(true);
                    else
                        XRUIManager.Instance.HideAll();
                }

                if (showGUI)
                    ShowMatchControlUI();
            }

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (showSlideDetails && showGUI)
                ShowSlideDetails();
        }

        private void ShowMatchControlUI()
        {
            GUIStyle labelStyleCentered = new GUIStyle(GUI.skin.label);
            labelStyleCentered.alignment = TextAnchor.MiddleCenter;
            labelStyleCentered.fontSize = 14; 
            labelStyleCentered.fixedHeight = 30f;

            if (XRSlideManager.Instance.CurrentPresentation != null && XRSlideManager.Instance.GetPresentationData() != null)
                GUILayout.Label("Slide: " + XRSlideManager.Instance.CurrentSlidePosition + " / " + XRSlideManager.Instance.GetPresentationData().contents.Count, labelStyleCentered);

            if (slidePreview != null)
            {
                float maxWidth = 180f;
                float normalizedWidth = Mathf.Clamp01(dissolveProgress) * maxWidth;

                Rect previewRect = GUILayoutUtility.GetRect(normalizedWidth, 5f, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                previewRect.x = 10;
                GUI.DrawTexture(previewRect, slidePreview, ScaleMode.ScaleAndCrop);
            }

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("<-", buttonStyle))
            {
                XRSlideManager.Instance.PreviousSlide();
            }

            if (GUILayout.Button("->", buttonStyle))
            {
                XRSlideManager.Instance.NextSlide();
            }

            GUILayout.EndHorizontal();

            if (GUILayout.Button("Show slide details: " + (showSlideDetails ? "ON" : "OFF"), buttonStyle))
            {
                showSlideDetails = !showSlideDetails;
            }

            if (GUILayout.Button("Leave match", buttonStyle))
            {
                showGUI = false;
                XRNetworkManager.Instance.LeaveMatch();
            }
        }

        private void ShowSlideDetails()
        {
            // Draw background
            Rect areaRect = new Rect(20, Screen.height - 320, 400, 300);
            Color prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f); // semi-transparent black
            GUI.Box(areaRect, GUIContent.none);
            GUI.color = prevColor;

            XRSlide currentSlide = XRSlideManager.Instance.GetCurrentSlide();

            if (currentSlide == null)
                return;

            GUILayout.BeginArea(areaRect);
            GUILayout.BeginVertical();

            if (!cameraOverrideInProgress)
            {
                if (GUILayout.Button("Update Spectator Camera", buttonStyle))
                {
                    cameraOverrideInProgress = true;
                    UpdateSpectatorCameraOnServer();
                }
            }
            else
            {
                if (cameraOverrideSuccess)
                    GUILayout.Label("Camera Transform updated!", disabledButtonStyle);
                else
                    GUILayout.Label("Camera Transform could not be updated!", disabledButtonStyle);
            }

            GUILayout.Label(currentSlide.cueNumber + " " + currentSlide.displayName, headlineStyle);
            GUILayout.Label(currentSlide.annotation);

            GUILayout.EndVertical();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(areaRect.xMax + 20, areaRect.y, 200, areaRect.height));
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Width(200), GUILayout.Height(300));
            GUILayout.BeginVertical();
            currentSlide.contentTags.ForEach(tag => GUILayout.Label(tag.name, tagStyle));
            currentSlide.contentTags.ForEach(tag => GUILayout.Label(tag.name, tagStyle));
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void UpdateSpectatorCameraOnServer()
        {
            if (XRNetworkManager.Instance.Match == null || XRSlideManager.Instance.CurrentPresentation == null)
                return;

            string url =
                HyperSlidesStateManager.Instance.CurrentServerConnection.Protocol + "://" +
                HyperSlidesStateManager.Instance.CurrentServerConnection.BackendIP +
                "/api/presentations/" + XRSlideManager.Instance.CurrentPresentation.id + "/updateSlide";

            Transform camTransform = XRCameraManager.CurrentCamera.transform;
            XRSlide currentSlide = XRSlideManager.Instance.GetCurrentSlide();

            CameraTransformOverride cameraTransformOverride = new()
            {
                data = new CameraTransformOverride.Data
                {
                    id = currentSlide.id,
                    enabled = true,
                    cameraTransform = new CameraTransformOverride.CameraTransform
                    {
                        location = new XRNetworkObjects.SerializedVector(camTransform.position),
                        rotation = new XRNetworkObjects.SerializedVector(camTransform.rotation.eulerAngles),
                        scale = new XRNetworkObjects.SerializedVector(camTransform.localScale)
                    }
                }
            };

            StartCoroutine(SendPostRequestToUpdate(url, cameraTransformOverride));
        }

        private IEnumerator SendPostRequestToUpdate(string url, CameraTransformOverride payload)
        {
            string jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            byte[] postData = System.Text.Encoding.UTF8.GetBytes(jsonData);

            using (UnityEngine.Networking.UnityWebRequest www = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
            {
                www.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(postData);
                www.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Error sending spectator camera update: " + www.error, this);
                    cameraOverrideSuccess = false;
                }
                else
                {
                    Debug.Log("Spectator camera update sent successfully!", this);
                    cameraOverrideSuccess = true;
                }
            }

            yield return new WaitForSeconds(2);
            cameraOverrideInProgress = false;
        }
#endif
    }
}