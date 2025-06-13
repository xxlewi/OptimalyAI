using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using OAI.Core.DTOs.Business;
using OAI.Core.Entities.Business;
using OAI.ServiceLayer.Services.Business;
using OAI.ServiceLayer.Services.Customers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OptimalyAI.Controllers
{
    [Route("[controller]")]
    public class RequestsController : Controller
    {
        private readonly IBusinessRequestService _requestService;
        private readonly IWorkflowTemplateService _workflowService;
        private readonly IRequestExecutionService _executionService;
        private readonly ICustomerService _customerService;

        public RequestsController(
            IBusinessRequestService requestService,
            IWorkflowTemplateService workflowService,
            IRequestExecutionService executionService,
            ICustomerService customerService)
        {
            _requestService = requestService;
            _workflowService = workflowService;
            _executionService = executionService;
            _customerService = customerService;
        }

        // GET: /Requests
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            ViewBag.Title = "Požadavky";
            var requests = await _requestService.GetAllAsync();
            return View(requests);
        }

        // GET: /Requests/New
        [HttpGet("New")]
        public async Task<IActionResult> New(Guid? customerId = null)
        {
            ViewBag.Title = "Nový požadavek";
            ViewBag.CustomerId = customerId;
            
            // Pokud je zadáno customerId, načti zákazníka
            if (customerId.HasValue)
            {
                var customer = await _customerService.GetByIdAsync(customerId.Value);
                if (customer != null)
                {
                    ViewBag.CustomerName = customer.Name;
                    ViewBag.CustomerCompany = customer.CompanyName;
                }
            }
            ViewBag.RequestTypes = new SelectList(new[]
            {
                new { Value = "ProductPhoto", Text = "Produktové foto" },
                new { Value = "DocumentAnalysis", Text = "Analýza dokumentu" },
                new { Value = "WebScraping", Text = "Web scraping" },
                new { Value = "DataProcessing", Text = "Zpracování dat" },
                new { Value = "EmailAutomation", Text = "E-mailová automatizace" },
                new { Value = "ReportGeneration", Text = "Generování reportů" },
                new { Value = "Custom", Text = "Vlastní" }
            }, "Value", "Text");
            
            ViewBag.Priorities = new SelectList(new[]
            {
                new { Value = RequestPriority.Low, Text = "Nízká" },
                new { Value = RequestPriority.Normal, Text = "Normální" },
                new { Value = RequestPriority.High, Text = "Vysoká" },
                new { Value = RequestPriority.Urgent, Text = "Urgentní" }
            }, "Value", "Text");
            
            return View();
        }

        // GET: /Requests/Queue
        [HttpGet("Queue")]
        public async Task<IActionResult> Queue()
        {
            ViewBag.Title = "Příchozí fronta";
            var requests = await _requestService.GetRequestsByStatusAsync(RequestStatus.New);
            return View(requests);
        }

        // GET: /Requests/Active
        [HttpGet("Active")]
        public async Task<IActionResult> Active()
        {
            ViewBag.Title = "Aktivní zpracování";
            var executions = await _executionService.GetActiveExecutionsAsync();
            return View(executions);
        }

        // GET: /Requests/Completed
        [HttpGet("Completed")]
        public async Task<IActionResult> Completed()
        {
            ViewBag.Title = "Dokončené požadavky";
            var requests = await _requestService.GetRequestsByStatusAsync(RequestStatus.Completed);
            return View(requests);
        }


        // GET: /Requests/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var request = await _requestService.GetRequestWithDetailsAsync(id);
            if (request == null)
            {
                return NotFound();
            }

            ViewBag.Title = $"Požadavek {request.RequestNumber}";
            return View(request);
        }

        // GET: /Requests/{id}/Progress
        [HttpGet("{id:int}/Progress")]
        public async Task<IActionResult> Progress(int id)
        {
            var request = await _requestService.GetRequestWithDetailsAsync(id);
            if (request == null)
            {
                return NotFound();
            }

            var activeExecution = request.Executions?.FirstOrDefault(e => 
                e.Status == ExecutionStatus.Running || e.Status == ExecutionStatus.Paused);
            
            if (activeExecution == null)
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            var progress = await _executionService.GetExecutionProgressAsync(activeExecution.Id);
            
            ViewBag.Title = $"Průběh zpracování - {request.RequestNumber}";
            ViewBag.Request = request;
            
            return View(progress);
        }

        // GET: /Requests/{id}/Edit
        [HttpGet("{id:int}/Edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var request = await _requestService.GetRequestWithDetailsAsync(id);
            if (request == null)
            {
                return NotFound();
            }

            // Allow editing all requests in simplified system

            ViewBag.Title = $"Upravit požadavek {request.RequestNumber}";
            return View(request);
        }
    }
}