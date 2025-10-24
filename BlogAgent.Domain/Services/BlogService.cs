using System.Text.Json;
using BlogAgent.Domain.Common.Extensions;
using BlogAgent.Domain.Domain.Dto;
using BlogAgent.Domain.Domain.Enum;
using BlogAgent.Domain.Domain.Model;
using BlogAgent.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace BlogAgent.Domain.Services
{
    /// <summary>
    /// 博客业务服务
    /// </summary>
    [ServiceDescription(typeof(BlogService), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped)]
    public class BlogService
    {
        private readonly BlogTaskRepository _taskRepository;
        private readonly BlogContentRepository _contentRepository;
        private readonly ReviewResultRepository _reviewResultRepository;
        private readonly ILogger<BlogService> _logger;

        public BlogService(
            BlogTaskRepository taskRepository,
            BlogContentRepository contentRepository,
            ReviewResultRepository reviewResultRepository,
            ILogger<BlogService> logger)
        {
            _taskRepository = taskRepository;
            _contentRepository = contentRepository;
            _reviewResultRepository = reviewResultRepository;
            _logger = logger;
        }

        /// <summary>
        /// 创建博客任务
        /// </summary>
        public async Task<int> CreateTaskAsync(CreateBlogRequest request)
        {
            var task = new BlogTask
            {
                Topic = request.Topic,
                ReferenceContent = request.ReferenceContent,
                ReferenceUrls = request.ReferenceUrls,
                TargetWordCount = request.TargetWordCount,
                Style = request.Style,
                TargetAudience = request.TargetAudience,
                Status = Domain.Enum.AgentTaskStatus.Created,
                CurrentStage = "created",
                CreatedAt = DateTime.Now
            };

            var taskId = await _taskRepository.InsertReturnIdentityAsync(task);
            _logger.LogInformation($"创建博客任务成功, TaskId: {taskId}, Topic: {request.Topic}");
            
            return taskId;
        }

        /// <summary>
        /// 获取任务
        /// </summary>
        public async Task<BlogTask?> GetTaskAsync(int taskId)
        {
            return await _taskRepository.GetByIdAsync(taskId);
        }

        /// <summary>
        /// 获取任务列表
        /// </summary>
        public async Task<List<BlogTaskDto>> GetTaskListAsync(int count = 20)
        {
            var tasks = await _taskRepository.GetRecentTasksAsync(count);
            
            return tasks.Select(t => new BlogTaskDto
            {
                Id = t.Id,
                Topic = t.Topic,
                Status = t.Status,
                CurrentStage = t.CurrentStage,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }).ToList();
        }

        /// <summary>
        /// 更新任务状态
        /// </summary>
        public async Task<bool> UpdateTaskStatusAsync(int taskId, Domain.Enum.AgentTaskStatus status, string? currentStage = null)
        {
            _logger.LogInformation($"更新任务状态, TaskId: {taskId}, Status: {status}, Stage: {currentStage}");
            return await _taskRepository.UpdateStatusAsync(taskId, status, currentStage);
        }

        /// <summary>
        /// 保存资料收集结果
        /// </summary>
        public async Task SaveResearchResultAsync(int taskId, ResearchResultDto result)
        {
            // 获取或创建BlogContent
            var content = await _contentRepository.GetByTaskIdAsync(taskId);
            
            if (content == null)
            {
                content = new BlogContent
                {
                    TaskId = taskId,
                    Title = "待生成",
                    Content = "",
                    ResearchSummary = result.Summary,
                    CreatedAt = DateTime.Now
                };
                await _contentRepository.InsertAsync(content);
            }
            else
            {
                content.ResearchSummary = result.Summary;
                content.UpdatedAt = DateTime.Now;
                await _contentRepository.UpdateAsync(content);
            }

            _logger.LogInformation($"保存资料收集结果成功, TaskId: {taskId}");
        }

        /// <summary>
        /// 保存博客初稿
        /// </summary>
        public async Task<int> SaveDraftContentAsync(int taskId, DraftContentDto draft)
        {
            var content = await _contentRepository.GetByTaskIdAsync(taskId);

            if (content == null)
            {
                content = new BlogContent
                {
                    TaskId = taskId,
                    Title = draft.Title,
                    Content = draft.Content,
                    WordCount = draft.WordCount,
                    CreatedAt = DateTime.Now
                };
                var contentId = await _contentRepository.InsertReturnIdentityAsync(content);
                _logger.LogInformation($"保存博客初稿成功, TaskId: {taskId}, ContentId: {contentId}");
                return contentId;
            }
            else
            {
                content.Title = draft.Title;
                content.Content = draft.Content;
                content.WordCount = draft.WordCount;
                content.UpdatedAt = DateTime.Now;
                await _contentRepository.UpdateAsync(content);
                _logger.LogInformation($"更新博客初稿成功, TaskId: {taskId}, ContentId: {content.Id}");
                return content.Id;
            }
        }

        /// <summary>
        /// 保存审查结果
        /// </summary>
        public async Task SaveReviewResultAsync(int taskId, int contentId, ReviewResultDto reviewDto)
        {
            var result = new ReviewResult
            {
                TaskId = taskId,
                ContentId = contentId,
                OverallScore = reviewDto.OverallScore,
                AccuracyScore = reviewDto.Accuracy.Score,
                AccuracyIssues = JsonSerializer.Serialize(reviewDto.Accuracy.Issues),
                LogicScore = reviewDto.Logic.Score,
                LogicIssues = JsonSerializer.Serialize(reviewDto.Logic.Issues),
                OriginalityScore = reviewDto.Originality.Score,
                OriginalityIssues = JsonSerializer.Serialize(reviewDto.Originality.Issues),
                FormattingScore = reviewDto.Formatting.Score,
                FormattingIssues = JsonSerializer.Serialize(reviewDto.Formatting.Issues),
                Recommendation = reviewDto.Recommendation,
                Summary = reviewDto.Summary,
                CreatedAt = DateTime.Now
            };

            await _reviewResultRepository.InsertAsync(result);
            _logger.LogInformation($"保存审查结果成功, TaskId: {taskId}, Score: {result.OverallScore}");
        }

        /// <summary>
        /// 获取博客内容
        /// </summary>
        public async Task<BlogContent?> GetContentAsync(int taskId)
        {
            return await _contentRepository.GetByTaskIdAsync(taskId);
        }

        /// <summary>
        /// 获取审查结果
        /// </summary>
        public async Task<ReviewResultDto?> GetReviewResultAsync(int taskId)
        {
            var result = await _reviewResultRepository.GetByTaskIdAsync(taskId);
            
            if (result == null)
                return null;

            return new ReviewResultDto
            {
                OverallScore = result.OverallScore,
                Accuracy = new DimensionScore
                {
                    Score = result.AccuracyScore,
                    Issues = JsonSerializer.Deserialize<List<string>>(result.AccuracyIssues ?? "[]") ?? new List<string>()
                },
                Logic = new DimensionScore
                {
                    Score = result.LogicScore,
                    Issues = JsonSerializer.Deserialize<List<string>>(result.LogicIssues ?? "[]") ?? new List<string>()
                },
                Originality = new DimensionScore
                {
                    Score = result.OriginalityScore,
                    Issues = JsonSerializer.Deserialize<List<string>>(result.OriginalityIssues ?? "[]") ?? new List<string>()
                },
                Formatting = new DimensionScore
                {
                    Score = result.FormattingScore,
                    Issues = JsonSerializer.Deserialize<List<string>>(result.FormattingIssues ?? "[]") ?? new List<string>()
                },
                Recommendation = result.Recommendation,
                Summary = result.Summary ?? string.Empty
            };
        }

        /// <summary>
        /// 发布博客
        /// </summary>
        public async Task<bool> PublishBlogAsync(int taskId)
        {
            var content = await _contentRepository.GetByTaskIdAsync(taskId);
            
            if (content == null)
            {
                _logger.LogWarning($"博客内容不存在, TaskId: {taskId}");
                return false;
            }

            // 更新发布状态
            await _contentRepository.UpdatePublishStatusAsync(content.Id, true);
            
            // 更新任务状态
            await UpdateTaskStatusAsync(taskId, Domain.Enum.AgentTaskStatus.Published, "published");

            _logger.LogInformation($"发布博客成功, TaskId: {taskId}, ContentId: {content.Id}");
            return true;
        }

        /// <summary>
        /// 删除任务
        /// </summary>
        public async Task<bool> DeleteTaskAsync(int taskId)
        {
            // 删除关联的内容和审查结果
            var content = await _contentRepository.GetByTaskIdAsync(taskId);
            if (content != null)
            {
                await _contentRepository.DeleteByIdAsync(content.Id);
            }

            // 删除任务
            var result = await _taskRepository.DeleteByIdAsync(taskId);
            
            if (result)
            {
                _logger.LogInformation($"删除任务成功, TaskId: {taskId}");
            }

            return result;
        }

        /// <summary>
        /// 导出为Markdown文件
        /// </summary>
        public async Task<string> ExportToMarkdownAsync(int taskId, string exportPath)
        {
            var content = await _contentRepository.GetByTaskIdAsync(taskId);
            
            if (content == null)
            {
                throw new Exception($"博客内容不存在, TaskId: {taskId}");
            }

            var fileName = $"{content.Title}_{DateTime.Now:yyyyMMdd_HHmmss}.md";
            var filePath = Path.Combine(exportPath, fileName);

            // 确保目录存在
            Directory.CreateDirectory(exportPath);

            // 写入文件
            await File.WriteAllTextAsync(filePath, content.Content);

            _logger.LogInformation($"导出Markdown成功, TaskId: {taskId}, File: {filePath}");
            return filePath;
        }

        /// <summary>
        /// 更新资料收集摘要
        /// </summary>
        public async Task<bool> UpdateResearchSummaryAsync(int taskId, string summary)
        {
            var content = await _contentRepository.GetByTaskIdAsync(taskId);
            
            if (content == null)
            {
                _logger.LogWarning($"博客内容不存在, TaskId: {taskId}");
                return false;
            }

            content.ResearchSummary = summary;
            content.UpdatedAt = DateTime.Now;
            await _contentRepository.UpdateAsync(content);

            _logger.LogInformation($"更新资料收集摘要成功, TaskId: {taskId}");
            return true;
        }

        /// <summary>
        /// 更新博客内容
        /// </summary>
        public async Task<bool> UpdateBlogContentAsync(int taskId, string title, string content)
        {
            var blogContent = await _contentRepository.GetByTaskIdAsync(taskId);
            
            if (blogContent == null)
            {
                _logger.LogWarning($"博客内容不存在, TaskId: {taskId}");
                return false;
            }

            blogContent.Title = title;
            blogContent.Content = content;
            blogContent.WordCount = content.Length;
            blogContent.UpdatedAt = DateTime.Now;
            await _contentRepository.UpdateAsync(blogContent);

            _logger.LogInformation($"更新博客内容成功, TaskId: {taskId}");
            return true;
        }

        /// <summary>
        /// 更新审查结果
        /// </summary>
        public async Task<bool> UpdateReviewResultAsync(int taskId, int overallScore, string recommendation, string summary)
        {
            var reviewResult = await _reviewResultRepository.GetByTaskIdAsync(taskId);
            
            if (reviewResult == null)
            {
                _logger.LogWarning($"审查结果不存在, TaskId: {taskId}");
                return false;
            }

            reviewResult.OverallScore = overallScore;
            reviewResult.Recommendation = recommendation;
            reviewResult.Summary = summary;
            await _reviewResultRepository.UpdateAsync(reviewResult);

            _logger.LogInformation($"更新审查结果成功, TaskId: {taskId}");
            return true;
        }
    }
}

