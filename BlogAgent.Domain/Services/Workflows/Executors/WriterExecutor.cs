using BlogAgent.Domain.Common.Constants;
using BlogAgent.Domain.Domain.Dto;
using BlogAgent.Domain.Services.Agents;
using BlogAgent.Domain.Services.Workflows.Messages;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace BlogAgent.Domain.Services.Workflows.Executors
{
    /// <summary>
    /// 作家执行器 - 负责博客撰写
    /// </summary>
    public class WriterExecutor : Executor<ResearchResultOutput, DraftContentOutput>
    {
        private readonly WriterAgent _agent;
        private readonly BlogService _blogService;
        private readonly ILogger<WriterExecutor> _logger;

        public WriterExecutor(
            WriterAgent agent,
            BlogService blogService,
            ILogger<WriterExecutor> logger)
            : base("WriterExecutor")
        {
            _agent = agent;
            _blogService = blogService;
            _logger = logger;
        }

        public override async ValueTask<DraftContentOutput> HandleAsync(
            ResearchResultOutput researchResult,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            var taskId = researchResult.TaskId;
            _logger.LogInformation($"[WriterExecutor] 开始撰写博客, TaskId: {taskId}");

            // 更新任务状态
            await _blogService.UpdateTaskStatusAsync(
                taskId,
                Domain.Enum.AgentTaskStatus.Writing,
                "write_executor_start");

            try
            {
                // 从 Shared State 获取任务信息
                var taskInfo = await context.ReadStateAsync<BlogTaskInput>(
                    BlogStateConstants.TaskInfoKey,
                    BlogStateConstants.BlogStateScope,
                    cancellationToken);

                if (taskInfo == null)
                {
                    throw new InvalidOperationException("任务信息不存在于 Shared State");
                }

                // 构建写作要求
                var requirements = new WritingRequirements
                {
                    TargetWordCount = taskInfo.TargetWordCount,
                    Style = taskInfo.Style,
                    TargetAudience = taskInfo.TargetAudience
                };

                // 调用 WriterAgent 撰写博客
                var result = await _agent.WriteAsync(
                    taskInfo.Topic,
                    researchResult.SummaryMarkdown,
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

                // 保存到数据库
                await _blogService.SaveDraftContentAsync(taskId, result);
                await _blogService.UpdateTaskStatusAsync(
                    taskId,
                    Domain.Enum.AgentTaskStatus.WritingCompleted,
                    "write_executor_completed");

                // 存储到 Shared State
                await context.QueueStateUpdateAsync(
                    BlogStateConstants.DraftContentKey,
                    output,
                    BlogStateConstants.BlogStateScope,
                    cancellationToken);

                _logger.LogInformation($"[WriterExecutor] 博客撰写完成, TaskId: {taskId}, 标题: {output.Title}");

                return output;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[WriterExecutor] 博客撰写失败, TaskId: {taskId}");
                await _blogService.UpdateTaskStatusAsync(
                    taskId,
                    Domain.Enum.AgentTaskStatus.Failed,
                    "write_executor_failed");
                throw;
            }
        }
    }
}
