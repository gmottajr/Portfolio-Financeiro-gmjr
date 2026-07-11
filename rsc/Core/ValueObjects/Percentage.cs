using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedKernel.ValueObjects;


/// <summary>
/// Conceito transversal para retornos e métricas de performance, representando um valor percentual.
/// </summary>
public readonly record struct Percentage
{
    public decimal Value { get; }

    public static Percentage Zero => new(0m);

    public Percentage(decimal value)
    {
        Value = decimal.Round(value, 4);
    }

    public decimal AsFraction()
        => Value / 100m;

    public static Percentage FromFraction(decimal fraction)
        => new(fraction * 100m);

    public static Percentage operator +(Percentage a, Percentage b)
        => new(a.Value + b.Value);

    public static Percentage operator -(Percentage a, Percentage b)
        => new(a.Value - b.Value);

    public static Percentage operator *(Percentage a, decimal factor)
        => new(a.Value * factor);

    public override string ToString()
        => $"{Value:N2}%";
}
