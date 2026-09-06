using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Application.Common.Models;
using Tugu.Domain.Entities;

namespace Tugu.Application.Transactions;

/// <summary>Consultas de movimientos (historial). La escritura vive en el motor.</summary>
public class TransactionQueryService
{
    public const int MaxPageSize = 100;

    private readonly ITransactionRepository _transactions;
    private readonly IWalletRepository _wallets;

    public TransactionQueryService(ITransactionRepository transactions, IWalletRepository wallets)
    {
        _transactions = transactions;
        _wallets = wallets;
    }

    public async Task<Transaction> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _transactions.GetByIdAsync(id, ct)
               ?? throw new NotFoundException($"No existe una transacción con id {id}.");
    }

    public async Task<PagedResult<Transaction>> GetForWalletAsync(TransactionFilter filter, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();

        if (filter.Page < 1)
            errors["page"] = new[] { "page debe ser mayor o igual a 1." };
        if (filter.PageSize < 1 || filter.PageSize > MaxPageSize)
            errors["pageSize"] = new[] { $"pageSize debe estar entre 1 y {MaxPageSize}." };
        if (filter.From is not null && filter.To is not null && filter.From > filter.To)
            errors["from"] = new[] { "from no puede ser posterior a to." };

        if (errors.Count > 0)
            throw new ValidationException("Filtros de consulta inválidos.", errors);

        if (await _wallets.GetByIdAsync(filter.WalletId, ct) is null)
            throw new NotFoundException($"No existe una billetera con id {filter.WalletId}.");

        return await _transactions.QueryAsync(filter, ct);
    }
}
