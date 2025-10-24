using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BlogAgent.Repositories.Demo;

namespace BlogAgent.Domain.Common.Extensions
{
    public static class InitExtensions
    {

        /// <summary>
        /// 根据程序集中的实体类创建数据库表
        /// </summary>
        /// <param name="services"></param>
        public static void CodeFirst(this WebApplication app)
        {
            using (var scope = app.Services.CreateScope())
            {
                // 获取仓储服务
                var _repository = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
                // 创建数据库（如果不存在）
                _repository.GetDB().DbMaintenance.CreateDatabase();
                
                // 扫描 BlogAgent.Domain 程序集中的所有实体类
                var domainAssembly = typeof(InitExtensions).Assembly;
                var entityTypes = domainAssembly.GetTypes()
                        .Where(type => TypeIsEntity(type));
                
                // 为每个找到的类型初始化数据库表
                foreach (var type in entityTypes)
                {
                    _repository.GetDB().CodeFirst.InitTables(type);
                }
            }
        }

        static bool TypeIsEntity(Type type)
        {
            // 检查类型是否具有SugarTable特性
            return type.GetCustomAttributes(typeof(SugarTable), inherit: false).Length > 0;
        }
    }
}
