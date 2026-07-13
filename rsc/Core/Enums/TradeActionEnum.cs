using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharedKernel.Enums;

[JsonConverter(typeof(UpperCaseTradeActionJsonConverter))]
public enum TradeActionEnum
{
    Buy,
    Sell
}

public sealed class UpperCaseTradeActionJsonConverter()
    : JsonStringEnumConverter<TradeActionEnum>(JsonNamingPolicy.SnakeCaseUpper);
