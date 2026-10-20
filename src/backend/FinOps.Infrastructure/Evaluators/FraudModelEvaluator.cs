using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FinOps.Infrastructure.Evaluators;

public class FraudModelEvaluator : IDisposable
{
    private readonly InferenceSession? _session;

    public FraudModelEvaluator(string modelPath)
    {
        // Safely initialize InferenceSession if the model exists on disk
        if (File.Exists(modelPath))
        {
            _session = new InferenceSession(modelPath);
        }
    }

    public double PredictRiskScore(double amount, int count60s, double total60s, int count1h, double total1h, bool isGeoAnomaly)
    {
        if (_session == null)
        {
            // Fallback rule-based score if ONNX model file is not present
            return isGeoAnomaly || total60s > 10000 ? 0.92 : 0.15;
        }

        // 1. Prepare feature vector matching ONNX model input shape: [1, 6]
        float[] featureValues = new float[]
        {
            (float)amount,
            count60s,
            (float)total60s,
            count1h,
            (float)total1h,
            isGeoAnomaly ? 1.0f : 0.0f
        };

        // 2. Wrap float array into an ONNX Tensor (batch_size = 1, feature_count = 6)
        var inputTensor = new DenseTensor<float>(featureValues, new int[] { 1, 6 });

        // 3. Bind input to model tensor input name
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("float_input", inputTensor)
        };

        // 4. Run inference
        using var results = _session.Run(inputs);

        // 5. Extract output probabilities
        var outputTensor = results.First(r => r.Name == "probabilities" || r.Name == "output_probability")
                                  .AsEnumerable<float>()
                                  .ToArray();

        return outputTensor.Length > 1 ? outputTensor[1] : outputTensor[0];
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}