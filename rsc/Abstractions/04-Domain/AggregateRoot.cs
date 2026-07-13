using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abstractions._04_Domain;

public abstract class AggregateRoot<TKey>: AuditableEntity
{
    public TKey Id { get; protected set; } = default!;

    public override bool Equals(object? obj)
    {
        if (obj is not AggregateRoot<TKey> other)
            return false;

        return EqualityComparer<TKey>.Default.Equals(Id, other.Id);
    }

    public override int GetHashCode()
    {
        return Id is null
            ? 0
            : EqualityComparer<TKey>.Default.GetHashCode(Id);
    }
}
