using BlogAgent.Domain.Common.Constants;
using BlogAgent.Domain.Services.Workflows.Messages;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace BlogAgent.Domain.Services.Workflows.Executors
{
    /// <summary>
    /// 发布执行器 - 负责发布博客
    /// </summary>
    public class PublishExecutor : Executor<ReviewResultOutput, string>
    {
        private readonly BlogService _blogService;
        private readonly ILogger<PublishExecutor> _logger;

        public PublishExecutor(
            BlogService blogService,
            ILogger<PublishExecutor> logger)
            : base("PublishExecutor")
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
            _logger.LogInformation($"[PublishExecutor] 开始发布博客, TaskId: {taskId}, 评分: {reviewResult.OverallScore}");

            try
            {
                // 获取草稿内容
                var draftContent = await context.ReadStateAsync<DraftContentOutput>(
                    BlogStateConstants.DraftContentKey,
                    BlogStateConstants.BlogStateScope,
                    cancellationToken);

                if (draftContent == null)
                {
                    throw new InvalidOperationException("草稿内容不存在");
                }

                // 发布博客
                await _blogService.PublishBlogAsync(taskId);

                var publishMessage = $"博客已成功发布！\n" +
                    $"标题: {draftContent.Title}\n" +
                    $"字数: {draftContent.WordCount}\n" +
                    $"综合评分: {reviewResult.OverallScore}/100\n" +
                    $"发布建议: {reviewResult.Recommendation}";

                _logger.LogInformation($"[PublishExecutor] 博客发布成功, TaskId: {taskId}");

                return publishMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[PublishExecutor] 博客发布失败, TaskId: {taskId}");
                throw;
            }
        }
    }
}
