using System;
using System.Collections.Generic;

namespace BlogAgent.Domain.Domain.Dto
{
    /// <summary>
    /// MCP服务器配置DTO
    /// </summary>
    public class McpServerConfigDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string TransportType { get; set; } = "stdio";
        public string? Command { get; set; }
        public List<string>? Arguments { get; set; }
        public string? ServerUrl { get; set; }
        public bool RequiresAuth { get; set; } = false;
        public string? OAuthClientId { get; set; }
        public string? OAuthRedirectUri { get; set; }
        public bool IsEnabled { get; set; } = true;
        public Dictionary<string, string>? EnvironmentVariables { get; set; }
        public string? Remarks { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// 创建/更新MCP服务器配置请求
    /// </summary>
    public class SaveMcpServerConfigRequest
    {
        public int? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string TransportType { get; set; } = "stdio";
        public string? Command { get; set; }
        public List<string>? Arguments { get; set; }
        public string? ServerUrl { get; set; }
        public bool RequiresAuth { get; set; } = false;
        public string? OAuthClientId { get; set; }
        public string? OAuthRedirectUri { get; set; }
        public bool IsEnabled { get; set; } = true;
        public Dictionary<string, string>? EnvironmentVariables { get; set; }
        public string? Remarks { get; set; }
    }

    /// <summary>
    /// MCP工具信息
    /// </summary>
    public class McpToolInfo
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public int ServerId { get; set; }
    }

    /// <summary>
    /// MCP服务器测试结果
    /// </summary>
    public class McpServerTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<McpToolInfo> Tools { get; set; } = new();
        public string? Error { get; set; }
    }
}
