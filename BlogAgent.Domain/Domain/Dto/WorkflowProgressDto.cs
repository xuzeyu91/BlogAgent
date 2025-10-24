namespace BlogAgent.Domain.Domain.Dto
{
    /// <summary>
    /// 工作流进度信息 DTO
    /// </summary>
    public class WorkflowProgressDto
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public int TaskId { get; set; }

        /// <summary>
        /// 当前步骤 (0=研究, 1=撰写, 2=审查, 3=完成)
        /// </summary>
        public int CurrentStep { get; set; }

        /// <summary>
        /// 步骤名称
        /// </summary>
        public string StepName { get; set; } = string.Empty;

        /// <summary>
        /// 执行状态
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// 执行消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 当前阶段输出内容(部分)
        /// </summary>
        public string? CurrentOutput { get; set; }

        /// <summary>
        /// 是否完成
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 研究结果(步骤1完成后)
        /// </summary>
        public ResearchResultDto? ResearchResult { get; set; }

        /// <summary>
        /// 草稿内容(步骤2完成后)
        /// </summary>
        public DraftContentDto? DraftContent { get; set; }

        /// <summary>
        /// 审查结果(步骤3完成后)
        /// </summary>
        public ReviewResultDto? ReviewResult { get; set; }
    }
}
