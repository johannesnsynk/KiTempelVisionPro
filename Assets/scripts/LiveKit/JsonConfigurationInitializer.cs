using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

public static class JsonConfigurationInitializer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // Konfiguriert Newtonsoft.Json um BEIDE naming conventions zu akzeptieren
        // Das ermöglicht es, dass serverUrl (camelCase) zu einem Field mit JsonProperty("server_url") gemappt wird
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new JsonConverter[] { new ConnectionDetailsConverter() }
        };
        
        JsonConvert.DefaultSettings = () => settings;
    }
}
