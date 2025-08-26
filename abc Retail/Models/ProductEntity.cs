using Microsoft.Azure.Cosmos.Table;

namespace abcRetail.Models
{
    public class ProductEntity : TableEntity
    {
        public ProductEntity() { }

        
        public ProductEntity(string category, string productId)
        {
            PartitionKey = category;
            RowKey = productId;
        }

        public string ProductName { get; set; }
        public string Description { get; set; }
        public double Price { get; set; }
        public string ImageUrl { get; set; }
    }
}