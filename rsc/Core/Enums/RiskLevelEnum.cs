using System.Text.Json.Serialization;

namespace SharedKernel.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<RiskLevelEnum>))]
public enum RiskLevelEnum
{
    Low,
    Medium,
    High
}
