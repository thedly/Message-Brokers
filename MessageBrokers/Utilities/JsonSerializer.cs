using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Linq;

namespace MessageBrokers.Utilities
{
    public interface ISerializationManager
    {
        string Serialize(object obj);
        T Deserialize<T>(string input);

    }

    internal class JsonSerializationManager : ISerializationManager
    {
        private readonly JsonSerializerSettings settings;

        public JsonSerializationManager()
        {
            this.settings = new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            this.settings.Converters.Add(new StringEnumConverter());
        }

        public T Deserialize<T>(string input)
        {
            


            var data = JsonConvert.DeserializeObject<T>(input, this.settings);


            return (T)data;
        }

        public string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, this.settings);
        }
    }
}
