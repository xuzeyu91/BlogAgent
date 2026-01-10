namespace BlogAgent.Domain.Services.Workflows.Messages
{
    /// <summary>
    /// 研究结果输出消息
    /// </summary>
    public class ResearchResultOutput
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public int TaskId { get; set; }

        /// <summary>
        /// 主题分析
        /// </summary>
        public string TopicAnalysis { get; set; } = string.Empty;

        /// <summary>
        /// 核心要点列表
        /// </summary>
        public List<KeyPoint> KeyPoints { get; set; } = new();

        /// <summary>
        /// 技术细节列表
        /// </summary>
        public List<TechnicalDetail> TechnicalDetails { get; set; } = new();

        /// <summary>
        /// 代码示例列表
        /// </summary>
        public List<CodeExample> CodeExamples { get; set; } = new();

        /// <summary>
        /// 参考来源列表
        /// </summary>
        public List<string> References { get; set; } = new();

        /// <summary>
        /// Markdown 格式的摘要
        /// </summary>
        public string SummaryMarkdown { get; set; } = string.Empty;

        /// <summary>
        /// 关键点
        /// </summary>
        public class KeyPoint
        {
            public int Importance { get; set; }
            public string Content { get; set; } = string.Empty;
        }

        /// <summary>
        /// 技术细节
        /// </summary>
        public class TechnicalDetail
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        /// <summary>
        /// 代码示例
        /// </summary>
        public class CodeExample
        {
            public string Language { get; set; } = string.Empty;
            public string Code { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }
    }
}
