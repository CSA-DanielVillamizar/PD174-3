using Microsoft.EntityFrameworkCore;
using Order.Api.Models;

namespace Order.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<OrderEntity> Orders { get; set; }
}