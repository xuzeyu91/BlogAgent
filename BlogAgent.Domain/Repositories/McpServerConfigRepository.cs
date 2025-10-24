using BlogAgent.Domain.Common.Extensions;
using BlogAgent.Domain.Domain.Model;
using BlogAgent.Domain.Repositories.Base;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlogAgent.Domain.Repositories
{
    /// <summary>
    /// MCP服务器配置仓储
    /// </summary>
    [ServiceDescription(typeof(McpServerConfigRepository), ServiceLifetime.Scoped)]
    public class McpServerConfigRepository : Repository<McpServerConfig>
    {
        public McpServerConfigRepository(ISqlSugarClient db) : base(db)
        {
        }

        /// <summary>
        /// 获取所有启用的MCP服务器配置
        /// </summary>
        public async Task<List<McpServerConfig>> GetEnabledConfigsAsync()
        {
            return await GetDB().Queryable<McpServerConfig>()
                .Where(c => c.IsEnabled)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        /// <summary>
        /// 根据名称查询配置
        /// </summary>
        public async Task<McpServerConfig?> GetByNameAsync(string name)
        {
            return await GetDB().Queryable<McpServerConfig>()
                .Where(c => c.Name == name)
                .FirstAsync();
        }

        /// <summary>
        /// 检查名称是否已存在
        /// </summary>
        public async Task<bool> ExistsNameAsync(string name, int? excludeId = null)
        {
            var query = GetDB().Queryable<McpServerConfig>()
                .Where(c => c.Name == name);

            if (excludeId.HasValue)
            {
                query = query.Where(c => c.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        /// <summary>
        /// 切换启用状态
        /// </summary>
        public async Task<bool> ToggleEnabledAsync(int id)
        {
            var config = await GetByIdAsync(id);
            if (config == null) return false;

            config.IsEnabled = !config.IsEnabled;
            config.UpdatedAt = System.DateTime.Now;

            return await GetDB().Updateable(config)
                .UpdateColumns(c => new { c.IsEnabled, c.UpdatedAt })
                .ExecuteCommandAsync() > 0;
        }
    }
}
