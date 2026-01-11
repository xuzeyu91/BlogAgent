using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace BlogAgent.Domain.Services.Middleware
{
    /// <summary>
    /// Agent 中间件辅助类
    /// 提供创建中间件委托的便捷方法
    ///
    /// 使用方式示例：
    /// var agentWithMiddleware = originalAgent
    ///     .AsBuilder()
    ///     .Use(AgentMiddlewareHelpers.PIIMiddleware(logger))
    ///     .Use(AgentMiddlewareHelpers.GuardrailMiddleware(logger))
    ///     .Build();
    /// </summary>
    public static class AgentMiddlewareHelpers
    {
        /// <summary>
        /// 创建 PII 过滤中间件委托
        /// </summary>
        public static Func<IEnumerable<ChatMessage>, AgentThread?, AgentRunOptions?, AIAgent, CancellationToken, Task<AgentRunResponse>> CreatePIIFilter(ILogger? logger = null)
        {
            return async (messages, thread, options, innerAgent, cancellationToken) =>
            {
                return await PIIMiddleware.FilterPIIAsync(messages, thread, options, innerAgent, cancellationToken, logger);
            };
        }

        /// <summary>
        /// 创建内容安全检查中间件委托
        /// </summary>
        public static Func<IEnumerable<ChatMessage>, AgentThread?, AgentRunOptions?, AIAgent, CancellationToken, Task<AgentRunResponse>> CreateGuardrail(
            IEnumerable<string>? forbiddenKeywords = null,
            ILogger? logger = null)
        {
            return async (messages, thread, options, innerAgent, cancellationToken) =>
            {
                return await GuardrailMiddleware.GuardrailAsync(messages, thread, options, innerAgent, cancellationToken, logger, forbiddenKeywords);
            };
        }

        /// <summary>
        /// 创建日志记录中间件委托
        /// </summary>
        public static Func<IEnumerable<ChatMessage>, AgentThread?, AgentRunOptions?, AIAgent, CancellationToken, Task<AgentRunResponse>> CreateLogging(
            ILogger? logger = null,
            string? agentName = null)
        {
            return async (messages, thread, options, innerAgent, cancellationToken) =>
            {
                return await LoggingMiddleware.LogExecutionAsync(messages, thread, options, innerAgent, cancellationToken, logger, agentName);
            };
        }

        /// <summary>
        /// 创建详细日志记录中间件委托
        /// </summary>
        public static Func<IEnumerable<ChatMessage>, AgentThread?, AgentRunOptions?, AIAgent, CancellationToken, Task<AgentRunResponse>> CreateDetailedLogging(
            ILogger? logger = null,
            string? agentName = null)
        {
            return async (messages, thread, options, innerAgent, cancellationToken) =>
            {
                return await LoggingMiddleware.LogDetailedAsync(messages, thread, options, innerAgent, cancellationToken, logger, agentName);
            };
        }

        /// <summary>
        /// 创建性能监控中间件委托
        /// </summary>
        public static Func<IEnumerable<ChatMessage>, AgentThread?, AgentRunOptions?, AIAgent, CancellationToken, Task<AgentRunResponse>> CreatePerformanceMonitoring(
            ILogger? logger = null,
            string? agentName = null)
        {
            return async (messages, thread, options, innerAgent, cancellationToken) =>
            {
                return await LoggingMiddleware.MonitorPerformanceAsync(messages, thread, options, innerAgent, cancellationToken, logger, agentName);
            };
        }

        /// <summary>
        /// 直接获取 PII 中间件方法（用于直接传递给 Use()）
        /// </summary>
        public static Func<IEnumerable<ChatMessage>, AgentThread?, AgentRunOptions?, AIAgent, CancellationToken, Task<AgentRunResponse>> CreatePIIMiddlewareFunc(ILogger? logger = null)
        {
            return async (messages, thread, options, innerAgent, cancellationToken) =>
            {
                return await PIIMiddleware.FilterPIIAsync(messages, thread, options, innerAgent, cancellationToken, logger);
            };
        }

        /// <summary>
        /// 直接获取安全检查中间件方法（用于直接传递给 Use()）
        /// </summary>
        public static Func<IEnumerable<ChatMessage>, AgentThread?, AgentRunOptions?, AIAgent, CancellationToken, Task<AgentRunResponse>> CreateGuardrailMiddlewareFunc(
            IEnumerable<string>? forbiddenKeywords = null,
            ILogger? logger = null)
        {
            return async (messages, thread, options, innerAgent, cancellationToken) =>
            {
                return await GuardrailMiddleware.GuardrailAsync(messages, thread, options, innerAgent, cancellationToken, logger, forbiddenKeywords);
            };
        }

        /// <summary>
        /// 直接获取日志中间件方法（用于直接传递给 Use()）
        /// </summary>
        public static Func<IEnumerable<ChatMessage>, AgentThread?, AgentRunOptions?, AIAgent, CancellationToken, Task<AgentRunResponse>> CreateLoggingMiddlewareFunc(
            ILogger? logger = null,
            string? agentName = null)
        {
            return async (messages, thread, options, innerAgent, cancellationToken) =>
            {
                return await LoggingMiddleware.LogExecutionAsync(messages, thread, options, innerAgent, cancellationToken, logger, agentName);
            };
        }
    }
}
