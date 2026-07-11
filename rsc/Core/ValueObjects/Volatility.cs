using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharedKernel.Exceptions;

namespace SharedKernel.ValueObjects;


/// <summary>
/// Métrica financeira específica que mede a volatilidade de um ativo ou portfólio de investimentos.
/// </summary>
public readonly record struct Volatility
{
    public Percentage Value { get; }

    public Volatility(decimal percentage)
    {
        if (percentage < 0)
            throw new DomainException("Volatility cannot be negative.");

        Value = new Percentage(percentage);
    }

    public override string ToString()
        => Value.ToString();
}
