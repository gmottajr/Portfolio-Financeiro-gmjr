using SharedKernel.ValueObjects;

namespace Models;

/// <summary>
/// Preço de fechamento de um ativo em uma data específica.
/// Value Object simples (imutável, sem identidade própria).
/// </summary>
public sealed record PricePoint(DateTime Date, Money Price);
