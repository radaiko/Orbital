// src/Orbital.Core/DateParsing/ParseResult.cs
namespace Orbital.Core.DateParsing;

public readonly record struct ParseResult
{
    public bool IsEmpty { get; init; }
    public bool IsError { get; init; }
    public DateOnly? Date { get; init; }
    public string? ErrorMessage { get; init; }

    public static ParseResult Empty() => new() { IsEmpty = true };
    public static ParseResult Ok(DateOnly d) => new() { Date = d };
    public static ParseResult Error(string msg) => new() { IsError = true, ErrorMessage = msg };
}
