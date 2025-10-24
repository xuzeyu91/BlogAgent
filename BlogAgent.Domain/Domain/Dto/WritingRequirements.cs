namespace BlogAgent.Domain.Domain.Dto
{
    /// <summary>
    /// 写作要求DTO
    /// </summary>
    public class WritingRequirements
    {
        /// <summary>
        /// 目标字数
        /// </summary>
        public int? TargetWordCount { get; set; }

        /// <summary>
        /// 写作风格
        /// </summary>
        public string? Style { get; set; }

        /// <summary>
        /// 目标读者
        /// </summary>
        public string? TargetAudience { get; set; }
    }
}

