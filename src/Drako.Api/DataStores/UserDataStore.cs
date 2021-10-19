using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Drako.Api.Configuration;
using Drako.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Drako.Api.DataStores
{
    public class UserDataStore
    {
        private readonly IOptions<DatabaseOptions> _options;
        private readonly IHubContext<UserHub, IUserHub> _userHub;

        public UserDataStore(IOptions<DatabaseOptions> options, IHubContext<UserHub, IUserHub> userHub)
        {
            _options = options;
            _userHub = userHub;
        }

        public async Task SaveUserAsync(string userTwitchId, string loginName, string displayName)
        {
            const string sql = @"
                INSERT INTO Users (UserTwitchId, LoginName, DisplayName, LastUpdated)
                SELECT @userTwitchId, @loginName, @displayName, @date
                ON CONFLICT (UserTwitchId) DO UPDATE
                SET LoginName = @loginName,
                    DisplayName = @displayName,
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
                    RETURNING Id, Balance
                )
                SELECT Id, Balance FROM i;
                ";

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            var result = (await connection.QueryAsync(sql, new
            {
                userTwitchId,
                amount,
                reason,
                date = DateTime.UtcNow
            })).FirstOrDefault();

            await _userHub.Clients.User(userTwitchId).CurrencyUpdated(result.id, result.balance);
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
                    RETURNING Id, Balance
                )
                SELECT id, Balance FROM i;
                ";

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            var result = (await connection.QueryAsync(sql, new
            {
                userTwitchId,
                amount,
                reason,
                date = DateTime.UtcNow
            })).FirstOrDefault();
            
            await _userHub.Clients.User(userTwitchId).CurrencyUpdated(result.id, result.balance);
        }
    }
}