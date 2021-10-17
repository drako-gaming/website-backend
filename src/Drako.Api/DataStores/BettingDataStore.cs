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
                SELECT Status FROM ChannelGames g
                INNER JOIN Junk j ON j.Name = 'ActiveBetId' AND j.Value = g.Id
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
                    INSERT INTO ChannelGames (Status, Options)
                    SELECT @status, NULL
                    WHERE 'Opening' = @status
                    RETURNING *
                )
                UPDATE Junk
                SET Value = cg.Id
                FROM cg
                WHERE Name = 'ActiveBetId';

                UPDATE ChannelGames g
                SET Status = @status
                FROM Junk j 
                WHERE 
                    j.Name = 'ActiveBetId'
                    AND g.Id = j.Value;
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
                        SELECT Winner
                        FROM ChannelGames cg
                        INNER JOIN Junk j
                        ON j.Name = 'ActiveBetId' AND j.Value = cg.Id
                    ";
            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            return await connection.ExecuteScalarAsync<int?>(sql);
        }
        
        public async Task<bool> HasUserAlreadyBetAsync(string userTwitchId)
        {
            const string sql = @"
                SELECT COUNT(cw.Id) FROM ChannelWagers cw
                INNER JOIN Junk j ON j.Name = 'ActiveBetId' AND j.Value = cw.GameId
                WHERE cw.UserTwitchId = @userTwitchId;
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
                        UPDATE ChannelGames cg
                        SET Winner = @winner
                        FROM Junk j
                        WHERE j.Name = 'ActiveBetId'
                        AND j.Value = cg.Id
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
                FROM ChannelGames cg
                INNER JOIN Junk j ON j.Name = 'ActiveBetId' AND j.Value = cg.Id
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
                UPDATE ChannelGames g
                SET MaximumBet = @maximumBet
                FROM Junk
                WHERE
                    j.Name = 'ActiveBetId'
                    AND g.Id = j.Value;
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
                UPDATE ChannelGames cg
                SET Options = @options
                FROM Junk j
                WHERE j.Name = 'ActiveBetId'
                AND j.Value = cg.Id
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
                INSERT INTO ChannelWagers (GameId, UserTwitchId, Amount, Option)
                SELECT j.Value, @userTwitchId, @Amount, @OptionId
                FROM Junk j
                WHERE j.Name = 'ActiveBetId'
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
                SELECT cw.UserTwitchId, Amount, Option as Pick
                FROM ChannelWagers cw
                INNER JOIN Junk j on j.Name = 'ActiveBetId'
                    AND j.Value = cw.GameId
            ";

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            var result = await connection.QueryAsync(sql);

            return result
                .Select(x => new BetResource
                {
                    Amount = x.Item1.amount,
                    OptionId = int.Parse(x.pick),
                    UserTwitchId = x.usertwitchid
                })
                .ToList();
        }
        
    }
}