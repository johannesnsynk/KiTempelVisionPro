using LiveKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

public class ConnectionDetailsConverter : JsonConverter<ConnectionDetails>
{
    public override ConnectionDetails ReadJson(JsonReader reader, Type objectType, ConnectionDetails existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        JObject jObject = JObject.Load(reader);
        
        // Versuche BEIDE naming conventions zu lesen
        string serverUrl = jObject["server_url"]?.Value<string>() ?? jObject["serverUrl"]?.Value<string>();
        string participantToken = jObject["participant_token"]?.Value<string>() ?? jObject["participantToken"]?.Value<string>();
        
        if (string.IsNullOrEmpty(serverUrl))
            throw new JsonException("server_url or serverUrl property not found in ConnectionDetails");
        if (string.IsNullOrEmpty(participantToken))
            throw new JsonException("participant_token or participantToken property not found in ConnectionDetails");
        
        // ConnectionDetails ist ein Struct mit public Fields, also direct assignment
        return new ConnectionDetails
        {
            ServerUrl = serverUrl,
            ParticipantToken = participantToken
        };
    }

    public override void WriteJson(JsonWriter writer, ConnectionDetails value, JsonSerializer serializer)
    {
        JObject jObject = new JObject
        {
            { "server_url", value.ServerUrl },
            { "participant_token", value.ParticipantToken }
        };
        jObject.WriteTo(writer);
    }
}
