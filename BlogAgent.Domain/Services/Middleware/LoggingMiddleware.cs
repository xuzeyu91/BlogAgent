using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace BlogAgent.Domain.Services.Middleware
{
    /// <summary>
    /// 日志中间件 - 记录 Agent 的输入输出和执行时间
    /// </summary>
    public static class LoggingMiddleware
    {
        /// <summary>
        /// 日志中间件 - 记录 Agent 运行的详细信息
        /// </summary>
        public static async Task<AgentRunResponse> LogExecutionAsync(
            IEnumerable<ChatMessage> messages,
            AgentThread? thread,
            AgentRunOptions? options,
            AIAgent innerAgent,
            CancellationToken cancellationToken,
            ILogger? logger = null,
            string? agentName = null)
        {
            var stopwatch = Stopwatch.StartNew();
            agentName ??= innerAgent.Id ?? "Agent";

            try
            {
                // 记录输入
                var inputText = string.Join("\n", messages.Select(m => $"[{m.Role}]: {m.Text?.Substring(0, Math.Min(100, m.Text?.Length ?? 0))}..."));
                logger?.LogInformation("[LoggingMiddleware] {AgentName} 开始执行", agentName);
                logger?.LogDebug("[LoggingMiddleware] {AgentName} 输入:\n{Input}", agentName, inputText);

                // 执行 Agent
                var response = await innerAgent.RunAsync(messages, thread, options, cancellationToken).ConfigureAwait(false);

                stopwatch.Stop();

                // 记录输出
                var outputText = response.Text ?? string.Empty;
                var outputPreview = outputText.Length > 200
                    ? outputText.Substring(0, 200) + "..."
                    : outputText;

                logger?.LogInformation("[LoggingMiddleware] {AgentName} 执行完成 - 耗时: {ElapsedMs}ms, 输出长度: {OutputLength}字符",
                    agentName, stopwatch.ElapsedMilliseconds, outputText.Length);
                logger?.LogDebug("[LoggingMiddleware] {AgentName} 输出:\n{Output}", agentName, outputPreview);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger?.LogError(ex, "[LoggingMiddleware] {AgentName} 执行失败 - 耗时: {ElapsedMs}ms, 错误: {Error}",
                    agentName, stopwatch.ElapsedMilliseconds, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 详细的日志中间件 - 记录更多详细信息
        /// </summary>
        public static async Task<AgentRunResponse> LogDetailedAsync(
            IEnumerable<ChatMessage> messages,
            AgentThread? thread,
            AgentRunOptions? options,
            AIAgent innerAgent,
            CancellationToken cancellationToken,
            ILogger? logger = null,
            string? agentName = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var executionId = Guid.NewGuid().ToString("N")[..8];
            agentName ??= innerAgent.Id ?? "Agent";

            try
            {
                logger?.LogInformation("[LoggingMiddleware:{ExecutionId}] === {AgentName} 执行开始 ===", executionId, agentName);

                // 记录详细输入信息
                foreach (var message in messages)
                {
                    logger?.LogDebug("[LoggingMiddleware:{ExecutionId}] [{Role}] {Text}",
                        executionId, message.Role, message.Text?.Substring(0, Math.Min(500, message.Text?.Length ?? 0)));
                }

                // 记录工具信息
                if (options is ChatClientAgentRunOptions chatClientOptions && chatClientOptions.ChatOptions?.Tools != null)
                {
                    logger?.LogDebug("[LoggingMiddleware:{ExecutionId}] 可用工具数量: {ToolCount}",
                        executionId, chatClientOptions.ChatOptions.Tools.Count());
                }

                // 执行 Agent
                var response = await innerAgent.RunAsync(messages, thread, options, cancellationToken).ConfigureAwait(false);

                stopwatch.Stop();

                // 记录详细输出信息
                logger?.LogInformation("[LoggingMiddleware:{ExecutionId}] === {AgentName} 执行成功 ===", executionId, agentName);
                logger?.LogInformation("[LoggingMiddleware:{ExecutionId}] 耗时: {ElapsedMs}ms, 消息数: {MessageCount}",
                    executionId, stopwatch.ElapsedMilliseconds, response.Messages.Count());

                foreach (var message in response.Messages)
                {
                    var preview = message.Text?.Length > 500
                        ? message.Text.Substring(0, 500) + "..."
                        : message.Text;

                    logger?.LogDebug("[LoggingMiddleware:{ExecutionId}] [{Role}] {Text}",
                        executionId, message.Role, preview);
                }

                // 记录函数调用
                if (response.UserInputRequests.Any())
                {
                    logger?.LogDebug("[LoggingMiddleware:{ExecutionId}] 函数调用请求: {RequestCount}",
                        executionId, response.UserInputRequests.Count());
                }

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger?.LogError(ex, "[LoggingMiddleware:{ExecutionId}] === {AgentName} 执行失败 === 耗时: {ElapsedMs}ms",
                    executionId, agentName, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        /// <summary>
        /// 性能监控中间件 - 记录性能指标
        /// </summary>
        public static async Task<AgentRunResponse> MonitorPerformanceAsync(
            IEnumerable<ChatMessage> messages,
            AgentThread? thread,
            AgentRunOptions? options,
            AIAgent innerAgent,
            CancellationToken cancellationToken,
            ILogger? logger = null,
            string? agentName = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var inputTokens = EstimateTokens(messages);
            agentName ??= innerAgent.Id ?? "Agent";

            try
            {
                var response = await innerAgent.RunAsync(messages, thread, options, cancellationToken).ConfigureAwait(false);

                stopwatch.Stop();
                var outputTokens = EstimateTokens(response.Messages);

                // 计算性能指标
                var totalTokens = inputTokens + outputTokens;
                var tokensPerSecond = totalTokens > 0 ? (totalTokens / (stopwatch.ElapsedMilliseconds / 1000.0)) : 0;

                logger?.LogInformation(
                    "[LoggingMiddleware] {AgentName} 性能指标 - 耗时: {ElapsedMs}ms, 输入Token: {InputTokens}, 输出Token: {OutputTokens}, 总Token: {TotalTokens}, 速度: {TokensPerSecond:F1} tokens/s",
                    agentName, stopwatch.ElapsedMilliseconds, inputTokens, outputTokens, totalTokens, tokensPerSecond);

                // 性能警告
                if (stopwatch.ElapsedMilliseconds > 30000)
                {
                    logger?.LogWarning("[LoggingMiddleware] {AgentName} 执行时间超过30秒: {ElapsedMs}ms",
                        agentName, stopwatch.ElapsedMilliseconds);
                }

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger?.LogError(ex, "[LoggingMiddleware] {AgentName} 性能监控 - 执行失败, 耗时: {ElapsedMs}ms",
                    agentName, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        /// <summary>
        /// 简单估算 Token 数量
        /// </summary>
        private static int EstimateTokens(IEnumerable<ChatMessage> messages)
        {
            return messages.Sum(m => EstimateTokens(m.Text ?? string.Empty));
        }

        /// <summary>
        /// 简单估算文本的 Token 数量
        /// </summary>
        private static int EstimateTokens(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            // 简单估算: 1个中文字符约1.5个token, 1个英文单词约1个token
            var totalChars = text.Length;
            return (int)(totalChars * 0.4);
        }
    }
}
