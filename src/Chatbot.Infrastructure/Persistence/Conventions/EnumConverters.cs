using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chatbot.Infrastructure.Persistence.Conventions;

/// <summary>Persists an enum as a snake_case token (RagVsFinetune &lt;-&gt; "rag_vs_finetune").</summary>
public sealed class SnakeCaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public SnakeCaseEnumConverter()
        : base(v => EnumTokens.ToToken(v), v => EnumTokens.FromToken<TEnum>(v))
    {
    }
}

public static class EnumTokens
{
    public static string ToToken<TEnum>(TEnum value) where TEnum : struct, Enum
        => ToSnake(value.ToString());

    public static TEnum FromToken<TEnum>(string token) where TEnum : struct, Enum
        => (TEnum)Enum.Parse(typeof(TEnum), FromSnake(token), ignoreCase: true);

    public static string ToSnake(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static string FromSnake(string token)
    {
        var parts = token.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder(token.Length);
        foreach (var part in parts)
        {
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1)
            {
                sb.Append(part.AsSpan(1));
            }
        }

        return sb.ToString();
    }
}

/// <summary>Reads SQL Server datetime2 values back as UTC (the column stores UTC).</summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}
