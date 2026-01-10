using BlogAgent.Domain.Services.Workflows.Messages;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace BlogAgent.Domain.Services.Workflows.Executors
{
    /// <summary>
    /// 失败处理执行器 - 处理达到最大重写次数的情况
    /// </summary>
    public class FailureExecutor : Executor<ReviewResultOutput, string>
    {
        private readonly BlogService _blogService;
        private readonly ILogger<FailureExecutor> _logger;

        public FailureExecutor(
            BlogService blogService,
            ILogger<FailureExecutor> logger)
            : base("FailureExecutor")
        {
            _blogService = blogService;
            _logger = logger;
        }

        public override async ValueTask<string> HandleAsync(
            ReviewResultOutput reviewResult,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            var taskId = reviewResult.TaskId;
            _logger.LogWarning($"[FailureExecutor] 处理发布失败, TaskId: {taskId}, 最终评分: {reviewResult.OverallScore}");

            // 更新任务状态为需要人工干预
            await _blogService.UpdateTaskStatusAsync(
                taskId,
                Domain.Enum.AgentTaskStatus.Failed,
                "max_rewrite_reached");

            var failureMessage = $"博客未能达到发布标准，需要人工干预。\n" +
                $"最终评分: {reviewResult.OverallScore}/100\n" +
                $"主要问题:\n" +
                $"{string.Join("\n", reviewResult.Issues.Take(5).Select(i => $"- {i.Category}: {i.Description}"))}\n" +
                $"\n改进建议:\n" +
                $"{string.Join("\n", reviewResult.Suggestions.Take(3).Select((s, i) => $"{i + 1}. {s}"))}";

            _logger.LogWarning($"[FailureExecutor] 任务已标记为需要人工干预, TaskId: {taskId}");

            return failureMessage;
        }
    }
}
