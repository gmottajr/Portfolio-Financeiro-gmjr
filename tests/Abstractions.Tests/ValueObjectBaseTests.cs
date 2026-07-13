using SharedKernel.ValueObjects;

namespace Abstractions.Tests;

public sealed class ValueObjectBaseTests
{
    [Fact]
    public void Constructor_StoresValueAndToStringUsesIt()
    {
        var valueObject = new TestValueObject(42);

        Assert.Equal(42, valueObject.Value);
        Assert.Equal("42", valueObject.ToString());
    }

    [Fact]
    public void Equality_UsesTheEncapsulatedValue()
    {
        Assert.Equal(new TestValueObject(42), new TestValueObject(42));
        Assert.NotEqual(new TestValueObject(42), new TestValueObject(7));
    }

    private sealed record TestValueObject : ValueObjectBase<int>
    {
        public TestValueObject(int value)
            : base(value)
        {
        }

        public override string ToString() => base.ToString();
    }
}
