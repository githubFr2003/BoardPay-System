using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BoardPaySystem.Models;
using BoardPaySystem.Services;
using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace BoardPaySystem.Controllers
{
    [Authorize(Roles = "Landlord")]
    public class BillingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBillingService _billingService;

        public BillingController(ApplicationDbContext context, IBillingService billingService)
        {
            _context = context;
            _billingService = billingService;
        }

        // GET: /Billing/Index
        public IActionResult Index()
        {
            return RedirectToAction("Bills");
        }

        // GET: /Billing/Bills
        public async Task<IActionResult> Bills()
        {
            var bills = await _context.Bills
                .Include(b => b.Tenant)
                .Include(b => b.Room)
                .OrderByDescending(b => b.BillingDate)
                .ToListAsync();
            return View(bills);
        }

        // GET: /Billing/BillDetails/5
        public async Task<IActionResult> BillDetails(int id)
        {
            var bill = await _context.Bills
                .Include(b => b.Tenant)
                .Include(b => b.Room)
                .FirstOrDefaultAsync(b => b.BillId == id);
            if (bill == null)
            {
                return NotFound();
            }
            var payments = await _context.Payments
                .Where(p => p.BillId == id)
                .OrderBy(p => p.PaymentDate)
                .ToListAsync();
            ViewBag.Payments = payments;
            return View(bill);
        }

        // GET: /Billing/CreateBill
        public async Task<IActionResult> CreateBill()
        {
            ViewBag.Tenants = await _context.Users.Where(u => u.RoomId != null).ToListAsync();
            ViewBag.Rooms = await _context.Rooms.ToListAsync();
            return View();
        }

        // POST: /Billing/CreateBill
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBill(Bill bill)
        {
            if (ModelState.IsValid)
            {
                _context.Bills.Add(bill);
                await _context.SaveChangesAsync();
                return RedirectToAction("Bills");
            }
            ViewBag.Tenants = await _context.Users.Where(u => u.RoomId != null).ToListAsync();
            ViewBag.Rooms = await _context.Rooms.ToListAsync();
            return View(bill);
        }

        // GET: /Billing/GenerateBills
        public IActionResult GenerateBills()
        {
            return View();
        }

        // POST: /Billing/GenerateBills
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateBills(DateTime? billingDate)
        {
            DateTime effectiveBillingDate = billingDate ?? DateTime.Now;
            
            try
            {
                int billsGenerated = await _billingService.GenerateMonthlyBillsAsync(effectiveBillingDate);
                TempData["SuccessMessage"] = $"Successfully generated {billsGenerated} bills.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating bills: {ex.Message}";
            }
            
            return RedirectToAction("Bills");
        }

        // GET: /Billing/RecordPayment/5
        public async Task<IActionResult> RecordPayment(int id)
        {
            var bill = await _context.Bills
                .Include(b => b.Tenant)
                .Include(b => b.Room)
                .FirstOrDefaultAsync(b => b.BillId == id);
                
            if (bill == null)
            {
                return NotFound();
            }
            
            return View(bill);
        }

        // POST: /Billing/RecordPayment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordPayment(int billId, decimal amount, DateTime paymentDate, string paymentMethod, string paymentReference, string? notes = null)
        {
            if (amount <= 0)
            {
                ModelState.AddModelError("", "Payment amount must be greater than zero.");
                var bill = await _context.Bills
                    .Include(b => b.Tenant)
                    .Include(b => b.Room)
                    .FirstOrDefaultAsync(b => b.BillId == billId);
                if (bill == null)
                {
                    return NotFound();
                }
                return View(bill);
            }

            try
            {
                var bill = await _context.Bills
                    .Include(b => b.Tenant)
                    .FirstOrDefaultAsync(b => b.BillId == billId);
                
                if (bill == null)
                {
                    TempData["ErrorMessage"] = "Bill not found.";
                    return RedirectToAction("Bills");
                }
                
                // Create new payment record
                var payment = new Payment
                {
                    BillId = billId,
                    TenantId = bill.TenantId,
                    Amount = amount,
                    PaymentDate = paymentDate,
                    PaymentMethod = paymentMethod,
                    ReferenceNumber = paymentReference,
                    Notes = notes
                };
                
                _context.Payments.Add(payment);
                
                // Update bill information
                decimal totalPaid = amount;
                if (bill.AmountPaid.HasValue)
                {
                    totalPaid += bill.AmountPaid.Value;
                }
                
                bill.AmountPaid = totalPaid;
                bill.PaymentDate = paymentDate;
                bill.PaymentReference = paymentReference;
                
                // Update bill status based on amount paid
                if (totalPaid >= bill.TotalAmount)
                {
                    bill.Status = BillStatus.Paid;
                }
                else if (totalPaid > 0)
                {
                    bill.Status = BillStatus.Pending; // Changed from PartiallyPaid to Pending
                }
                
                _context.Update(bill);
                await _context.SaveChangesAsync();
                
                string tenantName = bill.Tenant != null ? $"{bill.Tenant.FirstName} {bill.Tenant.LastName}" : "Unknown tenant";
                TempData["SuccessMessage"] = $"Payment of {amount:C} for {tenantName} recorded successfully.";
                return RedirectToAction("BillDetails", new { id = billId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error recording payment: {ex.Message}";
                return RedirectToAction("RecordPayment", new { id = billId });
            }
        }

        // POST: /Billing/UpdateBillStatuses
        [HttpPost]
        public async Task<IActionResult> UpdateBillStatuses()
        {
            await _billingService.UpdateBillStatusesAsync();
            TempData["SuccessMessage"] = "Bill statuses updated successfully.";
            return RedirectToAction("Bills");
        }

        // GET: /Billing/GenerateInitialBill
        public async Task<IActionResult> GenerateInitialBill(string tenantId)
        {
            // Get the tenant with room and building information
            var tenant = await _context.Users
                .Include(u => u.Room!)
                    .ThenInclude(r => r.Floor!)
                        .ThenInclude(f => f.Building!)
                .FirstOrDefaultAsync(u => u.Id == tenantId);
                
            if (tenant == null || tenant.Room == null || tenant.Room.Floor == null || tenant.Room.Floor.Building == null)
            {
                return NotFound("Tenant, room, or building information not found.");
            }
            
            var building = tenant.Room.Floor.Building;
            var today = DateTime.Today;
            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
            
            // Create the bill with proper property names
            var bill = new Bill
            {
                TenantId = tenantId,
                RoomId = tenant.Room.RoomId,
                BillingDate = firstDayOfMonth,
                DueDate = new DateTime(today.Year, today.Month, tenant.BillingCycleDay),
                MonthlyRent = tenant.Room.CustomMonthlyRent ?? building.DefaultMonthlyRent,
                WaterFee = tenant.Room.CustomWaterFee ?? building.DefaultWaterFee,
                ElectricityFee = 0, // Will be calculated based on meter readings
                WifiFee = tenant.Room.CustomWifiFee ?? building.DefaultWifiFee,
                Status = BillStatus.Pending,
                Notes = "Initial bill generated on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            // TotalAmount is calculated automatically
            
            _context.Bills.Add(bill);
            await _context.SaveChangesAsync();
            
            return RedirectToAction("Bills", "Tenant");
        }

        // POST: /Billing/ApprovePayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePayment(int billId, decimal amount)
        {
            var bill = await _context.Bills
                .Include(b => b.Tenant)
                .FirstOrDefaultAsync(b => b.BillId == billId);
                
            if (bill == null)
            {
                TempData["ErrorMessage"] = "Bill not found.";
                return RedirectToAction("Bills");
            }
            
            if (bill.Status != BillStatus.Pending)
            {
                TempData["ErrorMessage"] = "Only pending payments can be approved.";
                return RedirectToAction("BillDetails", new { id = billId });
            }
            
            // Update the bill status
            bill.Status = BillStatus.Paid;
            bill.PaymentDate = DateTime.Now;
            bill.AmountPaid = amount;
            
            // Record the payment - fixed property name to match Payment model
            var payment = new Payment
            {
                BillId = billId,
                TenantId = bill.TenantId,
                Amount = amount,
                PaymentDate = DateTime.Now,
                PaymentMethod = "GCash",
                ReferenceNumber = bill.PaymentReference ?? "Approved by landlord", // Correct property name
                Notes = $"GCash payment approved by landlord on {DateTime.Now}"
            };
            
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            
            string tenantName = "tenant";
            if (bill.Tenant != null)
            {
                tenantName = $"{bill.Tenant.FirstName} {bill.Tenant.LastName}";
            }
            
            TempData["SuccessMessage"] = $"GCash payment for {tenantName} has been approved.";
            return RedirectToAction("BillDetails", new { id = billId });
        }
    }
}