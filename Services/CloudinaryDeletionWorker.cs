using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using LoyalAnimal.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace LoyalAnimal.Server.Services;

public sealed class CloudinaryDeletionWorker : BackgroundService
{
    private readonly string _workerId = Guid.NewGuid().ToString("N");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CloudinaryDeletionWorker> _logger;

    public CloudinaryDeletionWorker(IServiceScopeFactory scopeFactory, ILogger<CloudinaryDeletionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cloudinary temizleme kuyruğunda hata.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var jobs = await db.CloudinaryDeletionJobs
            .Where(x => x.NextAttemptAtUtc <= DateTime.UtcNow)
            .OrderBy(x => x.Id)
            .Take(10)
            .ToListAsync(cancellationToken);

        if (jobs.Count == 0)
            return;

        var cloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME");
        var apiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY");
        var apiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET");

        if (string.IsNullOrWhiteSpace(cloudName) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(apiSecret))
        {
            _logger.LogWarning("Cloudinary ayarları eksik. Temizleme ertelendi.");
            return;
        }

        var cloudinary = new Cloudinary(
            new Account(cloudName.Trim(), apiKey.Trim(), apiSecret.Trim()));

        foreach (var job in jobs)
        {
            var now = DateTime.UtcNow;
            var leaseUntil = now.AddMinutes(60);

            var claimed = await db.CloudinaryDeletionJobs
                .Where(x => x.Id == job.Id
                    && x.NextAttemptAtUtc <= now
                    && (x.LeaseExpiresAtUtc == null
                        || x.LeaseExpiresAtUtc <= now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.LeaseOwner, _workerId)
                    .SetProperty(x => x.LeaseExpiresAtUtc, leaseUntil),
                    cancellationToken);

            if (claimed != 1)
                continue;

            job.LeaseOwner = _workerId;
            job.LeaseExpiresAtUtc = leaseUntil;

            try
            {
                string? cursor = null;
                var seenCursors = new HashSet<string>();

                do
                {
                    var renewedAt = DateTime.UtcNow;
                    var renewed = await db.CloudinaryDeletionJobs
                        .Where(x => x.Id == job.Id && x.LeaseOwner == _workerId && x.LeaseExpiresAtUtc > renewedAt)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(x => x.LeaseExpiresAtUtc, renewedAt.AddMinutes(60)), cancellationToken);
                    if (renewed != 1)
                        throw new InvalidOperationException("Cloudinary görev kilidi yenilenemedi.");
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromMinutes(2));
                    var result = await cloudinary.DeleteResourcesAsync(new DelResParams
                    {
                        Prefix = job.PublicIdPrefix,
                        Invalidate = true,
                        NextCursor = cursor
                    }, timeout.Token);

                    if (result == null || result.Error != null)
                        throw new InvalidOperationException("Cloudinary silme hatası.");

                    cursor = result.NextCursor;

                    if (result.Partial && string.IsNullOrEmpty(cursor))
                        throw new InvalidOperationException("Cloudinary silme işlemi eksik.");

                    if (!string.IsNullOrEmpty(cursor) && !seenCursors.Add(cursor))
                        throw new InvalidOperationException("Cloudinary sayfalama döngüsü.");

                } while (!string.IsNullOrEmpty(cursor));

                var deleted = await db.CloudinaryDeletionJobs
                    .Where(x => x.Id == job.Id
                        && x.LeaseOwner == _workerId
                        && x.LeaseExpiresAtUtc > DateTime.UtcNow)
                    .ExecuteDeleteAsync(cancellationToken);

                if (deleted != 1)
                    throw new InvalidOperationException(
                        "Görev kilidi kaybedildi; kayıt silinmedi.");

                db.Entry(job).State = EntityState.Detached;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                job.AttemptCount++;

                var retryMinutes = Math.Min(1440,
                    Math.Pow(2, Math.Min(job.AttemptCount, 10)));

                job.NextAttemptAtUtc =
                    DateTime.UtcNow.AddMinutes(retryMinutes);

                await db.CloudinaryDeletionJobs
                    .Where(x => x.Id == job.Id
                        && x.LeaseOwner == _workerId
                        && x.LeaseExpiresAtUtc > DateTime.UtcNow)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.AttemptCount, job.AttemptCount)
                        .SetProperty(x => x.NextAttemptAtUtc, job.NextAttemptAtUtc)
                        .SetProperty(x => x.LeaseOwner, (string?)null)
                        .SetProperty(x => x.LeaseExpiresAtUtc, (DateTime?)null),
                        cancellationToken);

                db.Entry(job).State = EntityState.Detached;

                _logger.LogWarning(ex,
                    "Cloudinary silme başarısız. Görev: {JobId}", job.Id);
            }

        }
    }
}
