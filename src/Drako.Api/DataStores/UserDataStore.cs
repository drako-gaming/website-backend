using System;
using System.Threading.Tasks;
using Dapper;
using Drako.Api.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Drako.Api.DataStores
{
    public class UserDataStore
    {
        private readonly IOptions<DatabaseOptions> _options;

        public UserDataStore(IOptions<DatabaseOptions> options)
        {
            _options = options;
        }

        public async Task SaveUserAsync(string userTwitchId, string loginName, string displayName, string accessToken,
            string refreshToken, DateTime tokenExpiry)
        {
            const string sql = @"
                INSERT INTO Users (UserTwitchId, LoginName, DisplayName, AccessToken, RefreshToken, TokenExpiry, LastUpdated)
                SELECT @userTwitchId, @loginName, @displayName, @accessToken, @refreshToken, @tokenExpiry, @date
                ON CONFLICT (UserTwitchId) DO UPDATE
                SET LoginName = @loginName,
                    DisplayName = @displayName,
                    AccessToken = @accessToken,
                    RefreshToken = @refreshToken,
                    TokenExpiry = @tokenExpiry,
                    LastUpdated = @date
                ";
            
            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            await connection.ExecuteAsync(
                sql,
                new
                {
                    userTwitchId,
                    loginName,
                    displayName,
                    accessToken,
                    refreshToken,
                    tokenExpiry,
                    date = DateTime.UtcNow
                });
        }
        
        public async Task<long> GetCurrencyAsync(string userTwitchId)
        {
            const string sql = @"
                SELECT cuc.Balance
                FROM Users cuc
                WHERE cuc.UserTwitchId = @userTwitchId
                ";
            
            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            return await connection.ExecuteScalarAsync<long>(sql, new
            {
                userTwitchId,
            });
        }
        
        public async Task AddCurrencyAsync(string userTwitchId, int amount, string reason)
        {
            const string sql = @"
                WITH up AS (
                    INSERT INTO Users (Balance, UserTwitchId, LastUpdated)
                    SELECT @amount, @userTwitchId, @date
                    ON CONFLICT (UserTwitchId) DO UPDATE
                    SET Balance = ChannelUserCurrencies.Balance + @amount,
                        LastUpdated = @date
                    RETURNING UserTwitchId, Balance
                ), i AS (
                    INSERT INTO CurrencyTransactions (UserTwitchId, Date, Amount, Balance, Reason)
                    SELECT up.UserTwitchId, @date, @amount, up.Balance, @reason
                    FROM up
                )
                SELECT Balance FROM up;
                ";

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            await connection.QueryAsync(sql, new
            {
                userTwitchId,
                amount,
                reason,
                date = DateTime.UtcNow
            });
        }

        public async Task RemoveCurrencyAsync(string userTwitchId, int amount, string reason)
        {
            const string sql = @"
                WITH up AS (
                    UPDATE Users 
                    SET Balance = Balance - @amount,
                        LastUpdated = @date
                    WHERE UserTwitchId = @userTwitchId
                    RETURNING UserTwitchId, Balance
                ), i AS (
                    INSERT INTO CurrencyTransactions (UserTwitchId, Date, Amount, Balance, Reason)
                    SELECT up.UserTwitchId, @date, -@amount, up.Balance, reason
                    FROM up
                )
                SELECT UserTwitchId, Balance FROM up;
                ";

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            await connection.QueryAsync(sql, new
            {
                userTwitchId,
                amount,
                reason,
                date = DateTime.UtcNow
            });
        }
    }
}