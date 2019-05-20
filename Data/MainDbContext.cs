using Microsoft.EntityFrameworkCore;
using TslWebApp.Models;

namespace TslWebApp.Data
{
    public class MainDbContext : DbContext
    {
        public MainDbContext(DbContextOptions<MainDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
        }

        public virtual DbSet<SmsMessage> Messages { get; set; }
    }
}
