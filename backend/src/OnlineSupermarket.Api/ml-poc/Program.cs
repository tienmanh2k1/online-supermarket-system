using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

var sw = System.Diagnostics.Stopwatch.StartNew();

// Fixed synthetic fixture: 5 users x 8 products, 30 interactions.
// Users 0..2 rate a subset; user 3 has 6 interactions (NOT a cold-start here —
// cold-start is a separate Gate B case with a brand-new user); user 4 includes
// product 7 candidate. Probes below include pairs never seen together in training.
var fixture = new (int User, int Product, float Score)[]
{
    (0,0,4.0f),(0,1,5.0f),(0,2,3.0f),(0,3,2.0f),
    (1,0,5.0f),(1,1,4.0f),(1,4,5.0f),(1,5,1.0f),
    (2,2,5.0f),(2,3,4.0f),(2,6,5.0f),(2,7,2.0f),
    (3,0,2.0f),(3,2,4.0f),(3,4,1.0f),(3,5,5.0f),(3,6,4.0f),(3,7,5.0f),
    (4,0,5.0f),(4,1,3.0f),(4,2,5.0f),
    (0,5,4.0f),(0,6,5.0f),
    (1,2,2.0f),(1,6,4.0f),
    (2,0,3.0f),(2,4,5.0f),
    (4,3,4.0f),(4,5,3.0f),(4,6,4.0f),
};
int n = fixture.Length;

var ml = new MLContext(seed: 42);

var data = ml.Data.LoadFromEnumerable(
    fixture.Select((r, i) => new RecRow { userId = (uint)r.User, productId = (uint)r.Product, label = r.Score }));

var split = ml.Data.TrainTestSplit(data, testFraction: 0.2, seed: 42);

try
{
    // Map raw uint ids to key columns the matrix-factorization trainer requires.
    var pipeline = ml.Transforms.Conversion.MapValueToKey(outputColumnName: "userIdKey", inputColumnName: nameof(RecRow.userId))
        .Append(ml.Transforms.Conversion.MapValueToKey(outputColumnName: "productIdKey", inputColumnName: nameof(RecRow.productId)))
        .Append(ml.Recommendation().Trainers.MatrixFactorization(new MatrixFactorizationTrainer.Options
        {
            MatrixColumnIndexColumnName = "userIdKey",
            MatrixRowIndexColumnName = "productIdKey",
            LabelColumnName = "label",
            NumberOfIterations = 100,
            LearningRate = 0.1,
            LossFunction = MatrixFactorizationTrainer.LossFunctionType.SquareLossOneClass,
            Alpha = 0.01,
            Lambda = 0.025,
            C = 0.00001,
            ApproximationRank = 16
        }));

    var model = pipeline.Fit(split.TrainSet);
    Console.WriteLine($"Fixture: {n} interactions, 5 users, 8 products");
    Console.WriteLine($"Model trained in {sw.Elapsed.TotalSeconds:F2}s on net10.0, ML.NET recommender");

    // Probe >= 5 user/product pairs (several never interacted as a pair).
    var probes = new (int User, int Product)[]
    {
        (0, 7), (1, 7), (2, 1), (4, 6), (3, 7), (0, 4),
    };

    int finite = 0;
    foreach (var (u, p) in probes)
    {
        var probeView = ml.Data.LoadFromEnumerable(new[] { new RecRow { userId = (uint)u, productId = (uint)p, label = 0f } });
        var transformed = model.Transform(probeView);
        var score = ml.Data.CreateEnumerable<RecPrediction>(transformed, reuseRowObject: false).Single().Score;
        bool isFinite = !float.IsNaN(score) && !float.IsInfinity(score);
        if (isFinite) finite++;
        Console.WriteLine($"user={u} product={p} score={score:F4} finite={isFinite}");
    }

    // Rerun in a fresh process is done by re-invoking this exe (see runner script).

    if (probes.Length >= 5 && finite == probes.Length)
    {
        Console.WriteLine($"POC_PASS: {finite} finite scores, {probes.Length} probes");
        return 0;
    }
    Console.WriteLine($"POC_FAIL: only {finite}/{probes.Length} finite scores");
    return 2;
}
catch (Exception ex)
{
    Console.WriteLine($"POC_FAIL_EXCEPTION: {ex.Message}");
    return 3;
}

public class RecRow
{
    public uint userId;
    public uint productId;
    public float label;
}

public class RecPrediction
{
    public float Score;
}
