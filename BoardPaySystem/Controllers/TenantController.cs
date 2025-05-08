using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using BoardPaySystem.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace BoardPaySystem.Controllers
{
    [Authorize(Roles = "Tenant")]
    public class TenantController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TenantController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Tenant Dashboard
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }
            
            // Get upcoming bills (not paid and not overdue)
            var upcomingBills = await _context.Bills
                .Where(b => b.TenantId == userId && 
                           (b.Status == BillStatus.NotPaid || b.Status == BillStatus.Pending) && 
                           b.DueDate >= DateTime.Today)
                .Include(b => b.Room)
                .OrderBy(b => b.DueDate)
                .Take(5)
                .ToListAsync();
            
            // Get recent payments
            var recentPayments = await _context.Bills
                .Where(b => b.TenantId == userId && b.Status == BillStatus.Paid)
                .Include(b => b.Room)
                .OrderByDescending(b => b.PaymentDate)
                .Take(5)
                .ToListAsync();
            
            // Get overdue bills
            var overdueBills = await _context.Bills
                .Where(b => b.TenantId == userId && 
                          b.Status == BillStatus.NotPaid && 
                          b.DueDate < DateTime.Today)
                .Include(b => b.Room)
                .OrderBy(b => b.DueDate)
                .ToListAsync();

            ViewBag.UpcomingBills = upcomingBills;
            ViewBag.RecentPayments = recentPayments;
            ViewBag.OverdueBills = overdueBills;
            
            return View();
        }

        // GET: Tenant/Bills
        public async Task<IActionResult> Bills()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var bills = await _context.Bills
                .Where(b => b.TenantId == userId && 
                          (b.Status == BillStatus.NotPaid || b.Status == BillStatus.Pending))
                .Include(b => b.Room)
                .OrderBy(b => b.DueDate)
                .ToListAsync();
            
            return View(bills);
        }

        // GET: Tenant/Payments
        public async Task<IActionResult> Payments()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var payments = await _context.Bills
                .Where(b => b.TenantId == userId && b.Status == BillStatus.Paid)
                .Include(b => b.Room)
                .OrderByDescending(b => b.PaymentDate)
                .ToListAsync();
            
            return View(payments);
        }

        // GET: Tenant/History
        public async Task<IActionResult> History()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var allBills = await _context.Bills
                .Where(b => b.TenantId == userId)
                .Include(b => b.Room)
                .OrderByDescending(b => b.BillingDate)
                .ToListAsync();
            
            return View(allBills);
        }

        // GET: Tenant/Profile
        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }
            
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound("User not found");
            }
            
            // Get tenant's current room
            var room = await _context.Rooms
                .Include(r => r.Floor)
                .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(r => r.TenantId == userId);
            
            ViewBag.Room = room;
            
            return View(user);
        }

        // GET: Tenant/Payment/5
        public async Task<IActionResult> Payment(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }
            
            var bill = await _context.Bills
                .Include(b => b.Room)
                .FirstOrDefaultAsync(b => b.BillId == id && b.TenantId == userId);
            
            if (bill == null)
            {
                return NotFound();
            }
            
            return View(bill);
        }

        // POST: Tenant/InitiatePayment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitiatePayment(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var bill = await _context.Bills
                .FirstOrDefaultAsync(b => b.BillId == id && b.TenantId == userId);
            
            if (bill == null)
            {
                return NotFound();
            }
            
            // Mark the bill as pending payment
            bill.Status = BillStatus.Pending;
            bill.PaymentReference = "GCash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            
            await _context.SaveChangesAsync();
            
            // Redirect to confirmation page or GCash payment page
            // For now, just redirect back to the bill details
            return RedirectToAction(nameof(Payment), new { id = bill.BillId });
        }

        // POST: Tenant/ConfirmPayment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int id, string referenceNumber, string paymentMethod)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var bill = await _context.Bills
                .FirstOrDefaultAsync(b => b.BillId == id && b.TenantId == userId && b.Status == BillStatus.Pending);
            
            if (bill == null)
            {
                return NotFound();
            }

            // Validate reference number
            if (string.IsNullOrEmpty(referenceNumber))
            {
                ModelState.AddModelError("ReferenceNumber", "Payment reference number is required.");
                return RedirectToAction(nameof(Payment), new { id = bill.BillId });
            }
            
            // Update payment information
            bill.Status = BillStatus.Paid;
            bill.PaymentDate = DateTime.Now;
            bill.PaymentReference = referenceNumber;
            bill.PaymentMethod = paymentMethod;
            
            // Create a payment record
            var payment = new Payment
            {
                BillId = bill.BillId,
                TenantId = userId,
                Amount = bill.Amount,
                PaymentDate = DateTime.Now,
                PaymentMethod = paymentMethod,
                ReferenceNumber = referenceNumber
            };
            
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Payment confirmed successfully!";
            return RedirectToAction(nameof(Payments));
        }

        // POST: Tenant/CancelPayment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelPayment(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var bill = await _context.Bills
                .FirstOrDefaultAsync(b => b.BillId == id && b.TenantId == userId && b.Status == BillStatus.Pending);
            
            if (bill == null)
            {
                return NotFound();
            }
            
            // Reset to unpaid status
            bill.Status = BillStatus.NotPaid;
            bill.PaymentReference = null;
            
            await _context.SaveChangesAsync();
            
            TempData["InfoMessage"] = "Payment process has been cancelled.";
            return RedirectToAction(nameof(Bills));
        }
    }
}

