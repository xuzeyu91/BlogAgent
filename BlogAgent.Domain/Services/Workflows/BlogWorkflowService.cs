using BlogAgent.Domain.Services.Agents;
using BlogAgent.Domain.Common.Extensions;
using BlogAgent.Domain.Domain.Dto;
using BlogAgent.Domain.Domain.Enum;
using BlogAgent.Domain.Services;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using TaskStatus = BlogAgent.Domain.Domain.Enum.AgentTaskStatus;

namespace BlogAgent.Domain.Services.Workflows
{
    /// <summary>
    /// 博客工作流编排服务
    /// </summary>
    [ServiceDescription(typeof(BlogWorkflowService), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped)]
    public class BlogWorkflowService
    {
        private readonly ResearcherAgent _researcherAgent;
        private readonly WriterAgent _writerAgent;
        private readonly ReviewerAgent _reviewerAgent;
        private readonly BlogService _blogService;
        private readonly WebContentService _webContentService;
        private readonly FileContentService _fileContentService;
        private readonly ILogger<BlogWorkflowService> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;

        public BlogWorkflowService(
            ResearcherAgent researcherAgent,
            WriterAgent writerAgent,
            ReviewerAgent reviewerAgent,
            BlogService blogService,
            WebContentService webContentService,
            FileContentService fileContentService,
            ILogger<BlogWorkflowService> logger)
        {
            _researcherAgent = researcherAgent;
            _writerAgent = writerAgent;
            _reviewerAgent = reviewerAgent;
            _blogService = blogService;
            _webContentService = webContentService;
            _fileContentService = fileContentService;
            _logger = logger;

            // 配置重试策略:最多重试3次,指数退避
            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            $"工作流执行失败,正在进行第{retryCount}次重试. 异常: {exception.Message}");
                    });
        }

        /// <summary>
        /// 执行指定阶段
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="stage">阶段名称(research/write/review)</param>
        /// <param name="additionalInput">额外输入参数</param>
        /// <returns>执行结果</returns>
        public async Task<WorkflowResult> ExecuteStageAsync(int taskId, string stage, object? additionalInput = null)
        {
            var task = await _blogService.GetTaskAsync(taskId);
            
            if (task == null)
            {
                return new WorkflowResult
                {
                    Success = false,
                    Stage = stage,
                    Message = $"任务不存在, TaskId: {taskId}"
                };
            }

            try
            {
                return stage.ToLower() switch
                {
                    "research" => await ExecuteResearchStageAsync(task),
                    "write" => await ExecuteWriteStageAsync(task),
                    "review" => await ExecuteReviewStageAsync(task),
                    _ => new WorkflowResult
                    {
                        Success = false,
                        Stage = stage,
                        Message = $"未知的阶段: {stage}"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行工作流阶段失败, TaskId: {taskId}, Stage: {stage}");
                
                return new WorkflowResult
                {
                    Success = false,
                    Stage = stage,
                    Message = "执行失败",
                    ErrorDetail = ex.Message
                };
            }
        }

        /// <summary>
        /// 执行资料收集阶段
        /// </summary>
        private async Task<WorkflowResult> ExecuteResearchStageAsync(Domain.Model.BlogTask task)
        {
            _logger.LogInformation($"开始执行资料收集阶段, TaskId: {task.Id}");

                // 更新状态
                await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.Researching, "research");

            try
            {
                // 使用重试策略执行
                var result = await _retryPolicy.ExecuteAsync(async () =>
                {
                    // 准备参考资料内容 - 处理 URL 和文件
                    var referenceContent = await PrepareReferenceContentAsync(task);

                    return await _researcherAgent.ResearchAsync(task.Topic, referenceContent, task.Id);
                });

                // 保存结果
                await _blogService.SaveResearchResultAsync(task.Id, result);
                await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.ResearchCompleted, "research_completed");

                _logger.LogInformation($"资料收集阶段完成, TaskId: {task.Id}, KeyPoints: {result.KeyPoints.Count}");

                return new WorkflowResult
                {
                    Success = true,
                    Stage = "research",
                    Output = result,
                    Message = "资料收集完成,请确认后继续"
                };
            }
            catch (Exception)
            {
                await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.Failed, "research_failed");
                throw;
            }
        }

        /// <summary>
        /// 执行博客撰写阶段
        /// </summary>
        private async Task<WorkflowResult> ExecuteWriteStageAsync(Domain.Model.BlogTask task)
        {
            _logger.LogInformation($"开始执行博客撰写阶段, TaskId: {task.Id}");

            // 更新状态
            await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.Writing, "write");

            try
            {
                // 获取资料收集结果
                var content = await _blogService.GetContentAsync(task.Id);
                
                if (content == null || string.IsNullOrWhiteSpace(content.ResearchSummary))
                {
                    return new WorkflowResult
                    {
                        Success = false,
                        Stage = "write",
                        Message = "资料收集结果不存在,请先执行资料收集阶段"
                    };
                }

                // 使用重试策略执行
                var result = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requirements = new WritingRequirements
                    {
                        TargetWordCount = task.TargetWordCount,
                        Style = task.Style,
                        TargetAudience = task.TargetAudience
                    };

                    return await _writerAgent.WriteAsync(
                        task.Topic, 
                        content.ResearchSummary, 
                        requirements, 
                        task.Id);
                });

                // 保存结果
                var contentId = await _blogService.SaveDraftContentAsync(task.Id, result);
                await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.WritingCompleted, "write_completed");

                _logger.LogInformation($"博客撰写阶段完成, TaskId: {task.Id}, WordCount: {result.WordCount}");

                return new WorkflowResult
                {
                    Success = true,
                    Stage = "write",
                    Output = result,
                    Message = "博客撰写完成,请预览后继续"
                };
            }
            catch (Exception)
            {
                await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.Failed, "write_failed");
                throw;
            }
        }

        /// <summary>
        /// 执行质量审查阶段
        /// </summary>
        private async Task<WorkflowResult> ExecuteReviewStageAsync(Domain.Model.BlogTask task)
        {
            _logger.LogInformation($"开始执行质量审查阶段, TaskId: {task.Id}");

            // 更新状态
            await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.Reviewing, "review");

            try
            {
                // 获取博客内容
                var content = await _blogService.GetContentAsync(task.Id);
                
                if (content == null || string.IsNullOrWhiteSpace(content.Content))
                {
                    return new WorkflowResult
                    {
                        Success = false,
                        Stage = "review",
                        Message = "博客内容不存在,请先执行撰写阶段"
                    };
                }

                // 使用重试策略执行
                var result = await _retryPolicy.ExecuteAsync(async () =>
                {
                    return await _reviewerAgent.ReviewAsync(content.Title, content.Content, task.Id);
                });

                // 保存结果
                await _blogService.SaveReviewResultAsync(task.Id, content.Id, result);
                await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.ReviewCompleted, "review_completed");

                _logger.LogInformation(
                    $"质量审查阶段完成, TaskId: {task.Id}, Score: {result.OverallScore}, Recommendation: {result.Recommendation}");

                return new WorkflowResult
                {
                    Success = true,
                    Stage = "review",
                    Output = result,
                    Message = result.OverallScore >= 80 
                        ? "审查通过,可以发布" 
                        : "建议修改后再发布"
                };
            }
            catch (Exception)
            {
                await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.Failed, "review_failed");
                throw;
            }
        }

        /// <summary>
        /// 执行完整工作流(自动化模式)
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="autoPublish">是否自动发布(审查通过后)</param>
        /// <returns>最终结果</returns>
        public async Task<WorkflowResult> ExecuteFullWorkflowAsync(int taskId, bool autoPublish = false)
        {
            _logger.LogInformation($"开始执行完整工作流, TaskId: {taskId}, AutoPublish: {autoPublish}");

            // 1. 资料收集
            var researchResult = await ExecuteStageAsync(taskId, "research");
            if (!researchResult.Success)
            {
                return researchResult;
            }

            // 2. 博客撰写
            var writeResult = await ExecuteStageAsync(taskId, "write");
            if (!writeResult.Success)
            {
                return writeResult;
            }

            // 3. 质量审查
            var reviewResult = await ExecuteStageAsync(taskId, "review");
            if (!reviewResult.Success)
            {
                return reviewResult;
            }

            // 4. 自动发布(如果开启且审查通过)
            if (autoPublish && reviewResult.Output is ReviewResultDto review && review.OverallScore >= 80)
            {
                var published = await _blogService.PublishBlogAsync(taskId);
                
                if (published)
                {
                    _logger.LogInformation($"完整工作流执行完成并已发布, TaskId: {taskId}");
                    
                    return new WorkflowResult
                    {
                        Success = true,
                        Stage = "completed",
                        Output = review,
                        Message = "工作流执行完成,博客已发布"
                    };
                }
            }

            _logger.LogInformation($"完整工作流执行完成, TaskId: {taskId}");

            return new WorkflowResult
            {
                Success = true,
                Stage = "completed",
                Output = reviewResult.Output,
                Message = "工作流执行完成,请手动确认发布"
            };
        }

        /// <summary>
        /// 获取工作流状态
        /// </summary>
        public async Task<WorkflowStateDto> GetWorkflowStateAsync(int taskId)
        {
            var task = await _blogService.GetTaskAsync(taskId);
            
            if (task == null)
            {
                throw new Exception($"任务不存在, TaskId: {taskId}");
            }

            var content = await _blogService.GetContentAsync(taskId);
            var reviewResult = await _blogService.GetReviewResultAsync(taskId);

            return new WorkflowStateDto
            {
                TaskId = taskId,
                Status = task.Status,
                CurrentStage = task.CurrentStage,
                HasResearchResult = content != null && !string.IsNullOrWhiteSpace(content.ResearchSummary),
                HasDraftContent = content != null && !string.IsNullOrWhiteSpace(content.Content),
                HasReviewResult = reviewResult != null,
                IsPublished = content?.IsPublished ?? false
            };
        }

        /// <summary>
        /// 准备参考资料内容 - 处理 URL 和文件
        /// </summary>
        private async Task<string> PrepareReferenceContentAsync(Domain.Model.BlogTask task)
        {
            var contentParts = new List<string>();

            // 1. 如果有直接提供的文本内容,优先使用
            if (!string.IsNullOrWhiteSpace(task.ReferenceContent))
            {
                contentParts.Add($@"
================================================================================
📝 用户提供的参考资料
================================================================================

{task.ReferenceContent}
");
            }

            // 2. 处理 URL 列表
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
                    
                    // 分离 URL 和文件路径
                    var webUrls = urls.Where(WebContentService.IsValidUrl).ToList();
                    var filePaths = urls.Except(webUrls).ToList();

                    // 抓取 Web URL 内容
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

                    // 读取文件内容
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

            // 3. 如果没有任何参考资料
            if (contentParts.Count == 0)
            {
                return "无参考资料,请根据主题自行分析和撰写。";
            }

            return string.Join("\n\n", contentParts);
        }
    }

    /// <summary>
    /// 工作流状态DTO
    /// </summary>
    public class WorkflowStateDto
    {
        public int TaskId { get; set; }
        public Domain.Enum.AgentTaskStatus Status { get; set; }
        public string? CurrentStage { get; set; }
        public bool HasResearchResult { get; set; }
        public bool HasDraftContent { get; set; }
        public bool HasReviewResult { get; set; }
        public bool IsPublished { get; set; }

        /// <summary>
        /// 当前步骤索引(0-3)
        /// </summary>
        public int CurrentStepIndex
        {
            get
            {
            return Status switch
            {
                Domain.Enum.AgentTaskStatus.Created or Domain.Enum.AgentTaskStatus.Researching => 0,
                Domain.Enum.AgentTaskStatus.ResearchCompleted or Domain.Enum.AgentTaskStatus.Writing => 1,
                Domain.Enum.AgentTaskStatus.WritingCompleted or Domain.Enum.AgentTaskStatus.Reviewing => 2,
                Domain.Enum.AgentTaskStatus.ReviewCompleted or Domain.Enum.AgentTaskStatus.Published => 3,
                _ => 0
            };
            }
        }
    }
}

