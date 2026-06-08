using FinFlow.Domain.Documents;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure;
using FinFlow.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.UnitTests.Infrastructure.Repositories;

public sealed class ReviewedDocumentRepositoryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid DepartmentId = Guid.NewGuid();
    private static readonly Guid SubmitterId = Guid.NewGuid();
    private static readonly Guid ApproverId = Guid.NewGuid();

    [Fact]
    public async Task GetAllActiveByTenantAsync_ExcludesRejectedAndDraftDocuments()
    {
        await using var db = CreateDbContext();

        var ready = CreateDoc();

        var approved = CreateDoc();
        approved.Approve(ApproverId);

        var rejected = CreateDoc();
        rejected.Reject("invalid receipt", ApproverId);

        var withdrawnToDraft = CreateDoc();
        withdrawnToDraft.Withdraw();

        db.Set<ReviewedDocument>().AddRange(ready, approved, rejected, withdrawnToDraft);
        await db.SaveChangesAsync();

        var repo = new ReviewedDocumentRepository(db);
        var result = await repo.GetAllActiveByTenantAsync(TenantId);

        var ids = result.Select(d => d.Id).ToHashSet();
        Assert.Contains(ready.Id, ids);
        Assert.Contains(approved.Id, ids);
        Assert.DoesNotContain(rejected.Id, ids);
        Assert.DoesNotContain(withdrawnToDraft.Id, ids);
    }

    private static ReviewedDocument CreateDoc() =>
        ReviewedDocument.CreateSubmitted(
            Guid.NewGuid(), TenantId, DepartmentId, SubmitterId,
            "invoice.pdf", "application/pdf",
            "Vendor", "INV-1",
            new DateOnly(2026, 5, 1), "SaaS", null,
            200m, 0m, 200m,
            "staff-upload", "staff@test.com", "Staff corrected",
            DateTime.UtcNow,
            new[] { ReviewedDocumentLineItem.Create("Item", 1m, 200m, 200m) }).Value;

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new TestCurrentTenant
        {
            Id = TenantId,
            MembershipId = SubmitterId
        });
    }

    private sealed class TestCurrentTenant : ICurrentTenant
    {
        public Guid? Id { get; set; }
        public Guid? MembershipId { get; set; }
        public bool IsSuperAdmin { get; set; }
        public bool IsAvailable => Id.HasValue;

        public IDisposable BeginScope(Guid? tenantId, Guid? membershipId = null, bool isSuperAdmin = false)
            => NoOpDisposable.Instance;
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public static readonly NoOpDisposable Instance = new();
        public void Dispose() { }
    }
}
