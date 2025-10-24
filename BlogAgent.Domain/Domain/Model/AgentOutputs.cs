using System.Text.Json.Serialization;

namespace BlogAgent.Domain.Domain.Model
{
    /// <summary>
    /// 资料收集结果的结构化输出
    /// </summary>
    public class ResearchOutput
    {
        [JsonPropertyName("topic_analysis")]
        public string TopicAnalysis { get; set; } = string.Empty;

        [JsonPropertyName("key_points")]
        public List<KeyPoint> KeyPoints { get; set; } = new();

        [JsonPropertyName("technical_details")]
        public List<TechnicalDetail> TechnicalDetails { get; set; } = new();

        [JsonPropertyName("code_examples")]
        public List<CodeExample> CodeExamples { get; set; } = new();

        [JsonPropertyName("references")]
        public List<string> References { get; set; } = new();
    }

    public class KeyPoint
    {
        [JsonPropertyName("importance")]
        public int Importance { get; set; } // 1-3, 3最高

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    public class TechnicalDetail
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public class CodeExample
    {
        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// 博客初稿的结构化输出
    /// </summary>
    public class DraftOutput
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("introduction")]
        public string Introduction { get; set; } = string.Empty;

        [JsonPropertyName("sections")]
        public List<ContentSection> Sections { get; set; } = new();

        [JsonPropertyName("conclusion")]
        public string Conclusion { get; set; } = string.Empty;

        [JsonPropertyName("word_count")]
        public int WordCount { get; set; }
    }

    public class ContentSection
    {
        [JsonPropertyName("heading")]
        public string Heading { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("subsections")]
        public List<ContentSection>? Subsections { get; set; }
    }

    /// <summary>
    /// 审查结果的结构化输出
    /// </summary>
    public class ReviewOutput
    {
        [JsonPropertyName("overall_score")]
        public int OverallScore { get; set; }

        [JsonPropertyName("accuracy")]
        public ScoreDetail Accuracy { get; set; } = new();

        [JsonPropertyName("logic")]
        public ScoreDetail Logic { get; set; } = new();

        [JsonPropertyName("originality")]
        public ScoreDetail Originality { get; set; } = new();

        [JsonPropertyName("formatting")]
        public ScoreDetail Formatting { get; set; } = new();

        [JsonPropertyName("recommendation")]
        public string Recommendation { get; set; } = string.Empty; // "通过", "需修改", "不通过"

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;
    }

    public class ScoreDetail
    {
        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("issues")]
        public List<string> Issues { get; set; } = new();
    }
}
