using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Services;

namespace FriendlyPMC.Server.Services;

public interface IFollowerChatMessageSender
{
    void SendUserMessage(MongoId sessionId, UserDialogInfo senderDetails, string message);
}

[Injectable(InjectionType.Singleton)]
public sealed class FollowerChatMessageSender(MailSendService mailSendService) : IFollowerChatMessageSender
{
    public void SendUserMessage(MongoId sessionId, UserDialogInfo senderDetails, string message)
    {
        mailSendService.SendUserMessageToPlayer(sessionId, senderDetails, message, null, null);
    }
}
