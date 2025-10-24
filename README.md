# 📝 BlogAgent - AI博客智能生成系统

<div align="center">

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)
![Blazor](https://img.shields.io/badge/Blazor-Server-green.svg)
![Agent Framework](https://img.shields.io/badge/Agent%20Framework-1.0.0--preview-orange.svg)

基于 **Microsoft Agent Framework** 的多Agent协作博客生成系统

[功能特性](#-功能特性) • [快速开始](#-快速开始) • [技术架构](#-技术架构) • [使用指南](#-使用指南) • [文档](#-文档)

</div>

---

## 📖 项目简介

BlogAgent 是一个基于 **Microsoft Agent Framework** 构建的智能博客生成系统,通过多个专业 AI Agent 协作完成从资料收集、博客撰写到质量审查的全流程自动化。系统采用 **Blazor Server** 架构,提供现代化的 Web 界面,支持全自动和分步两种工作模式。

### ✨ 核心亮点

- 🤖 **多Agent协作**: 三个专业 Agent (研究员、作家、审查员) 分工协作
- 🔄 **智能工作流**: 基于 Agent Framework Workflow 的声明式编排
- 🎨 **现代化界面**: 基于 Ant Design Blazor 的美观 UI
- 📊 **实时进度**: 支持工作流执行进度实时反馈
- 🔌 **MCP协议**: 支持 Model Context Protocol 扩展外部工具
- 💾 **持久化存储**: SQLite 数据库存储任务和内容
- 🌐 **内容抓取**: 自动从 URL 和文件中提取参考资料

---

## 🎯 功能特性

### 核心功能

#### 1. 资料收集Agent (ResearcherAgent)
- ✅ 智能提取和整理参考资料
- ✅ 生成结构化摘要 (JSON格式)
- ✅ 支持多种输入源:
  - 直接文本输入
  - 文档上传 (txt, md, doc, docx, pdf)
  - URL链接抓取
- ✅ 自定义工具函数 (字数统计、代码块提取)

#### 2. 博客撰写Agent (WriterAgent)
- ✅ 基于资料生成高质量技术博客
- ✅ 支持自定义写作风格和目标读者
- ✅ Markdown格式输出
- ✅ 可控制目标字数 (500-10000字)

#### 3. 质量审查Agent (ReviewerAgent)
- ✅ 多维度质量评估:
  - **准确性** (40分): 技术内容准确性
  - **逻辑性** (30分): 文章结构和逻辑
  - **原创性** (20分): 内容创新性
  - **规范性** (10分): 格式和排版
- ✅ 详细问题分析和改进建议
- ✅ 综合评分和发布建议

### 工作流模式

#### 🚀 全自动模式
一键完成资料收集 → 博客撰写 → 质量审查全流程,无需人工干预。

#### 🎮 分步模式
每个阶段手动触发,可随时查看和调整中间结果。

### 高级特性

- 🔧 **MCP工具配置**: 动态加载和管理外部工具
- 📈 **统计看板**: 任务总数、发布数、平均评分、通过率
- 📝 **内容编辑**: 支持对生成内容进行二次编辑
- 💾 **导出功能**: 支持导出为 Markdown 文件
- 🔍 **详细日志**: 完整的执行日志和错误追踪

---

## 🛠️ 技术架构

### 技术栈

| 类别 | 技术 | 版本 |
|------|------|------|
| **框架** | .NET | 9.0 |
| **UI** | Blazor Server | 9.0 |
| **组件库** | Ant Design Blazor | 1.4.0+ |
| **AI框架** | Microsoft Agent Framework | 1.0.0-preview |
| **AI扩展** | Microsoft.Extensions.AI | 9.10.1-preview |
| **数据库** | SQLite (SqlSugar ORM) | 5.1.4+ |
| **协议** | Model Context Protocol | 0.4.0-preview |
| **文档解析** | PdfPig, DocumentFormat.OpenXml | - |
| **Markdown** | Markdig | 0.37.0 |
| **日志** | Serilog | 4.1.0+ |
| **重试策略** | Polly | 8.5.2 |

### 项目结构

```
BlogAgent/
├── BlogAgent/                          # Blazor Server 主项目
│   ├── Pages/                          # 页面组件
│   │   ├── Index.razor                 # 首页 (统计看板)
│   │   └── Blog/
│   │       ├── Create.razor            # 创建任务
│   │       ├── List.razor              # 任务列表
│   │       ├── Detail.razor            # 任务详情
│   │       ├── Workflow.razor          # 分步工作流
│   │       ├── AutoWorkflow.razor      # 自动工作流
│   │       └── McpConfig.razor         # MCP工具配置
│   ├── Components/                     # 组件
│   │   ├── Markdown.razor              # Markdown渲染
│   │   └── GlobalHeader/               # 全局头部
│   ├── Layouts/                        # 布局
│   │   ├── BasicLayout.razor           # 基础布局
│   │   └── UserLayout.razor            # 用户布局
│   ├── wwwroot/                        # 静态资源
│   ├── Program.cs                      # 程序入口
│   └── appsettings.json                # 配置文件
│
├── BlogAgent.Domain/                   # 业务领域层
│   ├── Services/                       # 业务服务
│   │   ├── Agents/                     # Agent实现
│   │   │   ├── Base/
│   │   │   │   ├── BaseAgentService.cs # Agent基类
│   │   │   │   └── IAgentService.cs    # Agent接口
│   │   │   ├── ResearcherAgent.cs      # 资料收集Agent
│   │   │   ├── WriterAgent.cs          # 博客撰写Agent
│   │   │   └── ReviewerAgent.cs        # 质量审查Agent
│   │   ├── Workflows/                  # 工作流服务
│   │   │   ├── BlogAgentWorkflowService.cs  # 新Workflow服务
│   │   │   └── BlogWorkflowService.cs       # 旧工作流服务
│   │   ├── BlogService.cs              # 博客业务服务
│   │   ├── WebContentService.cs        # Web内容抓取
│   │   ├── FileContentService.cs       # 文件内容提取
│   │   └── McpConfigService.cs         # MCP配置服务
│   ├── Domain/                         # 领域模型
│   │   ├── Model/                      # 实体模型
│   │   │   ├── BlogTask.cs             # 博客任务
│   │   │   ├── BlogContent.cs          # 博客内容
│   │   │   └── ReviewResult.cs         # 审查结果
│   │   ├── Dto/                        # 数据传输对象
│   │   └── Enum/                       # 枚举
│   ├── Repositories/                   # 数据仓储
│   │   ├── Base/                       # 基础仓储
│   │   ├── BlogTaskRepository.cs
│   │   ├── BlogContentRepository.cs
│   │   └── ReviewResultRepository.cs
│   ├── Common/                         # 公共组件
│   │   ├── Extensions/                 # 扩展方法
│   │   └── Options/                    # 配置选项
│   └── Utils/                          # 工具类
│
├── agent-framework/                    # Microsoft Agent Framework (子模块)
│   └── ...
│
├── docs/                               # 文档
│   ├── Agent-Framework-Workflow改造说明.md
│   ├── Agent-Framework-Workflow测试指南.md
│   ├── MCP工具配置功能说明.md
│   ├── 快速参考卡.md
│   └── ...
│
└── README.md                           # 本文件
```

### 系统架构图

```
┌─────────────────────────────────────────────────────────────┐
│                      Blazor Server UI                        │
│  (Ant Design Blazor Components + Real-time Updates)         │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│                  Business Services Layer                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │ BlogService  │  │ WebContent   │  │ FileContent  │     │
│  │              │  │ Service      │  │ Service      │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│           Agent Framework Workflow Layer                     │
│  ┌──────────────────────────────────────────────────┐       │
│  │  BlogAgentWorkflowService                        │       │
│  │  (AgentWorkflowBuilder + InProcessExecution)     │       │
│  └──────┬───────────────────────────────────────────┘       │
│         │                                                    │
│  ┌──────▼────────┐  ┌──────────┐  ┌──────────────┐        │
│  │ Researcher    │─▶│ Writer   │─▶│ Reviewer     │        │
│  │ Agent         │  │ Agent    │  │ Agent        │        │
│  │ (Research)    │  │ (Write)  │  │ (Review)     │        │
│  └───────────────┘  └──────────┘  └──────────────┘        │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│              Microsoft.Extensions.AI                         │
│              (IChatClient Abstraction)                       │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│                   OpenAI ChatClient                          │
│         (Compatible with OpenAI/Azure OpenAI/etc)            │
└─────────────────────────────────────────────────────────────┘
```

---

## 🚀 快速开始

### 前置要求

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Git](https://git-scm.com/)
- OpenAI API Key (或兼容的API服务)

### 安装步骤

#### 1. 克隆项目

```bash
git clone https://github.com/xuzeyu91/BlogAgent.git
cd BlogAgent
```

#### 2. 配置 OpenAI API

编辑 `BlogAgent/appsettings.json`:

```json
{
  "OpenAI": {
    "Key": "your-api-key-here",
    "EndPoint": "https://api.antsk.cn/v1",
    "ChatModel": "gpt-41",
    "EmbeddingModel": "text-embedding-ada-002"
  }
}
```

> 💡 **提示**: 也支持 Azure OpenAI 和其他兼容 OpenAI API 的服务

#### 3. 还原依赖

```bash
dotnet restore
```

#### 4. 编译项目

```bash
dotnet build
```

#### 5. 运行应用

```bash
cd BlogAgent
dotnet run
```

应用将在 `http://localhost:5000` 启动。

### Docker 部署 (可选)

```dockerfile
# Dockerfile 示例
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["BlogAgent/BlogAgent.csproj", "BlogAgent/"]
COPY ["BlogAgent.Domain/BlogAgent.Domain.csproj", "BlogAgent.Domain/"]
RUN dotnet restore "BlogAgent/BlogAgent.csproj"
COPY . .
WORKDIR "/src/BlogAgent"
RUN dotnet build "BlogAgent.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BlogAgent.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BlogAgent.dll"]
```

构建和运行:

```bash
docker build -t blogagent .
docker run -d -p 5000:80 -e OpenAI__Key=your-key blogagent
```

---

## 📚 使用指南

### 创建博客任务

1. 访问 `http://localhost:5000/blog/create`
2. 填写博客主题 (必填)
3. 选择工作流模式:
   - **全自动模式**: 一键完成全流程
   - **分步模式**: 手动控制每个阶段
4. 选择输入方式:
   - **直接输入文本**: 粘贴参考资料
   - **上传文档**: 支持 txt, md, doc, docx, pdf
   - **提供URL**: 自动抓取网页内容
5. (可选) 设置写作要求:
   - 目标字数
   - 写作风格
   - 目标读者
6. 点击 "创建任务并开始"

### 全自动工作流

创建任务后,系统会自动:
1. 🔍 **资料收集**: 整理和分析参考资料
2. ✍️ **博客撰写**: 生成结构化博客内容
3. 📋 **质量审查**: 多维度评估和打分
4. ✅ **自动发布**: 达标后自动发布

全程无需人工干预,只需等待完成。

### 分步工作流

每个阶段手动触发,流程如下:

```
1. 点击 "执行资料收集阶段" 
   └─ 查看收集结果
   └─ 确认继续 或 重新执行

2. 点击 "执行博客撰写阶段"
   └─ 查看博客初稿
   └─ 可编辑修改
   └─ 确认继续

3. 点击 "执行质量审查阶段"
   └─ 查看评分和建议
   └─ 决定发布 或 重写

4. 点击 "发布博客"
   └─ 保存到数据库
   └─ 可导出 Markdown
```

### 任务管理

#### 查看任务列表
访问 `/blog/list` 查看所有任务,支持:
- 按状态筛选
- 查看详情
- 编辑内容
- 删除任务
- 导出Markdown

#### 查看任务详情
访问 `/blog/detail/{taskId}` 查看:
- 任务基本信息
- 资料收集摘要
- 博客完整内容 (Markdown渲染)
- 质量审查报告
- 历史操作记录

### MCP工具配置

访问 `/blog/mcp-config` 配置外部工具:
1. 添加 MCP 工具配置 (JSON)
2. 测试连接
3. 保存配置
4. Agent 自动加载工具

---

## 🎓 Agent Framework 核心概念

### Agent 基础

每个 Agent 继承自 `BaseAgentService`:

```csharp
public class ResearcherAgent : BaseAgentService
{
    protected override string AgentName => "资料收集专家";
    protected override string Instructions => "你是一位专业的技术资料研究员...";
    protected override AgentType AgentType => AgentType.Researcher;
    
    // 自定义工具
    protected override IEnumerable<AITool>? Tools => new[]
    {
        AIFunctionFactory.Create(CountWordsInText),
        AIFunctionFactory.Create(ExtractCodeBlocks)
    };
    
    // 结构化输出
    protected override ChatResponseFormat? ResponseFormat => 
        ChatResponseFormat.ForJsonSchema<ResearchOutput>(schemaName: "ResearchOutput");
}
```

### Workflow 工作流

使用 `AgentWorkflowBuilder` 构建声明式工作流:

```csharp
// 顺序执行
var workflow = AgentWorkflowBuilder.BuildSequential(
    "BlogGenerationWorkflow",
    researcherAgent,
    writerAgent,
    reviewerAgent
);

// 执行工作流
var run = await InProcessExecution.RunAsync(workflow, initialInput);

// 提取输出
var outputEvents = run.OutgoingEvents
    .OfType<WorkflowOutputEvent>()
    .ToList();
```

### 工具函数 (Tools)

Agent 可以调用自定义 C# 函数:

```csharp
[Description("统计给定文本的字数")]
private static int CountWordsInText(
    [Description("要统计的文本内容")] string text)
{
    // 中文字符
    int chineseCount = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
    
    // 英文单词
    var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, 
        StringSplitOptions.RemoveEmptyEntries);
    int englishCount = words.Count(w => w.Any(c => char.IsLetter(c)));
    
    return chineseCount + englishCount;
}
```

### 结构化输出 (Structured Output)

使用 JSON Schema 约束 AI 输出格式:

```csharp
public class ResearchOutput
{
    public string TopicAnalysis { get; set; }
    public List<KeyPoint> KeyPoints { get; set; }
    public List<TechnicalDetail> TechnicalDetails { get; set; }
    public List<CodeExample> CodeExamples { get; set; }
    public List<string> References { get; set; }
}

// 在 Agent 中配置
protected override ChatResponseFormat? ResponseFormat => 
    ChatResponseFormat.ForJsonSchema<ResearchOutput>(
        schemaName: "ResearchOutput", 
        schemaDescription: "研究结果结构"
    );
```

---

## 🔧 配置说明

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "urls": "http://*:5000",
  
  "ProSettings": {
    "NavTheme": "light",
    "Layout": "side",
    "Title": "BlogAgent",
    "FixedHeader": false,
    "FixSiderbar": true
  },
  
  "OpenAI": {
    "Key": "your-api-key",
    "EndPoint": "https://api.openai.com/v1",
    "ChatModel": "gpt-4o",
    "EmbeddingModel": "text-embedding-3-small"
  },
  
  "DBConnection": {
    "DbType": "Sqlite",
    "ConnectionStrings": "Data Source=BlogAgent.db",
    "VectorConnection": "BlogAgentMem.db",
    "VectorSize": 1536
  }
}
```

### 数据库配置

项目使用 **SqlSugar ORM** + **SQLite**:

- 主数据库: `BlogAgent.db` (任务、内容、审查结果)
- 向量数据库: `BlogAgentMem.db` (预留,用于RAG功能)

首次运行时会自动创建数据库和表结构 (Code First)。

---

## 📖 文档

### 主要文档

| 文档 | 说明 |
|------|------|
| [Agent Framework 功能分析](docs/BlogAgent项目Agent-Framework功能分析.md) | Agent Framework 已使用和可添加的功能 |
| [Workflow 改造说明](docs/Agent-Framework-Workflow改造说明.md) | 如何升级到 Workflow |
| [Workflow 测试指南](docs/Agent-Framework-Workflow测试指南.md) | 工作流测试步骤 |
| [快速参考卡](docs/快速参考卡.md) | 核心 API 速查 |
| [MCP 工具配置](docs/MCP工具配置功能说明.md) | MCP 协议集成说明 |
| [改造完成总结](docs/改造完成总结.md) | 改造成果总结 |

### 在线文档

- [Microsoft Agent Framework 官方文档](https://github.com/microsoft/agent-framework)
- [Microsoft.Extensions.AI 文档](https://devblogs.microsoft.com/dotnet/introducing-microsoft-extensions-ai-preview/)
- [Ant Design Blazor 文档](https://antblazor.com/)

---

## 🤝 贡献指南

欢迎贡献代码、报告问题或提出建议!

### 开发环境设置

1. Fork 本仓库
2. 创建特性分支: `git checkout -b feature/AmazingFeature`
3. 提交更改: `git commit -m 'Add some AmazingFeature'`
4. 推送到分支: `git push origin feature/AmazingFeature`
5. 提交 Pull Request

### 代码规范

- 遵循 C# 编码规范
- 使用有意义的变量和方法名
- 添加必要的注释和文档
- 确保所有测试通过

---

## 🗺️ Roadmap

### 已完成 ✅

- [x] 多Agent协作 (Researcher, Writer, Reviewer)
- [x] Agent Framework Workflow 集成
- [x] 全自动和分步两种工作模式
- [x] Web内容抓取和文件内容提取
- [x] MCP协议支持
- [x] 结构化输出 (JSON Schema)
- [x] 自定义工具函数
- [x] 实时进度反馈
- [x] 博客内容二次编辑
- [x] Markdown导出

### 开发中 🚧

- [ ] 流式输出 (Streaming) - UI实时显示生成过程
- [ ] RAG集成 - 从历史博客检索参考
- [ ] 可观测性增强 (OpenTelemetry)
- [ ] 条件工作流 - 根据评分决定下一步

### 计划中 📝

- [ ] 并发工作流 - 多Agent并行执行
- [ ] 多Provider支持 (Azure OpenAI, 本地模型)
- [ ] Agent评估系统 - 自动评估输出质量
- [ ] Prompt自动优化
- [ ] 多语言支持
- [ ] 云端部署方案

详细路线图请查看 [Agent Framework 功能分析文档](docs/BlogAgent项目Agent-Framework功能分析.md)。

---

## ❓ 常见问题

### Q: 编译时出现 AOT 警告怎么办?

A: 这些警告不影响正常运行,仅在使用 Native AOT 编译时有影响。Blazor Server 应用可以忽略。

### Q: 如何切换回旧的工作流服务?

A: 在 `Workflow.razor` 中设置:
```csharp
private bool useAgentFrameworkWorkflow = false;
```

### Q: 支持哪些 AI 模型?

A: 支持所有兼容 OpenAI API 的服务:
- OpenAI (gpt-4, gpt-4o, gpt-3.5-turbo)
- Azure OpenAI
- 自部署的兼容服务 (如 LocalAI, Ollama)

### Q: 数据库可以换成 MySQL/PostgreSQL 吗?

A: 可以。SqlSugar 支持多种数据库,只需修改配置:
```json
{
  "DBConnection": {
    "DbType": "MySql",
    "ConnectionStrings": "Server=localhost;Database=blogagent;..."
  }
}
```

### Q: MCP 工具如何配置?

A: 访问 `/blog/mcp-config` 页面,参考 [MCP工具配置文档](docs/MCP工具配置功能说明.md)。

### Q: 生成的博客质量不理想怎么办?

A: 可以尝试:
1. 提供更详细的参考资料
2. 明确指定写作风格和目标读者
3. 使用分步模式,对中间结果进行人工调整
4. 调整 Agent 的 Instructions (在对应 Agent 类中)

---

## 📄 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件。

---

## 🙏 致谢

- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) - 强大的 Agent 框架
- [Ant Design Blazor](https://github.com/ant-design-blazor/ant-design-blazor) - 优秀的 Blazor 组件库
- [SqlSugar](https://github.com/donet5/SqlSugar) - 高性能 ORM
- [Markdig](https://github.com/xoofx/markdig) - Markdown 解析器

---

## 📞 联系方式

- 项目问题: [GitHub Issues](https://github.com/your-username/BlogAgent/issues)
- 功能建议: [GitHub Discussions](https://github.com/your-username/BlogAgent/discussions)

---

<div align="center">

**如果这个项目对你有帮助,请给它一个 ⭐️ Star!**

Made with ❤️ by BlogAgent Team

</div>

