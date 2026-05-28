namespace DonkeyWork.Recordings.Persistence.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
