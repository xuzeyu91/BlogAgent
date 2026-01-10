namespace BlogAgent.Domain.Common.Constants
{
    /// <summary>
    /// 博客工作流共享状态常量
    /// </summary>
    internal static class BlogStateConstants
    {
        /// <summary>
        /// 博客状态作用域名称
        /// </summary>
        public const string BlogStateScope = "BlogState";

        /// <summary>
        /// 研究结果键
        /// </summary>
        public const string ResearchResultKey = "ResearchResult";

        /// <summary>
        /// 草稿内容键
        /// </summary>
        public const string DraftContentKey = "DraftContent";

        /// <summary>
        /// 审查结果键
        /// </summary>
        public const string ReviewResultKey = "ReviewResult";

        /// <summary>
        /// 重写次数键
        /// </summary>
        public const string RewriteCountKey = "RewriteCount";

        /// <summary>
        /// 任务信息键
        /// </summary>
        public const string TaskInfoKey = "TaskInfo";

        /// <summary>
        /// 最大重写次数
        /// </summary>
        public const int MaxRewriteCount = 3;
    }
}
