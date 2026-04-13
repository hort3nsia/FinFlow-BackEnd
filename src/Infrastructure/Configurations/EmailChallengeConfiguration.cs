using FinFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinFlow.Infrastructure.Configurations;

internal sealed class EmailChallengeConfiguration : IEntityTypeConfiguration<EmailChallenge>
{
    public void Configure(EntityTypeBuilder<EmailChallenge> builder)
    {
        builder.ToTable("email_challenge");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(100).IsRequired();
        builder.Property(x => x.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(x => x.Purpose).HasColumnName("purpose").IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(500).IsRequired();
        builder.Property(x => x.OtpHash).HasColumnName("otp_hash").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.LastSentAt).HasColumnName("last_sent_at");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(x => x.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.MaxOtpAttempts).HasColumnName("max_otp_attempts").HasDefaultValue(5).IsRequired();
        builder.Property(x => x.OtpAttemptCount).HasColumnName("otp_attempt_count").HasDefaultValue(0).IsRequired();

        builder.HasIndex(x => x.Email);
        builder.HasIndex(x => x.AccountId);
        builder.HasIndex(x => new { x.AccountId, x.Purpose });
        builder.HasIndex(x => x.TokenHash).IsUnique();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
