using BlogAgent.Domain.Common.Extensions;
using BlogAgent.Domain.Domain.Model;
using BlogAgent.Domain.Repositories.Base;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using TaskStatus = BlogAgent.Domain.Domain.Enum.AgentTaskStatus;

namespace BlogAgent.Domain.Repositories
{
    /// <summary>
    /// 博客任务仓储
    /// </summary>
    [ServiceDescription(typeof(BlogTaskRepository), ServiceLifetime.Scoped)]
    public class BlogTaskRepository : Repository<BlogTask>
    {
        public BlogTaskRepository(ISqlSugarClient db) : base(db)
        {
        }

        /// <summary>
        /// 根据状态查询任务列表
        /// </summary>
        public async Task<List<BlogTask>> GetByStatusAsync(Domain.Enum.AgentTaskStatus status)
        {
            return await GetDB().Queryable<BlogTask>()
                .Where(t => t.Status == status)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// 获取最近的任务列表
        /// </summary>
        public async Task<List<BlogTask>> GetRecentTasksAsync(int count = 20)
        {
            return await GetDB().Queryable<BlogTask>()
                .OrderByDescending(t => t.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        /// <summary>
        /// 更新任务状态
        /// </summary>
        public async Task<bool> UpdateStatusAsync(int taskId, Domain.Enum.AgentTaskStatus status, string? currentStage = null)
        {
            var updateColumns = new List<string> { nameof(BlogTask.Status), nameof(BlogTask.UpdatedAt) };
            var task = new BlogTask 
            { 
                Id = taskId, 
                Status = status, 
                UpdatedAt = DateTime.Now 
            };

            if (!string.IsNullOrEmpty(currentStage))
            {
                task.CurrentStage = currentStage;
                updateColumns.Add(nameof(BlogTask.CurrentStage));
            }

            return await GetDB().Updateable(task)
                .UpdateColumns(updateColumns.ToArray())
                .ExecuteCommandAsync() > 0;
        }
    }
}

