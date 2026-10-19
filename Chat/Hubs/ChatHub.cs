using Chat.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Chat.Hubs
{
    public class ChatHub : Hub
    {
        // Потокобезопасный счётчик онлайн-пользователей.
        // ConcurrentDictionary — потому что Hub по умолчанию создаётся на каждый вызов,
        // а соединения приходят параллельно.
        private static readonly ConcurrentDictionary<string, string> _onlineUsers = new();

        public override async Task OnConnectedAsync()
        {
            var userName = Context.GetHttpContext()?.Request.Query["user"].ToString();
            if (string.IsNullOrWhiteSpace(userName))
                userName = $"Guest-{Context.ConnectionId[..6]}";

            _onlineUsers[Context.ConnectionId] = userName;

            // По умолчанию все попадают в комнату general
            await Groups.AddToGroupAsync(Context.ConnectionId, "general");

            // Всем — обновлённый счётчик и список онлайн
            await BroadcastOnlineUsers();

            // Остальным — уведомление о новом участнике
            await Clients.Others.SendAsync("UserJoined", userName);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_onlineUsers.TryRemove(Context.ConnectionId, out var userName))
            {
                await Clients.Others.SendAsync("UserLeft", userName);
            }
            await BroadcastOnlineUsers();
            await base.OnDisconnectedAsync(exception);
        }

        // --- Комнаты ---

        public async Task JoinRoom(string room)
        {
            // Покидаем все текущие комнаты, кроме general (по желанию)
            await Groups.AddToGroupAsync(Context.ConnectionId, room);
            await Clients.Caller.SendAsync("RoomJoined", room);
            await Clients.Group(room).SendAsync("SystemMessage", 
                $"{GetUserName()} присоединился к комнате {room}");
        }

        public async Task LeaveRoom(string room)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, room);
            await Clients.Group(room).SendAsync("SystemMessage", 
                $"{GetUserName()} покинул комнату {room}");
        }

        // --- Сообщения ---

        public async Task SendMessageToRoom(ChatMessage message)
        {
            message.User = GetUserName();
            message.SentAt = DateTime.UtcNow;

            // Отправляем всем в комнате (включая отправителя — для синхронизации ID/времени)
            await Clients.Group(message.Room)
                         .SendAsync("ReceiveMessage", message);
        }

        // «X печатает...» — только остальным в комнате
        public async Task Typing(string room)
        {
            await Clients.OthersInGroup(room)
                         .SendAsync("UserTyping", GetUserName(), room);
        }

        // --- Личное сообщение ---

        public async Task SendPrivateMessage(string targetConnectionId, string text)
        {
            await Clients.Client(targetConnectionId).SendAsync("PrivateMessage",
                GetUserName(), text, DateTime.UtcNow);
        }

        // --- Вспомогательные ---

        private string GetUserName() =>
            _onlineUsers.TryGetValue(Context.ConnectionId, out var name) ? name : "Unknown";

        private async Task BroadcastOnlineUsers()
        {
            var users = _onlineUsers.Values.ToList();
            await Clients.All.SendAsync("OnlineUsers", users);
        }
    }
}