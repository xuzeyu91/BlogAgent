namespace BlogAgent.Domain.Services.Workflows.Messages
{
    /// <summary>
    /// 审查结果输出消息
    /// </summary>
    public class ReviewResultOutput
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public int TaskId { get; set; }

        /// <summary>
        /// 综合评分 (0-100)
        /// </summary>
        public int OverallScore { get; set; }

        /// <summary>
        /// 准确性评分 (0-40)
        /// </summary>
        public int AccuracyScore { get; set; }

        /// <summary>
        /// 逻辑性评分 (0-30)
        /// </summary>
        public int LogicScore { get; set; }

        /// <summary>
        /// 原创性评分 (0-20)
        /// </summary>
        public int OriginalityScore { get; set; }

        /// <summary>
        /// 规范性评分 (0-10)
        /// </summary>
        public int FormatScore { get; set; }

        /// <summary>
        /// 问题列表
        /// </summary>
        public List<Issue> Issues { get; set; } = new();

        /// <summary>
        /// 改进建议
        /// </summary>
        public List<string> Suggestions { get; set; } = new();

        /// <summary>
        /// 发布建议
        /// </summary>
        public string Recommendation { get; set; } = string.Empty;

        /// <summary>
        /// 详细评价
        /// </summary>
        public string DetailedFeedback { get; set; } = string.Empty;

        /// <summary>
        /// 是否通过审查（评分 >= 80）
        /// </summary>
        public bool IsPassed => OverallScore >= 80;

        /// <summary>
        /// 问题项
        /// </summary>
        public class Issue
        {
            public string Category { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string? Suggestion { get; set; }
            public int Severity { get; set; } // 1=低, 2=中, 3=高
        }
    }
}
