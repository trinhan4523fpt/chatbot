using Chatbot.Application.Common.Behaviors;
using Chatbot.Application.Features.Chat;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Chatbot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddAutoMapper(_ => { }, assembly);
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IRagChatService, RagChatService>();

        return services;
    }
}
