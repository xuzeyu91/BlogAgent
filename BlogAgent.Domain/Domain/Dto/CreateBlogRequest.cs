namespace BlogAgent.Domain.Domain.Dto
{
    /// <summary>
    /// 创建博客请求DTO
    /// </summary>
    public class CreateBlogRequest
    {
        /// <summary>
        /// 工作流模式: auto-全自动, manual-分步模式
        /// </summary>
        public string WorkflowMode { get; set; } = "auto";

        /// <summary>
        /// 博客主题
        /// </summary>
        public string Topic { get; set; } = string.Empty;

        /// <summary>
        /// 参考资料内容
        /// </summary>
        public string? ReferenceContent { get; set; }

        /// <summary>
        /// 参考链接
        /// </summary>
        public string? ReferenceUrls { get; set; }

        /// <summary>
        /// 目标字数
        /// </summary>
        public int? TargetWordCount { get; set; }

        /// <summary>
        /// 写作风格
        /// </summary>
        public string? Style { get; set; }

        /// <summary>
        /// 目标读者
        /// </summary>
        public string? TargetAudience { get; set; }
    }
}

