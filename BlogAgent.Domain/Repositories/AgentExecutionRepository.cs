using BlogAgent.Domain.Common.Extensions;
using BlogAgent.Domain.Domain.Enum;
using BlogAgent.Domain.Domain.Model;
using BlogAgent.Domain.Repositories.Base;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;

namespace BlogAgent.Domain.Repositories
{
    /// <summary>
    /// Agent执行记录仓储
    /// </summary>
    [ServiceDescription(typeof(AgentExecutionRepository), ServiceLifetime.Scoped)]
    public class AgentExecutionRepository : Repository<AgentExecution>
    {
        public AgentExecutionRepository(ISqlSugarClient db) : base(db)
        {
        }

        /// <summary>
        /// 根据任务ID获取执行记录
        /// </summary>
        public async Task<List<AgentExecution>> GetByTaskIdAsync(int taskId)
        {
            return await GetDB().Queryable<AgentExecution>()
                .Where(e => e.TaskId == taskId)
                .OrderBy(e => e.StartTime)
                .ToListAsync();
        }

        /// <summary>
        /// 根据任务ID和Agent类型获取最新的执行记录
        /// </summary>
        public async Task<AgentExecution?> GetLatestByTaskAndAgentAsync(int taskId, AgentType agentType)
        {
            return await GetDB().Queryable<AgentExecution>()
                .Where(e => e.TaskId == taskId && e.AgentType == agentType)
                .OrderByDescending(e => e.StartTime)
                .FirstAsync();
        }

        /// <summary>
        /// 获取执行统计信息
        /// </summary>
        public async Task<Dictionary<AgentType, int>> GetExecutionStatisticsAsync()
        {
            var list = await GetDB().Queryable<AgentExecution>()
                .Where(e => e.Success)
                .GroupBy(e => e.AgentType)
                .Select(g => new { AgentType = g.AgentType, Count = SqlFunc.AggregateCount(g.Id) })
                .ToListAsync();

            return list.ToDictionary(x => x.AgentType, x => x.Count);
        }
    }
}

