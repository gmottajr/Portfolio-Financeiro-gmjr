using Google.OrTools.Sat;

namespace Application.Rebalancing;

public sealed class CpSatOptimizationStrategy : IRebalancingOptimizationStrategy
{
    private const long MoneyScale = 100L;
    private const long FeeScale = 1_000L;
    private const long FeeRateScaled = 3L;

    public RebalancingOptimizationMode Key => RebalancingOptimizationMode.CpSat;
    public string Title => "Otimização inteira CP-SAT";
    public string Description =>
        "Usa variáveis monetárias inteiras e binárias para minimizar desvio, turnover e quantidade de trades sob autofinanciamento.";

    public RebalancingStrategyPlan Optimize(RebalancingProblem problem)
    {
        var fallback = RebalancingPlanEngine.Empty(Key.ToString(), Title, Description, problem);
        if (problem.TotalValue <= 0m || problem.TargetTotal <= 0m) return fallback;

        try
        {
            var candidates = RebalancingPlanEngine.EligiblePositions(problem).ToList();
            if (candidates.Count == 0) return fallback;

            var model = new CpModel();
            var totalCents = ToScaled(problem.TotalValue);
            var minimumCents = ToScaled(problem.MinimumTradeValue);
            var buyVariables = new List<IntVar>();
            var sellVariables = new List<IntVar>();
            var amountVariables = new List<IntVar>();
            var activeVariables = new List<BoolVar>();
            var deviationVariables = new List<IntVar>();
            var variablesBySymbol = new Dictionary<string, (IntVar Amount, bool IsBuy)>();

            foreach (var position in candidates)
            {
                var currentCents = ToScaled(position.CurrentValue);
                var targetCents = ToScaled(
                    problem.TotalValue *
                    RebalancingPlanEngine.NormalizedTarget(position, problem.TargetTotal) / 100m);
                var isBuy = currentCents < targetCents;
                var maximum = isBuy ? totalCents : currentCents;
                var amount = model.NewIntVar(0L, maximum, $"amount_{position.Symbol}");
                var active = model.NewBoolVar($"active_{position.Symbol}");
                model.Add(amount <= maximum * active);
                model.Add(amount >= minimumCents * active);

                LinearExpr projected = isBuy
                    ? currentCents + amount
                    : currentCents - amount;
                var deviation = model.NewIntVar(0L, totalCents, $"deviation_{position.Symbol}");
                model.AddAbsEquality(deviation, projected - targetCents);

                amountVariables.Add(amount);
                activeVariables.Add(active);
                deviationVariables.Add(deviation);
                variablesBySymbol[position.Symbol] = (amount, isBuy);
                if (isBuy) buyVariables.Add(amount);
                else sellVariables.Add(amount);
            }

            var totalBuys = LinearExpr.Sum(buyVariables);
            var totalSales = LinearExpr.Sum(sellVariables);
            var totalTurnover = LinearExpr.Sum(amountVariables);
            model.Add(totalBuys * FeeScale + totalTurnover * FeeRateScaled <= totalSales * FeeScale);

            // Deviation dominates; turnover and binary activation break near ties.
            model.Minimize(
                LinearExpr.Sum(deviationVariables) * 100L +
                totalTurnover +
                LinearExpr.Sum(activeVariables) * 10_000L);

            var solver = new CpSolver
            {
                StringParameters = "max_time_in_seconds:1 num_search_workers:1 random_seed:1"
            };
            var status = solver.Solve(model);
            if (status is not CpSolverStatus.Optimal and not CpSolverStatus.Feasible)
                return RebalancingPlanEngine.Empty(
                    Key.ToString(), Title, Description, problem, status.ToString(),
                    "O solver não encontrou um plano viável dentro do limite configurado.");

            var signedValues = variablesBySymbol.ToDictionary(
                item => item.Key,
                item => (item.Value.IsBuy ? 1m : -1m) *
                        solver.Value(item.Value.Amount) / MoneyScale);
            return RebalancingPlanEngine.BuildFromTradeValues(
                Key.ToString(), Title, Description, problem, signedValues);
        }
        catch (Exception exception) when (
            exception is DllNotFoundException or TypeInitializationException or BadImageFormatException)
        {
            return RebalancingPlanEngine.Empty(
                Key.ToString(), Title, Description, problem, "Unavailable",
                $"CP-SAT indisponível neste runtime: {exception.GetType().Name}.");
        }
    }

    private static long ToScaled(decimal value) =>
        checked((long)decimal.Round(value * MoneyScale, 0, MidpointRounding.AwayFromZero));
}
