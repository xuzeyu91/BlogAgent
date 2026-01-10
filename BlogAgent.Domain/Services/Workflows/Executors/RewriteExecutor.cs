using BlogAgent.Domain.Common.Constants;
using BlogAgent.Domain.Domain.Dto;
using BlogAgent.Domain.Services.Agents;
using BlogAgent.Domain.Services.Workflows.Messages;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace BlogAgent.Domain.Services.Workflows.Executors
{
    /// <summary>
    /// 重写执行器 - 根据审查意见重写博客
    /// </summary>
    public class RewriteExecutor : Executor<ReviewResultOutput, DraftContentOutput>
    {
        private readonly WriterAgent _agent;
        private readonly BlogService _blogService;
        private readonly ILogger<RewriteExecutor> _logger;

        public RewriteExecutor(
            WriterAgent agent,
            BlogService blogService,
            ILogger<RewriteExecutor> _logger)
            : base("RewriteExecutor")
        {
            _agent = agent;
            _blogService = blogService;
            _logger = _logger;
        }

        public override async ValueTask<DraftContentOutput> HandleAsync(
            ReviewResultOutput reviewResult,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            var taskId = reviewResult.TaskId;
            _logger.LogInformation($"[RewriteExecutor] 开始重写博客, TaskId: {taskId}, 上次评分: {reviewResult.OverallScore}");

            // 获取并增加重写次数
            var rewriteCount = await context.ReadStateAsync<int>(
                BlogStateConstants.RewriteCountKey,
                BlogStateConstants.BlogStateScope,
                cancellationToken);
            rewriteCount++;

            if (rewriteCount > BlogStateConstants.MaxRewriteCount)
            {
                _logger.LogWarning($"[RewriteExecutor] 达到最大重写次数 ({BlogStateConstants.MaxRewriteCount}), TaskId: {taskId}");
                throw new InvalidOperationException($"已达到最大重写次数 ({BlogStateConstants.MaxRewriteCount})，请人工干预");
            }

            // 更新重写次数到 Shared State
            await context.QueueStateUpdateAsync(
                BlogStateConstants.RewriteCountKey,
                rewriteCount,
                BlogStateConstants.BlogStateScope,
                cancellationToken);

            // 更新任务状态
            await _blogService.UpdateTaskStatusAsync(
                taskId,
                Domain.Enum.AgentTaskStatus.Writing,
                "rewrite_executor_start");

            try
            {
                // 从 Shared State 获取研究内容和任务信息
                var researchResult = await context.ReadStateAsync<ResearchResultOutput>(
                    BlogStateConstants.ResearchResultKey,
                    BlogStateConstants.BlogStateScope,
                    cancellationToken) ?? throw new InvalidOperationException("研究结果不存在");

                var taskInfo = await context.ReadStateAsync<BlogTaskInput>(
                    BlogStateConstants.TaskInfoKey,
                    BlogStateConstants.BlogStateScope,
                    cancellationToken) ?? throw new InvalidOperationException("任务信息不存在");

                // 构建改进建议
                var improvementSuggestions = string.Join("\n",
                    reviewResult.Suggestions.Select((s, i) => $"{i + 1}. {s}"));

                // 构建重写要求
                var rewritePrompt = $@"
**原始主题:** {taskInfo.Topic}

**改进建议:**
{improvementSuggestions}

**上次评分:**
- 综合评分: {reviewResult.OverallScore}/100
- 准确性: {reviewResult.AccuracyScore}/40
- 逻辑性: {reviewResult.LogicScore}/30
- 原创性: {reviewResult.OriginalityScore}/20
- 规范性: {reviewResult.FormatScore}/10

**需要改进的问题:**
{string.Join("\n", reviewResult.Issues.Select(i => $"- [{i.Category}] {i.Description} (严重程度: {i.Severity})"))}

请根据以上审查意见，重新撰写博客，重点改进指出的问题。";

                // 构建写作要求
                var requirements = new WritingRequirements
                {
                    TargetWordCount = taskInfo.TargetWordCount,
                    Style = taskInfo.Style,
                    TargetAudience = taskInfo.TargetAudience
                };

                // 调用 WriterAgent 重写博客
                var result = await _agent.WriteAsync(
                    taskInfo.Topic,
                    rewritePrompt,
                    requirements,
                    taskId);

                // 构建输出
                var output = new DraftContentOutput
                {
                    TaskId = taskId,
                    Title = result.Title,
                    Content = result.Content,
                    WordCount = result.WordCount,
                    GeneratedAt = result.GeneratedAt
                };

                // 更新草稿内容
                await _blogService.SaveDraftContentAsync(taskId, result);
                await _blogService.UpdateTaskStatusAsync(
                    taskId,
                    Domain.Enum.AgentTaskStatus.WritingCompleted,
                    "rewrite_executor_completed");

                // 更新 Shared State 中的草稿内容
                await context.QueueStateUpdateAsync(
                    BlogStateConstants.DraftContentKey,
                    output,
                    BlogStateConstants.BlogStateScope,
                    cancellationToken);

                _logger.LogInformation(
                    $"[RewriteExecutor] 博客重写完成, TaskId: {taskId}, 重写次数: {rewriteCount}, 新标题: {output.Title}");

                return output;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[RewriteExecutor] 博客重写失败, TaskId: {taskId}");
                await _blogService.UpdateTaskStatusAsync(
                    taskId,
                    Domain.Enum.AgentTaskStatus.Failed,
                    "rewrite_executor_failed");
                throw;
            }
        }
    }
}
