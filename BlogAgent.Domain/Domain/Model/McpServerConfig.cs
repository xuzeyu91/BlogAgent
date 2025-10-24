using SqlSugar;
using System;

namespace BlogAgent.Domain.Domain.Model
{
    /// <summary>
    /// MCP服务器配置模型
    /// </summary>
    [SugarTable("mcp_server_configs")]
    public class McpServerConfig
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>
        /// 配置名称
        /// </summary>
        [SugarColumn(Length = 100, IsNullable = false)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 描述
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Description { get; set; }

        /// <summary>
        /// 传输类型: stdio | http
        /// </summary>
        [SugarColumn(Length = 20, IsNullable = false)]
        public string TransportType { get; set; } = "stdio";

        /// <summary>
        /// Stdio模式: 命令 (如: npx, node, python)
        /// </summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string? Command { get; set; }

        /// <summary>
        /// Stdio模式: 命令参数 (JSON数组格式, 如: ["-y", "@modelcontextprotocol/server-github"])
        /// </summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Arguments { get; set; }

        /// <summary>
        /// Http模式: 服务器URL
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? ServerUrl { get; set; }

        /// <summary>
        /// 是否需要OAuth认证
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public bool RequiresAuth { get; set; } = false;

        /// <summary>
        /// OAuth客户端ID
        /// </summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string? OAuthClientId { get; set; }

        /// <summary>
        /// OAuth重定向URI
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? OAuthRedirectUri { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 创建时间
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 更新时间
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 环境变量配置 (JSON格式)
        /// </summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? EnvironmentVariables { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        [SugarColumn(Length = 1000, IsNullable = true)]
        public string? Remarks { get; set; }
    }
}
