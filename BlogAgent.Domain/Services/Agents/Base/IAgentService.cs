using BlogAgent.Domain.Domain.Enum;

namespace BlogAgent.Domain.Services.Agents.Base
{
    /// <summary>
    /// Agent服务接口
    /// </summary>
    public interface IAgentService
    {
        /// <summary>
        /// Agent名称
        /// </summary>
        string AgentName { get; }

        /// <summary>
        /// Agent类型
        /// </summary>
        AgentType AgentType { get; }

        /// <summary>
        /// 执行Agent任务
        /// </summary>
        /// <param name="input">输入内容</param>
        /// <param name="taskId">任务ID(用于记录执行日志)</param>
        /// <returns>输出内容</returns>
        Task<string> ExecuteAsync(string input, int taskId);
    }
}

