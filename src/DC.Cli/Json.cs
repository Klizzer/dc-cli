using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DC.Cli
{
    public static class Json
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented
        };
        
        public static string Serialize(object source)
        {
            return JsonConvert.SerializeObject(source, Settings);
        }

        public static T DeSerialize<T>(string source)
        {
            return JsonConvert.DeserializeObject<T>(source, Settings);
        }
    }
}