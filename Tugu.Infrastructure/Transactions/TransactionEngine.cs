using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tugu.Application.Common.Exceptions;
using Tugu.Application.Transactions;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;
using Tugu.Infrastructure.Persistence;

namespace Tugu.Infrastructure.Transactions;

/// <summary>
/// Implementación del motor sobre PostgreSQL.
///
/// Estrategia de concurrencia: lock pesimista de fila (SELECT ... FOR UPDATE)
/// sobre la billetera, dentro de una transacción SQL. Dos operaciones
/// simultáneas sobre la misma billetera se serializan: la segunda espera a que
/// la primera termine y lee el saldo ya actualizado.
///
/// Estrategia de idempotencia: verificación previa + índice único sobre
/// idempotency_key como red de seguridad. Si dos peticiones con la misma clave
/// entran a la vez, la segunda viola el índice al insertar, se revierte
/// completa (incluido el saldo) y devuelve la transacción original.
/// </summary>
public class TransactionEngine : ITransactionEngine
{
    private readonly TuguDbContext _db;

    public TransactionEngine(TuguDbContext db)
    {
        _db = db;
    }

    public async Task<TransactionResult> ExecuteAsync(TransactionCommand command, CancellationToken ct = default)
    {
        if (command.Amount <= 0)
            throw new ValidationException("El monto debe ser mayor que cero.");
        if (command.IdempotencyKey == Guid.Empty)
            throw new ValidationException("idempotencyKey es obligatoria y no puede ser un UUID vacío.");

        // Reintento con la misma clave: devolver la transacción original.
        var existing = await FindByKeyAsync(command.IdempotencyKey, ct);
        if (existing is not null)
            return new TransactionResult(EnsureSameOperation(existing, command), true);

        if (command.DeviceId is Guid deviceId &&
            await _db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deviceId, ct) is null)
        {
            throw new NotFoundException($"No existe un dispositivo con id {deviceId}.");
        }

        await using var dbTransaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Lock de fila: nadie más puede tocar esta billetera hasta el commit.
            var wallet = await _db.Wallets
                .FromSqlInterpolated($@"SELECT * FROM wallets WHERE ""Id"" = {command.WalletId} FOR UPDATE")
                .FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException($"No existe una billetera con id {command.WalletId}.");

            if (wallet.Status != WalletStatus.Active)
                throw new ConflictException($"La billetera no está activa (estado: {wallet.Status}).");

            // Un usuario bloqueado no puede mover saldo aunque su billetera
            // siga activa. (PendingVerification sí puede recargar: el bloqueo
            // de retiros por KYC incompleto se define en la fase de retiro.)
            var owner = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == wallet.UserId, ct)
                ?? throw new InvalidOperationException(
                    $"Inconsistencia: la billetera {wallet.Id} no tiene usuario dueño.");

            if (owner.Status == UserStatus.Blocked)
                throw new ConflictException("El usuario dueño de la billetera está bloqueado.");

            var newBalance = command.Type switch
            {
                TransactionType.Recharge => wallet.Balance + command.Amount,
                TransactionType.Withdrawal => wallet.Balance >= command.Amount
                    ? wallet.Balance - command.Amount
                    : throw new InsufficientFundsException(
                        $"Saldo insuficiente: disponible {wallet.Balance:0.##}, solicitado {command.Amount:0.##}."),
                _ => throw new ValidationException($"Tipo de transacción no soportado: {command.Type}.")
            };

            wallet.Balance = newBalance;
            wallet.UpdatedAt = DateTime.UtcNow;

            var transaction = new Transaction
            {
                WalletId = wallet.Id,
                Type = command.Type,
                Amount = command.Amount,
                BalanceAfter = newBalance,
                Status = TransactionStatus.Completed,
                IdempotencyKey = command.IdempotencyKey,
                Reference = command.Reference,
                DeviceId = command.DeviceId,
                CreatedBy = command.CreatedBy
            };

            _db.Transactions.Add(transaction);
            await _db.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            return new TransactionResult(transaction, false);
        }
        catch (DbUpdateException ex) when (IsIdempotencyKeyViolation(ex))
        {
            // Carrera: otra petición con la misma clave ganó. Todo lo nuestro
            // (incluido el cambio de saldo) se revierte y devolvemos la original.
            await dbTransaction.RollbackAsync(ct);
            _db.ChangeTracker.Clear();

            var winner = await FindByKeyAsync(command.IdempotencyKey, ct)
                ?? throw new InvalidOperationException(
                    "Violación de idempotencia sin transacción existente: inconsistencia inesperada.");

            return new TransactionResult(EnsureSameOperation(winner, command), true);
        }
    }

    private Task<Transaction?> FindByKeyAsync(Guid idempotencyKey, CancellationToken ct) =>
        _db.Transactions.AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);

    /// <summary>
    /// Una clave reutilizada con parámetros distintos es un bug del cliente,
    /// no un reintento: se rechaza en vez de devolver una transacción ajena.
    /// </summary>
    private static Transaction EnsureSameOperation(Transaction existing, TransactionCommand command)
    {
        if (existing.WalletId != command.WalletId ||
            existing.Type != command.Type ||
            existing.Amount != command.Amount)
        {
            throw new ConflictException(
                "La idempotencyKey ya fue usada para una operación diferente. Genera una clave nueva.");
        }

        return existing;
    }

    private static bool IsIdempotencyKeyViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg &&
        pg.SqlState == PostgresErrorCodes.UniqueViolation &&
        pg.ConstraintName is not null &&
        pg.ConstraintName.Contains("IdempotencyKey", StringComparison.OrdinalIgnoreCase);
}
