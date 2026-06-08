namespace Chatbot.Infrastructure.Persistence;

/// <summary>Database schema names.</summary>
public static class Schemas
{
    public const string Auth = "auth";
    public const string Dbo = "dbo";
    public const string Rag = "rag";
    public const string Rbl = "rbl";
}

/// <summary>Reusable column type/collation constants.</summary>
public static class ColumnTypes
{
    public const string Sha256 = "char(64)";
    public const string BinaryCollation = "Latin1_General_100_BIN2";
    public const string EmailCollation = "Latin1_General_100_CI_AS";
    public const string VietnameseText = "Vietnamese_100_CI_AI";
    public const string Json = "nvarchar(max)";
}
