using System.Collections;
using NSYNK.HyperSlides.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityMainThreadDispatcher;

namespace NSYNK.HyperSlides.UI
{
    public class UISlideButton : UIButton
    {
        public XRSlide slide;
        public Image thumbnailImage;
        public GameObject activeSlideIndicator;
        public MeshRenderer sphereMeshRenderer;

        private UnityWebRequest www;

        public override void EnableInteraction()
        {
            base.EnableInteraction();

            Dispatcher.Enqueue(() =>
            {
                if (XRSlideManager.GetCurrentSlide() && interactable)
                {
                    interactable = slide.id != XRSlideManager.GetCurrentSlide().id;

                    if (activeSlideIndicator)
                        activeSlideIndicator.SetActive(!interactable);
                }
            });
        }

        public void LoadThumbnail()
        {
            Dispatcher.Enqueue(() =>
            {
                if (slide && !string.IsNullOrEmpty(slide.preview))
                    StartCoroutine(GetSlideThumbnail(slide.preview));
            });
        }

        IEnumerator GetSlideThumbnail(string coverURL)
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

                    if (thumbnailImage != null)
                    {
                        thumbnailImage.color = Color.white;
                        thumbnailImage.sprite = Sprite.Create(coverTexture, new Rect(0, 0, coverTexture.width, coverTexture.height), Vector2.zero, 48f, 0, SpriteMeshType.FullRect);
                        thumbnailImage.GetComponent<AspectRatioFitter>().aspectRatio = (float)coverTexture.width / coverTexture.height;
                    }

                    if (sphereMeshRenderer != null)
                    {
                        sphereMeshRenderer.material.SetTexture("_BaseMap", coverTexture);
                        sphereMeshRenderer.material.SetTexture("_EmissionMap", coverTexture);
                    }
                }
            }
        }

        private void ClearResources()
        {
            if (thumbnailImage && thumbnailImage.sprite != null)
            {
                Destroy(thumbnailImage.sprite.texture);
                Destroy(thumbnailImage.sprite);
            }

            if (www != null)
            {
                www.Dispose();
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            ClearResources();
        }

        protected override void OnDestroy()
        {
            ClearResources();
        }
    }
}