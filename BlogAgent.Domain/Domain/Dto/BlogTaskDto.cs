using BlogAgent.Domain.Domain.Enum;
using AgentTaskStatus = BlogAgent.Domain.Domain.Enum.AgentTaskStatus;

namespace BlogAgent.Domain.Domain.Dto
{
    /// <summary>
    /// 博客任务DTO
    /// </summary>
    public class BlogTaskDto
    {
        public int Id { get; set; }
        public string Topic { get; set; } = string.Empty;
        public AgentTaskStatus Status { get; set; }
        public string? CurrentStage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// 状态显示文本
        /// </summary>
        public string StatusText => Status switch
        {
            AgentTaskStatus.Created => "已创建",
            AgentTaskStatus.Researching => "资料收集中",
            AgentTaskStatus.ResearchCompleted => "资料收集完成",
            AgentTaskStatus.Writing => "撰写中",
            AgentTaskStatus.WritingCompleted => "撰写完成",
            AgentTaskStatus.Reviewing => "审查中",
            AgentTaskStatus.ReviewCompleted => "审查完成",
            AgentTaskStatus.Published => "已发布",
            AgentTaskStatus.Failed => "失败",
            _ => "未知状态"
        };
    }
}

