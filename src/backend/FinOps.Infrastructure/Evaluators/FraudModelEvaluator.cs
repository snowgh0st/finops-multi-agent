namespace FinOps.Infrastructure.Evaluators;

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

public class FraudModelEvaluator
{
    private readonly InferenceSession? _session;

    public FraudModelEvaluator(string modelPath)
    {
        try
        {
            if (File.Exists(modelPath) && new FileInfo(modelPath).Length > 0)
            {
                _session = new InferenceSession(modelPath);
            }
        }
        catch
        {
            // Fall back gracefully if ONNX file is corrupted/blank
            _session = null;
        }
    }

    public double PredictRiskScore(
        double amount,
        int count60s,
        double total60s,
        int count1h,
        double total1h,
        bool isGeoAnomaly)
    {
        // 1. Fallback heuristic if ONNX session isn't loaded
        if (_session == null)
        {
            return CalculateFallbackScore(amount, count60s, total60s, isGeoAnomaly);
        }

        try
        {
            // 2. Prepare ONNX Input Tensor
            var inputData = new float[]
            {
                (float)amount,
                (float)count60s,
                (float)total60s,
                (float)count1h,
                (float)total1h,
                isGeoAnomaly ? 1.0f : 0.0f
            };

            var inputTensor = new DenseTensor<float>(inputData, new[] { 1, 6 });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("float_input", inputTensor)
            };

            // 3. Run Inference and safely extract results
            using var results = _session.Run(inputs);
            if (results == null || !results.Any())
            {
                return CalculateFallbackScore(amount, count60s, total60s, isGeoAnomaly);
            }

            var outputTensor = results.FirstOrDefault()?.AsTensor<float>();
            if (outputTensor == null)
            {
                return CalculateFallbackScore(amount, count60s, total60s, isGeoAnomaly);
            }

            // Extract probability or binary class output
            float rawScore = outputTensor.First();
            return Math.Clamp((double)rawScore, 0.0, 1.0);
        }
        catch
        {
            return CalculateFallbackScore(amount, count60s, total60s, isGeoAnomaly);
        }
    }

    private static double CalculateFallbackScore(double amount, int count60s, double total60s, bool isGeoAnomaly)
    {
        double score = 0.10;
        if (amount > 50000) score += 0.35;
        if (count60s > 10) score += 0.25;
        if (total60s > 100000) score += 0.20;
        if (isGeoAnomaly) score += 0.30;

        return Math.Min(score, 0.99);
    }
}