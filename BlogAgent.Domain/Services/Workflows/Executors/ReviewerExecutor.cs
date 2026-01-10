using BlogAgent.Domain.Common.Constants;
using BlogAgent.Domain.Domain.Dto;
using BlogAgent.Domain.Services.Agents;
using BlogAgent.Domain.Services.Workflows.Messages;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace BlogAgent.Domain.Services.Workflows.Executors
{
    /// <summary>
    /// 审查员执行器 - 负责质量审查
    /// </summary>
    public class ReviewerExecutor : Executor<DraftContentOutput, ReviewResultOutput>
    {
        private readonly ReviewerAgent _agent;
        private readonly BlogService _blogService;
        private readonly ILogger<ReviewerExecutor> _logger;

        public ReviewerExecutor(
            ReviewerAgent agent,
            BlogService blogService,
            ILogger<ReviewerExecutor> _logger)
            : base("ReviewerExecutor")
        {
            _agent = agent;
            _blogService = blogService;
            _logger = _logger;
        }

        public override async ValueTask<ReviewResultOutput> HandleAsync(
            DraftContentOutput draftContent,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            var taskId = draftContent.TaskId;
            _logger.LogInformation($"[ReviewerExecutor] 开始质量审查, TaskId: {taskId}");

            // 更新任务状态
            await _blogService.UpdateTaskStatusAsync(
                taskId,
                Domain.Enum.AgentTaskStatus.Reviewing,
                "review_executor_start");

            try
            {
                // 调用 ReviewerAgent 进行质量审查
                var reviewResult = await _agent.ReviewAsync(
                    draftContent.Title,
                    draftContent.Content,
                    taskId);

                // 构建输出 - 使用正确的 ReviewResultDto 结构
                var output = new ReviewResultOutput
                {
                    TaskId = taskId,
                    OverallScore = reviewResult.OverallScore,
                    AccuracyScore = reviewResult.Accuracy?.Score ?? 0,
                    LogicScore = reviewResult.Logic?.Score ?? 0,
                    OriginalityScore = reviewResult.Originality?.Score ?? 0,
                    FormatScore = reviewResult.Formatting?.Score ?? 0,
                    Issues = reviewResult.Accuracy?.Issues?
                        .Concat(reviewResult.Logic?.Issues ?? new())
                        .Concat(reviewResult.Originality?.Issues ?? new())
                        .Concat(reviewResult.Formatting?.Issues ?? new())
                        .Select(issue => new ReviewResultOutput.Issue
                        {
                            Category = "问题",
                            Description = issue,
                            Severity = 2
                        }).ToList() ?? new(),
                    Suggestions = new List<string>(),
                    Recommendation = reviewResult.Recommendation,
                    DetailedFeedback = reviewResult.Summary
                };

                // 获取内容ID用于保存审查结果
                var content = await _blogService.GetContentAsync(taskId);
                if (content != null)
                {
                    // 保存到数据库
                    await _blogService.SaveReviewResultAsync(taskId, content.Id, reviewResult);
                }

                await _blogService.UpdateTaskStatusAsync(
                    taskId,
                    Domain.Enum.AgentTaskStatus.ReviewCompleted,
                    "review_executor_completed");

                // 存储到 Shared State
                await context.QueueStateUpdateAsync(
                    BlogStateConstants.ReviewResultKey,
                    output,
                    BlogStateConstants.BlogStateScope,
                    cancellationToken);

                _logger.LogInformation(
                    $"[ReviewerExecutor] 质量审查完成, TaskId: {taskId}, 评分: {output.OverallScore}, 是否通过: {output.IsPassed}");

                return output;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[ReviewerExecutor] 质量审查失败, TaskId: {taskId}");
                await _blogService.UpdateTaskStatusAsync(
                    taskId,
                    Domain.Enum.AgentTaskStatus.Failed,
                    "review_executor_failed");
                throw;
            }
        }
    }
}
