using SqlSugar;
using BlogAgent.Domain.Domain.Enum;

namespace BlogAgent.Domain.Domain.Model
{
    /// <summary>
    /// 博客任务实体
    /// </summary>
    [SugarTable("blog_task")]
    public class BlogTask
    {
    /// <summary>
    /// 主键ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }        /// <summary>
        /// 博客主题
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = false)]
        public string Topic { get; set; } = string.Empty;

        /// <summary>
        /// 参考资料内容
        /// </summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? ReferenceContent { get; set; }

        /// <summary>
        /// 参考链接(多个用换行分隔)
        /// </summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? ReferenceUrls { get; set; }

        /// <summary>
        /// 任务状态
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public Domain.Enum.AgentTaskStatus Status { get; set; } = Domain.Enum.AgentTaskStatus.Created;

        /// <summary>
        /// 当前阶段
        /// </summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? CurrentStage { get; set; }

        /// <summary>
        /// 目标字数
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public int? TargetWordCount { get; set; }

        /// <summary>
        /// 写作风格
        /// </summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string? Style { get; set; }

        /// <summary>
        /// 目标读者
        /// </summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string? TargetAudience { get; set; }

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

