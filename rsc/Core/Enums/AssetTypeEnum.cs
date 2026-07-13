using System.Text.Json.Serialization;

namespace SharedKernel.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<AssetTypeEnum>))]
public enum AssetTypeEnum
{
    Stock,
    Etf,
    Fund,
    FixedIncome
}
