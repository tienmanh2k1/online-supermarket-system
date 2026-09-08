using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

namespace OnlineSupermarket.Infrastructure.Recommendations;

public static class MfScorer
{
    private const int MinInteractionsForModel = 5;
    private const int NumberOfIterations = 100;
    private const int ApproximationRank = 16;
    private const float LearningRate = 0.1f;
    private const float Alpha = 0.01f;
    private const float Lambda = 0.025f;
    private const float C = 0.00001f;

    private static MLContext? _mlContext;
    private static ITransformer? _model;
    private static Dictionary<Guid, uint>? _userIdMapping;
    private static Dictionary<Guid, uint>? _productIdMapping;

    public static void EnsureModel(ScoringInput input, int seed = 42)
    {
        var (interactions, userMap, productMap) = CollectInteractionsWithMapping(input);
        if (interactions.Count < MinInteractionsForModel)
        {
            _model = null;
            _userIdMapping = null;
            _productIdMapping = null;
            return;
        }

        _mlContext = new MLContext(seed);
        var data = _mlContext.Data.LoadFromEnumerable(interactions.Select(r =>
            new RecRow { userId = r.UserId, productId = r.ProductId, label = r.Score }));

        var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("userIdKey", nameof(RecRow.userId))
            .Append(_mlContext.Transforms.Conversion.MapValueToKey("productIdKey", nameof(RecRow.productId)))
            .Append(_mlContext.Recommendation().Trainers.MatrixFactorization(new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = "userIdKey",
                MatrixRowIndexColumnName = "productIdKey",
                LabelColumnName = "label",
                NumberOfIterations = NumberOfIterations,
                LearningRate = LearningRate,
                LossFunction = MatrixFactorizationTrainer.LossFunctionType.SquareLossOneClass,
                Alpha = Alpha,
                Lambda = Lambda,
                C = C,
                ApproximationRank = ApproximationRank
            }));

        _model = pipeline.Fit(data);
        _userIdMapping = userMap;
        _productIdMapping = productMap;
    }

    public static bool IsModelTrained => _model != null;

    internal static Func<ScoringInput, Guid, Guid, decimal>? ScoreSeam { get; set; }

    public static decimal Score(ScoringInput input, Guid userId, Guid productId)
    {
        if (_mlContext == null || _model == null || _userIdMapping == null || _productIdMapping == null)
        {
            return 0m;
        }

        if (!_userIdMapping.TryGetValue(userId, out var uintUserId) ||
            !_productIdMapping.TryGetValue(productId, out var uintProductId))
        {
            return 0m;
        }

        try
        {
            if (ScoreSeam != null)
            {
                return ScoreSeam(input, userId, productId);
            }

            var probe = new[] { new RecRow { userId = uintUserId, productId = uintProductId, label = 0f } };
            var probeData = _mlContext.Data.LoadFromEnumerable(probe);
            var transformed = _model.Transform(probeData);
            var prediction = _mlContext.Data.CreateEnumerable<RecPrediction>(transformed, reuseRowObject: false).Single();

            var score = prediction.Score;
            if (float.IsNaN(score) || float.IsInfinity(score))
            {
                return 0m;
            }

            return (decimal)Math.Clamp(score, 0f, 1f);
        }
        catch
        {
            return 0m;
        }
    }

    public static void ClearModel()
    {
        _model = null;
        _mlContext = null;
        _userIdMapping = null;
        _productIdMapping = null;
        ScoreSeam = null;
    }

    private static (List<(uint UserId, uint ProductId, float Score)> Interactions, Dictionary<Guid, uint> UserMapping, Dictionary<Guid, uint> ProductMapping) CollectInteractionsWithMapping(ScoringInput input)
    {
        var interactions = new List<(uint UserId, uint ProductId, float Score)>();
        var userMapping = new Dictionary<Guid, uint>();
        var productMapping = new Dictionary<Guid, uint>();
        uint nextUserId = 0;
        uint nextProductId = 0;

        foreach (var purchase in input.Purchases)
        {
            if (!purchase.UserId.HasValue)
            {
                continue;
            }

            if (!userMapping.TryGetValue(purchase.UserId.Value, out var uId))
            {
                uId = nextUserId++;
                userMapping[purchase.UserId.Value] = uId;
            }

            if (!productMapping.TryGetValue(purchase.ProductId, out var pId))
            {
                pId = nextProductId++;
                productMapping[purchase.ProductId] = pId;
            }

            var weight = ViewWeight(purchase.CompletedAtUtc, input.NowUtc);
            interactions.Add((uId, pId, Math.Clamp(weight * purchase.Quantity * 3f, 1f, 5f)));
        }

        foreach (var view in input.Views)
        {
            if (!view.UserId.HasValue)
            {
                continue;
            }

            if (!userMapping.TryGetValue(view.UserId.Value, out var uId))
            {
                uId = nextUserId++;
                userMapping[view.UserId.Value] = uId;
            }

            if (!productMapping.TryGetValue(view.ProductId, out var pId))
            {
                pId = nextProductId++;
                productMapping[view.ProductId] = pId;
            }

            if (!interactions.Any(i => i.UserId == uId && i.ProductId == pId))
            {
                var weight = ViewWeight(view.ViewedAtUtc, input.NowUtc);
                interactions.Add((uId, pId, Math.Clamp(weight, 1f, 5f)));
            }
        }

        return (interactions, userMapping, productMapping);
    }

    private static float ViewWeight(DateTime atUtc, DateTime nowUtc)
    {
        var ageDays = (nowUtc.Date - atUtc.Date).Days;
        if (ageDays < 0) ageDays = 0;
        return 1f / (1f + ageDays);
    }

    private class RecRow
    {
        public uint userId;
        public uint productId;
        public float label;
    }

    private class RecPrediction
    {
        public float Score { get; set; }
    }
}
