using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Collections.Frozen;

namespace BlogAgent.Domain.Services.HumanInLoop
{
    /// <summary>
    /// Human-in-the-Loop (人工介入) 辅助类
    /// 提供在 Agent 执行过程中需要人工确认的辅助方法
    /// </summary>
#pragma warning disable MEAI001 // 类型仅用于评估
    public static class HumanInLoopHelper
    {
        /// <summary>
        /// 创建需要审批的函数工具
        /// </summary>
        /// <param name="function">原始函数</param>
        /// <param name="functionName">函数名称</param>
        /// <returns>包装后的需要审批的函数</returns>
        public static AIFunction CreateApprovalRequiredFunction(AIFunction function, string? functionName = null)
        {
            return new ApprovalRequiredAIFunction(function);
        }

        /// <summary>
        /// 从静态方法创建需要审批的函数工具
        /// </summary>
        /// <param name="method">方法委托</param>
        /// <param name="functionName">函数名称</param>
        /// <returns>需要审批的函数工具</returns>
        public static AIFunction CreateApprovalRequiredFunction(
            Delegate method,
            string functionName)
        {
            var function = AIFunctionFactory.Create(method, name: functionName);
            return new ApprovalRequiredAIFunction(function);
        }

        /// <summary>
        /// 检查响应是否有待处理的人工输入请求
        /// </summary>
        /// <param name="response">Agent 运行响应</param>
        /// <returns>是否有待处理的请求</returns>
        public static bool HasPendingUserInputRequests(AgentRunResponse response)
        {
            return response?.UserInputRequests?.Any() == true;
        }

        /// <summary>
        /// 获取所有函数审批请求
        /// </summary>
        /// <param name="response">Agent 运行响应</param>
        /// <returns>函数审批请求列表</returns>
        public static List<FunctionApprovalRequestContent> GetFunctionApprovalRequests(AgentRunResponse response)
        {
            return response?.UserInputRequests?.OfType<FunctionApprovalRequestContent>().ToList() ?? new();
        }

        /// <summary>
        /// 创建审批响应消息
        /// </summary>
        /// <param name="request">审批请求</param>
        /// <param name="approved">是否批准</param>
        /// <returns>响应消息</returns>
        public static ChatMessage CreateApprovalResponse(FunctionApprovalRequestContent request, bool approved)
        {
            return new ChatMessage(ChatRole.User, [request.CreateResponse(approved)]);
        }

        /// <summary>
        /// 批量创建审批响应消息
        /// </summary>
        /// <param name="requests">审批请求列表</param>
        /// <param name="approved">是否全部批准</param>
        /// <returns>响应消息列表</returns>
        public static List<ChatMessage> CreateApprovalResponses(IEnumerable<FunctionApprovalRequestContent> requests, bool approved)
        {
            return requests.Select(request => CreateApprovalResponse(request, approved)).ToList();
        }

        /// <summary>
        /// 根据审批决策创建响应消息
        /// </summary>
        /// <param name="requests">审批请求列表</param>
        /// <param name="approvalFunc">审批决策函数，返回每个请求是否应该被批准</param>
        /// <returns>响应消息列表</returns>
        public static List<ChatMessage> CreateApprovalResponses(
            IEnumerable<FunctionApprovalRequestContent> requests,
            Func<FunctionApprovalRequestContent, bool> approvalFunc)
        {
            return requests.Select(request => CreateApprovalResponse(request, approvalFunc(request))).ToList();
        }

        /// <summary>
        /// 人工介入状态信息
        /// </summary>
        public class HumanInLoopStatus
        {
            /// <summary>
            /// 是否有人工介入请求
            /// </summary>
            public bool HasPendingRequests { get; set; }

            /// <summary>
            /// 待审批的函数列表
            /// </summary>
            public List<PendingFunctionApproval> PendingApprovals { get; set; } = new();

            /// <summary>
            /// 总请求数
            /// </summary>
            public int TotalRequestCount => PendingApprovals.Count;

            /// <summary>
            /// 是否已全部处理
            /// </summary>
            public bool IsFullyProcessed => !HasPendingRequests && PendingApprovals.Count == 0;
        }

        /// <summary>
        /// 待审批的函数信息
        /// </summary>
        public class PendingFunctionApproval
        {
            /// <summary>
            /// 函数名称
            /// </summary>
            public string FunctionName { get; set; } = string.Empty;

            /// <summary>
            /// 函数参数（JSON字符串）
            /// </summary>
            public string Arguments { get; set; } = string.Empty;

            /// <summary>
            /// 请求ID
            /// </summary>
            public string RequestId { get; set; } = Guid.NewGuid().ToString();

            /// <summary>
            /// 原始请求对象
            /// </summary>
            public FunctionApprovalRequestContent? Request { get; set; }
        }

        /// <summary>
        /// 获取人工介入状态
        /// </summary>
        /// <param name="response">Agent 运行响应</param>
        /// <returns>人工介入状态</returns>
        public static HumanInLoopStatus GetHumanInLoopStatus(AgentRunResponse response)
        {
            var status = new HumanInLoopStatus();

            if (response?.UserInputRequests == null)
            {
                return status;
            }

            var approvalRequests = response.UserInputRequests.OfType<FunctionApprovalRequestContent>();

            foreach (var request in approvalRequests)
            {
                status.PendingApprovals.Add(new PendingFunctionApproval
                {
                    FunctionName = request.FunctionCall.Name,
                    Arguments = request.FunctionCall.Arguments?.ToString() ?? string.Empty,
                    Request = request
                });
            }

            status.HasPendingRequests = status.PendingApprovals.Count > 0;

            return status;
        }

        /// <summary>
        /// 人工介入决策类型
        /// </summary>
        public enum ApprovalDecision
        {
            /// <summary>
            /// 批准
            /// </summary>
            Approve,

            /// <summary>
            /// 拒绝
            /// </summary>
            Reject,

            /// <summary>
            /// 跳过
            /// </summary>
            Skip
        }

        /// <summary>
        /// 根据决策创建响应消息
        /// </summary>
        /// <param name="requests">审批请求列表</param>
        /// <param name="decisions">每个请求的决策</param>
        /// <returns>响应消息列表</returns>
        public static List<ChatMessage> CreateApprovalResponsesByDecisions(
            IEnumerable<FunctionApprovalRequestContent> requests,
            IDictionary<string, ApprovalDecision> decisions)
        {
            var responses = new List<ChatMessage>();

            foreach (var request in requests)
            {
                var decision = decisions.TryGetValue(request.FunctionCall.Name, out var d) ? d : ApprovalDecision.Approve;
                bool approved = decision == ApprovalDecision.Approve;

                responses.Add(CreateApprovalResponse(request, approved));
            }

            return responses;
        }

        /// <summary>
        /// 启用评估 API 警告
        /// </summary>
        #pragma warning restore MEAI001 // 类型仅用于评估
    }
}
