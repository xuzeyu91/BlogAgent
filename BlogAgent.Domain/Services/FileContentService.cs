using BlogAgent.Domain.Common.Extensions;
using Microsoft.Extensions.Logging;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace BlogAgent.Domain.Services
{
    /// <summary>
    /// 文件内容读取服务
    /// </summary>
    [ServiceDescription(typeof(FileContentService), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped)]
    public class FileContentService
    {
        private readonly ILogger<FileContentService> _logger;

        // 支持的文本文件扩展名
        private static readonly HashSet<string> SupportedTextExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".markdown", ".json", ".xml", ".csv",
            ".cs", ".java", ".py", ".js", ".ts", ".jsx", ".tsx",
            ".html", ".htm", ".css", ".scss", ".less",
            ".yaml", ".yml", ".toml", ".ini", ".conf",
            ".sql", ".sh", ".bat", ".ps1",
            ".log", ".config", ".properties"
        };

        // 支持的文档格式扩展名
        private static readonly HashSet<string> SupportedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".docx", ".doc", ".pdf"
        };

        public FileContentService(ILogger<FileContentService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 读取文件内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件内容</returns>
        public async Task<string> ReadFileContentAsync(string filePath)
        {
            try
            {
                _logger.LogInformation($"开始读取文件: {filePath}");

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning($"文件不存在: {filePath}");
                    return $"[文件不存在: {filePath}]";
                }

                var fileInfo = new FileInfo(filePath);
                var extension = fileInfo.Extension;

                // 检查文件类型
                if (!SupportedTextExtensions.Contains(extension) && !SupportedDocumentExtensions.Contains(extension))
                {
                    _logger.LogWarning($"不支持的文件类型: {extension}, 文件: {filePath}");
                    return $"[不支持的文件类型: {extension}]\n文件: {filePath}";
                }

                // 检查文件大小(限制 50MB，文档文件通常较大)
                const long MaxFileSize = 50 * 1024 * 1024;
                if (fileInfo.Length > MaxFileSize)
                {
                    _logger.LogWarning($"文件过大: {fileInfo.Length} bytes, 文件: {filePath}");
                    return $"[文件过大: {fileInfo.Length / 1024 / 1024}MB, 最大支持 50MB]\n文件: {filePath}";
                }

                // 根据文件类型读取内容
                string content;
                if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
                {
                    content = await ReadWordDocumentAsync(filePath);
                }
                else if (extension.Equals(".doc", StringComparison.OrdinalIgnoreCase))
                {
                    content = "[.doc 格式不支持，请转换为 .docx 格式]";
                }
                else if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    content = await ReadPdfDocumentAsync(filePath);
                }
                else
                {
                    // 读取普通文本文件
                    content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                }

                _logger.LogInformation($"文件读取成功: {filePath}, 内容长度: {content.Length}");
                return content;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, $"无权限访问文件: {filePath}");
                return $"[无权限访问文件: {filePath}]";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"读取文件失败: {filePath}");
                return $"[文件读取失败: {filePath}]\n错误: {ex.Message}";
            }
        }

        /// <summary>
        /// 批量读取文件内容
        /// </summary>
        /// <param name="filePaths">文件路径列表</param>
        /// <returns>格式化的内容集合</returns>
        public async Task<string> ReadMultipleFilesAsync(IEnumerable<string> filePaths)
        {
            var fileContents = new List<string>();

            foreach (var filePath in filePaths)
            {
                var trimmedPath = filePath.Trim();
                if (string.IsNullOrWhiteSpace(trimmedPath))
                    continue;

                var content = await ReadFileContentAsync(trimmedPath);
                var fileName = Path.GetFileName(trimmedPath);
                
                var formattedContent = $@"
================================================================================
📁 文件: {fileName}
📂 路径: {trimmedPath}
================================================================================

{content}

";
                fileContents.Add(formattedContent);
            }

            if (fileContents.Count == 0)
            {
                return "无可用的文件内容";
            }

            return string.Join("\n", fileContents);
        }

        /// <summary>
        /// 检查文件是否存在且可读
        /// </summary>
        public bool IsFileAccessible(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                var fileInfo = new FileInfo(filePath);
                return SupportedTextExtensions.Contains(fileInfo.Extension) 
                    || SupportedDocumentExtensions.Contains(fileInfo.Extension);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取支持的文件扩展名列表
        /// </summary>
        public static IReadOnlyCollection<string> GetSupportedExtensions()
        {
            return SupportedTextExtensions.Union(SupportedDocumentExtensions).ToList();
        }

        /// <summary>
        /// 读取 Word 文档内容 (.docx)
        /// </summary>
        private async Task<string> ReadWordDocumentAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var document = WordprocessingDocument.Open(filePath, false);
                    var body = document.MainDocumentPart?.Document?.Body;
                    
                    if (body == null)
                    {
                        return "[Word 文档内容为空]";
                    }

                    var textBuilder = new StringBuilder();
                    
                    // 提取所有段落文本
                    foreach (var paragraph in body.Descendants<Paragraph>())
                    {
                        var paragraphText = paragraph.InnerText;
                        if (!string.IsNullOrWhiteSpace(paragraphText))
                        {
                            textBuilder.AppendLine(paragraphText);
                        }
                    }

                    // 提取表格内容
                    foreach (var table in body.Descendants<Table>())
                    {
                        textBuilder.AppendLine("\n[表格内容]");
                        foreach (var row in table.Descendants<TableRow>())
                        {
                            var rowText = string.Join(" | ", 
                                row.Descendants<TableCell>().Select(cell => cell.InnerText.Trim()));
                            if (!string.IsNullOrWhiteSpace(rowText))
                            {
                                textBuilder.AppendLine(rowText);
                            }
                        }
                        textBuilder.AppendLine();
                    }

                    var content = textBuilder.ToString().Trim();
                    return string.IsNullOrEmpty(content) ? "[Word 文档无可读取文本]" : content;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"读取 Word 文档失败: {filePath}");
                    return $"[Word 文档读取失败: {ex.Message}]";
                }
            });
        }

        /// <summary>
        /// 读取 PDF 文档内容
        /// </summary>
        private async Task<string> ReadPdfDocumentAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var document = PdfDocument.Open(filePath);
                    var textBuilder = new StringBuilder();
                    
                    // 读取每一页
                    foreach (var page in document.GetPages())
                    {
                        var pageText = page.Text;
                        
                        if (!string.IsNullOrWhiteSpace(pageText))
                        {
                            textBuilder.AppendLine($"[第 {page.Number} 页]");
                            textBuilder.AppendLine(pageText);
                            textBuilder.AppendLine();
                        }
                    }

                    var content = textBuilder.ToString().Trim();
                    return string.IsNullOrEmpty(content) ? "[PDF 文档无可读取文本]" : content;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"读取 PDF 文档失败: {filePath}");
                    return $"[PDF 文档读取失败: {ex.Message}]";
                }
            });
        }
    }
}
