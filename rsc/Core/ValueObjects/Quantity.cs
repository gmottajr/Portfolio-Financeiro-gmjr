using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharedKernel.Exceptions;

namespace SharedKernel.ValueObjects;

/// <summary>
/// Representa quantidade de ativos. 
/// </summary>
public sealed record Quantity : ValueObjectBase<decimal>
{
    public Quantity(decimal value)
        : base(Validate(value))
    {
    }

    private static decimal Validate(decimal value)
    {
        if (value < 0)
            throw new DomainException("Quantity cannot be negative.");

        return value;
    }

    public static Quantity Zero => new(0);

    public static Quantity operator +(Quantity a, Quantity b)
        => new(a.Value + b.Value);

    public static Quantity operator -(Quantity a, Quantity b)
    {
        if (a.Value < b.Value)
            throw new DomainException("Quantity cannot be negative.");

        return new(a.Value - b.Value);
    }

    public override string ToString() => base.ToString();
}
