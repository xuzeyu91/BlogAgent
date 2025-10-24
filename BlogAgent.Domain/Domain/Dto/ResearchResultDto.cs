namespace BlogAgent.Domain.Domain.Dto
{
    /// <summary>
    /// 资料收集结果DTO
    /// </summary>
    public class ResearchResultDto
    {
        /// <summary>
        /// 资料摘要(Markdown格式)
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// 提取的关键点列表
        /// </summary>
        public List<string> KeyPoints { get; set; } = new();

        /// <summary>
        /// 生成时间
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}

