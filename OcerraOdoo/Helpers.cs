using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using OcerraOdoo.Properties;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OcerraOdoo
{
    public static class Helpers
    {
        public static string Trim(this string value, int length)
        {
            if (value != null && value.Length > length)
                return value.Substring(0, length);
            return value;
        }

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

            if(ex.InnerException != null)
                textMessage += "\r\n---------------\r\n" + ex.InnerException.ToString();

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(textMessage);
            Console.ResetColor();

            EventLog.WriteEntry(
                    ".NET Runtime",
                    textMessage,
                    EventLogEntryType.Error,
                    1000);

            File.AppendAllText("LogError.txt", textMessage);
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

        private static string SettingsName = "Settings.json";
        public static void AddUpdateAppSettings(string key, string value)
        {
            try
            {
                var path = Path.Combine(GetDirectory(), SettingsName);
                var stringContent = File.Exists(path) ? File.ReadAllText(path) : null;
                var settings = stringContent.FromJson<SettingContainer>() ?? new SettingContainer();

                switch (key)
                {
                    case "LastVendorSyncDate":
                        settings.LastVendorSyncDate = value;
                        break;
                    case "LastPurchaseSyncDate":
                        settings.LastPurchaseSyncDate = value;
                        break;
                    case "LastInvoiceSyncDate":
                        settings.LastInvoiceSyncDate = value;
                        break;
                }

                File.WriteAllText(path, settings.ToJson());

            }
            catch (Exception ex)
            {
                LogError(ex, "Error writing app settings");
            }
        }

        public static string AppSetting(string key)
        {
            try
            {
                var path = Path.Combine(GetDirectory(), SettingsName);
                var stringContent = File.Exists(path) ? File.ReadAllText(path) : null;
                var settings = stringContent.FromJson<SettingContainer>() ?? new SettingContainer();

                if (key == "LastVendorSyncDate")
                    return settings.LastVendorSyncDate ?? Settings.Default.LastVendorSyncDate;
                else if (key == "LastPurchaseSyncDate")
                    return settings.LastPurchaseSyncDate ?? Settings.Default.LastPurchaseSyncDate;
                else if (key == "LastInvoiceSyncDate")
                    return settings.LastInvoiceSyncDate ?? Settings.Default.LastInvoiceSyncDate;

                return null;
            }
            catch (Exception ex)
            {
                LogError(ex, "Error writing app settings");
                return null;
            }
        }

        public static string GetDirectory() {
            return new FileInfo(System.Reflection.Assembly.GetCallingAssembly().CodeBase.Replace("file:///", ""))
                .Directory.Parent.FullName;
        }

        public static string ToJson<T>(this T obj)
        {
            if (obj == null) return null;
            try
            {
                var microsoftDateFormatSettings = new JsonSerializerSettings
                {
                    DateFormatHandling = DateFormatHandling.IsoDateFormat,
                    DateTimeZoneHandling = DateTimeZoneHandling.Unspecified,
                    StringEscapeHandling = StringEscapeHandling.EscapeHtml,
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented,
                    Converters = new JsonConverter[]
                    {
                        new Newtonsoft.Json.Converters.StringEnumConverter(),
                    },
                    MaxDepth = 10,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };
                return JsonConvert.SerializeObject(obj, microsoftDateFormatSettings);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static T FromJson<T>(this string value)
        {
            if (value.IsNullOrEmpty()) return default(T);

            JsonSerializerSettings settings = new JsonSerializerSettings();
            var result = (T)JsonConvert.DeserializeObject(value, typeof(T), settings);
            return result;
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

    public class SettingContainer {
        public string LastVendorSyncDate { get; set; }
        public string LastPurchaseSyncDate { get; set; }
        public string LastInvoiceSyncDate { get; set; }
    }
}
