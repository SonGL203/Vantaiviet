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
    public DbSet<ShipmentPhoto> ShipmentPhotos => Set<ShipmentPhoto>();
    public DbSet<ShipmentMessage> ShipmentMessages => Set<ShipmentMessage>();
    public DbSet<TripReview> TripReviews => Set<TripReview>();
    public DbSet<ShipmentIncident> ShipmentIncidents => Set<ShipmentIncident>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<SavedAddress> SavedAddresses => Set<SavedAddress>();
    public DbSet<ShipperBusinessProfile> ShipperBusinessProfiles => Set<ShipperBusinessProfile>();
    public DbSet<BrokerProfile> BrokerProfiles => Set<BrokerProfile>();
    public DbSet<VehicleAvailability> VehicleAvailabilities => Set<VehicleAvailability>();
    public DbSet<VehicleProposal> VehicleProposals => Set<VehicleProposal>();
    public DbSet<SavedDriver> SavedDrivers => Set<SavedDriver>();
    public DbSet<DriverGroup> DriverGroups => Set<DriverGroup>();
    public DbSet<DriverReport> DriverReports => Set<DriverReport>();
    public DbSet<BrokerRouteInterest> BrokerRouteInterests => Set<BrokerRouteInterest>();
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
        modelBuilder.HasSequence<long>("ShipmentCodes");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoreDbContext).Assembly);
    }
}
