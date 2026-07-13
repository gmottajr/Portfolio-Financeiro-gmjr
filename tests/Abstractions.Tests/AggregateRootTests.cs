using Abstractions._04_Domain;

namespace Abstractions.Tests;

public sealed class AggregateRootTests
{
    [Fact]
    public void Equals_UsesIdAndRejectsDifferentAggregateType()
    {
        var first = new TestAggregate(10);
        var sameId = new TestAggregate(10);
        var differentId = new TestAggregate(11);

        Assert.Equal(first, sameId);
        Assert.Equal(first.GetHashCode(), sameId.GetHashCode());
        Assert.NotEqual(first, differentId);
        Assert.False(first.Equals(null));
        Assert.False(first.Equals("10"));
    }

    [Fact]
    public void GetHashCode_ReturnsZeroForNullKey()
    {
        var aggregate = new NullableKeyAggregate(null);

        Assert.Equal(0, aggregate.GetHashCode());
    }

    private sealed class TestAggregate : AggregateRoot<int>
    {
        public TestAggregate(int id) => Id = id;
    }

    private sealed class NullableKeyAggregate : AggregateRoot<string>
    {
        public NullableKeyAggregate(string? id) => Id = id!;
    }
}
