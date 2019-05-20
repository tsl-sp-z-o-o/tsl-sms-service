using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TslWebApp.Models;

namespace TslWebApp.Data
{
    public class UserContext : IdentityDbContext<User>
    {
        public UserContext(DbContextOptions<UserContext> options) : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
        }

        public DbSet<TslWebApp.Models.UserModel> UserModel { get; set; }

        public DbSet<TslWebApp.Models.SmsMessage> SmsMessage { get; set; }

        public DbSet<TslWebApp.Models.EditMessageViewModel> EditMessageViewModel { get; set; }
    }
}
