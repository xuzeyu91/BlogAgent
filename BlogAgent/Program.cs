using AntDesign.ProLayout;
using Microsoft.AspNetCore.Components;
using BlogAgent.Domain.Common.Extensions;
using BlogAgent.Domain.Common.Options;
using BlogAgent.Domain.Repositories.Base;
using Serilog;
using SqlSugar;
using Log = Serilog.Log;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddAntDesign();
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(sp.GetService<NavigationManager>()!.BaseUri)
});
builder.Services.Configure<ProSettings>(builder.Configuration.GetSection("ProSettings"));

// 添加内存缓存服务
builder.Services.AddMemoryCache();

// 添加 HttpClient 工厂(用于 Web 内容抓取)
builder.Services.AddHttpClient("WebContentFetcher", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "BlogAgent/1.0 (Content Fetcher)");
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 配置数据库连接选项
builder.Configuration.GetSection("DBConnection").Get<DBConnectionOption>();
builder.Configuration.GetSection("OpenAI").Get<OpenAIOption>();

// 注册 SqlSugar
builder.Services.AddScoped<ISqlSugarClient>(sp => SqlSugarHelper.SqlScope());

builder.Services.AddServicesFromAssemblies("BlogAgent", "BlogAgent.Domain");


// 添加响应压缩服务
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/javascript", "text/css", "text/javascript", "application/json", "text/html" });
});

// 添加 CORS 服务
builder.Services.AddCors(options =>
{
    options.AddPolicy("Any", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// 配置 Serilog 静态日志器
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();

// 将 Serilog 集成到 ASP.NET Core 日志系统
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();

app.MapFallbackToPage("/_Host");

app.UseAuthorization();

app.MapControllers();

app.CodeFirst();

app.Run();
