using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Drako.Api.Configuration;
using Drako.Api.Controllers;
using Microsoft.Extensions.Options;
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
        
        public async Task<BettingResource> GetBetGameAsync(UnitOfWork uow, long gameId, string userTwitchId)
        {
            const string sql = @"
                SELECT game_options.id, description, odds, MAX(wagers.id) AS latest_wager_id, CAST(SUM(amount) AS BIGINT) AS total
                FROM game_options
                LEFT JOIN wagers ON wagers.game_option_id = game_options.id
                WHERE game_options.game_id = :gameId
                GROUP BY game_options.id
                ORDER BY game_options.id;

                SELECT g.id, status, winner, maximum_bet, objective, winner, CAST(SUM(amount) AS BIGINT) AS total
                FROM games g 
                LEFT JOIN wagers ON wagers.game_id = g.id
                WHERE g.id = :gameId
                GROUP BY g.id;
            ";

            var result = await uow.Connection.QueryMultipleAsync(sql, new { gameId }, uow.Transaction);

            var options = await result.ReadAsync();
            var resource = await result.ReadFirstAsync();
            var showOptionTotals = resource.status != "Open";

            return new BettingResource
            {
                Id = gameId,
                MaximumBet = resource.maximum_bet,
                Objective = resource.objective,
                Status = resource.status,
                WinningOption = resource.winner,
                Options = options.Select(
                    x => new BettingOption
                    {
                        Id = x.id,
                        Description = x.description,
                        Odds = x.odds,
                        Total = showOptionTotals ? x.total ?? 0L : 0L
                    }
                ).ToList(),
                Total = resource.total ?? 0L,
                AlreadyBet = await HasUserAlreadyBetAsync(uow, gameId, userTwitchId)
            };
        }

        public async Task SetBettingStatusAsync(UnitOfWork uow, long gameId, string status)
        {
            const string sql = @"
                UPDATE games g
                SET status = :status
                WHERE g.id = :gameId;
            ";

            var rowsAffected = await uow.Connection.ExecuteAsync(
                sql,
                new
                {
                    status,
                    gameId
                },
                uow.Transaction
            );

            if (rowsAffected == 0)
            {
                throw new Exception();
            }
        }

        public async Task<bool> HasUserAlreadyBetAsync(UnitOfWork uow, long gameId, string userTwitchId)
        {
            const string sql = @"
                SELECT COUNT(cw.Id) FROM wagers cw
                INNER JOIN users u ON cw.user_id = u.id
                WHERE u.user_twitch_id = :userTwitchId AND cw.game_id = :gameId;
            ";

            return await uow.Connection.ExecuteScalarAsync<int>(sql, new
            {
                userTwitchId,
                gameId
            }, uow.Transaction) > 0;
        }

        
        public async Task SetWinnerAsync(
            UnitOfWork uow,
            long gameId,
            long winner)
        {
            const string sql = @"
                        UPDATE games cg
                        SET winner = :winner
                        WHERE id = :gameId
                    ";

            var rowsAffected = await uow.Connection.ExecuteAsync(
                sql,
                new
                {
                    winner,
                    gameId
                }, uow.Transaction
            );

            if (rowsAffected == 0)
            {
                throw new Exception();
            }
        }

        public async Task RecordBetAsync(UnitOfWork uow, long gameId, string userTwitchId, long optionId, long amount)
        {
            const string sql = @"
                WITH w AS (
                    INSERT INTO wagers (game_id, user_id, amount, game_option_id)
                    SELECT :gameId, u.id, :Amount, :OptionId
                    FROM users u
                    WHERE u.user_twitch_id = :userTwitchId
                    RETURNING wagers.id
                )
                SELECT MAX(id) as maxId, SUM(amount) as total
                FROM wagers w1
                WHERE game_id = :gameId;
            ";

            var rowsAffected = await uow.Connection.ExecuteAsync(
                sql,
                new
                {
                    userTwitchId,
                    amount,
                    optionId,
                    gameId
                },
                uow.Transaction
            );

            if (rowsAffected == 0)
            {
                throw new Exception();
            }
        }
        
        public async Task<IList<BetResource>> GetBetsAsync(UnitOfWork uow, long gameId)
        {
            const string sql = @"
                SELECT user_twitch_id, amount, game_option_id
                FROM wagers w
                INNER JOIN users u ON u.id = w.user_id
                WHERE game_id = :gameId
            ";

            var result = await uow.Connection.QueryAsync(sql, new { gameId }, uow.Transaction);

            return result
                .Select(x => new BetResource
                {
                    Amount = x.amount,
                    OptionId = x.game_option_id,
                    UserTwitchId = x.user_twitch_id
                })
                .ToList();
        }

        public async Task<IList<BetSummaryResource>> GetBetsSummaryAsync(long gameId, bool groupByOption)
        {
            const string sqlTemplate = @"
                SELECT /**select**/
                FROM wagers w 
                WHERE game_id = :gameId
                /**groupby**/
            ";
            
            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);
            var builder = new SqlBuilder();
            builder.Select("MAX(w.id) maximum_wager_id");
            builder.Select("SUM(w.amount) total");
            builder.AddParameters(new { gameId });
            if (groupByOption)
            {
                builder.Select("option");
                builder.GroupBy("option");
            }

            var sql = builder.AddTemplate(sqlTemplate);
            return connection.Query(sql.RawSql, sql.Parameters)
                .Select(x => new BetSummaryResource
                {
                    Amount = x.total,
                    MaximumWagerId = x.maximum_wager_id,
                    OptionId = x.option
                })
                .ToList();
        }

        public async Task<long> NewBettingGame(UnitOfWork uow, string objective, long? maximumBet, IList<BettingOption> options)
        {
            const string sql = @"
                INSERT INTO games (objective, status, maximum_bet)
                SELECT :objective, 'Open', :maximumBet
                RETURNING id
            ";

            const string optionsSql = @"
                INSERT INTO game_options(game_id, odds, description)
                VALUES (:gameId, :odds, :description);
            ";
            
            var gameId = await uow.Connection.ExecuteScalarAsync<long>(
                sql,
                new
                {
                    objective,
                    maximumBet,
                },
                uow.Transaction
            );

            foreach (var option in options)
            {
                await uow.Connection.ExecuteAsync(
                    optionsSql,
                    new
                    {
                        gameId,
                        odds = option.Odds,
                        description = option.Description
                    },
                    uow.Transaction
                );
            }

            return gameId;
        }
    }
}