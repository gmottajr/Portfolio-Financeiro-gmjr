using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedKernel.Mathematics;

public static class FinancialMath
{
    /// <summary>Return (%) = (current value - invested value) / invested value × 100.</summary>
    public static decimal CalculateReturn(
        decimal invested,
        decimal current)
    {
        if (invested == 0)
            return 0;

        return (current - invested) / invested * 100;
    }

    /// <summary>Weight (%) = position market value / portfolio market value × 100.</summary>
    public static decimal CalculateWeight(
        decimal position,
        decimal portfolio)
    {
        if (portfolio == 0)
            return 0;

        return position / portfolio * 100;
    }

    /// <summary>Transaction cost = trade value × 0.3%.</summary>
    public static decimal CalculateTransactionCost(
        decimal value)
    {
        return value * 0.003m;
    }
}
