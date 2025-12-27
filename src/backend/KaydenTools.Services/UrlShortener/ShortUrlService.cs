using Kayden.Commons.Common;
using Kayden.Commons.Interfaces;
using KaydenTools.Core.Common;
using KaydenTools.Core.Configuration.Settings;
using KaydenTools.Models.UrlShortener.Dtos;
using KaydenTools.Models.UrlShortener.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.Interfaces;
using NanoidDotNet;

namespace KaydenTools.Services.UrlShortener;

public class ShortUrlService : IShortUrlService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeService _dateTimeService;
    private readonly UrlShortenerSettings _settings;

    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public ShortUrlService(
        IUnitOfWork unitOfWork,
        IDateTimeService dateTimeService,
        UrlShortenerSettings settings)
    {
        _unitOfWork = unitOfWork;
        _dateTimeService = dateTimeService;
        _settings = settings;
    }

    public async Task<Result<ShortUrlDto>> CreateAsync(CreateShortUrlDto dto, Guid? ownerId, CancellationToken ct = default)
    {
        // 驗證 URL 格式
        if (!Uri.TryCreate(dto.OriginalUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return Result.Failure<ShortUrlDto>(ErrorCodes.InvalidUrlFormat, "Invalid URL format. Only http and https are allowed.");
        }

        // 驗證過期時間
        if (dto.ExpiresAt.HasValue)
        {
            var maxExpiration = _dateTimeService.UtcNow.AddDays(_settings.MaxTtlDays);
            if (dto.ExpiresAt.Value > maxExpiration)
            {
                return Result.Failure<ShortUrlDto>(ErrorCodes.ExpirationExceeded, $"Expiration cannot exceed {_settings.MaxTtlDays} days.");
            }
            if (dto.ExpiresAt.Value <= _dateTimeService.UtcNow)
            {
                return Result.Failure<ShortUrlDto>(ErrorCodes.ValidationError, "Expiration must be in the future.");
            }
        }

        // 產生或驗證短碼
        string shortCode;
        if (!string.IsNullOrEmpty(dto.CustomCode))
        {
            if (!IsValidCustomCode(dto.CustomCode))
            {
                return Result.Failure<ShortUrlDto>(ErrorCodes.InvalidShortCode, "Custom code contains invalid characters. Use only letters, numbers, and hyphens.");
            }
            if (dto.CustomCode.Length < 3 || dto.CustomCode.Length > 20)
            {
                return Result.Failure<ShortUrlDto>(ErrorCodes.InvalidShortCode, "Custom code must be between 3 and 20 characters.");
            }
            if (await _unitOfWork.ShortUrls.ShortCodeExistsAsync(dto.CustomCode, ct))
            {
                return Result.Failure<ShortUrlDto>(ErrorCodes.ShortCodeInUse, "This custom code is already in use.");
            }
            shortCode = dto.CustomCode;
        }
        else
        {
            shortCode = await GenerateUniqueShortCodeAsync(ct);
        }

        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            OriginalUrl = dto.OriginalUrl,
            ShortCode = shortCode,
            OwnerId = ownerId,
            ExpiresAt = dto.ExpiresAt,
            IsActive = true,
            ClickCount = 0
        };

        await _unitOfWork.ShortUrls.AddAsync(shortUrl, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(shortUrl);
    }

    public async Task<Result<ShortUrlDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var shortUrl = await _unitOfWork.ShortUrls.GetByIdAsync(id, ct);
        if (shortUrl == null)
        {
            return Result.Failure<ShortUrlDto>(ErrorCodes.ShortUrlNotFound, "Short URL not found.");
        }

        return MapToDto(shortUrl);
    }

    public async Task<Result<ShortUrlDto>> GetByShortCodeAsync(string shortCode, CancellationToken ct = default)
    {
        var shortUrl = await _unitOfWork.ShortUrls.GetByShortCodeAsync(shortCode, ct);
        if (shortUrl == null)
        {
            return Result.Failure<ShortUrlDto>(ErrorCodes.ShortUrlNotFound, "Short URL not found.");
        }

        return MapToDto(shortUrl);
    }

    public async Task<Result<IReadOnlyList<ShortUrlSummaryDto>>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default)
    {
        var shortUrls = await _unitOfWork.ShortUrls.GetByOwnerIdAsync(ownerId, ct);
        var now = _dateTimeService.UtcNow;

        var summaries = shortUrls.Select(s => new ShortUrlSummaryDto(
            s.Id,
            s.ShortCode,
            $"{_settings.BaseUrl}/{s.ShortCode}",
            s.ClickCount,
            s.CreatedAt,
            s.IsActive,
            s.ExpiresAt.HasValue && s.ExpiresAt.Value < now
        )).ToList();

        return summaries;
    }

    public async Task<Result<ShortUrlDto>> UpdateAsync(Guid id, UpdateShortUrlDto dto, Guid? userId, CancellationToken ct = default)
    {
        var shortUrl = await _unitOfWork.ShortUrls.GetByIdAsync(id, ct);
        if (shortUrl == null)
        {
            return Result.Failure<ShortUrlDto>(ErrorCodes.ShortUrlNotFound, "Short URL not found.");
        }

        // 檢查擁有權（如有 owner）
        if (shortUrl.OwnerId.HasValue && shortUrl.OwnerId != userId)
        {
            return Result.Failure<ShortUrlDto>(ErrorCodes.Forbidden, "You don't have permission to update this URL.");
        }

        if (dto.OriginalUrl != null)
        {
            if (!Uri.TryCreate(dto.OriginalUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                return Result.Failure<ShortUrlDto>(ErrorCodes.InvalidUrlFormat, "Invalid URL format.");
            }
            shortUrl.OriginalUrl = dto.OriginalUrl;
        }

        if (dto.ExpiresAt.HasValue)
        {
            var maxExpiration = _dateTimeService.UtcNow.AddDays(_settings.MaxTtlDays);
            if (dto.ExpiresAt.Value > maxExpiration)
            {
                return Result.Failure<ShortUrlDto>(ErrorCodes.ExpirationExceeded, $"Expiration cannot exceed {_settings.MaxTtlDays} days.");
            }
            shortUrl.ExpiresAt = dto.ExpiresAt;
        }

        if (dto.IsActive.HasValue)
        {
            shortUrl.IsActive = dto.IsActive.Value;
        }

        _unitOfWork.ShortUrls.Update(shortUrl);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(shortUrl);
    }

    public async Task<Result> DeleteAsync(Guid id, Guid? userId, CancellationToken ct = default)
    {
        var shortUrl = await _unitOfWork.ShortUrls.GetByIdAsync(id, ct);
        if (shortUrl == null)
        {
            return Result.Failure(ErrorCodes.ShortUrlNotFound, "Short URL not found.");
        }

        // 檢查擁有權（如有 owner）
        if (shortUrl.OwnerId.HasValue && shortUrl.OwnerId != userId)
        {
            return Result.Failure(ErrorCodes.Forbidden, "You don't have permission to delete this URL.");
        }

        _unitOfWork.ShortUrls.Remove(shortUrl);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<string>> ResolveAsync(string shortCode, CancellationToken ct = default)
    {
        var shortUrl = await _unitOfWork.ShortUrls.GetByShortCodeAsync(shortCode, ct);

        if (shortUrl == null)
        {
            return Result.Failure<string>(ErrorCodes.ShortUrlNotFound, "Short URL not found.");
        }

        if (!shortUrl.IsActive)
        {
            return Result.Failure<string>(ErrorCodes.ShortUrlDisabled, "This short URL is disabled.");
        }

        if (shortUrl.ExpiresAt.HasValue && shortUrl.ExpiresAt.Value < _dateTimeService.UtcNow)
        {
            return Result.Failure<string>(ErrorCodes.ShortUrlExpired, "This short URL has expired.");
        }

        return shortUrl.OriginalUrl;
    }

    public async Task<Result<UrlStatsDto>> GetStatsAsync(Guid id, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var shortUrl = await _unitOfWork.ShortUrls.GetByIdAsync(id, ct);
        if (shortUrl == null)
        {
            return Result.Failure<UrlStatsDto>(ErrorCodes.ShortUrlNotFound, "Short URL not found.");
        }

        var fromDate = from ?? DateOnly.FromDateTime(_dateTimeService.UtcNow.AddDays(-30));
        var toDate = to ?? DateOnly.FromDateTime(_dateTimeService.UtcNow);

        var clicksByDate = await _unitOfWork.UrlClicks.GetClicksByDateAsync(id, fromDate, toDate, ct);
        var lastClick = await _unitOfWork.UrlClicks.GetLastClickAtAsync(id, ct);
        var referrers = await _unitOfWork.UrlClicks.GetClicksByReferrerAsync(id, 10, ct);
        var devices = await _unitOfWork.UrlClicks.GetClicksByDeviceTypeAsync(id, ct);

        return new UrlStatsDto(
            shortUrl.Id,
            shortUrl.ShortCode,
            shortUrl.ClickCount,
            lastClick,
            clicksByDate.Select(x => new ClicksByDateDto(x.Key, x.Value)).OrderBy(x => x.Date).ToList(),
            referrers.Select(x => new ClicksByReferrerDto(x.Key, x.Value)).ToList(),
            devices.Select(x => new ClicksByDeviceDto(x.Key, x.Value)).ToList()
        );
    }

    private async Task<string> GenerateUniqueShortCodeAsync(CancellationToken ct)
    {
        var length = _settings.DefaultCodeLength;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            var code = Nanoid.Generate(Alphabet, length);

            if (!await _unitOfWork.ShortUrls.ShortCodeExistsAsync(code, ct))
            {
                return code;
            }

            // 如果碰撞率高，增加長度
            if (attempt > 5)
            {
                length++;
            }
        }

        throw new InvalidOperationException("Failed to generate unique short code after multiple attempts.");
    }

    private static bool IsValidCustomCode(string code)
    {
        return code.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
    }

    private ShortUrlDto MapToDto(ShortUrl entity)
    {
        return new ShortUrlDto(
            entity.Id,
            entity.OriginalUrl,
            entity.ShortCode,
            $"{_settings.BaseUrl}/{entity.ShortCode}",
            entity.ClickCount,
            entity.ExpiresAt,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt
        );
    }
}
