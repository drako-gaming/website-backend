using System;

namespace Drako.Api.Tests.Support;

public static class Scrubbers
{
    public static void ScrubTransactions(dynamic transactions)
    {
        foreach (var transaction in transactions)
        {
            transaction.id = -1;
            transaction.date = DateTime.MinValue;
            transaction.balance = 0;
        }
    }
}