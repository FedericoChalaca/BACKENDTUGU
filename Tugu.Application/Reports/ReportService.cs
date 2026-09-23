using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Application.Common.Models;
using Tugu.Domain.Entities;

namespace Tugu.Application.Reports;

/// <summary>
/// Reportes sobre transacciones. Solo lectura. Un usuario ve su billetera; un
/// miembro de comercio ve la de su comercio y sus datáfonos. Sin identidad
/// real (Cognito) el alcance lo da el llamador; ver nota en el controller.
/// </summary>
public class ReportService
{
    public const int MaxPageSize = 100;
    public const int MaxRangeDays = 366;

    private readonly ITransactionRepository _transactions;
    private readonly IWalletRepository _wallets;
    private readonly ICompanyRepository _companies;
    private readonly IDeviceRepository _devices;

    public ReportService(
        ITransactionRepository transactions,
        IWalletRepository wallets,
        ICompanyRepository companies,
        IDeviceRepository devices)
    {
        _transactions = transactions;
        _wallets = wallets;
        _companies = companies;
        _devices = devices;
    }

    public async Task<TransactionSummary> SummaryAsync(ReportFilter filter, CancellationToken ct = default)
    {
        await ValidateAsync(filter, ct);
        return await _transactions.SummarizeAsync(filter, ct);
    }

    public async Task<PagedResult<Transaction>> TransactionsAsync(
        ReportFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        await ValidateAsync(filter, ct);

        var errors = new Dictionary<string, string[]>();
        if (page < 1) errors["page"] = new[] { "page debe ser mayor o igual a 1." };
        if (pageSize < 1 || pageSize > MaxPageSize)
            errors["pageSize"] = new[] { $"pageSize debe estar entre 1 y {MaxPageSize}." };
        if (errors.Count > 0) throw new ValidationException("Paginación inválida.", errors);

        return await _transactions.ReportAsync(filter, page, pageSize, ct);
    }

    private async Task ValidateAsync(ReportFilter f, CancellationToken ct)
    {
        if (f.WalletId is null && f.CompanyId is null && f.DeviceId is null)
            throw new ValidationException("Indica al menos uno de walletId, companyId o deviceId.");

        var errors = new Dictionary<string, string[]>();
        if (f.From is not null && f.To is not null)
        {
            if (f.From > f.To)
                errors["from"] = new[] { "from no puede ser posterior a to." };
            else if ((f.To.Value - f.From.Value).TotalDays > MaxRangeDays)
                errors["to"] = new[] { $"El rango máximo es de {MaxRangeDays} días." };
        }
        if (errors.Count > 0) throw new ValidationException("Filtros inválidos.", errors);

        if (f.WalletId is Guid w && await _wallets.GetByIdAsync(w, ct) is null)
            throw new NotFoundException($"No existe una billetera con id {w}.");
        if (f.CompanyId is Guid c && await _companies.GetByIdAsync(c, ct) is null)
            throw new NotFoundException($"No existe un comercio con id {c}.");
        if (f.DeviceId is Guid d && await _devices.GetByIdAsync(d, ct) is null)
            throw new NotFoundException($"No existe un dispositivo con id {d}.");
    }
}
