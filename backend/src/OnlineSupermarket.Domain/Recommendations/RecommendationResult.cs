using OnlineSupermarket.Domain.Common;

namespace OnlineSupermarket.Domain.Recommendations;

public sealed class RecommendationResult : Entity
{
    private RecommendationResult()
    {
    }

    private RecommendationResult(
        Guid id,
        RecommendationScope scope,
        string audienceKey,
        Guid? userId,
        Guid? sourceProductId,
        Guid recommendedProductId,
        decimal score,
        int rank,
        string reason,
        string algorithmVersion,
        DateTime generatedAtUtc,
        DateTime expiresAtUtc,
        Guid jobRunId)
        : base(id)
    {
        Scope = scope;
        AudienceKey = audienceKey;
        UserId = userId;
        SourceProductId = sourceProductId;
        RecommendedProductId = recommendedProductId;
        Score = score;
        Rank = rank;
        Reason = reason;
        AlgorithmVersion = algorithmVersion;
        GeneratedAtUtc = generatedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        JobRunId = jobRunId;
    }

    public RecommendationScope Scope { get; private set; }
    public string AudienceKey { get; private set; } = string.Empty;
    public Guid? UserId { get; private set; }
    public Guid? SourceProductId { get; private set; }
    public Guid RecommendedProductId { get; private set; }
    public decimal Score { get; private set; }
    public int Rank { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string AlgorithmVersion { get; private set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public Guid JobRunId { get; private set; }

    public static RecommendationResult CreateGlobal(
        Guid recommendedProductId,
        decimal score,
        int rank,
        string reason,
        string algorithmVersion,
        DateTime generatedAtUtc,
        DateTime expiresAtUtc,
        Guid jobRunId)
        => Create(
            RecommendationScope.Global,
            "global",
            null,
            null,
            recommendedProductId,
            score,
            rank,
            reason,
            algorithmVersion,
            generatedAtUtc,
            expiresAtUtc,
            jobRunId);

    public static RecommendationResult CreateForUser(
        Guid userId,
        Guid recommendedProductId,
        decimal score,
        int rank,
        string reason,
        string algorithmVersion,
        DateTime generatedAtUtc,
        DateTime expiresAtUtc,
        Guid jobRunId)
        => Create(
            RecommendationScope.User,
            $"user:{userId}",
            userId,
            null,
            recommendedProductId,
            score,
            rank,
            reason,
            algorithmVersion,
            generatedAtUtc,
            expiresAtUtc,
            jobRunId);

    public static RecommendationResult CreateSimilarProduct(
        Guid sourceProductId,
        Guid recommendedProductId,
        decimal score,
        int rank,
        string reason,
        string algorithmVersion,
        DateTime generatedAtUtc,
        DateTime expiresAtUtc,
        Guid jobRunId)
        => Create(
            RecommendationScope.SimilarProduct,
            $"product:{sourceProductId}",
            null,
            sourceProductId,
            recommendedProductId,
            score,
            rank,
            reason,
            algorithmVersion,
            generatedAtUtc,
            expiresAtUtc,
            jobRunId);

    private static RecommendationResult Create(
        RecommendationScope scope,
        string audienceKey,
        Guid? userId,
        Guid? sourceProductId,
        Guid recommendedProductId,
        decimal score,
        int rank,
        string reason,
        string algorithmVersion,
        DateTime generatedAtUtc,
        DateTime expiresAtUtc,
        Guid jobRunId)
    {
        if (scope == RecommendationScope.Global)
        {
            if (userId.HasValue || sourceProductId.HasValue)
            {
                throw new ArgumentException("Global results must not carry a user or source product.", nameof(scope));
            }
        }
        else if (scope == RecommendationScope.User)
        {
            if (!userId.HasValue || userId.Value == Guid.Empty)
            {
                throw new ArgumentException("User id is required for user results.", nameof(userId));
            }

            if (sourceProductId.HasValue)
            {
                throw new ArgumentException("User results must not carry a source product.", nameof(sourceProductId));
            }
        }
        else
        {
            if (!sourceProductId.HasValue || sourceProductId.Value == Guid.Empty)
            {
                throw new ArgumentException("Source product id is required for similar product results.", nameof(sourceProductId));
            }

            if (userId.HasValue)
            {
                throw new ArgumentException("Similar product results must not carry a user.", nameof(userId));
            }
        }

        if (recommendedProductId == Guid.Empty)
        {
            throw new ArgumentException("Recommended product id is required.", nameof(recommendedProductId));
        }

        if (rank <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), "Rank must be positive.");
        }

        if (score < 0 || score > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 0 and 1.");
        }

        var safeReason = Guard.Required(reason, nameof(reason));
        var safeAlgorithmVersion = Guard.Required(algorithmVersion, nameof(algorithmVersion));

        if (generatedAtUtc.Kind != DateTimeKind.Utc || expiresAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Generated and expiry timestamps must be UTC.", nameof(expiresAtUtc));
        }

        if (expiresAtUtc <= generatedAtUtc)
        {
            throw new ArgumentException("Expiry must be after generation time.", nameof(expiresAtUtc));
        }

        if (jobRunId == Guid.Empty)
        {
            throw new ArgumentException("Job run id is required.", nameof(jobRunId));
        }

        return new RecommendationResult(
            Guid.NewGuid(),
            scope,
            audienceKey,
            userId,
            sourceProductId,
            recommendedProductId,
            score,
            rank,
            safeReason,
            safeAlgorithmVersion,
            generatedAtUtc,
            expiresAtUtc,
            jobRunId);
    }
}