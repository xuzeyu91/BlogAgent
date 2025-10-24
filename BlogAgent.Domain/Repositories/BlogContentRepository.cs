using BlogAgent.Domain.Common.Extensions;
using BlogAgent.Domain.Domain.Model;
using BlogAgent.Domain.Repositories.Base;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;

namespace BlogAgent.Domain.Repositories
{
    /// <summary>
    /// 博客内容仓储
    /// </summary>
    [ServiceDescription(typeof(BlogContentRepository), ServiceLifetime.Scoped)]
    public class BlogContentRepository : Repository<BlogContent>
    {
        public BlogContentRepository(ISqlSugarClient db) : base(db)
        {
        }

        /// <summary>
        /// 根据任务ID获取内容
        /// </summary>
        public async Task<BlogContent?> GetByTaskIdAsync(int taskId)
        {
            return await GetDB().Queryable<BlogContent>()
                .Where(c => c.TaskId == taskId)
                .OrderByDescending(c => c.CreatedAt)
                .FirstAsync();
        }

        /// <summary>
        /// 更新发布状态
        /// </summary>
        public async Task<bool> UpdatePublishStatusAsync(int contentId, bool isPublished)
        {
            return await GetDB().Updateable<BlogContent>()
                .SetColumns(c => new BlogContent
                {
                    IsPublished = isPublished,
                    PublishedAt = isPublished ? DateTime.Now : null,
                    UpdatedAt = DateTime.Now
                })
                .Where(c => c.Id == contentId)
                .ExecuteCommandAsync() > 0;
        }

        /// <summary>
        /// 获取已发布的内容列表
        /// </summary>
        public async Task<List<BlogContent>> GetPublishedContentsAsync(int page = 1, int pageSize = 20)
        {
            return await GetDB().Queryable<BlogContent>()
                .Where(c => c.IsPublished)
                .OrderByDescending(c => c.PublishedAt)
                .ToPageListAsync(page, pageSize);
        }
    }
}

