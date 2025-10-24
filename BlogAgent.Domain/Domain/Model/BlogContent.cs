using SqlSugar;

namespace BlogAgent.Domain.Domain.Model
{
    /// <summary>
    /// 博客内容实体
    /// </summary>
    [SugarTable("blog_content")]
    public class BlogContent
    {
    /// <summary>
    /// 主键ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 关联的任务ID
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public int TaskId { get; set; }        /// <summary>
        /// 博客标题
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = false)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 博客正文(Markdown格式)
        /// </summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = false)]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 摘要
        /// </summary>
        [SugarColumn(Length = 1000, IsNullable = true)]
        public string? Summary { get; set; }

        /// <summary>
        /// 标签(逗号分隔)
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Tags { get; set; }

        /// <summary>
        /// 字数统计
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int WordCount { get; set; }

        /// <summary>
        /// 资料摘要(ResearcherAgent的输出)
        /// </summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? ResearchSummary { get; set; }

        /// <summary>
        /// 是否已发布
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public bool IsPublished { get; set; } = false;

        /// <summary>
        /// 发布时间
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? PublishedAt { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 更新时间
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? UpdatedAt { get; set; }
    }
}

