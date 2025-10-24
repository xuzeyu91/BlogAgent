using System.Text.Json.Serialization;

namespace BlogAgent.Domain.Domain.Dto
{
    /// <summary>
    /// 审查结果DTO
    /// </summary>
    public class ReviewResultDto
    {
        [JsonPropertyName("overallScore")]
        public int OverallScore { get; set; }

        [JsonPropertyName("accuracy")]
        public DimensionScore Accuracy { get; set; } = new();

        [JsonPropertyName("logic")]
        public DimensionScore Logic { get; set; } = new();

        [JsonPropertyName("originality")]
        public DimensionScore Originality { get; set; } = new();

        [JsonPropertyName("formatting")]
        public DimensionScore Formatting { get; set; } = new();

        [JsonPropertyName("recommendation")]
        public string Recommendation { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>
    /// 维度评分
    /// </summary>
    public class DimensionScore
    {
        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("issues")]
        public List<string> Issues { get; set; } = new();
    }
}

