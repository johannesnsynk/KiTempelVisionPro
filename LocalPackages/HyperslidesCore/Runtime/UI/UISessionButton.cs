using System;
using System.Collections;
using System.Collections.Generic;
using Nakama;
using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Runtime;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace NSYNK.HyperSlides.UI
{
    public class UISessionButton : MonoBehaviour
    {
        public RawImage sessionImage;
        public TMPro.TextMeshProUGUI sessionName;
        public MeshRenderer sphereMeshRenderer;

        private UnityWebRequest www;
        private UIButton uIButton;

        void OnEnable()
        {
            uIButton = GetComponent<UIButton>();
        }

        public void Init(IApiMatch match)
        {
            XRNetworkObjects.MatchLabel matchLabel = JsonUtility.FromJson<XRNetworkObjects.MatchLabel>(match.Label);
            sessionName.text = string.IsNullOrEmpty(matchLabel.name) ? match.MatchId : matchLabel.name;

            uIButton.onClick.AddListener(() => HyperSlidesStateManager.UpdateAppState(HyperSlidesStateManager.AppState.JOIN_SESSION, match.MatchId));

            XRPresentation foundPresentation = XRDataManager.allPresentations.Find(p => p.id == matchLabel.presentationId);

            if (foundPresentation && !string.IsNullOrEmpty(foundPresentation.cover))
                StartCoroutine(GetPresentationCover(foundPresentation.cover));
        }

        public void OnDestroy()
        {
            uIButton.onClick.RemoveAllListeners();
        }

        IEnumerator GetPresentationCover(string coverURL)
        {
            using (www = UnityWebRequestTexture.GetTexture(coverURL))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log(www.error);
                }
                else
                {
                    //Debug.Log("Got cover from URL: " + coverURL);
                    Texture2D coverTexture = DownloadHandlerTexture.GetContent(www);

                    if (coverTexture == null)
                        yield break;

                    sessionImage.texture = coverTexture;
                    sessionImage.GetComponent<AspectRatioFitter>().aspectRatio = (float)coverTexture.width / coverTexture.height;

                    sphereMeshRenderer.material.SetTexture("_BaseMap", coverTexture);
                    sphereMeshRenderer.material.SetTexture("_EmissionMap", coverTexture);
                }
            }
        }
    }
}