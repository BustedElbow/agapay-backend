using agapay_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace agapay_backend.Data
{
    public class agapayDbContext(DbContextOptions<agapayDbContext> options) : DbContext (options)
    {
        public DbSet<User> Users { get; set; }
    }
}
