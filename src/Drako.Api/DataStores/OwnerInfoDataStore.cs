using System.Threading.Tasks;
using Dapper;
using Drako.Api.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Drako.Api.DataStores
{
    public class OwnerInfoDataStore
    {
        private readonly IOptions<DatabaseOptions> _options;

        public OwnerInfoDataStore(IOptions<DatabaseOptions> options)
        {
            _options = options;
        }
        
        public async Task SaveTokens(string accessToken, string refreshToken)
        {
            const string sql = @"
                INSERT INTO junk_strings (name, value)
                VALUES ('AccessToken', @accessToken)
                ON CONFLICT (name) DO UPDATE
                    SET value = @accessToken;

                INSERT INTO junk_strings (name, value)
                VALUES ('RefreshToken', @refreshToken)
                ON CONFLICT (name) DO UPDATE
                    SET value = @refreshToken;
            ";
            
            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            await connection.ExecuteAsync(
                sql,
                new
                {
                    accessToken,
                    refreshToken
                }
            );
        }
    }
}