using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Drako.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog((_, lc) => lc.WriteTo.Console())
                .ConfigureWebHost(wb =>
                {
                    wb.ConfigureAppConfiguration(cb =>
                    {
                        cb.AddEnvironmentVariables();
                        cb.AddJsonFile("appSettings.json", false);
                    });
                })
                .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); });
    }
}