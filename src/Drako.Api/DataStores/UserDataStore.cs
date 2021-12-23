using System;
using System.Linq;
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
        
        public async Task<long> GetCurrencyAsync(UnitOfWork uow, string userTwitchId)
        {
            const string sql = @"
                SELECT cuc.balance
                FROM users cuc
                WHERE cuc.user_twitch_id = @userTwitchId
                ";

            return await uow.Connection.ExecuteScalarAsync<long>(sql, new
            {
                userTwitchId,
            }, uow.Transaction);
        }
        
        public async Task AddCurrencyAsync(UnitOfWork uow, string userTwitchId, long amount, string reason, string uniqueId = null)
        {
            const string sqlTemplate = @"
                WITH up AS (
                    INSERT INTO users (user_twitch_id, login_name, display_name, balance, last_updated)
                    SELECT @userTwitchId, 'user', 'user', @amount, @date
                    ON CONFLICT (user_twitch_id) DO UPDATE
                    SET balance = users.balance + @amount,
                        last_updated = @date
                    /**where**/
                    RETURNING id, balance
                ), i AS (
                    INSERT INTO transactions (user_id, date, amount, balance, reason, unique_id)
                    SELECT up.id, @date, @amount, up.balance, @reason, @uniqueId
                    FROM up
                    RETURNING id, balance
                )
                SELECT id, balance FROM i;
                ";

            var builder = new SqlBuilder();
            if (uniqueId != null)
            {
                builder.Where("NOT EXISTS (SELECT 1 FROM transactions WHERE unique_id = @uniqueId)");
            }

            var sql = builder.AddTemplate(sqlTemplate, new
            {
                userTwitchId,
                amount,
                reason,
                date = DateTime.UtcNow,
                uniqueId
            });
            var result = (
                await uow.Connection.QueryAsync(
                    sql.RawSql,
                    sql.Parameters,
                    uow.Transaction
                )
            ).SingleOrDefault();

            if (result != null)
            {
                uow.OnCommit(async hub =>
                    await hub.Clients.User(userTwitchId).CurrencyUpdated(result.id, result.balance)
                );
            }
        }

        public async Task RemoveCurrencyAsync(UnitOfWork uow, string userTwitchId, long amount, string reason)
        {
            const string sql = @"
                WITH up AS (
                    UPDATE users 
                    SET balance = balance - @amount,
                        last_updated = @date
                    WHERE user_twitch_id = @userTwitchId
                    RETURNING id, balance
                ), i AS (
                    INSERT INTO transactions (user_id, date, amount, balance, reason)
                    SELECT up.id, @date, -@amount, up.balance, @reason
                    FROM up
                    RETURNING id, balance
                )
                SELECT id, balance FROM i;
            ";

            var result = (
                await uow.Connection.QueryAsync(
                    sql,
                    new
                    {
                        userTwitchId,
                        amount,
                        reason,
                        date = DateTime.UtcNow
                    },
                    uow.Transaction
                )
            ).Single();

            uow.OnCommit(async hub => await hub.Clients.User(userTwitchId).CurrencyUpdated(result.id, result.balance));
        }
    }
}