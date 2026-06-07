using DonkeyWork.Recordings.Audio.Contracts.Models;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Mapping;
using DonkeyWork.Recordings.Identity.Contracts.Services;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.EntityFrameworkCore;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed class AudioCollectionService : IAudioCollectionService
{
    private readonly RecordingsDbContext _dbContext;
    private readonly IIdentityContext _identityContext;

    public AudioCollectionService(RecordingsDbContext dbContext, IIdentityContext identityContext)
    {
        _dbContext = dbContext;
        _identityContext = identityContext;
    }

    public async Task<ListAudioCollectionsResponseV1> ListAsync(int offset, int limit, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Collections.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip(Math.Max(0, offset))
            .Take(Math.Clamp(limit, 1, 200))
            .Select(c => new
            {
                Collection = c,
                RecordingCount = _dbContext.Recordings.Count(r => r.CollectionId == c.Id),
            })
            .ToListAsync(cancellationToken);

        return new ListAudioCollectionsResponseV1
        {
            Items = items.Select(x => x.Collection.ToV1(x.RecordingCount)).ToList(),
            TotalCount = totalCount,
        };
    }

    public async Task<AudioCollectionV1?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var collection = await _dbContext.Collections.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (collection is null)
        {
            return null;
        }

        var count = await _dbContext.Recordings.CountAsync(r => r.CollectionId == id, cancellationToken);
        return collection.ToV1(count);
    }

    public async Task<AudioCollectionV1> CreateAsync(CreateAudioCollectionRequestV1 request, CancellationToken cancellationToken = default)
    {
        if (!_identityContext.IsAuthenticated)
        {
            throw new InvalidOperationException("CreateAsync requires an authenticated identity.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Name is required.", nameof(request));
        }

        var entity = new TtsAudioCollectionEntity
        {
            UserId = _identityContext.UserId,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            CoverImagePath = request.CoverImagePath,
            DefaultVoice = request.DefaultVoice,
            DefaultLanguage = request.DefaultLanguage,
            Author = request.Author,
            AuthorEmail = request.AuthorEmail,
            ItunesCategory = request.ItunesCategory,
            IsExplicit = request.IsExplicit,
            Link = request.Link,
        };

        _dbContext.Collections.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToV1(recordingCount: 0);
    }

    public async Task<AudioCollectionV1?> UpdateAsync(Guid id, UpdateAudioCollectionRequestV1 request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Collections.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (request.Name is not null) entity.Name = request.Name;
        if (request.Description is not null) entity.Description = request.Description;
        if (request.CoverImagePath is not null) entity.CoverImagePath = request.CoverImagePath;
        if (request.DefaultVoice is not null) entity.DefaultVoice = request.DefaultVoice;
        if (request.DefaultLanguage is not null) entity.DefaultLanguage = request.DefaultLanguage;
        if (request.Author is not null) entity.Author = request.Author;
        if (request.AuthorEmail is not null) entity.AuthorEmail = request.AuthorEmail;
        if (request.ItunesCategory is not null) entity.ItunesCategory = request.ItunesCategory;
        if (request.IsExplicit.HasValue) entity.IsExplicit = request.IsExplicit.Value;
        if (request.Link is not null) entity.Link = request.Link;

        await _dbContext.SaveChangesAsync(cancellationToken);
        var count = await _dbContext.Recordings.CountAsync(r => r.CollectionId == id, cancellationToken);
        return entity.ToV1(count);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Collections.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _dbContext.Collections.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ListRecordingsResponseV1?> ListRecordingsAsync(Guid id, int offset, int limit, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.Collections.AnyAsync(c => c.Id == id, cancellationToken);
        if (!exists)
        {
            return null;
        }

        var query = _dbContext.Recordings.AsNoTracking().Where(r => r.CollectionId == id);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(r => r.SequenceNumber ?? int.MaxValue)
            .ThenBy(r => r.CreatedAt)
            .Skip(Math.Max(0, offset))
            .Take(Math.Clamp(limit, 1, 500))
            .Select(r => r.ToV1())
            .ToListAsync(cancellationToken);

        return new ListRecordingsResponseV1 { Items = items, TotalCount = totalCount };
    }
}
