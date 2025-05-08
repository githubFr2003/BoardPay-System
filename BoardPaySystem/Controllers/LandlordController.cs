using Microsoft.AspNetCore.Mvc;
using BoardPaySystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using System;

namespace BoardPaySystem.Controllers
{
    [Authorize(Roles = "Landlord")]
    public class LandlordController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LandlordController> _logger;

        public LandlordController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<LandlordController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var buildings = await _context.Buildings
                .Include(b => b.Floors)
                .ThenInclude(f => f.Rooms)
                .ToListAsync();

            var tenants = await _userManager.GetUsersInRoleAsync("Tenant");

            ViewBag.TotalBuildings = buildings.Count;
            ViewBag.TotalFloors = buildings.Sum(b => b.Floors.Count);
            ViewBag.TotalRooms = buildings.Sum(b => b.Floors.Sum(f => f.Rooms.Count));
            ViewBag.TotalTenants = tenants.Count;
            ViewBag.OccupiedRooms = buildings.Sum(b => b.Floors.Sum(f => f.Rooms.Count(r => r.IsOccupied)));
            ViewBag.VacantRooms = ViewBag.TotalRooms - ViewBag.OccupiedRooms;

            return View();
        }

        public IActionResult LandlordProfile()
        {
            return View();
        }

        public async Task<IActionResult> Overview()
        {
            try
            {
                // Get counts for the dashboard
                ViewBag.BuildingsCount = await _context.Buildings.CountAsync();
                ViewBag.FloorsCount = await _context.Floors.CountAsync();
                ViewBag.RoomsCount = await _context.Rooms.CountAsync();
                ViewBag.TenantsCount = (await _userManager.GetUsersInRoleAsync("Tenant")).Count;

                // Room status
                ViewBag.TotalRooms = await _context.Rooms.CountAsync();
                ViewBag.OccupiedRooms = await _context.Rooms.Where(r => r.IsOccupied).CountAsync();
                ViewBag.VacantRooms = ViewBag.TotalRooms - ViewBag.OccupiedRooms;
                ViewBag.OccupancyRate = ViewBag.TotalRooms > 0 
                    ? (ViewBag.OccupiedRooms * 100.0 / ViewBag.TotalRooms).ToString("F1") 
                    : "0.0";

                // Contracts
                var today = DateTime.Today;
                var thirtyDaysFromNow = today.AddDays(30);
                ViewBag.ActiveContracts = await _context.Contracts
                    .Where(c => c.Status == ContractStatus.Active)
                    .CountAsync();
                ViewBag.ExpiringContracts = await _context.Contracts
                    .Where(c => c.Status == ContractStatus.Active && c.EndDate <= thirtyDaysFromNow)
                    .CountAsync();

                // Billing status for current month
                var currentMonth = DateTime.Today.Month;
                var currentYear = DateTime.Today.Year;
                var monthlyBills = await _context.Bills
                    .Where(b => b.BillingDate.Month == currentMonth && b.BillingDate.Year == currentYear)
                    .ToListAsync();

                ViewBag.TotalBills = monthlyBills.Count;
                ViewBag.PaidBills = monthlyBills.Count(b => b.Status == BillStatus.Paid);
                ViewBag.PendingBills = monthlyBills.Count(b => b.Status == BillStatus.Pending);
                ViewBag.OverdueBills = monthlyBills.Count(b => b.Status == BillStatus.Overdue);

                // Calculate total amounts
                ViewBag.TotalBilledAmount = monthlyBills.Sum(b => b.TotalAmount).ToString("C");
                ViewBag.TotalPaidAmount = monthlyBills
                    .Where(b => b.Status == BillStatus.Paid)
                    .Sum(b => b.TotalAmount)
                    .ToString("C");
                ViewBag.TotalPendingAmount = monthlyBills
                    .Where(b => b.Status == BillStatus.Pending || b.Status == BillStatus.Overdue)
                    .Sum(b => b.TotalAmount)
                    .ToString("C");

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Overview action: {Message}", ex.Message);
                TempData["Error"] = "An error occurred while loading the dashboard. Please try again.";
                return View();
            }
        }

        public async Task<IActionResult> ManageBuildings()
        {
            var buildings = await _context.Buildings
                .Select(b => new BuildingListViewModel
                {
                    BuildingId = b.BuildingId,
                    BuildingName = b.BuildingName,
                    Address = b.Address,
                    TotalFloors = b.Floors.Count,
                    TotalRooms = b.Floors.Sum(f => f.Rooms.Count),
                    OccupiedRooms = b.Floors.Sum(f => f.Rooms.Count(r => r.IsOccupied))
                })
                .ToListAsync();
            return View(buildings);
        }

        public async Task<IActionResult> Billing()
        {
            // Get all bills with related data, including building information for filtering
            var bills = await _context.Bills
                .Include(b => b.Tenant)
                .Include(b => b.Room)
                    .ThenInclude(r => r.Floor != null ? r.Floor : null)
                        .ThenInclude(f => f != null ? f.Building : null)
                .OrderByDescending(b => b.DueDate)
                .ToListAsync();
            
            // Get all buildings for the filter dropdown
            ViewBag.Buildings = await _context.Buildings.ToListAsync();
                
            return View(bills);
        }

        public async Task<IActionResult> AddTenant()
        {
            var buildings = await _context.Buildings.ToListAsync();
            ViewBag.Buildings = buildings;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTenant([Bind("Username,Password,FirstName,LastName,PhoneNumber,BuildingId,RoomId,StartDate")] CreateTenantViewModel model)
        {
            try
            {
                _logger.LogInformation("Starting AddTenant action with data: FirstName={FirstName}, LastName={LastName}, Username={Username}, RoomId={RoomId}, StartDate={StartDate}",
                    model.FirstName, model.LastName, model.Username, model.RoomId, model.StartDate.ToString("yyyy-MM-dd"));

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state when adding tenant");
                    ViewBag.Buildings = await _context.Buildings.ToListAsync();
                    return View(model);
                }

                // Validate and update the room
                var room = await _context.Rooms
                    .Include(r => r.CurrentTenant)
                    .Include(r => r.Floor)
                    .FirstOrDefaultAsync(r => r.RoomId == model.RoomId);

                if (room == null)
                {
                    _logger.LogError("Room with ID {RoomId} not found", model.RoomId);
                    ModelState.AddModelError("RoomId", "Selected room not found.");
                    ViewBag.Buildings = await _context.Buildings.ToListAsync();
                    return View(model);
                }

                if (room.IsOccupied || room.CurrentTenant != null)
                {
                    _logger.LogWarning("Room {RoomId} is already occupied", model.RoomId);
                    ModelState.AddModelError("RoomId", "This room is already occupied.");
                    ViewBag.Buildings = await _context.Buildings.ToListAsync();
                    return View(model);
                }

                // Create the user
                var user = new ApplicationUser
                {
                    UserName = model.Username,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    PhoneNumber = model.PhoneNumber,
                    RoomId = model.RoomId,
                    BuildingId = room.Floor?.BuildingId,
                    StartDate = model.StartDate
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Tenant");
                    _logger.LogInformation("User created successfully with ID {UserId}, StartDate {StartDate}", user.Id, user.StartDate.ToString("yyyy-MM-dd"));

                    // Update the room
                    room.IsOccupied = true;
                    room.CurrentTenant = user;
                    _context.Update(room);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Room {RoomId} updated with tenant {UserId}", room.RoomId, user.Id);

                    // Generate initial bill for the tenant
                    await GenerateInitialBillForTenant(user.Id);
                    _logger.LogInformation("Initial bill generated for tenant {UserId}", user.Id);

                    TempData["Success"] = "Tenant added successfully!";
                    return RedirectToAction(nameof(ManageTenants));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding tenant: {ErrorMessage}", ex.Message);
                ModelState.AddModelError("", "Error adding tenant. Please try again.");
            }

            ViewBag.Buildings = await _context.Buildings.ToListAsync();
            return View(model);
        }

        // Helper method to generate initial bill for a new tenant
        private async Task GenerateInitialBillForTenant(string tenantId)
        {
            try
            {
                // Get the tenant with room and building information
                var tenant = await _context.Users
                    .Include(u => u.Room)
                        .ThenInclude(r => r.Floor)
                            .ThenInclude(f => f.Building)
                    .FirstOrDefaultAsync(u => u.Id == tenantId);
                    
                if (tenant == null || tenant.Room == null || tenant.Room.Floor == null || tenant.Room.Floor.Building == null)
                {
                    _logger.LogError("Cannot generate initial bill: Tenant {TenantId}, room, or building information not found", tenantId);
                    return;
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
                    DueDate = new DateTime(today.Year, today.Month, tenant.StartDate.Day),
                    MonthlyRent = tenant.Room.CustomMonthlyRent ?? building.DefaultMonthlyRent,
                    WaterFee = tenant.Room.CustomWaterFee ?? building.DefaultWaterFee,
                    ElectricityFee = 0, // Will be calculated based on meter readings
                    WifiFee = tenant.Room.CustomWifiFee ?? building.DefaultWifiFee,
                    Status = BillStatus.NotPaid,
                    Notes = "Initial bill generated on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                
                _context.Bills.Add(bill);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Initial bill {BillId} created for tenant {TenantId}", bill.BillId, tenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating initial bill for tenant {TenantId}: {ErrorMessage}", tenantId, ex.Message);
            }
        }

        public async Task<IActionResult> ManageTenants()
        {
            try
            {
                // First, get the IDs of all users with the 'Tenant' role
                var tenantUsers = await _userManager.GetUsersInRoleAsync("Tenant");
                var tenantIds = tenantUsers.Select(t => t.Id).ToList();
                
                // Then query the users with those IDs and include related data
                var tenantsWithDetails = await _context.Users
                    .Where(u => tenantIds.Contains(u.Id))
                    .Include(u => u.Room)
                    .ToListAsync();

                // Load additional data for each tenant
                foreach (var tenant in tenantsWithDetails)
                {
                    if (tenant.Room != null)
                    {
                        // Load the Floor explicitly
                        await _context.Entry(tenant.Room)
                            .Reference(r => r.Floor)
                            .LoadAsync();
                        
                        // If Floor exists, load its Building
                        if (tenant.Room.Floor != null)
                        {
                            await _context.Entry(tenant.Room.Floor)
                                .Reference(f => f.Building)
                                .LoadAsync();
                        }
                    }
                }

                ViewBag.Buildings = await _context.Buildings.ToListAsync();
                return View(tenantsWithDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ManageTenants: {Message}", ex.Message);
                TempData["Error"] = "An error occurred while loading tenants. Please try again.";
                return RedirectToAction("Index");
            }
        }

        public IActionResult MeterReadings()
        {
            return View();
        }

        public IActionResult Reports()
        {
            return View();
        }

        public IActionResult AddBuilding()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddBuilding(Building building)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Buildings.Add(building);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Building added successfully!";
                    return RedirectToAction(nameof(ManageBuildings));
                }
                catch (Exception)
                {
                    ModelState.AddModelError("", "Error adding building. Please try again.");
                }
            }
            return View(building);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBuilding(int id)
        {
            try
            {
                _logger.LogInformation("Starting deletion of building with ID {0}", id);
                
                // First, load the building with its related entities
                var building = await _context.Buildings
                    .Include(b => b.Floors)
                        .ThenInclude(f => f.Rooms)
                            .ThenInclude(r => r.CurrentTenant)
                    .Include(b => b.Tenants)
                    .FirstOrDefaultAsync(b => b.BuildingId == id);

                if (building == null)
                {
                    _logger.LogWarning("Building with ID {0} not found", id);
                    return Json(new { success = false, message = "Building not found." });
                }

                _logger.LogInformation("Building {0} found with {1} floors, {2} rooms, and {3} tenants", 
                    building.BuildingName, 
                    building.Floors?.Count ?? 0, 
                    building.Floors?.Sum(f => f.Rooms?.Count ?? 0) ?? 0,
                    building.Tenants?.Count ?? 0);

                // Using a single transaction for deletion
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // 1. Clear tenant associations with this building
                        var usersToUpdate = await _context.Users
                            .Where(u => u.BuildingId == id)
                            .ToListAsync();
                            
                        foreach (var user in usersToUpdate)
                        {
                            _logger.LogInformation("Detaching user {0} from building {1}", user.Id, id);
                            user.BuildingId = null;
                            _context.Update(user);
                        }
                        await _context.SaveChangesAsync();
                        
                        // 2. Clear CurrentTenant references and IsOccupied flags on all rooms
                        if (building.Floors != null)
                        {
                            foreach (var floor in building.Floors)
                            {
                                if (floor.Rooms != null)
                                {
                                    foreach (var room in floor.Rooms.Where(r => r.CurrentTenant != null).ToList())
                                    {
                                        if (room.CurrentTenant != null)
                                        {
                                            room.CurrentTenant.RoomId = null;
                                            _context.Update(room.CurrentTenant);
                                        }
                                        room.CurrentTenant = null;
                                        room.TenantId = null;
                                        room.IsOccupied = false;
                                        _context.Update(room);
                                    }
                                }
                            }
                            await _context.SaveChangesAsync();
                        }
                        
                        // 3. Get all room IDs in this building
                        var roomIds = building.Floors?
                            .SelectMany(f => f.Rooms ?? Enumerable.Empty<Room>())
                            .Select(r => r.RoomId)
                            .ToList() ?? new List<int>();

                        // 4. Delete all bills associated with these rooms
                        if (roomIds.Any())
                        {
                            var billsToDelete = await _context.Bills
                                .Where(b => roomIds.Contains(b.RoomId))
                                .ToListAsync();
                                
                            if (billsToDelete.Any())
                            {
                                _logger.LogInformation("Deleting {0} bills", billsToDelete.Count);
                                
                                // 5. Delete all payments associated with these bills
                                var billIds = billsToDelete.Select(b => b.BillId).ToList();
                                var paymentsToDelete = await _context.Payments
                                    .Where(p => billIds.Contains(p.BillId))
                                    .ToListAsync();
                                    
                                if (paymentsToDelete.Any())
                                {
                                    _logger.LogInformation("Deleting {0} payments", paymentsToDelete.Count);
                                    _context.Payments.RemoveRange(paymentsToDelete);
                                    await _context.SaveChangesAsync();
                                }
                                
                                _context.Bills.RemoveRange(billsToDelete);
                                await _context.SaveChangesAsync();
                            }
                            
                            // 6. Delete all contracts associated with these rooms
                            var contractsToDelete = await _context.Contracts
                                .Where(c => roomIds.Contains(c.RoomId))
                                .ToListAsync();
                                
                            if (contractsToDelete.Any())
                            {
                                _logger.LogInformation("Deleting {0} contracts", contractsToDelete.Count);
                                _context.Contracts.RemoveRange(contractsToDelete);
                                await _context.SaveChangesAsync();
                            }
                            
                            // 7. Delete all meter readings associated with these rooms
                            var meterReadingsToDelete = await _context.MeterReadings
                                .Where(m => roomIds.Contains(m.RoomId))
                                .ToListAsync();
                                
                            if (meterReadingsToDelete.Any())
                            {
                                _logger.LogInformation("Deleting {0} meter readings", meterReadingsToDelete.Count);
                                _context.MeterReadings.RemoveRange(meterReadingsToDelete);
                                await _context.SaveChangesAsync();
                            }
                        }
                        
                        // 8. For each floor, clear the rooms collection first
                        if (building.Floors != null)
                        {
                            foreach (var floor in building.Floors)
                            {
                                if (floor.Rooms != null && floor.Rooms.Any())
                                {
                                    _context.Rooms.RemoveRange(floor.Rooms);
                                }
                            }
                            await _context.SaveChangesAsync();
                            
                            // 9. Now remove all floors
                            _context.Floors.RemoveRange(building.Floors);
                            await _context.SaveChangesAsync();
                        }

                        // 10. Finally delete the building
                        _context.Buildings.Remove(building);
                        await _context.SaveChangesAsync();
                        
                        await transaction.CommitAsync();
                        _logger.LogInformation("Building {0} successfully deleted", building.BuildingName);
                        return Json(new { success = true, message = "Building successfully deleted" });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError("Error deleting building: " + ex.Message);
                        return Json(new { success = false, message = "Error deleting building: " + ex.Message });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Unexpected error deleting building: " + ex.Message);
                return Json(new { success = false, message = "Unexpected error deleting building: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceDeleteBuilding(int id)
        {
            try
            {
                _logger.LogInformation("Starting force deletion of building with ID {0}", id);
                
                // Direct database operations to clean up related entities
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // 1. Get all rooms in the building via floors
                        var floorsInBuilding = await _context.Floors
                            .Where(f => f.BuildingId == id)
                            .ToListAsync();
                            
                        var floorIds = floorsInBuilding.Select(f => f.FloorId).ToList();
                        
                        var roomsInBuilding = await _context.Rooms
                            .Where(r => floorIds.Contains(r.FloorId))
                            .ToListAsync();
                            
                        var roomIds = roomsInBuilding.Select(r => r.RoomId).ToList();
                        
                        // 2. Delete all payments associated with bills for these rooms
                        var billsForRooms = await _context.Bills
                            .Where(b => roomIds.Contains(b.RoomId))
                            .ToListAsync();
                        
                        var billIds = billsForRooms.Select(b => b.BillId).ToList();
                        
                        var paymentsToDelete = await _context.Payments
                            .Where(p => billIds.Contains(p.BillId))
                            .ToListAsync();
                            
                        if (paymentsToDelete.Any())
                        {
                            _logger.LogInformation("Force deleting {0} payments", paymentsToDelete.Count);
                            _context.Payments.RemoveRange(paymentsToDelete);
                            await _context.SaveChangesAsync();
                        }
                        
                        // 3. Delete all bills associated with these rooms
                        if (billsForRooms.Any())
                        {
                            _logger.LogInformation("Force deleting {0} bills", billsForRooms.Count);
                            _context.Bills.RemoveRange(billsForRooms);
                            await _context.SaveChangesAsync();
                        }
                        
                        // 4. Delete all contracts associated with these rooms
                        var contractsToDelete = await _context.Contracts
                            .Where(c => roomIds.Contains(c.RoomId))
                            .ToListAsync();
                            
                        if (contractsToDelete.Any())
                        {
                            _logger.LogInformation("Force deleting {0} contracts", contractsToDelete.Count);
                            _context.Contracts.RemoveRange(contractsToDelete);
                            await _context.SaveChangesAsync();
                        }
                        
                        // 5. Try to delete meter readings - handle if table doesn't exist
                        try
                        {
                            var meterReadingsExist = await _context.Database.ExecuteSqlRawAsync("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MeterReadings'") > 0;
                            
                            if (meterReadingsExist)
                            {
                                var meterReadingsToDelete = await _context.MeterReadings
                                    .Where(m => roomIds.Contains(m.RoomId))
                                    .ToListAsync();
                                    
                                if (meterReadingsToDelete.Any())
                                {
                                    _logger.LogInformation("Force deleting {0} meter readings", meterReadingsToDelete.Count);
                                    _context.MeterReadings.RemoveRange(meterReadingsToDelete);
                                    await _context.SaveChangesAsync();
                                }
                            }
                            else
                            {
                                _logger.LogWarning("MeterReadings table does not exist - skipping meter readings deletion");
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log the error but continue with the deletion process
                            _logger.LogWarning("Error accessing MeterReadings table: {0}. Continuing deletion process.", ex.Message);
                        }
                        
                        // 6. Force update any users that reference this building or rooms
                        var usersToUpdate = await _context.Users
                            .Where(u => u.BuildingId == id || (u.RoomId.HasValue && roomIds.Contains(u.RoomId.Value)))
                            .ToListAsync();
                            
                        foreach (var user in usersToUpdate)
                        {
                            _logger.LogWarning("Force detaching user {0} from building {1}", user.Id, id);
                            user.BuildingId = null;
                            user.RoomId = null;
                            _context.Update(user);
                        }
                        await _context.SaveChangesAsync();
                        
                        // 7. Delete all rooms
                        if (roomsInBuilding.Any())
                        {
                            _logger.LogInformation("Force deleting {0} rooms", roomsInBuilding.Count);
                            _context.Rooms.RemoveRange(roomsInBuilding);
                            await _context.SaveChangesAsync();
                        }
                        
                        // 8. Delete all floors
                        if (floorsInBuilding.Any())
                        {
                            _logger.LogInformation("Force deleting {0} floors", floorsInBuilding.Count);
                            _context.Floors.RemoveRange(floorsInBuilding);
                            await _context.SaveChangesAsync();
                        }
                        
                        // 9. Finally delete the building
                        var building = await _context.Buildings.FindAsync(id);
                        if (building != null)
                        {
                            _logger.LogInformation("Force deleting building {0}", building.BuildingName);
                            _context.Buildings.Remove(building);
                            await _context.SaveChangesAsync();
                            
                            // Commit transaction
                            await transaction.CommitAsync();
                            return Json(new { success = true, message = "Building and all related data successfully deleted" });
                        }
                        else
                        {
                            _logger.LogWarning("Building {0} not found for force deletion", id);
                            return Json(new { success = false, message = "Building not found" });
                        }
                    }
                    catch (Exception ex)
                    {
                        // Rollback transaction on error
                        await transaction.RollbackAsync();
                        _logger.LogError("Error in force delete: {0}", ex.Message);
                        return Json(new { success = false, message = "Error deleting building: " + ex.Message });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Critical error in ForceDeleteBuilding: {0}", ex.Message);
                return Json(new { success = false, message = "Critical error: " + ex.Message });
            }
        }

        public async Task<IActionResult> EditBuilding(int id)
        {
            var building = await _context.Buildings.FindAsync(id);
            if (building == null)
            {
                return NotFound();
            }
            return View(building);
        }

        [HttpPost]
        public async Task<IActionResult> EditBuilding(int id, Building building)
        {
            if (id != building.BuildingId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingBuilding = await _context.Buildings.AsNoTracking().FirstOrDefaultAsync(b => b.BuildingId == id);
                    if (existingBuilding == null)
                    {
                        return NotFound();
                    }

                    // Check if any changes were actually made
                    if (existingBuilding.BuildingName == building.BuildingName &&
                        existingBuilding.Address == building.Address &&
                        existingBuilding.DefaultMonthlyRent == building.DefaultMonthlyRent &&
                        existingBuilding.DefaultWaterFee == building.DefaultWaterFee &&
                        existingBuilding.DefaultElectricityFee == building.DefaultElectricityFee &&
                        existingBuilding.DefaultWifiFee == building.DefaultWifiFee &&
                        existingBuilding.LateFee == building.LateFee)
                    {
                        // No changes were made
                        TempData["Info"] = "No changes were made to the building.";
                        return RedirectToAction(nameof(ManageBuildings));
                    }

                    _context.Update(building);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Building updated successfully!";
                    return RedirectToAction(nameof(ManageBuildings));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Buildings.AnyAsync(b => b.BuildingId == id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        ModelState.AddModelError("", "Error updating building. Please try again.");
                    }
                }
            }
            return View(building);
        }

        public async Task<IActionResult> EditTenant(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var tenant = await _userManager.FindByIdAsync(id);
            if (tenant == null)
            {
                return NotFound();
            }

            var isTenant = await _userManager.IsInRoleAsync(tenant, "Tenant");
            if (!isTenant)
            {
                return NotFound();
            }

            return View(tenant);
        }

        [HttpPost]
        public async Task<IActionResult> EditTenant(string id, ApplicationUser tenant)
        {
            if (id != tenant.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingTenant = await _userManager.FindByIdAsync(id);
                    if (existingTenant == null)
                    {
                        return NotFound();
                    }
                    
                    var isTenant = await _userManager.IsInRoleAsync(existingTenant, "Tenant");
                    if (!isTenant)
                    {
                        return NotFound();
                    }

                    // Check if any changes were made
                    if (existingTenant.FirstName == tenant.FirstName &&
                        existingTenant.LastName == tenant.LastName &&
                        existingTenant.PhoneNumber == tenant.PhoneNumber &&
                        existingTenant.StartDate == tenant.StartDate)
                    {
                        TempData["Info"] = "No changes were made to the tenant.";
                        return RedirectToAction(nameof(ManageTenants));
                    }

                    // Update user properties
                    existingTenant.FirstName = tenant.FirstName;
                    existingTenant.LastName = tenant.LastName;
                    existingTenant.PhoneNumber = tenant.PhoneNumber;
                    existingTenant.StartDate = tenant.StartDate;
                    
                    _logger.LogInformation("Updating tenant {Id} with StartDate {StartDate}", 
                        existingTenant.Id, existingTenant.StartDate.ToString("yyyy-MM-dd"));

                    var result = await _userManager.UpdateAsync(existingTenant);
                    if (result.Succeeded)
                    {
                        TempData["Success"] = "Tenant updated successfully!";
                        return RedirectToAction(nameof(ManageTenants));
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating tenant: {ErrorMessage}", ex.Message);
                    ModelState.AddModelError("", "Error updating tenant. Please try again.");
                }
            }
            return View(tenant);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTenant(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return Json(new { success = false, message = "Invalid tenant ID." });
            }

            try
            {
                var tenant = await _userManager.FindByIdAsync(id);
                if (tenant == null)
                {
                    return Json(new { success = false, message = "Tenant not found." });
                }

                var isTenant = await _userManager.IsInRoleAsync(tenant, "Tenant");
                if (!isTenant)
                {
                    return Json(new { success = false, message = "User is not a tenant." });
                }

                // Delete bills
                var bills = await _context.Bills.Where(b => b.TenantId == id).ToListAsync();
                _context.Bills.RemoveRange(bills);
                
                // Delete contracts
                var contracts = await _context.Contracts.Where(c => c.TenantId == id).ToListAsync();
                _context.Contracts.RemoveRange(contracts);
                
                // Save changes
                await _context.SaveChangesAsync();

                // Update the room
                if (tenant.RoomId.HasValue)
                {
                    var room = await _context.Rooms.FindAsync(tenant.RoomId.Value);
                    if (room != null)
                    {
                        room.IsOccupied = false;
                        room.CurrentTenant = null;
                        _context.Update(room);
                        await _context.SaveChangesAsync();
                    }
                }

                // Delete the tenant
                var result = await _userManager.DeleteAsync(tenant);
                if (result.Succeeded)
                {
                    return Json(new { success = true });
                }

                return Json(new { success = false, message = "Error deleting tenant." });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error deleting tenant: " + ex.Message);
                return Json(new { success = false, message = "Error deleting tenant." });
            }
        }

        public async Task<IActionResult> BuildingDetails(int id)
        {
            try
            {
                var building = await _context.Buildings
                    .Include(b => b.Floors)
                        .ThenInclude(f => f.Rooms)
                    .FirstOrDefaultAsync(b => b.BuildingId == id);

                if (building == null)
                {
                    return NotFound();
                }

                return View(building);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading building details for BuildingId={0}: {1}", id, ex.Message);
                TempData["Error"] = "An error occurred while loading the building details.";
                return RedirectToAction(nameof(ManageBuildings));
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetFloors(int id)
        {
            _logger.LogInformation("GetFloors called with id={0}", id);

            var floors = await _context.Floors
                .Where(f => f.BuildingId == id)
                .OrderBy(f => f.FloorNumber)
                .Select(f => new {
                    FloorId = f.FloorId,
                    displayName = $"{f.FloorName} (Floor {f.FloorNumber})"
                })
                .ToListAsync();

            _logger.LogInformation("Found {0} floors for building {1}", floors.Count, id);

            return Json(new { success = true, data = floors });
        }

        [HttpPost]
        [Authorize(Roles = "Landlord")]
        public async Task<IActionResult> ResetDatabase()
        {
            try
            {
                _logger.LogWarning("Database reset initiated by landlord");
                
                // Use a transaction to ensure all-or-nothing behavior
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // Step 1: Clear meter readings
                        var meterReadings = await _context.MeterReadings.ToListAsync();
                        _context.MeterReadings.RemoveRange(meterReadings);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Cleared {0} meter readings", meterReadings.Count);
                        
                        // Step 2: Clear bills
                        var bills = await _context.Bills.ToListAsync();
                        _context.Bills.RemoveRange(bills);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Cleared {0} bills", bills.Count);
                        
                        // Step 3: Clear payments
                        var payments = await _context.Payments.ToListAsync();
                        _context.Payments.RemoveRange(payments);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Cleared {0} payments", payments.Count);
                        
                        // Step 4: Clear contracts
                        var contracts = await _context.Contracts.ToListAsync();
                        _context.Contracts.RemoveRange(contracts);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Cleared {0} contracts", contracts.Count);
                        
                        // Step 5: Clear tenant references from rooms and users
                        var rooms = await _context.Rooms.ToListAsync();
                        foreach (var room in rooms)
                        {
                            room.IsOccupied = false;
                            room.CurrentTenant = null;
                        }
                        _context.UpdateRange(rooms);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Cleared tenant references from {0} rooms", rooms.Count);
                        
                        // Step 6: Clear room and building references from users
                        var users = await _context.Users.ToListAsync();
                        foreach (var user in users)
                        {
                            user.RoomId = null;
                            user.BuildingId = null;
                        }
                        _context.UpdateRange(users);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Cleared room and building references from {0} users", users.Count);
                        
                        // Step 7: Delete all rooms
                        _context.Rooms.RemoveRange(rooms);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Deleted {0} rooms", rooms.Count);
                        
                        // Step 8: Delete all floors
                        var floors = await _context.Floors.ToListAsync();
                        _context.Floors.RemoveRange(floors);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Deleted {0} floors", floors.Count);
                        
                        // Step 9: Delete all buildings
                        var buildings = await _context.Buildings.ToListAsync();
                        _context.Buildings.RemoveRange(buildings);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Deleted {0} buildings", buildings.Count);
                        
                        // Step 10: Delete all tenant users
                        var tenantRoleId = (await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Tenant"))?.Id;
                        if (tenantRoleId != null)
                        {
                            var tenantUserIds = await _context.UserRoles
                                .Where(ur => ur.RoleId == tenantRoleId)
                                .Select(ur => ur.UserId)
                                .ToListAsync();
                            
                            foreach (var userId in tenantUserIds)
                            {
                                var user = await _userManager.FindByIdAsync(userId);
                                if (user != null)
                                {
                                    await _userManager.DeleteAsync(user);
                                }
                            }
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("Deleted {0} tenant users", tenantUserIds.Count);
                        }
                        
                        // Commit the transaction
                        await transaction.CommitAsync();
                        TempData["Success"] = "Database reset successfully. All buildings, floors, rooms, tenants, and related data have been removed.";
                        return RedirectToAction(nameof(Index));
                    }
                    catch (Exception ex)
                    {
                        // Rollback the transaction if there's an error
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error during database reset: {Message}", ex.Message);
                        TempData["Error"] = "An error occurred while resetting the database: " + ex.Message;
                        return RedirectToAction(nameof(Index));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during database reset: {Message}", ex.Message);
                TempData["Error"] = "A critical error occurred: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
