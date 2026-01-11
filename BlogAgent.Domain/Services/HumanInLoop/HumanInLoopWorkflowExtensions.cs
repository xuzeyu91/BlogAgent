using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace BlogAgent.Domain.Services.HumanInLoop
{
    /// <summary>
    /// 支持 Human-in-the-Loop 的工作流服务扩展
    /// 允许在关键步骤暂停工作流，等待人工确认后再继续
    /// </summary>
    public static class HumanInLoopWorkflowExtensions
    {
        /// <summary>
        /// 人工介入点类型
        /// </summary>
        public enum HumanInterventionPoint
        {
            /// <summary>
            /// 资料收集完成后
            /// </summary>
            AfterResearch,

            /// <summary>
            /// 博客撰写完成后
            /// </summary>
            AfterWriting,

            /// <summary>
            /// 质量审查完成后，决定是否需要重写
            /// </summary>
            AfterReview,

            /// <summary>
            /// 发布前确认
            /// </summary>
            BeforePublish
        }

        /// <summary>
        /// 人工介入请求信息
        /// </summary>
        public class InterventionRequest
        {
            /// <summary>
            /// 介入点类型
            /// </summary>
            public HumanInterventionPoint Point { get; set; }

            /// <summary>
            /// 任务ID
            /// </summary>
            public int TaskId { get; set; }

            /// <summary>
            /// 请求描述
            /// </summary>
            public string Description { get; set; } = string.Empty;

            /// <summary>
            /// 相关数据（JSON格式）
            /// </summary>
            public string? Data { get; set; }

            /// <summary>
            /// 创建时间
            /// </summary>
            public DateTime CreatedAt { get; set; } = DateTime.Now;

            /// <summary>
            /// 请求ID
            /// </summary>
            public string RequestId { get; set; } = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 人工介入响应
        /// </summary>
        public class InterventionResponse
        {
            /// <summary>
            /// 是否批准继续
            /// </summary>
            public bool Approved { get; set; }

            /// <summary>
            /// 用户反馈
            /// </summary>
            public string? Feedback { get; set; }

            /// <summary>
            /// 修改建议的数据（JSON格式）
            /// </summary>
            public string? ModifiedData { get; set; }

            /// <summary>
            /// 响应时间
            /// </summary>
            public DateTime RespondedAt { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// 人工介入回调函数类型
        /// </summary>
        /// <param name="request">介入请求</param>
        /// <returns>介入响应</returns>
        public delegate Task<InterventionResponse> HumanInterventionCallback(InterventionRequest request);

        /// <summary>
        /// 工作流人工介入管理器
        /// 管理整个工作流中的人工介入流程
        /// </summary>
        public class WorkflowHumanInterventionManager
        {
            private readonly Dictionary<HumanInterventionPoint, HumanInterventionCallback> _callbacks = new();
            private readonly ILogger? _logger;
            private readonly Dictionary<int, List<InterventionRequest>> _pendingRequests = new();

            public WorkflowHumanInterventionManager(ILogger? logger = null)
            {
                _logger = logger;
            }

            /// <summary>
            /// 注册介入点回调
            /// </summary>
            /// <param name="point">介入点</param>
            /// <param name="callback">回调函数</param>
            /// <returns>管理器本身，支持链式调用</returns>
            public WorkflowHumanInterventionManager RegisterCallback(
                HumanInterventionPoint point,
                HumanInterventionCallback callback)
            {
                _callbacks[point] = callback;
                _logger?.LogInformation("[HumanInLoop] 已注册介入点回调: {Point}", point);
                return this;
            }

            /// <summary>
            /// 移除介入点回调
            /// </summary>
            /// <param name="point">介入点</param>
            /// <returns>管理器本身，支持链式调用</returns>
            public WorkflowHumanInterventionManager UnregisterCallback(HumanInterventionPoint point)
            {
                _callbacks.Remove(point);
                _logger?.LogInformation("[HumanInLoop] 已移除介入点回调: {Point}", point);
                return this;
            }

            /// <summary>
            /// 触发人工介入
            /// </summary>
            /// <param name="point">介入点</param>
            /// <param name="request">介入请求</param>
            /// <returns>介入响应</returns>
            public async Task<InterventionResponse> TriggerInterventionAsync(
                HumanInterventionPoint point,
                InterventionRequest request)
            {
                if (_callbacks.TryGetValue(point, out var callback))
                {
                    _logger?.LogInformation("[HumanInLoop] 触发介入点: {Point}, TaskId: {TaskId}", point, request.TaskId);

                    // 记录待处理请求
                    if (!_pendingRequests.ContainsKey(request.TaskId))
                    {
                        _pendingRequests[request.TaskId] = new List<InterventionRequest>();
                    }
                    _pendingRequests[request.TaskId].Add(request);

                    var response = await callback(request);

                    // 移除已处理的请求
                    _pendingRequests[request.TaskId].Remove(request);

                    return response;
                }

                _logger?.LogWarning("[HumanInLoop] 介入点没有注册回调: {Point}，自动批准", point);
                return new InterventionResponse { Approved = true };
            }

            /// <summary>
            /// 检查介入点是否有回调
            /// </summary>
            /// <param name="point">介入点</param>
            /// <returns>是否有回调</returns>
            public bool HasCallback(HumanInterventionPoint point)
            {
                return _callbacks.ContainsKey(point);
            }

            /// <summary>
            /// 获取所有已注册的介入点
            /// </summary>
            /// <returns>介入点列表</returns>
            public IEnumerable<HumanInterventionPoint> GetRegisteredPoints()
            {
                return _callbacks.Keys;
            }

            /// <summary>
            /// 获取指定任务的待处理请求
            /// </summary>
            /// <param name="taskId">任务ID</param>
            /// <returns>待处理请求列表</returns>
            public List<InterventionRequest> GetPendingRequests(int taskId)
            {
                return _pendingRequests.GetValueOrDefault(taskId, new List<InterventionRequest>());
            }

            /// <summary>
            /// 清除指定任务的所有待处理请求
            /// </summary>
            /// <param name="taskId">任务ID</param>
            public void ClearPendingRequests(int taskId)
            {
                _pendingRequests.Remove(taskId);
            }

            /// <summary>
            /// 获取介入点描述
            /// </summary>
            /// <param name="point">介入点</param>
            /// <returns>描述文本</returns>
            public static string GetInterventionDescription(HumanInterventionPoint point)
            {
                return point switch
                {
                    HumanInterventionPoint.AfterResearch => "资料收集已完成，请审查收集的内容是否满足需求",
                    HumanInterventionPoint.AfterWriting => "博客草稿已生成，请审查内容质量",
                    HumanInterventionPoint.AfterReview => "质量审查已完成，请决定是否需要重写",
                    HumanInterventionPoint.BeforePublish => "准备发布博客，请最终确认",
                    _ => "需要人工确认"
                };
            }
        }

        /// <summary>
        /// 创建拒绝响应消息
        /// </summary>
        /// <param name="point">介入点</param>
        /// <param name="feedback">反馈信息</param>
        /// <returns>拒绝响应消息</returns>
        public static string CreateRejectionMessage(HumanInterventionPoint point, string? feedback)
        {
            var pointName = point switch
            {
                HumanInterventionPoint.AfterResearch => "资料收集",
                HumanInterventionPoint.AfterWriting => "博客撰写",
                HumanInterventionPoint.AfterReview => "质量审查",
                HumanInterventionPoint.BeforePublish => "发布",
                _ => "操作"
            };

            return $"[人工介入拒绝] 在{pointName}阶段被拒绝。{feedback ?? ""}";
        }

        /// <summary>
        /// 判断是否应该触发人工介入
        /// </summary>
        /// <param name="point">介入点</param>
        /// <param name="enableAlways">是否总是启用</param>
        /// <returns>是否应该触发</returns>
        public static bool ShouldTriggerIntervention(HumanInterventionPoint point, bool enableAlways = false)
        {
            return enableAlways || point == HumanInterventionPoint.BeforePublish;
        }
    }
}
