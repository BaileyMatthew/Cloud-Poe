using abcRetail.Models;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.IO;

public class StoreController : Controller
{
    private readonly StorageService _storageService;

    public StoreController(StorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<IActionResult> Customers()
    {
        var customers = await _storageService.GetCustomersAsync();
        return View(customers);
    }

    [HttpPost]
    public async Task<IActionResult> AddCustomer(CustomerEntity customer)
    {
        if (ModelState.IsValid)
        {
            customer.PartitionKey = "Customers";
            customer.RowKey = Guid.NewGuid().ToString();
            await _storageService.AddCustomerAsync(customer);
        }
        return RedirectToAction("Customers");
    }

    public async Task<IActionResult> Products()
    {
        var products = await _storageService.GetProductsAsync();
        return View(products);
    }

    [HttpPost]
    public async Task<IActionResult> AddProduct(ProductEntity product, IFormFile ImageFile)
    {
        if (ModelState.IsValid && ImageFile != null && ImageFile.Length > 0)
        {
            product.PartitionKey = "Products";
            product.RowKey = Guid.NewGuid().ToString();

            using (var stream = ImageFile.OpenReadStream())
            {
                var uniqueFileName = $"{Guid.NewGuid()}_{ImageFile.FileName}";
                product.ImageUrl = await _storageService.UploadImageAsync(uniqueFileName, stream);
            }

            await _storageService.AddProductAsync(product);
        }
        return RedirectToAction("Products");
    }

    public IActionResult Ques()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> EnqueueMessage(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            await _storageService.AddOrderMessageAsync(message);
        }
        return View("Ques");
    }

    [HttpPost]
    public async Task<IActionResult> DequeueMessage()
    {
        var message = await _storageService.ProcessNextOrderMessageAsync();
        return View("Ques", message);
    }

    public async Task<IActionResult> Contracts()
    {
        var fileNames = await _storageService.ListContractsAsync();
        return View(fileNames);
    }

    [HttpPost]
    public async Task<IActionResult> UploadContract(IFormFile File)
    {
        if (File != null && File.Length > 0)
        {
            using (var stream = File.OpenReadStream())
            {
                var uniqueFileName = $"{Guid.NewGuid()}_{File.FileName}";
                await _storageService.UploadContractAsync(uniqueFileName, stream);
            }
        }
        return RedirectToAction("Contracts");
    }
}