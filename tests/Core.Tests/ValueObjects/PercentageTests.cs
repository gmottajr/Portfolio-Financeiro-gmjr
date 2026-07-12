using System.Globalization;
using SharedKernel.Exceptions;
using SharedKernel.ValueObjects;

namespace SharedKernel.Tests.ValueObjects;


/// <summary>
/// Testes do Value Object <see cref="Percentage"/>.
///
/// Diferenças importantes em relação a <see cref="Money"/>, que moldam esta suíte:
///   1. Percentage NÃO valida sinal: valores negativos são um "AlternativePath" válido
///      (retornos, desvios de rebalanceamento e variações podem ser negativos),
///      diferente de Money, onde negativo é sempre "ExceptionalPath".
///      Por isso não existe uma seção "ExceptionalPath" para o construtor aqui.
///   2. Percentage arredonda para 4 casas decimais (não 2), preservando mais
///      precisão internamente — o que importa para cálculos encadeados
///      (ex.: Sharpe Ratio, annualizedReturn) antes de exibir o valor.
///   3. Não existe operador de divisão em Percentage (apenas AsFraction(), que
///      divide por uma constante 100m — nunca lança DivideByZeroException).
///      Por isso não há testes de "divisão por zero" aqui.
///
/// Organização: "AlternativePath" (fluxo de sucesso) e "ExceptionalPath" (fluxo de
/// exceção) nos nomes/DisplayNames dos testes, seguido de uma seção final de
/// "Cenários de Domínio" alinhados ao README/SeedData do desafio.
/// </summary>
public class PercentageTests
{
    // =========================================================================
    // Construção
    // =========================================================================

    [Theory(DisplayName = "AlternativePath: valores positivos, negativos e zero são todos aceitos (sem validação de sinal)")]
    [InlineData(0, 0)]
    [InlineData(37.5, 37.5)]
    [InlineData(-5.25, -5.25)]
    [InlineData(-100, -100)]
    [InlineData(9999.9999, 9999.9999)]
    public void Constructor_WithAnyValue_AlternativePath_SetsValueCorrectly(decimal input, decimal expected)
    {
        var percentage = new Percentage(input);

        Assert.Equal(expected, percentage.Value);
    }
    // Não existe "ExceptionalPath" para o construtor: ao contrário de Money,
    // Percentage não impõe não-negatividade — retornos negativos (perdas) e
    // desvios negativos (setor sub-alocado) são estados válidos do domínio.

    [Theory(DisplayName = "AlternativePath: valor é arredondado para 4 casas decimais usando banker's rounding (ToEven)")]
    [InlineData(12.34565, 12.3456)]  // ponto médio; dígito anterior (6) já é par -> mantém
    [InlineData(12.34575, 12.3458)]  // ponto médio; dígito anterior (7) é ímpar -> sobe para o par (8)
    [InlineData(12.00005, 12.0000)]  // ponto médio; dígito anterior (0) já é par -> mantém
    [InlineData(12.00015, 12.0002)]  // ponto médio; dígito anterior (1) é ímpar -> sobe para o par (2)
    [InlineData(12.345678, 12.3457)] // não é ponto médio, arredondamento comum para cima
    public void Constructor_RoundsToFourDecimalPlaces_AlternativePath_UsesBankersRounding(decimal input, decimal expected)
    {
        var percentage = new Percentage(input);

        Assert.Equal(expected, percentage.Value);
    }

    // =========================================================================
    // Zero
    // =========================================================================

    [Fact(DisplayName = "AlternativePath: Percentage.Zero representa 0%")]
    public void Zero_AlternativePath_ReturnsPercentageInstanceWithValueZero()
    {
        var zero = Percentage.Zero;

        Assert.Equal(0m, zero.Value);
    }

    // =========================================================================
    // AsFraction / FromFraction
    // =========================================================================

    [Theory(DisplayName = "AlternativePath: AsFraction converte o percentual em fração decimal (÷100)")]
    [InlineData(100, 1)]
    [InlineData(50, 0.5)]
    [InlineData(25, 0.25)]
    [InlineData(-10, -0.1)]
    [InlineData(0, 0)]
    public void AsFraction_AlternativePath_DividesValueByOneHundred(decimal percentageValue, decimal expectedFraction)
    {
        var percentage = new Percentage(percentageValue);

        Assert.Equal(expectedFraction, percentage.AsFraction());
    }
    // Não há "ExceptionalPath" para AsFraction: o divisor é a constante 100m,
    // nunca um valor fornecido externamente, então DivideByZeroException nunca é possível aqui.

    [Theory(DisplayName = "AlternativePath: FromFraction converte uma fração decimal em percentual (×100)")]
    [InlineData(1, 100)]
    [InlineData(0.5, 50)]
    [InlineData(0.25, 25)]
    [InlineData(-0.1, -10)]
    [InlineData(0, 0)]
    [InlineData(0.1234567, 12.3457)] // fração com mais de 4 casas após ×100 -> arredonda
    public void FromFraction_AlternativePath_MultipliesFractionByOneHundred(decimal fraction, decimal expectedPercentage)
    {
        var percentage = Percentage.FromFraction(fraction);

        Assert.Equal(expectedPercentage, percentage.Value);
    }

    [Fact(DisplayName = "AlternativePath: FromFraction(AsFraction(p)) é a identidade (round-trip)")]
    public void FromFraction_AsFraction_AlternativePath_RoundTripsToSameValue()
    {
        var original = new Percentage(18.33m);

        var roundTripped = Percentage.FromFraction(original.AsFraction());

        Assert.Equal(original.Value, roundTripped.Value);
    }

    // =========================================================================
    // Operador +
    // =========================================================================

    [Theory(DisplayName = "AlternativePath: soma de dois percentuais, incluindo operandos negativos")]
    [InlineData(10, 5, 15)]
    [InlineData(10, -5, 5)]
    [InlineData(-10, -5, -15)]
    [InlineData(0, 0, 0)]
    [InlineData(20, 25, 45)]
    public void Addition_AlternativePath_ReturnsSum(decimal left, decimal right, decimal expected)
    {
        var a = new Percentage(left);
        var b = new Percentage(right);

        var result = a + b;

        Assert.Equal(expected, result.Value);
    }
    // Sem "ExceptionalPath": Percentage não tem invariante de sinal a violar.

    // =========================================================================
    // Operador -
    // =========================================================================

    [Theory(DisplayName = "AlternativePath: subtração retorna diferença mesmo quando o resultado é negativo")]
    [InlineData(10, 15, -5)]     // ao contrário de Money, isto NÃO lança exceção
    [InlineData(5, 5, 0)]
    [InlineData(-5, -10, 5)]
    [InlineData(20, 5, 15)]
    [InlineData(25.5, 20.0, 5.5)]  // README: PETR4 está 5.5pp acima do alvo -> SELL
    [InlineData(8.5, 12.0, -3.5)]  // README: ITUB4 está 3.5pp abaixo do alvo -> BUY (desvio negativo)
    public void Subtraction_AlternativePath_ReturnsDifferenceEvenWhenNegative(decimal left, decimal right, decimal expected)
    {
        var a = new Percentage(left);
        var b = new Percentage(right);

        var result = a - b;

        Assert.Equal(expected, result.Value);
    }
    // Sem "ExceptionalPath": este é justamente o comportamento que diferencia
    // Percentage de Money — um desvio de alocação ou um retorno negativo é um
    // resultado de negócio válido, não um erro.

    // =========================================================================
    // Operador *
    // =========================================================================

    [Theory(DisplayName = "AlternativePath: multiplicação por fator positivo, negativo ou zero")]
    [InlineData(10, 2, 20)]
    [InlineData(10, -1, -10)]   // ao contrário de Money, fator negativo é permitido
    [InlineData(0, 100, 0)]
    [InlineData(12.5, 0.5, 6.25)]
    [InlineData(-8, -1, 8)]     // negativo × negativo = positivo
    public void Multiplication_AlternativePath_ReturnsProduct(decimal value, decimal factor, decimal expected)
    {
        var percentage = new Percentage(value);

        var result = percentage * factor;

        Assert.Equal(expected, result.Value);
    }
    // Sem "ExceptionalPath": não há invariante de sinal para violar aqui.
    // Nota: Percentage não define operador de divisão — apenas AsFraction()/FromFraction(),
    // então não há cenário de "divisão por zero" a testar para este VO.

    // =========================================================================
    // ToString
    // =========================================================================

    [Theory(DisplayName = "AlternativePath: ToString formata com 2 casas decimais + '%', respeitando a cultura corrente")]
    [InlineData("en-US", "1,234.50%")]
    [InlineData("pt-BR", "1.234,50%")]
    public void ToString_AlternativePath_FormatsAccordingToCurrentCulture(string cultureName, string expected)
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
        try
        {
            var percentage = new Percentage(1234.5m);

            Assert.Equal(expected, percentage.ToString());
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact(DisplayName = "AlternativePath: ToString exibe apenas 2 casas mesmo com 4 casas armazenadas internamente")]
    public void ToString_AlternativePath_DisplaysFewerDecimalsThanStoredInternally()
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
        try
        {
            // Value é armazenado com 4 casas (12.3456), mas ToString usa "N2".
            // Este caso NÃO é um empate (12.3456 está mais perto de 12.35 do que
            // de 12.34), então o resultado é o mesmo em qualquer convenção de
            // arredondamento — apenas evidencia a perda de precisão na exibição.
            var percentage = new Percentage(12.3456m);

            Assert.Equal(12.3456m, percentage.Value);
            Assert.Equal("12.35%", percentage.ToString());
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact(DisplayName = "ATENÇÃO - Possível divergência de arredondamento: ToString(\"N2\") pode não seguir o mesmo banker's rounding (ToEven) usado no restante do domínio")]
    public void ToString_PossibleRoundingDivergence_FromDomainStandardBankersRounding()
    {
        // Value = 12.3450 (exatamente 4 casas, sem arredondamento na construção).
        // Ao formatar com "N2", o valor cai EXATAMENTE no ponto médio entre
        // 12.34 e 12.35. A documentação da Microsoft descreve que os format
        // strings numéricos padrão ("N", "F" etc.) aplicados a Decimal arredondam
        // usando a convenção "round half away from zero", diferente da convenção
        // ToEven usada por decimal.Round (a mesma usada no construtor de Money e
        // de Percentage). Ou seja, ToString poderia devolver "12.35%" mesmo que
        // um arredondamento ToEven consistente com o resto do domínio desse "12.34%".
        //
        // ATENÇÃO: não tenho um runtime .NET disponível neste ambiente para
        // confirmar isso na prática — rode `dotnet test` localmente para validar
        // esta asserção. Se ela falhar, o valor real da sua versão do .NET é
        // o que importa, não o comentário acima.
        var percentage = new Percentage(12.345m);

        var originalCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
        try
        {
            Assert.Equal("12.35%", percentage.ToString());
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    // =========================================================================
    // Igualdade (record struct → igualdade estrutural por valor)
    // =========================================================================

    [Theory(DisplayName = "AlternativePath: igualdade compara pelo valor já arredondado para 4 casas")]
    [InlineData(10, 10, true)]
    [InlineData(10, 20, false)]
    [InlineData(-5, -5, true)]
    [InlineData(10.00001, 10.00004, true)]  // ambos arredondam para 10.0000
    [InlineData(10.00001, 10.00006, false)] // 10.0000 vs 10.0001
    public void Equality_ComparesByRoundedValue_AlternativePath(decimal left, decimal right, bool expectedEqual)
    {
        var a = new Percentage(left);
        var b = new Percentage(right);

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

    [Fact(DisplayName = "AlternativePath: soma dos targetAllocation do Portfólio Conservador totaliza 100%")]
    public void DomainScenario_ConservativePortfolioTargetAllocations_AlternativePath_SumToOneHundredPercent()
    {
        // Portfólio Conservador (user-001): targetAllocation de cada posição, como frações
        var targetAllocationFractions = new[] { 0.20m, 0.25m, 0.20m, 0.25m, 0.10m };

        var total = targetAllocationFractions
            .Select(Percentage.FromFraction)
            .Aggregate(Percentage.Zero, (sum, p) => sum + p);

        Assert.Equal(100.00m, total.Value);
    }

    [Fact(DisplayName = "AlternativePath: desvio de alocação do exemplo do README (PETR4 acima do alvo, ITUB4 abaixo) tem o sinal correto")]
    public void DomainScenario_RebalancingDeviation_AlternativePath_SignIndicatesSellOrBuy()
    {
        // Exemplo do README (Rebalancing Suggestions):
        //   PETR4: currentWeight 25.5%, targetWeight 20.0% -> SELL (desvio positivo)
        //   ITUB4: currentWeight 8.5%,  targetWeight 12.0%  -> BUY  (desvio negativo)
        var petr4Deviation = new Percentage(25.5m) - new Percentage(20.0m);
        var itub4Deviation = new Percentage(8.5m) - new Percentage(12.0m);

        Assert.Equal(5.5m, petr4Deviation.Value);
        Assert.True(petr4Deviation.Value > 0); // sinal positivo => reduzir posição (SELL)

        Assert.Equal(-3.5m, itub4Deviation.Value);
        Assert.True(itub4Deviation.Value < 0); // sinal negativo => aumentar posição (BUY)
    }

    [Fact(DisplayName = "IMPORTANTE - Armadilha de design: TotalReturn negativo não pode ser calculado com `Money - Money`, precisa passar pelos decimais crus antes de virar Percentage")]
    public void DomainScenario_NegativeTotalReturn_CannotBeComputedDirectlyFromMoneySubtraction()
    {
        // Um portfólio pode ter desempenho negativo (currentValue < investedAmount).
        // Money não permite valores negativos, então currentValue - investedAmount
        // usando o operador de Money LANÇA DomainException quando há perda.
        var investedAmount = new Money(10000.00m);
        var currentValue = new Money(8500.00m); // perda de 15%

        Action subtractAsMoney = () => { var _ = currentValue - investedAmount; };
        Assert.Throws<DomainException>(subtractAsMoney);

        // Para obter um TotalReturn (Percentage) que pode ser negativo, a subtração
        // deve ser feita com os decimais "crus" (.Value), e só o resultado final
        // (a fração/percentual) é que vira um Value Object:
        var totalReturnFraction = (currentValue.Value - investedAmount.Value) / investedAmount.Value;
        var totalReturn = Percentage.FromFraction(totalReturnFraction);

        Assert.Equal(-15.00m, Math.Round(totalReturn.Value, 2));
    }
}