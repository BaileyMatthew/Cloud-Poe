// StorageService.cs
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Microsoft.Azure.Cosmos.Table;
using abcRetail.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public class StorageService
{
    private readonly string _connectionString;

    // Table Storage
    private CloudTableClient _tableClient;
    private CloudTable _customerTable;
    private CloudTable _productTable;

    // Blob Storage
    private BlobContainerClient _blobContainerClient;

    // Queue Storage
    private QueueClient _queueClient;

    // File Storage
    private ShareClient _shareClient;
    private ShareDirectoryClient _contractsDirectory;

    public StorageService(IConfiguration configuration)
    {
        _connectionString = configuration.GetValue<string>("AzureStorage:ConnectionString");

        // Initialize all clients and create containers/tables/queues on startup
        InitializeAzureClients().Wait();
    }

    private async Task InitializeAzureClients()
    {
        // Initialize Table Storage
        var storageAccount = CloudStorageAccount.Parse(_connectionString);
        _tableClient = storageAccount.CreateCloudTableClient();
        _customerTable = _tableClient.GetTableReference("Customers");
        _productTable = _tableClient.GetTableReference("Products");
        await _customerTable.CreateIfNotExistsAsync();
        await _productTable.CreateIfNotExistsAsync();

        // Initialize Blob Storage
        _blobContainerClient = new BlobContainerClient(_connectionString, "images");
        await _blobContainerClient.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);

        // Initialize Queue Storage
        _queueClient = new QueueClient(_connectionString, "order-queue");
        await _queueClient.CreateIfNotExistsAsync();

        // Initialize File Storage
        _shareClient = new ShareClient(_connectionString, "contracts");
        await _shareClient.CreateIfNotExistsAsync();
        _contractsDirectory = _shareClient.GetDirectoryClient("dummycontracts");
        await _contractsDirectory.CreateIfNotExistsAsync();
    }

    // --- Azure Table Storage Operations ---
    public async Task AddCustomerAsync(CustomerEntity customer)
    {
        var insertOperation = TableOperation.InsertOrReplace(customer);
        await _customerTable.ExecuteAsync(insertOperation);
    }

    public async Task<List<CustomerEntity>> GetCustomersAsync()
    {
        var query = new TableQuery<CustomerEntity>();
        var customers = new List<CustomerEntity>();
        TableContinuationToken token = null;

        do
        {
            var segment = await _customerTable.ExecuteQuerySegmentedAsync(query, token);
            token = segment.ContinuationToken;
            customers.AddRange(segment);
        } while (token != null);

        return customers;
    }

    public async Task AddProductAsync(ProductEntity product)
    {
        var insertOperation = TableOperation.InsertOrReplace(product);
        await _productTable.ExecuteAsync(insertOperation);
    }

    public async Task<List<ProductEntity>> GetProductsAsync()
    {
        var query = new TableQuery<ProductEntity>();
        var products = new List<ProductEntity>();
        TableContinuationToken token = null;

        do
        {
            var segment = await _productTable.ExecuteQuerySegmentedAsync(query, token);
            token = segment.ContinuationToken;
            products.AddRange(segment);
        } while (token != null);

        return products;
    }

    // --- Azure Blob Storage Operations ---
    public async Task<string> UploadImageAsync(string fileName, Stream fileStream)
    {
        var blobClient = _blobContainerClient.GetBlobClient(fileName);
        await blobClient.UploadAsync(fileStream, true);
        return blobClient.Uri.AbsoluteUri;
    }

    // --- Azure Queue Storage Operations ---
    public async Task AddOrderMessageAsync(string message)
    {
        await _queueClient.SendMessageAsync(message);
    }

    public async Task<string> ProcessNextOrderMessageAsync()
    {
        var message = await _queueClient.ReceiveMessageAsync();
        if (message.Value != null)
        {
            var messageText = message.Value.Body.ToString();
            await _queueClient.DeleteMessageAsync(message.Value.MessageId, message.Value.PopReceipt);
            return messageText;
        }
        return null;
    }

    // --- Azure File Storage Operations ---
    public async Task UploadContractAsync(string fileName, Stream fileStream)
    {
        var fileClient = _contractsDirectory.GetFileClient(fileName);
        await fileClient.CreateAsync(fileStream.Length);
        await fileClient.UploadAsync(fileStream);
    }

    public async Task<List<string>> ListContractsAsync()
    {
        var fileNames = new List<string>();
        await foreach (var item in _contractsDirectory.GetFilesAndDirectoriesAsync())
        {
            if (!item.IsDirectory)
            {
                fileNames.Add(item.Name);
            }
        }
        return fileNames;
    }
}