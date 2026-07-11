using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abstractions._04_Domain;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
