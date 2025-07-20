namespace BondsServer
{
    public class Bond
    {
        public required string id { get; set; }
        public int price { get; set; }
        public int faceValue { get; set; }
        public int coupon { get; set; }
        public int duration { get; set; }
    }

    public class BondWithStatistics : Bond
    {
        public float yield { get; set; }
    }

    public class BondUpdate
    {
        public required string bondId { get; set; }
        public required string serializedStatus { get; set; }
    }
}