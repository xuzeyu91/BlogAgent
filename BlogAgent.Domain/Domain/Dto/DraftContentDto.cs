namespace BlogAgent.Domain.Domain.Dto
{
    /// <summary>
    /// 博客初稿DTO
    /// </summary>
    public class DraftContentDto
    {
        /// <summary>
        /// 博客标题
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 博客内容(Markdown格式)
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 字数统计
        /// </summary>
        public int WordCount { get; set; }

        /// <summary>
        /// 生成时间
        /// </summary>
        public DateTime GeneratedAt { get; set; }
    }
}

