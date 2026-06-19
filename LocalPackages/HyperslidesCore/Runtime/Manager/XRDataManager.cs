using Newtonsoft.Json.Linq;
using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Runtime;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NSYNK.HyperSlides.Core
{
    /// <summary>
    /// This class is used to manage the data for the XR presentations.
    /// It handles the JSON response from the server and converts it to a list of XRPresentation objects.
    /// It also provides a method to load a local backup file from the Resources folder.
    /// </summary>
    public class XRDataManager : Singleton<XRDataManager>
    {
        public List<XRPresentation> AllPresentations = new();
        public event Action OnDataReceived;

        /// <summary>
        /// Convert the string response to a jobject and presentation array
        /// </summary>
        /// <param name="response"></param>
        public List<T> HandleJsonResponse<T>(string response)
        {
            string jsonResponse = response;

            JObject parsedJson = JObject.Parse(jsonResponse);

            //Check for session transforms being an object or array
            if (parsedJson["data"] != null)
            {
                if (parsedJson["data"].Type == JTokenType.Array)
                {
                    XRJsonArray<T> dataArray = parsedJson.ToObject<XRJsonArray<T>>();

                    if (typeof(T) == typeof(XRPresentation))
                        AllPresentations = dataArray.data as List<XRPresentation>;

                    return dataArray.data;
                }
                else
                {
                    XRJson<T> dataObject = parsedJson.ToObject<XRJson<T>>();

                    if (typeof(T) == typeof(XRPresentation))
                        AllPresentations = new List<XRPresentation> { dataObject.data as XRPresentation };

                    return new List<T> { dataObject.data };
                }
            }
            else
                Debug.LogWarning("JSON response does not contain 'data' field: " + jsonResponse, Instance);

            return new List<T>();
        }

        /// <summary>
        /// Load the local resources folder textfile as last backup instance
        /// </summary>
        /// <returns></returns>
        public void LoadResourceTextfile()
        {
            Debug.Log("Loading local backup file for: ", this);

            TextAsset targetFile = Resources.Load<TextAsset>("SampleData");

            if (targetFile)
                HandleJsonResponse<XRPresentation>(targetFile.text);
            else
            {
                Debug.LogWarning("Could not load local backup file!");
                return;
            }

            OnDataReceived?.Invoke();
        }
    }
}