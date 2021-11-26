using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Drako.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace Drako.Api.DataStores
{
    public class UnitOfWork : IAsyncDisposable 
    {
        private readonly IHubContext<UserHub, IUserHub> _userHub;

        private readonly List<Func<IHubContext<UserHub, IUserHub>, Task>> _commitActions = new();
        
        public NpgsqlConnection Connection { get; }
        public NpgsqlTransaction Transaction { get; private set; }

        private UnitOfWork(string connectionString, IHubContext<UserHub, IUserHub> userHub)
        {
            _userHub = userHub;
            Connection = new NpgsqlConnection(connectionString);
        }

        public static async Task<UnitOfWork> CreateInstance(string connectionString, IHubContext<UserHub, IUserHub> userHub)
        {
            var unitOfWork = new UnitOfWork(connectionString, userHub);
            await unitOfWork.Connection.OpenAsync(); 
            unitOfWork.Transaction = await unitOfWork.Connection.BeginTransactionAsync(); 
            return unitOfWork;
        }

        public async Task CommitAsync() 
        { 
            try 
            { 
                await Transaction.CommitAsync();
                foreach (var commitAction in _commitActions)
                {
                    await commitAction(_userHub);
                }
            } 
            catch 
            { 
                await Transaction.RollbackAsync(); 
                throw; 
            } 
            finally 
            { 
                await Transaction.DisposeAsync(); 
            } 
        }

        public async ValueTask DisposeAsync()
        {
            if (Transaction != null) 
            { 
                await Transaction.DisposeAsync(); 
            } 
  
            if (Connection != null) 
            { 
                await Connection.DisposeAsync(); 
            } 
        }

        public void OnCommit(Func<IHubContext<UserHub, IUserHub>, Task> action)
        {
            _commitActions.Add(action);
        }
    }
}