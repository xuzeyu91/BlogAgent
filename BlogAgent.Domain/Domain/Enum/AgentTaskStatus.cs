namespace BlogAgent.Domain.Domain.Enum
{
    /// <summary>
    /// 博客任务状态枚举
    /// </summary>
    public enum AgentTaskStatus
    {
        /// <summary>
        /// 已创建
        /// </summary>
        Created = 0,

        /// <summary>
        /// 资料收集中
        /// </summary>
        Researching = 1,

        /// <summary>
        /// 资料收集完成
        /// </summary>
        ResearchCompleted = 2,

        /// <summary>
        /// 撰写中
        /// </summary>
        Writing = 3,

        /// <summary>
        /// 撰写完成
        /// </summary>
        WritingCompleted = 4,

        /// <summary>
        /// 审查中
        /// </summary>
        Reviewing = 5,

        /// <summary>
        /// 审查完成
        /// </summary>
        ReviewCompleted = 6,

        /// <summary>
        /// 已发布
        /// </summary>
        Published = 7,

        /// <summary>
        /// 失败
        /// </summary>
        Failed = 99
    }
}

