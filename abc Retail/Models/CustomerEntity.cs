
using System.Collections.Concurrent;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.WindowsAzure.Storage.Table;

namespace abcRetail.Models
{
    public class CustomerEntity : Microsoft.Azure.Cosmos.Table.TableEntity
    {
        public CustomerEntity() { }

        public CustomerEntity(string customerId, string email)
        {
            PartitionKey = "Customers";
            RowKey = customerId;
            Email = email;
        }

        public string CustomerName { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
    }
}