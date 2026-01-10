using BlogAgent.Domain.Common.Constants;
using BlogAgent.Domain.Domain.Dto;
using BlogAgent.Domain.Services.Agents;
using BlogAgent.Domain.Services.Workflows.Messages;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace BlogAgent.Domain.Services.Workflows.Executors
{
    /// <summary>
    /// 研究员执行器 - 负责资料收集和整理
    /// </summary>
    public class ResearcherExecutor : Executor<BlogTaskInput, ResearchResultOutput>
    {
        private readonly ResearcherAgent _agent;
        private readonly BlogService _blogService;
        private readonly ILogger<ResearcherExecutor> _logger;

        public ResearcherExecutor(
            ResearcherAgent agent,
            BlogService blogService,
            ILogger<ResearcherExecutor> logger)
            : base("ResearcherExecutor")
        {
            _agent = agent;
            _blogService = blogService;
            _logger = logger;
        }

        public override async ValueTask<ResearchResultOutput> HandleAsync(
            BlogTaskInput input,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"[ResearcherExecutor] 开始执行资料收集, TaskId: {input.TaskId}");

            // 更新任务状态
            await _blogService.UpdateTaskStatusAsync(
                input.TaskId,
                Domain.Enum.AgentTaskStatus.Researching,
                "research_executor_start");

            try
            {
                // 调用 ResearcherAgent 进行资料收集
                var result = await _agent.ResearchAsync(
                    input.Topic,
                    input.ReferenceContent,
                    input.TaskId);

                // 构建输出
                var output = new ResearchResultOutput
                {
                    TaskId = input.TaskId,
                    SummaryMarkdown = result.Summary,
                    KeyPoints = result.KeyPoints.Select(kp =>
                        new ResearchResultOutput.KeyPoint { Importance = 3, Content = kp }).ToList(),
                    References = new List<string>()
                };

                // 保存到数据库
                await _blogService.SaveResearchResultAsync(input.TaskId, result);
                await _blogService.UpdateTaskStatusAsync(
                    input.TaskId,
                    Domain.Enum.AgentTaskStatus.ResearchCompleted,
                    "research_executor_completed");

                // 存储到 Shared State
                await context.QueueStateUpdateAsync(
                    BlogStateConstants.ResearchResultKey,
                    output,
                    BlogStateConstants.BlogStateScope,
                    cancellationToken);

                _logger.LogInformation($"[ResearcherExecutor] 资料收集完成, TaskId: {input.TaskId}");

                return output;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[ResearcherExecutor] 资料收集失败, TaskId: {input.TaskId}");
                await _blogService.UpdateTaskStatusAsync(
                    input.TaskId,
                    Domain.Enum.AgentTaskStatus.Failed,
                    "research_executor_failed");
                throw;
            }
        }
    }
}
