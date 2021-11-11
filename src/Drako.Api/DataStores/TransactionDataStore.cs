using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Drako.Api.Configuration;
using Drako.Api.Controllers.Transactions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Drako.Api.DataStores
{
    public class TransactionDataStore
    {
        private readonly IOptions<DatabaseOptions> _options;

        public TransactionDataStore(IOptions<DatabaseOptions> options)
        {
            _options = options;
        }

        public async Task<IList<Transaction>> GetTransactionsAsync(TransactionQueryParameters parameters)
        {
            const string sqlTemplate = "SELECT * FROM transactions /**where**/";

            var builder = new SqlBuilder();
            if (parameters.UserId != null)
            {
                builder = builder.Where("user_id = @userId", new { parameters.UserId });
            }

            if (parameters.UniqueId != null)
            {
                builder = builder.Where("unique_id = @uniqueId", new { parameters.UniqueId });
            }

            var sql = builder.AddTemplate(sqlTemplate);

            await using var connection = new NpgsqlConnection(_options.Value.ConnectionString);

            return connection.Query<Transaction>(sql.RawSql, sql.Parameters).ToList();
        }
    }
}