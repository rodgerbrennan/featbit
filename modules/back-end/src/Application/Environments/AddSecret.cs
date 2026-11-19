using Application.Bases;
using Domain.Environments;

namespace Application.Environments;

public class AddSecret : IRequest<Secret>
{
    public Guid EnvId { get; set; }

    public string Name { get; set; }

    public string Type { get; set; }

    public Secret AsSecret()
    {
        var secret = new Secret(EnvId, Name, Type);
        return secret;
    }
}

public class AddSecretValidator : AbstractValidator<AddSecret>
{
    public AddSecretValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(ErrorCodes.Required("name"));

        RuleFor(x => x.Type)
            .Must(SecretTypes.IsDefined).WithErrorCode(ErrorCodes.Invalid("type"));
    }
}

public class AddSecretHandler : IRequestHandler<AddSecret, Secret>
{
    private readonly IEnvironmentService _service;
    private readonly IPublisher _publisher;

    public AddSecretHandler(IEnvironmentService service, IPublisher publisher)
    {
        _service = service;
        _publisher = publisher;
    }

    public async Task<Secret> Handle(AddSecret request, CancellationToken cancellationToken)
    {
        var secret = request.AsSecret();
        await _service.AddSecretAsync(request.EnvId, secret);

        // publish on secret added notification, only once the secret is stored
        await _publisher.Publish(new OnSecretAdded(request.EnvId, secret), cancellationToken);

        return secret;
    }
}