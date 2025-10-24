using SqlSugar;

namespace BlogAgent.Domain.Domain.Model
{
    /// <summary>
    /// 审查结果实体
    /// </summary>
    [SugarTable("review_result")]
    public class ReviewResult
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
    public int TaskId { get; set; }

    /// <summary>
    /// 关联的内容ID
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public int ContentId { get; set; }        /// <summary>
        /// 综合评分(0-100)
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int OverallScore { get; set; }

        /// <summary>
        /// 准确性评分(0-40)
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int AccuracyScore { get; set; }

        /// <summary>
        /// 准确性问题列表(JSON格式)
        /// </summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? AccuracyIssues { get; set; }

        /// <summary>
        /// 逻辑性评分(0-30)
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int LogicScore { get; set; }

        /// <summary>
        /// 逻辑性问题列表(JSON格式)
        /// </summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? LogicIssues { get; set; }

        /// <summary>
        /// 原创性评分(0-20)
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int OriginalityScore { get; set; }

        /// <summary>
        /// 原创性问题列表(JSON格式)
        /// </summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? OriginalityIssues { get; set; }

        /// <summary>
        /// 规范性评分(0-10)
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int FormattingScore { get; set; }

        /// <summary>
        /// 规范性问题列表(JSON格式)
        /// </summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? FormattingIssues { get; set; }

        /// <summary>
        /// 审查建议(通过/需修改/不通过)
        /// </summary>
        [SugarColumn(Length = 50, IsNullable = false)]
        public string Recommendation { get; set; } = string.Empty;

        /// <summary>
        /// 审查总结
        /// </summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Summary { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}

