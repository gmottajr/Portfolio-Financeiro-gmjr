using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abstractions._04_Domain;
using SharedKernel.ValueObjects;

namespace Models.Events;

public sealed class AssetPriceUpdated : DomainEventBase
{
    public AssetSymbol Symbol { get; }
    public Money OldPrice { get; }
    public Money NewPrice { get; }

    public AssetPriceUpdated(AssetSymbol symbol, Money oldPrice, Money newPrice)
    {
        Symbol = symbol;
        OldPrice = oldPrice;
        NewPrice = newPrice;
    }
}
