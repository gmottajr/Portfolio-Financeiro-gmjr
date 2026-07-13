using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SharedKernel.Exceptions;

namespace SharedKernel.ValueObjects;

/// <summary>
/// Identidade de ativo com regras próprias de validação.
/// </summary>
public readonly partial record struct AssetSymbol
{
    private static readonly Regex Regex =
        SymbolRegex();

    public string Value { get; }

    public AssetSymbol(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Asset symbol is required.");

        value = value.Trim().ToUpperInvariant();

        if (!Regex.IsMatch(value))
            throw new DomainException($"Invalid asset symbol '{value}'.");

        Value = value;
    }

    // B3SA3 is a valid B3 ticker even though it does not use the usual
    // four-letter prefix (for example, PETR4 or TAEE11).
    [GeneratedRegex(@"^(?:[A-Z]{4}|[A-Z][0-9][A-Z]{2})[0-9]{1,2}$")]
    private static partial Regex SymbolRegex();

    public override string ToString()
        => Value;

    public static implicit operator string(AssetSymbol symbol)
        => symbol.Value;
}
