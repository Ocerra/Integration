using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OcerraOdoo
{
    public class OdooKeyValue
    {
        public OdooKeyValue()
        {

        }

        public OdooKeyValue(long key)
        {
            this.Key = key;
        }

        public long? Key { get; set; }
        public string Value { get; set; }

        public override string ToString()
        {
            return Key != null || Value != null ? $"{Key}:{Value}" : null;
        }
    }

    public class OdooString
    {
        readonly string value;
        public OdooString(string value)
        {
            this.value = value;
        }
        public static implicit operator string(OdooString d)
        {
            return d?.value;
        }
        public static implicit operator OdooString(string d)
        {
            return new OdooString(d);
        }

        public string Value => value;

        public override string ToString()
        {
            return value != null ? value.ToString() : null;
        }
    }

    public class OdooDate
    {
        readonly DateTime? value;
        public OdooDate(DateTime? value)
        {
            this.value = value;
        }
        public static implicit operator DateTime?(OdooDate d)
        {
            return d.value;
        }
        public static implicit operator OdooDate(DateTime? d)
        {
            return new OdooDate(d);
        }
        public override string ToString()
        {
            return value != null ? value.ToString() : null;
        }

        public DateTime? Value => value;
    }

    public class OdooDecimal
    {
        readonly Decimal? value;
        public OdooDecimal(Decimal? value)
        {
            this.value = value;
        }
        public static implicit operator Decimal?(OdooDecimal d)
        {
            return d != null ? d.value : null;
        }
        public static implicit operator OdooDecimal(Decimal? d)
        {
            return new OdooDecimal(d);
        }
        public override string ToString()
        {
            return value != null ? value.ToString() : null;
        }

        public Decimal? Value => value;
    }

    public interface IOdooArray
    {
        long[] Ids { get; set; }
        IList Objects { get; }
    }

    public interface IOdooLine
    {
        long Id { get; set; }
    }

    public class OdooArray<TLine> : IOdooArray where TLine : IOdooLine
    {
        public long[] Ids { get; set; }
        public List<TLine> Objects { get; set; }

        IList IOdooArray.Objects
        {
            get => Objects != null ? Objects.Select(o => new[] { 0, 0, (object)o }).ToList() : null;
        }
    }

    public class OdooKeyValueJsonConverter : JsonConverter
    {
        public OdooKeyValueJsonConverter()
        {

        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var keyValue = value as OdooKeyValue;

            //var array = keyValue != null ? (object)new[] { (object)keyValue.Key, (object)keyValue.Value } : false;

            var array = keyValue != null ? (object)keyValue.Key : false;

            JToken t = JToken.FromObject(array);

            t.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.ValueType == typeof(Boolean))
                return null;

            else if (reader.TokenType == JsonToken.StartArray)
            {
                var array = JArray.Load(reader);
                if (array != null && array.Count > 0)
                {
                    var result = array.Select(a => a?.ToString()).ToArray();

                    var keyValue = new OdooKeyValue
                    {
                        Key = result.Length > 0 && int.TryParse(result[0], out int intVal) ? (int?)intVal : null,
                        Value = result.Length > 1 ? result[1] : null
                    };

                    return keyValue;
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
            return objectType == typeof(OdooKeyValue);
        }
    }

    public class OdooObjectArrayJsonConverter : JsonConverter
    {
        public OdooObjectArrayJsonConverter()
        {

        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var odooArray = value as IOdooArray;

            var nullableVal =
                odooArray != null && odooArray.Objects != null ? (object)odooArray.Objects :
                odooArray != null && odooArray.Ids != null ? (object)odooArray.Ids :
                false;

            JToken t = JToken.FromObject(nullableVal);

            t.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.ValueType == typeof(Boolean))
                return null;

            else if (reader.TokenType == JsonToken.StartArray)
            {
                var array = JArray.Load(reader);

                if (array != null && array.Count > 0)
                {
                    //Working with digits only
                    var result = array.Select(a => long.TryParse(a?.ToString(), out long longVal) ? longVal : 0).ToArray();

                    var typedArray = (IOdooArray)Activator.CreateInstance(objectType);

                    typedArray.Ids = result;

                    return typedArray;
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
            return typeof(IOdooArray).IsAssignableFrom(objectType);
        }
    }

    public class OdooArrayJsonConverter : JsonConverter
    {
        public OdooArrayJsonConverter()
        {

        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var nullableVal = value != null ? (object)value : false;

            JToken t = JToken.FromObject(nullableVal);

            t.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.ValueType == typeof(Boolean))
                return null;

            else if (reader.TokenType == JsonToken.StartArray)
            {
                var array = JArray.Load(reader);
                if (array != null && array.Count > 0)
                {
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

    public class OdooDateJsonConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var odooDate = value as OdooDate;
            var date = (DateTime?)odooDate;
            var strVal = date.HasValue ? (object)date.Value.ToString("yyyy-MM-dd") : false;
            JToken t = JToken.FromObject(strVal);
            t.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.ValueType == typeof(Boolean))
                return null;
            else if (reader.ValueType == typeof(string) && DateTime.TryParse((string)reader.Value, out DateTime result))
            {
                return new OdooDate(result);
            }
            return null;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(OdooDate);
        }
    }

    public class OdooDecimalJsonConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var odooDecimal = value as OdooDecimal;
            var dec = (decimal?)odooDecimal;
            var nullableVal = dec.HasValue ? (object)dec : false;
            JToken t = JToken.FromObject(nullableVal);
            t.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.ValueType == typeof(bool))
                return null;
            else if (reader.ValueType == typeof(double))
            {
                return new OdooDecimal(((decimal)(double)reader.Value));
            }
            else if (reader.ValueType == typeof(int))
            {
                return new OdooDecimal(((decimal)(int)reader.Value));
            }
            else if (reader.ValueType == typeof(decimal))
            {
                return new OdooDecimal((decimal)reader.Value);
            }
            return null;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(OdooDecimal);
        }
    }

    public class OdooStringJsonConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var odooStr = value as OdooString;
            var strVal = (string)odooStr;
            var nullableValue = strVal != null ? (object)strVal : false;
            JToken t = JToken.FromObject(nullableValue);
            t.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.ValueType == typeof(Boolean))
                return null;
            else if (reader.ValueType == typeof(string))
            {
                return new OdooString((string)reader.Value);
            }
            return null;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(OdooString);
        }
    }
}
