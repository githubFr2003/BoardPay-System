    using BoardPaySystem.Services;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.AspNetCore.Authentication.Cookies;

    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    builder.Services.AddControllersWithViews();

    builder.Services.AddDbContext<ApplicationDBContext>(options => {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        options.UseSqlServer(connectionString);
    }
    );

    // ****************************************
    // ***** ADD AUTHENTICATION SERVICES ******
    // ****************************************
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromMinutes(60); // Set cookie duration (e.g., 60 minutes)
            options.SlidingExpiration = true; // Renew cookie on user activity
            options.LoginPath = "/Home/Index"; // Redirect here if authentication is required
            options.LogoutPath = "/Home/Logout"; // Path for logout process
            options.AccessDeniedPath = "/Home/AccessDenied"; // Optional: Path if authorized but lacks role/policy permission
        });
    // ****************************************
    // ********** END ADDED SECTION ***********
    // ****************************************

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles(); // Serves files from wwwroot
    app.UseRouting();
    // ****************************************
    // ***** ADD AUTHENTICATION MIDDLEWARE ****
    // ***** ORDER MATTERS: Before AuthZ *****
    // ****************************************
    app.UseAuthentication(); // Determines *who* the user is (reads the cookie)
    // ****************************************
    // ********** END ADDED SECTION ***********
    // ****************************************

    app.UseAuthorization();

    app.MapStaticAssets();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
        .WithStaticAssets();


    app.Run();
