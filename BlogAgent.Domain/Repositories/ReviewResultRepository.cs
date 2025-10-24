using BlogAgent.Domain.Common.Extensions;
using BlogAgent.Domain.Domain.Model;
using BlogAgent.Domain.Repositories.Base;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;

namespace BlogAgent.Domain.Repositories
{
    /// <summary>
    /// 审查结果仓储
    /// </summary>
    [ServiceDescription(typeof(ReviewResultRepository), ServiceLifetime.Scoped)]
    public class ReviewResultRepository : Repository<ReviewResult>
    {
        public ReviewResultRepository(ISqlSugarClient db) : base(db)
        {
        }

        /// <summary>
        /// 根据任务ID获取审查结果
        /// </summary>
        public async Task<ReviewResult?> GetByTaskIdAsync(int taskId)
        {
            return await GetDB().Queryable<ReviewResult>()
                .Where(r => r.TaskId == taskId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstAsync();
        }

        /// <summary>
        /// 根据内容ID获取审查结果
        /// </summary>
        public async Task<ReviewResult?> GetByContentIdAsync(int contentId)
        {
            return await GetDB().Queryable<ReviewResult>()
                .Where(r => r.ContentId == contentId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstAsync();
        }

        /// <summary>
        /// 获取平均评分统计
        /// </summary>
        public async Task<Dictionary<string, double>> GetAverageScoresAsync()
        {
            var result = await GetDB().Queryable<ReviewResult>()
                .Select(r => new
                {
                    r.OverallScore,
                    r.AccuracyScore,
                    r.LogicScore,
                    r.OriginalityScore,
                    r.FormattingScore
                })
                .ToListAsync();

            if (!result.Any())
                return new Dictionary<string, double>();

            return new Dictionary<string, double>
            {
                ["Overall"] = result.Average(r => r.OverallScore),
                ["Accuracy"] = result.Average(r => r.AccuracyScore),
                ["Logic"] = result.Average(r => r.LogicScore),
                ["Originality"] = result.Average(r => r.OriginalityScore),
                ["Formatting"] = result.Average(r => r.FormattingScore)
            };
        }

        /// <summary>
        /// 获取通过率统计
        /// </summary>
        public async Task<double> GetPassRateAsync()
        {
            var total = await GetDB().Queryable<ReviewResult>().CountAsync();
            if (total == 0) return 0;

            var passedCount = await GetDB().Queryable<ReviewResult>()
                .Where(r => r.OverallScore >= 80)
                .CountAsync();

            return (double)passedCount / total * 100;
        }
    }
}

