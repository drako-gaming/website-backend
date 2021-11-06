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

        public async Task<(string AccessToken, string RefreshToken)> GetTokens()
        {
            const string sql = @"
                SELECT a.Value as accessToken, b.Value as RefreshToken
                FROM junk_strings a
                CROSS JOIN junk_strings b
                WHERE a.name = 'AccessToken' and b.name = 'RefreshToken';
            ";
            
            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            var result = await connection.QuerySingleAsync(
                sql
            );

            return (result.accesstoken, result.refreshtoken);
        }
    }
}