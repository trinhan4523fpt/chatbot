using Chatbot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Common.Interfaces;

/// <summary>Mở rộng IAppDbContext với Payment entities.</summary>
public interface IPaymentDbContext
{
    // Payment / Token
    DbSet<TokenPackage> TokenPackages { get; }
    DbSet<StudentTokenOrder> StudentTokenOrders { get; }
    DbSet<StudentTokenWallet> StudentTokenWallets { get; }
    DbSet<TokenTransaction> TokenTransactions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
