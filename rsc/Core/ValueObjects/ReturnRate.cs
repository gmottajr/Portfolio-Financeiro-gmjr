using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedKernel.ValueObjects;


/// <summary>
/// Significa a taxa de retorno de um investimento, expressa em porcentagem.
/// </summary>
public sealed record ReturnRate : ValueObjectBase<Percentage>
{
    public ReturnRate(decimal percentage)
        : base(new Percentage(percentage))
    {
    }

    public override string ToString() => base.ToString();
}
