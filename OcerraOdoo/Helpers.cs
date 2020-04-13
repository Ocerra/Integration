using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OcerraOdoo
{
    public static class Helpers
    {
        public static string FromBase64(this string encoded)
        {
            byte[] data = System.Convert.FromBase64String(encoded);
            var base64Decoded = System.Text.UTF8Encoding.Default.GetString(data);
            return base64Decoded;
        }

        public static string ToBase64(this string decoded)
        {
            var base64Decoded = System.Text.UTF8Encoding.Default.GetBytes(decoded);
            var encoded = System.Convert.ToBase64String(base64Decoded);
            return encoded;
        }

        public static DateTime ToDate(this string value, DateTime defaultVal) {
            if (DateTime.TryParse(value, out DateTime result)) {
                return result;
            }
            return defaultVal;
        }

        public static DateTimeOffset? ToDateOffset(this string value, DateTimeOffset? defaultVal)
        {
            if (value != null && DateTimeOffset.TryParse(value, out DateTimeOffset result))
            {
                return result;
            }
            return defaultVal;
        }

        public static void LogError(this Exception ex, string message = null)
        {
            var textMessage = message + " \r\n" ?? "";
            textMessage += ex?.ToString() ?? "";

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(textMessage);
            Console.ResetColor();

            EventLog.WriteEntry(
                    ".NET Runtime",
                    textMessage,
                    EventLogEntryType.Error,
                    1000);
        }

        public static bool IsNullOrEmpty<T>(this IEnumerable<T> value)
        {
            return value == null || !value.Any();
        }

        /// <summary>
        /// Determines whether the collection is null or contains no elements.
        /// </summary>
        /// <param name="enumerable">The enumerable, which may be null or empty.</param>
        /// <returns>
        ///     <c>true</c> if the IEnumerable is null or empty; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsNullOrEmpty(this IEnumerable enumerable)
        {
            if (enumerable == null)
            {
                return true;
            }
            /* If this is a list, use the Count property for efficiency. 
             * The Count property is O(1) while IEnumerable.Count() is O(N). */
            var collection = enumerable as ICollection;
            if (collection != null)
            {
                return collection.Count < 1;
            }
            return !enumerable.GetEnumerator().MoveNext();
        }

        public static bool HasItems<T>(this IEnumerable<T> value)
        {
            return !IsNullOrEmpty(value);
        }

        public static bool HasItems<T>(this IList<T> items)
        {
            if (items != null && items.Count > 0) return true;
            return false;
        }

        public static long ToLong(this string val, long defaultVal)
        {
            if (string.IsNullOrEmpty(val)) return defaultVal;

            if (long.TryParse(val, out long result)) return result;

            return defaultVal;
        }

        public static int ToInt(this string val, int defaultVal)
        {
            if (string.IsNullOrEmpty(val)) return defaultVal;

            if (int.TryParse(val, out int result)) return result;

            return defaultVal;
        }

        public static int? ToInt(this string val, int? defaultVal)
        {
            if (string.IsNullOrEmpty(val)) return defaultVal;

            if (int.TryParse(val, out int result)) return result;

            return defaultVal;
        }

        public static Uri ToUri(this string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;

            var validPrefixes = new[] { "http", "www", ".co", ".nz", ".au", ".us" };

            val = val.ToLower();

            if (!validPrefixes.Any(vp => val.Contains(vp))) return null;

            try
            {
                var result = new Uri(val, UriKind.Absolute);
                return result;
            }
            catch {
                return null;
            }

        }

        static void AddUpdateAppSettings(string key, string value)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;
                if (settings[key] == null)
                {
                    settings.Add(key, value);
                }
                else
                {
                    settings[key].Value = value;
                }
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }
            catch (ConfigurationErrorsException ex)
            {
                LogError(ex, "Error writing app settings");
            }
        }
    }

    public class NullableArrayJsonConverter : JsonConverter
    {
        public NullableArrayJsonConverter()
        {

        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JToken t = JToken.FromObject(value);
            t.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.ValueType == typeof(Boolean))
                return null;
            else if(reader.TokenType == JsonToken.StartArray)
            {
                var array = JArray.Load(reader);
                if (array != null && array.Count > 0) {
                    var result = array.Select(a => a?.ToString()).ToArray();
                    return result;
                }
            }
            return null;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanConvert(Type objectType)
        {
            return true;
        }
    }

    public class NullableStringJsonConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JToken t = JToken.FromObject(value);
            t.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.ValueType == typeof(Boolean))
                return null;
            else if (reader.ValueType == typeof(string))
            {
                return reader.Value;
            }
            return null;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanConvert(Type objectType)
        {
            return true;
        }
    }
}
