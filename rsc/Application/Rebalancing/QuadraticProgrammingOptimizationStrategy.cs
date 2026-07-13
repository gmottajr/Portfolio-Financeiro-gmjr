namespace Application.Rebalancing;

/// <summary>
/// Solves the convex quadratic objective
/// ||w-target||² + turnoverPenalty × ||w-current||² on the probability simplex.
/// </summary>
public sealed class QuadraticProgrammingOptimizationStrategy : IRebalancingOptimizationStrategy
{
    private const decimal TurnoverPenalty = 0.15m;
    private const int Iterations = 250;

    public RebalancingOptimizationMode Key => RebalancingOptimizationMode.QuadraticProgramming;
    public string Title => "Programação quadrática";
    public string Description =>
        "Minimiza simultaneamente o erro quadrático de alocação e o turnover, com projeção determinística no simplex.";

    public RebalancingStrategyPlan Optimize(RebalancingProblem problem)
    {
        var fallback = RebalancingPlanEngine.Empty(Key.ToString(), Title, Description, problem);
        if (problem.TotalValue <= 0m || problem.TargetTotal <= 0m) return fallback;

        var positions = problem.Positions.ToList();
        var current = positions.Select(position => position.CurrentValue / problem.TotalValue).ToArray();
        var target = positions.Select(position => position.TargetWeight / problem.TargetTotal).ToArray();
        var weights = current.ToArray();
        var step = 0.20m / (1m + TurnoverPenalty);

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            var next = new decimal[weights.Length];
            for (var index = 0; index < weights.Length; index++)
            {
                var gradient = 2m * (weights[index] - target[index]) +
                               2m * TurnoverPenalty * (weights[index] - current[index]);
                next[index] = weights[index] - step * gradient;
            }
            weights = ProjectToSimplex(next);
        }

        var desiredWeights = positions
            .Select((position, index) => new { position.Symbol, Weight = weights[index] * 100m })
            .ToDictionary(item => item.Symbol, item => item.Weight);
        var selected = RebalancingPlanEngine.EligiblePositions(problem)
            .Select(position => position.Symbol)
            .ToHashSet();

        return RebalancingPlanEngine.BuildFromDesiredWeights(
            Key.ToString(), Title, Description, problem, desiredWeights, selected);
    }

    private static decimal[] ProjectToSimplex(IReadOnlyList<decimal> values)
    {
        var sorted = values.OrderByDescending(value => value).ToArray();
        decimal cumulative = 0m;
        var rho = 0;
        for (var index = 0; index < sorted.Length; index++)
        {
            cumulative += sorted[index];
            var threshold = (cumulative - 1m) / (index + 1);
            if (sorted[index] - threshold > 0m) rho = index + 1;
        }

        if (rho == 0) return Enumerable.Repeat(1m / values.Count, values.Count).ToArray();
        var theta = (sorted.Take(rho).Sum() - 1m) / rho;
        return values.Select(value => decimal.Max(0m, value - theta)).ToArray();
    }
}

