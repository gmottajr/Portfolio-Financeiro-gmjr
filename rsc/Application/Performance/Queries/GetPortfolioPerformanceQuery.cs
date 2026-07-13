using MediatR;

namespace Application.Performance.Queries;

public sealed record GetPortfolioPerformanceQuery(int PortfolioId) : IRequest<PortfolioPerformanceResponse?>;
