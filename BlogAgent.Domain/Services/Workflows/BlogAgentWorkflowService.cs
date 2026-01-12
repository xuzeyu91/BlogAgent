using BlogAgent.Domain.Common.Extensions;
using BlogAgent.Domain.Domain.Dto;
using BlogAgent.Domain.Domain.Model;
using BlogAgent.Domain.Services.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BlogAgent.Domain.Services.Workflows
{
    /// <summary>
    /// 博客工作流服务 - 基于 Agent Framework Workflow
    /// </summary>
    [ServiceDescription(typeof(BlogAgentWorkflowService), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped)]
    public class BlogAgentWorkflowService
    {
        private readonly ResearcherAgent _researcherAgent;
        private readonly WriterAgent _writerAgent;
        private readonly ReviewerAgent _reviewerAgent;
        private readonly BlogService _blogService;
        private readonly IMemoryCache _memoryCache;
        private readonly WebContentService _webContentService;
        private readonly FileContentService _fileContentService;
        private readonly ILogger<BlogAgentWorkflowService> _logger;

        public BlogAgentWorkflowService(
            ResearcherAgent researcherAgent,
            WriterAgent writerAgent,
            ReviewerAgent reviewerAgent,
            BlogService blogService,
            IMemoryCache memoryCache,
            WebContentService webContentService,
            FileContentService fileContentService,
            ILogger<BlogAgentWorkflowService> logger)
        {
            _researcherAgent = researcherAgent;
            _writerAgent = writerAgent;
            _reviewerAgent = reviewerAgent;
            _blogService = blogService;
            _memoryCache = memoryCache;
            _webContentService = webContentService;
            _fileContentService = fileContentService;
            _logger = logger;
        }

        /// <summary>
        /// 获取工作流进度缓存键
        /// </summary>
        private string GetProgressCacheKey(int taskId) => $"workflow_progress_{taskId}";

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
        /// 构建博客生成工作流 - 使用 AgentWorkflowBuilder.BuildSequential
        /// </summary>
        private async Task<Workflow> BuildBlogWorkflowAsync()
        {
            _logger.LogInformation("开始构建博客生成工作流");

            // 获取各个 Agent (异步)
            var researcherAgent = await _researcherAgent.GetAgentAsync().ConfigureAwait(false);
            var writerAgent = await _writerAgent.GetAgentAsync().ConfigureAwait(false);
            var reviewerAgent = await _reviewerAgent.GetAgentAsync().ConfigureAwait(false);

            // 使用 AgentWorkflowBuilder 构建顺序执行的工作流
            var workflow = AgentWorkflowBuilder.BuildSequential(
                "BlogGenerationWorkflow",
                researcherAgent,
                writerAgent,
                reviewerAgent
            );

            _logger.LogInformation("博客生成工作流构建完成");

            return workflow;
        }

        /// <summary>
        /// 执行完整的博客生成工作流(带进度更新) - 真正使用 Workflow.RunAsync
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>最终结果</returns>
        public async Task<WorkflowResult> ExecuteFullWorkflowAsync(int taskId)
        {
            _logger.LogInformation($"开始执行博客生成工作流, TaskId: {taskId}");

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
                    StepName = "资料收集",
                    Status = "running",
                    Message = "正在启动 Workflow...",
                    IsCompleted = false
                });

                // 准备输入数据 - 处理 URL 和文件
                var referenceContent = await PrepareReferenceContentAsync(task);

                var input = $@"**主题:** {task.Topic}

**参考资料:**
{referenceContent}

**撰写要求:**
- 目标字数: {task.TargetWordCount ?? 1500}
- 写作风格: {task.Style ?? "专业易懂"}
- 目标读者: {task.TargetAudience ?? "中级开发者"}

请按照三个阶段完成博客生成:
1. 资料收集: 分析主题和参考资料,提取核心要点
2. 博客撰写: 基于收集的资料撰写博客内容
3. 质量审查: 评估博客质量,给出评分和建议";

                _logger.LogInformation($"开始执行 Workflow (真正使用 InProcessExecution.StreamAsync), TaskId: {taskId}");
                await _blogService.UpdateTaskStatusAsync(taskId, Domain.Enum.AgentTaskStatus.Researching, "workflow_running");

                // 构建并执行 Workflow (异步)
                var workflow = await BuildBlogWorkflowAsync().ConfigureAwait(false);
                var messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, input) };
                
                await using var run = await InProcessExecution.StreamAsync(workflow, messages);
                
                // 发送 TurnToken 触发 Agent 执行
                await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
                
                int currentStep = 0;
                string? lastExecutorId = null;

                // 监听 Workflow 事件并更新进度
                var agentOutputs = new Dictionary<string, string>();
                
                await foreach (var evt in run.WatchStreamAsync())
                {
                    if (evt is AgentRunUpdateEvent agentUpdate)
                    {
                        // 检测 Agent 切换
                        if (agentUpdate.ExecutorId != lastExecutorId)
                        {
                            lastExecutorId = agentUpdate.ExecutorId;
                            
                            // 更新当前步骤
                            if (agentUpdate.ExecutorId?.Contains("Researcher") == true)
                            {
                                currentStep = 0;
                                _logger.LogInformation($"ResearcherAgent 开始执行, TaskId: {taskId}");
                                await _blogService.UpdateTaskStatusAsync(taskId, Domain.Enum.AgentTaskStatus.Researching, "research");
                                
                                UpdateProgress(taskId, new WorkflowProgressDto
                                {
                                    TaskId = taskId,
                                    CurrentStep = 0,
                                    StepName = "资料收集",
                                    Status = "running",
                                    Message = "ResearcherAgent 正在分析主题...",
                                    IsCompleted = false,
                                    CurrentOutput = $"🔍 开始分析主题: {task.Topic}\n� 参考资料长度: {referenceContent.Length} 字符"
                                });
                            }
                            else if (agentUpdate.ExecutorId?.Contains("Writer") == true)
                            {
                                currentStep = 1;
                                _logger.LogInformation($"WriterAgent 开始执行, TaskId: {taskId}");
                                await _blogService.UpdateTaskStatusAsync(taskId, Domain.Enum.AgentTaskStatus.Writing, "write");
                                
                                UpdateProgress(taskId, new WorkflowProgressDto
                                {
                                    TaskId = taskId,
                                    CurrentStep = 1,
                                    StepName = "博客撰写",
                                    Status = "running",
                                    Message = "WriterAgent 正在撰写博客...",
                                    IsCompleted = false,
                                    CurrentOutput = $"📝 准备撰写博客\n🎯 目标字数: {task.TargetWordCount ?? 1500} 字"
                                });
                            }
                            else if (agentUpdate.ExecutorId?.Contains("Reviewer") == true)
                            {
                                currentStep = 2;
                                _logger.LogInformation($"ReviewerAgent 开始执行, TaskId: {taskId}");
                                await _blogService.UpdateTaskStatusAsync(taskId, Domain.Enum.AgentTaskStatus.Reviewing, "review");
                                
                                UpdateProgress(taskId, new WorkflowProgressDto
                                {
                                    TaskId = taskId,
                                    CurrentStep = 2,
                                    StepName = "质量审查",
                                    Status = "running",
                                    Message = "ReviewerAgent 正在审查内容...",
                                    IsCompleted = false,
                                    CurrentOutput = "📋 正在评估博客质量..."
                                });
                            }
                            
                            agentOutputs[agentUpdate.ExecutorId!] = "";
                        }
                        
                        // 收集 Agent 输出
                        if (!string.IsNullOrEmpty(agentUpdate.Update.Text))
                        {
                            agentOutputs[agentUpdate.ExecutorId!] += agentUpdate.Update.Text;
                            
                            UpdateProgress(taskId, new WorkflowProgressDto
                            {
                                TaskId = taskId,
                                CurrentStep = currentStep,
                                StepName = GetStepName(currentStep),
                                Status = "running",
                                Message = $"{agentUpdate.ExecutorId} 正在执行...",
                                IsCompleted = false,
                                CurrentOutput = agentOutputs[agentUpdate.ExecutorId!]
                            });
                        }
                    }
                    else if (evt is WorkflowOutputEvent output)
                    {
                        _logger.LogInformation($"Workflow 执行完成, TaskId: {taskId}");
                        
                        // 处理最终输出 - 需要从 Agent 的输出中解析结果
                        await ProcessWorkflowOutputAsync(taskId, task, agentOutputs);
                        break;
                    }
                }
                
                // 获取最终的审查结果
                var reviewResult = await _blogService.GetReviewResultAsync(taskId);
                
                // 更新进度 - 全部完成
                UpdateProgress(taskId, new WorkflowProgressDto
                {
                    TaskId = taskId,
                    CurrentStep = 3,
                    StepName = "完成",
                    Status = "completed",
                    Message = "工作流执行完成!",
                    IsCompleted = true,
                    IsSuccess = true,
                    ReviewResult = reviewResult,
                    CurrentOutput = reviewResult != null 
                        ? $"✅ 工作流执行完成!\n🏆 综合评分: {reviewResult.OverallScore}/100\n✍️ 建议: {reviewResult.Recommendation}"
                        : "✅ 工作流执行完成!"
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
                _logger.LogError(ex, $"工作流执行失败, TaskId: {taskId}");
                
                await _blogService.UpdateTaskStatusAsync(taskId, Domain.Enum.AgentTaskStatus.Failed, "workflow_failed");

                // 更新进度 - 失败
                var currentProgress = GetWorkflowProgress(taskId);
                UpdateProgress(taskId, new WorkflowProgressDto
                {
                    TaskId = taskId,
                    CurrentStep = currentProgress?.CurrentStep ?? 0,
                    StepName = currentProgress?.StepName ?? "执行中",
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
        /// 处理 Workflow 输出 - 解析各个 Agent 的输出并保存到数据库
        /// </summary>
        private async Task ProcessWorkflowOutputAsync(int taskId, Domain.Model.BlogTask task, Dictionary<string, string> agentOutputs)
        {
            _logger.LogInformation($"开始处理 Workflow 输出, TaskId: {taskId}");

            try
            {
                // 1. 处理 ResearcherAgent 输出
                var researcherOutput = agentOutputs.FirstOrDefault(kv => kv.Key.Contains("Researcher")).Value;
                if (!string.IsNullOrEmpty(researcherOutput))
                {
                    var researchOutput = ParseJsonResponse<ResearchOutput>(researcherOutput);
                    if (researchOutput != null)
                    {
                        var researchResult = new ResearchResultDto
                        {
                            Summary = ConvertResearchToMarkdown(researchOutput),
                            KeyPoints = researchOutput.KeyPoints.Select(kp => kp.Content).ToList(),
                            Timestamp = DateTime.Now
                        };
                        await _blogService.SaveResearchResultAsync(taskId, researchResult);
                        await _blogService.UpdateTaskStatusAsync(taskId, Domain.Enum.AgentTaskStatus.ResearchCompleted, "research_completed");
                        _logger.LogInformation($"ResearcherAgent 结果已保存, TaskId: {taskId}");
                    }
                }

                // 2. 处理 WriterAgent 输出
                var writerOutput = agentOutputs.FirstOrDefault(kv => kv.Key.Contains("Writer")).Value;
                if (!string.IsNullOrEmpty(writerOutput))
                {
                    var title = ExtractTitle(writerOutput);
                    var wordCount = CountWords(writerOutput);
                    
                    var draftContent = new DraftContentDto
                    {
                        Title = title,
                        Content = writerOutput,
                        WordCount = wordCount,
                        GeneratedAt = DateTime.Now
                    };
                    await _blogService.SaveDraftContentAsync(taskId, draftContent);
                    await _blogService.UpdateTaskStatusAsync(taskId, Domain.Enum.AgentTaskStatus.WritingCompleted, "write_completed");
                    _logger.LogInformation($"WriterAgent 结果已保存, TaskId: {taskId}, 标题: {title}");
                }

                // 3. 处理 ReviewerAgent 输出
                var reviewerOutput = agentOutputs.FirstOrDefault(kv => kv.Key.Contains("Reviewer")).Value;
                if (!string.IsNullOrEmpty(reviewerOutput))
                {
                    var reviewResult = ParseJsonResponse<ReviewResultDto>(reviewerOutput);
                    if (reviewResult != null)
                    {
                        var content = await _blogService.GetContentAsync(taskId);
                        if (content != null)
                        {
                            await _blogService.SaveReviewResultAsync(taskId, content.Id, reviewResult);
                            await _blogService.UpdateTaskStatusAsync(taskId, Domain.Enum.AgentTaskStatus.ReviewCompleted, "review_completed");
                            _logger.LogInformation($"ReviewerAgent 结果已保存, TaskId: {taskId}, 评分: {reviewResult.OverallScore}");
                        }
                    }
                }

                _logger.LogInformation($"Workflow 输出处理完成, TaskId: {taskId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"处理 Workflow 输出失败, TaskId: {taskId}");
                throw;
            }
        }

        /// <summary>
        /// 根据步骤索引获取步骤名称
        /// </summary>
        private string GetStepName(int stepIndex)
        {
            return stepIndex switch
            {
                0 => "资料收集",
                1 => "博客撰写",
                2 => "质量审查",
                _ => "完成"
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

        /// <summary>
        /// 执行指定阶段(保持向后兼容)
        /// </summary>
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
        /// 执行资料收集阶段(单独执行)
        /// </summary>
        private async Task<WorkflowResult> ExecuteResearchStageAsync(Domain.Model.BlogTask task)
        {
            _logger.LogInformation($"开始执行资料收集阶段, TaskId: {task.Id}");

            await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.Researching, "research");

            try
            {
                // 准备参考资料内容 - 处理 URL 和文件
                var referenceContent = await PrepareReferenceContentAsync(task);

                var result = await _researcherAgent.ResearchAsync(task.Topic, referenceContent, task.Id);

                await _blogService.SaveResearchResultAsync(task.Id, result);
                await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.ResearchCompleted, "research_completed");

                _logger.LogInformation($"资料收集阶段完成, TaskId: {task.Id}");

                return new WorkflowResult
                {
                    Success = true,
                    Stage = "research",
                    Output = result,
                    Message = "资料收集完成"
                };
            }
            catch (Exception)
            {
                await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.Failed, "research_failed");
                throw;
            }
        }

        /// <summary>
        /// 执行博客撰写阶段(单独执行)
        /// </summary>
        private async Task<WorkflowResult> ExecuteWriteStageAsync(Domain.Model.BlogTask task)
        {
            _logger.LogInformation($"开始执行博客撰写阶段, TaskId: {task.Id}");

            await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.Writing, "write");

            try
            {
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

                var requirements = new WritingRequirements
                {
                    TargetWordCount = task.TargetWordCount,
                    Style = task.Style,
                    TargetAudience = task.TargetAudience
                };

                var result = await _writerAgent.WriteAsync(task.Topic, content.ResearchSummary, requirements, task.Id);

                await _blogService.SaveDraftContentAsync(task.Id, result);
                await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.WritingCompleted, "write_completed");

                _logger.LogInformation($"博客撰写阶段完成, TaskId: {task.Id}");

                return new WorkflowResult
                {
                    Success = true,
                    Stage = "write",
                    Output = result,
                    Message = "博客撰写完成"
                };
            }
            catch (Exception)
            {
                await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.Failed, "write_failed");
                throw;
            }
        }

        /// <summary>
        /// 执行质量审查阶段(单独执行)
        /// </summary>
        private async Task<WorkflowResult> ExecuteReviewStageAsync(Domain.Model.BlogTask task)
        {
            _logger.LogInformation($"开始执行质量审查阶段, TaskId: {task.Id}");

            await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.Reviewing, "review");

            try
            {
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

                var result = await _reviewerAgent.ReviewAsync(content.Title, content.Content, task.Id);

                await _blogService.SaveReviewResultAsync(task.Id, content.Id, result);
                await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.ReviewCompleted, "review_completed");

                _logger.LogInformation($"质量审查阶段完成, TaskId: {task.Id}");

                return new WorkflowResult
                {
                    Success = true,
                    Stage = "review",
                    Output = result,
                    Message = result.OverallScore >= 80 ? "审查通过" : "建议修改后再发布"
                };
            }
            catch (Exception)
            {
                await _blogService.UpdateTaskStatusAsync(task.Id, Domain.Enum.AgentTaskStatus.Failed, "review_failed");
                throw;
            }
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

        // ============ 辅助方法 ============

        private string ConvertResearchToMarkdown(ResearchOutput output)
        {
            var markdown = new System.Text.StringBuilder();

            markdown.AppendLine("## 主题分析");
            if (output.TopicAnalysis != null)
            {
                if (!string.IsNullOrEmpty(output.TopicAnalysis.TechnicalBackground))
                    markdown.AppendLine($"**技术背景:** {output.TopicAnalysis.TechnicalBackground}");
                if (!string.IsNullOrEmpty(output.TopicAnalysis.UseCases))
                    markdown.AppendLine($"**应用场景:** {output.TopicAnalysis.UseCases}");
                if (!string.IsNullOrEmpty(output.TopicAnalysis.TargetAudience))
                    markdown.AppendLine($"**目标读者:** {output.TopicAnalysis.TargetAudience}");
                if (!string.IsNullOrEmpty(output.TopicAnalysis.Summary))
                    markdown.AppendLine($"**概述:** {output.TopicAnalysis.Summary}");
            }
            markdown.AppendLine();

            markdown.AppendLine("## 核心要点");
            foreach (var point in output.KeyPoints.OrderByDescending(p => p.Importance))
            {
                var stars = new string('⭐', point.Importance);
                markdown.AppendLine($"{stars} {point.Content}");
            }
            markdown.AppendLine();

            markdown.AppendLine("## 技术细节");
            foreach (var detail in output.TechnicalDetails)
            {
                markdown.AppendLine($"### {detail.Title}");
                markdown.AppendLine(detail.Description);
                markdown.AppendLine();
            }

            if (output.CodeExamples.Any())
            {
                markdown.AppendLine("## 代码示例");
                foreach (var example in output.CodeExamples)
                {
                    markdown.AppendLine($"```{example.Language}");
                    markdown.AppendLine(example.Code);
                    markdown.AppendLine("```");
                    markdown.AppendLine(example.Description);
                    markdown.AppendLine();
                }
            }

            markdown.AppendLine("## 参考来源");
            foreach (var reference in output.References)
            {
                markdown.AppendLine($"- {reference}");
            }

            return markdown.ToString();
        }

        private string ExtractTitle(string markdown)
        {
            var lines = markdown.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("# "))
                {
                    return trimmed.Substring(2).Trim();
                }
            }
            return "未命名博客";
        }

        private int CountWords(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return 0;

            var cleanContent = content
                .Replace("```", "")
                .Replace("#", "")
                .Replace("*", "")
                .Replace("-", "")
                .Replace(">", "")
                .Replace("|", "")
                .Trim();

            int chineseChars = cleanContent.Count(c => c >= 0x4E00 && c <= 0x9FA5);
            int englishWords = cleanContent.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(word => word.Any(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')));

            return chineseChars + englishWords;
        }

        private T? ParseJsonResponse<T>(string jsonContent) where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                var startIndex = jsonContent.IndexOf('{');
                var endIndex = jsonContent.LastIndexOf('}');

                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var jsonStr = jsonContent.Substring(startIndex, endIndex - startIndex + 1);
                    return JsonSerializer.Deserialize<T>(jsonStr, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }

                return null;
            }
        }
    }
}
