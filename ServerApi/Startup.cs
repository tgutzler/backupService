using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServerApi.Options;

namespace ServerApi
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; }

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();

            var isWindowsOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var dbOptions = new DbOptions()
            {
                ConnectionString = isWindowsOS ?
                    Configuration["ConnectionStrings:sqlite"] :
                    Configuration["ConnectionStrings:mysql"],
                DbType = isWindowsOS ? DbTypeEnym.SQLite : DbTypeEnym.MySQL
            };
            if (!Database.DatabaseService.Start(dbOptions))
            {
                throw new Exception("Could not start database service");
            }
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Adds services required for using options.
            services.AddOptions();

            // Register the IConfiguration instance which Options bind against.
            services.Configure<ApiOptions>(Configuration.GetSection("ApiOptions"));
            services.Configure<StorageOptions>(Configuration.GetSection("StorageOptions"));

            // Add framework services.
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseMvc();
        }
    }
}
