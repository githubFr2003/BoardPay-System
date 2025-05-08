using BoardPaySystem.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoardPaySystem.Services
{
    public class BillingService : IBillingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMeterReadingService _meterReadingService;
        private readonly ILogger<BillingService> _logger;

        public BillingService(
            ApplicationDbContext context,
            IMeterReadingService meterReadingService,
            ILogger<BillingService> logger)
        {
            _context = context;
            _meterReadingService = meterReadingService;
            _logger = logger;
        }

        public async Task<int> GenerateMonthlyBillsAsync(DateTime billingDate)
        {
            // Get tenants who need bills generated today based on their billing cycle
            var tenants = await GetTenantsWithDueBillsAsync(billingDate);
            int billsGenerated = 0;

            foreach (var tenant in tenants)
            {
                // Check if bill already exists for this tenant and month/year
                var existingBill = await _context.Bills
                    .FirstOrDefaultAsync(b => 
                        b.TenantId == tenant.Id && 
                        b.BillingDate.Month == billingDate.Month && 
                        b.BillingDate.Year == billingDate.Year);

                if (existingBill == null)
                {
                    // Generate bill for this tenant
                    await GenerateBillForTenantAsync(tenant.Id, billingDate);
                    billsGenerated++;
                }
            }

            return billsGenerated;
        }

        public async Task<Bill> GenerateBillForTenantAsync(string tenantId, DateTime billingDate)
        {
            // Get tenant with room information
            var tenant = await _context.Users
                .Include(u => u.Room)
                    .ThenInclude(r => r != null ? r.Floor : null)
                        .ThenInclude(f => f != null ? f.Building : null)
                .FirstOrDefaultAsync(u => u.Id == tenantId);

            if (tenant == null || tenant.Room == null)
            {
                throw new ArgumentException($"Tenant with ID {tenantId} not found or has no assigned room.");
            }

            if (tenant.Room.Floor == null || tenant.Room.Floor.Building == null)
            {
                throw new ArgumentException($"Tenant with ID {tenantId} has a room with incomplete floor or building data.");
            }

            // Check if this tenant has a meter reading for this billing period
            var hasReading = await _meterReadingService.HasCompletedReadingForBillingPeriodAsync(tenantId, billingDate);

            if (!hasReading)
            {
                _logger.LogWarning("Tenant {TenantId} does not have a meter reading for {BillingPeriod}",
                    tenantId, billingDate.ToString("yyyy-MM"));
                throw new InvalidOperationException($"Cannot generate bill for tenant {tenantId}: No meter reading available for {billingDate:yyyy-MM}");
            }

            // Use default values if custom values are not set
            decimal monthlyRent = tenant.Room.CustomMonthlyRent ?? tenant.Room.Floor.Building.DefaultMonthlyRent;
            decimal waterFee = tenant.Room.CustomWaterFee ?? tenant.Room.Floor.Building.DefaultWaterFee;

            // Calculate electricity fee based on the latest meter reading
            decimal electricityFee = await _meterReadingService.CalculateElectricityChargeAsync(tenantId, billingDate);

            decimal wifiFee = tenant.Room.CustomWifiFee ?? tenant.Room.Floor.Building.DefaultWifiFee;

            // Create new bill using the room's rate information
            var bill = new Bill
            {
                TenantId = tenantId,
                RoomId = tenant.Room.RoomId,
                BillingDate = billingDate,
                // Calculate due date based on the tenant's start date
                DueDate = CalculateBillDueDate(tenant.StartDate, billingDate),
                MonthlyRent = monthlyRent,
                WaterFee = waterFee,
                ElectricityFee = electricityFee,
                WifiFee = wifiFee,
                Status = BillStatus.NotPaid
            };

            _context.Bills.Add(bill);
            await _context.SaveChangesAsync();
            return bill;
        }

        public async Task<Bill> GenerateInitialBillForTenantAsync(string tenantId)
        {
            // Get tenant with room information
            var tenant = await _context.Users
                .Include(u => u.Room)
                    .ThenInclude(r => r != null ? r.Floor : null)
                        .ThenInclude(f => f != null ? f.Building : null)
                .FirstOrDefaultAsync(u => u.Id == tenantId);
            
            if (tenant == null || tenant.Room == null)
            {
                throw new ArgumentException($"Tenant with ID {tenantId} not found or has no assigned room.");
            }

            if (tenant.Room.Floor == null || tenant.Room.Floor.Building == null)
            {
                throw new ArgumentException($"Tenant with ID {tenantId} has a room with incomplete floor or building data.");
            }
            
            var building = tenant.Room.Floor.Building;

            // For initial bill, we use the tenant's start date
            var firstDayOfMonth = new DateTime(tenant.StartDate.Year, tenant.StartDate.Month, 1);

            // Create the bill with proper property names, but without electricity fee
            var bill = new Bill
            {
                TenantId = tenantId,
                RoomId = tenant.Room.RoomId,
                BillingDate = firstDayOfMonth,
                DueDate = tenant.StartDate, // Due immediately on start date for initial bill
                MonthlyRent = tenant.Room.CustomMonthlyRent ?? building.DefaultMonthlyRent,
                WaterFee = tenant.Room.CustomWaterFee ?? building.DefaultWaterFee,
                ElectricityFee = 0, // No electricity fee for initial bill
                WifiFee = tenant.Room.CustomWifiFee ?? building.DefaultWifiFee,
                Status = BillStatus.NotPaid,
                Notes = "Initial bill generated on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            _context.Bills.Add(bill);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Initial bill {BillId} created for tenant {TenantId}", bill.BillId, tenantId);

            return bill;
        }

        public async Task<bool> ProcessPaymentAsync(int billId, decimal amount, string reference)
        {
            var bill = await _context.Bills.FindAsync(billId);
            if (bill == null)
            {
                return false;
            }

            // In-person payments get marked as Paid immediately
            bill.Status = BillStatus.Paid;
            bill.PaymentDate = DateTime.Now;
            bill.PaymentReference = reference;
            bill.AmountPaid = amount;

            // Create payment record
            var payment = new Payment
            {
                BillId = billId,
                Amount = amount,
                PaymentDate = DateTime.Now,
                PaymentMethod = "Cash/In-Person",
                ReferenceNumber = reference
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task UpdateBillStatusesAsync()
        {
            var today = DateTime.Now.Date;

            // Find all bills that are NotPaid or Pending and past due date
            var overdueBills = await _context.Bills
                .Where(b => (b.Status == BillStatus.NotPaid || b.Status == BillStatus.Pending) &&
                            b.DueDate < today)
                .ToListAsync();

            foreach (var bill in overdueBills)
            {
                bill.Status = BillStatus.Overdue;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<ApplicationUser>> GetTenantsWithDueBillsAsync(DateTime currentDate)
        {
            // First, get all tenants who have a room assigned
            var tenantsWithRooms = await _context.Users
                .Where(u => u.RoomId.HasValue)
                .ToListAsync();

            // Then filter to only include tenants whose billing day matches today's date
            // and who have a completed meter reading for this billing period
            var tenantsWithDueBills = new List<ApplicationUser>();

            foreach (var tenant in tenantsWithRooms)
            {
                // Calculate if today is their billing day based on start date
                if (IsBillingDayForTenant(tenant.StartDate, currentDate))
                {
                    // Only include tenants with completed meter readings
                    // (except for their first month)
                    bool isFirstMonth = IsFirstBillingMonth(tenant.StartDate, currentDate);
                    bool hasReading = await _meterReadingService.HasCompletedReadingForBillingPeriodAsync(tenant.Id, currentDate);

                    if (isFirstMonth || hasReading)
                    {
                        tenantsWithDueBills.Add(tenant);
                    }
                }
            }

            return tenantsWithDueBills;
        }

        public DateTime CalculateNextBillingDate(DateTime startDate, DateTime currentDate)
        {
            // Calculate the next billing date based on the start date
            // If today is before the billing day in the current month, use this month
            // Otherwise, use next month
            int startDay = startDate.Day;
            int currentMonth = currentDate.Month;
            int currentYear = currentDate.Year;

            // Try to create a date with the same day in the current month
            DateTime candidateDate = new DateTime(currentYear, currentMonth, 1);

            try
            {
                candidateDate = new DateTime(currentYear, currentMonth, startDay);
            }
            catch (ArgumentOutOfRangeException)
            {
                // If the day doesn't exist in the current month, use the last day of the month
                candidateDate = new DateTime(currentYear, currentMonth, 1).AddMonths(1).AddDays(-1);
            }

            // If the candidate date is in the past, move to next month
            if (candidateDate < currentDate)
            {
                currentMonth++;
                if (currentMonth > 12)
                {
                    currentMonth = 1;
                    currentYear++;
                }

                try
                {
                    candidateDate = new DateTime(currentYear, currentMonth, startDay);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // If the day doesn't exist in the next month, use the last day of the month
                    candidateDate = new DateTime(currentYear, currentMonth, 1).AddMonths(1).AddDays(-1);
                }
            }

            return candidateDate;
        }

        // Helper method to determine if today is the billing day for a tenant
        private bool IsBillingDayForTenant(DateTime startDate, DateTime currentDate)
        {
            // If the day of the month matches, it's their billing day
            // Handle months with fewer days (e.g., if start date was 31st, Feb would use 28th)
            int startDay = startDate.Day;
            int daysInCurrentMonth = DateTime.DaysInMonth(currentDate.Year, currentDate.Month);

            // If the start day is beyond the days in the current month, use the last day
            int effectiveBillingDay = Math.Min(startDay, daysInCurrentMonth);

            return currentDate.Day == effectiveBillingDay;
        }

        // Helper method to calculate the due date for a bill
        private DateTime CalculateBillDueDate(DateTime startDate, DateTime billingDate)
        {
            // Set the due date to the same day of the month as the start date
            try
            {
                return new DateTime(billingDate.Year, billingDate.Month, startDate.Day);
            }
            catch (ArgumentOutOfRangeException)
            {
                // If the day doesn't exist in this month, use the last day of the month
                return new DateTime(billingDate.Year, billingDate.Month, 1)
                    .AddMonths(1)
                    .AddDays(-1);
            }
        }

        // Helper method to determine if this is the tenant's first billing month
        private bool IsFirstBillingMonth(DateTime startDate, DateTime currentDate)
        {
            return startDate.Year == currentDate.Year && startDate.Month == currentDate.Month;
        }
    }
}
