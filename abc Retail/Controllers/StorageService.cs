using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Microsoft.Azure.Cosmos.Table;
using abcRetail.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System;
using Azure.Storage.Sas;

public class StorageService
{
    private readonly string _connectionString;

    private CloudTableClient _tableClient;
    private CloudTable _customerTable;
    private CloudTable _productTable;

    private BlobContainerClient _blobContainerClient;

    private QueueClient _queueClient;

    private ShareClient _shareClient;
    private ShareDirectoryClient _contractsDirectory;

    public StorageService(IConfiguration configuration)
    {
        _connectionString = configuration.GetValue<string>("AzureStorage:ConnectionString");

        InitializeAzureClients().Wait();
    }

    private async Task InitializeAzureClients()
    {
        var storageAccount = CloudStorageAccount.Parse(_connectionString);
        _tableClient = storageAccount.CreateCloudTableClient();
        _customerTable = _tableClient.GetTableReference("Customers");
        _productTable = _tableClient.GetTableReference("Products");
        await _customerTable.CreateIfNotExistsAsync();
        await _productTable.CreateIfNotExistsAsync();

        _blobContainerClient = new BlobContainerClient(_connectionString, "images");
        await _blobContainerClient.CreateIfNotExistsAsync();

        _queueClient = new QueueClient(_connectionString, "order-queue");
        await _queueClient.CreateIfNotExistsAsync();

        _shareClient = new ShareClient(_connectionString, "contracts");
        await _shareClient.CreateIfNotExistsAsync();
        _contractsDirectory = _shareClient.GetDirectoryClient("dummycontracts");
        await _contractsDirectory.CreateIfNotExistsAsync();
    }

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

    public async Task<string> UploadImageAsync(string fileName, Stream fileStream)
    {
        var blobClient = _blobContainerClient.GetBlobClient(fileName);
        await blobClient.UploadAsync(fileStream, true);

        var sasBuilder = new BlobSasBuilder()
        {
            BlobContainerName = _blobContainerClient.Name,
            BlobName = blobClient.Name,
            Resource = "b",
            StartsOn = System.DateTimeOffset.UtcNow,
            ExpiresOn = System.DateTimeOffset.UtcNow.AddHours(24)
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);

        return sasUri.AbsoluteUri;
    }

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