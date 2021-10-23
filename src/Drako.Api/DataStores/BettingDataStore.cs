using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Drako.Api.Configuration;
using Drako.Api.Controllers;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Npgsql;

namespace Drako.Api.DataStores
{
    public class BettingDataStore
    {
        private readonly IOptions<DatabaseOptions> _options;

        public BettingDataStore(IOptions<DatabaseOptions> options)
        {
            _options = options;
        }
        
        public async Task<string> GetBettingStatusAsync()
        {
            const string sql = @"
                SELECT status FROM games g
                INNER JOIN junk j ON j.name = 'ActiveBetId' AND j.value = g.Id
            ";

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            return await connection.ExecuteScalarAsync<string>(sql, new
            {
            }) ?? BettingStatus.Canceled;
        }
        
        public async Task SetBettingStatusAsync(string status)
        {
            const string sql = @"
                WITH cg AS (
                    INSERT INTO games (status, options)
                    SELECT @status, NULL
                    WHERE 'Opening' = @status
                    RETURNING *
                )
                UPDATE junk
                SET value = cg.Id
                FROM cg
                WHERE name = 'ActiveBetId';

                UPDATE games g
                SET status = @status
                FROM junk j 
                WHERE 
                    j.name = 'ActiveBetId'
                    AND g.id = j.value;
            ";

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                status
            });

            if (rowsAffected == 0)
            {
                throw new Exception();
            }
        }

        public async Task<int?> GetWinnerAsync()
        {
            const string sql = @"
                        SELECT winner
                        FROM games cg
                        INNER JOIN junk j
                        ON j.name = 'ActiveBetId' AND j.value = cg.id
                    ";
            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            return await connection.ExecuteScalarAsync<int?>(sql);
        }
        
        public async Task<bool> HasUserAlreadyBetAsync(string userTwitchId)
        {
            const string sql = @"
                SELECT COUNT(cw.Id) FROM wagers cw
                INNER JOIN junk j ON j.name = 'ActiveBetId' AND j.value = cw.game_id
                WHERE cw.user_twitch_id = @userTwitchId;
            ";

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                userTwitchId
            }) > 0;
        }

        
        public async Task<BettingOption> SetWinnerAsync(
            int winner)
        {
            const string sql = @"
                        UPDATE games cg
                        SET winner = @winner
                        FROM junk j
                        WHERE j.name = 'ActiveBetId'
                        AND j.value = cg.Id
                    ";
            await using (var connection = new NpgsqlConnection(_options.Value.ConnectionString))
            {
                var rowsAffected = await connection.ExecuteAsync(sql, new
                {
                    winner
                });

                if (rowsAffected == 0)
                {
                    throw new Exception();
                }
            }

            return (await GetOptionsAsync()).First(x => x.Id == winner);
        }

        public async Task<IList<BettingOption>> GetOptionsAsync()
        {
            const string sql = @"
                SELECT Options
                FROM games cg
                INNER JOIN junk j ON j.name = 'ActiveBetId' AND j.value = cg.id
            ";

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            var result = await connection.ExecuteScalarAsync<string>(sql);

            if (result == null)
            {
                return new List<BettingOption>();
            }

            var options = JsonConvert.DeserializeObject<Tuple<string, int, int>[]>(result);

            return options
                .Select((x, i) => new BettingOption
                {
                    Id = i,
                    Description = x.Item1,
                    Odds = $"{x.Item2}:${x.Item3}"
                })
                .ToArray();
        }
        
        public async Task SetMaximumBetAsync(int maximumBet)
        {
            const string sql = @"
                UPDATE games g
                SET maximum_bet = @maximumBet
                FROM junk
                WHERE
                    j.name = 'ActiveBetId'
                    AND g.id = j.value;
            ";
            
            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                maximumBet
            });

            if (rowsAffected == 0)
            {
                throw new Exception();
            }
        }
        
        public async Task SetOptionsAsync(IList<BettingOption> options)
        {
            const string sql = @"
                UPDATE games cg
                SET Options = @options
                FROM junk j
                WHERE j.name = 'ActiveBetId'
                AND j.value = cg.id
            ";
            
            var fullOptions = options
                .Select(x => Tuple.Create(x.Description, x.OddsImpl.Numerator, x.OddsImpl.Denominator))
                .ToArray();

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                options = JsonConvert.SerializeObject(fullOptions)
            });

            if (rowsAffected == 0)
            {
                throw new Exception();
            }
        }
        
        public async Task RecordBetAsync(string userTwitchId, int optionId, int amount)
        {
            const string sql = @"
                INSERT INTO wagers (game_id, user_twitch_id, amount, option)
                SELECT j.value, @userTwitchId, @Amount, @OptionId
                FROM junk j
                WHERE j.name = 'ActiveBetId'
            ";

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                userTwitchId,
                amount,
                optionId
            });

            if (rowsAffected == 0)
            {
                throw new Exception();
            }
        }
        
        public async Task<IList<BetResource>> GetBetsAsync()
        {
            const string sql = @"
                SELECT user_twitch_id, amount, option 
                FROM wagers w
                INNER JOIN users u ON u.id = w.user_id
                INNER JOIN junk j on j.name = 'ActiveBetId'
                    AND j.value = w.game_id
            ";

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            var result = await connection.QueryAsync(sql);

            return result
                .Select(x => new BetResource
                {
                    Amount = x.Item1.amount,
                    OptionId = int.Parse(x.option),
                    UserTwitchId = x.usertwitchid
                })
                .ToList();
        }
        
    }
}