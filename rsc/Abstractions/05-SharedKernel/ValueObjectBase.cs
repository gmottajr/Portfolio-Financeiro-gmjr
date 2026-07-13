namespace SharedKernel.ValueObjects;

/// <summary>
/// Base imutável para objetos de valor do domínio.
/// A igualdade de <c>record</c> inclui o tipo concreto e este valor.
/// </summary>
/// <typeparam name="TValue">O tipo do valor encapsulado.</typeparam>
public abstract record ValueObjectBase<TValue>
{
    public TValue Value { get; }

    protected ValueObjectBase(TValue value)
    {
        Value = value;
    }

    public override string ToString() => Value?.ToString() ?? string.Empty;
}
