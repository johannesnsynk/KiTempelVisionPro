using Newtonsoft.Json.Linq;
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
#if UNITY_EDITOR
        public List<XRPresentation> inspectorPresentations;
#endif

        public delegate void DataReceived();

        public static List<XRPresentation> allPresentations = new List<XRPresentation>();
        public static DataReceived onDataReceived;

        /// <summary>
        /// Convert the string response to a jobject and presentation array
        /// </summary>
        /// <param name="response"></param>
        public void HandleJsonResponse(string response)
        {
            string jsonResponse = response;

            JObject parsedJson = JObject.Parse(jsonResponse);

            if (parsedJson["data"] == null)
                return;

            //Check for presentations being an object or array
            if (parsedJson["data"].Type == JTokenType.Array)
            {
                XRJsonArray presentations = parsedJson.ToObject<XRJsonArray>();
                allPresentations = presentations.data;
            }
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
                HandleJsonResponse(targetFile.text);
            else
            {
                Debug.LogWarning("Could not load local backup file!");
                return;
            }

            onDataReceived?.Invoke();
        }
    }
}