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
                    INSERT INTO ChannelUserCurrencies (Balance, UserTwitchId, LastUpdated)
                    SELECT @amount, @userTwitchId, @date
                    ON CONFLICT (UserTwitchId) DO UPDATE
                    SET Balance = ChannelUserCurrencies.Balance + @amount,
                        LastUpdated = @date
                    RETURNING UserTwitchId, Balance
                ), i AS (
                    INSERT INTO ChannelCurrencyTransactions (UserTwitchId, Date, Amount, Balance, Reason)
                    SELECT up.UserTwitchId, @date, @amount, up.Balance, @reason
                    FROM up
                )
                SELECT Balance FROM up;
                ";

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            connection.QueryAsync(sql, new
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
                    UPDATE ChannelUserCurrencies
                    SET Balance = Balance - @amount,
                        LastUpdated = @date
                    WHERE UserTwitchId = @userTwitchId
                    RETURNING UserTwitchId, Balance
                ), i AS (
                    INSERT INTO ChannelCurrencyTransactions (UserTwitchId, Date, Amount, Balance, Reason)
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