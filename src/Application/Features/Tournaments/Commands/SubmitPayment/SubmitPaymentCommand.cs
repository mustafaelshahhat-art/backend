using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Commands.SubmitPayment;

public record SubmitPaymentCommand(Guid TournamentId, Guid TeamId, Guid UserId, string PaymentReceiptUrl, string SenderNumber, string PaymentMethod) : IRequest<TeamRegistrationDto>;
