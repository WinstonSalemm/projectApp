using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Tests;

sealed class LenientStringEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString() ?? string.Empty;
            // Прямая попытка (без чувствит. к регистру)
            if (Enum.TryParse<TEnum>(s, ignoreCase: true, out var val))
                return val;

            // Нормализация: убираем - _ пробелы, делаем PascalCase первую букву
            var compact = new string(s.Replace("-", "").Replace("_", "").Replace(" ", "").ToCharArray());
            if (compact.Length > 0)
                compact = char.ToUpperInvariant(compact[0]) + compact.Substring(1);

            if (Enum.TryParse<TEnum>(compact, ignoreCase: true, out val))
                return val;

            throw new JsonException($"Cannot convert \"{s}\" to {typeof(TEnum).Name}.");
        }
        if (reader.TokenType == JsonTokenType.Number)
        {
            var num = reader.GetInt32();
            return (TEnum)Enum.ToObject(typeof(TEnum), num);
        }
        throw new JsonException($"Unexpected token {reader.TokenType} when parsing enum {typeof(TEnum).Name}.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}

public static class TestJson
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    static TestJson()
    {
        // Конвертер для PaymentType (нужен в интеграционных тестах)
        Web.Converters.Add(new LenientStringEnumConverter<PaymentType>());
        // Плюс стандартный, чтобы прочие enum тоже писались строками
        Web.Converters.Add(new JsonStringEnumConverter());
    }
}
