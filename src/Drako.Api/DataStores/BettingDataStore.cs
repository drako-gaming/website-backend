using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Drako.Api.Controllers;

namespace Drako.Api.DataStores
{
    public class BettingDataStore
    {
        public async Task<BettingResource> GetLatestBetGameAsync(UnitOfWork uow, string twitchId)
        {
            const string sql = @"
                SELECT id FROM games
                ORDER BY id DESC
                LIMIT 1
            ";

            var result = await uow.ExecuteScalarAsync<long?>(sql);
            if (result.HasValue)
            {
                return await GetBetGameAsync(uow, result.Value, twitchId);
            }

            return new BettingResource
            {
                Id = 0,
                MaximumBet = null,
                Objective = null,
                Status = BettingStatus.Canceled,
                WinningOption = null,
                Options = new List<BettingOption>(),
                Total = 0L,
                AlreadyBet = false
            };
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

            var result = await uow.QueryMultipleAsync(sql, new { gameId });

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

            var rowsAffected = await uow.ExecuteAsync(
                sql,
                new
                {
                    status,
                    gameId
                }
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

            return await uow.ExecuteScalarAsync<int>(sql, new
            {
                userTwitchId,
                gameId
            }) > 0;
        }

        public async Task ResetWinnerAsync(UnitOfWork uow, long gameId)
        {
            const string sql = @"
                UPDATE games cg
                SET winner = NULL
                WHERE id = :gameId;

                UPDATE wagers
                SET awarded = 0
                WHERE game_id = :gameId;
            ";

            await uow.ExecuteAsync(sql, new { gameId });
        }

        public async Task<List<(string UserTwitchId, long Awarded)>> SetWinnerAsync(
            UnitOfWork uow,
            long gameId,
            long winner,
            decimal multiplier)
        {
            const string sql = @"
                        UPDATE games cg
                        SET winner = :winner
                        WHERE id = :gameId;
                        
                        WITH w AS (
                            UPDATE wagers
                            SET awarded = floor(:multiplier * wagers.amount)
                            WHERE game_id = :gameId
                            AND wagers.game_option_id = :winner
                            RETURNING user_id, awarded
                        )
                        SELECT user_twitch_id, awarded
                        FROM w
                        INNER JOIN users on users.id = w.user_id
                    ";

            var awarded = await uow.QueryAsync<(string user_twitch_id, long awarded)>(
                sql,
                new
                {
                    winner,
                    gameId,
                    multiplier
                }
            );

            return awarded
                .Select(x => (x.user_twitch_id, x.awarded))
                .ToList();
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

            var rowsAffected = await uow.ExecuteAsync(
                sql,
                new
                {
                    userTwitchId,
                    amount,
                    optionId,
                    gameId
                }
            );

            if (rowsAffected == 0)
            {
                throw new Exception();
            }
        }

        public async Task<IList<BetResource>> GetBetsAsync(UnitOfWork uow, long gameId)
        {
            const string sql = @"
                SELECT user_twitch_id, display_name, amount, awarded, game_option_id
                FROM wagers w
                INNER JOIN users u ON u.id = w.user_id
                WHERE game_id = :gameId
                ORDER BY amount DESC
            ";

            var result = await uow.QueryAsync(
                sql,
                new
                {
                    gameId
                }
            );

            return result
                .Select(x => new BetResource
                {
                    Amount = x.amount,
                    OptionId = x.game_option_id,
                    UserTwitchId = x.user_twitch_id,
                    UserTwitchDisplayName = x.display_name,
                    Awarded = x.awarded
                })
                .ToList();
        }
        
        public async Task<IList<BetResource>> GetBetsAsync(UnitOfWork uow, long gameId, GetBetsQuery query)
        {
            const string sqlTemplate = @"
                SELECT user_twitch_id, display_name, amount, awarded, game_option_id
                FROM wagers w
                INNER JOIN users u ON u.id = w.user_id
                /**where**/
                ORDER BY amount DESC
                LIMIT :limit OFFSET :offset
            ";

            var builder = new SqlBuilder();
            if (query.OptionId != null)
            {
                builder.Where("game_option_id = :optionId", new { query.OptionId });
            }

            if (query.UserId != null)
            {
                builder.Where("user_twitch_id = :userId", new { query.UserId });
            }

            builder.Where("game_id = :gameId", new { gameId });
            
            int limit = query.PageSize == 0 ? 20 : query.PageSize;
            int offset = query.PageNum == 0 ? 0 : (query.PageNum - 1) * limit;

            var sql = builder.AddTemplate(
                sqlTemplate,
                new
                {
                    limit,
                    offset
                }
            );
            
            var result = await uow.QueryAsync(
                sql.RawSql,
                sql.Parameters
            );

            return result
                .Select(x => new BetResource
                {
                    Amount = x.amount,
                    OptionId = x.game_option_id,
                    UserTwitchId = x.user_twitch_id,
                    UserTwitchDisplayName = x.display_name,
                    Awarded = x.awarded
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
            
            var gameId = await uow.ExecuteScalarAsync<long>(
                sql,
                new
                {
                    objective,
                    maximumBet,
                }
            );

            foreach (var option in options)
            {
                await uow.ExecuteAsync(
                    optionsSql,
                    new
                    {
                        gameId,
                        odds = option.Odds,
                        description = option.Description
                    }
                );
            }

            return gameId;
        }
    }
}