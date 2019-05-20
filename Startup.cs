using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using TslWebApp.Data;
using System.Threading.Tasks;
using System;
using TslWebApp.Models;
using TslWebApp.Services;
using TslWebApp.Hubs;
using TslWebApp.Utils;
using System.Diagnostics;
using System.Linq;

namespace TslWebApp
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        public IServiceProvider serviceProvider;

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<UserContext>(options =>
                options.UseNpgsql(Configuration.GetConnectionString("UserConnection")));
            services.AddDbContext<MainDbContext>(options => options.UseNpgsql(Configuration.GetConnectionString("MainConnection")));
            services.AddIdentity<User, IdentityRole>()
                .AddEntityFrameworkStores<UserContext>()
                .AddDefaultTokenProviders();

            services.Configure<CookiePolicyOptions>(options =>
            {
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                options.SlidingExpiration = true;
                options.LoginPath = "/User/Login";
            });

            services.AddScoped<ISmsService, SmsService>();
            services.AddSingleton<IComService, ComHelper>();
            services.AddSingleton<IGammuService, GammuService>();
            services.AddSingleton<IGammuConfigService, GammuConfigService>();
            
            services.AddSession(opts => 
            {
                opts.IdleTimeout = TimeSpan.FromMinutes(5);
            });

            
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
            services.AddSignalR(sr =>
            {
                sr.EnableDetailedErrors = true;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, 
            IHostingEnvironment env, 
            IServiceProvider serviceProvider,
            IApplicationLifetime applicationLifetime)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseSession();
            //app.UseCors("CorsPolicy");

            applicationLifetime.ApplicationStopping.Register(OnShutdown);
            applicationLifetime.ApplicationStopped.Register(OnShutdowned);

            app.UseSignalR(route =>
            {
                route.MapHub<SmsHub>("/smshub");
                
            });

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
            var smsService = (SmsService)serviceProvider.GetRequiredService(typeof(ISmsService));
            smsService.Init().Wait();
            this.serviceProvider = serviceProvider;
            
            CreateRoles(serviceProvider).Wait();
        }

        private void OnShutdowned()
        {
            Task.Factory.StartNew(() => WipeGammuProcesses());
        }

        public async Task WipeGammuProcesses()
        {
            await Task.Factory.StartNew(() =>
            {
                var processList = Process.GetProcesses().ToList();
                var gammuProcessList = processList.FindAll(proc => proc.ProcessName.Contains("gammu"));
                gammuProcessList.ForEach(gprocess =>
                {
                    gprocess.Kill();
                });
            });
        }

        private void OnShutdown()
        {
            Task.Factory.StartNew(() => WipeGammuProcesses());               
        }

        private async Task CreateRoles(IServiceProvider serviceProvider)
        {
            //adding custom roles
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
            string[] roleNames = { "Admin", "Manager", "User" };

            foreach (var roleName in roleNames)
            {
                //creating the roles and seeding them to the database
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            //creating a super user who could maintain the web app
            var poweruser = new User
            {
                UserName = Configuration.GetSection("UserSettings")["UserEmail"],
                Email = Configuration.GetSection("UserSettings")["UserEmail"]
            };

            var userPassword = Configuration.GetSection("UserSettings")["UserPassword"];
            var user = await userManager.FindByEmailAsync(Configuration.GetSection("UserSettings")["UserEmail"]);

            //Debug.WriteLine(_user.Email);
            if (user == null)
            {
                var createPowerUser = await userManager.CreateAsync(poweruser, userPassword);
                if (createPowerUser.Succeeded)
                {
                    //here we tie the new user to the "Admin" role 
                    await userManager.AddToRoleAsync(poweruser, "Admin");
                }
            }
        }
    }
}
