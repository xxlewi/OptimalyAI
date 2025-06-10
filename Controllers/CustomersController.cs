using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Customers;
using OAI.Core.Entities.Customers;
using OAI.ServiceLayer.Services.Customers;
using OptimalyAI.ViewModels;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// Controller pro správu zákazníků
    /// </summary>
    public class CustomersController : Controller
    {
        private readonly ICustomerService _customerService;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(
            ICustomerService customerService,
            ILogger<CustomersController> logger)
        {
            _customerService = customerService;
            _logger = logger;
        }

        /// <summary>
        /// Seznam všech zákazníků
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var customers = await _customerService.GetAllListAsync();
            return View(customers);
        }

        /// <summary>
        /// Detail zákazníka
        /// </summary>
        public async Task<IActionResult> Details(Guid id)
        {
            try
            {
                var customer = await _customerService.GetDetailedAsync(id);
                return View(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer details for {Id}", id);
                TempData["Error"] = "Nepodařilo se načíst detail zákazníka.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Formulář pro vytvoření zákazníka
        /// </summary>
        [HttpGet]
        public IActionResult Create()
        {
            var model = new CreateCustomerViewModel
            {
                Type = CustomerType.Company,
                Segment = CustomerSegment.Standard,
                PreferredCommunication = CommunicationPreference.Email,
                PaymentTermDays = 14,
                BillingCountry = "Česká republika"
            };
            return View(model);
        }

        /// <summary>
        /// Vytvoření nového zákazníka
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCustomerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var dto = new CreateCustomerDto
                {
                    Name = model.Name,
                    CompanyName = model.CompanyName,
                    ICO = model.ICO,
                    DIC = model.DIC,
                    Email = model.Email,
                    Phone = model.Phone,
                    Mobile = model.Mobile,
                    ContactPerson = model.ContactPerson,
                    BillingStreet = model.BillingStreet,
                    BillingCity = model.BillingCity,
                    BillingZip = model.BillingZip,
                    BillingCountry = model.BillingCountry,
                    UseDeliveryAddress = model.UseDeliveryAddress,
                    DeliveryStreet = model.DeliveryStreet,
                    DeliveryCity = model.DeliveryCity,
                    DeliveryZip = model.DeliveryZip,
                    DeliveryCountry = model.DeliveryCountry,
                    Type = model.Type,
                    Segment = model.Segment,
                    PreferredCommunication = model.PreferredCommunication,
                    Notes = model.Notes,
                    CreditLimit = model.CreditLimit,
                    PaymentTermDays = model.PaymentTermDays
                };

                var customer = await _customerService.CreateAsync(dto);
                _logger.LogInformation("Created new customer: {Name} with ID: {Id}", model.Name, customer.Id);

                TempData["Success"] = "Zákazník byl úspěšně vytvořen.";
                return RedirectToAction(nameof(Details), new { id = customer.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer");
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        /// <summary>
        /// Formulář pro úpravu zákazníka
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            try
            {
                var customer = await _customerService.GetByIdAsync(id);
                
                var model = new EditCustomerViewModel
                {
                    Id = customer.Id,
                    Name = customer.Name,
                    CompanyName = customer.CompanyName,
                    ICO = customer.ICO,
                    DIC = customer.DIC,
                    Email = customer.Email,
                    Phone = customer.Phone,
                    Mobile = customer.Mobile,
                    ContactPerson = customer.ContactPerson,
                    BillingStreet = customer.BillingStreet,
                    BillingCity = customer.BillingCity,
                    BillingZip = customer.BillingZip,
                    BillingCountry = customer.BillingCountry,
                    DeliveryStreet = customer.DeliveryStreet,
                    DeliveryCity = customer.DeliveryCity,
                    DeliveryZip = customer.DeliveryZip,
                    DeliveryCountry = customer.DeliveryCountry,
                    Type = customer.Type,
                    Status = customer.Status,
                    Segment = customer.Segment,
                    PreferredCommunication = customer.PreferredCommunication,
                    Notes = customer.Notes,
                    CreditLimit = customer.CreditLimit,
                    PaymentTermDays = customer.PaymentTermDays
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer for edit {Id}", id);
                TempData["Error"] = "Nepodařilo se načíst zákazníka.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Aktualizace zákazníka
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditCustomerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var dto = new UpdateCustomerDto
                {
                    Name = model.Name,
                    CompanyName = model.CompanyName,
                    ICO = model.ICO,
                    DIC = model.DIC,
                    Email = model.Email,
                    Phone = model.Phone,
                    Mobile = model.Mobile,
                    ContactPerson = model.ContactPerson,
                    BillingStreet = model.BillingStreet,
                    BillingCity = model.BillingCity,
                    BillingZip = model.BillingZip,
                    BillingCountry = model.BillingCountry,
                    DeliveryStreet = model.DeliveryStreet,
                    DeliveryCity = model.DeliveryCity,
                    DeliveryZip = model.DeliveryZip,
                    DeliveryCountry = model.DeliveryCountry,
                    Type = model.Type,
                    Status = model.Status,
                    Segment = model.Segment,
                    PreferredCommunication = model.PreferredCommunication,
                    Notes = model.Notes,
                    CreditLimit = model.CreditLimit,
                    PaymentTermDays = model.PaymentTermDays
                };

                await _customerService.UpdateAsync(model.Id, dto);
                _logger.LogInformation("Updated customer {Id}", model.Id);

                TempData["Success"] = "Zákazník byl úspěšně aktualizován.";
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer {Id}", model.Id);
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        /// <summary>
        /// Deaktivace zákazníka
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _customerService.DeleteAsync(id);
                _logger.LogInformation("Deactivated customer: {Id}", id);
                return Json(new { success = true, message = "Zákazník byl deaktivován." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating customer {Id}", id);
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Vyhledávání zákazníků
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Search(string query)
        {
            var customers = await _customerService.SearchAsync(query);
            return PartialView("_CustomerList", customers);
        }

        /// <summary>
        /// Aktualizace metrik zákazníka
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateMetrics(Guid id)
        {
            try
            {
                await _customerService.UpdateMetricsAsync(id);
                return Json(new { success = true, message = "Metriky byly aktualizovány." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating metrics for customer {Id}", id);
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}