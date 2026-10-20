using Chat.Data;
using Chat.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace Chat.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IDbContextFactory<ChatDbContext> _contextFactory;

        // Потокобезопасный словарь: connectionId -> userName
        private static readonly ConcurrentDictionary<string, string> _onlineUsers = new();

        public ChatHub(IDbContextFactory<ChatDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public override async Task OnConnectedAsync()
        {
            // Имя берём из query-строки (?user=...), которую клиент передаёт в withUrl
            var userName = Context.GetHttpContext()?.Request.Query["user"].ToString();
            if (string.IsNullOrWhiteSpace(userName))
                userName = $"Guest-{Context.ConnectionId[..6]}";

            _onlineUsers[Context.ConnectionId] = userName;

            // Комната по умолчанию
            await Groups.AddToGroupAsync(Context.ConnectionId, "general");

            Console.WriteLine($"[CONNECT] {userName} ({Context.ConnectionId}) joined general");

            // Рассылаем всем актуальный список онлайн
            await BroadcastOnlineUsers();

            // Остальным — уведомление о новом участнике
            await Clients.Others.SendAsync("UserJoined", userName);

            var history = await GetHistory("general", 50);
            await Clients.Caller.SendAsync("HistoryLoaded", "general", history);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_onlineUsers.TryRemove(Context.ConnectionId, out var userName))
            {
                Console.WriteLine($"[DISCONNECT] {userName} ({Context.ConnectionId})");
                await Clients.Others.SendAsync("UserLeft", userName);
            }

            await BroadcastOnlineUsers();
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessageToRoom(ChatMessage message)
        {
            message.User = GetUserName();
            message.SentAt = DateTime.UtcNow;

            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Messages.Add(message);
            await context.SaveChangesAsync();

            Console.WriteLine($"[SEND] Room={message.Room}, User={message.User}, Text={message.Text}");

            await Clients.Group(message.Room)
                         .SendAsync("ReceiveMessage", message);
        }

        public async Task JoinRoom(string room)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, room);
            await Clients.Caller.SendAsync("RoomJoined", room);
            Console.WriteLine($"[JOIN] {GetUserName()} -> {room}");
        }

        public async Task LeaveRoom(string room)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, room);
            Console.WriteLine($"[LEAVE] {GetUserName()} <- {room}");
        }

        public async Task Typing(string room)
        {
            await Clients.OthersInGroup(room)
                         .SendAsync("UserTyping", GetUserName(), room);
        }

        public async Task<List<ChatMessage>> GetHistory(string room, int limit = 50)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Messages
                .Where(m => m.Room == room)
                .OrderByDescending(m => m.SentAt)
                .Take(limit)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }

        private string GetUserName() =>
            _onlineUsers.TryGetValue(Context.ConnectionId, out var name) ? name : "Unknown";

        private async Task BroadcastOnlineUsers()
        {
            var users = _onlineUsers.Values.ToList();
            Console.WriteLine($"[ONLINE] {users.Count}: {string.Join(", ", users)}");
            await Clients.All.SendAsync("OnlineUsers", users);
        }
    }
}