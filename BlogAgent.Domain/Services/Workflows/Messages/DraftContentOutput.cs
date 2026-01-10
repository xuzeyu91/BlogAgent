namespace BlogAgent.Domain.Services.Workflows.Messages
{
    /// <summary>
    /// 草稿内容输出消息
    /// </summary>
    public class DraftContentOutput
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public int TaskId { get; set; }

        /// <summary>
        /// 博客标题
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 博客内容（Markdown）
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 字数统计
        /// </summary>
        public int WordCount { get; set; }

        /// <summary>
        /// 生成时间
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }
}
