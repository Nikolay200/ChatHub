using Microsoft.AspNetCore.SignalR;

namespace Chat.Hubs
{
    public class ChatHub : Hub
    {
        // Метод, который будут вызывать клиенты
        public async Task SendMessage(string user, string message)
        {
            // Отправляем сообщение всем подключенным клиентам
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
    }
}