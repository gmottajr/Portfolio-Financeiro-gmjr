using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharedKernel.Exceptions;

namespace SharedKernel.ValueObjects;


/// <summary>
/// Todo o domínio trabalha com dinheiro, então é importante ter um Value Object para representar o dinheiro de forma consistente e segura.
/// </summary>
public sealed record Money : ValueObjectBase<decimal>
{
    /// <summary>
    /// O valor do dinheiro, sempre arredondado para 2 casas decimais.
    /// </summary>
    /// <summary>
    /// Representa o valor zero de dinheiro.
    /// </summary>
    public static Money Zero => new(0);

    /// <summary>
    /// Cria uma nova instância de Money com o valor especificado. O valor não pode ser negativo e será arredondado para 2 casas decimais.
    /// </summary>
    /// <param name="value"></param>
    /// <exception cref="DomainException"></exception>
    public Money(decimal value)
        : base(Normalize(value))
    {
    }

    private static decimal Normalize(decimal value)
    {
        if (value < 0)
            throw new DomainException("Money cannot be negative.");

        return decimal.Round(value, 2, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Soma dois valores de Money. O resultado será um novo Money com o valor da soma. 
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static Money operator +(Money left, Money right)
        => new(left.Value + right.Value);

    /// <summary>
    /// Subtrai dois valores de Money. O resultado será um novo Money com o valor da subtração. Se o resultado for negativo, uma exceção será lançada.
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    /// <exception cref="DomainException"></exception>
    public static Money operator -(Money left, Money right)
    {
        if (left.Value < right.Value)
            throw new DomainException("Money cannot be negative.");

        return new(left.Value - right.Value);
    }

    /// <summary>
    /// Multiplica um valor de Money por um fator decimal. O resultado será um novo Money com o valor da multiplicação.
    /// </summary>
    /// <param name="money"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static Money operator *(Money money, decimal factor)
        => new(money.Value * factor);

    /// <summary>
    /// Divide um valor de Money por um divisor decimal. O resultado será um novo Money com o valor da divisão. Se o divisor for zero, uma exceção será lançada.
    /// </summary>
    /// <param name="money"></param>
    /// <param name="divisor"></param>
    /// <returns></returns>
    /// <exception cref="DivideByZeroException"></exception>
    public static Money operator /(Money money, decimal divisor)
    {
        if (divisor == 0)
            throw new DivideByZeroException();

        return new(money.Value / divisor);
    }

    /// <summary>
    /// Converte o valor de Money para uma string formatada com duas casas decimais.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => Value.ToString("N2");
}
