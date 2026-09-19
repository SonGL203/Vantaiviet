using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data;

public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RegistrationApplication> RegistrationApplications => Set<RegistrationApplication>();
    public DbSet<RegistrationSession> RegistrationSessions => Set<RegistrationSession>();
    public DbSet<VerificationChallenge> VerificationChallenges => Set<VerificationChallenge>();
    public DbSet<KycApplication> KycApplications => Set<KycApplication>();
    public DbSet<KycIdentityDetail> KycIdentityDetails => Set<KycIdentityDetail>();
    public DbSet<KycDocument> KycDocuments => Set<KycDocument>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<DriverProfile> DriverProfiles => Set<DriverProfile>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<TransportRequest> TransportRequests => Set<TransportRequest>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripEvent> TripEvents => Set<TripEvent>();
    public DbSet<TripProof> TripProofs => Set<TripProof>();
    public DbSet<TripLocation> TripLocations => Set<TripLocation>();
    public DbSet<TripParticipant> TripParticipants => Set<TripParticipant>();
    public DbSet<OtpDelivery> OtpDeliveries => Set<OtpDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoreDbContext).Assembly);
    }
}
