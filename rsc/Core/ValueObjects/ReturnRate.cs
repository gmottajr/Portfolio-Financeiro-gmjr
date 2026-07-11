using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedKernel.ValueObjects;


/// <summary>
/// Significa a taxa de retorno de um investimento, expressa em porcentagem.
/// </summary>
public readonly record struct ReturnRate
{
    public Percentage Value { get; }

    public ReturnRate(decimal percentage)
    {
        Value = new Percentage(percentage);
    }

    public override string ToString()
        => Value.ToString();
}
