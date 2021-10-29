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
                INSERT INTO users (user_twitch_id, login_name, display_name, last_updated)
                SELECT @userTwitchId, @loginName, @displayName, @date
                ON CONFLICT (user_twitch_id) DO UPDATE
                SET login_name = @loginName,
                    display_name = @displayName,
                    last_updated = @date
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

        public async Task<dynamic> GetUserAsync(string userTwitchId)
        {
            const string sql = @"
                SELECT u.login_name, u.display_name, u.balance, MAX(t.id) as last_transaction_id
                FROM users u
                LEFT JOIN transactions t ON t.user_id = t.id
                WHERE user_twitch_id = @userTwitchId
                GROUP BY u.login_name, u.display_name, u.balance
            ";

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            return await connection.QuerySingleAsync(sql, new { userTwitchId });
        }
        
        public async Task<long> GetCurrencyAsync(string userTwitchId)
        {
            const string sql = @"
                SELECT cuc.balance
                FROM users cuc
                WHERE cuc.user_twitch_id = @userTwitchId
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
                    INSERT INTO users (balance, user_twitch_id, last_updated)
                    SELECT @amount, @userTwitchId, @date
                    ON CONFLICT (user_twitch_id) DO UPDATE
                    SET balance = users.balance + @amount,
                        last_updated = @date
                    RETURNING id, balance
                ), i AS (
                    INSERT INTO transactions (user_id, date, amount, balance, reason)
                    SELECT up.id, @date, @amount, up.balance, @reason
                    FROM up
                    RETURNING id, balance
                )
                SELECT id, balance FROM i;
                ";

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            var result = (await connection.QueryAsync(sql, new
            {
                userTwitchId,
                amount,
                reason,
                date = DateTime.UtcNow
            })).First();

            await _userHub.Clients.User(userTwitchId).CurrencyUpdated(result.id, result.balance);
        }

        public async Task RemoveCurrencyAsync(string userTwitchId, int amount, string reason)
        {
            const string sql = @"
                WITH up AS (
                    UPDATE users 
                    SET balance = balance - @amount,
                        last_updated = @date
                    WHERE user_twitch_id = @userTwitchId
                    RETURNING user_twitch_id, balance
                ), i AS (
                    INSERT INTO transactions (user_id, date, amount, balance, reason)
                    SELECT up.id, @date, -@amount, up.balance, reason
                    FROM up
                    RETURNING id, balance
                )
                SELECT id, balance FROM i;
            ";

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            var result = (await connection.QueryAsync(sql, new
            {
                userTwitchId,
                amount,
                reason,
                date = DateTime.UtcNow
            })).First();
            
            await _userHub.Clients.User(userTwitchId).CurrencyUpdated(result.id, result.balance);
        }
    }
}