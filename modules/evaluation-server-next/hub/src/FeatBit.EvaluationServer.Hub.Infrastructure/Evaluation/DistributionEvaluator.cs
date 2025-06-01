using System.Security.Cryptography;
using System.Text;
using FeatBit.EvaluationServer.Hub.Domain.Evaluation;

namespace FeatBit.EvaluationServer.Hub.Infrastructure.Evaluation;

public class DistributionEvaluator : IDistributionEvaluator
{
    private const int BucketSize = 10000;

    public Task<bool> EvaluateAsync(Guid distributionId, string userId)
    {
        // Generate a hash based on the distribution ID and user ID
        var key = $"{distributionId}:{userId}";
        var hash = ComputeHash(key);
        
        // Convert hash to a bucket value (0-9999)
        var bucket = hash % BucketSize;
        
        // For now, we'll use a fixed 50% distribution
        // In a real implementation, this would be configurable per distribution ID
        var threshold = BucketSize / 2;
        
        return Task.FromResult(bucket < threshold);
    }

    private static int ComputeHash(string input)
    {
        using var md5 = MD5.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = md5.ComputeHash(inputBytes);
        
        // Use the first 4 bytes of the hash as an integer
        return BitConverter.ToInt32(hashBytes, 0);
    }
} 