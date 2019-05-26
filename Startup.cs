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
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using static TslWebApp.Services.Module;

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
                options.AccessDeniedPath = "/Home/Error";
            });

            services.AddScoped<ISmsService, SmsService>();
            services.AddSingleton<IComService, ComHelper>();
            services.AddSingleton<IGammuService, GammuService>();
            services.AddSingleton<IGammuConfigService, GammuConfigService>();
            services.AddSingleton<IModuleService, ModuleService>();
            
            services.AddSession(opts => 
            {
                opts.Cookie.IsEssential = true;
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
            app.UseDefaultFiles();
            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseSession();

            applicationLifetime.ApplicationStopping.Register(OnShutdown);
            applicationLifetime.ApplicationStopped.Register(OnShutdowned);
            applicationLifetime.ApplicationStarted.Register(OnStarting);

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
            var moduleService = (ModuleService)serviceProvider.GetRequiredService(typeof(IModuleService));
            var modules = LoadModules().Result;

            moduleService.Init(modules);

            var smsService = (SmsService)serviceProvider.GetRequiredService(typeof(ISmsService));
            smsService.Init().Wait();
            this.serviceProvider = serviceProvider;

            CreateRoles(serviceProvider).Wait();
        }

        private void OnStarting()
        {
            //WipeGammuProcesses().Wait();
        }

        private void OnShutdowned()
        {
            Task.Factory.StartNew(() => WipeGammuProcesses());
        }

        public async Task WipeGammuProcesses()
        {
            var processList = Process.GetProcesses().ToList();

            await Task.Factory.StartNew(() =>
            {
                //TODO: Determine how to detect all processes needed to be killed.
                var gammuProcessList = processList.FindAll(proc => proc.ProcessName.Contains("gammu-") 
                                                        || proc.ProcessName.Contains("python"));
                gammuProcessList.ForEach(gprocess =>
                {
                    try
                    {
                        gprocess.Kill();
                    }
                    catch
                    { }
                });
            });
        }

        private void OnShutdown()
        {
            Task.Factory.StartNew(() => WipeGammuProcesses());               
        }

        private async Task<Stack<Module>> LoadModules()
        {
            return await Task.Factory.StartNew(() =>
            {
                var modulesPath = Configuration.GetSection("Modules")["ModuleConfigPath"];
                var json = File.ReadAllText(modulesPath);
                var modules = JsonConvert.DeserializeObject<Dictionary<string, List<Module>>>(json);

                var moduleStack = new Stack<Module>();

                modules.ToList().ForEach(pair => 
                {
                    pair.Value.ForEach(module => 
                    {
                        var types = typeof(ModuleType).GetEnumNames();
                        int index = 0;
                        foreach (var type in types)
                        {
                            if (type.Equals(pair.Key))
                            {
                                module.Type = (ModuleType)typeof(ModuleType).GetEnumValues().GetValue(index);
                                break;
                            }
                            index++;
                        }
                        
                        moduleStack.Push(module);
                    });
                });
                
                return moduleStack;
            });
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
