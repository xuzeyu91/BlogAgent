using BlogAgent.Domain.Common.Extensions;
using BlogAgent.Domain.Domain.Dto;
using BlogAgent.Domain.Domain.Model;
using BlogAgent.Domain.Repositories;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using System.Text.Json;

namespace BlogAgent.Domain.Services
{
    /// <summary>
    /// MCP配置服务 - 使用 ModelContextProtocol SDK
    /// </summary>
    [ServiceDescription(typeof(McpConfigService), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped)]
    public class McpConfigService : IAsyncDisposable
    {
        private readonly McpServerConfigRepository _repository;
        private readonly ILogger<McpConfigService> _logger;
        private readonly IHttpClientFactory? _httpClientFactory;

        // 缓存已启动的MCP客户端
        private static readonly Dictionary<int, McpClient> _mcpClients = new();
        private static readonly object _lockObj = new();

        public McpConfigService(
            McpServerConfigRepository repository,
            ILogger<McpConfigService> logger,
            IHttpClientFactory? httpClientFactory = null)
        {
            _repository = repository;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        #region 配置管理

        public async Task<List<McpServerConfigDto>> GetAllConfigsAsync()
        {
            var configs = await _repository.GetListAsync();
            return configs.Select(MapToDto).ToList();
        }

        public async Task<List<McpServerConfigDto>> GetEnabledConfigsAsync()
        {
            var configs = await _repository.GetEnabledConfigsAsync();
            return configs.Select(MapToDto).ToList();
        }

        public async Task<McpServerConfigDto?> GetConfigByIdAsync(int id)
        {
            var config = await _repository.GetByIdAsync(id);
            return config == null ? null : MapToDto(config);
        }

        public async Task<int> SaveConfigAsync(SaveMcpServerConfigRequest request)
        {
            if (await _repository.ExistsNameAsync(request.Name, request.Id))
            {
                throw new InvalidOperationException($"配置名称 '{request.Name}' 已存在");
            }

            McpServerConfig config;
            if (request.Id.HasValue && request.Id.Value > 0)
            {
                config = await _repository.GetByIdAsync(request.Id.Value)
                    ?? throw new InvalidOperationException($"配置 ID {request.Id} 不存在");

                UpdateConfigFromRequest(config, request);
                config.UpdatedAt = DateTime.Now;
                await _repository.UpdateAsync(config);

                // 如果客户端已启动,需要重启
                await StopMcpClientAsync(config.Id);
            }
            else
            {
                config = new McpServerConfig();
                UpdateConfigFromRequest(config, request);
                config.CreatedAt = DateTime.Now;
                config.UpdatedAt = DateTime.Now;
                config.Id = await _repository.InsertReturnIdentityAsync(config);
            }

            _logger.LogInformation($"MCP配置已保存: {config.Name} (ID: {config.Id})");
            return config.Id;
        }

        public async Task<bool> DeleteConfigAsync(int id)
        {
            await StopMcpClientAsync(id);
            var result = await _repository.DeleteAsync(id);
            if (result) _logger.LogInformation($"MCP配置已删除: ID {id}");
            return result;
        }

        public async Task<bool> ToggleEnabledAsync(int id)
        {
            var result = await _repository.ToggleEnabledAsync(id);
            if (result)
            {
                var config = await _repository.GetByIdAsync(id);
                if (config != null && !config.IsEnabled)
                {
                    await StopMcpClientAsync(id);
                    _logger.LogInformation($"MCP 配置已禁用: {config.Name} (ID: {id})");
                }
            }

            return result;
        }

        #endregion

        #region MCP 客户端操作

        /// <summary>
        /// 测试 MCP 服务器连接并获取可用工具
        /// </summary>
        public async Task<McpServerTestResult> TestServerAsync(int configId)
        {
            try
            {
                var config = await _repository.GetByIdAsync(configId);
                if (config == null)
                {
                    return new McpServerTestResult
                    {
                        Success = false,
                        Message = "配置不存在",
                        Error = $"ID {configId} 的配置不存在"
                    };
                }

                _logger.LogInformation($"开始测试 MCP 服务器: {config.Name}");

                // 创建临时客户端进行测试
                await using var mcpClient = await CreateMcpClientAsync(config);

                // 获取工具列表
                var mcpTools = await mcpClient.ListToolsAsync().ConfigureAwait(false);

                var toolInfos = mcpTools.Select(tool => new McpToolInfo
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    ServerName = config.Name,
                    ServerId = config.Id
                }).ToList();

                _logger.LogInformation($"MCP 服务器 {config.Name} 测试成功, 发现 {toolInfos.Count} 个工具");

                return new McpServerTestResult
                {
                    Success = true,
                    Message = $"连接成功! 发现 {toolInfos.Count} 个可用工具",
                    Tools = toolInfos
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"测试 MCP 服务器失败: {ex.Message}");
                return new McpServerTestResult
                {
                    Success = false,
                    Message = "连接失败",
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// 获取所有启用的MCP服务器的工具列表
        /// </summary>
        public async Task<List<AITool>> GetAllEnabledToolsAsync()
        {
            var configs = await _repository.GetEnabledConfigsAsync();
            var allTools = new List<AITool>();

            foreach (var config in configs)
            {
                try
                {
                    _logger.LogInformation($"尝试从 MCP 服务器 {config.Name} (ID: {config.Id}) 获取工具");
                    
                    // 添加超时保护 - 10秒超时
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var tools = await GetToolsFromServerAsync(config, cts.Token);
                    
                    allTools.AddRange(tools);
                    _logger.LogInformation($"从 MCP 服务器 {config.Name} 获取到 {tools.Count} 个工具");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning($"获取 MCP 服务器 {config.Name} 的工具超时(10秒),跳过");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"获取 MCP 服务器 {config.Name} 的工具失败: {ex.Message}");
                }
            }

            _logger.LogInformation($"从 {configs.Count} 个 MCP 服务器共获取到 {allTools.Count} 个工具");
            return allTools;
        }

        /// <summary>
        /// 从指定MCP服务器获取工具
        /// </summary>
        private async Task<List<AITool>> GetToolsFromServerAsync(McpServerConfig config, CancellationToken cancellationToken = default)
        {
            var client = await GetOrCreateMcpClientAsync(config, cancellationToken);
            
            // ValueTask<T> 需要先转换为 Task<T> 再使用 WaitAsync
            var listToolsTask = client.ListToolsAsync().AsTask();
            var mcpTools = await listToolsTask.WaitAsync(cancellationToken);
            
            return mcpTools.Cast<AITool>().ToList();
        }

        /// <summary>
        /// 获取或创建MCP客户端(带缓存)
        /// </summary>
        private async Task<McpClient> GetOrCreateMcpClientAsync(McpServerConfig config, CancellationToken cancellationToken = default)
        {
            lock (_lockObj)
            {
                if (_mcpClients.TryGetValue(config.Id, out var existingClient))
                {
                    return existingClient;
                }
            }

            var client = await CreateMcpClientAsync(config, cancellationToken);

            lock (_lockObj)
            {
                if (!_mcpClients.ContainsKey(config.Id))
                {
                    _mcpClients[config.Id] = client;
                }
            }

            return client;
        }

        /// <summary>
        /// 创建MCP客户端
        /// </summary>
        private async Task<McpClient> CreateMcpClientAsync(McpServerConfig config, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"创建 MCP 客户端: {config.Name}, 类型: {config.TransportType}");

            IClientTransport transport;

            if (config.TransportType.Equals("stdio", StringComparison.OrdinalIgnoreCase))
            {
                // Stdio 传输
                if (string.IsNullOrWhiteSpace(config.Command))
                {
                    throw new InvalidOperationException("Stdio 模式需要指定命令");
                }

                var arguments = string.IsNullOrWhiteSpace(config.Arguments)
                    ? Array.Empty<string>()
                    : JsonSerializer.Deserialize<string[]>(config.Arguments) ?? Array.Empty<string>();

                var envVars = string.IsNullOrWhiteSpace(config.EnvironmentVariables)
                    ? null
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(config.EnvironmentVariables);

                var options = new StdioClientTransportOptions
                {
                    Name = config.Name,
                    Command = config.Command,
                    Arguments = arguments.ToList()
                };

                if (envVars != null && envVars.Count > 0)
                {
                    options.EnvironmentVariables = envVars;
                }

                transport = new StdioClientTransport(options);
            }
            else if (config.TransportType.Equals("http", StringComparison.OrdinalIgnoreCase))
            {
                // HTTP 传输
                if (string.IsNullOrWhiteSpace(config.ServerUrl))
                {
                    throw new InvalidOperationException("HTTP 模式需要指定服务器 URL");
                }

                var httpClient = _httpClientFactory?.CreateClient() ?? new HttpClient();

                var options = new HttpClientTransportOptions
                {
                    Endpoint = new Uri(config.ServerUrl),
                    Name = config.Name
                };

                // 如果需要 OAuth 认证
                if (config.RequiresAuth && !string.IsNullOrWhiteSpace(config.OAuthClientId))
                {
                    options.OAuth = new()
                    {
                        ClientId = config.OAuthClientId,
                        RedirectUri = string.IsNullOrWhiteSpace(config.OAuthRedirectUri)
                            ? null
                            : new Uri(config.OAuthRedirectUri)
                    };
                }

                transport = new HttpClientTransport(options, httpClient);
            }
            else
            {
                throw new InvalidOperationException($"不支持的传输类型: {config.TransportType}");
            }

            // 使用带超时的创建方式
            return await McpClient.CreateAsync(transport).WaitAsync(cancellationToken);
        }

        /// <summary>
        /// 停止MCP客户端
        /// </summary>
        private async Task StopMcpClientAsync(int configId)
        {
            McpClient? client = null;
            lock (_lockObj)
            {
                if (_mcpClients.TryGetValue(configId, out var foundClient))
                {
                    client = foundClient;
                    _mcpClients.Remove(configId);
                }
            }

            if (client != null)
            {
                try
                {
                    await client.DisposeAsync();
                    _logger.LogInformation($"MCP 客户端已停止: ID {configId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"停止 MCP 客户端失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 停止所有MCP客户端
        /// </summary>
        public async Task StopAllClientsAsync()
        {
            List<McpClient> clients;
            lock (_lockObj)
            {
                clients = _mcpClients.Values.ToList();
                _mcpClients.Clear();
            }

            foreach (var client in clients)
            {
                try
                {
                    await client.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "停止 MCP 客户端时出错");
                }
            }

            _logger.LogInformation($"已停止 {clients.Count} 个 MCP 客户端");
        }

        #endregion

        #region 私有辅助方法

        private McpServerConfigDto MapToDto(McpServerConfig config)
        {
            return new McpServerConfigDto
            {
                Id = config.Id,
                Name = config.Name,
                Description = config.Description,
                TransportType = config.TransportType,
                Command = config.Command,
                Arguments = string.IsNullOrWhiteSpace(config.Arguments)
                    ? null
                    : JsonSerializer.Deserialize<List<string>>(config.Arguments),
                ServerUrl = config.ServerUrl,
                RequiresAuth = config.RequiresAuth,
                OAuthClientId = config.OAuthClientId,
                OAuthRedirectUri = config.OAuthRedirectUri,
                IsEnabled = config.IsEnabled,
                EnvironmentVariables = string.IsNullOrWhiteSpace(config.EnvironmentVariables)
                    ? null
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(config.EnvironmentVariables),
                Remarks = config.Remarks,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };
        }

        private void UpdateConfigFromRequest(McpServerConfig config, SaveMcpServerConfigRequest request)
        {
            config.Name = request.Name;
            config.Description = request.Description;
            config.TransportType = request.TransportType;
            config.Command = request.Command;
            config.Arguments = request.Arguments == null || request.Arguments.Count == 0
                ? null
                : JsonSerializer.Serialize(request.Arguments);
            config.ServerUrl = request.ServerUrl;
            config.RequiresAuth = request.RequiresAuth;
            config.OAuthClientId = request.OAuthClientId;
            config.OAuthRedirectUri = request.OAuthRedirectUri;
            config.IsEnabled = request.IsEnabled;
            config.EnvironmentVariables = request.EnvironmentVariables == null || request.EnvironmentVariables.Count == 0
                ? null
                : JsonSerializer.Serialize(request.EnvironmentVariables);
            config.Remarks = request.Remarks;
        }

        #endregion

        public async ValueTask DisposeAsync()
        {
            await StopAllClientsAsync();
        }
    }
}
