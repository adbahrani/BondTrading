using System.Text.Json.Serialization;

namespace BondsServer
{
    public class Bond
    {
        [JsonPropertyName("Id")]
        public required string id { get; set; }
        
        [JsonPropertyName("Price")]
        public int price { get; set; }
        
        [JsonPropertyName("FaceValue")]
        public int faceValue { get; set; }
        
        [JsonPropertyName("Coupon")]
        public int coupon { get; set; }
        
        [JsonPropertyName("Duration")]
        public int duration { get; set; }
    }

    public class BondWithStatistics
    {
        [JsonPropertyName("Bond")]
        public required Bond Bond { get; set; }
        
        [JsonPropertyName("Yield")]
        public float Yield { get; set; }
        
        [JsonPropertyName("YieldToMaturity")]
        public float YieldToMaturity { get; set; }
        
        [JsonPropertyName("ModifiedDuration")]
        public float ModifiedDuration { get; set; }
        
        [JsonPropertyName("DollarDuration")]
        public float DollarDuration { get; set; }
        
        [JsonPropertyName("Convexity")]
        public float Convexity { get; set; }
        
        [JsonPropertyName("LastUpdated")]
        public DateTime LastUpdated { get; set; }
    }

    public class BondUpdate
    {
        public required string bondId { get; set; }
        public required string serializedStatus { get; set; }
    }
}