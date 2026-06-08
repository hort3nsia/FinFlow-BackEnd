using FinFlow.Application.Budgets.Services;
using FinFlow.Application.Chat.Interfaces;
using FinFlow.Application.Documents.Commands.RejectReviewedDocument;
using FinFlow.Domain.Abstractions;
using FinFlow.Domain.Documents;
using FinFlow.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinFlow.UnitTests.Application.Documents;

public sealed class RejectReviewedDocumentCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid DepartmentId = Guid.NewGuid();
    private static readonly Guid SubmitterMembershipId = Guid.NewGuid();
    private static readonly Guid ApproverMembershipId = Guid.NewGuid();

    [Fact]
    public async Task Handle_RemovesChunks_WhenDocumentRejected()
    {
        var doc = CreateReadyDoc();
        var (handler, indexer, _) = BuildHandler(doc);

        var result = await handler.Handle(
            new RejectReviewedDocumentCommand(doc.Id, TenantId, ApproverMembershipId, "Not valid"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Rejected", result.Value.Status);
        indexer.Verify(i => i.RemoveAsync(doc.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenChunkRemovalFails()
    {
        var doc = CreateReadyDoc();
        var (handler, indexer, _) = BuildHandler(doc);
        indexer.Setup(i => i.RemoveAsync(doc.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Vector store unavailable"));

        var result = await handler.Handle(
            new RejectReviewedDocumentCommand(doc.Id, TenantId, ApproverMembershipId, "Not valid"),
            CancellationToken.None);

        // Chunk removal is best-effort — reject still succeeds.
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenDocDoesNotExist()
    {
        var docRepo = new Mock<IReviewedDocumentRepository>();
        docRepo.Setup(r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReviewedDocument?)null);
        var handler = BuildHandler(docRepo, out var indexer);

        var result = await handler.Handle(
            new RejectReviewedDocumentCommand(Guid.NewGuid(), TenantId, ApproverMembershipId, "Not valid"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReviewedDocumentErrors.NotFound.Code, result.Error.Code);
        indexer.Verify(i => i.RemoveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DoesNotRemoveChunks_WhenRejectFails()
    {
        // Self-approval guard fires before the domain transition — no chunk removal.
        var doc = CreateReadyDoc();
        var (handler, indexer, _) = BuildHandler(doc);

        var result = await handler.Handle(
            new RejectReviewedDocumentCommand(doc.Id, TenantId, SubmitterMembershipId, "Not valid"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReviewedDocumentErrors.SelfApprovalNotAllowed.Code, result.Error.Code);
        indexer.Verify(i => i.RemoveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (RejectReviewedDocumentCommandHandler Handler, Mock<IReviewedDocumentChunkIndexer> Indexer, Mock<IUnitOfWork> Uow) BuildHandler(ReviewedDocument doc)
    {
        var docRepo = new Mock<IReviewedDocumentRepository>();
        docRepo.Setup(r => r.GetByIdForUpdateAsync(doc.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        var handler = BuildHandler(docRepo, out var indexer, out var uow);
        return (handler, indexer, uow);
    }

    private static RejectReviewedDocumentCommandHandler BuildHandler(
        Mock<IReviewedDocumentRepository> docRepo,
        out Mock<IReviewedDocumentChunkIndexer> indexer)
        => BuildHandler(docRepo, out indexer, out _);

    private static RejectReviewedDocumentCommandHandler BuildHandler(
        Mock<IReviewedDocumentRepository> docRepo,
        out Mock<IReviewedDocumentChunkIndexer> indexer,
        out Mock<IUnitOfWork> uow)
    {
        var budget = new Mock<IBudgetReservationService>();
        budget.Setup(b => b.ReleaseCommitmentAsync(It.IsAny<BudgetMovement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        indexer = new Mock<IReviewedDocumentChunkIndexer>();
        indexer.Setup(i => i.RemoveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        return new RejectReviewedDocumentCommandHandler(
            docRepo.Object,
            budget.Object,
            uow.Object,
            indexer.Object,
            NullLogger<RejectReviewedDocumentCommandHandler>.Instance);
    }

    private static ReviewedDocument CreateReadyDoc() =>
        ReviewedDocument.CreateSubmitted(
            Guid.NewGuid(), TenantId, DepartmentId, SubmitterMembershipId,
            "invoice.pdf", "application/pdf",
            "Vendor", "INV-1",
            new DateOnly(2026, 5, 1), "SaaS", null,
            200m, 0m, 200m,
            "staff-upload", "staff@test.com", "Staff corrected",
            DateTime.UtcNow,
            new[] { ReviewedDocumentLineItem.Create("Item", 1m, 200m, 200m) }).Value;
}
