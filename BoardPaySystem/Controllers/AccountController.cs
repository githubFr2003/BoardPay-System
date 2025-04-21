using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BoardPaySystem.Models;
using BoardPaySystem.Services;
using Microsoft.AspNetCore.Identity;

// Required for Cookie Authentication
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.Tasks; // Required for async actions

public class AccountController : Controller
{
    private readonly ApplicationDBContext _context;

    public AccountController(ApplicationDBContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Login()
    {
        // If user is already logged in, redirect them away from login page
        if (User.Identity.IsAuthenticated)
        {
            // Optional: Redirect based on existing role, or just to a default page
            if (User.IsInRole("Landlord")) return RedirectToAction("Overview", "Landlord");
            if (User.IsInRole("Tenant")) return RedirectToAction("Bills", "Tenant"); // Assuming Tenant controller exists
            return RedirectToAction("Index", "Home"); // Fallback
        }
        return View(new LoginViewModel
        {
            Username = string.Empty, // Initialize with default values
            Password = string.Empty,
            Role = string.Empty
        }); // Pass initialized model to avoid null reference on initial load
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Make the method async Task<IActionResult> to use await
    public async Task<IActionResult> Login(LoginViewModel model) // Role from dropdown is now in the model
    {
        // Check if the model state is valid (based on annotations like [Required])
        if (!ModelState.IsValid)
        {
            // If not valid, return the view with the model to display validation errors
            return View(model);
        }

        // --- Find User ---
        // Include the Role navigation property to check the role name
        var user = await _context.users
            .Include(u => u.Role) // Eager load the Role data
            .FirstOrDefaultAsync(u => u.username == model.Username);

        // --- Validate Credentials ---
        // 1. Check if user exists
        // 2. !!! SECURITY WARNING: PLAIN TEXT PASSWORD CHECK - REPLACE WITH HASHING ASAP !!!
        // 3. Check if the stored Role name matches the selected Role (case-insensitive)
        if (user != null && user.password == model.Password && user.Role != null && user.Role.Name.Equals(model.Role, StringComparison.OrdinalIgnoreCase))
        {
            // --- Credentials Valid - Create Authentication Cookie ---

            // Create claims (pieces of information about the user)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.username), // Standard claim for username
                new Claim(ClaimTypes.NameIdentifier, user.userID.ToString()), // Standard claim for user ID
                new Claim(ClaimTypes.Role, user.Role.Name) // Standard claim for role
                // Add other claims if needed (e.g., email, first name)
            };

            // Create identity based on claims
            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            // Create principal (represents the user)
            var authPrincipal = new ClaimsPrincipal(claimsIdentity);

            // Define authentication properties (optional, e.g., for persistence)
            var authProperties = new AuthenticationProperties
            {
                // Allow the session to be persisted across browser closes (optional)
                // IsPersistent = true,

                // Redirect URL after successful login (can be overridden below)
                // RedirectUri = <string>

                // ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(60) // Can override scheme default
            };

            // Sign the user in, creating the authentication cookie
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                authPrincipal,
                authProperties);

            // --- Redirect based on Role ---
            if (user.Role.Name.Equals("Landlord", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Overview", "Landlord");
            }
            else if (user.Role.Name.Equals("Tenant", StringComparison.OrdinalIgnoreCase))
            {
                // Assuming you have a Tenant controller and Bills action
                return RedirectToAction("Bills", "Tenant");
            }
            else
            {
                // Fallback redirect if role is neither Landlord nor Tenant (or handle error)
                return RedirectToAction("Index", "Home");
            }
        }
        else
        {
            // --- Credentials Invalid ---
            // Add a model error to display a general message on the login page
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            ViewBag.LoginError = "Invalid username, password, or role."; // Or use ViewBag as before
            return View(model); // Return the view with the model and error message
        }
    }

    // --- Add Logout Action ---
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // Clear the existing external cookie
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction("Index", "Home"); // Redirect to home/login page after logout
    }

    // --- Optional: Access Denied Action ---
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View(); // Create a simple AccessDenied.cshtml view if needed
    }
}

