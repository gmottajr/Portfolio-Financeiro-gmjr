using System.Globalization;
using SharedKernel.Exceptions;
using SharedKernel.ValueObjects;

namespace SharedKernel.Tests.ValueObjects;

public class Moneytests
{
    private const string NegativeMoneyMessage = "Money cannot be negative.";

    // =========================================================================
    // Construção
    // =========================================================================

    [Theory(DisplayName = "AlternativePath: valores válidos (zero e positivos) são aceitos")]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(150.75, 150.75)]
    [InlineData(999999.99, 999999.99)]
    public void Constructor_WithValidValue_AlternativePath_SetsValueCorrectly(decimal input, decimal expected)
    {
        var money = new Money(input);

        Assert.Equal(expected, money.Value);
    }

    [Theory(DisplayName = "ExceptionalPath: valores negativos violam o invariante e lançam DomainException")]
    [InlineData(-0.01)]
    [InlineData(-1)]
    [InlineData(-100.5)]
    [InlineData(-9999999.99)]
    public void Constructor_WithNegativeValue_ExceptionalPath_ThrowsDomainException(decimal value)
    {
        Action act = () => new Money(value);

        var ex = Assert.Throws<DomainException>(act);
        Assert.Equal(NegativeMoneyMessage, ex.Message);
    }

    [Theory(DisplayName = "AlternativePath: valor é arredondado para 2 casas usando banker's rounding (ToEven)")]
    [InlineData(10.125, 10.12)] // dígito 5 é o ponto médio; dígito anterior (2) já é par -> mantém
    [InlineData(10.135, 10.14)] // dígito anterior (3) é ímpar -> sobe para o par (4)
    [InlineData(10.005, 10.00)] // dígito anterior (0) já é par -> mantém
    [InlineData(10.015, 10.02)] // dígito anterior (1) é ímpar -> sobe para o par (2)
    [InlineData(99.995, 100.00)] // ponto médio entre 99.99 (ímpar) e 100.00 (par) -> sobe
    [InlineData(99.999, 100.00)] // não é ponto médio, arredondamento comum para cima
    public void Constructor_RoundsToTwoDecimalPlaces_AlternativePath_UsesBankersRounding(decimal input, decimal expected)
    {
        var money = new Money(input);

        Assert.Equal(expected, money.Value);
    }

    // =========================================================================
    // Zero
    // =========================================================================

    [Fact(DisplayName = "AlternativePath: Money.Zero representa o valor zero")]
    public void Zero_AlternativePath_ReturnsMoneyInstanceWithValueZero()
    {
        var zero = Money.Zero;

        Assert.Equal(0m, zero.Value);
    }

    // =========================================================================
    // Operador +
    // =========================================================================

    [Theory(DisplayName = "AlternativePath: soma de dois valores não-negativos retorna o total esperado")]
    [InlineData(100.50, 50.25, 150.75)]
    [InlineData(0, 100, 100)]
    [InlineData(0, 0, 0)]
    [InlineData(10.005, 10.005, 20.00)] // cada parcela arredonda para 10.00 antes de somar
    [InlineData(99.995, 0.005, 100.00)] // 99.995->100.00 (par) e 0.005->0.00 (par)
    public void Addition_AlternativePath_ReturnsSum(decimal left, decimal right, decimal expected)
    {
        var a = new Money(left);
        var b = new Money(right);

        var result = a + b;

        Assert.Equal(expected, result.Value);
    }
    // Não existe "ExceptionalPath" para +, pois a soma de dois valores não-negativos
    // nunca pode violar o invariante de não-negatividade.

    // =========================================================================
    // Operador -
    // =========================================================================

    [Theory(DisplayName = "AlternativePath: subtração com resultado >= 0 retorna a diferença esperada")]
    [InlineData(100, 30, 70)]
    [InlineData(50, 50, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(150.75, 100.50, 50.25)]
    public void Subtraction_WhenResultIsNonNegative_AlternativePath_ReturnsDifference(decimal left, decimal right, decimal expected)
    {
        var a = new Money(left);
        var b = new Money(right);

        var result = a - b;

        Assert.Equal(expected, result.Value);
    }

    [Theory(DisplayName = "ExceptionalPath: subtração com resultado negativo lança DomainException")]
    [InlineData(10, 20)]
    [InlineData(0, 0.01)]
    [InlineData(99.99, 100.00)]
    public void Subtraction_WhenResultWouldBeNegative_ExceptionalPath_ThrowsDomainException(decimal left, decimal right)
    {
        var a = new Money(left);
        var b = new Money(right);

        Action act = () => { var _ = a - b; };

        var ex = Assert.Throws<DomainException>(act);
        Assert.Equal(NegativeMoneyMessage, ex.Message);
    }

    // =========================================================================
    // Operador *
    // =========================================================================

    [Theory(DisplayName = "AlternativePath: multiplicação por fator não-negativo retorna o produto arredondado")]
    [InlineData(10, 3, 30)]
    [InlineData(500, 0, 0)]
    [InlineData(10, 0.333, 3.33)]
    [InlineData(100, 0.003, 0.30)] // 0,3% de custo de transação (regra do desafio)
    [InlineData(0, 100, 0)]
    public void Multiplication_ByNonNegativeFactor_AlternativePath_ReturnsRoundedProduct(decimal value, decimal factor, decimal expected)
    {
        var money = new Money(value);

        var result = money * factor;

        Assert.Equal(expected, result.Value);
    }

    [Theory(DisplayName = "ExceptionalPath: multiplicação que resulta em valor negativo lança DomainException")]
    [InlineData(10, -1)]
    [InlineData(0.01, -0.5)]
    [InlineData(100, -0.003)]
    public void Multiplication_ByNegativeFactor_ExceptionalPath_ThrowsDomainException(decimal value, decimal factor)
    {
        var money = new Money(value);

        Action act = () => { var _ = money * factor; };

        var ex = Assert.Throws<DomainException>(act);
        Assert.Equal(NegativeMoneyMessage, ex.Message);
    }

    // =========================================================================
    // Operador /
    // =========================================================================

    [Theory(DisplayName = "AlternativePath: divisão por divisor positivo retorna o quociente arredondado")]
    [InlineData(100, 4, 25)]
    [InlineData(10, 3, 3.33)]
    [InlineData(0, 5, 0)]
    [InlineData(51050.00, 4, 12762.50)]
    public void Division_ByPositiveDivisor_AlternativePath_ReturnsRoundedQuotient(decimal value, decimal divisor, decimal expected)
    {
        var money = new Money(value);

        var result = money / divisor;

        Assert.Equal(expected, result.Value);
    }

    [Theory(DisplayName = "ExceptionalPath: divisão por zero lança DivideByZeroException")]
    [InlineData(100, 0)]
    [InlineData(0.01, 0)]
    [InlineData(0, 0)]
    public void Division_ByZero_ExceptionalPath_ThrowsDivideByZeroException(decimal value, decimal divisor)
    {
        var money = new Money(value);

        Action act = () => { var _ = money / divisor; };

        Assert.Throws<DivideByZeroException>(act);
    }

    [Theory(DisplayName = "ExceptionalPath: divisão por divisor negativo (resultado negativo) lança DomainException")]
    [InlineData(100, -2)]
    [InlineData(10, -0.5)]
    public void Division_ByNegativeDivisor_ExceptionalPath_ThrowsDomainException(decimal value, decimal divisor)
    {
        var money = new Money(value);

        Action act = () => { var _ = money / divisor; };

        var ex = Assert.Throws<DomainException>(act);
        Assert.Equal(NegativeMoneyMessage, ex.Message);
    }

    // =========================================================================
    // ToString
    // =========================================================================

    [Theory(DisplayName = "AlternativePath: ToString formata com 2 casas decimais respeitando a cultura corrente")]
    [InlineData("en-US", "1,234.50")]
    [InlineData("pt-BR", "1.234,50")]
    public void ToString_AlternativePath_FormatsAccordingToCurrentCulture(string cultureName, string expected)
    {
        // "N2" depende da cultura corrente da thread (separador decimal/milhar).
        // Isso é relevante para o domínio: os dados do desafio são de ativos
        // negociados na B3 (mercado brasileiro), então a cultura pt-BR é a que
        // normalmente será usada ao exibir esses valores para o usuário final.
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
        try
        {
            var money = new Money(1234.5m);

            Assert.Equal(expected, money.ToString());
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact(DisplayName = "AlternativePath: ToString de Money.Zero retorna '0.00' em en-US")]
    public void ToString_WithZero_AlternativePath_ReturnsZeroFormatted()
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
        try
        {
            Assert.Equal("0.00", Money.Zero.ToString());
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    // =========================================================================
    // Igualdade (record struct → igualdade estrutural por valor)
    // =========================================================================

    [Theory(DisplayName = "AlternativePath: igualdade compara pelo valor já arredondado")]
    [InlineData(100, 100, true)]
    [InlineData(100, 200, false)]
    [InlineData(10.001, 10.004, true)]  // ambos arredondam para 10.00
    [InlineData(10.001, 10.01, false)]  // 10.00 vs 10.01
    public void Equality_ComparesByRoundedValue_AlternativePath(decimal left, decimal right, bool expectedEqual)
    {
        var a = new Money(left);
        var b = new Money(right);

        Assert.Equal(expectedEqual, a == b);
        Assert.Equal(expectedEqual, a.Equals(b));
        Assert.NotEqual(expectedEqual, a != b);

        if (expectedEqual)
        {
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }
    }

    // =========================================================================
    // Cenários de Domínio alinhados ao desafio (README.md / SeedData.json)
    // =========================================================================

    [Fact(DisplayName = "AlternativePath: cálculo de valor investido e valor atual reproduz os dados reais do SeedData (posição PETR4)")]
    public void DomainScenario_Petr4Position_AlternativePath_MatchesSeedDataFigures()
    {
        // Portfólio Conservador (user-001), posição PETR4:
        // quantity = 500, averagePrice = 30.00, currentPrice (Asset) = 35.50
        var averagePrice = new Money(30.00m);
        var currentPrice = new Money(35.50m);
        const int quantity = 500;

        var investedAmount = averagePrice * quantity;
        var currentValue = currentPrice * quantity;

        Assert.Equal(15000.00m, investedAmount.Value);
        Assert.Equal(17750.00m, currentValue.Value);

        // Total Return (%) = (ValorAtual - ValorInvestido) / ValorInvestido * 100
        var gain = currentValue - investedAmount;
        Assert.Equal(2750.00m, gain.Value);

        var totalReturnPercentage = Math.Round((gain.Value / investedAmount.Value) * 100m, 2);
        Assert.Equal(18.33m, totalReturnPercentage);
    }

    [Fact(DisplayName = "AlternativePath: soma das posições de um portfólio via Aggregate reproduz o totalInvestment do SeedData")]
    public void DomainScenario_SumOfPositions_AlternativePath_MatchesPortfolioTotalInvestment()
    {
        // Portfólio Conservador (user-001): soma de quantity * averagePrice de cada posição
        var positions = new[]
        {
            (Quantity: 500, AveragePrice: new Money(30.00m)),  // PETR4
            (Quantity: 300, AveragePrice: new Money(60.00m)),  // VALE3
            (Quantity: 1000, AveragePrice: new Money(18.00m)), // BBDC4
            (Quantity: 600, AveragePrice: new Money(28.00m)),  // ITUB4
            (Quantity: 200, AveragePrice: new Money(45.00m)),  // WEGE3
        };

        var totalInvested = positions.Aggregate(
            Money.Zero,
            (total, position) => total + position.AveragePrice * position.Quantity);

        // 15000 + 18000 + 18000 + 16800 + 9000 = 76800
        Assert.Equal(76800.00m, totalInvested.Value);
    }

    [Theory(DisplayName = "AlternativePath: custo de transação de 0,3% (regra do desafio) é calculado corretamente")]
    [InlineData(1740.00, 5.22)] // BUY ITUB4, conforme exemplo do README
    [InlineData(10000.00, 30.00)]
    [InlineData(100.00, 0.30)]
    public void DomainScenario_TransactionCost_AlternativePath_IsThreeTenthsPercentOfTradeValue(decimal tradeValue, decimal expectedCost)
    {
        var value = new Money(tradeValue);

        var cost = value * 0.003m;

        Assert.Equal(expectedCost, cost.Value);
    }

    [Fact(DisplayName = "IMPORTANTE - Discrepância: exemplo do README (SELL PETR4, R$ 5,33) assume arredondamento comercial, mas Money usa banker's rounding (ToEven), resultando em R$ 5,32")]
    public void DomainScenario_TransactionCost_RoundingDiscrepancy_WithReadmeExample()
    {
        // O README (seção Rebalancing Suggestions) traz como exemplo:
        //   { "action": "SELL", "asset": "PETR4", "value": 15000.00 }  (não usado aqui)
        //   suggestedTrades[0].estimatedValue = 1775.00, transactionCost = 5.33
        //
        // 1775.00 * 0.3% = 5.325, que é EXATAMENTE o ponto médio entre 5.32 e 5.33.
        // O README espera 5.33 (arredondamento comercial, "round half away from zero"),
        // mas Money usa MidpointRounding.ToEven, que arredonda 5.325 para 5.32
        // (dígito final par). Este teste documenta esse comportamento de propósito,
        // para que a divergência seja uma decisão consciente (e não um bug
        // silencioso) ao documentar premissas no README do projeto.
        var estimatedValue = new Money(1775.00m);

        var transactionCost = estimatedValue * 0.003m;

        Assert.Equal(5.32m, transactionCost.Value); // comportamento real do Money (ToEven)
        Assert.NotEqual(5.33m, transactionCost.Value); // valor citado no README do desafio
    }
}
