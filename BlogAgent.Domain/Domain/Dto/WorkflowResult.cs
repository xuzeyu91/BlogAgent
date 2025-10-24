namespace BlogAgent.Domain.Domain.Dto
{
    /// <summary>
    /// 工作流执行结果DTO
    /// </summary>
    public class WorkflowResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 当前阶段
        /// </summary>
        public string Stage { get; set; } = string.Empty;

        /// <summary>
        /// 输出数据
        /// </summary>
        public object? Output { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 错误详情
        /// </summary>
        public string? ErrorDetail { get; set; }
    }
}

