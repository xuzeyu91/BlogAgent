using BlogAgent.Domain.Common.Extensions;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace BlogAgent.Domain.Services
{
    /// <summary>
    /// Web 内容抓取服务
    /// </summary>
    [ServiceDescription(typeof(WebContentService), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped)]
    public class WebContentService
    {
        private readonly ILogger<WebContentService> _logger;
        private readonly HttpClient _httpClient;

        public WebContentService(ILogger<WebContentService> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("WebContentFetcher");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "BlogAgent/1.0 (Content Fetcher)");
        }

        /// <summary>
        /// 抓取 URL 内容
        /// </summary>
        /// <param name="url">目标 URL</param>
        /// <returns>清洗后的文本内容</returns>
        public async Task<string> FetchUrlContentAsync(string url)
        {
            try
            {
                _logger.LogInformation($"开始抓取 URL: {url}");

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

                // 根据内容类型处理
                string textContent;
                if (contentType.Contains("text/html"))
                {
                    textContent = ExtractTextFromHtml(content);
                }
                else if (contentType.Contains("text/plain"))
                {
                    textContent = content;
                }
                else if (contentType.Contains("application/json"))
                {
                    textContent = content;
                }
                else
                {
                    textContent = content; // 其他类型直接返回
                }

                _logger.LogInformation($"URL 抓取成功: {url}, 内容长度: {textContent.Length}");
                return textContent;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"HTTP 请求失败: {url}");
                return $"[无法访问 URL: {url}]\n错误: {ex.Message}";
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, $"请求超时: {url}");
                return $"[URL 访问超时: {url}]";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"抓取 URL 失败: {url}");
                return $"[URL 抓取失败: {url}]\n错误: {ex.Message}";
            }
        }

        /// <summary>
        /// 批量抓取 URL 内容
        /// </summary>
        /// <param name="urls">URL 列表</param>
        /// <returns>格式化的内容集合</returns>
        public async Task<string> FetchMultipleUrlsAsync(IEnumerable<string> urls)
        {
            var fetchedContents = new List<string>();

            foreach (var url in urls)
            {
                var trimmedUrl = url.Trim();
                if (string.IsNullOrWhiteSpace(trimmedUrl))
                    continue;

                var content = await FetchUrlContentAsync(trimmedUrl);
                var formattedContent = $@"
================================================================================
📄 来源: {trimmedUrl}
================================================================================

{content}

";
                fetchedContents.Add(formattedContent);
            }

            if (fetchedContents.Count == 0)
            {
                return "无可用的参考资料";
            }

            return string.Join("\n", fetchedContents);
        }

        /// <summary>
        /// 从 HTML 中提取纯文本内容
        /// </summary>
        private string ExtractTextFromHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            try
            {
                // 移除 script 和 style 标签
                html = Regex.Replace(html, @"<script[^>]*?>.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"<style[^>]*?>.*?</style>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                // 保留代码块(pre, code)的换行
                html = Regex.Replace(html, @"<pre[^>]*?>(.*?)</pre>", "\n```\n$1\n```\n", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"<code[^>]*?>(.*?)</code>", "`$1`", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                // 将常见块级元素转换为换行
                html = Regex.Replace(html, @"<(p|div|br|h[1-6]|li|tr)[^>]*?>", "\n", RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"</(p|div|h[1-6]|li|tr)>", "\n", RegexOptions.IgnoreCase);

                // 移除所有其他 HTML 标签
                html = Regex.Replace(html, @"<[^>]+>", "");

                // 解码 HTML 实体
                html = WebUtility.HtmlDecode(html);

                // 清理多余空白
                html = Regex.Replace(html, @"[ \t]+", " "); // 多个空格/制表符 -> 单个空格
                html = Regex.Replace(html, @"\n\s*\n\s*\n+", "\n\n"); // 多个换行 -> 双换行
                html = html.Trim();

                // 限制内容长度(避免过长)
                const int MaxLength = 50000;
                if (html.Length > MaxLength)
                {
                    html = html.Substring(0, MaxLength) + "\n\n[内容过长,已截断...]";
                }

                return html;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTML 文本提取失败");
                return html; // 失败时返回原始内容
            }
        }

        /// <summary>
        /// 验证 URL 格式
        /// </summary>
        public static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }
    }
}
