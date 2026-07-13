using Application.Performance;
using Application.Performance.Queries;
using Application.Exceptions;
using Application.Risk;
using Application.Rebalancing;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Portfolio_Financeiro_WebApplication.Controllers;

[ApiController]
[Route("api/portfolios")]
public sealed class AnalyticsController(ISender sender, RiskAnalysisAppService riskAnalysis, GenerateRebalancingSuggestionsUseCase rebalancing) : ControllerBase
{
    [HttpGet("{id:int}/performance")]
    [ProducesResponseType<PortfolioPerformanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortfolioPerformanceResponse>> GetPerformance(
        int id,
        CancellationToken ct)
    {
        if (id <= 0)
        {
            return BadRequest("The portfolio id must be greater than zero.");
        }

        try
        {
            var performance = await sender.Send(new GetPortfolioPerformanceQuery(id), ct);
            return performance is null ? NotFound() : Ok(performance);
        }
        catch (PortfolioDataIncompleteException exception)
        {
            return UnprocessableEntity(CreateIncompleteDataProblem(exception));
        }
    }

    [HttpGet("{id:int}/risk-analysis")]
    [ProducesResponseType<RiskAnalysisResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RiskAnalysisResponse>> GetRiskAnalysis(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest();

        try
        {
            return (await riskAnalysis.AnalyzeAsync(id, ct)) is { } result ? Ok(result) : NotFound();
        }
        catch (PortfolioDataIncompleteException exception)
        {
            return UnprocessableEntity(CreateIncompleteDataProblem(exception));
        }
    }

    [HttpGet("{id:int}/rebalancing")]
    [ProducesResponseType<RebalancingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RebalancingResponse>> GetRebalancing(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest();

        try
        {
            return (await rebalancing.ExecuteAsync(id, ct)) is { } result ? Ok(result) : NotFound();
        }
        catch (PortfolioDataIncompleteException exception)
        {
            return UnprocessableEntity(CreateIncompleteDataProblem(exception));
        }
    }

    private static ProblemDetails CreateIncompleteDataProblem(PortfolioDataIncompleteException exception) => new()
    {
        Title = "Portfolio data is incomplete.",
        Detail = exception.Message,
        Status = StatusCodes.Status422UnprocessableEntity
    };
}
