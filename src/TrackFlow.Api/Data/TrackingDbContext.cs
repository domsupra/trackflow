using Microsoft.EntityFrameworkCore;

namespace TrackFlow.Api.Data;

public class TrackingDbContext : DbContext
{
    public TrackingDbContext(DbContextOptions<TrackingDbContext> options) : base(options) { }
}
