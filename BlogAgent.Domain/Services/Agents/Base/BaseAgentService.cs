using System.Diagnostics;
using System.Text.Json;
using System.ClientModel;
using BlogAgent.Domain.Common.Options;
using BlogAgent.Domain.Domain.Enum;
using BlogAgent.Domain.Domain.Model;
using BlogAgent.Domain.Repositories;
using BlogAgent.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

namespace BlogAgent.Domain.Services.Agents.Base
{
    /// <summary>
    /// Agent服务基类 - 使用 Microsoft Agent Framework
    /// </summary>
    public abstract class BaseAgentService : IAgentService
    {
        protected readonly ILogger _logger;
        protected readonly AgentExecutionRepository _executionRepository;
        protected ChatClientAgent? _agent;
        protected AgentThread? _currentThread;

        /// <summary>
        /// Agent名称
        /// </summary>
        public abstract string AgentName { get; }

        /// <summary>
        /// Agent类型
        /// </summary>
        public abstract AgentType AgentType { get; }

        /// <summary>
        /// Agent指令(System Prompt)
        /// </summary>
        protected abstract string Instructions { get; }

        /// <summary>
        /// 最大Token数
        /// </summary>
        protected virtual int MaxTokens => 4000;

        /// <summary>
        /// 温度参数
        /// </summary>
        protected virtual float Temperature => 0.7f;

        /// <summary>
        /// Agent的工具函数列表
        /// </summary>
        protected virtual IEnumerable<AITool>? Tools => null;

        /// <summary>
        /// 响应格式(用于结构化输出)
        /// </summary>
        protected virtual ChatResponseFormat? ResponseFormat => null;

        /// <summary>
        /// 是否启用MCP工具(默认启用)
        /// </summary>
        protected virtual bool EnableMcpTools => true;

        // MCP配置服务 (可选依赖)
        protected McpConfigService? _mcpConfigService;

        public BaseAgentService(
            ILogger logger,
            AgentExecutionRepository executionRepository,
            McpConfigService? mcpConfigService = null)
        {
            _logger = logger;
            _executionRepository = executionRepository;
            _mcpConfigService = mcpConfigService;

            _logger.LogInformation($"初始化 Agent 服务: {AgentName}");
        }

        /// <summary>
        /// 获取或创建 AIAgent 实例
        /// </summary>
        public virtual async Task<ChatClientAgent> GetAgentAsync()
        {
            if (_agent == null)
            {
                var endpoint = OpenAIOption.EndPoint.TrimEnd('/');
                _logger.LogInformation($"[{AgentName}] 创建 Agent, Endpoint: {endpoint}, Model: {OpenAIOption.ChatModel}");

                // 合并工具列表 (异步获取)
                var allTools = await GetAllToolsAsync().ConfigureAwait(false);

                var options = new ChatClientAgentOptions(instructions: Instructions)
                {
                    Name = AgentName,
                    ChatOptions = new ChatOptions
                    {
                        MaxOutputTokens = MaxTokens,
                        Temperature = Temperature,
                        ResponseFormat = ResponseFormat,
                        Tools = allTools
                    }
                };

                // 使用 CreateAIAgent 扩展方法创建 Agent
                var chatClient = new OpenAIClient(
                    new ApiKeyCredential(OpenAIOption.Key),
                    new OpenAIClientOptions
                    {
                        Endpoint = new Uri(endpoint)
                    })
                    .GetChatClient(OpenAIOption.ChatModel);

                _agent = chatClient.CreateAIAgent(options);
            }

            return _agent;
        }

        /// <summary>
        /// 获取所有工具(包括Agent自定义工具和MCP工具)
        /// </summary>
        protected virtual async Task<List<AITool>?> GetAllToolsAsync()
        {
            var toolsList = new List<AITool>();

            // 添加Agent自定义的工具
            if (Tools != null)
            {
                toolsList.AddRange(Tools);
            }

            // 如果启用MCP工具,添加MCP工具
            if (EnableMcpTools && _mcpConfigService != null)
            {
                try
                {
                    // 添加整体超时保护 - 15秒 (使用异步超时)
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    var mcpTools = await _mcpConfigService.GetAllEnabledToolsAsync()
                        .WaitAsync(cts.Token)
                        .ConfigureAwait(false);
                        
                    if (mcpTools != null && mcpTools.Count > 0)
                    {
                        toolsList.AddRange(mcpTools);
                        _logger.LogInformation($"[{AgentName}] 加载了 {mcpTools.Count} 个 MCP 工具");
                    }
                    else
                    {
                        _logger.LogInformation($"[{AgentName}] 没有可用的 MCP 工具");
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning($"[{AgentName}] 加载 MCP 工具超时(15秒),跳过 MCP 工具");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"[{AgentName}] 加载 MCP 工具失败: {ex.Message}");
                }
            }

            return toolsList.Count > 0 ? toolsList : null;
        }

        /// <summary>
        /// 获取新的会话线程
        /// </summary>
        protected virtual async Task<AgentThread> GetNewThreadAsync()
        {
            var agent = await GetAgentAsync().ConfigureAwait(false);
            _currentThread = agent.GetNewThread();
            return _currentThread;
        }

        /// <summary>
        /// 执行Agent任务
        /// </summary>
        public virtual async Task<string> ExecuteAsync(string input, int taskId)
        {
            var stopwatch = Stopwatch.StartNew();
            var execution = new AgentExecution
            {
                TaskId = taskId,
                AgentType = AgentType,
                Input = input,
                StartTime = DateTime.Now
            };

            try
            {
                _logger.LogInformation($"[{AgentName}] 开始执行任务, TaskId: {taskId}");
                _logger.LogInformation($"[{AgentName}] 使用模型: {OpenAIOption.ChatModel}, 端点: {OpenAIOption.EndPoint}");

                var agent = await GetAgentAsync().ConfigureAwait(false);
                var thread = await GetNewThreadAsync().ConfigureAwait(false);

                // 使用 Agent Framework 执行
                var result = await agent.RunAsync(input, thread);

                var output = result.Text;
                
                stopwatch.Stop();

                // 记录成功执行
                execution.Success = true;
                execution.Output = output;
                execution.TokensUsed = EstimateTokens(input, output); // Agent Framework 可能不直接返回 token 数
                execution.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
                execution.EndTime = DateTime.Now;

                _logger.LogInformation(
                    $"[{AgentName}] 执行成功, TaskId: {taskId}, 耗时: {execution.ExecutionTimeMs}ms");

                return output;
            }
            catch (ClientResultException ex)
            {
                stopwatch.Stop();

                // 记录失败执行
                execution.Success = false;
                execution.ErrorMessage = ex.Message;
                execution.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
                execution.EndTime = DateTime.Now;

                // 尝试读取响应内容
                _logger.LogError($"[{AgentName}] ClientResultException - Status: {ex.Status}");
                _logger.LogError($"[{AgentName}] 错误消息: {ex.Message}");
                
                throw;
            }
            catch (System.Text.Json.JsonException ex)
            {
                stopwatch.Stop();

                execution.Success = false;
                execution.ErrorMessage = ex.Message;
                execution.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
                execution.EndTime = DateTime.Now;

                _logger.LogError(ex, $"[{AgentName}] JSON 解析错误 - API 可能返回了非 JSON 响应(如 HTML 错误页面)");
                _logger.LogError($"[{AgentName}] 请检查: 1) API Key 是否正确 2) Endpoint 是否正确 3) 模型名称是否被代理服务器支持");
                
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // 记录失败执行
                execution.Success = false;
                execution.ErrorMessage = ex.Message;
                execution.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
                execution.EndTime = DateTime.Now;

                // 记录更详细的错误信息
                _logger.LogError(ex, $"[{AgentName}] 执行失败, TaskId: {taskId}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"[{AgentName}] 内部异常: {ex.InnerException.Message}");
                }

                throw;
            }
            finally
            {
                // 保存执行记录
                try
                {
                    await _executionRepository.InsertAsync(execution);
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "保存执行记录失败");
                }
            }
        }

        /// <summary>
        /// 执行Agent任务并支持流式输出
        /// </summary>
        public virtual async IAsyncEnumerable<string> ExecuteStreamingAsync(string input, int taskId)
        {
            var agent = await GetAgentAsync().ConfigureAwait(false);
            var thread = await GetNewThreadAsync().ConfigureAwait(false);

            _logger.LogInformation($"[{AgentName}] 开始流式执行任务, TaskId: {taskId}");

            await foreach (var update in agent.RunStreamingAsync(input, thread))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    yield return update.Text;
                }
            }
        }

        /// <summary>
        /// 估算Token数量(简单估算)
        /// </summary>
        private int EstimateTokens(string input, string output)
        {
            // 简单估算: 1个中文字符约1.5个token, 1个英文单词约1个token
            var totalChars = input.Length + output.Length;
            return (int)(totalChars * 0.4); // 粗略估算
        }

        /// <summary>
        /// 从Markdown内容中提取标题
        /// </summary>
        protected string ExtractTitle(string markdown)
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

        /// <summary>
        /// 统计中文字数(排除Markdown标记)
        /// </summary>
        protected int CountWords(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return 0;

            // 简单的字数统计:移除常见Markdown标记后计算字符数
            var cleanContent = content
                .Replace("```", "")
                .Replace("#", "")
                .Replace("*", "")
                .Replace("-", "")
                .Replace(">", "")
                .Replace("|", "")
                .Trim();

            // 统计中文字符和英文单词
            int chineseChars = cleanContent.Count(c => c >= 0x4E00 && c <= 0x9FA5);
            int englishWords = cleanContent.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(word => word.Any(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')));

            return chineseChars + englishWords;
        }

        /// <summary>
        /// 从文本中提取关键点列表
        /// </summary>
        protected List<string> ExtractKeyPoints(string content)
        {
            var keyPoints = new List<string>();
            var lines = content.Split('\n');
            bool inKeyPointsSection = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // 检测核心要点section
                if (trimmed.Contains("核心要点") || trimmed.Contains("关键点"))
                {
                    inKeyPointsSection = true;
                    continue;
                }

                // 遇到下一个section标题,停止提取
                if (inKeyPointsSection && trimmed.StartsWith("##"))
                {
                    break;
                }

                // 提取列表项
                if (inKeyPointsSection)
                {
                    if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                    {
                        keyPoints.Add(trimmed.Substring(2).Trim());
                    }
                    else if (char.IsDigit(trimmed.FirstOrDefault()) && trimmed.Contains("."))
                    {
                        var index = trimmed.IndexOf('.');
                        if (index > 0 && index < trimmed.Length - 1)
                        {
                            keyPoints.Add(trimmed.Substring(index + 1).Trim());
                        }
                    }
                }
            }

            return keyPoints;
        }

        /// <summary>
        /// 解析JSON响应
        /// </summary>
        protected T? ParseJsonResponse<T>(string jsonContent) where T : class
        {
            try
            {
                // 尝试直接解析
                return JsonSerializer.Deserialize<T>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                // 如果失败,尝试提取JSON代码块
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

