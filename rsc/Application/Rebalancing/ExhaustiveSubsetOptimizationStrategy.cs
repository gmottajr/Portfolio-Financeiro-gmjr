namespace Application.Rebalancing;

public sealed class ExhaustiveSubsetOptimizationStrategy : IRebalancingOptimizationStrategy
{
    private const int MaximumCandidates = 15;

    public RebalancingOptimizationMode Key => RebalancingOptimizationMode.Exhaustive;
    public string Title => "Busca exaustiva de subconjuntos";
    public string Description =>
        "Compara todas as combinações elegíveis e escolhe o menor conjunto de trades dentro de 95% do melhor benefício líquido.";

    public RebalancingStrategyPlan Optimize(RebalancingProblem problem)
    {
        var fallback = RebalancingPlanEngine.Empty(Key.ToString(), Title, Description, problem);
        if (problem.TotalValue <= 0m || problem.TargetTotal <= 0m) return fallback;

        var candidates = RebalancingPlanEngine.EligiblePositions(problem)
            .Take(MaximumCandidates)
            .ToList();
        if (candidates.Count == 0) return fallback;

        var desiredWeights = problem.Positions.ToDictionary(
            position => position.Symbol,
            position => RebalancingPlanEngine.NormalizedTarget(position, problem.TargetTotal));
        var plans = new List<RebalancingStrategyPlan>();
        var combinations = 1 << candidates.Count;
        for (var mask = 1; mask < combinations; mask++)
        {
            var symbols = new HashSet<string>();
            for (var index = 0; index < candidates.Count; index++)
                if ((mask & (1 << index)) != 0)
                    symbols.Add(candidates[index].Symbol);

            plans.Add(RebalancingPlanEngine.BuildFromDesiredWeights(
                Key.ToString(), Title, Description, problem, desiredWeights, symbols));
        }

        return RebalancingPlanEngine.SelectNearOptimal(plans, fallback);
    }
}

