using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;
namespace VantaiViet.CoreApi.Data.Configurations;
public sealed class OtpDeliveryConfiguration : IEntityTypeConfiguration<OtpDelivery>
{
    public void Configure(EntityTypeBuilder<OtpDelivery> b)
    {
        b.ToTable("OtpDeliveries", "public", t => {
            t.HasCheckConstraint("CK_OtpDelivery_Channel", "\"Channel\" IN ('Email','Phone')");
            t.HasCheckConstraint("CK_OtpDelivery_Purpose", "\"Purpose\" IN ('Verification','PasswordReset')");
            t.HasCheckConstraint("CK_OtpDelivery_Attempts", "\"SendAttempts\" BETWEEN 0 AND 3 AND \"VerifyAttempts\" BETWEEN 0 AND 5");
        });
        b.HasKey(x=>x.Id);
        b.HasIndex(x=>new{x.UserId,x.RequestKey}).IsUnique();
        b.HasIndex(x=>new{x.State,x.NextAttemptAt});
        b.HasIndex(x=>x.ExpiresAt);
        b.HasIndex(x=>x.CreatedAt);
        b.HasIndex(x=>x.LastAttemptAt);
        b.HasIndex(x=>new{x.UserId,x.CreatedAt});
        b.HasIndex(x=>new{x.Destination,x.CreatedAt});
        b.Property(x=>x.Channel).HasMaxLength(10);
        b.Property(x=>x.Purpose).HasMaxLength(20).HasDefaultValue("Verification");
        b.Property(x=>x.CredentialStamp).HasMaxLength(256);
        b.Property(x=>x.Destination).HasMaxLength(256);
        b.Property(x=>x.State).HasMaxLength(20);
        b.HasOne<User>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
