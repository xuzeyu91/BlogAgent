using BlogAgent.Domain.Common.Constants;
using BlogAgent.Domain.Common.Extensions;
using BlogAgent.Domain.Domain.Dto;
using BlogAgent.Domain.Domain.Model;
using BlogAgent.Domain.Repositories;
using BlogAgent.Domain.Services.Agents;
using BlogAgent.Domain.Services.Workflows.Executors;
using BlogAgent.Domain.Services.Workflows.Messages;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BlogAgent.Domain.Services.Workflows
{
    /// <summary>
    /// 博客工作流服务 V2 - 使用条件工作流和 Shared State
    /// 特性：
    /// 1. 根据评分自动决定重写或发布
    /// 2. 使用 Shared State 在 Executor 间传递数据
    /// 3. 支持最大重写次数限制
    /// </summary>
    [ServiceDescription(typeof(BlogAgentWorkflowServiceV2), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped)]
    public class BlogAgentWorkflowServiceV2
    {
        private readonly ResearcherAgent _researcherAgent;
        private readonly WriterAgent _writerAgent;
        private readonly ReviewerAgent _reviewerAgent;
        private readonly BlogService _blogService;
        private readonly IMemoryCache _memoryCache;
        private readonly WebContentService _webContentService;
        private readonly FileContentService _fileContentService;
        private readonly ILogger<BlogAgentWorkflowServiceV2> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public BlogAgentWorkflowServiceV2(
            ResearcherAgent researcherAgent,
            WriterAgent writerAgent,
            ReviewerAgent reviewerAgent,
            BlogService blogService,
            IMemoryCache memoryCache,
            WebContentService webContentService,
            FileContentService fileContentService,
            ILogger<BlogAgentWorkflowServiceV2> logger,
            ILoggerFactory loggerFactory)
        {
            _researcherAgent = researcherAgent;
            _writerAgent = writerAgent;
            _reviewerAgent = reviewerAgent;
            _blogService = blogService;
            _memoryCache = memoryCache;
            _webContentService = webContentService;
            _fileContentService = fileContentService;
            _logger = logger;
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// 获取工作流进度缓存键
        /// </summary>
        private string GetProgressCacheKey(int taskId) => $"workflow_v2_progress_{taskId}";

        /// <summary>
        /// 更新工作流进度
        /// </summary>
        private void UpdateProgress(int taskId, WorkflowProgressDto progress)
        {
            var cacheKey = GetProgressCacheKey(taskId);
            _memoryCache.Set(cacheKey, progress, TimeSpan.FromHours(1));
        }

        /// <summary>
        /// 获取工作流执行进度
        /// </summary>
        public WorkflowProgressDto? GetWorkflowProgress(int taskId)
        {
            var cacheKey = GetProgressCacheKey(taskId);
            return _memoryCache.Get<WorkflowProgressDto>(cacheKey);
        }

        /// <summary>
        /// 构建条件工作流
        /// 流程：
        /// 1. Researcher → Writer → Reviewer
        /// 2. Reviewer 判断评分：
        ///    - 评分 >= 80 → Publish
        ///    - 评分 < 80 且重写次数 < 最大值 → Rewrite → Reviewer
        ///    - 评分 < 80 且重写次数 >= 最大值 → Failure
        /// </summary>
        private Workflow BuildConditionalWorkflow()
        {
            _logger.LogInformation("开始构建条件工作流");

            // 创建执行器（使用 ILoggerFactory 创建正确类型的 logger）
            var researcherExecutor = new ResearcherExecutor(_researcherAgent, _blogService, _loggerFactory.CreateLogger<ResearcherExecutor>());
            var writerExecutor = new WriterExecutor(_writerAgent, _blogService, _loggerFactory.CreateLogger<WriterExecutor>());
            var reviewerExecutor = new ReviewerExecutor(_reviewerAgent, _blogService, _loggerFactory.CreateLogger<ReviewerExecutor>());
            var rewriteExecutor = new RewriteExecutor(_writerAgent, _blogService, _loggerFactory.CreateLogger<RewriteExecutor>());
            var publishExecutor = new PublishExecutor(_blogService, _loggerFactory.CreateLogger<PublishExecutor>());
            var failureExecutor = new FailureExecutor(_blogService, _loggerFactory.CreateLogger<FailureExecutor>());

            // 构建条件工作流
            var workflow = new WorkflowBuilder(researcherExecutor)

                // Researcher → Writer
                .AddEdge(researcherExecutor, writerExecutor)

                // Writer → Reviewer
                .AddEdge(writerExecutor, reviewerExecutor)

                // Reviewer → Rewrite (评分 < 80)
                .AddEdge(reviewerExecutor, rewriteExecutor,
                    condition: (ReviewResultOutput review) => review.OverallScore < 80)

                // Reviewer → Publish (评分 >= 80)
                .AddEdge(reviewerExecutor, publishExecutor,
                    condition: (ReviewResultOutput review) => review.OverallScore >= 80)

                // Rewrite → Reviewer (重写后重新审查)
                .AddEdge(rewriteExecutor, reviewerExecutor)

                // 从 Rewrite 直接到 Publish (重写后评分达标)
                .AddEdge(rewriteExecutor, publishExecutor,
                    condition: (DraftContentOutput draft) => false) // 这个条件需要特殊处理

                // Rewrite → Failure (达到最大重写次数)
                // 这个逻辑在 RewriteExecutor 内部处理

                // 设置工作流输出
                .WithOutputFrom(publishExecutor)
                .WithOutputFrom(failureExecutor)

                .Build();

            _logger.LogInformation("条件工作流构建完成");

            return workflow;
        }

        /// <summary>
        /// 执行完整的博客生成工作流（带自动重写）
        /// </summary>
        public async Task<WorkflowResult> ExecuteFullWorkflowAsync(int taskId)
        {
            _logger.LogInformation($"开始执行条件工作流, TaskId: {taskId}");

            var task = await _blogService.GetTaskAsync(taskId);
            if (task == null)
            {
                return new WorkflowResult
                {
                    Success = false,
                    Stage = "workflow",
                    Message = $"任务不存在, TaskId: {taskId}"
                };
            }

            try
            {
                // 初始化进度
                UpdateProgress(taskId, new WorkflowProgressDto
                {
                    TaskId = taskId,
                    CurrentStep = 0,
                    StepName = "准备中",
                    Status = "running",
                    Message = "正在启动条件工作流...",
                    IsCompleted = false
                });

                // 准备输入数据
                var referenceContent = await PrepareReferenceContentAsync(task);

                var input = new BlogTaskInput
                {
                    TaskId = task.Id,
                    Topic = task.Topic,
                    ReferenceContent = referenceContent,
                    TargetWordCount = task.TargetWordCount,
                    Style = task.Style,
                    TargetAudience = task.TargetAudience,
                    TaskInfo = task
                };

                // 构建工作流
                var workflow = BuildConditionalWorkflow();

                _logger.LogInformation($"开始执行条件工作流, TaskId: {taskId}");
                await _blogService.UpdateTaskStatusAsync(taskId, Domain.Enum.AgentTaskStatus.Researching, "workflow_v2_running");

                // 执行工作流
                await using var run = await InProcessExecution.StreamAsync(workflow, input);

                int currentStep = 0;
                string lastExecutorId = string.Empty;
                var executorLog = new List<string>();

                // 监听工作流事件
                await foreach (var evt in run.WatchStreamAsync())
                {
                    if (evt is ExecutorInvokedEvent invokedEvent)
                    {
                        lastExecutorId = invokedEvent.ExecutorId;
                        executorLog.Add($"[{DateTime.Now:HH:mm:ss}] {invokedEvent.ExecutorId} 开始执行");

                        // 更新进度
                        UpdateProgress(taskId, new WorkflowProgressDto
                        {
                            TaskId = taskId,
                            CurrentStep = currentStep,
                            StepName = GetExecutorDisplayName(invokedEvent.ExecutorId),
                            Status = "running",
                            Message = $"{GetExecutorDisplayName(invokedEvent.ExecutorId)} 正在执行...",
                            IsCompleted = false,
                            ExecutorLog = string.Join("\n", executorLog)
                        });

                        // 更新任务状态
                        await UpdateTaskStatusByExecutor(taskId, invokedEvent.ExecutorId);
                    }
                    else if (evt is ExecutorCompletedEvent completedEvent)
                    {
                        _logger.LogInformation($"[{completedEvent.ExecutorId}] 执行完成");

                        if (completedEvent.ExecutorId.Contains("Rewrite"))
                        {
                            currentStep++; // 重写算作额外步骤
                        }
                    }
                    else if (evt is WorkflowOutputEvent outputEvent)
                    {
                        _logger.LogInformation($"条件工作流执行完成, 输出: {outputEvent.Data}");

                        var finalMessage = outputEvent.Data?.ToString() ?? "工作流执行完成";

                        UpdateProgress(taskId, new WorkflowProgressDto
                        {
                            TaskId = taskId,
                            CurrentStep = currentStep,
                            StepName = "完成",
                            Status = "completed",
                            Message = finalMessage,
                            IsCompleted = true,
                            IsSuccess = true,
                            ExecutorLog = string.Join("\n", executorLog)
                        });

                        return new WorkflowResult
                        {
                            Success = true,
                            Stage = "completed",
                            Message = finalMessage
                        };
                    }
                    else if (evt is ExecutorFailedEvent failedEvent)
                    {
                        _logger.LogError($"[{failedEvent.ExecutorId}] 执行失败: {failedEvent.Data?.Message}");

                        UpdateProgress(taskId, new WorkflowProgressDto
                        {
                            TaskId = taskId,
                            CurrentStep = currentStep,
                            StepName = "失败",
                            Status = "failed",
                            Message = $"工作流执行失败: {failedEvent.Data?.Message}",
                            IsCompleted = true,
                            IsSuccess = false,
                            ErrorMessage = failedEvent.Data?.Message,
                            ExecutorLog = string.Join("\n", executorLog)
                        });

                        return new WorkflowResult
                        {
                            Success = false,
                            Stage = "workflow",
                            Message = "工作流执行失败",
                            ErrorDetail = failedEvent.Data?.Message
                        };
                    }
                }

                // 如果没有输出事件，检查最终状态
                var reviewResult = await _blogService.GetReviewResultAsync(taskId);
                UpdateProgress(taskId, new WorkflowProgressDto
                {
                    TaskId = taskId,
                    CurrentStep = currentStep,
                    StepName = "完成",
                    Status = "completed",
                    Message = "工作流执行完成",
                    IsCompleted = true,
                    IsSuccess = true,
                    ReviewResult = reviewResult,
                    ExecutorLog = string.Join("\n", executorLog)
                });

                return new WorkflowResult
                {
                    Success = true,
                    Stage = "completed",
                    Message = "工作流执行完成"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"条件工作流执行失败, TaskId: {taskId}");

                await _blogService.UpdateTaskStatusAsync(taskId, Domain.Enum.AgentTaskStatus.Failed, "workflow_v2_failed");

                UpdateProgress(taskId, new WorkflowProgressDto
                {
                    TaskId = taskId,
                    CurrentStep = 0,
                    StepName = "失败",
                    Status = "failed",
                    Message = "工作流执行失败",
                    IsCompleted = true,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });

                return new WorkflowResult
                {
                    Success = false,
                    Stage = "workflow",
                    Message = "工作流执行失败",
                    ErrorDetail = ex.Message
                };
            }
        }

        /// <summary>
        /// 根据执行器ID更新任务状态
        /// </summary>
        private async Task UpdateTaskStatusByExecutor(int taskId, string executorId)
        {
            var status = executorId switch
            {
                var id when id.Contains("Researcher") => Domain.Enum.AgentTaskStatus.Researching,
                var id when id.Contains("Writer") => Domain.Enum.AgentTaskStatus.Writing,
                var id when id.Contains("Reviewer") => Domain.Enum.AgentTaskStatus.Reviewing,
                var id when id.Contains("Rewrite") => Domain.Enum.AgentTaskStatus.Writing,
                var id when id.Contains("Publish") => Domain.Enum.AgentTaskStatus.Published,
                var id when id.Contains("Failure") => Domain.Enum.AgentTaskStatus.Failed,
                _ => Domain.Enum.AgentTaskStatus.Created
            };

            var stage = executorId switch
            {
                var id when id.Contains("Researcher") => "research",
                var id when id.Contains("Writer") => "write",
                var id when id.Contains("Reviewer") => "review",
                var id when id.Contains("Rewrite") => "rewrite",
                var id when id.Contains("Publish") => "publish",
                var id when id.Contains("Failure") => "failed",
                _ => "unknown"
            };

            await _blogService.UpdateTaskStatusAsync(taskId, status, stage);
        }

        /// <summary>
        /// 获取执行器显示名称
        /// </summary>
        private static string GetExecutorDisplayName(string executorId)
        {
            return executorId switch
            {
                var id when id.Contains("Researcher") => "资料收集",
                var id when id.Contains("Writer") => "博客撰写",
                var id when id.Contains("Reviewer") => "质量审查",
                var id when id.Contains("Rewrite") => "内容重写",
                var id when id.Contains("Publish") => "发布",
                var id when id.Contains("Failure") => "失败处理",
                _ => executorId
            };
        }

        /// <summary>
        /// 准备参考资料内容（复用原有逻辑）
        /// </summary>
        private async Task<string> PrepareReferenceContentAsync(BlogTask task)
        {
            var contentParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(task.ReferenceContent))
            {
                contentParts.Add($@"
================================================================================
📝 用户提供的参考资料
================================================================================

{task.ReferenceContent}
");
            }

            if (!string.IsNullOrWhiteSpace(task.ReferenceUrls))
            {
                var urls = task.ReferenceUrls
                    .Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(u => u.Trim())
                    .Where(u => !string.IsNullOrWhiteSpace(u))
                    .ToList();

                if (urls.Any())
                {
                    _logger.LogInformation($"开始抓取 {urls.Count} 个 URL, TaskId: {task.Id}");

                    var webUrls = urls.Where(WebContentService.IsValidUrl).ToList();
                    var filePaths = urls.Except(webUrls).ToList();

                    if (webUrls.Any())
                    {
                        try
                        {
                            var webContent = await _webContentService.FetchMultipleUrlsAsync(webUrls);
                            contentParts.Add(webContent);
                            _logger.LogInformation($"成功抓取 {webUrls.Count} 个 URL, TaskId: {task.Id}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"URL 抓取失败, TaskId: {task.Id}");
                            contentParts.Add($"[部分 URL 抓取失败: {ex.Message}]");
                        }
                    }

                    if (filePaths.Any())
                    {
                        try
                        {
                            var fileContent = await _fileContentService.ReadMultipleFilesAsync(filePaths);
                            contentParts.Add(fileContent);
                            _logger.LogInformation($"成功读取 {filePaths.Count} 个文件, TaskId: {task.Id}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"文件读取失败, TaskId: {task.Id}");
                            contentParts.Add($"[部分文件读取失败: {ex.Message}]");
                        }
                    }
                }
            }

            if (contentParts.Count == 0)
            {
                return "无参考资料,请根据主题自行分析和撰写。";
            }

            return string.Join("\n\n", contentParts);
        }
    }
}
