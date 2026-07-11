using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abstractions._04_Domain;

public abstract class AuditableEntity: EntityBase
{
    public DateTime CreatedAt { get; protected set; }

    public DateTime? UpdatedAt { get; protected set; }

    protected AuditableEntity()
    {
        CreatedAt = DateTime.UtcNow;
    }

    protected void MarkAsUpdated(DateTime? when = null)
    {
        UpdatedAt = when ?? DateTime.UtcNow;
    }
}
