using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Drako.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace Drako.Api.DataStores
{
    public class UnitOfWork : IAsyncDisposable 
    {
        private readonly IHubContext<UserHub, IUserHub> _userHub;
        private readonly List<Func<IHubContext<UserHub, IUserHub>, Task>> _commitActions = new();
        private readonly NpgsqlConnection _connection;
        private NpgsqlTransaction _transaction;

        private UnitOfWork(string connectionString, IHubContext<UserHub, IUserHub> userHub)
        {
            _userHub = userHub;
            _connection = new NpgsqlConnection(connectionString);
        }

        public static async Task<UnitOfWork> CreateInstance(string connectionString, IHubContext<UserHub, IUserHub> userHub)
        {
            var unitOfWork = new UnitOfWork(connectionString, userHub);
            await unitOfWork._connection.OpenAsync(); 
            unitOfWork._transaction = await unitOfWork._connection.BeginTransactionAsync(); 
            return unitOfWork;
        }

        public async Task<IEnumerable<dynamic>> QueryAsync(string sql, object parameters = null)
        {
            return await _connection.QueryAsync(sql, parameters, _transaction);
        }
        
        public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object parameters = null)
        {
            return await _connection.QueryAsync<T>(sql, parameters, _transaction);
        }

        public async Task<int> ExecuteAsync(string sql, object parameters = null)
        {
            return await _connection.ExecuteAsync(sql, parameters, _transaction);
        }

        public async Task<T> ExecuteScalarAsync<T>(string sql, object parameters = null)
        {
            return await _connection.ExecuteScalarAsync<T>(sql, parameters, _transaction);
        }

        public async Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object parameters = null)
        {
            return await _connection.QueryMultipleAsync(sql, parameters, _transaction);
        }
        
        public async Task CommitAsync() 
        { 
            try 
            { 
                await _transaction.CommitAsync();
                foreach (var commitAction in _commitActions)
                {
                    await commitAction(_userHub);
                }
            } 
            catch 
            { 
                await _transaction.RollbackAsync(); 
                throw; 
            } 
            finally 
            { 
                await _transaction.DisposeAsync(); 
            } 
        }

        public async ValueTask DisposeAsync()
        {
            if (_transaction != null) 
            { 
                await _transaction.DisposeAsync(); 
            } 
  
            if (_connection != null) 
            { 
                await _connection.DisposeAsync(); 
            } 
        }

        public void OnCommit(Func<IHubContext<UserHub, IUserHub>, Task> action)
        {
            _commitActions.Add(action);
        }
    }
}