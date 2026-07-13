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
public sealed record Volatility : ValueObjectBase<Percentage>
{
    public Volatility(decimal percentage)
        : base(CreatePercentage(percentage))
    {
    }

    private static Percentage CreatePercentage(decimal percentage)
    {
        if (percentage < 0)
            throw new DomainException("Volatility cannot be negative.");

        return new Percentage(percentage);
    }

    public override string ToString() => base.ToString();
}
