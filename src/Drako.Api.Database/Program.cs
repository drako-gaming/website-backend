using System;
using System.Reflection;
using DbUp;
using Microsoft.Extensions.Configuration;

namespace Drako.Api.Database
{
    class Program
    {
        static int Main()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appSettings.json", true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration["database:connectionString"];

            var upgradeEngine = DeployChanges.To.PostgresqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(typeof(Program).Assembly)
                .LogToConsole()
                .Build();

            var result = upgradeEngine.PerformUpgrade();

            if (!result.Successful)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(result.Error);
                Console.ResetColor();
                return -1;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Success!");
            Console.ResetColor();
            return 0;
        }
    }
}