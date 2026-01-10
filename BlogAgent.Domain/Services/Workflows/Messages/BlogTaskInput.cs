using BlogAgent.Domain.Domain.Model;

namespace BlogAgent.Domain.Services.Workflows.Messages
{
    /// <summary>
    /// 博客生成任务输入消息
    /// </summary>
    public class BlogTaskInput
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public int TaskId { get; set; }

        /// <summary>
        /// 博客主题
        /// </summary>
        public string Topic { get; set; } = string.Empty;

        /// <summary>
        /// 参考资料
        /// </summary>
        public string ReferenceContent { get; set; } = string.Empty;

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

        /// <summary>
        /// 任务完整信息（可选，用于获取更多上下文）
        /// </summary>
        public BlogTask? TaskInfo { get; set; }
    }
}
