using System.Text.RegularExpressions;
using SharedKernel.Exceptions;

namespace SharedKernel.ValueObjects;

/// <summary>
/// Identidade de ativo com regras próprias de validação.
/// </summary>
public sealed partial record AssetSymbol : ValueObjectBase<string>
{
    private static readonly Regex Regex =
        SymbolRegex();

    public AssetSymbol(string value)
        : base(Normalize(value))
    {
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Asset symbol is required.");

        value = value.Trim().ToUpperInvariant();

        if (!Regex.IsMatch(value))
            throw new DomainException($"Invalid asset symbol '{value}'.");

        return value;
    }

    // B3SA3 is a valid B3 ticker even though it does not use the usual
    // four-letter prefix (for example, PETR4 or TAEE11).
    [GeneratedRegex(@"^(?:[A-Z]{4}|[A-Z][0-9][A-Z]{2})[0-9]{1,2}$")]
    private static partial Regex SymbolRegex();

    public override string ToString() => base.ToString();

    public static implicit operator string(AssetSymbol symbol)
        => symbol.Value;
}
