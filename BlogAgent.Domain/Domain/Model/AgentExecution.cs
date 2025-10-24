using SqlSugar;
using BlogAgent.Domain.Domain.Enum;

namespace BlogAgent.Domain.Domain.Model
{
    /// <summary>
    /// Agent执行记录实体
    /// </summary>
    [SugarTable("agent_execution")]
    public class AgentExecution
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
        /// Agent类型
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public AgentType AgentType { get; set; }

        /// <summary>
        /// 输入内容
        /// </summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Input { get; set; }

        /// <summary>
        /// 输出内容
        /// </summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Output { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public bool Success { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        [SugarColumn(Length = 2000, IsNullable = true)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Token使用量
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public int? TokensUsed { get; set; }

        /// <summary>
        /// 执行耗时(毫秒)
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? ExecutionTimeMs { get; set; }

        /// <summary>
        /// 开始时间
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public DateTime StartTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 结束时间
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? EndTime { get; set; }
    }
}

