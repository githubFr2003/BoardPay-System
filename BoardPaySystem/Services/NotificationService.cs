using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using BoardPaySystem.Models;

namespace BoardPaySystem.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task CreateNotificationAsync(string userId, string title, string message, NotificationType type, string? actionLink = null, int? billId = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                ActionLink = actionLink,
                BillId = billId,
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task CreateBillNotificationAsync(Bill bill, NotificationType type)
        {
            string title;
            string message;
            string? actionLink = $"/Tenant/Payment/{bill.BillId}";

            switch (type)
            {
                case NotificationType.NewBill:
                    title = "New Bill Available";
                    message = $"Your bill for {bill.BillingPeriod} has been approved and is now available. Total amount: {bill.TotalAmount:C}";
                    break;

                case NotificationType.UpcomingDue:
                    title = "Upcoming Bill Due Date";
                    message = $"Your bill for {bill.BillingPeriod} is due in 3 days. Total amount: {bill.TotalAmount:C}";
                    break;

                case NotificationType.Overdue:
                    title = "Bill Overdue";
                    message = $"Your bill for {bill.BillingPeriod} is overdue. Please settle the payment as soon as possible.";
                    break;

                case NotificationType.PaymentConfirmed:
                    title = "Payment Confirmed";
                    message = $"Your payment for {bill.BillingPeriod} has been confirmed. Thank you!";
                    break;

                case NotificationType.BillModified:
                    title = "Bill Updated";
                    message = $"Your bill for {bill.BillingPeriod} has been modified. Please check the updated details.";
                    break;

                default:
                    throw new ArgumentException("Invalid notification type for bill notification");
            }

            await CreateNotificationAsync(bill.TenantId, title, message, type, actionLink, bill.BillId);
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(string userId, bool includeRead = false)
        {
            var query = _context.Notifications
                .Include(n => n.Bill)
                .Where(n => n.UserId == userId);

            if (!includeRead)
            {
                query = query.Where(n => !n.IsRead);
            }

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteNotificationAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task CheckAndCreateDueDateNotificationsAsync()
        {
            var threeDaysFromNow = DateTime.Now.Date.AddDays(3);
            var today = DateTime.Now.Date;

            // Find bills due in 3 days
            var upcomingBills = await _context.Bills
                .Where(b => b.DueDate.Date == threeDaysFromNow && b.Status == BillStatus.NotPaid)
                .ToListAsync();

            foreach (var bill in upcomingBills)
            {
                await CreateBillNotificationAsync(bill, NotificationType.UpcomingDue);
            }

            // Find overdue bills that haven't been marked as overdue yet
            var overdueBills = await _context.Bills
                .Where(b => b.DueDate.Date < today && b.Status == BillStatus.NotPaid)
                .ToListAsync();

            foreach (var bill in overdueBills)
            {
                await CreateBillNotificationAsync(bill, NotificationType.Overdue);
            }
        }
    }
} 