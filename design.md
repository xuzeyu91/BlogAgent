# 博客智能体增强方案设计

## 概述

基于对 Agent Framework 高级特性的研究，本文档设计了提升博客智能体智能程度的具体实现方案。

---

## 一、条件工作流增强（优先级：高）

### 1.1 当前问题

现有工作流使用 `AgentWorkflowBuilder.BuildSequential` 只能顺序执行：
```
Researcher → Writer → Reviewer → 结束
```

当评分不达标时，需要手动重新执行整个流程。

### 1.2 增强方案

#### 1.2.1 条件重写机制

```
Researcher → Writer → Reviewer
                          ↓
                    ┌─────┴─────┐
                    ↓           ↓
                评分 < 80     评分 ≥ 80
                    ↓           ↓
                RewriteAgent → Publish
                    ↓
                回到 Reviewer
```

#### 1.2.2 技术实现

使用 `WorkflowBuilder` 替代 `AgentWorkflowBuilder.BuildSequential`：

```csharp
// 定义执行器（Executors）
var researcherExecutor = new ResearcherExecutor(researcherAgent);
var writerExecutor = new WriterExecutor(writerAgent);
var reviewerExecutor = new ReviewerExecutor(reviewerAgent);
var rewriteExecutor = new RewriteExecutor(writerAgent);  // 新增
var publishExecutor = new PublishExecutor();

// 构建条件工作流
var workflow = new WorkflowBuilder(researcherExecutor)
    .AddEdge(researcherExecutor, writerExecutor)
    .AddEdge(writerExecutor, reviewerExecutor)

    // 条件边：评分 < 80 走重写分支
    .AddEdge(reviewerExecutor, rewriteExecutor,
        condition: review => review.OverallScore < 80)

    // 条件边：评分 >= 80 走发布分支
    .AddEdge(reviewerExecutor, publishExecutor,
        condition: review => review.OverallScore >= 80)

    // 重写后回到审查
    .AddEdge(rewriteExecutor, reviewerExecutor)

    .Build();
```

#### 1.2.3 新增文件

```
BlogAgent.Domain/Services/Workflows/Executors/
├── ResearcherExecutor.cs      # 研究员执行器
├── WriterExecutor.cs           # 作家执行器
├── ReviewerExecutor.cs         # 审查员执行器
├── RewriteExecutor.cs          # 重写执行器（新增）
└── PublishExecutor.cs          # 发布执行器（新增）
```

#### 1.2.4 Shared State 常量

```csharp
// BlogAgent.Domain/Common/Constants/BlogStateConstants.cs
internal static class BlogStateConstants
{
    public const string BlogStateScope = "BlogState";
    public const string ResearchResultKey = "ResearchResult";
    public const string DraftContentKey = "DraftContent";
    public const string ReviewResultKey = "ReviewResult";
    public const string RewriteCountKey = "RewriteCount";
}
```

---

## 二、Shared State 增强（优先级：高）

### 2.1 当前问题

当前 Agent 之间通过字符串传递数据，需要反复序列化/反序列化：
```csharp
// ResearcherAgent 输出 JSON 字符串
var output = await ExecuteAsync(input, taskId);
// WriterAgent 需要解析
var research = JsonSerializer.Deserialize<ResearchOutput>(output);
```

### 2.2 增强方案

使用 Workflow Shared State 在 Executor 之间直接传递对象：

```csharp
// ResearcherExecutor 存储
await context.QueueStateUpdateAsync(
    BlogStateConstants.ResearchResultKey,
    researchOutput,
    BlogStateConstants.BlogStateScope,
    cancellationToken);

// WriterExecutor 读取
var researchResult = await context.ReadStateAsync<ResearchOutput>(
    BlogStateConstants.ResearchResultKey,
    BlogStateConstants.BlogStateScope,
    cancellationToken);
```

### 2.3 优势

- **类型安全**: 直接传递强类型对象
- **性能提升**: 避免重复序列化
- **代码清晰**: 不需要 JSON 解析逻辑

---

## 三、Middleware 安全防护（优先级：中）

### 3.1 功能

#### 3.1.1 内容安全 Guardrails

过滤敏感词、违禁内容：
```csharp
async Task<AgentRunResponse> ContentSafetyMiddleware(
    IEnumerable<ChatMessage> messages,
    AgentThread? thread,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    var filteredMessages = messages.Select(m => new ChatMessage(m.Role, FilterContent(m.Text))).ToList();

    var response = await innerAgent.RunAsync(filteredMessages, thread, options, cancellationToken);

    // 过滤输出
    response.Messages = response.Messages.Select(m => new ChatMessage(m.Role, FilterContent(m.Text))).ToList();

    return response;

    static string FilterContent(string content)
    {
        // 敏感词列表
        var sensitiveWords = new[] { "暴力", "色情", "赌博", "诈骗" };

        foreach (var word in sensitiveWords)
        {
            content = content.Replace(word, "[内容已过滤]");
        }

        return content;
    }
}
```

#### 3.1.2 PII 隐私过滤

检测并脱敏隐私信息：
```csharp
async Task<AgentRunResponse> PIIMiddleware(...)
{
    // 过滤输入
    var filteredMessages = messages.Select(m =>
        new ChatMessage(m.Role, FilterPII(m.Text))).ToList();

    var response = await innerAgent.RunAsync(filteredMessages, thread, options, cancellationToken);

    // 过滤输出
    response.Messages = response.Messages.Select(m =>
        new ChatMessage(m.Role, FilterPII(m.Text))).ToList();

    return response;

    static string FilterPII(string content)
    {
        // 手机号
        content = Regex.Replace(content, @"\b\d{3}-\d{3}-\d{4}\b", "[手机号已隐藏]");
        // 邮箱
        content = Regex.Replace(content, @"\b[\w\.-]+@[\w\.-]+\.\w+\b", "[邮箱已隐藏]");
        return content;
    }
}
```

### 3.2 应用位置

在 `BaseAgentService.GetAgentAsync()` 中添加：

```csharp
protected override async Task<ChatClientAgent> GetAgentAsync()
{
    if (_agent == null)
    {
        var chatClient = new OpenAIClient(...)
            .GetChatClient(OpenAIOption.ChatModel)
            .AsIChatClient();

        _agent = chatClient
            .AsBuilder()
            .Use(PIIMiddleware, null)
            .Use(ContentSafetyMiddleware, null)
            .BuildAIAgent(options);
    }

    return _agent;
}
```

---

## 四、Human-in-the-Loop 增强（优先级：中）

### 4.1 应用场景

1. **评分确认**: 审查完成后，让用户决定是否接受
2. **发布确认**: 发布前最终确认
3. **重写确认**: 评分不达标时，询问是否自动重写

### 4.2 技术实现

使用 `UserInputRequest` 请求用户输入：

```csharp
// ReviewerExecutor 中
if (reviewResult.OverallScore < 80)
{
    // 请求用户决定
    await context.YieldOutputAsync(new UserInputRequest(
        "评分不达标 (总分: {reviewResult.OverallScore})，是否自动重写？(Y/N)",
        "rewrite_approval"
    ));
}
```

### 4.3 UI 更新

在 Blazor 组件中监听 `UserInputRequest` 事件，显示确认对话框。

---

## 五、实施路线图

### 第一阶段：条件工作流（1-2天）

1. 创建 Executor 基类
2. 实现 ResearcherExecutor、WriterExecutor、ReviewerExecutor
3. 实现 RewriteExecutor、PublishExecutor
4. 更新 `BlogAgentWorkflowService` 使用新工作流
5. UI 更新：显示重写次数和条件分支路径

### 第二阶段：Shared State（1天）

1. 定义 `BlogStateConstants`
2. 修改 Executor 使用 Shared State 传递数据
3. 移除 JSON 序列化/反序列化代码

### 第三阶段：Middleware（1天）

1. 实现 `ContentSafetyMiddleware`
2. 实现 `PIIMiddleware`
3. 在 `BaseAgentService` 中集成

### 第四阶段：Human-in-the-Loop（1天）

1. 在关键节点添加 `UserInputRequest`
2. UI 支持用户输入响应

---

## 六、风险评估

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| 工作流复杂度增加 | 调试困难 | 添加详细日志和可视化 |
| Shared State 序列化问题 | 类型不匹配 | 使用 JSON Schema 验证 |
| Middleware 性能影响 | 响应变慢 | 异步处理、缓存 |
| 用户交互流程断裂 | 体验下降 | 提供超时自动继续 |

---

## 七、向后兼容

保留原有的 `BlogWorkflowService`（基于直接调用 Agent），新增 `BlogAgentWorkflowServiceV2`（基于条件工作流）。

用户可在配置中选择使用哪个版本。
