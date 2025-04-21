using Microsoft.EntityFrameworkCore;
using BoardPaySystem.Models;

namespace BoardPaySystem.Services
{
    public class ApplicationDBContext : DbContext
    {
        public ApplicationDBContext(DbContextOptions options) : base(options)
        {

        }
        public DbSet<Users> users { get; set; }
        public DbSet<Role> Roles { get; set; }

       
    }
}
