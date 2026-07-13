using SharedKernel.ValueObjects;

namespace SharedKernel.Tests.ValueObjects;

public class ValueObjectInheritanceTests
{
    [Theory]
    [InlineData(typeof(AssetSymbol))]
    [InlineData(typeof(Money))]
    [InlineData(typeof(Percentage))]
    [InlineData(typeof(Quantity))]
    [InlineData(typeof(ReturnRate))]
    [InlineData(typeof(Volatility))]
    public void AllValueObjects_InheritFromValueObjectBase(Type valueObjectType)
    {
        Assert.True(IsValueObject(valueObjectType));
    }

    private static bool IsValueObject(Type type) =>
        type.BaseType is { IsGenericType: true } baseType &&
        baseType.GetGenericTypeDefinition() == typeof(ValueObjectBase<>);
}
